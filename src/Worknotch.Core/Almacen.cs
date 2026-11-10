using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Worknotch.Core;

/// <summary>
/// Guarda todo en un único archivo JSON. Cada cambio es leer, modificar y escribir con un cerrojo
/// entre procesos, porque la app y el servidor de Claude (worknotch-mcp) tocan el mismo archivo.
/// Se escribe en un temporal y se renombra, para que un corte no deje el archivo a medias.
/// </summary>
public sealed class Store
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const int MaxUndo = 30;
    private readonly string _lockPath;
    private readonly List<string> _undo = [];
    private string? _lastWritten;

    public string Path { get; }

    /// <summary>Si el archivo guardado no se pudo leer, dónde se ha dejado la copia.</summary>
    public string? PreservedAt { get; private set; }

    public Store(string path)
    {
        Path = path;
        _lockPath = path + ".lock";
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
    }

    /// <summary>La carpeta de datos: %APPDATA%\Worknotch, o la que diga WORKNOTCH_DATOS (pruebas).</summary>
    public static string DefaultFolder()
    {
        string? forced = Environment.GetEnvironmentVariable("WORKNOTCH_DATOS");
        if (!string.IsNullOrEmpty(forced)) return forced;
        return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Worknotch");
    }

    public static Store Default() => new(System.IO.Path.Combine(DefaultFolder(), "datos.json"));

    public bool CanUndo => _undo.Count > 0;

    public AppData Read()
    {
        using var _ = Lock();
        return Load();
    }

    /// <summary>Lee, aplica el cambio y guarda. Si el cambio lanza una excepción, no se guarda nada.</summary>
    public T Mutate<T>(Func<AppData, T> change, bool undoable = false)
    {
        using var _ = Lock();
        string before = File.Exists(Path) ? File.ReadAllText(Path) : "";
        var data = Load(before);
        T result = change(data);
        string after = JsonSerializer.Serialize(data, Json);
        if (after == before) return result;
        if (undoable && before.Length > 0)
        {
            _undo.Add(before);
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        }
        Write(after);
        return result;
    }

    public void Mutate(Action<AppData> change, bool undoable = false) =>
        Mutate(d => { change(d); return 0; }, undoable);

    /// <summary>
    /// Deshace el último cambio que se podía deshacer. El temporizador y los ajustes se quedan como
    /// están: deshacer devuelve tareas, notas y recortes, no el tiempo.
    /// </summary>
    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        using var _ = Lock();
        var current = Load();
        string json = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        var old = JsonSerializer.Deserialize<AppData>(json, Json) ?? new AppData();
        old.Timer = current.Timer;
        old.Settings = current.Settings;
        old.Sessions = current.Sessions;
        old.LastDay = current.LastDay;
        Write(JsonSerializer.Serialize(old, Json));
        return true;
    }

    /// <summary>True si lo que hay en disco no es lo último que escribió este proceso (lo ha cambiado otro).</summary>
    public bool ChangedOutside()
    {
        try
        {
            if (!File.Exists(Path)) return false;
            return File.ReadAllText(Path) != _lastWritten;
        }
        catch (IOException) { return false; }
    }

    private AppData Load() => Load(File.Exists(Path) ? File.ReadAllText(Path) : "");

    private AppData Load(string json)
    {
        _lastWritten = json;
        if (string.IsNullOrWhiteSpace(json)) return new AppData();
        try
        {
            return Normalize(JsonSerializer.Deserialize<AppData>(json, Json) ?? new AppData());
        }
        catch (JsonException)
        {
            // No se pisa lo que no se entiende: se aparta con la fecha y se empieza de cero.
            string copy = Path + ".dañado-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(Path, copy, overwrite: true);
            PreservedAt = copy;
            return new AppData();
        }
    }

    private static AppData Normalize(AppData d)
    {
        d.Tasks ??= [];
        d.Projects ??= [];
        d.Notepad ??= "";
        d.Sessions ??= [];
        d.Timer ??= new TimerState();
        d.Clips ??= [];
        d.Settings ??= new Settings();
        d.Settings.Cards ??= [];
        d.Settings.Cards = d.Settings.Cards.Select(c => c == "semana" ? "progreso" : c).ToList(); // la tarjeta cambió de nombre
        if (d.Settings.Cards.Count == 0) d.Settings.Cards.Add("tareas");
        foreach (var t in d.Tasks) { t.Steps ??= []; t.Notes ??= ""; }
        return d;
    }

    private void Write(string json)
    {
        string tmp = Path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, Path, overwrite: true);
        _lastWritten = json;
    }

    // Un archivo abierto en exclusiva hace de cerrojo entre procesos (y entre hilos de este).
    private FileStream Lock()
    {
        for (int i = 0; ; i++)
        {
            try
            {
                return new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.None);
            }
            catch (IOException) when (i < 200)
            {
                Thread.Sleep(15);
            }
        }
    }
}
