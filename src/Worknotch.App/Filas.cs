using System.Windows.Media;

namespace Worknotch.App;

public enum FactKind { Normal, Accent, Alert }

/// <summary>Un dato de una tarea en la lista: la hora del aviso, el proyecto…</summary>
public sealed record Fact(string Text, FactKind Kind = FactKind.Normal);

public sealed class TaskRow
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public bool Done { get; init; }
    public bool CanEdit { get; init; }
    public bool InFocus { get; init; }
    public Priority Priority { get; init; }
    public List<Fact> Facts { get; init; } = [];
    public string CheckName => Done ? $"Dejar sin hacer: {Title}" : $"Marcar como hecha: {Title}";

    public static TaskRow From(TaskItem t, AppData data, DateTime now, bool canEdit)
    {
        var today = DateOnly.FromDateTime(now);
        var facts = new List<Fact>();
        if (t.Done && t.CompletedAt is { } c) facts.Add(new Fact($"Hecha a las {Texts.Time(c)}"));
        if (!t.Done && t.RemindAt is { } r)
            facts.Add(r < now ? new Fact($"Aviso de las {Texts.Time(r)}, pasado", FactKind.Alert) : new Fact(Texts.Time(r), FactKind.Accent));
        if (!t.Done && t.Deadline is { } dl)
            facts.Add(new Fact($"Límite: {Planner.DayName(dl, today).ToLower(Texts.Culture)}", dl <= today ? FactKind.Alert : FactKind.Normal));
        if (t.Project != null) facts.Add(new Fact("#" + t.Project));
        if (t.Steps.Count > 0) facts.Add(new Fact($"{t.Steps.Count(s => s.Done)} de {t.Steps.Count} pasos"));
        if (t.Repeat != Repeat.None) facts.Add(new Fact(RepeatName(t.Repeat)));
        if (t.FocusSeconds >= 60) facts.Add(new Fact($"{Texts.Duration(t.FocusSeconds)} de foco"));
        else if (!t.Done && t.LimitMinutes > 0) facts.Add(new Fact($"{t.LimitMinutes} min"));
        if (!t.Done && t.CarriedFrom is { } from) facts.Add(new Fact($"Viene de {CarriedName(from, today)}"));
        return new TaskRow
        {
            Id = t.Id,
            Title = t.Title,
            Done = t.Done,
            CanEdit = canEdit,
            InFocus = data.Timer.Active && data.Timer.TaskId == t.Id,
            Priority = t.Done ? Priority.None : t.Priority,
            Facts = facts,
        };
    }

    public static string RepeatName(Repeat r) => r switch
    {
        Repeat.Daily => "Cada día",
        Repeat.Weekdays => "Días laborables",
        Repeat.Weekly => "Cada semana",
        Repeat.Monthly => "Cada mes",
        _ => "No se repite",
    };

    public static string PriorityName(Priority p) => p switch
    {
        Priority.High => "Alta",
        Priority.Medium => "Media",
        Priority.Low => "Baja",
        _ => "Ninguna",
    };

    private static string CarriedName(DateOnly from, DateOnly today)
    {
        int ago = today.DayNumber - from.DayNumber;
        if (ago == 1) return "ayer";
        return ago < 7 ? "el " + from.ToString("dddd", Texts.Culture) : "el " + from.ToString("d 'de' MMMM", Texts.Culture);
    }
}

public sealed record StepRow(int Index, string Title, bool Done);

public sealed class ClipRow
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Sub { get; init; }
    public ImageSource? Thumb { get; init; }
    public bool HasThumb => Thumb != null;
}

public sealed record DayBar(string Label, double TasksHeight, double FocusHeight, string Tip, bool IsToday);
