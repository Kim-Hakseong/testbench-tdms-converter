using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Tdms.App.Localization;
using Tdms.App.ViewModels;

namespace Tdms.App.Views;

/// <summary>Channel picker, format choice and progress for an export.</summary>
public sealed partial class ExportWindow : Window
{
    /// <summary>Creates the window.</summary>
    public ExportWindow() => InitializeComponent();

    private ExportViewModel? ViewModel => DataContext as ExportViewModel;

    private void OnSelectAllClick(object? sender, RoutedEventArgs e) => ViewModel?.SelectAll();

    private void OnSelectNoneClick(object? sender, RoutedEventArgs e) => ViewModel?.SelectNone();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => ViewModel?.Cancel();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Loc.T("SaveDialogTitle"),
            SuggestedFileName = viewModel.SuggestedFileName,
            DefaultExtension = "csv",
            FileTypeChoices =
            [
                new FilePickerFileType(Loc.T("CsvFiles")) { Patterns = ["*.csv"] },
            ],
        });

        if (file?.TryGetLocalPath() is not { } path)
        {
            return;
        }

        await viewModel.ExportAsync(path);
    }
}
