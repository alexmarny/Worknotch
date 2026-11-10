using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Worknotch.App.Servicios;

/// <summary>
/// Pone los colores de tokens.json como recursos de la aplicación («wn-ambar», «wn-texto»…), en
/// Noche o en Día. Todo lo visible los toma con DynamicResource: nunca un color escrito a mano.
/// </summary>
internal static class Theme
{
    private static readonly Tokens All = Tokens.Parse(TokensFile.Read());

    public static bool IsDay { get; private set; }

    /// <summary>
    /// Los tokens que cambian dentro de una tarjeta de color: cada tarjeta lo lleva en sus recursos, y
    /// así los mismos estilos (texto, campos, casillas) salen en tinta sin tocarlos.
    /// </summary>
    public static ResourceDictionary CardScope { get; } = [];

    public static void Apply(ThemeChoice choice)
    {
        IsDay = choice == ThemeChoice.Day || (choice == ThemeChoice.System && SystemIsLight());
        var resources = Application.Current.Resources;
        foreach (var (name, hex) in IsDay ? All.Day : All.Night) resources[name] = Brush(hex);
        foreach (var (name, hex) in IsDay ? All.CardsDay : All.CardsNight) resources[name] = Brush(hex);
        foreach (var (name, hex) in IsDay ? All.InCardDay : All.InCardNight) CardScope[name] = Brush(hex);
    }

    /// <summary>Todos los colores opacos que puede llevar un texto en el tema puesto (para la prueba de humo).</summary>
    public static HashSet<(byte, byte, byte)> TextColors() =>
        (IsDay ? All.Day.Values.Concat(All.InCardDay.Values) : All.Night.Values.Concat(All.InCardNight.Values))
            .Where(hex => hex.Length == 7).Select(Tokens.Rgb).ToHashSet();

    private static SolidColorBrush Brush(string hex)
    {
        var (a, r, g, b) = Tokens.Argb(hex);
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private static bool SystemIsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v != 0;
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
