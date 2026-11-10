using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Worknotch.Core;

/// <summary>Lo que se puede comprobar de una actualización sin red: números de versión y sumas.</summary>
public static partial class Update
{
    public const string Repo = "alexmarny/Worknotch";
    public const string Installer = "Worknotch-setup.exe";
    public const string Sums = "SHA256SUMS.txt";

    /// <summary>Los tres números de «v1.2.3», «1.2.3» o «v1.2.3-4-gabc». Null si no es una versión.</summary>
    public static Version? ParseVersion(string? text)
    {
        var m = VersionRx().Match(text ?? "");
        return m.Success ? new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)) : null;
    }

    /// <summary>True si <paramref name="latest"/> es una versión estable posterior a la instalada.</summary>
    public static bool IsNewer(string? latest, string? installed)
    {
        if (latest is null || latest.Contains('-')) return false; // betas y compilaciones intermedias no cuentan
        var l = ParseVersion(latest);
        var i = ParseVersion(installed);
        return l != null && i != null && l > i;
    }

    /// <summary>La suma de un archivo dentro de SHA256SUMS.txt («suma  nombre» por línea).</summary>
    public static string? SumFor(string sums, string fileName)
    {
        foreach (string line in sums.Split('\n'))
        {
            string[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1].TrimStart('*') == fileName && parts[0].Length == 64) return parts[0].ToLowerInvariant();
        }
        return null;
    }

    public static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static bool Verify(string path, string sums, string fileName) =>
        SumFor(sums, fileName) is { } expected && Sha256(path) == expected;

    [GeneratedRegex(@"^v?(\d+)\.(\d+)\.(\d+)")]
    private static partial Regex VersionRx();
}
