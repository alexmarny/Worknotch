namespace Worknotch.Tests;

/// <summary>Un reloj que se pone a mano.</summary>
public sealed class FakeClock(DateTime now) : IClock
{
    public DateTime Now { get; set; } = now;
}

/// <summary>Una carpeta de datos temporal que se borra al acabar la prueba.</summary>
public sealed class TempStore : IDisposable
{
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "worknotch-pruebas-" + Guid.NewGuid().ToString("N"));
    public Store Store { get; }

    public TempStore() => Store = new Store(Path.Combine(Folder, "datos.json"));

    public void Dispose()
    {
        try { Directory.Delete(Folder, recursive: true); } catch (IOException) { }
    }
}

public static class Dates
{
    // Un miércoles
    public static readonly DateTime Now = new(2026, 11, 11, 10, 0, 0);
    public static readonly DateOnly Today = DateOnly.FromDateTime(Now);
}
