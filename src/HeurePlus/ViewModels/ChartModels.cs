using HeurePlus.Infrastructure;

namespace HeurePlus.ViewModels;

/// <summary>Une barre d'un mini-graphique (hauteur en pixels pré-calculée).</summary>
public sealed class BarViewModel : ObservableObject
{
    public BarViewModel(string label, double value, double pixelHeight, string tooltip)
    {
        Label = label;
        Value = value;
        PixelHeight = pixelHeight;
        Tooltip = tooltip;
    }

    public string Label { get; }
    public double Value { get; }
    public double PixelHeight { get; }
    public string Tooltip { get; }
    public bool IsEmpty => Value <= 0;
}

/// <summary>Élément de légende (répartition des jours).</summary>
public sealed class LegendItem
{
    public LegendItem(string label, int count, string colorKey)
    {
        Label = label;
        Count = count;
        ColorKey = colorKey;
    }

    public string Label { get; }
    public int Count { get; }
    public string ColorKey { get; }
    public string CountText => Count.ToString();
}
