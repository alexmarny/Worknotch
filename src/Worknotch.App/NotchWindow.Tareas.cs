using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Worknotch.App;

// Los días de arriba, la lista de tareas y el detalle de una tarea.
public partial class NotchWindow
{
    private string? _detailId;

    // ---------- Días ----------

    private void RenderDays()
    {
        int inbox = _data.Tasks.Count(t => t.Day is null && !t.Done);
        InboxChip.Content = inbox > 0 ? $"Bandeja ({inbox})" : "Bandeja";
        InboxChip.IsChecked = _day is null;

        DayStrip.Children.Clear();
        if (_day is { } past && past < _today)
        {
            // Un día pasado: su ficha y una flecha para volver hacia hoy
            DayStrip.Children.Add(DayChip(past));
            var next = new Button { Style = (Style)FindResource("BotonIcono"), ToolTip = "Día siguiente", Margin = new Thickness(0, 0, 6, 0) };
            next.Content = new System.Windows.Shapes.Path { Style = (Style)FindResource("Icono"), Data = Geometry.Parse("M6,3.5 L10.5,8 L6,12.5") };
            System.Windows.Automation.AutomationProperties.SetName(next, "Día siguiente");
            next.Click += (_, _) => SelectDay(past.AddDays(1));
            DayStrip.Children.Add(next);
        }
        for (int i = 0; i < 7; i++) DayStrip.Children.Add(DayChip(_today.AddDays(i)));
    }

    private RadioButton DayChip(DateOnly day)
    {
        int diff = day.DayNumber - _today.DayNumber;
        string label = diff is 0 or 1 or -1 ? Planner.DayName(day, _today) : Capital(day.ToString("ddd d", Texts.Culture).Replace(".", ""));
        var chip = new RadioButton { Style = (Style)FindResource("Ficha"), GroupName = "dias", Content = label, IsChecked = _day == day };
        chip.Click += (_, _) => SelectDay(day);
        return chip;
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Texts.Culture) + s[1..];

    private void SelectDay(DateOnly? day)
    {
        _day = day;
        _detailId = null;
        _settingsOpen = false;
        HideAddError();
        Refresh();
    }

    private void Inbox_Click(object sender, RoutedEventArgs e) => SelectDay(null);

    private void PrevDay_Click(object sender, RoutedEventArgs e) => SelectDay((_day ?? _today).AddDays(-1));

    // ---------- Lista ----------

