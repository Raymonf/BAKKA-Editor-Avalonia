using System.Collections.Generic;

namespace BAKKA_Editor.Operations;

internal class CompositeOperation : IOperation
{
    public CompositeOperation(string description, IEnumerable<IOperation> operations)
    {
        Description = description;
        Operations = operations;
    }

    protected IEnumerable<IOperation> Operations { get; }
    public string Description { get; }

    public void Redo()
    {
        foreach (var op in Operations) op.Redo();
    }

    public void Undo()
    {
        foreach (var op in Operations) op.Undo();
    }
}