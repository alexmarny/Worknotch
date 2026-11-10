namespace Worknotch.Tests;

public class PlanificadorTests
{
    private static readonly DateTime Now = Dates.Now;
    private static readonly DateOnly Today = Dates.Today;

    [Fact]
    public void AnadeAlDiaQueSeEstaViendo()
    {
        var d = new AppData();
        var t = Planner.AddFromLine(d, "Comprar pan", Today.AddDays(3), Now);
        Assert.Equal(Today.AddDays(3), t.Day);
        Assert.Equal(5, t.Id.Length);
    }

    [Fact]
    public void ElDiaEscritoGanaAlQueSeEstaViendo()
    {
        var d = new AppData();
        Assert.Equal(Today.AddDays(1), Planner.AddFromLine(d, "Comprar pan mañana", Today, Now).Day);
        Assert.Null(Planner.AddFromLine(d, "Idea sin día", Today, Now).Day);
    }

    [Fact]
    public void UnaHoraEnLaBandejaLlevaLaTareaAHoy()
    {
        var d = new AppData();
        var t = Planner.AddFromLine(d, "Llamar 18:00", null, Now);
        Assert.Equal(Today, t.Day);
        Assert.Equal(Today.ToDateTime(new TimeOnly(18, 0)), t.RemindAt);
        Assert.False(t.ReminderShown);
    }

    [Fact]
    public void UnaHoraYaPasadaNoAvisa()
    {
        var d = new AppData();
        var t = Planner.AddFromLine(d, "Llamar 9:00", Today, Now);
        Assert.True(t.ReminderShown);
        Assert.Empty(Planner.TakeDueReminders(d, Now));
    }

    [Fact]
    public void NoSeAnadeAUnDiaPasado()
    {
        var d = new AppData();
        Assert.Throws<UserException>(() => Planner.AddFromLine(d, "Algo", Today.AddDays(-1), Now));
        Assert.Throws<UserException>(() => Planner.AddFromLine(d, "   ", Today, Now));
    }

    [Fact]
    public void ElProyectoNoSeDuplicaPorLasMayusculas()
    {
        var d = new AppData();
        Planner.AddFromLine(d, "Uno #Casa", Today, Now);
        var t = Planner.AddFromLine(d, "Dos #casa", Today, Now);
        Assert.Equal(["Casa"], d.Projects);
        Assert.Equal("Casa", t.Project);
    }

    [Fact]
    public void LosAvisosSalenUnaSolaVez()
    {
        var d = new AppData();
        var t = Planner.AddFromLine(d, "Llamar 10:30", Today, Now);
        Assert.Empty(Planner.TakeDueReminders(d, Now));
        Assert.Equal([t], Planner.TakeDueReminders(d, Now.AddMinutes(30)));
        Assert.Empty(Planner.TakeDueReminders(d, Now.AddMinutes(31)));
        Planner.Snooze(d, t.Id, 5, Now.AddMinutes(31));
        Assert.Empty(Planner.TakeDueReminders(d, Now.AddMinutes(35)));
        Assert.Equal([t], Planner.TakeDueReminders(d, Now.AddMinutes(36)));
    }

    [Fact]
    public void CompletarUnaTareaQueSeRepiteDejaLaSiguiente()
    {
        var d = new AppData();
        var t = Planner.AddFromLine(d, "Regar 9:00 cada semana", Today, Now);
        var next = Planner.Complete(d, t.Id, Now);
        Assert.NotNull(next);
        Assert.True(t.Done);
        Assert.Equal(Repeat.None, t.Repeat);
        Assert.Equal(Today.AddDays(7), next.Day);
        Assert.Equal(Repeat.Weekly, next.Repeat);
        Assert.Equal(Today.AddDays(7).ToDateTime(new TimeOnly(9, 0)), next.RemindAt);
        Assert.False(next.ReminderShown);
    }

    [Fact]
    public void LaborablesSaltaElFinDeSemana()
    {
        var friday = new DateOnly(2026, 11, 13);
        Assert.Equal(new DateOnly(2026, 11, 16), Planner.NextOccurrence(friday, Repeat.Weekdays));
        Assert.Equal(new DateOnly(2026, 11, 12), Planner.NextOccurrence(Today, Repeat.Weekdays));
    }

    [Fact]
    public void UnaRepeticionAtrasadaNoCaeEnElPasado()
    {
        var d = new AppData();
        var t = Planner.AddFromLine(d, "Regar cada día", Today, Now);
        var later = Now.AddDays(5);
        Planner.CatchUp(d, later);
        var next = Planner.Complete(d, t.Id, later)!;
        Assert.Equal(DateOnly.FromDateTime(later).AddDays(1), next.Day);
    }

