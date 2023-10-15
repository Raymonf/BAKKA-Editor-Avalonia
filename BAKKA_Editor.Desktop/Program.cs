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
        // TODO: make this platform-specific
        // .With(new FontManagerOptions { DefaultFamilyName = "avares://BAKKA_Editor/Assets/Roboto-Regular.ttf#Roboto"})
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            //.LogToTrace()
            .UseReactiveUI();
}
