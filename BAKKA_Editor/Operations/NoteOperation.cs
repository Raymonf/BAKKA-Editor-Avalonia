using BAKKA_Editor.Enums;

namespace BAKKA_Editor.Operations;

internal abstract class NoteOperation : IOperation
{
    public NoteOperation(Chart chart, Note item)
    {
        Chart = chart;
        Note = item;

        // Force End of Chart note to be the correct position and size 
        if (Note.NoteType == NoteType.EndOfChart)
        {
            Note.Position = 0;
            Note.Size = 60;
        }
    }

    public Note Note { get; }
    protected Chart Chart { get; }
    public abstract string Description { get; }

    public abstract void Redo();

    public abstract void Undo();
}

internal class InsertNote : NoteOperation
{
    public InsertNote(Chart chart, Note item) : base(chart, item)
    {
    }

    public override string Description => "Insert note";

    public override void Redo()
    {
        lock (Chart)
            Chart.Notes.Add(Note);
    }

    public override void Undo()
    {
        lock (Chart)
            Chart.Notes.Remove(Note);
    }
}

internal class RemoveNote : NoteOperation
{
    public RemoveNote(Chart chart, Note item) : base(chart, item)
    {
    }

    public override string Description => "Remove note";

    public override void Redo()
    {
        lock (Chart)
            Chart.Notes.Remove(Note);
    }

    public override void Undo()
    {
        lock (Chart)
            Chart.Notes.Add(Note);
    }
}

internal class EditNote : IOperation
{
    public EditNote(Note baseNote, Note newNote)
    {
        Base = baseNote;
        OldNote = new Note(baseNote);
        NewNote = new Note(newNote);
    }

    protected Note Base { get; }
    protected Note OldNote { get; }
    protected Note NewNote { get; }
    public string Description => "Edit note";

    public void Redo()
    {
        Base.BeatInfo = new BeatInfo(NewNote.BeatInfo);
        Base.Position = NewNote.Position;
        Base.Size = NewNote.Size;
    }

    public void Undo()
    {
        Base.BeatInfo = new BeatInfo(OldNote.BeatInfo);
        Base.Position = OldNote.Position;
        Base.Size = OldNote.Size;
    }
}

internal class MirrorNote : IOperation
{
    public MirrorNote(Note baseNote, Note newNote)
    {
        Base = baseNote;
        OldNote = new Note(baseNote);
        NewNote = new Note(newNote);
    }

    protected Note Base { get; }
    protected Note OldNote { get; }
    protected Note NewNote { get; }
    public string Description => "Edit note";

    public void Redo()
    {
        Base.BeatInfo = new BeatInfo(NewNote.BeatInfo);
        Base.Position = NewNote.Position;
        Base.Size = NewNote.Size;
        Base.NoteType = NewNote.NoteType;
    }

    public void Undo()
    {
        Base.BeatInfo = new BeatInfo(OldNote.BeatInfo);
        Base.Position = OldNote.Position;
        Base.Size = OldNote.Size;
        Base.NoteType = OldNote.NoteType;
    }
}

internal class InsertHoldNote : NoteOperation
{
    private Note prevNote;

    public InsertHoldNote(Chart chart, Note item) : base(chart, item)
    {
        prevNote = item.PrevReferencedNote;
    }

    public override string Description => "Insert hold note";

    public override void Redo()
    {
        if (Note.PrevReferencedNote != null)
            Note.PrevReferencedNote.NextReferencedNote = Note;

        lock (Chart)
            Chart.Notes.Add(Note);
    }

    public override void Undo()
    {
        if (Note.PrevReferencedNote != null)
            Note.PrevReferencedNote.NextReferencedNote = null;

        lock (Chart)
            Chart.Notes.Remove(Note);
    }
}

internal class RemoveHoldNote : NoteOperation
{
    private readonly Note nextNote;
    private readonly NoteType nextNoteType;
    private readonly Note prevNote;
    private readonly NoteType prevNoteType;

    public RemoveHoldNote(Chart chart, Note item) : base(chart, item)
    {
        prevNote = item.PrevReferencedNote;
        if (prevNote != null)
            prevNoteType = prevNote.NoteType;
        nextNote = item.NextReferencedNote;
        if (nextNote != null)
            nextNoteType = nextNote.NoteType;
    }

    public override string Description => "Remove hold note";

    public override void Redo()
    {
        switch (Note.NoteType)
        {
            case NoteType.HoldStartNoBonus:
            case NoteType.HoldStartBonusFlair:
                if (nextNote != null)
                {
                    nextNote.PrevReferencedNote = null;
                    if (nextNote.NoteType == NoteType.HoldJoint)
                        nextNote.NoteType = Note.NoteType;
                }

                break;
            case NoteType.HoldJoint:
                prevNote.NextReferencedNote = nextNote;
                if (nextNote != null)
                    nextNote.PrevReferencedNote = prevNote;
                break;
            case NoteType.HoldEnd:
                if (prevNote != null)
                {
                    prevNote.NextReferencedNote = null;
                    prevNote.NoteType = NoteType.HoldEnd;
                }

                break;
        }
        
        lock (Chart)
            Chart.Notes.Remove(Note);
    }

    public override void Undo()
    {
        switch (Note.NoteType)
        {
            case NoteType.HoldStartNoBonus:
            case NoteType.HoldStartBonusFlair:
                if (nextNote != null)
                {
                    nextNote.PrevReferencedNote = Note;
                    nextNote.NoteType = nextNoteType;
                }

                break;
            case NoteType.HoldJoint:
                prevNote.NextReferencedNote = Note;
                if (nextNote != null)
                    nextNote.PrevReferencedNote = Note;
                break;
            case NoteType.HoldEnd:
                if (prevNote != null)
                {
                    prevNote.NextReferencedNote = Note;
                    prevNote.NoteType = prevNoteType;
                }

                break;
        }


        lock (Chart)
            Chart.Notes.Add(Note);
    }
}