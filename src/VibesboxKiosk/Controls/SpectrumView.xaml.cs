using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using VibesboxKiosk.Services;
using Windows.UI;

namespace VibesboxKiosk.Controls;

public sealed partial class SpectrumView : UserControl
{
    private const double LabelAreaH = 40.0;
    // Width of the dashboard's left/right edge fades: grid lines under them only
    // half-fade and read as stray lines, so they aren't drawn there.
    private const double EdgeFadeW = 80.0;

    private double _minHz = 10, _maxHz = 30000, _floorDb = -70;
    private bool _gridLines = true;
    private LinearGradientBrush? _gridStroke;
    private float[] _bands = CreateDemoSpectrum();
    private readonly List<Line> _gridLineShapes = new();
    private readonly List<TextBlock> _freqLabels = new();
    private bool _initialized;

    // Decade grid (1-2-3…9 × 10^n) and labels, filtered to the lines strictly
    // inside (MinHz, MaxHz): a line exactly on an edge maps to x=0 or x=w and
    // its 1 px stroke bleeds past the edge fades, which already mark the bounds.
    private double[] _gridFreqs = [];
    private (double Freq, string Text)[] _labeledFreqs = [];

    private static readonly (double Freq, string Text)[] LabelCandidates =
    [
        (20, "20"), (100, "100"), (1000, "1k"), (10000, "10k"), (20000, "20k"),
    ];

    /// <summary>Sets the displayed range. Call before the control loads.</summary>
    public void Configure(double minHz, double maxHz, double floorDb, bool gridLines)
    {
        _minHz = minHz; _maxHz = maxHz; _floorDb = floorDb; _gridLines = gridLines;
    }

    private void BuildFrequencyTables()
    {
        var grid = new List<double>();
        for (double decade = 1; decade <= 100000; decade *= 10)
            for (int m = 1; m <= 9; m++)
            {
                double f = m * decade;
                if (f > _minHz && f < _maxHz) grid.Add(f);
            }
        _gridFreqs = grid.ToArray();
        _labeledFreqs = Array.FindAll(LabelCandidates, l => l.Freq > _minHz && l.Freq < _maxHz);
    }

