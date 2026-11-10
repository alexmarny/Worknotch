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

    public static void Apply(ThemeChoice choice)
    {
        IsDay = choice == ThemeChoice.Day || (choice == ThemeChoice.System && SystemIsLight());
        var resources = Application.Current.Resources;
        foreach (var (name, hex) in IsDay ? All.Day : All.Night)
        {
            var (r, g, b) = Tokens.Rgb(hex);
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            resources[name] = brush;
        }
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
