namespace Worknotch.Core;

public sealed record DayStats(DateOnly Day, int Planned, int Completed, int FocusSeconds);

public sealed record WeekStats(
    IReadOnlyList<DayStats> Days,
    int Completed,
    int CompletedBefore,
    int FocusSeconds,
    int FocusSecondsBefore,
    int Streak,
    int? PeakHour);

/// <summary>La semana de un vistazo: tareas, foco, racha y hora punta.</summary>
public static class Stats
{
    public static WeekStats Week(AppData data, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var days = Enumerable.Range(0, 7).Select(i => Day(data, today.AddDays(i - 6), now)).ToList();
        var before = Enumerable.Range(0, 7).Select(i => Day(data, today.AddDays(i - 13), now)).ToList();
        return new WeekStats(
            days,
            days.Sum(d => d.Completed),
            before.Sum(d => d.Completed),
            days.Sum(d => d.FocusSeconds),
            before.Sum(d => d.FocusSeconds),
            Streak(data, today),
            PeakHour(data, today));
    }

    public static DayStats Day(AppData data, DateOnly day, DateTime now)
    {
        int completed = data.Tasks.Count(t => t.Done && t.CompletedAt is { } c && DateOnly.FromDateTime(c) == day);
        int pending = data.Tasks.Count(t => !t.Done && t.Day == day);
        int focus = data.Sessions.Where(s => DateOnly.FromDateTime(s.Start) == day).Sum(s => s.Seconds);
        if (day == DateOnly.FromDateTime(now) && data.Timer is { Active: true, Running: true, StartedAt: { } st })
            focus += Math.Max(0, (int)(now - st).TotalSeconds);
        return new DayStats(day, completed + pending, completed, focus);
    }

    /// <summary>
    /// Días seguidos con al menos una tarea hecha, hasta hoy. Hoy no rompe la racha si todavía no hay
    /// nada hecho; los fines de semana tampoco, salvo que se elija contarlos.
    /// </summary>
    public static int Streak(AppData data, DateOnly today)
    {
        var doneDays = data.Tasks
            .Where(t => t.Done && t.CompletedAt != null)
            .Select(t => DateOnly.FromDateTime(t.CompletedAt!.Value))
            .ToHashSet();
        bool weekends = data.Settings.WeekendsInStreak;
        int streak = 0;
        for (var d = today; streak < 3660; d = d.AddDays(-1))
        {
            if (doneDays.Contains(d)) { streak++; continue; }
            if (d == today) continue;
            if (!weekends && d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            break;
        }
        return streak;
    }

    /// <summary>La hora del día con más foco en los últimos 30 días; sin foco, con más tareas hechas.</summary>
    public static int? PeakHour(AppData data, DateOnly today)
    {
        var since = today.AddDays(-30);
        var byHour = new int[24];
        foreach (var s in data.Sessions.Where(s => DateOnly.FromDateTime(s.Start) >= since)) byHour[s.Start.Hour] += s.Seconds;
        if (byHour.Max() == 0)
            foreach (var t in data.Tasks.Where(t => t.Done && t.CompletedAt is { } c && DateOnly.FromDateTime(c) >= since))
                byHour[t.CompletedAt!.Value.Hour]++;
        int max = byHour.Max();
        return max == 0 ? null : Array.IndexOf(byHour, max);
    }

    /// <summary>Una frase sobre la semana, para la tarjeta y para Claude.</summary>
    public static string Sentence(WeekStats w)
    {
        if (w.Completed == 0 && w.FocusSeconds == 0) return "Termina una tarea o empieza una sesión de foco y tu semana irá tomando forma aquí.";
        string tasks = Texts.Plural(w.Completed, "tarea hecha", "tareas hechas");
        string cmp = w.CompletedBefore == 0 ? "" :
            w.Completed > w.CompletedBefore ? $", más que la semana anterior ({w.CompletedBefore})" :
            w.Completed < w.CompletedBefore ? $", menos que la semana anterior ({w.CompletedBefore})" :
            ", igual que la semana anterior";
        string focus = w.FocusSeconds < 60 ? "" : $" y {Texts.Duration(w.FocusSeconds)} de foco";
        return $"{tasks} en siete días{cmp}{focus}.";
    }
}
