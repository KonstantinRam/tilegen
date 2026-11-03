using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TileGen.Core;
using TileGen.IO;

namespace TileGen.App.ViewModels;

public sealed record PresetEntryViewModel(string Path, string Id, string DisplayName, string? Error)
{
    public string Label => Error is null ? DisplayName : DisplayName + " (unreadable)";
}

public sealed partial class FavoriteViewModel : ObservableObject
{
    public FavoriteViewModel(Favorite favorite)
    {
        Favorite = favorite;
        _name = favorite.Name;
    }

    public Favorite Favorite { get; private set; }

    [ObservableProperty]
    private string _name;

    public string Detail => $"{Favorite.Preset.DisplayName}, seed {Favorite.BaseSeed}, {Favorite.Size} px, {Favorite.VariantCount} variants";

    public void Rename(string name)
    {
        Favorite = Favorite with { Name = name };
        Name = name;
    }
}

/// <summary>
/// The one state of the window: the current preset with its coverage values, the base seed, the size and the variant
/// count. Everything else is derived from it, and every change ends in one place that re-renders the previews.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly PreviewRenderer _renderer = new();
    private readonly string _presetFolder;
    private Preset _preset;
    private string? _loadedPresetPath;
    private IReadOnlyList<Variant>? _pinnedVariants;
    private bool _suppress;
    private UiState _uiState;
    private readonly bool _persist;

    public MainViewModel()
        : this(PresetFolder.Locate(), persistAppData: true)
    {
    }

    /// <summary>With persistAppData false nothing is read from or written to the user's application data, which the tests rely on.</summary>
    public MainViewModel(string presetFolder, bool persistAppData)
    {
        _presetFolder = presetFolder;
        _persist = persistAppData;
        _uiState = _persist ? AppData.LoadUiState() : new UiState(null, null, false);
        _writeContactSheet = _uiState.WriteContactSheet;
        RefreshPresetList();
        var entry = Presets.FirstOrDefault(p => p.Error is null && p.Id == _uiState.LastPresetId) ?? Presets.FirstOrDefault(p => p.Error is null);
        _preset = entry is null ? BuiltInPreset() : PresetFile.Load(entry.Path);
        _loadedPresetPath = entry?.Path;
        _setName = _preset.Id;
        _variantCount = _preset.Variants;
        _baseSeed = NewSeed();
        _baseSeedText = _baseSeed.ToString(CultureInfo.InvariantCulture);

        Wall = new MaterialColumnViewModel(TileKind.Wall, _preset.Wall, edit => EditTileType(TileKind.Wall, edit));
        Floor = new MaterialColumnViewModel(TileKind.Floor, _preset.Floor, edit => EditTileType(TileKind.Floor, edit));
        Room = new RoomPreviewViewModel(() => ScheduleRender(floor: true, wall: true));
        if (_persist)
        {
            foreach (var favorite in LoadFavoritesSafely())
            {
                Favorites.Add(new FavoriteViewModel(favorite));
            }
        }

        _renderer.Rendered += OnRendered;
        _renderer.Failed += message => Status = "Render failed: " + message;
        _renderer.BusyChanged += busy => IsRendering = busy;
        ScheduleRender(floor: true, wall: true);
    }

    public IDialogs? Dialogs { get; set; }

    public MaterialColumnViewModel Wall { get; }
    public MaterialColumnViewModel Floor { get; }
    public RoomPreviewViewModel Room { get; }
    public ObservableCollection<PresetEntryViewModel> Presets { get; } = new();
    public ObservableCollection<FavoriteViewModel> Favorites { get; } = new();
    public IReadOnlyList<int> Sizes => Generator.SupportedSizes;

    public Preset CurrentPreset => _preset;
    public string PresetFolderPath => _presetFolder;

    /// <summary>The variants the previews show and the export writes: the ones opened from a settings.json until something changes, otherwise derived.</summary>
    public IReadOnlyList<Variant> CurrentVariants => _pinnedVariants ?? Variants.Resolve(_preset, BaseSeed, VariantCount);

    public int PreviewSize => Room.FullSize ? Size : Math.Min(256, Size);

    [ObservableProperty]
    private PresetEntryViewModel? _selectedPreset;

    [ObservableProperty]
    private FavoriteViewModel? _selectedFavorite;

    [ObservableProperty]
    private string _setName;

    [ObservableProperty]
    private int _size = 256;

    [ObservableProperty]
    private int _variantCount;

    [ObservableProperty]
    private ulong _baseSeed;

    [ObservableProperty]
    private string _baseSeedText;

    [ObservableProperty]
    private string _status = "Ready.";

    [ObservableProperty]
    private bool _isRendering;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private bool _writeContactSheet;

    [ObservableProperty]
    private string _presetDisplayName = "";

    /// <summary>The column the user touched last, which is where "palette from image" goes.</summary>
    [ObservableProperty]
    private TileKind _activeKind = TileKind.Floor;

    public string WindowTitle => $"TileGen - {_preset.DisplayName}";

    public Task WaitForIdleAsync()
    {
        return _renderer.WaitForIdleAsync();
    }

    // Set-level edits.

    partial void OnSizeChanged(int value)
    {
        if (!_suppress)
        {
            ScheduleRender(floor: true, wall: true);
        }
    }

    partial void OnVariantCountChanged(int value)
    {
        if (value < SetExporter.MinVariants)
        {
            VariantCount = SetExporter.MinVariants;
            return;
        }
        if (value > SetExporter.MaxVariants)
        {
            VariantCount = SetExporter.MaxVariants;
            return;
        }
        if (!_suppress)
        {
            _pinnedVariants = null;
            ScheduleRender(floor: true, wall: true);
        }
    }

    partial void OnBaseSeedChanged(ulong value)
    {
        BaseSeedText = value.ToString(CultureInfo.InvariantCulture);
        if (!_suppress)
        {
            _pinnedVariants = null;
            ScheduleRender(floor: true, wall: true);
        }
    }

    partial void OnBaseSeedTextChanged(string value)
    {
        if (ulong.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed) && seed != BaseSeed)
        {
            BaseSeed = seed;
        }
    }

    partial void OnWriteContactSheetChanged(bool value)
    {
        SaveUiState(_uiState with { WriteContactSheet = value });
    }

    [RelayCommand]
    private void RerollSeed()
    {
        BaseSeed = NewSeed();
        Status = $"Seed {BaseSeed}.";
    }

    [RelayCommand]
    private void RandomizeParameters()
    {
        ReplacePreset(Variants.Randomize(_preset, NewSeed()), keepSeed: true, pinned: null);
        Status = "Parameters randomized within their ranges.";
    }

    [RelayCommand]
    private void RollBoth()
    {
        _suppress = true;
        BaseSeed = NewSeed();
        _suppress = false;
        ReplacePreset(Variants.Randomize(_preset, NewSeed()), keepSeed: true, pinned: null);
        Status = $"Seed {BaseSeed}, parameters randomized.";
    }

    [RelayCommand]
    private void ResetToPresetDefaults()
    {
        string? path = _loadedPresetPath ?? Presets.FirstOrDefault(p => p.Error is null && p.Id == _preset.Id)?.Path;
        if (path is null || !File.Exists(path))
        {
            Status = $"No preset file for '{_preset.Id}' to reset to.";
            return;
        }
        try
        {
            ReplacePreset(PresetFile.Load(path), keepSeed: true, pinned: null);
            Status = $"Reset to {_preset.DisplayName} defaults.";
        }
        catch (Exception e) when (e is PresetFormatException or IOException)
        {
            Status = e.Message;
        }
    }

    // Presets.

    [RelayCommand]
    private void LoadPreset()
    {
        if (SelectedPreset is null)
        {
            return;
        }
        if (SelectedPreset.Error is not null)
        {
            Status = SelectedPreset.Error;
            return;
        }
        try
        {
            var preset = PresetFile.Load(SelectedPreset.Path);
            _loadedPresetPath = SelectedPreset.Path;
            // A load draws a new seed (docs/GUI.md), so the first impression of a preset is never the same tile twice.
            _suppress = true;
            BaseSeed = NewSeed();
            VariantCount = preset.Variants;
            _suppress = false;
            ReplacePreset(preset, keepSeed: true, pinned: null);
            SaveUiState(_uiState with { LastPresetId = preset.Id });
            Status = $"Loaded {preset.DisplayName}.";
        }
        catch (Exception e) when (e is PresetFormatException or IOException)
        {
            Status = e.Message;
        }
    }

    [RelayCommand]
    private async Task SaveAsPresetAsync()
    {
        if (Dialogs is null)
        {
            return;
        }
        var names = await Dialogs.PromptPresetNameAsync(_preset.Id, _preset.DisplayName);
        if (names is null)
        {
            return;
        }
        string id = SetExporter.SanitizeSetName(names.Value.Id.Trim()).ToLowerInvariant();
        string path = PresetFolder.PathFor(_presetFolder, id);
        if (File.Exists(path) && !await Dialogs.ConfirmAsync("Overwrite preset", $"{Path.GetFileName(path)} exists. Overwrite it?"))
        {
            return;
        }
        try
        {
            var preset = _preset with { Id = id, DisplayName = names.Value.DisplayName.Trim() };
            PresetFile.Save(path, preset);
            _preset = preset;
            _loadedPresetPath = path;
            OnPropertyChanged(nameof(WindowTitle));
            RefreshPresetList();
            SelectedPreset = Presets.FirstOrDefault(p => p.Path == path);
            Status = $"Saved {path}.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowErrorAsync("Save failed", e.Message);
        }
    }

    [RelayCommand]
    private async Task DeletePresetAsync()
    {
        if (Dialogs is null || SelectedPreset is null)
        {
            return;
        }
        if (!await Dialogs.ConfirmAsync("Delete preset", $"Delete {Path.GetFileName(SelectedPreset.Path)}? This removes the file."))
        {
            return;
        }
        try
        {
            File.Delete(SelectedPreset.Path);
            RefreshPresetList();
            Status = "Preset deleted.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowErrorAsync("Delete failed", e.Message);
        }
    }

    // Favorites.

    [RelayCommand]
    private async Task AddFavoriteAsync()
    {
        string name = $"{_preset.DisplayName} {BaseSeed}";
        if (Dialogs is not null)
        {
            string? chosen = await Dialogs.PromptTextAsync("Add favorite", "Name", name);
            if (chosen is null)
            {
                return;
            }
            name = chosen.Trim().Length == 0 ? name : chosen.Trim();
        }
        Favorites.Add(new FavoriteViewModel(new Favorite(name, _preset, BaseSeed, Size, VariantCount)));
        SaveFavorites();
        Status = $"Favorite '{name}' added.";
    }

    [RelayCommand]
    private void LoadFavorite()
    {
        if (SelectedFavorite is null)
        {
            return;
        }
        var f = SelectedFavorite.Favorite;
        _suppress = true;
        BaseSeed = f.BaseSeed;
        Size = Generator.SupportedSizes.Contains(f.Size) ? f.Size : 256;
        VariantCount = f.VariantCount;
        _suppress = false;
        _loadedPresetPath = null;
        ReplacePreset(f.Preset, keepSeed: true, pinned: null);
        Status = $"Loaded favorite '{f.Name}'.";
    }

    [RelayCommand]
    private async Task RenameFavoriteAsync()
    {
        if (Dialogs is null || SelectedFavorite is null)
        {
            return;
        }
        string? name = await Dialogs.PromptTextAsync("Rename favorite", "Name", SelectedFavorite.Name);
        if (name is null || name.Trim().Length == 0)
        {
            return;
        }
        SelectedFavorite.Rename(name.Trim());
        SaveFavorites();
    }

    [RelayCommand]
    private async Task DeleteFavoriteAsync()
    {
        if (Dialogs is null || SelectedFavorite is null)
        {
            return;
        }
        if (!await Dialogs.ConfirmAsync("Delete favorite", $"Delete '{SelectedFavorite.Name}'?"))
        {
            return;
        }
        Favorites.Remove(SelectedFavorite);
        SaveFavorites();
    }

    // Menu actions.

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        if (Dialogs is null)
        {
            return;
        }
        string? path = await Dialogs.PickOpenFileAsync("Open settings.json", "Settings", new[] { "settings.json", "*.json" });
        if (path is null)
        {
            return;
        }
        try
        {
            var settings = SettingsFile.Load(path);
            _suppress = true;
            BaseSeed = settings.BaseSeed;
            Size = Generator.SupportedSizes.Contains(settings.Size) ? settings.Size : 256;
            VariantCount = settings.VariantCount;
            SetName = settings.SetName;
            _suppress = false;
            _loadedPresetPath = null;
            var preset = settings.Preset with
            {
                Floor = settings.Preset.Floor with { GarbageCoverage = settings.FloorCoverage },
                Wall = settings.Preset.Wall with { GarbageCoverage = settings.WallCoverage },
            };
            ReplacePreset(preset, keepSeed: true, pinned: settings.Variants);
            Status = settings.GeneratorVersion == Generator.Version
                ? $"Opened {path}."
                : $"Opened {path}, made with generator {settings.GeneratorVersion}; this is {Generator.Version}, so the bytes may differ.";
        }
        catch (Exception e) when (e is PresetFormatException or IOException)
        {
            await Dialogs.ShowErrorAsync("Open failed", e.Message);
        }
    }

    [RelayCommand]
    private async Task PaletteFromImageAsync()
    {
        if (Dialogs is null)
        {
            return;
        }
        var targets = new List<PaletteTarget>
        {
            new("Wall base", TileKind.Wall, TileTypeParameters.BasePrefix),
            new("Wall garbage", TileKind.Wall, TileTypeParameters.GarbagePrefix),
            new("Floor base", TileKind.Floor, TileTypeParameters.BasePrefix),
            new("Floor garbage", TileKind.Floor, TileTypeParameters.GarbagePrefix),
        };
        var active = ActiveKind == TileKind.Wall ? Wall : Floor;
        var initial = targets.First(t => t.Kind == ActiveKind && t.Prefix == active.SelectedPrefix);
        var choice = await Dialogs.PromptPaletteAsync(targets, initial);
        if (choice is null)
        {
            return;
        }
        string? path = await Dialogs.PickOpenFileAsync("Palette from image", "Images", new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp" });
        if (path is null)
        {
            return;
        }
        try
        {
            var result = PaletteExtractor.Extract(path, choice.Stops);
            EditTileType(choice.Target.Kind, type => choice.Target.Prefix == TileTypeParameters.BasePrefix
                ? type with { Base = type.Base with { Ramp = result.Ramp } }
                : type with { Garbage = type.Garbage with { Ramp = result.Ramp } });
            if (choice.UseImageAsSeed)
            {
                BaseSeed = result.Seed;
            }
            Status = $"Palette of {choice.Stops} colors from {Path.GetFileName(path)} into {choice.Target.Label.ToLowerInvariant()}.";
        }
        catch (Exception e) when (e is ArgumentException or IOException)
        {
            await Dialogs.ShowErrorAsync("Palette failed", e.Message);
        }
    }

    [RelayCommand]
    private async Task ChooseOutputFolderAsync()
    {
        if (Dialogs is null)
        {
            return;
        }
        string? folder = await Dialogs.PickFolderAsync("Output folder for exports");
        if (folder is not null)
        {
            SaveUiState(_uiState with { OutputFolder = folder });
            Status = $"Exports go to {folder}.";
        }
    }

    [RelayCommand]
    private async Task ExportSetAsync()
    {
        if (IsExporting)
        {
            return;
        }
        string? folder = _uiState.OutputFolder;
        if (folder is null || !Directory.Exists(folder))
        {
            if (Dialogs is null)
            {
                Status = "No output folder chosen.";
                return;
            }
            folder = await Dialogs.PickFolderAsync("Output folder for exports");
            if (folder is null)
            {
                return;
            }
            SaveUiState(_uiState with { OutputFolder = folder });
        }
        var settings = new ExportSettings(
            Generator.Version,
            SetExporter.SanitizeSetName(SetName),
            _preset,
            Size,
            VariantCount,
            BaseSeed,
            _preset.Floor.GarbageCoverage,
            _preset.Wall.GarbageCoverage,
            CurrentVariants);
        bool contact = WriteContactSheet;
        IsExporting = true;
        Status = $"Exporting {settings.SetName} at {Size}...";
        try
        {
            var result = await Task.Run(() => SetExporter.Export(settings, folder, contact));
            Status = result.Warnings.Count == 0
                ? $"Exported {result.Tiles.Count} tiles to {result.SetDirectory}."
                : $"Exported to {result.SetDirectory} with warnings: {string.Join(" ", result.Warnings)}";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Status = "Export failed: " + e.Message;
            if (Dialogs is not null)
            {
                await Dialogs.ShowErrorAsync("Export failed", e.Message);
            }
        }
        finally
        {
            IsExporting = false;
        }
    }

    // The one path every edit takes.

    private void EditTileType(TileKind kind, Func<TileType, TileType> edit)
    {
        TileType changed;
        try
        {
            changed = edit(_preset.Get(kind));
        }
        catch (ArgumentException e)
        {
            Status = e.Message;
            return;
        }
        _preset = kind == TileKind.Floor ? _preset with { Floor = changed } : _preset with { Wall = changed };
        _pinnedVariants = null;
        Column(kind).Refresh(changed);
        ScheduleRender(floor: kind == TileKind.Floor, wall: kind == TileKind.Wall);
    }

    private void ReplacePreset(Preset preset, bool keepSeed, IReadOnlyList<Variant>? pinned)
    {
        _preset = preset;
        _pinnedVariants = pinned;
        if (!keepSeed)
        {
            _suppress = true;
            BaseSeed = NewSeed();
            _suppress = false;
        }
        Wall.Refresh(preset.Wall);
        Floor.Refresh(preset.Floor);
        OnPropertyChanged(nameof(WindowTitle));
        ScheduleRender(floor: true, wall: true);
    }

    private void ScheduleRender(bool floor, bool wall)
    {
        IReadOnlyList<Variant> variants;
        try
        {
            variants = CurrentVariants;
        }
        catch (ArgumentException e)
        {
            Status = e.Message;
            return;
        }
        _renderer.Schedule(new RenderJob(_preset, variants, BaseSeed, PreviewSize, floor, wall));
    }

    private void OnRendered(TileKind kind, IReadOnlyList<TileImage> tiles)
    {
        Column(kind).SetTiles(tiles);
        if (kind == TileKind.Floor)
        {
            Room.FloorTiles = tiles;
        }
        else
        {
            Room.WallTiles = tiles;
        }
    }

    private MaterialColumnViewModel Column(TileKind kind)
    {
        return kind == TileKind.Floor ? Floor : Wall;
    }

    private void RefreshPresetList()
    {
        Presets.Clear();
        foreach (var entry in PresetFolder.List(_presetFolder))
        {
            Presets.Add(new PresetEntryViewModel(entry.Path, entry.Id, entry.DisplayName, entry.Error));
        }
    }

    private void SaveFavorites()
    {
        if (!_persist)
        {
            return;
        }
        try
        {
            AppData.SaveFavorites(Favorites.Select(f => f.Favorite).ToList());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = "Could not save favorites: " + e.Message;
        }
    }

    private static IReadOnlyList<Favorite> LoadFavoritesSafely()
    {
        try
        {
            return AppData.LoadFavorites();
        }
        catch (Exception e) when (e is PresetFormatException or IOException or UnauthorizedAccessException)
        {
            return Array.Empty<Favorite>();
        }
    }

    private void SaveUiState(UiState state)
    {
        _uiState = state;
        if (!_persist)
        {
            return;
        }
        try
        {
            AppData.SaveUiState(state);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = "Could not save UI state: " + e.Message;
        }
    }

    /// <summary>Seeds are drawn outside the pixel path, so System.Random is fine here.</summary>
    private static ulong NewSeed()
    {
        return (ulong)Random.Shared.NextInt64(0, long.MaxValue);
    }

    /// <summary>A minimal preset so the window opens even when the presets folder is empty.</summary>
    private static Preset BuiltInPreset()
    {
        var ramp = new[] { new RampStop(0.0, new Rgb(20, 20, 24)), new RampStop(1.0, new Rgb(140, 140, 150)) };
        var material = new Material { Structure = new FieldStructure(), Ramp = ramp };
        var type = new TileType { Base = material, Garbage = material };
        return new Preset { Id = "untitled", DisplayName = "Untitled", Floor = type, Wall = type };
    }
}
