namespace Worknotch.Core;

public enum LinkAction { Show, Add }

public sealed record LinkCommand(LinkAction Action, string Line = "");

/// <summary>
/// Los enlaces worknotch://, para scripts y accesos directos:
/// worknotch://add?title=Comprar%20pan&amp;day=mañana&amp;time=17:30&amp;priority=alta&amp;project=Casa
/// worknotch://show
/// </summary>
public static class Link
{
    public const string Scheme = "worknotch";

    public static LinkCommand? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Scheme) return null;
        string action = (uri.Host.Length > 0 ? uri.Host : uri.AbsolutePath.Trim('/')).ToLowerInvariant();
        var q = Query(uri.Query);
        switch (action)
        {
            case "show" or "open" or "":
                return new LinkCommand(LinkAction.Show);
            case "add":
                if (!q.TryGetValue("title", out var title) || title.Trim().Length == 0) return null;
                // Se compone la misma línea que se escribiría a mano: una sola forma de entender las tareas.
                var parts = new List<string> { title.Trim().ReplaceLineEndings(" ") };
                if (q.TryGetValue("day", out var day)) parts.Add(day);
                if (q.TryGetValue("time", out var time)) parts.Add(time);
                if (q.TryGetValue("priority", out var p)) parts.Add("!" + p.TrimStart('!'));
                if (q.TryGetValue("project", out var pr)) parts.Add("#" + pr.Trim().TrimStart('#').Replace(' ', '_'));
                if (q.TryGetValue("repeat", out var r)) parts.Add(r);
                return new LinkCommand(LinkAction.Add, string.Join(' ', parts));
            default:
                return null;
        }
    }

    private static Dictionary<string, string> Query(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            string key = Uri.UnescapeDataString(eq < 0 ? pair : pair[..eq]);
            string value = eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
            result[key] = value;
        }
        return result;
    }
}
