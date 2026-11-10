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

/// <summary>Lo que se puede comprobar del diseño leyendo el XAML y el C# de la app, sin pintarla.</summary>
public partial class FuentesTests
{
    private static readonly string Root = FindRoot();
    private static readonly string AppFolder = Path.Combine(Root, "src", "Worknotch.App");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Worknotch.sln"))) return dir.FullName;
        throw new InvalidOperationException("No encuentro la raíz del repositorio.");
    }

    private static IEnumerable<(string Name, string Text)> Files(string pattern) =>
        Directory.EnumerateFiles(AppFolder, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(f => (Path.GetRelativePath(Root, f), File.ReadAllText(f)));

    [Fact]
    public void ElXamlNoLlevaColoresEscritosAMano()
    {
        var bad = new List<string>();
        foreach (var (name, text) in Files("*.xaml"))
        {
            foreach (System.Text.RegularExpressions.Match m in HexColor().Matches(text)) bad.Add($"{name}: {m.Value}");
            foreach (System.Text.RegularExpressions.Match m in NamedColor().Matches(text)) bad.Add($"{name}: {m.Value}");
            foreach (System.Text.RegularExpressions.Match m in StaticToken().Matches(text)) bad.Add($"{name}: {m.Value} debe ser DynamicResource, o no cambia con el tema");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    [Fact]
    public void ElCodigoNoLlevaColoresEscritosAMano()
    {
        var bad = Files("*.cs")
            .Where(f => !f.Name.EndsWith("Tema.cs"))
            .SelectMany(f => CodeColor().Matches(f.Text).Select(m => $"{f.Name}: {m.Value}"))
            .ToList();
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    [Fact]
    public void LosTokensQueUsaElXamlExisten()
    {
        var tokens = Tokens.Parse(TokensFile.Read()).Night.Keys.ToHashSet();
        var missing = Files("*.xaml")
            .SelectMany(f => UsedToken().Matches(f.Text).Select(m => (f.Name, Token: m.Groups[1].Value)))
            .Where(u => !tokens.Contains(u.Token))
            .Select(u => $"{u.Name}: {u.Token}")
            .Distinct()
            .ToList();
        Assert.True(missing.Count == 0, string.Join("\n", missing));
    }

    [Fact]
    public void NadaVisibleBajaDe12()
    {
        var bad = Files("*.xaml")
            .SelectMany(f => FontSize().Matches(f.Text).Select(m => (f.Name, Size: double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))))
            .Where(s => s.Size < 12)
            .Select(s => $"{s.Name}: FontSize {s.Size}")
            .ToList();
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    [Fact]
    public void LosCamposLlevanAyudaYNombre()
    {
        var bad = new List<string>();
        foreach (var (name, text) in Files("*.xaml"))
            foreach (System.Text.RegularExpressions.Match m in TextBoxTag().Matches(text))
            {
                string box = m.Value;
                var tag = System.Text.RegularExpressions.Regex.Match(box, "Tag=\"([^\"]*)\"");
                if (!tag.Success || !tag.Groups[1].Value.EndsWith('…')) bad.Add($"{name}: un campo sin ayuda acabada en «…»: {box[..Math.Min(80, box.Length)]}");
                if (!box.Contains("AutomationProperties.Name=")) bad.Add($"{name}: un campo sin nombre: {box[..Math.Min(80, box.Length)]}");
            }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    [Fact]
    public void LosBotonesDeIconoLlevanNombre()
    {
        var bad = new List<string>();
        foreach (var (name, text) in Files("*.xaml"))
            foreach (System.Text.RegularExpressions.Match m in IconButton().Matches(text))
                if (!m.Value.Contains("AutomationProperties.Name=")) bad.Add($"{name}: {m.Value[..Math.Min(100, m.Value.Length)]}");
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    [System.Text.RegularExpressions.GeneratedRegex("\"#[0-9A-Fa-f]{3,8}\"")]
    private static partial System.Text.RegularExpressions.Regex HexColor();

    [System.Text.RegularExpressions.GeneratedRegex("(Background|Foreground|Fill|Stroke|BorderBrush)=\"(?!Transparent\")[A-Z][A-Za-z]+\"")]
    private static partial System.Text.RegularExpressions.Regex NamedColor();

    [System.Text.RegularExpressions.GeneratedRegex(@"\{StaticResource wn-[a-z-]+\}")]
    private static partial System.Text.RegularExpressions.Regex StaticToken();

    [System.Text.RegularExpressions.GeneratedRegex(@"\{DynamicResource (wn-[a-z-]+)\}")]
    private static partial System.Text.RegularExpressions.Regex UsedToken();

    [System.Text.RegularExpressions.GeneratedRegex(@"Color\.From(A?)Rgb|Colors\.[A-Z]|Brushes\.[A-Z]|""#[0-9A-Fa-f]{6,8}""")]
    private static partial System.Text.RegularExpressions.Regex CodeColor();

    [System.Text.RegularExpressions.GeneratedRegex("FontSize\"? ?(?:Value)?=\"([0-9.]+)\"")]
    private static partial System.Text.RegularExpressions.Regex FontSize();

    [System.Text.RegularExpressions.GeneratedRegex("<TextBox [^>]*>", System.Text.RegularExpressions.RegexOptions.Singleline)]
    private static partial System.Text.RegularExpressions.Regex TextBoxTag();

    [System.Text.RegularExpressions.GeneratedRegex("<Button [^>]*BotonIcono[^>]*>", System.Text.RegularExpressions.RegexOptions.Singleline)]
    private static partial System.Text.RegularExpressions.Regex IconButton();
}
