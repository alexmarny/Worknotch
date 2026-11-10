namespace Worknotch.Tests;

public class EntradaRapidaTests
{
    private static QuickEntry P(string line) => QuickAdd.Parse(line, Dates.Today);

    [Fact]
    public void SoloTitulo()
    {
        var e = P("Comprar pan");
        Assert.Equal("Comprar pan", e.Title);
        Assert.False(e.DayGiven);
        Assert.Null(e.Time);
        Assert.Equal(Priority.None, e.Priority);
    }

    [Fact]
    public void TodoEnUnaLinea()
    {
        var e = P("Llamar a mamá mañana 17:30 !alta #Familia");
        Assert.Equal("Llamar a mamá", e.Title);
        Assert.Equal(Dates.Today.AddDays(1), e.Day);
        Assert.Equal(new TimeOnly(17, 30), e.Time);
        Assert.Equal(Priority.High, e.Priority);
        Assert.Equal("Familia", e.Project);
    }

    [Theory]
    [InlineData("Gimnasio a las 7 pm", 19, 0)]
    [InlineData("Gimnasio 7pm", 19, 0)]
    [InlineData("Gimnasio a las 7", 7, 0)]
    [InlineData("Gimnasio 5:30 p.m.", 17, 30)]
    [InlineData("Gimnasio 12 am", 0, 0)]
    public void Horas(string line, int h, int m)
    {
        var e = P(line);
        Assert.Equal(new TimeOnly(h, m), e.Time);
        Assert.Equal("Gimnasio", e.Title);
    }

    [Fact]
    public void UnNumeroSueltoNoEsUnaHora()
    {
        var e = P("Comprar 12 huevos");
        Assert.Null(e.Time);
        Assert.Equal("Comprar 12 huevos", e.Title);
    }

    [Theory]
    [InlineData("hoy", 0)]
    [InlineData("pasado mañana", 2)]
    [InlineData("viernes", 2)]
    [InlineData("el lunes", 5)]
    [InlineData("miércoles", 7)] // hoy es miércoles: el siguiente
    [InlineData("2026-11-20", 9)]
    [InlineData("el 13/11", 2)]
    public void Dias(string word, int offset)
    {
        var e = P($"Entregar informe {word}");
        Assert.True(e.DayGiven);
        Assert.Equal(Dates.Today.AddDays(offset), e.Day);
        Assert.Equal("Entregar informe", e.Title);
    }

    [Fact]
    public void UnaFechaCortaYaPasadaEsDelAnioQueViene()
    {
        Assert.Equal(new DateOnly(2027, 1, 5), P("Reyes 5/1").Day);
    }

    [Fact]
    public void Bandeja()
    {
        var e = P("Idea: revisión semanal sin día");
        Assert.True(e.DayGiven);
        Assert.Null(e.Day);
        Assert.Equal("Idea: revisión semanal", e.Title);
        Assert.Equal(Repeat.None, e.Repeat); // «semanal» suelto es parte del título
    }

    [Theory]
    [InlineData("Regar las plantas cada semana", Repeat.Weekly)]
    [InlineData("Regar las plantas laborables", Repeat.Weekdays)]
    [InlineData("Regar las plantas cada día", Repeat.Daily)]
    [InlineData("Regar las plantas cada mes", Repeat.Monthly)]
    public void Repeticiones(string line, Repeat repeat)
    {
        var e = P(line);
        Assert.Equal(repeat, e.Repeat);
        Assert.Equal("Regar las plantas", e.Title);
    }

    [Theory]
    [InlineData("Preparar diapositivas 45m", 45)]
    [InlineData("Preparar diapositivas 45 min", 45)]
    [InlineData("Preparar diapositivas 2h", 120)]
    public void Minutos(string line, int minutes)
    {
        var e = P(line);
        Assert.Equal(minutes, e.Minutes);
        Assert.Equal("Preparar diapositivas", e.Title);
    }

    [Theory]
    [InlineData("Pagar la luz !", Priority.High)]
    [InlineData("Pagar la luz !!", Priority.High)]
    [InlineData("Pagar la luz !media", Priority.Medium)]
    [InlineData("Pagar la luz !baja", Priority.Low)]
    public void Prioridades(string line, Priority p)
    {
        var e = P(line);
        Assert.Equal(p, e.Priority);
        Assert.Equal("Pagar la luz", e.Title);
    }

    [Fact]
    public void LaExclamacionPegadaSeQuedaEnElTitulo()
    {
        Assert.Equal("¡Feliz cumpleaños!", P("¡Feliz cumpleaños!").Title);
    }

    [Fact]
    public void ProyectoConGuionBajo()
    {
        Assert.Equal("Trabajo fin de grado", P("Índice #Trabajo_fin_de_grado").Project);
    }
}
