namespace TileGen.App.ViewModels;

/// <summary>Which material receives a palette from an image.</summary>
public sealed record PaletteTarget(string Label, Core.TileKind Kind, string Prefix);

public sealed record PaletteChoice(PaletteTarget Target, int Stops, bool UseImageAsSeed);

/// <summary>
/// What the view models need from the window: pickers, prompts and confirmations. The window implements it, and the
/// headless test leaves it null, so the view models never reference a UI type.
/// </summary>
public interface IDialogs
{
    Task<string?> PickOpenFileAsync(string title, string filterName, IReadOnlyList<string> patterns);
    Task<string?> PickFolderAsync(string title);
    Task<string?> PromptTextAsync(string title, string label, string initial);
    Task<(string Id, string DisplayName)?> PromptPresetNameAsync(string initialId, string initialName);
    Task<PaletteChoice?> PromptPaletteAsync(IReadOnlyList<PaletteTarget> targets, PaletteTarget initial);
    Task<bool> ConfirmAsync(string title, string message);
    Task ShowErrorAsync(string title, string message);
}
