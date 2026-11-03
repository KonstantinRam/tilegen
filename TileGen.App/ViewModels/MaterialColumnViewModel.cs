using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TileGen.Core;

namespace TileGen.App.ViewModels;

public sealed partial class VariantThumbViewModel : ObservableObject
{
    public VariantThumbViewModel(int index, Action<int> select)
    {
        Index = index;
        SelectCommand = new RelayCommand(() => select(index));
    }

    public int Index { get; }
    public string Label => (Index + 1).ToString("00");
    public IRelayCommand SelectCommand { get; }

    [ObservableProperty]
    private TileImage? _tile;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// One of the two material columns. It owns the previews of its tile type, the garbage slider and the two editors,
/// and every edit goes back to the main view model, which holds the one preset state.
/// </summary>
public sealed partial class MaterialColumnViewModel : ObservableObject
{
    private readonly Action<Func<TileType, TileType>> _edit;
    private bool _suppress;

    public MaterialColumnViewModel(TileKind kind, TileType type, Action<Func<TileType, TileType>> edit)
    {
        Kind = kind;
        Title = kind == TileKind.Floor ? "Floor" : "Wall";
        _edit = edit;
        _coverage = type.GarbageCoverage * 100.0;
        Base = new MaterialEditorViewModel(TileTypeParameters.BasePrefix, "Base", type, edit);
        Garbage = new MaterialEditorViewModel(TileTypeParameters.GarbagePrefix, "Garbage", type, edit);
    }

    public TileKind Kind { get; }
    public string Title { get; }
    public MaterialEditorViewModel Base { get; }
    public MaterialEditorViewModel Garbage { get; }
    public ObservableCollection<VariantThumbViewModel> Variants { get; } = new();

    /// <summary>The headline knob, shown as 0 to 100 while the preset stores a fraction.</summary>
    [ObservableProperty]
    private double _coverage;

    public decimal? CoverageValue
    {
        get => (decimal)Math.Round(Coverage);
        set
        {
            if (value.HasValue)
            {
                Coverage = (double)value.Value;
            }
        }
    }

    [ObservableProperty]
    private int _selectedVariant;

    [ObservableProperty]
    private TileImage? _selectedTile;

    [ObservableProperty]
    private int _selectedTab;

    /// <summary>Base or garbage, whichever tab is open, which is the target of "palette from image".</summary>
    public string SelectedPrefix => SelectedTab == 0 ? TileTypeParameters.BasePrefix : TileTypeParameters.GarbagePrefix;

    public void Refresh(TileType type)
    {
        _suppress = true;
        try
        {
            Coverage = type.GarbageCoverage * 100.0;
        }
        finally
        {
            _suppress = false;
        }
        Base.Refresh(type);
        Garbage.Refresh(type);
    }

    public void SetTiles(IReadOnlyList<TileImage> tiles)
    {
        while (Variants.Count > tiles.Count)
        {
            Variants.RemoveAt(Variants.Count - 1);
        }
        while (Variants.Count < tiles.Count)
        {
            Variants.Add(new VariantThumbViewModel(Variants.Count, i => SelectedVariant = i));
        }
        for (int i = 0; i < tiles.Count; i++)
        {
            Variants[i].Tile = tiles[i];
        }
        if (SelectedVariant >= tiles.Count)
        {
            SelectedVariant = 0;
        }
        UpdateSelection();
    }

    partial void OnCoverageChanged(double value)
    {
        OnPropertyChanged(nameof(CoverageValue));
        if (_suppress)
        {
            return;
        }
        double fraction = Math.Clamp(value, 0.0, 100.0) / 100.0;
        _edit(t => t with { GarbageCoverage = fraction });
    }

    partial void OnSelectedVariantChanged(int value)
    {
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < Variants.Count; i++)
        {
            Variants[i].IsSelected = i == SelectedVariant;
        }
        SelectedTile = SelectedVariant < Variants.Count ? Variants[SelectedVariant].Tile : null;
    }
}
