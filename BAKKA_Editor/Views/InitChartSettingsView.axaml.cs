using Avalonia.Controls;

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