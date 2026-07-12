using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;

namespace CdpInspectorApp.Controls;

public class GenerationsBarChart : Grid
{
    public static readonly StyledProperty<long> Gen0SizeProperty =
        AvaloniaProperty.Register<GenerationsBarChart, long>(nameof(Gen0Size), 0);

    public static readonly StyledProperty<long> Gen1SizeProperty =
        AvaloniaProperty.Register<GenerationsBarChart, long>(nameof(Gen1Size), 0);

    public static readonly StyledProperty<long> Gen2SizeProperty =
        AvaloniaProperty.Register<GenerationsBarChart, long>(nameof(Gen2Size), 0);

    public static readonly StyledProperty<long> LohSizeProperty =
        AvaloniaProperty.Register<GenerationsBarChart, long>(nameof(LohSize), 0);

    public long Gen0Size
    {
        get => GetValue(Gen0SizeProperty);
        set => SetValue(Gen0SizeProperty, value);
    }

    public long Gen1Size
    {
        get => GetValue(Gen1SizeProperty);
        set => SetValue(Gen1SizeProperty, value);
    }

    public long Gen2Size
    {
        get => GetValue(Gen2SizeProperty);
        set => SetValue(Gen2SizeProperty, value);
    }

    public long LohSize
    {
        get => GetValue(LohSizeProperty);
        set => SetValue(LohSizeProperty, value);
    }

    private readonly Border _gen0Border;
    private readonly Border _gen1Border;
    private readonly Border _gen2Border;
    private readonly Border _lohBorder;

    private readonly ColumnDefinition _col0;
    private readonly ColumnDefinition _col1;
    private readonly ColumnDefinition _col2;
    private readonly ColumnDefinition _col3;

    static GenerationsBarChart()
    {
        AffectsMeasure<GenerationsBarChart>(
            Gen0SizeProperty,
            Gen1SizeProperty,
            Gen2SizeProperty,
            LohSizeProperty);
    }

    public GenerationsBarChart()
    {
        _col0 = new ColumnDefinition(1, GridUnitType.Star);
        _col1 = new ColumnDefinition(1, GridUnitType.Star);
        _col2 = new ColumnDefinition(1, GridUnitType.Star);
        _col3 = new ColumnDefinition(1, GridUnitType.Star);

        ColumnDefinitions.Add(_col0);
        ColumnDefinitions.Add(_col1);
        ColumnDefinitions.Add(_col2);
        ColumnDefinitions.Add(_col3);

        _gen0Border = new Border
        {
            Background = Brush.Parse("#4caf50"), // Green
            CornerRadius = new CornerRadius(4, 0, 0, 4),
            Margin = new Thickness(0, 0, 2, 0)
        };
        _gen1Border = new Border
        {
            Background = Brush.Parse("#2196f3"), // Blue
            Margin = new Thickness(0, 0, 2, 0)
        };
        _gen2Border = new Border
        {
            Background = Brush.Parse("#ffeb3b"), // Yellow
            Margin = new Thickness(0, 0, 2, 0)
        };
        _lohBorder = new Border
        {
            Background = Brush.Parse("#ff9800"), // Orange
            CornerRadius = new CornerRadius(0, 4, 4, 0)
        };

        SetColumn(_gen0Border, 0);
        SetColumn(_gen1Border, 1);
        SetColumn(_gen2Border, 2);
        SetColumn(_lohBorder, 3);

        Children.Add(_gen0Border);
        Children.Add(_gen1Border);
        Children.Add(_gen2Border);
        Children.Add(_lohBorder);

        this.PropertyChanged += (sender, e) =>
        {
            if (e.Property == Gen0SizeProperty ||
                e.Property == Gen1SizeProperty ||
                e.Property == Gen2SizeProperty ||
                e.Property == LohSizeProperty)
            {
                UpdateLayoutAndToolTips();
            }
        };

        UpdateLayoutAndToolTips();
    }

    private void UpdateLayoutAndToolTips()
    {
        double g0 = Gen0Size;
        double g1 = Gen1Size;
        double g2 = Gen2Size;
        double loh = LohSize;
        double total = g0 + g1 + g2 + loh;

        double g0Mb = g0 / (1024.0 * 1024.0);
        double g1Mb = g1 / (1024.0 * 1024.0);
        double g2Mb = g2 / (1024.0 * 1024.0);
        double lohMb = loh / (1024.0 * 1024.0);

        ToolTip.SetTip(_gen0Border, $"Gen 0: {g0Mb:F2} MB ({(total > 0 ? g0 / total * 100.0 : 0):F1}%)");
        ToolTip.SetTip(_gen1Border, $"Gen 1: {g1Mb:F2} MB ({(total > 0 ? g1 / total * 100.0 : 0):F1}%)");
        ToolTip.SetTip(_gen2Border, $"Gen 2: {g2Mb:F2} MB ({(total > 0 ? g2 / total * 100.0 : 0):F1}%)");
        ToolTip.SetTip(_lohBorder, $"LOH: {lohMb:F2} MB ({(total > 0 ? loh / total * 100.0 : 0):F1}%)");

        if (total <= 0)
        {
            _col0.Width = new GridLength(1, GridUnitType.Star);
            _col1.Width = new GridLength(1, GridUnitType.Star);
            _col2.Width = new GridLength(1, GridUnitType.Star);
            _col3.Width = new GridLength(1, GridUnitType.Star);

            _gen0Border.IsVisible = false;
            _gen1Border.IsVisible = false;
            _gen2Border.IsVisible = false;
            _lohBorder.IsVisible = false;
        }
        else
        {
            _col0.Width = new GridLength(g0 > 0 ? g0 : 0.001, GridUnitType.Star);
            _col1.Width = new GridLength(g1 > 0 ? g1 : 0.001, GridUnitType.Star);
            _col2.Width = new GridLength(g2 > 0 ? g2 : 0.001, GridUnitType.Star);
            _col3.Width = new GridLength(loh > 0 ? loh : 0.001, GridUnitType.Star);

            _gen0Border.IsVisible = g0 > 0;
            _gen1Border.IsVisible = g1 > 0;
            _gen2Border.IsVisible = g2 > 0;
            _lohBorder.IsVisible = loh > 0;

            bool hasLeftRounded = false;
            if (g0 > 0) { _gen0Border.CornerRadius = new CornerRadius(4, 0, 0, 4); hasLeftRounded = true; }
            if (g1 > 0) { _gen1Border.CornerRadius = hasLeftRounded ? new CornerRadius(0) : new CornerRadius(4, 0, 0, 4); hasLeftRounded = true; }
            if (g2 > 0) { _gen2Border.CornerRadius = hasLeftRounded ? new CornerRadius(0) : new CornerRadius(4, 0, 0, 4); hasLeftRounded = true; }
            if (loh > 0) { _lohBorder.CornerRadius = hasLeftRounded ? new CornerRadius(0, 4, 4, 0) : new CornerRadius(4); }
            else
            {
                if (g2 > 0) _gen2Border.CornerRadius = new CornerRadius(_gen2Border.CornerRadius.TopLeft, 4, 4, _gen2Border.CornerRadius.BottomLeft);
                else if (g1 > 0) _gen1Border.CornerRadius = new CornerRadius(_gen1Border.CornerRadius.TopLeft, 4, 4, _gen1Border.CornerRadius.BottomLeft);
                else if (g0 > 0) _gen0Border.CornerRadius = new CornerRadius(4);
            }
        }
    }
}
