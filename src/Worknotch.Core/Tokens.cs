using System.Globalization;
using System.Text.Json;

namespace Worknotch.Core;

public sealed record ContrastRule(string Text, string Background, double Ratio);

/// <summary>
/// Los colores del diseño. La fuente es docs/design-system/tokens.json, que va dentro de la app:
/// cada token tiene su valor en Noche y en Día, y la lista de contrastes que deben cumplirse la
/// comprueban las pruebas.
/// </summary>
public sealed class Tokens
{
    public Dictionary<string, string> Night { get; } = [];
    public Dictionary<string, string> Day { get; } = [];
    public List<ContrastRule> Contrasts { get; } = [];

    public static Tokens Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var tokens = new Tokens();
        foreach (var color in doc.RootElement.GetProperty("colores").EnumerateObject())
        {
            tokens.Night[color.Name] = color.Value.GetProperty("noche").GetString()!;
            tokens.Day[color.Name] = color.Value.GetProperty("dia").GetString()!;
        }
        foreach (var rule in doc.RootElement.GetProperty("contrastes").EnumerateArray())
            tokens.Contrasts.Add(new ContrastRule(rule[0].GetString()!, rule[1].GetString()!, rule[2].GetDouble()));
        return tokens;
    }

    public static (byte R, byte G, byte B) Rgb(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 8) hex = hex[2..]; // #AARRGGBB
        return (byte.Parse(hex[..2], NumberStyles.HexNumber), byte.Parse(hex[2..4], NumberStyles.HexNumber), byte.Parse(hex[4..6], NumberStyles.HexNumber));
    }

    /// <summary>Contraste WCAG entre dos colores opacos, de 1 a 21.</summary>
    public static double Contrast(string a, string b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(string hex)
    {
        var (r, g, b) = Rgb(hex);
        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
        static double Channel(byte v)
        {
            double c = v / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}

public static class TokensFile
{
    /// <summary>El tokens.json que va dentro del ensamblado.</summary>
    public static string Read()
    {
        using var stream = typeof(TokensFile).Assembly.GetManifestResourceStream("tokens.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
