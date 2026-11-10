using System.Windows.Input;
using System.Windows.Interop;

namespace Worknotch.App.Servicios;

/// <summary>El atajo global que abre y cierra el panel desde cualquier programa.</summary>
internal sealed class Hotkey(IntPtr hwnd)
{
    private const int Id = 0x574E; // «WN»
    private const uint ModAlt = 1, ModCtrl = 2, ModShift = 4, ModWin = 8, ModNoRepeat = 0x4000;

    /// <summary>Los atajos que se pueden elegir en Ajustes.</summary>
    public static readonly string[] Choices = ["Ctrl+Alt+Espacio", "Ctrl+Alt+N", "Alt+Mayús+Espacio", "Ctrl+Mayús+Espacio", "Win+Alt+N"];

    public bool Registered { get; private set; }

    /// <summary>Registra el atajo. Devuelve false si lo está usando otro programa.</summary>
    public bool Register(string choice)
    {
        Unregister();
        uint mods = ModNoRepeat;
        Key key = Key.Space;
        foreach (string part in choice.Split('+'))
        {
            switch (part)
            {
                case "Ctrl": mods |= ModCtrl; break;
                case "Alt": mods |= ModAlt; break;
                case "Mayús": mods |= ModShift; break;
                case "Win": mods |= ModWin; break;
                case "Espacio": key = Key.Space; break;
                default: if (Enum.TryParse(part, out Key k)) key = k; break;
            }
        }
        Registered = Native.RegisterHotKey(hwnd, Id, mods, (uint)KeyInterop.VirtualKeyFromKey(key));
        return Registered;
    }

    public void Unregister()
    {
        if (Registered) Native.UnregisterHotKey(hwnd, Id);
        Registered = false;
    }

    public static bool IsHotkeyMessage(int msg, IntPtr wParam) => msg == Native.WmHotkey && wParam.ToInt32() == Id;
}
