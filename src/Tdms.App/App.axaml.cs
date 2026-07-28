using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Tdms.App.ViewModels;
using Tdms.App.Views;

namespace Tdms.App;

/// <summary>Avalonia application root.</summary>
public sealed class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // A path on the command line opens straight away: drag a .tdms onto the exe.
            var file = desktop.Args?.FirstOrDefault(a => a.EndsWith(".tdms", StringComparison.OrdinalIgnoreCase));
            if (file is not null)
            {
                _ = viewModel.OpenAsync(file);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
