namespace Worknotch.Core;

/// <summary>
/// Las operaciones sobre las tareas. Trabajan sobre un <see cref="AppData"/> ya cargado: quien llama
/// se encarga de guardarlo (<see cref="Store.Mutate"/>). Los errores que puede corregir quien usa la
/// app salen como <see cref="UserException"/>, con un texto que ya se puede enseñar.
/// </summary>
public static class Planner
{
    private const string IdChars = "abcdefghjkmnpqrstuvwxyz23456789";

    public static string NewId(AppData data)
    {
        while (true)
        {
            string id = string.Create(5, 0, (s, _) =>
            {
                for (int i = 0; i < s.Length; i++) s[i] = IdChars[Random.Shared.Next(IdChars.Length)];
            });
            if (data.Tasks.All(t => t.Id != id) && data.Clips.All(c => c.Id != id)) return id;
        }
    }

    /// <summary>Añade una tarea a partir de una línea escrita. Sin día en la línea, va a <paramref name="defaultDay"/>.</summary>
    public static TaskItem AddFromLine(AppData data, string line, DateOnly? defaultDay, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var e = QuickAdd.Parse(line, today);
        if (e.Title.Length == 0) throw new UserException("Escribe qué hay que hacer.");
        var day = e.DayGiven ? e.Day : defaultDay;
        if (day is { } d && d < today) throw new UserException("Solo se pueden añadir tareas a hoy o a un día futuro.");
        // Una hora o una repetición necesitan día: sin él, la tarea va a hoy.
        if (day is null && (e.Time != null || e.Repeat != Repeat.None)) day = today;
        var task = new TaskItem
        {
            Id = NewId(data),
            Title = e.Title,
            Day = day,
            Priority = e.Priority,
            Project = MatchProject(data, e.Project),
            Repeat = e.Repeat,
            LimitMinutes = e.Minutes,
            Created = now,
        };
        if (e.Time is { } t && day is { } dd) task.RemindAt = dd.ToDateTime(t);
        if (task.RemindAt is { } r && r <= now) task.ReminderShown = true; // la hora ya ha pasado: no avisa
        data.Tasks.Add(task);
        return task;
    }

    /// <summary>Devuelve el proyecto ya existente que coincide sin mirar mayúsculas, o crea el nuevo.</summary>
    public static string? MatchProject(AppData data, string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        string? found = data.Projects.FirstOrDefault(p => string.Equals(p, name, StringComparison.CurrentCultureIgnoreCase));
        if (found != null) return found;
        data.Projects.Add(name);
        return name;
    }

    public static TaskItem Find(AppData data, string id) =>
        data.Tasks.FirstOrDefault(t => t.Id == id) ?? throw new UserException($"No hay ninguna tarea con el id {id}.");

    /// <summary>Marca una tarea como hecha. Si se repite, deja ya planificada la siguiente y la devuelve.</summary>
    public static TaskItem? Complete(AppData data, string id, DateTime now)
    {
        var task = Find(data, id);
        if (task.Done) return null;
        if (data.Timer.Active && data.Timer.TaskId == id) FocusTimer.Stop(data, now);
        task.Done = true;
        task.CompletedAt = now;
        if (task.Repeat == Repeat.None) return null;

        var from = task.Day ?? DateOnly.FromDateTime(now);
        var today = DateOnly.FromDateTime(now);
        var next = NextOccurrence(from, task.Repeat);
        while (next <= today) next = NextOccurrence(next, task.Repeat);
        var copy = new TaskItem
        {
            Id = NewId(data),
            Title = task.Title,
            Day = next,
            Priority = task.Priority,
            Project = task.Project,
            Notes = task.Notes,
            Steps = task.Steps.Select(s => new Step { Title = s.Title }).ToList(),
            Repeat = task.Repeat,
            LimitMinutes = task.LimitMinutes,
            Created = now,
        };
        if (task.RemindAt is { } r) copy.RemindAt = next.ToDateTime(TimeOnly.FromDateTime(r));
        // La hecha deja de repetirse: la repetición sigue en la copia.
        task.Repeat = Repeat.None;
        data.Tasks.Add(copy);
        return copy;
    }

    public static void Reopen(AppData data, string id, DateTime now)
    {
        var task = Find(data, id);
        if (!task.Done) return;
        var today = DateOnly.FromDateTime(now);
        if (task.CompletedAt is { } c && DateOnly.FromDateTime(c) != today)
            throw new UserException("Los días pasados solo se pueden consultar.");
        task.Done = false;
        task.CompletedAt = null;
    }

    public static DateOnly NextOccurrence(DateOnly from, Repeat repeat) => repeat switch
    {
        Repeat.Daily => from.AddDays(1),
        Repeat.Weekdays => from.DayOfWeek switch
        {
            DayOfWeek.Friday => from.AddDays(3),
            DayOfWeek.Saturday => from.AddDays(2),
            _ => from.AddDays(1),
        },
        Repeat.Weekly => from.AddDays(7),
        Repeat.Monthly => from.AddMonths(1),
        _ => from,
    };

    /// <summary>Mueve una tarea sin hacer a otro día, o a la bandeja con <paramref name="day"/> vacío.</summary>
    public static void Move(AppData data, string id, DateOnly? day, DateTime now)
    {
        var task = Find(data, id);
        var today = DateOnly.FromDateTime(now);
        if (task.Done) throw new UserException($"«{task.Title}» ya está hecha. Vuelve a abrirla antes de moverla.");
        if (day is { } d && d < today) throw new UserException("Las tareas solo se pueden mover a hoy o a un día futuro.");
        if (task.Day == day) return;
        if (day is { } nd)
        {
            if (task.RemindAt is { } r)
            {
                task.RemindAt = nd.ToDateTime(TimeOnly.FromDateTime(r));
                task.ReminderShown = task.RemindAt <= now;
            }
        }
        else
        {
            // En la bandeja no hay día: tampoco aviso ni repetición.
            task.RemindAt = null;
            task.ReminderShown = false;
            task.Repeat = Repeat.None;
        }
        task.Day = day;
        task.CarriedFrom = null;
        // Al final de su nuevo día
        data.Tasks.Remove(task);
        data.Tasks.Add(task);
    }

