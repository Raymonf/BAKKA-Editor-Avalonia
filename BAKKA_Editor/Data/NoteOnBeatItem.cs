using BAKKA_Editor.Enums;

namespace BAKKA_Editor.Data;

public class NoteOnBeatItem
{
    public string Type { get; private set; }
    public int Position { get; private set; }
    public int Size { get; private set; }
    
    public NoteOnBeatItem(string type, int position, int size)
    {
        Type = type;
        Position = position;
        Size = size;
    }
}