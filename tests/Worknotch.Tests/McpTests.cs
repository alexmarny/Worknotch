using System.Text.Json.Nodes;
using Worknotch.Core.Mcp;

namespace Worknotch.Tests;

public class McpTests : IDisposable
{
    private readonly TempStore _tmp = new();
    private readonly FakeClock _clock = new(Dates.Now);
    private readonly McpServer _server;

    public McpTests() => _server = new McpServer(_tmp.Store, _clock, "1.2.3");

    public void Dispose() => _tmp.Dispose();

    private JsonNode Send(string method, JsonObject? p = null)
    {
        var msg = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method };
        if (p != null) msg["params"] = p;
        return JsonNode.Parse(_server.Handle(msg.ToJsonString())!)!;
    }

    private (string Text, bool IsError) Call(string tool, JsonObject? args = null)
    {
        var r = Send("tools/call", new JsonObject { ["name"] = tool, ["arguments"] = args ?? [] })["result"]!;
        return (r["content"]![0]!["text"]!.GetValue<string>(), r["isError"]!.GetValue<bool>());
    }

    private string IdOf(string title) => _tmp.Store.Read().Tasks.Single(t => t.Title == title).Id;

    [Fact]
    public void Saludo()
    {
        var r = Send("initialize", new JsonObject { ["protocolVersion"] = "2024-11-05" })["result"]!;
        Assert.Equal("2024-11-05", r["protocolVersion"]!.GetValue<string>());
        Assert.Equal("worknotch", r["serverInfo"]!["name"]!.GetValue<string>());
        Assert.Equal("1.2.3", r["serverInfo"]!["version"]!.GetValue<string>());
        Assert.NotNull(r["capabilities"]!["tools"]);

        var unknown = Send("initialize", new JsonObject { ["protocolVersion"] = "1999-01-01" })["result"]!;
        Assert.Equal("2025-06-18", unknown["protocolVersion"]!.GetValue<string>());
    }

    [Fact]
    public void LasNotificacionesNoSeContestan()
    {
        Assert.Null(_server.Handle("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""));
    }

    [Fact]
    public void ErroresDeProtocolo()
    {
        Assert.Equal(-32700, JsonNode.Parse(_server.Handle("{ roto")!)!["error"]!["code"]!.GetValue<int>());
        Assert.Equal(-32601, Send("no/existe")["error"]!["code"]!.GetValue<int>());
        Assert.Equal(-32602, Send("tools/call", new JsonObject { ["name"] = "nada" })["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public void CadaHerramientaTieneEsquemaYDescripcion()
    {
        var tools = Send("tools/list")["result"]!["tools"]!.AsArray();
        Assert.Equal(McpTools.Names.Count, tools.Count);
        foreach (var t in tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(t!["description"]!.GetValue<string>()));
            Assert.Equal("object", t["inputSchema"]!["type"]!.GetValue<string>());
            var props = t["inputSchema"]!["properties"]!.AsObject();
            foreach (var req in t["inputSchema"]!["required"]?.AsArray() ?? [])
                Assert.True(props.ContainsKey(req!.GetValue<string>()), $"{t["name"]}: falta {req}");
        }
    }

    [Fact]
    public void PlanificarCompletarYMover()
    {
        var (added, err) = Call("add_tasks", new JsonObject { ["titles"] = new JsonArray("Informe", "Gimnasio"), ["priority"] = "alta", ["project"] = "Trabajo" });
        Assert.False(err);
        Assert.Contains("2 tareas añadidas a hoy", added);

        var (list, _) = Call("list_tasks");
        Assert.Contains("Informe", list);
        Assert.Contains("prioridad alta", list);
        Assert.Contains($"[id: {IdOf("Informe")}]", list);

        Assert.Contains("Hecha", Call("complete_task", new JsonObject { ["id"] = IdOf("Informe") }).Text);
        Assert.Contains("ya estaba hecha", Call("complete_task", new JsonObject { ["id"] = IdOf("Informe") }).Text);
        Assert.Contains("pasa a mañana", Call("move_task", new JsonObject { ["id"] = IdOf("Gimnasio"), ["date"] = "mañana" }).Text);
        Assert.Contains("Gimnasio", Call("list_tasks", new JsonObject { ["date"] = "tomorrow" }).Text);
        Assert.Contains("vuelve a estar sin hacer", Call("reopen_task", new JsonObject { ["id"] = IdOf("Informe") }).Text);

        var data = _tmp.Store.Read();
        Assert.Equal(["Trabajo"], data.Projects);
        Assert.Equal(Dates.Today.AddDays(1), data.Tasks.Single(t => t.Title == "Gimnasio").Day);
    }

    [Fact]
    public void LosErroresDeUsoVuelvenComoTextoParaClaude()
    {
        Assert.True(Call("complete_task", new JsonObject { ["id"] = "zzzzz" }).IsError);
        Assert.True(Call("add_tasks", new JsonObject { ["titles"] = new JsonArray() }).IsError);
        Assert.True(Call("add_tasks", new JsonObject { ["titles"] = new JsonArray("x"), ["date"] = "ayer" }).IsError);
        var (text, err) = Call("list_tasks", new JsonObject { ["date"] = "el martes" });
        Assert.True(err);
        Assert.Contains("yyyy-MM-dd", text);
        Assert.True(Call("pause_timer").IsError);
        Assert.Empty(_tmp.Store.Read().Tasks);
    }

    [Fact]
    public void CambiarUnaTarea()
    {
        Call("add_tasks", new JsonObject { ["titles"] = new JsonArray("Viaje") });
        string id = IdOf("Viaje");
        var (text, err) = Call("update_task", new JsonObject
        {
            ["id"] = id, ["title"] = "Viaje a Lisboa", ["remind_at"] = "17:30", ["repeat"] = "semanal", ["minutes"] = 50,
            ["steps"] = new JsonArray("Vuelos", "Hotel"), ["done_steps"] = new JsonArray("vuelos"), ["deadline"] = "2026-12-01",
        });
        Assert.False(err, text);
        var t = _tmp.Store.Read().Tasks.Single();
        Assert.Equal("Viaje a Lisboa", t.Title);
        Assert.Equal(Dates.Today.ToDateTime(new TimeOnly(17, 30)), t.RemindAt);
        Assert.Equal(Repeat.Weekly, t.Repeat);
        Assert.Equal(50, t.LimitMinutes);
        Assert.Equal(new DateOnly(2026, 12, 1), t.Deadline);
        Assert.True(t.Steps[0].Done);
        Assert.False(t.Steps[1].Done);

        var (detail, _) = Call("get_task", new JsonObject { ["id"] = id });
        Assert.Contains("[x] Vuelos", detail);
        Assert.Contains("Aviso: 2026-11-11 17:30", detail);

        Assert.True(Call("update_task", new JsonObject { ["id"] = id }).IsError);
        Assert.True(Call("update_task", new JsonObject { ["id"] = id, ["remind_at"] = "9:00" }).IsError); // ya ha pasado
        Assert.True(Call("update_task", new JsonObject { ["id"] = id, ["done_steps"] = new JsonArray("Coche") }).IsError);
    }

    [Fact]
    public void Temporizador()
    {
        Call("add_tasks", new JsonObject { ["titles"] = new JsonArray("Escribir") });
        Assert.Contains("25 minutos", Call("start_timer", new JsonObject { ["id"] = IdOf("Escribir") }).Text);
        _clock.Now = Dates.Now.AddMinutes(10);
        Assert.Contains("Quedan 15:00", Call("pause_timer").Text);
        Assert.False(Call("resume_timer").IsError);
        _clock.Now = Dates.Now.AddMinutes(12);
        Assert.False(Call("stop_timer").IsError);
        var d = _tmp.Store.Read();
        Assert.False(d.Timer.Active);
        Assert.Equal(12 * 60, d.Tasks.Single().FocusSeconds);
        Assert.Contains("12 min de foco", Call("week_summary").Text);
    }

    [Fact]
    public void BlocDeNotas()
    {
        Assert.Contains("vacío", Call("read_notepad").Text);
        Call("write_notepad", new JsonObject { ["text"] = "Uno" });
        Call("write_notepad", new JsonObject { ["text"] = "Dos" });
        Assert.Equal("Uno\nDos", Call("read_notepad").Text);
        Call("write_notepad", new JsonObject { ["text"] = "Tres", ["mode"] = "sustituir" });
        Assert.Equal("Tres", Call("read_notepad").Text);
    }

    [Fact]
    public async Task UnaConversacionEnteraPorLaEntradaEstandar()
    {
        var input = new StringReader(string.Join('\n',
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18"}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            "",
            """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"add_tasks","arguments":{"titles":["Año nuevo"],"date":"bandeja"}}}"""));
        var output = new StringWriter();
        await _server.RunAsync(input, output);
        string[] lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("añadida a la bandeja", lines[1]);
        Assert.Contains("Año nuevo", lines[1]); // sin escapar la ñ
        Assert.Null(_tmp.Store.Read().Tasks.Single().Day);
    }
}
