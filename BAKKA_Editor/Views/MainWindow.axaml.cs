using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ReactiveUI;

namespace BAKKA_Editor.Views;

public partial class MainWindow : Window, IPassSetting
{
    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        NewCommand = ReactiveCommand.CreateFromTask(NewMenuItem_OnClick);
        OpenCommand = ReactiveCommand.CreateFromTask(OpenMenuItem_OnClick);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveMenuItem_OnClick);
        SaveAsCommand = ReactiveCommand.CreateFromTask(SaveAsMenuItem_OnClick);
        ExitCommand = ReactiveCommand.CreateFromTask(ExitMenuItem_OnClick);
        UndoCommand = ReactiveCommand.Create(UndoMenuItem_OnClick);
        RedoCommand = ReactiveCommand.Create(RedoMenuItem_OnClick);
    }

    // Commands
    public ReactiveCommand<Unit, Unit> NewCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; set; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; set; }

    public async Task OpenChartSettings_OnClick()
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(async () => await view.OpenChartSettings_OnClick(), DispatcherPriority.Background);
    }

    public async Task NewMenuItem_OnClick()
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(async () => await view.NewMenuItem_OnClick(), DispatcherPriority.Background);
    }

    public async Task OpenMenuItem_OnClick()
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(async () => await view.OpenMenuItem_OnClick(), DispatcherPriority.Background);
    }

    public async Task SaveMenuItem_OnClick()
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(async () => await view.SaveMenuItem_OnClick(), DispatcherPriority.Background);
    }

    public async Task SaveAsMenuItem_OnClick()
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(async () => await view.SaveAsMenuItem_OnClick(), DispatcherPriority.Background);
    }

    public async Task ExitMenuItem_OnClick()
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(async () => await view.ExitMenuItem_OnClick(), DispatcherPriority.Background);
    }

    public void UndoMenuItem_OnClick()
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(() => view.UndoMenuItem_OnClick(), DispatcherPriority.Background);
    }

    public void RedoMenuItem_OnClick()
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(() => view.RedoMenuItem_OnClick(), DispatcherPriority.Background);
    }

    public void SetShowCursor(bool value)
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(() => view.SetShowCursor(value), DispatcherPriority.Background);
    }

    public void SetShowCursorDuringPlayback(bool value)
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(() => view.SetShowCursorDuringPlayback(value), DispatcherPriority.Background);
    }

    public void SetHighlightViewedNote(bool value)
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(() => view.SetHighlightViewedNote(value), DispatcherPriority.Background);
    }

    public void SetSelectLastInsertedNote(bool value)
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(() => view.SetSelectLastInsertedNote(value), DispatcherPriority.Background);
    }

    public void SetShowGimmicksInCircleView(bool value)
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(() => view.SetShowGimmicksInCircleView(value), DispatcherPriority.Background);
    }

    public void SetShowGimmicksDuringPlaybackInCircleView(bool value)
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(() => view.SetShowGimmicksDuringPlaybackInCircleView(value),
            DispatcherPriority.Background);
    }

    public void SetDarkMode(bool value)
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(() => view.SetDarkMode(value), DispatcherPriority.Background);
    }

    public void SetShowMeasureButtons(bool value)
    {
        var view = GetMainView();
        if (view == null)
            return;
        Dispatcher.UIThread.Post(() => view.SetShowMeasureButtons(value), DispatcherPriority.Background);
    }

    private MainView? GetMainView()
    {
        return this.FindControl<MainView>("View");
    }

    private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // we need this condition because Shutdown() calls Close()
        var view = GetMainView();
        if (view == null || view.CanShutdown)
            return;
        e.Cancel = true;
        Dispatcher.UIThread.Post(async () => await view.ExitMenuItem_OnClick(), DispatcherPriority.Background);
    }
}