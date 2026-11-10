using System.Windows;

namespace Worknotch.App;

// La tarjeta de foco y la pastilla.
public partial class NotchWindow
{
    private void RenderFocus()
    {
        var now = _clock.Now;
        var t = _data.Timer;
        var s = _data.Settings;
        string clock;
        if (t.Active)
        {
            var task = t.TaskId == null ? null : _data.Tasks.FirstOrDefault(x => x.Id == t.TaskId);
            clock = Texts.Clock(FocusTimer.Display(t, now));
            FocusTask.Text = task?.Title ?? "Solo foco";
            FocusHint.Text = FocusTimer.Finished(t, now) ? "Tiempo cumplido." : t.Running ? "" : "En pausa.";
            FocusPause.Content = t.Running ? "Pausar" : "Reanudar";
            FocusDone.Visibility = task != null ? Visibility.Visible : Visibility.Collapsed;
            FocusPlus.Visibility = t.Mode == TimerMode.Countdown ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            clock = Texts.Clock(s.Stopwatch ? 0 : s.FocusMinutes * 60);
            FocusTask.Text = "Solo foco";
            FocusHint.Text = "Para centrarte en una tarea, pulsa su triángulo.";
            FocusMinutes.Text = $"{s.FocusMinutes} min";
            FocusLength.Visibility = s.Stopwatch ? Visibility.Collapsed : Visibility.Visible;
            FocusMode.Content = s.Stopwatch ? "Usar cuenta atrás" : "Usar cronómetro";
        }
        FocusHint.Visibility = FocusHint.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        FocusIdle.Visibility = t.Active ? Visibility.Collapsed : Visibility.Visible;
        FocusActive.Visibility = t.Active ? Visibility.Visible : Visibility.Collapsed;

        // Con horas («1:05:00») los puntos se hacen algo más pequeños para que quepa
        bool wide = clock.Length > 5;
        Clock.Dot = wide ? 4.5 : 6;
        Clock.Gap = wide ? 1.5 : 2;
        Clock.Text = clock;

        int today = Stats.Day(_data, _today, now).FocusSeconds;
        FocusToday.Text = today >= 60 ? $"Hoy, {Texts.Duration(today)}" : "";
    }

    private void RenderPill()
    {
        var now = _clock.Now;
        var t = _data.Timer;
        // La pequeña no lleva texto: solo el reloj de puntos mientras hay foco
        bool small = _data.Settings.Pill != PillChoice.Normal;
        PillText.Visibility = small ? Visibility.Collapsed : Visibility.Visible;
        PillClock.Margin = new Thickness(0, 0, small ? 0 : 8, 0);
        PillClock.Dot = small ? 1.5 : 2; // que le quede aire arriba y abajo
        PillClock.Gap = small ? 0.75 : 1;
        if (t.Active)
        {
            PillClock.Visibility = Visibility.Visible;
            PillClock.Text = Texts.Clock(FocusTimer.Display(t, now));
            string title = t.TaskId != null && _data.Tasks.FirstOrDefault(x => x.Id == t.TaskId) is { } task ? task.Title : "Foco";
            PillText.Text = t.Running ? title : "En pausa";
            return;
        }
        PillClock.Visibility = Visibility.Collapsed;
        var today = Planner.ForDay(_data, _today, now);
        var next = today.Where(x => !x.Done && x.RemindAt is { } r && r > now).OrderBy(x => x.RemindAt).FirstOrDefault();
        int pending = today.Count(x => !x.Done);
        PillText.Text = next != null ? $"{Texts.Time(next.RemindAt!.Value)}  {next.Title}"
            : pending > 0 ? $"{pending} por hacer hoy"
            : today.Count > 0 ? "Todo hecho por hoy"
            : "Nada para hoy";
    }

    private void StartFocus(string? taskId)
    {
        var mode = taskId == null && _data.Settings.Stopwatch ? TimerMode.Stopwatch : TimerMode.Countdown;
        Do((d, now) => FocusTimer.Start(d, taskId, null, mode, now));
    }

    private void FocusStart_Click(object sender, RoutedEventArgs e) => StartFocus(null);

    private void FocusMode_Click(object sender, RoutedEventArgs e) => Do((d, _) => d.Settings.Stopwatch = !d.Settings.Stopwatch);

    private void FocusLess_Click(object sender, RoutedEventArgs e) => Do((d, _) => d.Settings.FocusMinutes = Math.Max(5, d.Settings.FocusMinutes - 5));

    private void FocusMore_Click(object sender, RoutedEventArgs e) => Do((d, _) => d.Settings.FocusMinutes = Math.Min(180, d.Settings.FocusMinutes + 5));

    private void FocusPause_Click(object sender, RoutedEventArgs e) => Do((d, now) =>
    {
        if (d.Timer.Running) FocusTimer.Pause(d, now);
        else FocusTimer.Resume(d, now);
    });

    private void FocusPlus_Click(object sender, RoutedEventArgs e) => Do((d, _) => FocusTimer.AddMinutes(d, 5));

    private void FocusStop_Click(object sender, RoutedEventArgs e) => Do((d, now) => FocusTimer.Stop(d, now));

    private void FocusDone_Click(object sender, RoutedEventArgs e) => Do((d, now) =>
    {
        if (d.Timer.TaskId is { } id) Planner.Complete(d, id, now);
        else FocusTimer.Stop(d, now);
    }, undoable: true);
}
