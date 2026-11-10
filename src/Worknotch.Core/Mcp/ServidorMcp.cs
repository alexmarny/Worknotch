using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Worknotch.Core.Mcp;

/// <summary>
/// Servidor MCP por la entrada y la salida estándar (JSON-RPC, un mensaje por línea), para que
/// Claude pueda planificar, completar y cronometrar tareas. Trabaja sobre el mismo archivo que la
/// app, que enseña los cambios en cuanto se guardan.
/// </summary>
public sealed class McpServer(Store store, IClock clock, string version)
{
    private static readonly string[] Protocols = ["2025-06-18", "2025-03-26", "2024-11-05"];

    public async Task RunAsync(TextReader input, TextWriter output)
    {
        while (await input.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string? reply = Handle(line);
            if (reply == null) continue;
            await output.WriteLineAsync(reply);
            await output.FlushAsync();
        }
    }

    /// <summary>Atiende un mensaje. Devuelve la respuesta, o null si era una notificación.</summary>
    public string? Handle(string line)
    {
        JsonNode? msg;
        try { msg = JsonNode.Parse(line); }
        catch (JsonException) { return Error(null, -32700, "Parse error"); }
        if (msg is not JsonObject obj) return Error(null, -32600, "Invalid Request");
        JsonNode? id = obj["id"]?.DeepClone();
        string? method = obj["method"]?.GetValue<string>();
        if (method == null) return null; // una respuesta del cliente
        if (id == null) return null; // notificación: no se contesta

        try
        {
            switch (method)
            {
                case "initialize":
                    string asked = obj["params"]?["protocolVersion"]?.GetValue<string>() ?? "";
                    return Result(id, new JsonObject
                    {
                        ["protocolVersion"] = Protocols.Contains(asked) ? asked : Protocols[0],
                        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                        ["serverInfo"] = new JsonObject { ["name"] = "worknotch", ["version"] = version },
                        ["instructions"] = "Worknotch guarda las tareas, el temporizador de foco y el bloc de notas de quien lo usa. " +
                            "Los días se escriben como yyyy-MM-dd, hoy, mañana o ayer; «bandeja» es para tareas sin día. " +
                            "Añade los títulos tal como te los digan. Los días pasados solo se pueden consultar.",
                    });
                case "ping":
                    return Result(id, new JsonObject());
                case "tools/list":
                    return Result(id, new JsonObject { ["tools"] = McpTools.List() });
                case "tools/call":
                    string name = obj["params"]?["name"]?.GetValue<string>() ?? "";
                    var args = obj["params"]?["arguments"] as JsonObject ?? [];
                    if (!McpTools.Names.Contains(name)) return Error(id, -32602, $"Unknown tool: {name}");
                    try
                    {
                        string text = new McpTools(store, clock).Call(name, args);
                        return Result(id, ToolText(text, false));
                    }
                    catch (UserException e)
                    {
                        return Result(id, ToolText(e.Message, true));
                    }
                default:
                    return Error(id, -32601, $"Method not found: {method}");
            }
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or IOException)
        {
            return Error(id, -32603, e.Message);
        }
    }

    private static JsonObject ToolText(string text, bool isError) => new()
    {
        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
        ["isError"] = isError,
    };

    private static string Result(JsonNode? id, JsonNode result) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result }.ToJsonString(Compact);

    private static string Error(JsonNode? id, int code, string message) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        }.ToJsonString(Compact);

    private static readonly JsonSerializerOptions Compact = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}

/// <summary>Las herramientas que ve Claude y lo que hace cada una.</summary>
public sealed class McpTools(Store store, IClock clock)
{
    private const string DayHelp = "Usa yyyy-MM-dd, hoy, mañana o ayer.";

