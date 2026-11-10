using System.Text.Json.Serialization;

namespace Worknotch.Core;

public enum Priority { None, Low, Medium, High }

public enum Repeat { None, Daily, Weekdays, Weekly, Monthly }

public enum TimerMode { Countdown, Stopwatch }

public enum ClipKind { Text, Link, Image, File }

public enum ThemeChoice { System, Night, Day }

public enum ScreenChoice { Pointer, Primary }

/// <summary>Cómo se ve la pastilla cuando el panel está cerrado.</summary>
public enum PillChoice { Normal, Small, Hidden }

/// <summary>Un paso dentro de una tarea.</summary>
public sealed class Step
{
    public string Title { get; set; } = "";
    public bool Done { get; set; }
}

/// <summary>Una tarea. <see cref="Day"/> vacío quiere decir que espera en la bandeja, sin día.</summary>
public sealed class TaskItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public DateOnly? Day { get; set; }
    public bool Done { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Priority Priority { get; set; }
    public string? Project { get; set; }
    public string Notes { get; set; } = "";
    public List<Step> Steps { get; set; } = [];
    public DateTime? RemindAt { get; set; }
    /// <summary>El aviso de <see cref="RemindAt"/> ya se ha enseñado.</summary>
    public bool ReminderShown { get; set; }
    public Repeat Repeat { get; set; }
    public DateOnly? Deadline { get; set; }
    /// <summary>Minutos de foco que se le quieren dar; 0 usa la duración por defecto.</summary>
    public int LimitMinutes { get; set; }
    public int FocusSeconds { get; set; }
    /// <summary>El día en que se planificó, si se ha traído a hoy por no haberse hecho.</summary>
    public DateOnly? CarriedFrom { get; set; }
    public DateTime Created { get; set; }

    [JsonIgnore]
    public bool InInbox => Day is null;
}

/// <summary>Un tramo de foco ya transcurrido.</summary>
public sealed class FocusSession
{
    public DateTime Start { get; set; }
    public int Seconds { get; set; }
    public string? TaskId { get; set; }
}

/// <summary>El temporizador en marcha o en pausa. Se guarda para que sobreviva a un reinicio.</summary>
public sealed class TimerState
{
    public bool Active { get; set; }
    public bool Running { get; set; }
    public TimerMode Mode { get; set; }
    public string? TaskId { get; set; }
    public int TargetSeconds { get; set; }
    /// <summary>Segundos ya contados en tramos anteriores a <see cref="StartedAt"/>.</summary>
    public int AccumulatedSeconds { get; set; }
    public DateTime? StartedAt { get; set; }
    /// <summary>El aviso de «tiempo cumplido» ya se ha enseñado.</summary>
    public bool EndShown { get; set; }
}

/// <summary>Algo colgado en Recortes: un texto copiado, un enlace, una captura o un archivo.</summary>
public sealed class Clip
{
    public string Id { get; set; } = "";
    public ClipKind Kind { get; set; }
    /// <summary>El texto o el enlace; en capturas y archivos, la ruta.</summary>
    public string Content { get; set; } = "";
    public DateTime Added { get; set; }
}

public sealed class Settings
{
    public ThemeChoice Theme { get; set; } = ThemeChoice.System;
    public ScreenChoice Screen { get; set; } = ScreenChoice.Pointer;
    public PillChoice Pill { get; set; } = PillChoice.Normal;
    /// <summary>Tarjetas visibles, en orden: tareas, foco, notas, recortes, progreso.</summary>
    public List<string> Cards { get; set; } = ["tareas", "foco", "notas", "recortes"];
    public string Hotkey { get; set; } = "Ctrl+Alt+Espacio";
    public int FocusMinutes { get; set; } = 25;
    /// <summary>El foco sin tarea cuenta hacia arriba en vez de hacia atrás.</summary>
    public bool Stopwatch { get; set; }
    public bool Sound { get; set; } = true;
    public bool CarryForward { get; set; } = true;
    public bool WeekendsInStreak { get; set; }
    public bool CatchClipboard { get; set; } = true;
    public bool CatchScreenshots { get; set; } = true;
    public bool CheckUpdates { get; set; } = true;
    public bool WelcomeSeen { get; set; }
}

/// <summary>Todo lo que se guarda.</summary>
public sealed class AppData
{
    public int Version { get; set; } = 1;
    public List<TaskItem> Tasks { get; set; } = [];
    public List<string> Projects { get; set; } = [];
    public string Notepad { get; set; } = "";
    public List<FocusSession> Sessions { get; set; } = [];
    public TimerState Timer { get; set; } = new();
    public List<Clip> Clips { get; set; } = [];
    public Settings Settings { get; set; } = new();
    /// <summary>Último día para el que se ha hecho el paso de día (<see cref="Planner.CatchUp"/>).</summary>
    public DateOnly? LastDay { get; set; }
}

/// <summary>La hora, para poder fijarla en las pruebas.</summary>
public interface IClock
{
    DateTime Now { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}

/// <summary>Un error que se le puede enseñar tal cual a quien usa la app (o a Claude).</summary>
public sealed class UserException(string message) : Exception(message);
