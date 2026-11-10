using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Worknotch.App.Servicios;

namespace Worknotch.App;

/// <summary>
/// La ventana: una pastilla pegada al borde de arriba que se abre en un panel con las tarjetas, o en
/// un aviso. La ventana en sí es transparente y no cambia de tamaño; lo que crece es el notch.
/// Este archivo lleva el estado, la colocación y el latido de cada segundo; el resto de partes, una
/// por tarjeta, están en los NotchWindow.*.cs.
/// </summary>
public partial class NotchWindow : Window
{
    private enum Mode { Pill, Panel, Alert }

    private const double PillWidth = 240, PillHeight = 30, PanelHeight = 430, AlertWidth = 460, AlertHeight = 116;
    private const double WindowWidthDip = 1320, WindowHeightDip = 520;

    private readonly Store _store;
    private readonly IClock _clock;
    private readonly ClipCatcher _clips;
    private readonly Updater _updater = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _leave = new() { Interval = TimeSpan.FromMilliseconds(420) };
    private readonly DispatcherTimer _notesSave = new() { Interval = TimeSpan.FromMilliseconds(700) };

    private AppData _data;
    private Mode _mode = Mode.Pill;
    private bool _pinned, _settingsOpen, _hiddenForFullscreen, _placing;
    private int _menusOpen;
    private DateOnly? _day; // el día que se está viendo; vacío es la bandeja
    private DateOnly _today;
    private DateTime _stamp;
    private IntPtr _hwnd, _monitor;
    private double _maxPanelWidth = 1280;
    private Hotkey? _hotkey;
    private int _ticks;

    /// <summary>En las pruebas de humo no hay animación ni latido: cada estado se pone a mano.</summary>
    public bool Instant { get; set; }

    public NotchWindow(Store store, IClock clock)
    {
        _store = store;
        _clock = clock;
        _clips = new ClipCatcher(store, () => Dispatcher.BeginInvoke(Refresh));
        _data = store.Read();
        _today = DateOnly.FromDateTime(clock.Now);
        _day = _today;
        InitializeComponent();

        _tick.Tick += (_, _) => Tick();
        _leave.Tick += (_, _) => { _leave.Stop(); CloseIfLeft(); };
        _notesSave.Tick += (_, _) => SaveNotes();
        SourceInitialized += OnSourceInitialized;
        Deactivated += (_, _) => { if (_mode == Mode.Panel && _menusOpen == 0 && !Instant) Close_(); };
        PreviewKeyDown += OnPreviewKeyDown;
        DpiChanged += (_, _) => { if (!_placing) Dispatcher.BeginInvoke(Place); };
        Closed += (_, _) => { _hotkey?.Unregister(); Native.RemoveClipboardFormatListener(_hwnd); _clips.Dispose(); };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        // Fuera de Alt+Tab: es parte del escritorio, no una ventana más
        Native.SetWindowLongPtr(_hwnd, Native.GwlExStyle, Native.GetWindowLongPtr(_hwnd, Native.GwlExStyle) | Native.WsExToolWindow);
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        Place();
        Refresh();
        if (Instant) return;

        _hotkey = new Hotkey(_hwnd);
        _hotkey.Register(_data.Settings.Hotkey);
        Native.AddClipboardFormatListener(_hwnd);
        _clips.WatchScreenshots();
        _tick.Start();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (Hotkey.IsHotkeyMessage(msg, wParam))
        {
            Toggle();
            handled = true;
        }
        else if (msg == Native.WmClipboardUpdate && _data.Settings.CatchClipboard)
        {
            _clips.ClipboardChanged();
        }
        return IntPtr.Zero;
    }

    // ---------- Colocación ----------

    /// <summary>Centra la ventana en el borde de arriba de la pantalla elegida.</summary>
    public void Place()
    {
        if (_hwnd == IntPtr.Zero) return;
        _placing = true;
        try
        {
            _monitor = Native.TargetMonitor(_data.Settings.Screen == ScreenChoice.Pointer);
            var r = Native.MonitorRect(_monitor);
            double scale = Native.MonitorScale(_monitor);
            if (r.Width <= 0) return;
            double widthDip = Math.Min(WindowWidthDip, r.Width / scale);
            int w = (int)Math.Round(widthDip * scale), h = (int)Math.Round(WindowHeightDip * scale);
            Native.SetWindowPos(_hwnd, IntPtr.Zero, r.Left + (r.Width - w) / 2, r.Top, w, h, Native.SwpNoActivate | Native.SwpNoZOrder);
            _maxPanelWidth = widthDip - 24;
        }
        finally { _placing = false; }
        if (_mode == Mode.Panel) ApplyMode(animate: false);
    }

    // ---------- Abrir y cerrar ----------

    public void Toggle()
    {
        if (_mode == Mode.Panel) Close_();
        else Open(pinned: true);
    }

