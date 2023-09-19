using System;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace BAKKA_Editor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private MainView? GetMainView() => this.FindControl<MainView>("View");

    private void RunInView(Func<MainView?, Task> action)
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(() => action(view), DispatcherPriority.Background);
    }

    public void OnNewCommand()
    {
        RunInView(view =>
        {
            view?.NewMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    public void OnOpenCommand()
    {
        RunInView(view =>
        {
            view?.OpenMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    public void OnSaveCommand()
    {
        RunInView(view =>
        {
            view?.SaveMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    public void OnSaveAsCommand()
    {
        RunInView(view =>
        {
            view?.SaveAsMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    public void ExitMenuItem_OnClick()
    {
        RunInView(view =>
        {
            view?.ExitMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    public void OnUndoCommand()
    {
        RunInView(view =>
        {
            view?.UndoMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    public void OnRedoCommand()
    {
        RunInView(view =>
        {
            view?.RedoMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // we need this condition because Shutdown() calls Close()
        var view = GetMainView();
        if (view == null || view.CanShutdown)
            return;
        e.Cancel = true;
        Dispatcher.UIThread.Post(() => view.ExitMenuItem_OnClick(), DispatcherPriority.Background);
    }
}