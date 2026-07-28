using Avalonia;
using Avalonia.Headless;
using Tdms.App;
using Tdms.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Tdms.App.Tests;

/// <summary>Avalonia app builder for the headless UI tests.</summary>
public static class TestAppBuilder
{
    /// <summary>
    /// Configures the headless platform. Skia is used instead of the headless drawing stub so
    /// that the app-wide Inter font produces real glyphs and the screenshot test can render.
    /// </summary>
    /// <returns>The configured builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .WithInterFont()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
