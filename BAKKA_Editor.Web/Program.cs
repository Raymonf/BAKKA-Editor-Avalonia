using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Web;
using Avalonia.ReactiveUI;
using BAKKA_Editor;

[assembly: SupportedOSPlatform("browser")]

internal partial class Program
{
    private static void Main(string[] args) => BuildAvaloniaApp()
        .UseReactiveUI()
        .WithInterFont()
        .SetupBrowserApp("out");

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}