    private static readonly (string Name, string Description, string Schema)[] Defs =
    [
        ("list_tasks", "Lista las tareas de un día, o de la bandeja (tareas sin día).",
            """{"date":{"type":"string","description":"El día, o bandeja. Por defecto, hoy. Usa yyyy-MM-dd, hoy, mañana o ayer."}}"""),
        ("add_tasks", "Añade una o varias tareas al final de un día, o a la bandeja.",
            """{"titles":{"type":"array","items":{"type":"string"},"description":"Un título por tarea, tal como te lo han dicho."},"date":{"type":"string","description":"El día, o bandeja. Por defecto, hoy. Solo hoy o días futuros."},"priority":{"type":"string","enum":["alta","media","baja"]},"project":{"type":"string","description":"Nombre del proyecto; uno nuevo lo crea."}}|titles"""),
        ("get_task", "Todo sobre una tarea: su día, si está hecha, aviso, prioridad, proyecto, notas, pasos y tiempo de foco.",
            """{"id":{"type":"string","description":"El id de la tarea. list_tasks los enseña."}}|id"""),
        ("update_task", "Cambia una tarea sin hacer: título, aviso, prioridad, proyecto, notas, repetición, fecha límite, minutos o pasos.",
            """{"id":{"type":"string"},"title":{"type":"string","description":"El título nuevo, en una línea."},"remind_at":{"type":"string","description":"Cuándo avisar: 17:30, o 2026-10-30 17:30. Vacío quita el aviso."},"priority":{"type":"string","enum":["alta","media","baja","ninguna"]},"project":{"type":"string","description":"Nombre del proyecto; vacío lo quita."},"notes":{"type":"string"},"repeat":{"type":"string","enum":["diaria","laborables","semanal","mensual","ninguna"]},"deadline":{"type":"string","description":"El día límite, como yyyy-MM-dd; vacío lo quita."},"minutes":{"type":"integer","description":"Minutos de foco que necesita; 0 lo quita."},"steps":{"type":"array","items":{"type":"string"},"description":"Sustituye los pasos por estos."},"done_steps":{"type":"array","items":{"type":"string"},"description":"Títulos de los pasos que se marcan como hechos."}}|id"""),
        ("complete_task", "Marca una tarea como hecha.", """{"id":{"type":"string"}}|id"""),
        ("reopen_task", "Vuelve a dejar sin hacer una tarea hecha hoy.", """{"id":{"type":"string"}}|id"""),
        ("move_task", "Mueve una tarea sin hacer a hoy, a un día futuro o a la bandeja.",
            """{"id":{"type":"string"},"date":{"type":"string","description":"El día al que va, o bandeja. Usa yyyy-MM-dd, hoy o mañana."}}|id,date"""),
        ("remove_task", "Quita una tarea. Se puede deshacer desde Worknotch.", """{"id":{"type":"string"}}|id"""),
        ("snooze_task", "Vuelve a avisar de una tarea dentro de unos minutos.",
            """{"id":{"type":"string"},"minutes":{"type":"integer","description":"Cuántos minutos. Por defecto, 5."}}|id"""),
        ("start_timer", "Empieza una sesión de foco, sobre una tarea de hoy o sin tarea.",
            """{"id":{"type":"string","description":"La tarea en la que centrarse. Opcional."},"minutes":{"type":"integer","description":"Duración de la cuenta atrás en minutos."}}"""),
        ("pause_timer", "Pone en pausa el temporizador de foco en marcha.", "{}"),
        ("resume_timer", "Reanuda el temporizador de foco en pausa.", "{}"),
        ("stop_timer", "Para el temporizador de foco sin completar la tarea.", "{}"),
        ("read_notepad", "Lee el bloc de notas.", "{}"),
        ("write_notepad", "Escribe en el bloc de notas: añade una línea al final o lo sustituye entero.",
            """{"text":{"type":"string","description":"El texto que se escribe."},"mode":{"type":"string","enum":["añadir","sustituir"],"description":"Por defecto, añadir."}}|text"""),
        ("week_summary", "Cómo va la semana: tareas hechas y minutos de foco de los últimos siete días, racha y hora punta.", "{}"),
    ];

    public static readonly HashSet<string> Names = Defs.Select(d => d.Name).ToHashSet();