    /// <summary>Abre el panel. Fijado (atajo, icono, enlace) no se cierra al apartar el puntero.</summary>
    public void Open(bool pinned)
    {
        if (_mode == Mode.Alert) return;
        _pinned = pinned;
        if (_hiddenForFullscreen) { _hiddenForFullscreen = false; Shell.Visibility = Visibility.Visible; }
        if (_mode != Mode.Panel)
        {
            _mode = Mode.Panel;
            Refresh();
            ApplyMode(animate: true);
        }
        if (pinned)
        {
            Activate();
            Native.SetForegroundWindow(_hwnd);
            if (!_settingsOpen && Detail.Visibility != Visibility.Visible && AddBox.IsVisible) AddBox.Focus();
        }
    }

    // «Close» ya es de Window: cerrar el panel es volver a la pastilla.
    private void Close_()
    {
        if (_mode != Mode.Panel) return;
        SaveNotes();
        _pinned = false;
        _settingsOpen = false;
        _mode = Mode.Pill;
        ApplyMode(animate: true);
    }

    private void CloseIfLeft()
    {
        if (_mode == Mode.Panel && !_pinned && !IsActive && !Shell.IsMouseOver && _menusOpen == 0) Close_();
    }

    private void Shell_MouseEnter(object sender, MouseEventArgs e)
    {
        _leave.Stop();
        if (_mode == Mode.Pill && !Instant) Open(pinned: false);
    }

