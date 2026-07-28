using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Tdms.App.Localization;
using Tdms.App.ViewModels;

namespace Tdms.App.Views;

/// <summary>The main window.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the window.</summary>
    public MainWindow() => InitializeComponent();

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("OpenDialogTitle"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Loc.T("TdmsFiles")) { Patterns = ["*.tdms"] },
            ],
        });

        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
        {
            return;
        }

        await viewModel.OpenAsync(path);
    }

    private async void OnReloadClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.ReloadAsync();
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { Document: { } document })
        {
            return;
        }

        var dialog = new ExportWindow { DataContext = new ExportViewModel(document) };
        await dialog.ShowDialog(this);
    }
}