    public static JsonArray List()
    {
        var tools = new JsonArray();
        foreach (var (name, description, schema) in Defs)
        {
            string[] parts = schema.Split('|');
            var input = new JsonObject { ["type"] = "object", ["properties"] = JsonNode.Parse(parts[0]) };
            if (parts.Length > 1) input["required"] = new JsonArray(parts[1].Split(',').Select(r => (JsonNode)r).ToArray());
            tools.Add(new JsonObject { ["name"] = name, ["description"] = description, ["inputSchema"] = input });
        }
        return tools;
    }

    public string Call(string name, JsonObject a)
    {
        DateTime now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        switch (name)
        {
            case "list_tasks":
                {
                    var day = ParseDay(Str(a, "date"), today, today);
                    var data = Synced(now);
                    var tasks = Planner.ForDay(data, day, now);
                    string where = day is null ? "en la bandeja" : $"para {Label(day, today)}";
                    if (tasks.Count == 0) return $"No hay tareas {where}.";
                    var sb = new StringBuilder($"Tareas {where}:\n");
                    foreach (var t in tasks) sb.AppendLine(Line(t));
                    return sb.ToString().TrimEnd();
                }
            case "add_tasks":
                {
                    var titles = (a["titles"] as JsonArray)?.Select(n => n?.GetValue<string>()?.Trim() ?? "").Where(s => s.Length > 0).ToList() ?? [];
                    if (titles.Count == 0) throw new UserException("Indica al menos un título de tarea.");
                    var day = ParseDay(Str(a, "date"), today, today);
                    if (day is { } d && d < today) throw new UserException("Solo se pueden añadir tareas a hoy o a un día futuro.");
                    var priority = ParsePriority(Str(a, "priority"));
                    string? project = Str(a, "project");
                    var added = store.Mutate(data =>
                    {
                        Planner.CatchUp(data, now);
                        string? proj = Planner.MatchProject(data, project);
                        return titles.Select(title =>
                        {
                            var t = new TaskItem { Id = Planner.NewId(data), Title = title.ReplaceLineEndings(" "), Day = day, Priority = priority ?? Priority.None, Project = proj, Created = now };
                            data.Tasks.Add(t);
                            return t;
                        }).ToList();
                    });
                    string where = day is null ? "a la bandeja" : $"a {Label(day, today)}";
                    return $"{Texts.Plural(added.Count, "tarea añadida", "tareas añadidas")} {where}:\n" + string.Join('\n', added.Select(Line));
                }
            case "get_task":
                {
                    var t = Planner.Find(Synced(now), Req(a, "id"));
                    var sb = new StringBuilder();
                    sb.AppendLine($"{t.Title} [{t.Id}]");
                    sb.AppendLine(t.Done ? $"Hecha el {t.CompletedAt:yyyy-MM-dd} a las {Texts.Time(t.CompletedAt!.Value)}." : $"Sin hacer, {(t.Day is null ? "en la bandeja" : "para " + Label(t.Day, today))}.");
                    if (t.CarriedFrom is { } cf) sb.AppendLine($"Viene del {cf:yyyy-MM-dd}.");
                    if (t.Priority != Priority.None) sb.AppendLine($"Prioridad {PriorityName(t.Priority)}.");
                    if (t.Project != null) sb.AppendLine($"Proyecto: {t.Project}.");
                    if (t.RemindAt is { } r) sb.AppendLine($"Aviso: {r:yyyy-MM-dd} {Texts.Time(r)}.");
                    if (t.Repeat != Repeat.None) sb.AppendLine($"Se repite: {RepeatName(t.Repeat)}.");
                    if (t.Deadline is { } dl) sb.AppendLine($"Fecha límite: {dl:yyyy-MM-dd}.");
                    if (t.LimitMinutes > 0) sb.AppendLine($"Minutos previstos: {t.LimitMinutes}.");
                    if (t.FocusSeconds > 0) sb.AppendLine($"Foco dedicado: {Texts.Duration(t.FocusSeconds)}.");
                    if (t.Steps.Count > 0)
                    {
                        sb.AppendLine("Pasos:");
                        foreach (var s in t.Steps) sb.AppendLine($"  [{(s.Done ? "x" : " ")}] {s.Title}");
                    }
                    if (t.Notes.Length > 0) sb.AppendLine("Notas: " + t.Notes);
                    return sb.ToString().TrimEnd();
                }
            case "update_task":
                return store.Mutate(data =>
                {
                    var t = Planner.Find(data, Req(a, "id"));
                    if (t.Done) throw new UserException($"«{t.Title}» ya está hecha. Vuelve a abrirla antes de cambiarla.");
                    var changed = new List<string>();
                    if (a.ContainsKey("title"))
                    {
                        string title = (Str(a, "title") ?? "").ReplaceLineEndings(" ").Trim();
                        if (title.Length == 0) throw new UserException("El título no puede quedar vacío.");
                        t.Title = title; changed.Add("título");
                    }
                    if (a.ContainsKey("priority")) { t.Priority = ParsePriority(Str(a, "priority")) ?? Priority.None; changed.Add("prioridad"); }
                    if (a.ContainsKey("project")) { t.Project = Planner.MatchProject(data, Str(a, "project")); changed.Add("proyecto"); }
                    if (a.ContainsKey("notes")) { t.Notes = Str(a, "notes") ?? ""; changed.Add("notas"); }
                    if (a.ContainsKey("minutes")) { t.LimitMinutes = Math.Clamp(a["minutes"]!.GetValue<int>(), 0, 600); changed.Add("minutos"); }
                    if (a.ContainsKey("deadline"))
                    {
                        string? dl = Str(a, "deadline");
                        t.Deadline = string.IsNullOrEmpty(dl) ? null : ParseDay(dl, today, today) ?? throw new UserException("La fecha límite necesita un día.");
                        changed.Add("fecha límite");
                    }
                    if (a.ContainsKey("repeat"))
                    {
                        var rep = ParseRepeat(Str(a, "repeat"));
                        if (rep != Repeat.None && t.Day is null) throw new UserException($"«{t.Title}» está en la bandeja, sin día. Muévela a un día antes de ponerle una repetición.");
                        t.Repeat = rep; changed.Add("repetición");
                    }
                    if (a.ContainsKey("remind_at"))
                    {
                        string? when = Str(a, "remind_at");
                        if (string.IsNullOrEmpty(when)) Planner.SetReminder(data, t.Id, null, now);
                        else
                        {
                            if (t.Day is null) throw new UserException($"«{t.Title}» está en la bandeja, sin día. Muévela a un día antes de ponerle un aviso.");
                            Planner.SetReminder(data, t.Id, ParseWhen(when, t.Day.Value), now);
                        }
                        changed.Add("aviso");
                    }
                    if (a["steps"] is JsonArray steps)
                    {
                        t.Steps = steps.Select(s => new Step { Title = s?.GetValue<string>() ?? "" }).Where(s => s.Title.Length > 0).ToList();
                        changed.Add("pasos");
                    }
                    if (a["done_steps"] is JsonArray done)
                    {
                        foreach (var d in done)
                        {
                            string title = d?.GetValue<string>() ?? "";
                            var step = t.Steps.FirstOrDefault(s => string.Equals(s.Title, title, StringComparison.CurrentCultureIgnoreCase))
                                ?? throw new UserException($"«{t.Title}» no tiene el paso «{title}». Usa get_task para ver sus pasos.");
                            step.Done = true;
                        }
                        changed.Add("pasos hechos");
                    }
                    if (changed.Count == 0) throw new UserException("Di qué cambiar: title, remind_at, priority, project, notes, repeat, deadline, minutes, steps o done_steps.");
                    return $"Cambiado en «{t.Title}»: {string.Join(", ", changed)}.";
                });
            case "complete_task":
                return store.Mutate(data =>
                {
                    var t = Planner.Find(data, Req(a, "id"));
                    if (t.Done) return $"«{t.Title}» ya estaba hecha.";
                    var next = Planner.Complete(data, t.Id, now);
                    return $"Hecha: «{t.Title}»." + (next != null ? $" La siguiente queda para {Label(next.Day, today)}." : "");
                });
            case "reopen_task":
                return store.Mutate(data =>
                {
                    var t = Planner.Find(data, Req(a, "id"));
                    Planner.Reopen(data, t.Id, now);
                    return $"«{t.Title}» vuelve a estar sin hacer.";
                });
            case "move_task":
                {
                    string date = Req(a, "date");
                    var day = ParseDay(date, today, today);
                    return store.Mutate(data =>
                    {
                        var t = Planner.Find(data, Req(a, "id"));
                        Planner.Move(data, t.Id, day, now);
                        return day is null ? $"«{t.Title}» espera en la bandeja, sin día." : $"«{t.Title}» pasa a {Label(day, today)}.";
                    });
                }
            case "remove_task":
                return store.Mutate(data =>
                {
                    var t = Planner.Find(data, Req(a, "id"));
                    Planner.Remove(data, t.Id, now);
                    return $"Quitada: «{t.Title}».";
                });
            case "snooze_task":
                return store.Mutate(data =>
                {
                    var t = Planner.Find(data, Req(a, "id"));
                    int minutes = a["minutes"]?.GetValue<int>() ?? 5;
                    Planner.Snooze(data, t.Id, minutes, now);
                    return $"«{t.Title}» volverá a avisar a las {Texts.Time(t.RemindAt!.Value)}.";
                });
            case "start_timer":
                return store.Mutate(data =>
                {
                    string? id = Str(a, "id");
                    int? minutes = a["minutes"]?.GetValue<int>();
                    FocusTimer.Start(data, string.IsNullOrEmpty(id) ? null : id, minutes, TimerMode.Countdown, now);
                    string what = data.Timer.TaskId != null ? $" para «{Planner.Find(data, data.Timer.TaskId).Title}»" : "";
                    return $"Temporizador en marcha{what}: {data.Timer.TargetSeconds / 60} minutos.";
                });
            case "pause_timer":
                return store.Mutate(data => { FocusTimer.Pause(data, now); return $"Temporizador en pausa. Quedan {Texts.Clock(FocusTimer.Display(data.Timer, now))}."; });
            case "resume_timer":
                return store.Mutate(data => { FocusTimer.Resume(data, now); return "Temporizador reanudado."; });
            case "stop_timer":
                return store.Mutate(data =>
                {
                    if (!data.Timer.Active) throw new UserException("No hay ningún temporizador activo.");
                    FocusTimer.Stop(data, now);
                    return "Temporizador parado. El tiempo de foco queda apuntado y la tarea sigue en su día.";
                });
            case "read_notepad":
                {
                    string text = store.Read().Notepad;
                    return text.Trim().Length == 0 ? "El bloc de notas está vacío." : text;
                }
            case "write_notepad":
                {
                    string text = Req(a, "text");
                    if (text.Length > 20000) throw new UserException("El texto es demasiado largo: hasta 20000 caracteres.");
                    bool replace = Str(a, "mode") == "sustituir";
                    store.Mutate(data => data.Notepad = replace || data.Notepad.Trim().Length == 0 ? text : data.Notepad.TrimEnd() + "\n" + text);
                    return replace ? "Bloc de notas sustituido." : "Añadido al bloc de notas.";
                }
            case "week_summary":
                {
                    var data = Synced(now);
                    var w = Stats.Week(data, now);
                    var sb = new StringBuilder(Stats.Sentence(w)).AppendLine();
                    foreach (var d in w.Days)
                        sb.AppendLine($"{d.Day:yyyy-MM-dd}: {d.Completed} de {d.Planned} tareas, {Texts.Duration(d.FocusSeconds)} de foco");
                    sb.AppendLine($"Racha: {Texts.Plural(w.Streak, "día", "días")}.");
                    if (w.PeakHour is { } h) sb.AppendLine($"Hora punta: de {h}:00 a {h + 1}:00.");
                    int inbox = data.Tasks.Count(t => t.Day is null && !t.Done);
                    if (inbox > 0) sb.AppendLine($"En la bandeja: {Texts.Plural(inbox, "tarea", "tareas")}.");
                    return sb.ToString().TrimEnd();
                }
            default:
                throw new UserException($"No existe la herramienta {name}.");
        }
    }

