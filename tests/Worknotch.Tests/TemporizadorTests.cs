namespace Worknotch.Tests;

public class TemporizadorTests
{
    private static readonly DateTime Now = Dates.Now;

    [Fact]
    public void CuentaAtrasConPausa()
    {
        var d = new AppData();
        FocusTimer.Start(d, null, null, TimerMode.Countdown, Now);
        Assert.Equal(25 * 60, FocusTimer.Display(d.Timer, Now));
        Assert.Equal(15 * 60, FocusTimer.Display(d.Timer, Now.AddMinutes(10)));

        FocusTimer.Pause(d, Now.AddMinutes(10));
        Assert.Equal(15 * 60, FocusTimer.Display(d.Timer, Now.AddMinutes(40))); // en pausa no corre
        FocusTimer.Resume(d, Now.AddMinutes(40));
        Assert.Equal(10 * 60, FocusTimer.Display(d.Timer, Now.AddMinutes(45)));
        Assert.False(FocusTimer.Finished(d.Timer, Now.AddMinutes(45)));
        Assert.True(FocusTimer.Finished(d.Timer, Now.AddMinutes(55)));
        Assert.Equal(0, FocusTimer.Display(d.Timer, Now.AddMinutes(70)));
    }

    [Fact]
    public void ElFinalAvisaUnaVezYAnadirMinutosLoReabre()
    {
        var d = new AppData();
        FocusTimer.Start(d, null, 1, TimerMode.Countdown, Now);
        Assert.False(FocusTimer.TakeEnd(d, Now.AddSeconds(59)));
        Assert.True(FocusTimer.TakeEnd(d, Now.AddSeconds(60)));
        Assert.False(FocusTimer.TakeEnd(d, Now.AddSeconds(61)));
        FocusTimer.AddMinutes(d, 5);
        Assert.False(FocusTimer.Finished(d.Timer, Now.AddSeconds(61)));
        Assert.True(FocusTimer.TakeEnd(d, Now.AddMinutes(6)));
    }

    [Fact]
    public void ElFocoSeApuntaEnLaTareaYEnLasSesiones()
    {
        var d = new AppData();
        var t = Planner.AddFromLine(d, "Escribir 40m", Dates.Today, Now);
        FocusTimer.Start(d, t.Id, null, TimerMode.Countdown, Now);
        Assert.Equal(40 * 60, d.Timer.TargetSeconds);
        FocusTimer.Pause(d, Now.AddMinutes(10));
        FocusTimer.Resume(d, Now.AddMinutes(20));
        FocusTimer.Stop(d, Now.AddMinutes(25));
        Assert.False(d.Timer.Active);
        Assert.Equal(15 * 60, t.FocusSeconds);
        Assert.Equal(2, d.Sessions.Count);
        Assert.False(t.Done);
    }

    [Fact]
    public void CompletarLaTareaParaSuTemporizador()
    {
        var d = new AppData();
        var t = Planner.AddFromLine(d, "Escribir", Dates.Today, Now);
        FocusTimer.Start(d, t.Id, null, TimerMode.Countdown, Now);
        Planner.Complete(d, t.Id, Now.AddMinutes(5));
        Assert.False(d.Timer.Active);
        Assert.Equal(300, t.FocusSeconds);
    }

    [Fact]
    public void CentrarseEnUnaTareaDeLaBandejaLaTraeAHoy()
    {
        var d = new AppData();
        var t = Planner.AddFromLine(d, "Idea", null, Now);
        FocusTimer.Start(d, t.Id, null, TimerMode.Stopwatch, Now);
        Assert.Equal(Dates.Today, t.Day);
        Assert.Equal(90, FocusTimer.Display(d.Timer, Now.AddSeconds(90)));
        Assert.False(FocusTimer.Finished(d.Timer, Now.AddHours(3)));
    }

    [Fact]
    public void UnEquipoSuspendidoNoCuentaHorasDeFoco()
    {
        var d = new AppData();
        FocusTimer.Start(d, null, 25, TimerMode.Countdown, Now);
        FocusTimer.Stop(d, Now.AddHours(30));
        Assert.Equal(4 * 3600, d.Sessions.Single().Seconds);
    }

    [Fact]
    public void ErroresClaros()
    {
        var d = new AppData();
        Assert.Throws<UserException>(() => FocusTimer.Pause(d, Now));
        Assert.Throws<UserException>(() => FocusTimer.Resume(d, Now));
        Assert.Throws<UserException>(() => FocusTimer.AddMinutes(d, 5));
        Assert.Throws<UserException>(() => FocusTimer.Start(d, "nada", null, TimerMode.Countdown, Now));
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(65, "01:05")]
    [InlineData(3725, "1:02:05")]
    public void Reloj(int seconds, string text) => Assert.Equal(text, Texts.Clock(seconds));
}
