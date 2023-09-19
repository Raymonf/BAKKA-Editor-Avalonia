namespace BAKKA_Editor.Operations;

internal interface IOperation
{
    string Description { get; }

    void Undo();

    void Redo();
}