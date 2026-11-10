using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Worknotch.App.Servicios;

namespace Worknotch.App;

public sealed record AlertAction(string Label, Action Run, bool Primary = false);

public sealed record AlertInfo(string Title, string Text, IReadOnlyList<AlertAction> Actions);

// Notas, recortes, semana, ajustes y avisos.
public partial class NotchWindow
{
    private static readonly string[] CardOrder = ["tareas", "foco", "notas", "recortes", "semana"];
    private readonly Queue<AlertInfo> _alerts = new();
    private bool _settingNotes;
    private Point? _dragStart;
    private string? _clipsNotice;

    // ---------- Notas ----------

    private void RenderNotes()
    {
        if (Notes.IsKeyboardFocused || Notes.Text == _data.Notepad) return;
        _settingNotes = true;
        Notes.Text = _data.Notepad;
        _settingNotes = false;
    }

    private void Notes_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_settingNotes) return;
        _notesSave.Stop();
        _notesSave.Start();
    }

    private void Notes_LostFocus(object sender, RoutedEventArgs e) => SaveNotes();

    private void SaveNotes()
    {
        _notesSave.Stop();
        string text = Notes.Text;
        if (text == _data.Notepad) return;
        _store.Mutate(d => d.Notepad = text);
        _data.Notepad = text;
        _stamp = Stamp();
    }

    // ---------- Recortes ----------

    private void RenderClips()
    {
        var now = _clock.Now;
        ClipList.ItemsSource = _data.Clips.Select(c => new ClipRow
        {
            Id = c.Id,
            Title = c.Kind is ClipKind.Text or ClipKind.Link ? Shorten(c.Content) : Path.GetFileName(c.Content),
            Sub = $"{KindName(c)}, {(DateOnly.FromDateTime(c.Added) == _today ? Texts.Time(c.Added) : c.Added.ToString("d MMM", Texts.Culture))}",
            Thumb = c.Kind == ClipKind.Image ? Thumbnail(c.Content) : null,
        }).ToList();
        bool empty = _data.Clips.Count == 0;
        ClipsEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        ClipsEmpty.Text = "Lo que copies y tus capturas de pantalla se cuelgan aquí. También puedes arrastrar archivos.";
        ClipsHint.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        ClipsHint.Text = _clipsNotice ?? "Pulsa uno para copiarlo. Con doble clic se abre.";
        _clipsNotice = null;
    }

    private static string Shorten(string text)
    {
        text = text.ReplaceLineEndings(" ").Trim();
        return text.Length > 140 ? text[..140] + "…" : text;
    }

    private static string KindName(Clip c) => c.Kind switch
    {
        ClipKind.Link => "Enlace",
        ClipKind.Image => "Imagen",
        ClipKind.File => "Archivo",
        _ => "Texto",
    };

    private static BitmapImage? Thumbnail(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.DecodePixelWidth = 96;
            image.CacheOption = BitmapCacheOption.OnLoad; // no deja el archivo cogido
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private Clip? ClipOf(object sender) => _data.Clips.FirstOrDefault(c => c.Id == IdOf(sender));

    private void Clip_Click(object sender, RoutedEventArgs e)
    {
        if (ClipOf(sender) is not { } clip) return;
        _clips.Copy(clip);
        _clipsNotice = "Copiado. Ya puedes pegarlo.";
        RenderClips();
    }

    private void Clip_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ClipOf(sender) is { Kind: not ClipKind.Text } clip) AppInfo.Open(clip.Content);
    }

    // Arrastrar un recorte fuera, a otro programa
    private void Clip_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) { _dragStart = null; return; }
        var p = e.GetPosition(this);
        if (_dragStart is not { } start) { _dragStart = p; return; }
        if (Math.Abs(p.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _dragStart = null;
        if (ClipOf(sender) is not { } clip) return;
        var data = new DataObject();
        if (clip.Kind is ClipKind.Text or ClipKind.Link) data.SetText(clip.Content);
        else if (File.Exists(clip.Content)) data.SetFileDropList([clip.Content]);
        else return;
        _menusOpen++; // mientras se arrastra, el panel no se cierra
        try { DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy); }
        finally { _menusOpen--; }
    }

    private void ClipRemove_Click(object sender, RoutedEventArgs e) => _clips.Remove(IdOf(sender));

    private void Clips_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Clips_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            foreach (string f in files.Take(10)) _clips.Add(ClipCatcher.KindOfFile(f), f);
        else if (e.Data.GetData(DataFormats.UnicodeText) is string text && text.Trim().Length > 0)
            _clips.Add(ClipCatcher.IsLink(text.Trim()) ? ClipKind.Link : ClipKind.Text, text.Trim());
        e.Handled = true;
    }

    // ---------- Semana ----------

    private void RenderWeek()
    {
        var w = Stats.Week(_data, _clock.Now);
        const double max = 80;
        int topTasks = Math.Max(1, w.Days.Max(d => d.Completed));
        int topFocus = Math.Max(1, w.Days.Max(d => d.FocusSeconds));
        string[] letters = ["D", "L", "M", "X", "J", "V", "S"];
        WeekChart.ItemsSource = w.Days.Select(d => new DayBar(
            letters[(int)d.Day.DayOfWeek],
            d.Completed == 0 ? 0 : Math.Max(3, max * d.Completed / topTasks),
            d.FocusSeconds < 60 ? 0 : Math.Max(3, max * d.FocusSeconds / topFocus),
            $"{Capital(d.Day.ToString("dddd d", Texts.Culture))}: {Texts.Plural(d.Completed, "tarea hecha", "tareas hechas")}, {Texts.Duration(d.FocusSeconds)} de foco",
            d.Day == _today)).ToList();
        WeekSentence.Text = Stats.Sentence(w);
        WeekStreak.Text = w.Streak > 0 ? $"Racha: {Texts.Plural(w.Streak, "día seguido", "días seguidos")}" : "";
        WeekStreak.Visibility = w.Streak > 0 ? Visibility.Visible : Visibility.Collapsed;
        WeekPeak.Text = w.PeakHour is { } h ? $"Tu mejor hora: de {h} a {h + 1}" : "";
        WeekPeak.Visibility = w.PeakHour != null ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- Ajustes ----------

    private void RenderSettings()
    {
        var s = _data.Settings;
        CardTasks.Visibility = Shown("tareas");
        CardFocus.Visibility = Shown("foco");
        CardNotes.Visibility = Shown("notas");
        CardClips.Visibility = Shown("recortes");
        CardWeek.Visibility = Shown("semana");
        CardsScroll.Visibility = _settingsOpen ? Visibility.Collapsed : Visibility.Visible;
        SettingsView.Visibility = _settingsOpen ? Visibility.Visible : Visibility.Collapsed;
        Days.Visibility = _settingsOpen ? Visibility.Hidden : Visibility.Visible;

        UpdateButton.Visibility = _updater.Available ? Visibility.Visible : Visibility.Collapsed;
        if (_updater.Available && UpdateButton.IsEnabled) UpdateButton.Content = $"Actualizar a la {_updater.Latest!.TrimStart('v')}";
        if (!_settingsOpen) return;

        ThemeSystem.IsChecked = s.Theme == ThemeChoice.System;
        ThemeNight.IsChecked = s.Theme == ThemeChoice.Night;
        ThemeDay.IsChecked = s.Theme == ThemeChoice.Day;
        ScreenPointer.IsChecked = s.Screen == ScreenChoice.Pointer;
        ScreenPrimary.IsChecked = s.Screen == ScreenChoice.Primary;
        HotkeyButton.Content = s.Hotkey;
        HotkeyError.Visibility = _hotkey is { Registered: false } ? Visibility.Visible : Visibility.Collapsed;
        SettingsMinutes.Text = $"{s.FocusMinutes} min";
        ShowTasks.IsChecked = s.Cards.Contains("tareas");
        ShowFocus.IsChecked = s.Cards.Contains("foco");
        ShowNotes.IsChecked = s.Cards.Contains("notas");
        ShowClips.IsChecked = s.Cards.Contains("recortes");
        ShowWeek.IsChecked = s.Cards.Contains("semana");
        OptClipboard.IsChecked = s.CatchClipboard;
        OptScreenshots.IsChecked = s.CatchScreenshots;
        OptAutostart.IsChecked = Autostart.Enabled;
        OptSound.IsChecked = s.Sound;
        OptCarry.IsChecked = s.CarryForward;
        OptWeekends.IsChecked = s.WeekendsInStreak;
        OptUpdates.IsChecked = s.CheckUpdates;
        string version = $"Worknotch {AppInfo.Version}.";
        if (_store.PreservedAt is { } copy) version += $" Los datos guardados no se pudieron leer y se ha empezado de cero. La copia está en la carpeta de datos: {Path.GetFileName(copy)}.";
        VersionText.Text = version;

        Visibility Shown(string card) => s.Cards.Contains(card) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        _settingsOpen = !_settingsOpen;
        ClaudeButton.Content = "Copiar la orden para conectar Claude";
        RenderSettings();
        ApplyMode(animate: false);
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        var choice = Enum.Parse<ThemeChoice>((string)((FrameworkElement)sender).Tag);
        Do((d, _) => d.Settings.Theme = choice);
        Theme.Apply(choice);
    }

    private void Screen_Click(object sender, RoutedEventArgs e)
    {
        var choice = Enum.Parse<ScreenChoice>((string)((FrameworkElement)sender).Tag);
        Do((d, _) => d.Settings.Screen = choice);
        Place();
    }

    private void Hotkey_Click(object sender, RoutedEventArgs e)
    {
        int i = Array.IndexOf(Hotkey.Choices, _data.Settings.Hotkey);
        string next = Hotkey.Choices[(i + 1) % Hotkey.Choices.Length];
        _hotkey?.Register(next);
        Do((d, _) => d.Settings.Hotkey = next);
    }

    private void Card_Click(object sender, RoutedEventArgs e)
    {
        string card = (string)((FrameworkElement)sender).Tag;
        bool on = ((CheckBox)sender).IsChecked == true;
        Do((d, _) =>
        {
            var cards = d.Settings.Cards;
            if (on && !cards.Contains(card)) cards.Add(card);
            if (!on && cards.Count > 1) cards.Remove(card); // siempre queda al menos una a la vista
            d.Settings.Cards = CardOrder.Where(cards.Contains).ToList();
        });
    }

    private void Option_Click(object sender, RoutedEventArgs e)
    {
        bool on = ((CheckBox)sender).IsChecked == true;
        if (sender == OptAutostart) { Autostart.Enabled = on; RenderSettings(); return; }
        Do((d, _) =>
        {
            var s = d.Settings;
            if (sender == OptClipboard) s.CatchClipboard = on;
            else if (sender == OptScreenshots) s.CatchScreenshots = on;
            else if (sender == OptSound) s.Sound = on;
            else if (sender == OptCarry) s.CarryForward = on;
            else if (sender == OptWeekends) s.WeekendsInStreak = on;
            else if (sender == OptUpdates) s.CheckUpdates = on;
        });
    }

    private void Claude_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText($"claude mcp add worknotch -- \"{AppInfo.McpPath}\"");
            ClaudeButton.Content = "Copiada. Pégala en una terminal.";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            ClaudeButton.Content = "No se ha podido copiar. Inténtalo otra vez.";
        }
    }

    private void DataFolder_Click(object sender, RoutedEventArgs e) => AppInfo.Open(Store.DefaultFolder());

    private void Quit_Click(object sender, RoutedEventArgs e)
    {
        SaveNotes();
        Application.Current.Shutdown();
    }

    /// <summary>Mira si hay versión nueva; si la hay, aparece el botón «Actualizar» arriba.</summary>
    public async Task CheckUpdatesAsync()
    {
        if (await _updater.CheckAsync()) RenderSettings();
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "Descargando…";
        if (await _updater.InstallAsync())
        {
            SaveNotes();
            Application.Current.Shutdown();
            return;
        }
        UpdateButton.IsEnabled = true;
        UpdateButton.Content = "No se ha podido actualizar. Reintentar";
    }

    public void OpenSettings(bool open = true)
    {
        _settingsOpen = open;
        Open(pinned: true);
        RenderSettings();
        ApplyMode(animate: false);
    }

    // Para la prueba de humo
    internal FrameworkElement ShellElement => Shell;

    internal void OpenTask(string? id)
    {
        _detailId = id;
        RenderDetail();
    }

    internal void DismissAlert()
    {
        _alerts.Clear();
        NextAlert();
    }

    // ---------- Avisos ----------

    /// <summary>Enseña un aviso en el notch. Si ya hay uno, espera su turno.</summary>
    public void ShowAlert(AlertInfo alert)
    {
        _alerts.Enqueue(alert);
        if (_mode != Mode.Alert) NextAlert();
    }

    private void NextAlert()
    {
        if (!_alerts.TryDequeue(out var alert))
        {
            _mode = Mode.Pill;
            ApplyMode(animate: true);
            if (Shell.IsMouseOver && !Instant) Open(pinned: false);
            return;
        }
        SaveNotes();
        AlertTitle.Text = alert.Title;
        AlertText.Text = alert.Text;
        AlertText.Visibility = alert.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        AlertButtons.Children.Clear();
        foreach (var action in alert.Actions)
        {
            var button = new Button { Content = action.Label, Style = (Style)FindResource(action.Primary ? "BotonPrincipal" : "Boton"), Margin = new Thickness(0, 0, 6, 0) };
            button.Click += (_, _) =>
            {
                action.Run();
                Refresh();
                NextAlert();
            };
            AlertButtons.Children.Add(button);
        }
        if (_hiddenForFullscreen) { _hiddenForFullscreen = false; Shell.Visibility = Visibility.Visible; }
        _mode = Mode.Alert;
        _settingsOpen = false;
        ApplyMode(animate: true);
        if (_data.Settings.Sound && !Instant) System.Media.SystemSounds.Asterisk.Play();
    }

    private void QueueReminder(TaskItem task)
    {
        string id = task.Id;
        ShowAlert(new AlertInfo(task.Title, $"Aviso de las {Texts.Time(task.RemindAt ?? _clock.Now)}.",
        [
            new AlertAction("Empezar foco", () => _store.Mutate(d => FocusTimer.Start(d, id, null, TimerMode.Countdown, _clock.Now)), true),
            new AlertAction("Avisar en 5 min", () => _store.Mutate(d => Planner.Snooze(d, id, 5, _clock.Now))),
            new AlertAction("Hecha", () => _store.Mutate(d => Planner.Complete(d, id, _clock.Now), undoable: true)),
            new AlertAction("Cerrar", () => { }),
        ]));
    }

    private void QueueFocusEnd()
    {
        var t = _data.Timer;
        var task = t.TaskId == null ? null : _data.Tasks.FirstOrDefault(x => x.Id == t.TaskId);
        string minutes = Texts.Duration(t.TargetSeconds);
        var actions = new List<AlertAction>();
        if (task != null) actions.Add(new AlertAction("Hecha", () => _store.Mutate(d => Planner.Complete(d, task.Id, _clock.Now), undoable: true), true));
        actions.Add(new AlertAction("5 min más", () => _store.Mutate(d => { if (d.Timer.Active) FocusTimer.AddMinutes(d, 5); }), task == null));
        actions.Add(new AlertAction("Terminar", () => _store.Mutate(d => FocusTimer.Stop(d, _clock.Now))));
        ShowAlert(new AlertInfo("Tiempo cumplido",
            task != null ? $"{minutes} de foco en «{task.Title}». Márcala como hecha o tómate un descanso antes de seguir." : $"{minutes} de foco. Tómate un descanso.",
            actions));
    }

    /// <summary>El saludo de la primera vez.</summary>
    public void ShowWelcome()
    {
        ShowAlert(new AlertInfo("Tu día vive aquí arriba",
            $"Pasa el puntero por esta pastilla o pulsa {_data.Settings.Hotkey} para abrirla desde cualquier programa.",
            [new AlertAction("Entendido", () => _store.Mutate(d => d.Settings.WelcomeSeen = true), true)]));
    }
}
