using System.Windows;
using System.Windows.Media;

namespace Worknotch.App.Controles;

/// <summary>
/// La pantalla de puntos del temporizador: cada cifra en una rejilla de 5 × 7, con los puntos
/// apagados a la vista, como un marcador. El tamaño sale de <see cref="Dot"/> (diámetro) y
/// <see cref="Gap"/> (separación).
/// </summary>
public sealed class DotDisplay : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(DotDisplay),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DotProperty = DependencyProperty.Register(
        nameof(Dot), typeof(double), typeof(DotDisplay),
        new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(DotDisplay),
        new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OnBrushProperty = DependencyProperty.Register(
        nameof(OnBrush), typeof(Brush), typeof(DotDisplay), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OffBrushProperty = DependencyProperty.Register(
        nameof(OffBrush), typeof(Brush), typeof(DotDisplay), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double Dot { get => (double)GetValue(DotProperty); set => SetValue(DotProperty, value); }
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public Brush? OnBrush { get => (Brush?)GetValue(OnBrushProperty); set => SetValue(OnBrushProperty, value); }
    public Brush? OffBrush { get => (Brush?)GetValue(OffBrushProperty); set => SetValue(OffBrushProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        int cols = DotMatrix.Render(Text ?? "").GetLength(1);
        double step = Dot + Gap;
        return new Size(Math.Max(0, cols * step - Gap), DotMatrix.Rows * step - Gap);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var cells = DotMatrix.Render(Text ?? "");
        double step = Dot + Gap, r = Dot / 2;
        for (int row = 0; row < cells.GetLength(0); row++)
            for (int col = 0; col < cells.GetLength(1); col++)
            {
                var brush = cells[row, col] ? OnBrush : OffBrush;
                if (brush != null) dc.DrawEllipse(brush, null, new Point(col * step + r, row * step + r), r, r);
            }
    }
}
