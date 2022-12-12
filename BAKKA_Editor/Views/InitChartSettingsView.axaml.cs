using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BAKKA_Editor.Views;

public partial class InitChartSettingsView : UserControl
{
    public InitChartSettingsView()
    {
        InitializeComponent();
#if DEBUG
        // this.AttachDevTools();
#endif
    }
}