    [Fact]
    public void ElPasoDeDiaTraeLoPendienteAHoy()
    {
        var d = new AppData();
        var old = Planner.AddFromLine(d, "Pendiente", Today, Now);
        var done = Planner.AddFromLine(d, "Hecha", Today, Now);
        var future = Planner.AddFromLine(d, "Futura", Today.AddDays(5), Now);
        var inbox = Planner.AddFromLine(d, "Idea", null, Now);
        Planner.Complete(d, done.Id, Now);
        Planner.CatchUp(d, Now);

        var tomorrow = Now.AddDays(1);
        Assert.Equal(1, Planner.CatchUp(d, tomorrow));
        Assert.Equal(Today.AddDays(1), old.Day);
        Assert.Equal(Today, old.CarriedFrom);
        Assert.Equal(Today, done.Day);
        Assert.Equal(Today.AddDays(5), future.Day);
        Assert.Null(inbox.Day);
        Assert.Equal(0, Planner.CatchUp(d, tomorrow)); // una vez por día

        // Dos días después sigue recordando el día original
        Planner.CatchUp(d, Now.AddDays(2));
        Assert.Equal(Today, old.CarriedFrom);
    }

    [Fact]
    public void SinTraerPendientesSeQuedanEnSuDia()
    {
        var d = new AppData();
        d.Settings.CarryForward = false;
        var t = Planner.AddFromLine(d, "Pendiente", Today, Now);
        Assert.Equal(0, Planner.CatchUp(d, Now.AddDays(1)));
        Assert.Equal(Today, t.Day);
        Assert.Equal([t], Planner.ForDay(d, Today, Now.AddDays(1)));
    }

    [Fact]
    public void UnDiaPasadoEnsenaLoQueSeHizoEseDia()
    {
        var d = new AppData();
        var a = Planner.AddFromLine(d, "A", Today, Now);
        var b = Planner.AddFromLine(d, "B", Today, Now);
        Planner.Complete(d, a.Id, Now);
        var tomorrow = Now.AddDays(1);
        Planner.CatchUp(d, tomorrow);
        Assert.Equal([a], Planner.ForDay(d, Today, tomorrow));
        Assert.Equal([b], Planner.ForDay(d, Today.AddDays(1), tomorrow));
        Assert.Throws<UserException>(() => Planner.Reopen(d, a.Id, tomorrow));
    }

    [Fact]
    public void MoverALaBandejaQuitaAvisoYRepeticion()
    {
        var d = new AppData();
        var t = Planner.AddFromLine(d, "Regar 18:00 cada día", Today, Now);
        Planner.Move(d, t.Id, null, Now);
        Assert.Null(t.Day);
        Assert.Null(t.RemindAt);
        Assert.Equal(Repeat.None, t.Repeat);
        Assert.Throws<UserException>(() => Planner.Move(d, t.Id, Today.AddDays(-1), Now));
    }

    [Fact]
    public void MoverDeDiaConservaLaHoraDelAviso()
    {
        var d = new AppData();
        var t = Planner.AddFromLine(d, "Llamar 18:00", Today, Now);
        Planner.Move(d, t.Id, Today.AddDays(2), Now);
        Assert.Equal(Today.AddDays(2).ToDateTime(new TimeOnly(18, 0)), t.RemindAt);
    }

    [Fact]
    public void SubirYBajar()
    {
        var d = new AppData();
        var a = Planner.AddFromLine(d, "A", Today, Now);
        var other = Planner.AddFromLine(d, "Otro día", Today.AddDays(1), Now);
        var b = Planner.AddFromLine(d, "B", Today, Now);
        Planner.Shift(d, b.Id, -1);
        Assert.Equal([b, a], Planner.ForDay(d, Today, Now));
        Planner.Shift(d, b.Id, -1); // ya es la primera
        Assert.Equal([b, a], Planner.ForDay(d, Today, Now));
        Assert.Equal([other], Planner.ForDay(d, Today.AddDays(1), Now));
    }

    [Fact]
    public void NombresDeLosDias()
    {
        Assert.Equal("Bandeja", Planner.DayName(null, Today));
        Assert.Equal("Hoy", Planner.DayName(Today, Today));
        Assert.Equal("Mañana", Planner.DayName(Today.AddDays(1), Today));
        Assert.Equal("Ayer", Planner.DayName(Today.AddDays(-1), Today));
        Assert.Equal("Viernes", Planner.DayName(Today.AddDays(2), Today));
    }
}
