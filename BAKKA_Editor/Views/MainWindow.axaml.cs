using System;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using BAKKA_Editor.Operations;
using ReactiveUI;

namespace BAKKA_Editor.Views;

public partial class MainWindow : Window
{
    private MainView view;

    public ReactiveCommand<Unit, Unit> NewCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; set; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; set; }
    public ReactiveCommand<Unit, Unit> CutCommand { get; set; }
    public ReactiveCommand<Unit, Unit> CopyCommand { get; set; }
    public ReactiveCommand<Unit, Unit> PasteCommand { get; set; }
    public ReactiveCommand<Unit, Unit> BakeHoldCommand { get; set; }
    public ReactiveCommand<Unit, Unit> InsertHoldSegmentCommand { get; set; }
    public ReactiveCommand<Unit, Unit> SelectHighlightedNoteCommand { get; set; }
    public ReactiveCommand<Unit, Unit> DeselectNotesCommand { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        view = GetMainView()!;
        NewCommand = ReactiveCommand.Create(OnNewCommand);
        OpenCommand = ReactiveCommand.Create(OnOpenCommand);
        SaveCommand = ReactiveCommand.Create(OnSaveCommand);
        SaveAsCommand = ReactiveCommand.Create(OnSaveAsCommand);
        ExitCommand = ReactiveCommand.Create(ExitMenuItem_OnClick);
        UndoCommand = ReactiveCommand.Create(OnUndoCommand);
        RedoCommand = ReactiveCommand.Create(OnRedoCommand);
        CutCommand = ReactiveCommand.Create(OnCutCommand);
        CopyCommand = ReactiveCommand.Create(OnCopyCommand);
        PasteCommand = ReactiveCommand.Create(OnPasteCommand);
        BakeHoldCommand = ReactiveCommand.Create(OnBakeHoldCommand);
        InsertHoldSegmentCommand = ReactiveCommand.Create(OnInsertHoldSegmentCommand);
        SelectHighlightedNoteCommand = ReactiveCommand.Create(OnSelectHighlightedNoteCommand);
        DeselectNotesCommand = ReactiveCommand.Create(OnDeselectNotesCommand);
    }

    private MainView? GetMainView() => this.FindControl<MainView>("View");

    private void RunInView(Func<MainView?, Task> action)
    {
        Dispatcher.UIThread.Post(() => action(GetMainView()), DispatcherPriority.Background);
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

    private void OnNewCommand()
    {
        RunInView(view =>
        {
            view?.NewMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    private void OnOpenCommand()
    {
        RunInView(view =>
        {
            view?.OpenMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    private void OnSaveCommand()
    {
        RunInView(view =>
        {
            view?.SaveMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    private void OnSaveAsCommand()
    {
        RunInView(view =>
        {
            view?.SaveAsMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    private void ExitMenuItem_OnClick()
    {
        RunInView(view =>
        {
            view?.ExitMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    private void OnUndoCommand()
    {
        RunInView(view =>
        {
            view?.UndoMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    private void OnRedoCommand()
    {
        RunInView(view =>
        {
            view?.RedoMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    private void OnCutCommand()
    {
        RunInView(view =>
        {
            view?.CutMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    private void OnCopyCommand()
    {
        RunInView(view =>
        {
            view?.CopyMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    private void OnPasteCommand()
    {
        RunInView(view =>
        {
            view?.PasteMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    private void OnBakeHoldCommand()
    {
        RunInView(view =>
        {
            view?.BakeHoldMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    private void OnInsertHoldSegmentCommand()
    {
        RunInView(view =>
        {
            view?.InsertHoldSegmentMenuItem_OnClick();
            return Task.CompletedTask;
        });
    }

    private void OnSelectHighlightedNoteCommand()
    {
        RunInView(view =>
        {
            view?.OnSelectHighlightedNote_OnClick();
            return Task.CompletedTask;
        });
    }

    private void OnDeselectNotesCommand()
    {
        RunInView(view =>
        {
            view?.OnDeselectNotes_OnClick();
            return Task.CompletedTask;
        });
    }
}