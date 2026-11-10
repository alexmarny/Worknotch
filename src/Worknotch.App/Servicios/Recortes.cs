using System.Windows;
using System.Windows.Media.Imaging;

namespace Worknotch.App.Servicios;

/// <summary>
/// Cuelga en Recortes lo que se copia y las capturas nuevas. Solo lee: nunca cambia los archivos ni
/// lo copiado. Lo que un gestor de contraseñas marca como privado no se guarda.
/// </summary>
internal sealed class ClipCatcher(Store store, Action changed) : IDisposable
{
    public const int Max = 40;
    private const int MaxText = 4000;
    private readonly List<FileSystemWatcher> _watchers = [];
    private string? _ownCopy;

    public static string ImagesFolder => Path.Combine(Store.DefaultFolder(), "recortes");

    /// <summary>Vigila la carpeta de capturas de Windows y la de grabaciones (Win+Alt+R).</summary>
    public void WatchScreenshots()
    {
        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        string videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        foreach (var (folder, filter) in new[] { (Path.Combine(pictures, "Screenshots"), "*.png"), (Path.Combine(pictures, "Capturas de pantalla"), "*.png"), (Path.Combine(videos, "Captures"), "*.mp4") })
        {
            if (!Directory.Exists(folder)) continue;
            try
            {
                var w = new FileSystemWatcher(folder, filter) { EnableRaisingEvents = true };
                w.Created += (_, e) => Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    if (store.Read().Settings.CatchScreenshots) Add(e.FullPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? ClipKind.Image : ClipKind.File, e.FullPath);
                });
                _watchers.Add(w);
            }
            catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>El portapapeles ha cambiado.</summary>
    public void ClipboardChanged()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data == null) return;
            // Los gestores de contraseñas piden así que no se guarde lo copiado
            if (data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing") || data.GetDataPresent("Clipboard Viewer Ignore")) return;

            if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (string f in files.Take(5)) Add(KindOfFile(f), f);
            }
            else if (data.GetDataPresent(DataFormats.UnicodeText) && data.GetData(DataFormats.UnicodeText) is string text)
            {
                text = text.Trim();
                if (text.Length == 0 || text.Length > MaxText || text == _ownCopy) return;
                Add(IsLink(text) ? ClipKind.Link : ClipKind.Text, text);
            }
            else if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } image)
            {
                Directory.CreateDirectory(ImagesFolder);
                string path = Path.Combine(ImagesFolder, $"copiado-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (var file = File.Create(path)) encoder.Save(file);
                Add(ClipKind.Image, path);
            }
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.ExternalException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            // El portapapeles lo tiene otro programa, o la imagen no se puede leer: no se cuelga nada
        }
    }

    public static ClipKind KindOfFile(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" ? ClipKind.Image : ClipKind.File;

    public static bool IsLink(string text) =>
        !text.Contains(' ') && !text.Contains('\n') && Uri.TryCreate(text, UriKind.Absolute, out var u) && u.Scheme is "http" or "https";

    public void Add(ClipKind kind, string content)
    {
        bool added = store.Mutate(d =>
        {
            if (d.Clips.Count > 0 && d.Clips[0].Content == content) return false;
            d.Clips.RemoveAll(c => c.Content == content);
            d.Clips.Insert(0, new Clip { Id = Planner.NewId(d), Kind = kind, Content = content, Added = DateTime.Now });
            foreach (var old in d.Clips.Skip(Max).ToList()) Forget(d, old);
            return true;
        });
        if (added) changed();
    }

    public void Remove(string id)
    {
        store.Mutate(d => { if (d.Clips.FirstOrDefault(c => c.Id == id) is { } c) Forget(d, c); }, undoable: true);
        changed();
    }

    // Las imágenes copiadas son nuestras y se borran con su recorte; las capturas y los archivos, no.
    private static void Forget(AppData d, Clip clip)
    {
        d.Clips.Remove(clip);
        if (clip.Kind == ClipKind.Image && clip.Content.StartsWith(ImagesFolder, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(clip.Content); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Vuelve a copiar un recorte, sin que eso lo cuelgue otra vez.</summary>
    public void Copy(Clip clip)
    {
        try
        {
            switch (clip.Kind)
            {
                case ClipKind.Text or ClipKind.Link:
                    _ownCopy = clip.Content;
                    Clipboard.SetText(clip.Content);
                    break;
                default:
                    if (!File.Exists(clip.Content)) return;
                    var files = new System.Collections.Specialized.StringCollection { clip.Content };
                    var data = new DataObject();
                    data.SetFileDropList(files);
                    if (clip.Kind == ClipKind.Image) data.SetImage(new BitmapImage(new Uri(clip.Content)));
                    Clipboard.SetDataObject(data, copy: true);
                    break;
            }
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.ExternalException or IOException or NotSupportedException) { }
    }

    public void Dispose()
    {
        foreach (var w in _watchers) w.Dispose();
    }
}
