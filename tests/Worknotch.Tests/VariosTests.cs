namespace Worknotch.Tests;

public class EnlaceTests
{
    [Fact]
    public void AnadirConTodo()
    {
        var c = Link.Parse("worknotch://add?title=Comprar%20pan&day=ma%C3%B1ana&time=17:30&priority=alta&project=Mi%20casa");
        Assert.NotNull(c);
        Assert.Equal(LinkAction.Add, c.Action);
        var e = QuickAdd.Parse(c.Line, Dates.Today);
        Assert.Equal("Comprar pan", e.Title);
        Assert.Equal(Dates.Today.AddDays(1), e.Day);
        Assert.Equal(new TimeOnly(17, 30), e.Time);
        Assert.Equal(Priority.High, e.Priority);
        Assert.Equal("Mi casa", e.Project);
    }

    [Theory]
    [InlineData("worknotch://show")]
    [InlineData("worknotch://show/")]
    public void Mostrar(string url) => Assert.Equal(LinkAction.Show, Link.Parse(url)!.Action);

    [Theory]
    [InlineData("worknotch://add")]
    [InlineData("worknotch://add?title=%20")]
    [InlineData("worknotch://borrar-todo")]
    [InlineData("https://example.com/add?title=x")]
    [InlineData("")]
    [InlineData(null)]
    public void LoQueNoSeEntiendeSeIgnora(string? url) => Assert.Null(Link.Parse(url));
}

public class ActualizacionTests
{
    [Theory]
    [InlineData("v1.2.0", "1.1.9", true)]
    [InlineData("v1.10.0", "v1.9.0", true)]
    [InlineData("v1.2.0", "1.2.0", false)]
    [InlineData("v1.2.0", "1.3.0", false)]
    [InlineData("v1.3.0-beta.1", "1.2.0", false)]
    [InlineData("v1.3.0", "v1.2.0-4-gabc123", true)]
    [InlineData("cualquier cosa", "1.0.0", false)]
    [InlineData(null, "1.0.0", false)]
    public void EsMasNueva(string? latest, string installed, bool expected) =>
        Assert.Equal(expected, Update.IsNewer(latest, installed));

    [Fact]
    public void LaSumaDelInstaladorSeComprueba()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "instalador");
            string sum = Update.Sha256(file);
            string sums = $"{new string('0', 64)}  otro.exe\n{sum}  Worknotch-setup.exe\n";
            Assert.True(Update.Verify(file, sums, "Worknotch-setup.exe"));
            Assert.False(Update.Verify(file, sums, "otro.exe"));
            Assert.False(Update.Verify(file, sums, "no-esta.exe"));
            File.AppendAllText(file, "!");
            Assert.False(Update.Verify(file, sums, "Worknotch-setup.exe"));
        }
        finally { File.Delete(file); }
    }
}

public class MatrizPuntosTests
{
    [Fact]
    public void TodasLasCifrasSonDeCincoPorSiete()
    {
        foreach (char c in "0123456789")
        {
            var g = DotMatrix.Glyph(c);
            Assert.Equal(7, g.Length);
            Assert.All(g, row => Assert.Equal(5, row.Length));
        }
        Assert.Equal(10, "0123456789".Select(c => string.Join("", DotMatrix.Glyph(c))).Distinct().Count());
    }

    [Fact]
    public void ElAnchoDeUnaHora()
    {
        // 4 cifras de 5, los dos puntos de 1 y 4 separaciones
        Assert.Equal(25, DotMatrix.Render("25:00").GetLength(1));
        Assert.Equal(7, DotMatrix.Render("25:00").GetLength(0));
    }
}