    // Lee los datos haciendo antes el paso de día, por si la app no está abierta.
    private AppData Synced(DateTime now) => store.Mutate(d => { Planner.CatchUp(d, now); return d; });

    private static string Line(TaskItem t)
    {
        var extras = new List<string>();
        if (t.Priority != Priority.None) extras.Add("prioridad " + PriorityName(t.Priority));
        if (t.Project != null) extras.Add("#" + t.Project);
        if (t.RemindAt is { } r) extras.Add("aviso " + Texts.Time(r));
        if (t.Repeat != Repeat.None) extras.Add(RepeatName(t.Repeat));
        if (t.Steps.Count > 0) extras.Add($"{t.Steps.Count(s => s.Done)}/{t.Steps.Count} pasos");
        string tail = extras.Count > 0 ? " (" + string.Join(", ", extras) + ")" : "";
        return $"- [{(t.Done ? "x" : " ")}] {t.Title}{tail} [id: {t.Id}]";
    }

    private static string Label(DateOnly? day, DateOnly today) =>
        day is not { } d ? "la bandeja" : d == today ? "hoy" : d == today.AddDays(1) ? "mañana" : d == today.AddDays(-1) ? "ayer" : $"el {d:yyyy-MM-dd}";

    private static string PriorityName(Priority p) => p switch { Priority.High => "alta", Priority.Medium => "media", Priority.Low => "baja", _ => "ninguna" };

