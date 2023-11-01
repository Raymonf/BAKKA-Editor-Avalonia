using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;

namespace BAKKA_Editor.Views.Settings;

public partial class SettingsWindow : UserControl
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void NavigationView_OnSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        var view = ((NavigationViewItem)e.SelectedItem).Tag switch
        {
            "General" => (UserControl) new GeneralSettingsView(),
            "Visual" => new VisualSettingsView(),
            "Color" => new ColorSettingsView(),
            _ => throw new ArgumentOutOfRangeException()
        };
        view.DataContext = DataContext;
        ((NavigationView) sender).Content = view;
    }
}