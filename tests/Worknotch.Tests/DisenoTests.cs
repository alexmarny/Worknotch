namespace Worknotch.Tests;

/// <summary>La guía de diseño hecha pruebas (docs/design-system/README.md). Si algo nuevo no pasa, se arregla lo nuevo.</summary>
public class DisenoTests
{
    private static readonly Tokens T = Tokens.Parse(TokensFile.Read());

    [Fact]
    public void CadaTokenTieneValorEnLosDosTemas()
    {
        Assert.NotEmpty(T.Night);
        Assert.Equal(T.Night.Keys.Order(), T.Day.Keys.Order());
        Assert.All(T.Night.Values.Concat(T.Day.Values), v => Assert.Matches("^#[0-9A-F]{6}$", v));
    }

    [Fact]
    public void LosContrastesSeCumplenEnNocheYEnDia()
    {
        Assert.NotEmpty(T.Contrasts);
        var failures = new List<string>();
        foreach (var rule in T.Contrasts)
            foreach (var (theme, colors) in new[] { ("Noche", T.Night), ("Día", T.Day) })
            {
                double c = Tokens.Contrast(colors[rule.Text], colors[rule.Background]);
                if (c < rule.Ratio) failures.Add($"{rule.Text} sobre {rule.Background} en {theme}: {c:0.00} (mínimo {rule.Ratio})");
            }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void ContrasteDeReferencia()
    {
        Assert.Equal(21, Tokens.Contrast("#000000", "#FFFFFF"), 2);
        Assert.Equal(1, Tokens.Contrast("#808080", "#808080"), 2);
    }
}