    private static string RepeatName(Repeat r) => r switch
    {
        Repeat.Daily => "cada día",
        Repeat.Weekdays => "días laborables",
        Repeat.Weekly => "cada semana",
        Repeat.Monthly => "cada mes",
        _ => "no se repite",
    };

    private static string? Str(JsonObject a, string key) => a[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static string Req(JsonObject a, string key) =>
        Str(a, key) is { Length: > 0 } s ? s : throw new UserException($"Falta «{key}».");

    public static DateOnly? ParseDay(string? text, DateOnly today, DateOnly? fallback)
    {
        text = text?.Trim().ToLowerInvariant();
        switch (text)
        {
            case null or "": return fallback;
            case "hoy" or "today": return today;
            case "mañana" or "manana" or "tomorrow": return today.AddDays(1);
            case "ayer" or "yesterday": return today.AddDays(-1);
            case "bandeja" or "inbox": return null;
        }
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        throw new UserException($"No entiendo «{text}» como día. {DayHelp}");
    }

    private static DateTime ParseWhen(string text, DateOnly day)
    {
        text = text.Trim();
        if (DateTime.TryParseExact(text, ["yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm", "yyyy-MM-ddTHH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var full)) return full;
        var e = QuickAdd.Parse(text.Contains(':') || text.Contains('m', StringComparison.OrdinalIgnoreCase) ? text : text + ":00", day);
        if (e.Time is { } t && e.Title.Length == 0) return (e.Day ?? day).ToDateTime(t);
        throw new UserException($"No entiendo «{text}» como hora. Prueba 17:30, 5:30 pm o 2026-10-30 17:30.");
    }

    private static Priority? ParsePriority(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "alta" or "high" => Priority.High,
        "media" or "medium" => Priority.Medium,
        "baja" or "low" => Priority.Low,
        "ninguna" or "none" => Priority.None,
        var other => throw new UserException($"No entiendo «{other}» como prioridad. Usa alta, media, baja o ninguna."),
    };

    private static Repeat ParseRepeat(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        null or "" or "ninguna" or "none" => Repeat.None,
        "diaria" or "daily" or "cada día" => Repeat.Daily,
        "laborables" or "weekdays" => Repeat.Weekdays,
        "semanal" or "weekly" => Repeat.Weekly,
        "mensual" or "monthly" => Repeat.Monthly,
        var other => throw new UserException($"No entiendo «{other}» como repetición. Usa diaria, laborables, semanal, mensual o ninguna."),
    };
}
