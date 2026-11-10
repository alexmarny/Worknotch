namespace Worknotch.Core;

/// <summary>
/// Las cifras de la pantalla de puntos del temporizador, en una rejilla de 5 × 7. Cada glifo son
/// siete filas; «#» es un punto encendido. Los dos puntos ocupan una sola columna.
/// </summary>
public static class DotMatrix
{
    public const int Rows = 7;

    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['0'] = [" ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### "],
        ['1'] = ["  #  ", " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### "],
        ['2'] = [" ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####"],
        ['3'] = [" ### ", "#   #", "    #", "  ## ", "    #", "#   #", " ### "],
        ['4'] = ["   # ", "  ## ", " # # ", "#  # ", "#####", "   # ", "   # "],
        ['5'] = ["#####", "#    ", "#### ", "    #", "    #", "#   #", " ### "],
        ['6'] = [" ### ", "#    ", "#    ", "#### ", "#   #", "#   #", " ### "],
        ['7'] = ["#####", "    #", "   # ", "  #  ", "  #  ", "  #  ", "  #  "],
        ['8'] = [" ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### "],
        ['9'] = [" ### ", "#   #", "#   #", " ####", "    #", "    #", " ### "],
        [':'] = [" ", " ", "#", " ", "#", " ", " "],
        [' '] = ["  ", "  ", "  ", "  ", "  ", "  ", "  "],
    };

    public static string[] Glyph(char c) => Glyphs.TryGetValue(c, out var g) ? g : Glyphs[' '];

    /// <summary>El texto entero como filas de celdas, con una columna vacía entre glifos.</summary>
    public static bool[,] Render(string text)
    {
        var glyphs = text.Select(Glyph).ToList();
        int cols = glyphs.Sum(g => g[0].Length) + Math.Max(0, glyphs.Count - 1);
        var cells = new bool[Rows, Math.Max(cols, 1)];
        int x = 0;
        foreach (var g in glyphs)
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < g[r].Length; c++)
                    cells[r, x + c] = g[r][c] == '#';
            x += g[0].Length + 1;
        }
        return cells;
    }
}
