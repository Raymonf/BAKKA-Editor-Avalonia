using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.ReactiveUI;

namespace BAKKA_Editor.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .With(() =>
        {
            if (!OperatingSystem.IsWindows())
            {
                return new FontManagerOptions
                {
                    DefaultFamilyName = "avares://BAKKA_Editor/Assets/Roboto-Regular.ttf#Roboto"
                };
            }

            return new FontManagerOptions();
        })
        .With(() =>
        {
            if (OperatingSystem.IsMacOS())
            {
                return new AvaloniaNativePlatformOptions
                {
                    RenderingMode = new[]
                    {
#pragma warning disable CS0618
                        // experimental option
                        AvaloniaNativeRenderingMode.Metal,
#pragma warning restore CS0618
                        AvaloniaNativeRenderingMode.OpenGl,
                        AvaloniaNativeRenderingMode.Software
                    }
                };
            }

            return new AvaloniaNativePlatformOptions();
        })
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            //.LogToTrace()
            .UseReactiveUI();
}
