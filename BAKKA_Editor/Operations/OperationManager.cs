using System;
using System.Collections.Generic;
using System.Linq;

namespace BAKKA_Editor.Operations;

internal class OperationManager
{
    protected Stack<IOperation> UndoStack { get; } = new();
    protected Stack<IOperation> RedoStack { get; } = new();

    private IOperation? LastCommittedOperation { get; set; }

    public IEnumerable<string> UndoOperationsDescription
    {
        get { return UndoStack.Select(p => p.Description); }
    }

    public IEnumerable<string> RedoOperationsDescription
    {
        get { return RedoStack.Select(p => p.Description); }
    }

    public bool CanUndo => UndoStack.Count > 0;

    public bool CanRedo => RedoStack.Count > 0;
    public event EventHandler? OperationHistoryChanged;
    public event EventHandler? ChangesCommitted;

    public void Push(IOperation op)
    {
        UndoStack.Push(op);
        RedoStack.Clear();
        OperationHistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void InvokeAndPush(IOperation op)
    {
        op.Redo();
        Push(op);
    }

    public IOperation Undo()
    {
        var op = UndoStack.Pop();
        op.Undo();
        var type = op.GetType();
        RedoStack.Push(op);
        OperationHistoryChanged?.Invoke(this, EventArgs.Empty);
        return op;
    }

    public IOperation Redo()
    {
        var op = RedoStack.Pop();
        op.Redo();
        UndoStack.Push(op);
        OperationHistoryChanged?.Invoke(this, EventArgs.Empty);
        return op;
    }

    public void Clear()
    {
        UndoStack.Clear();
        RedoStack.Clear();
        LastCommittedOperation = null;
        OperationHistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}