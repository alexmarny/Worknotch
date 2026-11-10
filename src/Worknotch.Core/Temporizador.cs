namespace Worknotch.Core;

/// <summary>
/// El temporizador de foco. No cuenta con un reloj propio: guarda cuándo empezó el tramo en marcha y
/// lo ya acumulado, así que sigue bien tras cerrar la app o suspender el equipo.
/// </summary>
public static class FocusTimer
{
    public static int Elapsed(TimerState t, DateTime now)
    {
        if (!t.Active) return 0;
        int running = t.Running && t.StartedAt is { } s ? Math.Max(0, (int)(now - s).TotalSeconds) : 0;
        return t.AccumulatedSeconds + running;
    }

    /// <summary>Lo que falta en una cuenta atrás (nunca menos de 0). En cronómetro, lo transcurrido.</summary>
    public static int Display(TimerState t, DateTime now) =>
        t.Mode == TimerMode.Countdown ? Math.Max(0, t.TargetSeconds - Elapsed(t, now)) : Elapsed(t, now);

    public static bool Finished(TimerState t, DateTime now) =>
        t.Active && t.Mode == TimerMode.Countdown && Elapsed(t, now) >= t.TargetSeconds;

    /// <summary>Empieza a contar. Con tarea, usa su límite; sin él, <paramref name="minutes"/> o la duración por defecto.</summary>
    public static void Start(AppData data, string? taskId, int? minutes, TimerMode mode, DateTime now)
    {
        if (data.Timer.Active) Stop(data, now);
        int target = minutes ?? 0;
        if (taskId != null)
        {
            var task = Planner.Find(data, taskId);
            if (task.Done) throw new UserException($"«{task.Title}» ya está hecha.");
            if (task.Day is null) task.Day = DateOnly.FromDateTime(now);
            if (target == 0) target = task.LimitMinutes;
        }
        if (target == 0) target = data.Settings.FocusMinutes;
        data.Timer = new TimerState
        {
            Active = true,
            Running = true,
            Mode = mode,
            TaskId = taskId,
            TargetSeconds = Math.Clamp(target, 1, 600) * 60,
            StartedAt = now,
        };
    }

    public static void Pause(AppData data, DateTime now)
    {
        var t = data.Timer;
        if (!t.Active || !t.Running) throw new UserException("No hay ningún temporizador en marcha.");
        Log(data, now);
        t.AccumulatedSeconds = Elapsed(t, now);
        t.Running = false;
        t.StartedAt = null;
    }

    public static void Resume(AppData data, DateTime now)
    {
        var t = data.Timer;
        if (!t.Active || t.Running) throw new UserException("No hay ningún temporizador en pausa.");
        t.Running = true;
        t.StartedAt = now;
    }

    /// <summary>Añade minutos a la cuenta atrás. El tiempo ya transcurrido se conserva.</summary>
    public static void AddMinutes(AppData data, int minutes)
    {
        var t = data.Timer;
        if (!t.Active) throw new UserException("No hay ningún temporizador activo.");
        t.TargetSeconds = Math.Clamp(t.TargetSeconds + minutes * 60, 60, 600 * 60);
        t.EndShown = false;
    }

    /// <summary>Para el temporizador sin completar la tarea. El tiempo de foco queda apuntado.</summary>
    public static void Stop(AppData data, DateTime now)
    {
        if (!data.Timer.Active) return;
        if (data.Timer.Running) Log(data, now);
        data.Timer = new TimerState();
    }

    /// <summary>Si la cuenta atrás ha terminado y aún no se ha avisado, lo marca y devuelve true.</summary>
    public static bool TakeEnd(AppData data, DateTime now)
    {
        var t = data.Timer;
        if (!Finished(t, now) || t.EndShown) return false;
        t.EndShown = true;
        return true;
    }

    // Apunta el tramo en marcha como sesión y lo suma a su tarea.
    private static void Log(AppData data, DateTime now)
    {
        var t = data.Timer;
        if (t.StartedAt is not { } start) return;
        int seconds = Math.Max(0, (int)(now - start).TotalSeconds);
        // Un equipo suspendido con el temporizador en marcha no cuenta horas de foco
        seconds = Math.Min(seconds, Math.Max(t.TargetSeconds, 4 * 3600));
        if (seconds < 1) return;
        data.Sessions.Add(new FocusSession { Start = start, Seconds = seconds, TaskId = t.TaskId });
        if (t.TaskId != null && data.Tasks.FirstOrDefault(x => x.Id == t.TaskId) is { } task) task.FocusSeconds += seconds;
    }
}
