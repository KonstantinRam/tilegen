using CommunityToolkit.Mvvm.ComponentModel;

namespace TileGen.App.ViewModels;

/// <summary>The room preview: walls around a floor, variants mixed, with zoom, room size and the full-size switch.</summary>
public sealed partial class RoomPreviewViewModel : ObservableObject
{
    public static readonly IReadOnlyList<string> ZoomModes = new[] { "Fit", "1:1", "2:1" };

    private readonly Action _fullSizeChanged;

    public RoomPreviewViewModel(Action fullSizeChanged)
    {
        _fullSizeChanged = fullSizeChanged;
    }

    public IReadOnlyList<string> Zooms => ZoomModes;

    /// <summary>Raised when the tiles or the layout change, so the control redraws once per change.</summary>
    public event Action? Changed;

    [ObservableProperty]
    private string _zoom = ZoomModes[0];

    [ObservableProperty]
    private int _roomSize = 8;

    [ObservableProperty]
    private bool _fullSize;

    [ObservableProperty]
    private IReadOnlyList<TileImage> _floorTiles = Array.Empty<TileImage>();

    [ObservableProperty]
    private IReadOnlyList<TileImage> _wallTiles = Array.Empty<TileImage>();

    public bool IsFit => Zoom == ZoomModes[0];
    public double Scale => Zoom == ZoomModes[2] ? 2.0 : 1.0;

    partial void OnZoomChanged(string value)
    {
        OnPropertyChanged(nameof(IsFit));
        OnPropertyChanged(nameof(Scale));
        Changed?.Invoke();
    }

    partial void OnRoomSizeChanged(int value)
    {
        Changed?.Invoke();
    }

    partial void OnFullSizeChanged(bool value)
    {
        _fullSizeChanged();
    }

    partial void OnFloorTilesChanged(IReadOnlyList<TileImage> value)
    {
        Changed?.Invoke();
    }

    partial void OnWallTilesChanged(IReadOnlyList<TileImage> value)
    {
        Changed?.Invoke();
    }
}
