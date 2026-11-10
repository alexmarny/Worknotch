using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Worknotch.App.Servicios;
using Forms = System.Windows.Forms;

namespace Worknotch.App;

/// <summary>
/// Arranque: una sola instancia, el icono junto al reloj y la ventana del notch.
///   Worknotch.exe                    abre (o enseña la que ya está abierta)
///   Worknotch.exe worknotch://add?…  le pasa el enlace a la que está abierta
///   Worknotch.exe --inicio           arranque con Windows: sin saludo
///   Worknotch.exe --humo carpeta     prueba de humo: pinta cada vista, la guarda como PNG y sale
/// </summary>
public partial class App : Application
{
    private SingleInstance? _instance;
    private Forms.NotifyIcon? _tray;
    private NotchWindow? _window;
    private Store? _store;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string first = e.Args.FirstOrDefault() ?? "";

        if (first == "--humo")
        {
            Shutdown(Smoke.Run(e.Args.ElementAtOrDefault(1) ?? "humo"));
            return;
        }

        _instance = new SingleInstance();
        if (!_instance.IsFirst)
        {
            SingleInstance.SendToFirst(first.StartsWith(Link.Scheme + ":", StringComparison.OrdinalIgnoreCase) ? first : Link.Scheme + "://show");
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandled;
        var clock = new SystemClock();
        _store = Store.Default();
        var data = _store.Mutate(d => { Planner.CatchUp(d, clock.Now); return d; });
        Theme.Apply(data.Settings.Theme);

        _window = new NotchWindow(_store, clock);
        _window.Show();
        CreateTray();

        _instance.Listen(message => Dispatcher.BeginInvoke(() => _window.HandleLink(message)));
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;

        if (first.StartsWith(Link.Scheme + ":", StringComparison.OrdinalIgnoreCase)) _window.HandleLink(first);
        else if (!data.Settings.WelcomeSeen) _window.ShowWelcome();
        if (data.Settings.CheckUpdates) _ = _window.CheckUpdatesAsync();
    }

    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir Worknotch", null, (_, _) => _window?.Open(pinned: true));
        menu.Items.Add("Ajustes", null, (_, _) => _window?.OpenSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Shutdown());
        _tray = new Forms.NotifyIcon
        {
            Icon = Environment.ProcessPath is { } exe ? System.Drawing.Icon.ExtractAssociatedIcon(exe) : System.Drawing.SystemIcons.Application,
            Text = "Worknotch",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, args) => { if (args.Button == Forms.MouseButtons.Left) _window?.Toggle(); };
    }

    private void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // Con el tema «El de Windows», seguir el cambio entre claro y oscuro
        if (e.Category == UserPreferenceCategory.General && _store != null)
            Dispatcher.BeginInvoke(() => Theme.Apply(_store.Read().Settings.Theme));
    }

    private void OnDisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => _window?.Place());

    // Un fallo inesperado no cierra la app ni se enseña en crudo: se apunta y se sigue.
    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            File.AppendAllText(Path.Combine(Store.DefaultFolder(), "errores.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {AppInfo.Version}\n{e.Exception}\n\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        e.Handled = true;
        _window?.Refresh();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _instance?.Dispose();
        base.OnExit(e);
    }
}
