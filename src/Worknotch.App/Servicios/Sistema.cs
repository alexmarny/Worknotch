using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using Microsoft.Win32;

namespace Worknotch.App.Servicios;

/// <summary>Arranque con Windows: una entrada en Run del usuario, sin permisos de administrador.</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "Worknotch";

    public static bool Enabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(Name) is string;
            }
            catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException) { return false; }
        }
        set
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (value) key.SetValue(Name, $"\"{Environment.ProcessPath}\" --inicio");
                else key.DeleteValue(Name, throwOnMissingValue: false);
            }
            catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException) { }
        }
    }
}

/// <summary>
/// Una sola Worknotch por usuario. La segunda que se abre (por ejemplo, con un enlace worknotch://)
/// le pasa su argumento a la primera por una tubería con nombre y se cierra.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private static readonly string Id = "Worknotch." + Environment.UserName;
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();

    public bool IsFirst { get; }

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, Id, out bool created);
        IsFirst = created;
    }

    public static void SendToFirst(string message)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", Id, PipeDirection.Out);
            pipe.Connect(2000);
            using var writer = new StreamWriter(pipe);
            writer.WriteLine(message);
        }
        catch (Exception e) when (e is IOException or TimeoutException or UnauthorizedAccessException) { }
    }

    /// <summary>Escucha lo que mandan las siguientes instancias. <paramref name="received"/> llega en otro hilo.</summary>
    public void Listen(Action<string> received)
    {
        _ = Task.Run(async () =>
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    await using var pipe = new NamedPipeServerStream(Id, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(_stop.Token);
                    using var reader = new StreamReader(pipe);
                    string? line = await reader.ReadLineAsync(_stop.Token);
                    if (line != null) received(line);
                }
                catch (OperationCanceledException) { return; }
                catch (IOException) { await Task.Delay(200); }
            }
        });
    }

    public void Dispose()
    {
        _stop.Cancel();
        if (IsFirst) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}

internal static class AppInfo
{
    public static string Version =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public static string Folder => AppContext.BaseDirectory;

    public static string McpPath => Path.Combine(Folder, "worknotch-mcp.exe");

    public static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or FileNotFoundException or InvalidOperationException) { }
    }
}