    public SpectrumView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // Called by SpectrumService at ~30 Hz with live FFT data.
    public void UpdateBands(float[] bands)
    {
        _bands = bands;
        if (_initialized) Redraw();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildFrequencyTables();
        BuildGridLines();
        BuildFreqLabels();
        _initialized = true;
        ApplySize(ActualWidth, ActualHeight);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_initialized) return;
        ApplySize(e.NewSize.Width, e.NewSize.Height);
    }

    private void ApplySize(double w, double h)
    {
        if (w <= 0 || h <= 0) return;

        GridCanvas.Width     = w;
        GridCanvas.Height    = h;
        SpectrumCanvas.Width = w;
        SpectrumCanvas.Height = h;
        LabelCanvas.Width    = w;
        LabelCanvas.Height   = h;

        LayoutGridLines(w, h);
        LayoutFreqLabels(w, h);
        Redraw();
    }

    private void BuildGridLines()
    {
        // Lines fade in from the top so they don't run into the logo above.
        var lineColor = ThemeService.WithAlpha(ThemeService.Muted, 45);
        // Absolute mapping: a Line's bounding box is zero-wide, which a relative
        // brush can't map. EndPoint follows the height in LayoutGridLines.
        var stroke = _gridStroke = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute };
        stroke.GradientStops.Add(new GradientStop { Color = ThemeService.WithAlpha(ThemeService.Muted, 0), Offset = 0 });
        stroke.GradientStops.Add(new GradientStop { Color = lineColor, Offset = 1 });
        OutlineStartStop.Color = ThemeService.WithAlpha(ThemeService.AccentSoft, 0);
        OutlineStop1.Color     = ThemeService.AccentSoft;
        OutlineStop2.Color     = ThemeService.AccentSoft;
        OutlineEndStop.Color   = ThemeService.WithAlpha(ThemeService.AccentSoft, 0);
        if (!_gridLines) return;
        foreach (var _ in _gridFreqs)
        {
            var line = new Line { Stroke = stroke, StrokeThickness = 1, IsHitTestVisible = false };
            GridCanvas.Children.Add(line);
            _gridLineShapes.Add(line);
        }
    }

    private void BuildFreqLabels()
    {
        FontFamily? font = null;
        if (Application.Current.Resources.TryGetValue("PrimaryFontFamilyMedium", out var raw))
            font = raw as FontFamily;

        Brush? brush = null;
        if (Application.Current.Resources.TryGetValue("TextMutedBrush", out var bObj))
            brush = bObj as Brush;

        foreach (var (_, text) in _labeledFreqs)
        {
            var tb = new TextBlock
            {
                Text             = text,
                FontSize         = 12,
                CharacterSpacing = 150,
                IsHitTestVisible = false,
            };
            if (font is not null) tb.FontFamily = font;
            if (brush is not null) tb.Foreground = brush;
            LabelCanvas.Children.Add(tb);
            _freqLabels.Add(tb);
        }
    }

    private void LayoutGridLines(double w, double h)
    {
        double specH = h - LabelAreaH;
        if (_gridStroke is not null)
        {
            _gridStroke.StartPoint = new Point(0, 0);
            _gridStroke.EndPoint   = new Point(0, specH * 0.35);
        }
        for (int i = 0; i < _gridLineShapes.Count; i++)
        {
            double x = FreqToX(_gridFreqs[i], w);
            var line = _gridLineShapes[i];
            line.X1 = x; line.X2 = x;
            line.Y1 = 0; line.Y2 = specH;
            line.Visibility = x < EdgeFadeW || x > w - EdgeFadeW ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void LayoutFreqLabels(double w, double h)
    {
        double labelTop = h - LabelAreaH + 16;
        for (int i = 0; i < _freqLabels.Count; i++)
        {
            var tb = _freqLabels[i];
            double x = FreqToX(_labeledFreqs[i].Freq, w);
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, x - tb.DesiredSize.Width / 2);
            Canvas.SetTop(tb, labelTop);
        }
    }

    private void Redraw()
    {
        double w = SpectrumCanvas.Width;
        double h = SpectrumCanvas.Height;
        if (w <= 0 || h <= 0 || _bands.Length == 0) return;

        double specH = h - LabelAreaH;
        int n = _bands.Length;

        var pts = new Point[n];
        for (int i = 0; i < n; i++)
        {
            double freq = BandToFreq(i, n);
            double x    = FreqToX(freq, w);
            double norm = Math.Clamp((_bands[i] - _floorDb) / -_floorDb, 0.0, 1.0);

            // Shift line up 1px so it is hidden by the 4px baseline at the floor
            double y    = (specH * (1.0 - norm)) - 1.0;
            pts[i] = new Point(x, y);
        }

        var outlineFigure = new PathFigure { StartPoint = pts[0], IsFilled = false };
        var fillFigure = new PathFigure { StartPoint = new Point(pts[0].X, specH), IsFilled = true };
        fillFigure.Segments.Add(new LineSegment { Point = pts[0] });

        var outlineBezier = new PolyBezierSegment();
        var fillBezier = new PolyBezierSegment();
        
        for (int i = 0; i < n - 1; i++)
        {
            var p0 = i > 0 ? pts[i - 1] : pts[i];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = i < n - 2 ? pts[i + 2] : p2;

            // Simple Catmull-Rom to Cubic Bezier conversion
            double tension = 0.2;
            var cp1 = new Point(p1.X + (p2.X - p0.X) * tension, p1.Y + (p2.Y - p0.Y) * tension);
            var cp2 = new Point(p2.X - (p3.X - p1.X) * tension, p2.Y - (p3.Y - p1.Y) * tension);

            outlineBezier.Points.Add(cp1);
            outlineBezier.Points.Add(cp2);
            outlineBezier.Points.Add(p2);
            
            fillBezier.Points.Add(cp1);
            fillBezier.Points.Add(cp2);
            fillBezier.Points.Add(p2);
        }

        outlineFigure.Segments.Add(outlineBezier);
        fillFigure.Segments.Add(fillBezier);
        fillFigure.Segments.Add(new LineSegment { Point = new Point(pts[n - 1].X, specH) });

        var outlineGeo = new PathGeometry();
        outlineGeo.Figures.Add(outlineFigure);
        OutlinePath.Data = outlineGeo;

        var fillGeo = new PathGeometry();
        fillGeo.Figures.Add(fillFigure);
        FillPath.Data = fillGeo;
    }

    // Maps a frequency (Hz) to an x pixel position on a log10 scale spanning MinHz–MaxHz.
    private double FreqToX(double freq, double width)
        => width * Math.Log10(freq / _minHz) / Math.Log10(_maxHz / _minHz);

    // Maps a band index to its centre frequency, assuming log-spaced bands over MinHz–MaxHz.
    private double BandToFreq(int i, int n)
        => _minHz * Math.Pow(_maxHz / _minHz, (double)i / (n - 1));

    private static float[] CreateDemoSpectrum()
    {
        const int n = 64;
        var b = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / n;
            double v =
                -8  * Math.Exp(-Math.Pow((t - 0.08) / 0.06, 2))
              - 14  * Math.Exp(-Math.Pow((t - 0.22) / 0.12, 2))
              - 18  * Math.Exp(-Math.Pow((t - 0.48) / 0.10, 2))
              - 16  * Math.Exp(-Math.Pow((t - 0.70) / 0.08, 2))
              - 38  * t
              - 6
              + 4   * Math.Sin(t * 50) * (1 - t);
            b[i] = (float)Math.Clamp(v, -70.0, 0.0);
        }
        return b;
    }
}