    private void Shell_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_mode == Mode.Panel && !Instant) { _leave.Stop(); _leave.Start(); }
    }

    // Al arrastrar algo hacia la pastilla se abre el panel, para poder soltarlo en Recortes
    private void Shell_DragEnter(object sender, DragEventArgs e)
    {
        if (_mode == Mode.Pill && !Instant) Open(pinned: true);
    }

    private void Pill_Click(object sender, RoutedEventArgs e) => Open(pinned: true);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_mode == Mode.Alert) return;
            if (_settingsOpen) { _settingsOpen = false; RenderSettings(); }
            else if (Detail.Visibility == Visibility.Visible) CloseDetail();
            else Close_();
            e.Handled = true;
        }
        else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.FocusedElement is not TextBox)
        {
            Undo();
            e.Handled = true;
        }
    }

    /// <summary>Pone el tamaño y la forma del notch según el modo, y enseña solo la vista que toca.</summary>
    private void ApplyMode(bool animate)
    {
        double w = PillWidth, h = PillHeight;
        if (_mode == Mode.Panel) { w = PanelWidth(); h = PanelHeight; }
        else if (_mode == Mode.Alert) { w = AlertWidth; h = AlertHeight; }

        Pill.Visibility = _mode == Mode.Pill ? Visibility.Visible : Visibility.Collapsed;
        Alert.Visibility = _mode == Mode.Alert ? Visibility.Visible : Visibility.Collapsed;
        PanelView.Width = w;
        Shell.CornerRadius = new CornerRadius(0, 0, _mode == Mode.Pill ? 14 : 22, _mode == Mode.Pill ? 14 : 22);

        // Con «mostrar animaciones» apagado en Windows, el cambio es directo
        bool motion = animate && !Instant && SystemParameters.ClientAreaAnimation;
        var size = TimeSpan.FromMilliseconds(motion ? 220 : 0);
        Shell.BeginAnimation(WidthProperty, null);
        Shell.BeginAnimation(HeightProperty, null);
        PanelView.BeginAnimation(OpacityProperty, null);
        double fromW = Shell.ActualWidth > 0 ? Shell.ActualWidth : Shell.Width, fromH = Shell.ActualHeight > 0 ? Shell.ActualHeight : Shell.Height;
        Shell.Width = w;
        Shell.Height = h;
        if (motion)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Shell.BeginAnimation(WidthProperty, new DoubleAnimation(fromW, w, size) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
            Shell.BeginAnimation(HeightProperty, new DoubleAnimation(fromH, h, size) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
        }

        if (_mode == Mode.Panel)
        {
            PanelView.Visibility = Visibility.Visible;
            PanelView.Opacity = 1;
            if (motion) PanelView.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { BeginTime = TimeSpan.FromMilliseconds(80), FillBehavior = FillBehavior.Stop });
        }
        else
        {
            PanelView.Opacity = 0;
            PanelView.Visibility = Visibility.Collapsed;
        }
    }

    private double PanelWidth()
    {
        if (_settingsOpen) return Math.Min(_maxPanelWidth, 1000);
        double cards = 0;
        foreach (FrameworkElement card in Cards.Children)
            if (card.Visibility == Visibility.Visible) cards += card.Width + 10;
        // márgenes del panel (14 + 4) más el hueco que deja la última tarjeta
        return Math.Clamp(cards + 18, Math.Min(760, _maxPanelWidth), _maxPanelWidth); // los días de arriba necesitan su sitio
    }

    // ---------- Datos ----------

    /// <summary>Vuelve a leer lo guardado y repinta todo.</summary>
    public void Refresh()
    {
        _data = _store.Read();
        _stamp = Stamp();
        _today = DateOnly.FromDateTime(_clock.Now);
        RenderSettings();
        RenderDays();
        RenderTasks();
        RenderDetail();
        RenderFocus();
        RenderNotes();
        RenderClips();
        RenderWeek();
        RenderPill();
        UndoButton.IsEnabled = _store.CanUndo;
        if (_mode == Mode.Panel && Math.Abs(PanelWidth() - Shell.Width) > 0.5) ApplyMode(animate: false);
    }

    /// <summary>Aplica un cambio y repinta. Si no se puede, devuelve el motivo para enseñarlo junto al campo.</summary>
    private string? Do(Action<AppData, DateTime> change, bool undoable = false)
    {
        string? error = null;
        try { _store.Mutate(d => change(d, _clock.Now), undoable); }
        catch (UserException e) { error = e.Message; }
        Refresh();
        return error;
    }

    private void Undo()
    {
        if (_store.Undo()) Refresh();
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => Undo();

    private DateTime Stamp()
    {
        try { return File.GetLastWriteTimeUtc(_store.Path); }
        catch (IOException) { return _stamp; }
    }

    // ---------- El latido ----------

    private void Tick()
    {
        var now = _clock.Now;
        _ticks++;

        // Otro proceso (Claude) ha cambiado los datos
        if (Stamp() != _stamp) Refresh();

        // Ha cambiado el día
        if (DateOnly.FromDateTime(now) != _today || _data.LastDay != DateOnly.FromDateTime(now))
        {
            bool viewingToday = _day == _today;
            _store.Mutate(d => Planner.CatchUp(d, now));
            if (viewingToday) _day = DateOnly.FromDateTime(now);
            Refresh();
        }

        if (_data.Tasks.Any(t => !t.Done && !t.ReminderShown && t.RemindAt is { } r && r <= now))
        {
            foreach (var t in _store.Mutate(d => Planner.TakeDueReminders(d, now))) QueueReminder(t);
            Refresh();
        }

        if (FocusTimer.Finished(_data.Timer, now) && !_data.Timer.EndShown && _store.Mutate(d => FocusTimer.TakeEnd(d, now)))
        {
            Refresh();
            QueueFocusEnd();
        }

        if (_data.Timer.Active)
        {
            RenderFocus();
            RenderPill();
        }
        else if (_ticks % 30 == 0) RenderPill(); // la hora del próximo aviso va cambiando

        if (_ticks % 2 == 0 && _mode == Mode.Pill) FollowScreen();
    }

    // La pastilla sigue al puntero entre pantallas y se aparta de lo que esté a pantalla completa.
    private void FollowScreen()
    {
        if (_data.Settings.Screen == ScreenChoice.Pointer && Native.TargetMonitor(true) != _monitor) Place();
        bool fullscreen = Native.FullscreenAppOn(_monitor, _hwnd);
        if (fullscreen == _hiddenForFullscreen) return;
        _hiddenForFullscreen = fullscreen;
        Shell.Visibility = fullscreen ? Visibility.Hidden : Visibility.Visible;
    }

    // ---------- Enlaces worknotch:// ----------

    public void HandleLink(string text)
    {
        var cmd = Link.Parse(text);
        if (cmd == null || cmd.Action == LinkAction.Show) { Open(pinned: true); return; }
        string? error = null;
        TaskItem? added = null;
        try { added = _store.Mutate(d => Planner.AddFromLine(d, cmd.Line, _today, _clock.Now)); }
        catch (UserException e) { error = e.Message; }
        Refresh();
        if (added != null)
            ShowAlert(new AlertInfo("Tarea añadida", $"«{added.Title}», {Where(added.Day)}.", [new AlertAction("Vale", () => { }, true)]));
        else
            ShowAlert(new AlertInfo("No se ha podido añadir la tarea", error ?? "", [new AlertAction("Vale", () => { }, true)]));
    }

    private string Where(DateOnly? day) => day is null ? "en la bandeja" : "para " + Planner.DayName(day, _today).ToLower(Texts.Culture);

    // ---------- Menús ----------

    private ContextMenu NewMenu(UIElement target)
    {
        var menu = new ContextMenu { PlacementTarget = target, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        menu.Opened += (_, _) => _menusOpen++;
        menu.Closed += (_, _) =>
        {
            _menusOpen = Math.Max(0, _menusOpen - 1);
            if (!Shell.IsMouseOver) { _leave.Stop(); _leave.Start(); }
        };
        return menu;
    }

    private static void AddItem(ContextMenu menu, string text, Action action, bool enabled = true)
    {
        var item = new MenuItem { Header = text, IsEnabled = enabled };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }
}