    private void RenderTasks()
    {
        var now = _clock.Now;
        bool past = _day is { } d && d < _today;
        var all = Planner.ForDay(_data, _day, now);
        int done = all.Count(t => t.Done), todo = all.Count - done;
        if (done == 0) _showDone = false;
        var tasks = all.Where(t => t.Done == _showDone).ToList();
        TaskList.ItemsSource = tasks.Select(t => TaskRow.From(t, _data, now, canEdit: !past)).ToList();
        TabTodo.Content = $"Por hacer {todo}";
        TabDone.Content = $"Hechas {done}";
        TabTodo.IsChecked = !_showDone;
        TabDone.IsChecked = _showDone;
        TabDone.Visibility = done > 0 ? Visibility.Visible : Visibility.Collapsed;
        TabTodo.Visibility = all.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        DayTitle.Text = _day is { } day
            ? (Math.Abs(day.DayNumber - _today.DayNumber) <= 1 ? Planner.DayName(day, _today) : Capital(day.ToString("dddd d 'de' MMMM", Texts.Culture)))
            : "Bandeja";
        TasksEmpty.Visibility = tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TasksEmpty.Text = all.Count > 0 ? "Todo hecho. No queda nada por hacer aquí."
            : _day is null ? "La bandeja está vacía. Lo que añadas aquí espera sin día."
            : past ? "Ese día no quedó ninguna tarea apuntada."
            : _day == _today ? "Nada para hoy. Escribe abajo tu primera tarea."
            : "Nada planificado. Lo que añadas aquí te esperará ese día.";
        AddBox.Visibility = past ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AddBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) { HideAddError(); return; }
        e.Handled = true;
        string line = AddBox.Text;
        if (line.Trim().Length == 0) return;
        string? error = Do((d, now) => Planner.AddFromLine(d, line, _day, now), undoable: true);
        if (error == null) AddBox.Clear();
        else { AddError.Text = error; AddError.Visibility = Visibility.Visible; }
    }

    private void TaskTab_Click(object sender, RoutedEventArgs e)
    {
        _showDone = sender == TabDone;
        RenderTasks();
    }

    // ---------- Semana: una columna por día ----------

    private const int WeekDays = 4;

    private void RenderWeekView()
    {
        if (!_weekView) { WeekColumns.ItemsSource = null; return; }
        var now = _clock.Now;
        var columns = new List<WeekColumn>();
        foreach (DateOnly? day in new DateOnly?[] { null }.Concat(Enumerable.Range(0, WeekDays).Select(i => (DateOnly?)_today.AddDays(i))))
        {
            var rows = Planner.ForDay(_data, day, now).Where(t => !t.Done).Select(t => TaskRow.From(t, _data, now, canEdit: true)).ToList();
            string title = day is { } d && d.DayNumber - _today.DayNumber > 1 ? Capital(d.ToString("dddd d", Texts.Culture)) : Planner.DayName(day, _today);
            columns.Add(new WeekColumn(title, rows.Count > 0 ? rows.Count.ToString() : "", day, day == _today, rows));
        }
        WeekColumns.ItemsSource = columns;
        var last = _today.AddDays(WeekDays - 1);
        WeekRange.Text = last.Month == _today.Month
            ? $"Del {_today.Day} al {last.ToString("d 'de' MMMM", Texts.Culture)}"
            : $"Del {_today.ToString("d 'de' MMMM", Texts.Culture)} al {last.ToString("d 'de' MMMM", Texts.Culture)}";
    }

    private void View_Click(object sender, RoutedEventArgs e) => ShowWeek(sender == ViewWeek);

    internal void ShowWeek(bool week)
    {
        _weekView = week;
        _detailId = null;
        Refresh();
        ApplyMode(animate: false);
    }

    private void WeekAdd_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var box = (TextBox)sender;
        if (box.Text.Trim().Length == 0 || box.DataContext is not WeekColumn column) return;
        string line = box.Text;
        string? error = Do((d, now) => Planner.AddFromLine(d, line, column.Day, now), undoable: true);
        if (error != null) ShowAlert(new AlertInfo("No se ha podido añadir la tarea", error, [new AlertAction("Vale", () => { }, true)]));
    }

    private void HideAddError() => AddError.Visibility = Visibility.Collapsed;

    private static string IdOf(object sender) => (string)((FrameworkElement)sender).Tag;

    private void TaskCheck_Click(object sender, RoutedEventArgs e)
    {
        string id = IdOf(sender);
        bool done = ((CheckBox)sender).IsChecked == true;
        Do((d, now) =>
        {
            if (done) Planner.Complete(d, id, now);
            else Planner.Reopen(d, id, now);
        }, undoable: true);
    }

    private void TaskFocus_Click(object sender, RoutedEventArgs e) => StartFocus(IdOf(sender));

    private void TaskOpen_Click(object sender, RoutedEventArgs e)
    {
        _detailId = IdOf(sender);
        DetailError.Visibility = Visibility.Collapsed;
        if (_weekView && _data.Tasks.FirstOrDefault(t => t.Id == _detailId) is { } task)
        {
            // El detalle está en la tarjeta de Tareas: se pasa a la vista de día, en el día de la tarea
            _weekView = false;
            _day = task.Day;
            if (!_data.Settings.Cards.Contains("tareas")) _store.Mutate(d => d.Settings.Cards.Insert(0, "tareas"));
            Refresh();
            ApplyMode(animate: false);
            return;
        }
        RenderDetail();
    }

    private void TaskMenu_Click(object sender, RoutedEventArgs e) => OpenTaskMenu(IdOf(sender), (UIElement)sender);

    private void TaskRow_RightClick(object sender, MouseButtonEventArgs e)
    {
        OpenTaskMenu(IdOf(sender), (UIElement)sender);
        e.Handled = true;
    }

    private void OpenTaskMenu(string id, UIElement target)
    {
        var task = _data.Tasks.FirstOrDefault(t => t.Id == id);
        if (task == null || (!_weekView && _day is { } d && d < _today)) return;
        var menu = NewMenu(target);
        var tomorrow = _today.AddDays(1);
        if (task.Done)
        {
            AddItem(menu, "Dejarla sin hacer", () => Do((x, now) => Planner.Reopen(x, id, now), true));
        }
        else
        {
            AddItem(menu, "Centrarte en ella", () => StartFocus(id));
            menu.Items.Add(new Separator());
            if (task.Day != _today) AddItem(menu, "Pasar a hoy", () => Do((x, now) => Planner.Move(x, id, _today, now), true));
            if (task.Day != tomorrow) AddItem(menu, "Pasar a mañana", () => Do((x, now) => Planner.Move(x, id, tomorrow, now), true));
            if (task.Day != null) AddItem(menu, "Dejar en la bandeja", () => Do((x, now) => Planner.Move(x, id, null, now), true));
            menu.Items.Add(new Separator());
            AddItem(menu, "Avisar dentro de 15 minutos", () => Do((x, now) => Planner.Snooze(x, id, 15, now), true));
            AddItem(menu, "Avisar dentro de una hora", () => Do((x, now) => Planner.Snooze(x, id, 60, now), true));
            if (task.RemindAt != null) AddItem(menu, "Quitar el aviso", () => Do((x, now) => Planner.SetReminder(x, id, null, now), true));
            menu.Items.Add(new Separator());
            foreach (var p in new[] { Priority.High, Priority.Medium, Priority.None })
                if (task.Priority != p)
                    AddItem(menu, p == Priority.None ? "Sin prioridad" : "Prioridad " + TaskRow.PriorityName(p).ToLower(Texts.Culture), () => Do((x, _) => Planner.Find(x, id).Priority = p, true));
            menu.Items.Add(new Separator());
            AddItem(menu, "Subir", () => Do((x, _) => Planner.Shift(x, id, -1), true));
            AddItem(menu, "Bajar", () => Do((x, _) => Planner.Shift(x, id, 1), true));
        }
        menu.Items.Add(new Separator());
        AddItem(menu, "Quitar", () => Do((x, now) => Planner.Remove(x, id, now), true));
        menu.IsOpen = true;
    }

    // ---------- Detalle ----------

    private void RenderDetail()
    {
        var task = _detailId == null ? null : _data.Tasks.FirstOrDefault(t => t.Id == _detailId);
        if (task == null)
        {
            _detailId = null;
            Detail.Visibility = Visibility.Collapsed;
            TaskListView.Visibility = Visibility.Visible;
            return;
        }
        TaskListView.Visibility = Visibility.Collapsed;
        Detail.Visibility = Visibility.Visible;

        bool editable = !task.Done;
        foreach (var box in new[] { DetailTitle, DetailRemind, DetailProject, DetailMinutes, DetailDeadline, DetailNotes, DetailStepBox }) box.IsReadOnly = !editable;
        DetailDay.IsEnabled = DetailPriority.IsEnabled = DetailRepeat.IsEnabled = editable;

        SetField(DetailTitle, task.Title);
        SetField(DetailRemind, task.RemindAt is { } r ? (DateOnly.FromDateTime(r) == task.Day ? Texts.Time(r) : $"{r:yyyy-MM-dd} {Texts.Time(r)}") : "");
        SetField(DetailProject, task.Project ?? "");
        SetField(DetailMinutes, task.LimitMinutes > 0 ? task.LimitMinutes.ToString(CultureInfo.InvariantCulture) : "");
        SetField(DetailDeadline, task.Deadline is { } dl ? dl.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "");
        SetField(DetailNotes, task.Notes);
        DetailDay.Content = Planner.DayName(task.Day, _today);
        DetailPriority.Content = TaskRow.PriorityName(task.Priority);
        DetailRepeat.Content = TaskRow.RepeatName(task.Repeat);
        DetailSteps.ItemsSource = task.Steps.Select((s, i) => new StepRow(i, s.Title, s.Done)).ToList();

        var info = new List<string>();
        if (task.Done && task.CompletedAt is { } c) info.Add($"Hecha el {c.ToString("d 'de' MMMM", Texts.Culture)} a las {Texts.Time(c)}. Para cambiarla, vuelve a dejarla sin hacer.");
        if (task.FocusSeconds >= 60) info.Add($"Le has dedicado {Texts.Duration(task.FocusSeconds)} de foco.");
        DetailInfo.Text = string.Join(" ", info);
        DetailInfo.Visibility = info.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // No se pisa lo que se está escribiendo.
    private static void SetField(TextBox box, string value)
    {
        if (!box.IsKeyboardFocused && box.Text != value) box.Text = value;
    }

    private void CloseDetail()
    {
        _detailId = null;
        RenderDetail();
    }

    private void DetailBack_Click(object sender, RoutedEventArgs e) => CloseDetail();

    private void DetailRemove_Click(object sender, RoutedEventArgs e)
    {
        if (_detailId is not { } id) return;
        _detailId = null;
        Do((d, now) => Planner.Remove(d, id, now), undoable: true);
    }

    private void DetailField_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        SaveDetailField((TextBox)sender);
    }

    private void DetailField_Changed(object sender, RoutedEventArgs e) => SaveDetailField((TextBox)sender);

    private void SaveDetailField(TextBox box)
    {
        if (_detailId is not { } id || box.IsReadOnly) return;
        string text = box.Text.Trim();
        string? error = Do((d, now) =>
        {
            var t = Planner.Find(d, id);
            var today = DateOnly.FromDateTime(now);
            if (box == DetailTitle)
            {
                if (text.Length == 0) throw new UserException("El título no puede quedar vacío.");
                t.Title = text;
            }
            else if (box == DetailRemind)
            {
                if (text.Length == 0) { Planner.SetReminder(d, id, null, now); return; }
                var e = QuickAdd.Parse(text, today);
                if (e.Time is not { } time || e.Title.Length > 0) throw new UserException("No se entiende esa hora. Prueba 17:30, 5 pm o «mañana 9:00».");
                var day = e.DayGiven && e.Day != null ? e.Day.Value : t.Day ?? today;
                Planner.SetReminder(d, id, day.ToDateTime(time), now);
            }
            else if (box == DetailProject) t.Project = Planner.MatchProject(d, text.TrimStart('#'));
            else if (box == DetailMinutes)
            {
                if (text.Length == 0) { t.LimitMinutes = 0; return; }
                if (!int.TryParse(text, out int m) || m is < 1 or > 600) throw new UserException("Los minutos van de 1 a 600.");
                t.LimitMinutes = m;
            }
            else if (box == DetailDeadline)
            {
                if (text.Length == 0) { t.Deadline = null; return; }
                var e = QuickAdd.Parse(text, today);
                if (!e.DayGiven || e.Day is null || e.Title.Length > 0) throw new UserException("No se entiende esa fecha. Prueba viernes, 20/11 o 2026-11-20.");
                t.Deadline = e.Day;
            }
            else if (box == DetailNotes) t.Notes = box.Text.TrimEnd();
        }, undoable: true);
        DetailError.Text = error ?? "";
        DetailError.Visibility = error == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void DetailDay_Click(object sender, RoutedEventArgs e)
    {
        if (_detailId is not { } id) return;
        var menu = NewMenu(DetailDay);
        for (int i = 0; i < 7; i++)
        {
            var day = _today.AddDays(i);
            AddItem(menu, i < 2 ? Planner.DayName(day, _today) : Capital(day.ToString("dddd d", Texts.Culture)), () => MoveDetail(id, day));
        }
        menu.Items.Add(new Separator());
        AddItem(menu, "Bandeja, sin día", () => MoveDetail(id, null));
        menu.IsOpen = true;
    }

    private void MoveDetail(string id, DateOnly? day)
    {
        string? error = Do((d, now) => Planner.Move(d, id, day, now), undoable: true);
        DetailError.Text = error ?? "";
        DetailError.Visibility = error == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void DetailPriority_Click(object sender, RoutedEventArgs e)
    {
        if (_detailId is not { } id) return;
        Do((d, _) =>
        {
            var t = Planner.Find(d, id);
            t.Priority = t.Priority switch { Priority.None => Priority.High, Priority.High => Priority.Medium, Priority.Medium => Priority.Low, _ => Priority.None };
        }, undoable: true);
    }

    private void DetailRepeat_Click(object sender, RoutedEventArgs e)
    {
        if (_detailId is not { } id) return;
        string? error = Do((d, _) =>
        {
            var t = Planner.Find(d, id);
            if (t.Day is null) throw new UserException("En la bandeja no hay día. Elige uno antes de ponerle una repetición.");
            t.Repeat = t.Repeat switch { Repeat.None => Repeat.Daily, Repeat.Daily => Repeat.Weekdays, Repeat.Weekdays => Repeat.Weekly, Repeat.Weekly => Repeat.Monthly, _ => Repeat.None };
        }, undoable: true);
        DetailError.Text = error ?? "";
        DetailError.Visibility = error == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void StepBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _detailId is not { } id) return;
        e.Handled = true;
        string title = DetailStepBox.Text.Trim();
        if (title.Length == 0 || DetailStepBox.IsReadOnly) return;
        Do((d, _) => Planner.Find(d, id).Steps.Add(new Step { Title = title }), undoable: true);
        DetailStepBox.Clear();
    }

    private void StepCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_detailId is not { } id) return;
        int index = (int)((FrameworkElement)sender).Tag;
        bool done = ((CheckBox)sender).IsChecked == true;
        Do((d, _) =>
        {
            var t = Planner.Find(d, id);
            if (!t.Done && index < t.Steps.Count) t.Steps[index].Done = done;
        }, undoable: true);
    }

    private void StepRemove_Click(object sender, RoutedEventArgs e)
    {
        if (_detailId is not { } id) return;
        int index = (int)((FrameworkElement)sender).Tag;
        Do((d, _) =>
        {
            var t = Planner.Find(d, id);
            if (!t.Done && index < t.Steps.Count) t.Steps.RemoveAt(index);
        }, undoable: true);
    }
}
