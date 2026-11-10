namespace Worknotch.Tests;

public class EstadisticasTests
{
    private static readonly DateTime Now = Dates.Now; // miércoles 11-11-2026

    private static AppData WithDone(params int[] daysAgo)
    {
        var d = new AppData();
        foreach (int ago in daysAgo)
            d.Tasks.Add(new TaskItem { Id = "t" + ago, Title = "T", Done = true, Day = Dates.Today.AddDays(-ago), CompletedAt = Now.AddDays(-ago) });
        return d;
    }

    [Fact]
    public void RachaDeDiasSeguidos()
    {
        Assert.Equal(3, Stats.Streak(WithDone(0, 1, 2, 6), Dates.Today));
        Assert.Equal(0, Stats.Streak(new AppData(), Dates.Today));
    }

    [Fact]
    public void HoySinNadaHechoNoRompeLaRacha()
    {
        Assert.Equal(2, Stats.Streak(WithDone(1, 2), Dates.Today));
    }

    [Fact]
    public void ElFinDeSemanaNoRompeLaRachaSalvoQueCuente()
    {
        // lunes 9 y viernes 6: entre medias, sábado y domingo
        var d = WithDone(2, 5);
        Assert.Equal(2, Stats.Streak(d, Dates.Today.AddDays(-2)));
        d.Settings.WeekendsInStreak = true;
        Assert.Equal(1, Stats.Streak(d, Dates.Today.AddDays(-2)));
    }

    [Fact]
    public void LaSemanaCuentaTareasYFoco()
    {
        var d = WithDone(0, 0, 3, 8);
        d.Tasks.Add(new TaskItem { Id = "p", Title = "Pendiente", Day = Dates.Today });
        d.Sessions.Add(new FocusSession { Start = Now.AddHours(-1), Seconds = 1500 });
        d.Sessions.Add(new FocusSession { Start = Now.AddDays(-3), Seconds = 600 });
        var w = Stats.Week(d, Now);
        Assert.Equal(7, w.Days.Count);
        Assert.Equal(Dates.Today, w.Days[^1].Day);
        Assert.Equal(3, w.Completed);
        Assert.Equal(1, w.CompletedBefore);
        Assert.Equal(2100, w.FocusSeconds);
        Assert.Equal(3, w.Days[^1].Planned);
        Assert.Equal(2, w.Days[^1].Completed);
        Assert.Equal(9, w.PeakHour);
        Assert.Contains("3 tareas hechas", Stats.Sentence(w));
        Assert.Contains("más que la semana anterior", Stats.Sentence(w));
    }

    [Fact]
    public void ElTemporizadorEnMarchaYaCuentaHoy()
    {
        var d = new AppData();
        FocusTimer.Start(d, null, 25, TimerMode.Countdown, Now.AddMinutes(-10));
        Assert.Equal(600, Stats.Day(d, Dates.Today, Now).FocusSeconds);
    }

    [Fact]
    public void SinDatosLoDice()
    {
        var w = Stats.Week(new AppData(), Now);
        Assert.Null(w.PeakHour);
        Assert.StartsWith("Termina una tarea", Stats.Sentence(w));
    }
}
