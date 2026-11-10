using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace Worknotch.App.Servicios;

/// <summary>
/// Mira en GitHub si hay una versión nueva y la instala: descarga el instalador, comprueba su suma
/// con SHA256SUMS.txt y lo lanza en silencio. Es lo único para lo que la app sale a internet.
/// </summary>
internal sealed class Updater
{
    private static readonly HttpClient Http = CreateClient();

    public string? Latest { get; private set; }

    public bool Available => Update.IsNewer(Latest, AppInfo.Version);

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Worknotch");
        return http;
    }

    public async Task<bool> CheckAsync()
    {
        try
        {
            string json = await Http.GetStringAsync($"https://api.github.com/repos/{Update.Repo}/releases/latest");
            using var doc = JsonDocument.Parse(json);
            Latest = doc.RootElement.GetProperty("tag_name").GetString();
            return Available;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Descarga, comprueba y lanza el instalador. Si devuelve true, hay que cerrar la app.</summary>
    public async Task<bool> InstallAsync()
    {
        if (!Available) return false;
        string baseUrl = $"https://github.com/{Update.Repo}/releases/download/{Latest}/";
        string folder = Path.Combine(Path.GetTempPath(), "Worknotch-actualizar");
        string installer = Path.Combine(folder, Update.Installer);
        try
        {
            Directory.CreateDirectory(folder);
            string sums = await Http.GetStringAsync(baseUrl + Update.Sums);
            await using (var file = File.Create(installer))
            await using (var stream = await Http.GetStreamAsync(baseUrl + Update.Installer))
                await stream.CopyToAsync(file);
            if (!Update.Verify(installer, sums, Update.Installer))
            {
                File.Delete(installer);
                return false;
            }
            Process.Start(new ProcessStartInfo(installer, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /FORCECLOSEAPPLICATIONS /relanzar=1") { UseShellExecute = true });
            return true;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