    public static void Remove(AppData data, string id, DateTime now)
    {
        var task = Find(data, id);
        if (data.Timer.Active && data.Timer.TaskId == id) FocusTimer.Stop(data, now);
        data.Tasks.Remove(task);
    }

    /// <summary>Sube o baja una tarea entre las que comparten día con ella.</summary>
    public static void Shift(AppData data, string id, int delta)
    {
        var task = Find(data, id);
        var peers = data.Tasks.Where(t => t.Day == task.Day && t.Done == task.Done).ToList();
        int i = peers.IndexOf(task);
        int j = i + delta;
        if (j < 0 || j >= peers.Count) return;
        int a = data.Tasks.IndexOf(task), b = data.Tasks.IndexOf(peers[j]);
        (data.Tasks[a], data.Tasks[b]) = (data.Tasks[b], data.Tasks[a]);
    }

    /// <summary>Pone un aviso a una hora. Con <paramref name="when"/> vacío, lo quita.</summary>
    public static void SetReminder(AppData data, string id, DateTime? when, DateTime now)
    {
        var task = Find(data, id);
        if (when is null) { task.RemindAt = null; task.ReminderShown = false; return; }
        if (when <= now) throw new UserException("Esa hora ya ha pasado. Elige una más tarde.");
        task.Day ??= DateOnly.FromDateTime(when.Value);
        task.RemindAt = when;
        task.ReminderShown = false;
    }

    public static void Snooze(AppData data, string id, int minutes, DateTime now)
    {
        var task = Find(data, id);
        if (task.Done) throw new UserException($"«{task.Title}» ya está hecha.");
        task.Day ??= DateOnly.FromDateTime(now);
        task.RemindAt = now.AddMinutes(Math.Clamp(minutes, 1, 24 * 60));
        task.ReminderShown = false;
    }

    /// <summary>Los avisos cuya hora ha llegado y aún no se han enseñado. Los deja marcados como enseñados.</summary>
    public static List<TaskItem> TakeDueReminders(AppData data, DateTime now)
    {
        var due = data.Tasks.Where(t => !t.Done && !t.ReminderShown && t.RemindAt is { } r && r <= now).ToList();
        foreach (var t in due) t.ReminderShown = true;
        return due;
    }

    /// <summary>
    /// El paso de día: lo que quedó sin hacer en días anteriores se trae a hoy (si así está elegido en
    /// Ajustes), recordando de qué día viene. Devuelve cuántas tareas ha traído.
    /// </summary>
    public static int CatchUp(AppData data, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        if (data.LastDay == today) return 0;
        data.LastDay = today;
        if (!data.Settings.CarryForward) return 0;
        int moved = 0;
        foreach (var t in data.Tasks.Where(t => !t.Done && t.Day is { } d && d < today))
        {
            t.CarriedFrom ??= t.Day;
            t.Day = today;
            if (t.RemindAt is { } r && r < now) t.ReminderShown = true;
            moved++;
        }
        // Recortes: se guardan dos semanas
        data.Clips.RemoveAll(c => c.Added < now.AddDays(-14));
        return moved;
    }

    /// <summary>Las tareas de un día, en su orden: primero las pendientes y después las hechas.</summary>
    public static List<TaskItem> ForDay(AppData data, DateOnly? day, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        IEnumerable<TaskItem> q;
        if (day is null) q = data.Tasks.Where(t => t.Day is null && !t.Done);
        else if (day < today)
            // Un día pasado: lo que se hizo ese día y lo que se quedó allí sin hacer
            q = data.Tasks.Where(t => t.Done ? t.CompletedAt is { } c && DateOnly.FromDateTime(c) == day : t.Day == day);
        else if (day == today)
            q = data.Tasks.Where(t => t.Done ? t.CompletedAt is { } c && DateOnly.FromDateTime(c) == day : t.Day == day);
        else q = data.Tasks.Where(t => t.Day == day && !t.Done);
        return q.OrderBy(t => t.Done).ToList();
    }

    public static string DayName(DateOnly? day, DateOnly today)
    {
        if (day is not { } d) return "Bandeja";
        int diff = d.DayNumber - today.DayNumber;
        return diff switch
        {
            0 => "Hoy",
            1 => "Mañana",
            -1 => "Ayer",
            _ => Capitalize(d.ToString(diff is > 1 and < 7 ? "dddd" : "ddd d MMM", Texts.Culture)).Replace(".", ""),
        };
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Texts.Culture) + s[1..];
}

/// <summary>Formatos comunes: siempre en español de España, sea cual sea el idioma de Windows.</summary>
public static class Texts
{
    public static readonly System.Globalization.CultureInfo Culture = System.Globalization.CultureInfo.GetCultureInfo("es-ES");

    public static string Time(DateTime t) => t.ToString("H:mm", Culture);

    public static string Clock(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return seconds >= 3600
            ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}"
            : $"{seconds / 60:00}:{seconds % 60:00}";
    }

    public static string Duration(int seconds)
    {
        int m = (int)Math.Round(seconds / 60.0);
        if (m < 60) return $"{m} min";
        return m % 60 == 0 ? $"{m / 60} h" : $"{m / 60} h {m % 60} min";
    }

    public static string Plural(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n} {many}";
}
