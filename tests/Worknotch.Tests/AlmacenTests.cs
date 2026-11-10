namespace Worknotch.Tests;

public class AlmacenTests
{
    [Fact]
    public void GuardaYVuelveALeer()
    {
        using var tmp = new TempStore();
        tmp.Store.Mutate(d => { Planner.AddFromLine(d, "Comprar pan mañana 17:30 !alta #Casa", Dates.Today, Dates.Now); d.Notepad = "ñ «comillas»"; });
        var d2 = new Store(tmp.Store.Path).Read();
        var t = Assert.Single(d2.Tasks);
        Assert.Equal("Comprar pan", t.Title);
        Assert.Equal(Dates.Today.AddDays(1), t.Day);
        Assert.Equal(Priority.High, t.Priority);
        Assert.Equal("ñ «comillas»", d2.Notepad);
        Assert.Contains("\"Priority\": \"High\"", File.ReadAllText(tmp.Store.Path));
        Assert.Contains("ñ «comillas»", File.ReadAllText(tmp.Store.Path));
    }

    [Fact]
    public void SiElCambioFallaNoSeGuardaNada()
    {
        using var tmp = new TempStore();
        tmp.Store.Mutate(d => d.Notepad = "antes");
        Assert.Throws<UserException>(() => tmp.Store.Mutate(d => { d.Notepad = "después"; throw new UserException("no"); }));
        Assert.Equal("antes", tmp.Store.Read().Notepad);
    }

    [Fact]
    public void DeshacerDevuelveLasTareasPeroNoElTemporizador()
    {
        using var tmp = new TempStore();
        var s = tmp.Store;
        var id = s.Mutate(d => Planner.AddFromLine(d, "Importante", Dates.Today, Dates.Now).Id);
        Assert.False(s.CanUndo);
        s.Mutate(d => Planner.Remove(d, id, Dates.Now), undoable: true);
        s.Mutate(d => FocusTimer.Start(d, null, 25, TimerMode.Countdown, Dates.Now));
        Assert.Empty(s.Read().Tasks);
        Assert.True(s.Undo());
        var data = s.Read();
        Assert.Single(data.Tasks);
        Assert.True(data.Timer.Active);
        Assert.False(s.Undo());
    }

    [Fact]
    public void UnArchivoDanadoSeApartaYNoSePierde()
    {
        using var tmp = new TempStore();
        Directory.CreateDirectory(tmp.Folder);
        File.WriteAllText(tmp.Store.Path, "{ esto no es json");
        var data = tmp.Store.Read();
        Assert.Empty(data.Tasks);
        Assert.NotNull(tmp.Store.PreservedAt);
        Assert.Equal("{ esto no es json", File.ReadAllText(tmp.Store.PreservedAt));
    }

    [Fact]
    public void NotaLosCambiosDeOtroProceso()
    {
        using var tmp = new TempStore();
        var app = tmp.Store;
        app.Mutate(d => d.Notepad = "app");
        Assert.False(app.ChangedOutside());
        new Store(app.Path).Mutate(d => d.Notepad = "claude");
        Assert.True(app.ChangedOutside());
        Assert.Equal("claude", app.Read().Notepad);
        Assert.False(app.ChangedOutside());
    }

    [Fact]
    public async Task DosProcesosALaVezNoSePisan()
    {
        using var tmp = new TempStore();
        var a = tmp.Store;
        var b = new Store(a.Path);
        await Task.WhenAll(
            Task.Run(() => { for (int i = 0; i < 40; i++) a.Mutate(d => d.Tasks.Add(new TaskItem { Id = "a" + i, Title = "a" })); }),
            Task.Run(() => { for (int i = 0; i < 40; i++) b.Mutate(d => d.Tasks.Add(new TaskItem { Id = "b" + i, Title = "b" })); }));
        Assert.Equal(80, a.Read().Tasks.Count);
    }

    [Fact]
    public void UnArchivoAntiguoSinCamposNuevosSeCompleta()
    {
        using var tmp = new TempStore();
        Directory.CreateDirectory(tmp.Folder);
        File.WriteAllText(tmp.Store.Path, """{"Tasks":[{"Id":"abc12","Title":"Vieja"}],"Settings":{"Cards":[]}}""");
        var d = tmp.Store.Read();
        Assert.Empty(d.Tasks[0].Steps);
        Assert.Equal(["tareas"], d.Settings.Cards);
        Assert.Equal(25, d.Settings.FocusMinutes);
    }
}
