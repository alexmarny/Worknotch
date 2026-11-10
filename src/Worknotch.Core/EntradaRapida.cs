using System.Globalization;
using System.Text.RegularExpressions;

namespace Worknotch.Core;

/// <summary>Lo que se ha entendido de una línea escrita en «Añadir tarea».</summary>
public sealed record QuickEntry(
    string Title,
    DateOnly? Day,
    bool DayGiven,
    TimeOnly? Time,
    Priority Priority,
    string? Project,
    Repeat Repeat,
    int Minutes);

/// <summary>
/// Lee una línea como «Llamar a mamá mañana 17:30 !alta #Familia» y separa el título del día, la
/// hora, la prioridad, el proyecto, la repetición y los minutos. Lo que no se entiende se queda en
/// el título: nunca se pierde texto.
/// </summary>
public static partial class QuickAdd
{
    private static readonly string[] Weekdays = ["lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo"];

    public static QuickEntry Parse(string line, DateOnly today)
    {
        string text = " " + line.Trim() + " ";
        DateOnly? day = null;
        bool dayGiven = false;
        TimeOnly? time = null;
        var priority = Priority.None;
        string? project = null;
        var repeat = Repeat.None;
        int minutes = 0;

        text = ProjectRx().Replace(text, m => { project ??= m.Groups[1].Value.Replace('_', ' '); return " "; });

        text = PriorityRx().Replace(text, m =>
        {
            priority = m.Groups[1].Value.ToLowerInvariant() switch
            {
                "baja" => Priority.Low,
                "media" => Priority.Medium,
                _ => Priority.High,
            };
            return " ";
        });

        text = RepeatRx().Replace(text, m =>
        {
            if (repeat != Repeat.None) return m.Value;
            string r = m.Groups[1].Value.ToLowerInvariant();
            repeat = r switch
            {
                "cada día" or "cada dia" or "todos los días" or "todos los dias" => Repeat.Daily,
                "laborables" or "cada laborable" => Repeat.Weekdays,
                "cada semana" => Repeat.Weekly,
                _ => Repeat.Monthly,
            };
            return " ";
        });

        text = MinutesRx().Replace(text, m =>
        {
            if (minutes != 0) return m.Value;
            int n = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            minutes = m.Groups[2].Value.StartsWith('h') ? n * 60 : n;
            if (minutes is < 1 or > 600) { minutes = 0; return m.Value; }
            return " ";
        });

        text = TimeRx().Replace(text, m =>
        {
            if (time != null) return m.Value;
            int h = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
            int min = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
            string ap = m.Groups["ap"].Value.ToLowerInvariant().Replace(".", "").Replace(" ", "");
            bool explicitTime = m.Groups["m"].Success || ap.Length > 0 || m.Groups["alas"].Success;
            if (!explicitTime) return m.Value;
            if (ap == "pm" && h < 12) h += 12;
            if (ap == "am" && h == 12) h = 0;
            if (h > 23 || min > 59) return m.Value;
            time = new TimeOnly(h, min);
            return " ";
        });

        text = IsoDateRx().Replace(text, m =>
        {
            if (dayGiven) return m.Value;
            if (!DateOnly.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return m.Value;
            day = d; dayGiven = true;
            return " ";
        });

        text = ShortDateRx().Replace(text, m =>
        {
            if (dayGiven) return m.Value;
            int dd = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            int mm = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            if (mm is < 1 or > 12 || dd < 1 || dd > DateTime.DaysInMonth(today.Year, mm)) return m.Value;
            var d = new DateOnly(today.Year, mm, dd);
            if (d < today) d = d.AddYears(1);
            day = d; dayGiven = true;
            return " ";
        });

        text = WordDayRx().Replace(text, m =>
        {
            if (dayGiven) return m.Value;
            string w = m.Groups[1].Value.ToLowerInvariant();
            switch (w)
            {
                case "hoy": day = today; break;
                case "mañana": day = today.AddDays(1); break;
                case "pasado mañana": day = today.AddDays(2); break;
                case "bandeja" or "sin día" or "sin dia": day = null; break;
                default:
                    int target = Array.IndexOf(Weekdays, w.Replace("miercoles", "miércoles").Replace("sabado", "sábado"));
                    int now = ((int)today.DayOfWeek + 6) % 7;
                    int delta = (target - now + 7) % 7;
                    day = today.AddDays(delta == 0 ? 7 : delta);
                    break;
            }
            dayGiven = true;
            return " ";
        });

        string title = SpacesRx().Replace(text, " ").Trim();
        // «el», «a las», «para» y «a» que se quedan colgando al quitar el día o la hora
        title = DanglingRx().Replace(title, "").Trim();
        return new QuickEntry(title, day, dayGiven, time, priority, project, repeat, minutes);
    }

    [GeneratedRegex(@"(?<=\s)#([\p{L}\p{N}_-]{1,40})(?=\s)")]
    private static partial Regex ProjectRx();

    [GeneratedRegex(@"(?<=\s)!(alta|media|baja|!|)(?=\s)", RegexOptions.IgnoreCase)]
    private static partial Regex PriorityRx();

    [GeneratedRegex(@"(?<=\s)(cada d[ií]a|todos los d[ií]as|cada laborable|laborables|cada semana|cada mes)(?=\s)", RegexOptions.IgnoreCase)]
    private static partial Regex RepeatRx();

    [GeneratedRegex(@"(?<=\s)(\d{1,3}) ?(min|m|h)(?=\s)", RegexOptions.IgnoreCase)]
    private static partial Regex MinutesRx();

    [GeneratedRegex(@"(?<=\s)(?<alas>a las? )?(?<h>\d{1,2})(?:[:.](?<m>\d{2}))? ?(?<ap>a\.? ?m\.?|p\.? ?m\.?)?(?=\s)", RegexOptions.IgnoreCase)]
    private static partial Regex TimeRx();

    [GeneratedRegex(@"(?<=\s)(\d{4}-\d{2}-\d{2})(?=\s)")]
    private static partial Regex IsoDateRx();

    [GeneratedRegex(@"(?<=\s)(?:el )?(\d{1,2})/(\d{1,2})(?=\s)", RegexOptions.IgnoreCase)]
    private static partial Regex ShortDateRx();

    [GeneratedRegex(@"(?<=\s)(?:el |para el |para )?(pasado mañana|hoy|mañana|bandeja|sin d[ií]a|lunes|martes|mi[eé]rcoles|jueves|viernes|s[aá]bado|domingo)(?=\s)", RegexOptions.IgnoreCase)]
    private static partial Regex WordDayRx();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex SpacesRx();

    [GeneratedRegex(@"\s+(el|para|para el|a|a las|a la)$", RegexOptions.IgnoreCase)]
    private static partial Regex DanglingRx();
}
