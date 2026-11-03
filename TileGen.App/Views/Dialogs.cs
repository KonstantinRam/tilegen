using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using TileGen.App.ViewModels;

namespace TileGen.App.Views;

/// <summary>The window side of IDialogs: storage pickers and small modal prompts built in code.</summary>
public sealed class Dialogs : IDialogs
{
    private readonly Window _owner;

    public Dialogs(Window owner)
    {
        _owner = owner;
    }

    public async Task<string?> PickOpenFileAsync(string title, string filterName, IReadOnlyList<string> patterns)
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType(filterName) { Patterns = patterns.ToList() } },
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PromptTextAsync(string title, string label, string initial)
    {
        var box = new TextBox { Text = initial, Width = 320 };
        var body = new StackPanel { Spacing = 6, Children = { new TextBlock { Text = label }, box } };
        return await ShowAsync(title, body, box) ? box.Text : null;
    }

    public async Task<(string Id, string DisplayName)?> PromptPresetNameAsync(string initialId, string initialName)
    {
        var id = new TextBox { Text = initialId, Width = 320 };
        var name = new TextBox { Text = initialName, Width = 320 };
        var body = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "Id (letters, digits and underscores; the file name)" },
                id,
                new TextBlock { Text = "Display name" },
                name,
            },
        };
        if (!await ShowAsync("Save preset as", body, id))
        {
            return null;
        }
        return (id.Text ?? "", name.Text ?? "");
    }

    public async Task<PaletteChoice?> PromptPaletteAsync(IReadOnlyList<PaletteTarget> targets, PaletteTarget initial)
    {
        var target = new ComboBox { ItemsSource = targets, SelectedItem = initial, DisplayMemberBinding = new Avalonia.Data.Binding("Label"), Width = 320 };
        var stops = new NumericUpDown { Minimum = 1, Maximum = 8, Increment = 1, Value = 4, FormatString = "0", Width = 120 };
        var seed = new CheckBox { Content = "Also use the image as the base seed", IsChecked = false };
        var body = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "Material that receives the ramp" },
                target,
                new TextBlock { Text = "Number of colors" },
                stops,
                seed,
                new TextBlock { Text = "Only colors and a seed come from the image. The structure stays the preset's.", TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 320, Opacity = 0.7 },
            },
        };
        if (!await ShowAsync("Palette from image", body, target))
        {
            return null;
        }
        return new PaletteChoice((PaletteTarget)(target.SelectedItem ?? initial), (int)(stops.Value ?? 4), seed.IsChecked == true);
    }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        return ShowAsync(title, new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 400 }, null, "Yes", "No");
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        await ShowAsync(title, new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 480 }, null, "OK", null);
    }

    private async Task<bool> ShowAsync(string title, Control body, Control? focus, string okText = "OK", string? cancelText = "Cancel")
    {
        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
        };
        var ok = new Button { Content = okText, IsDefault = true, MinWidth = 80 };
        ok.Click += (_, _) => dialog.Close(true);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok } };
        if (cancelText is not null)
        {
            var cancel = new Button { Content = cancelText, IsCancel = true, MinWidth = 80 };
            cancel.Click += (_, _) => dialog.Close(false);
            buttons.Children.Add(cancel);
        }
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 12, Children = { body, buttons } };
        dialog.Opened += (_, _) => focus?.Focus();
        return await dialog.ShowDialog<bool>(_owner);
    }
}
