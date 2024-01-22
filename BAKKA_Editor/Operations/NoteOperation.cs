using BAKKA_Editor.Enums;
using System.Collections.Generic;
using System.Linq;

namespace BAKKA_Editor.Operations;

internal abstract class NoteOperation : IOperation
{
    public NoteOperation(Chart chart, List<Note> multiSelect, Note item)
    {
        Chart = chart;
        Note = item;
        MultiSelect = multiSelect;

        // Force End of Chart note to be the correct position and size 
        if (Note.NoteType == NoteType.EndOfChart)
        {
            Note.Position = 0;
            Note.Size = 60;
        }
    }

    public Note Note { get; }
    public List<Note> MultiSelect { get; }
    protected Chart Chart { get; }
    public abstract string Description { get; }

    public abstract void Redo();

    public abstract void Undo();
}

internal class InsertNote : NoteOperation
{
    public InsertNote(Chart chart, List<Note> multiSelect, Note item) : base(chart, multiSelect, item)
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
        {
            Chart.Notes.Remove(Note);
            MultiSelect.Remove(Note);
        }
    }
}

internal class RemoveNote : NoteOperation
{
    public RemoveNote(Chart chart, List<Note> multiSelect, Note item) : base(chart, multiSelect, item)
    {
    }

    public override string Description => "Remove note";

    public override void Redo()
    {
        lock (Chart)
        {
            Chart.Notes.Remove(Note);
            MultiSelect.Remove(Note);
        }
    }

    public override void Undo()
    {
        lock (Chart)
            Chart.Notes.Add(Note);
    }
}

internal class RemoveNoteBulk : IOperation
{
    public RemoveNoteBulk(Chart chart, List<Note> multiSelect, List<Note> notes)
    {
        Chart = chart;
        MultiSelect = multiSelect;
        Notes = notes;
    }

    public string Description => "Remove note [Bulk]";

    private Chart Chart;
    private List<Note> MultiSelect;
    private List<Note> Notes;

    public void Redo()
    {
        lock (Chart)
        {
            foreach (Note note in Notes)
            {
                Chart.Notes.Remove(note);
                MultiSelect.Remove(note);
            }
        }
    }

    public void Undo()
    {
        lock (Chart)
        {
            foreach (Note note in Notes)
            {
                Chart.Notes.Add(note);
            }
        }
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

internal class EditNoteBulk : IOperation
{
    // !!!! What the bingle
    // If you undo to the point where edited notes are deleted,
    // then redo back to the edit operation, the op loses reference
    // to all previously deleted notes. Help.
    public EditNoteBulk(List<Note> baseNotes, List<Note> newNotes)
    {
        BaseNotes = baseNotes;
        OldNotes = new();
        foreach (Note note in baseNotes)
            OldNotes.Add(new(note));

        NewNotes = new();
        foreach (Note note in newNotes)
            NewNotes.Add(new(note));
    }

    public List<Note> BaseNotes;
    public List<Note> OldNotes;
    public List<Note> NewNotes;

    public string Description => "Edit note [Bulk]";

    public void Redo()
    {
        for (int i = 0; i < BaseNotes.Count; i++)
        {
            BaseNotes[i].BeatInfo = new(NewNotes[i].BeatInfo);
            BaseNotes[i].Position = NewNotes[i].Position;
            BaseNotes[i].Size = NewNotes[i].Size;
        }
    }

    public void Undo()
    {
        for (int i = 0; i < BaseNotes.Count; i++)
        {
            BaseNotes[i].BeatInfo = new(OldNotes[i].BeatInfo);
            BaseNotes[i].Position = OldNotes[i].Position;
            BaseNotes[i].Size = OldNotes[i].Size;
        }
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
    public string Description => "Mirror note";

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

internal class MirrorNoteBulk : IOperation
{
    // !!!! What the bingle
    // This one is even worse. It's so broken. Help.
    public MirrorNoteBulk(List<Note> baseNotes, List<Note> newNotes)
    {
        BaseNotes = baseNotes;
        OldNotes = new();
        foreach (Note note in baseNotes)
            OldNotes.Add(new(note));

        NewNotes = new();
        foreach (Note note in newNotes)
            NewNotes.Add(new(note));
    }

    public List<Note> BaseNotes;
    public List<Note> OldNotes;
    public List<Note> NewNotes;

    public string Description => "Edit note [Bulk]";

    public void Redo()
    {
        for (int i = 0; i < BaseNotes.Count; i++)
        {
            BaseNotes[i].BeatInfo = new(NewNotes[i].BeatInfo);
            BaseNotes[i].Position = NewNotes[i].Position;
            BaseNotes[i].Size = NewNotes[i].Size;
            BaseNotes[i].NoteType = NewNotes[i].NoteType;
        }
    }

    public void Undo()
    {
        for (int i = 0; i < BaseNotes.Count; i++)
        {
            BaseNotes[i].BeatInfo = new(OldNotes[i].BeatInfo);
            BaseNotes[i].Position = OldNotes[i].Position;
            BaseNotes[i].Size = OldNotes[i].Size;
            BaseNotes[i].NoteType = OldNotes[i].NoteType;
        }
    }
}

internal class BakeHoldNote : IOperation
{
    private List<Note> Segments;
    private List<Note> MultiSelect;
    private Chart Chart;
    private Note Start;
    private Note End;

    public BakeHoldNote(Chart chart, List<Note> multiSelect, Note startNote, Note endNote, List<Note> segmentNotes)
    {
        Chart = chart;
        MultiSelect = multiSelect;
        Start = startNote;
        End = endNote;
        Segments = segmentNotes;
    }

    
    public string Description => "Bake Hold note";
    public void Redo()
    {
        lock (Chart)
        {
            foreach (Note note in Segments)
                Chart.Notes.Add(note);
        }


        Start.NextReferencedNote = Segments[0];
        Segments.Last().NextReferencedNote = End;
        End.PrevReferencedNote = Segments.Last();
    }

    public void Undo()
    {
        lock (Chart)
        {
            foreach (Note note in Segments)
            {
                Chart.Notes.Remove(note);
                MultiSelect.Remove(note);
            }
        }


        Start.NextReferencedNote = End;
        End.PrevReferencedNote = Start;
    }
}

internal class InsertHoldSegment : IOperation
{
    private Chart Chart;
    private List<Note> MultiSelect;
    private Note HighlightedNote;
    private Note NewNote;

    public InsertHoldSegment(Chart chart, List<Note> multiSelect, Note highlightedNote, Note newNote)
    {
        MultiSelect = multiSelect;
        Chart = chart;
        HighlightedNote = highlightedNote;
        NewNote = newNote;
    }

    public string Description => "Insert hold segment";

    public void Redo()
    {
        lock (Chart)
            Chart.Notes.Add(NewNote);

        HighlightedNote.PrevReferencedNote.NextReferencedNote = NewNote;
        HighlightedNote.PrevReferencedNote = NewNote;
    }

    public void Undo()
    {
        lock (Chart)
        {
            Chart.Notes.Remove(NewNote);
            MultiSelect.Remove(NewNote);
        }

        HighlightedNote.PrevReferencedNote = NewNote.PrevReferencedNote;
        NewNote.PrevReferencedNote.NextReferencedNote = HighlightedNote;
    }
}

internal class InsertHoldNote : NoteOperation
{
    private Note prevNote;

    public InsertHoldNote(Chart chart, List<Note> multiSelect, Note item) : base(chart, multiSelect, item)
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
        {
            Chart.Notes.Remove(Note);
            MultiSelect.Remove(Note);
        }
    }
}

internal class DeleteEntireHold : IOperation
{
    private List<Note> Notes;
    private Chart Chart;
    private List<Note> MultiSelect;

    public DeleteEntireHold(Chart chart, List<Note> multiSelect, List<Note> notes)
    {
        Notes = notes;
        MultiSelect = multiSelect;
        Chart = chart;
    }

    public string Description => "Delete an entire hold note";
    public void Redo()
    {
        lock (Chart)
        {
            foreach (Note note in Notes)
            {
                Chart.Notes.Remove(note);
                MultiSelect.Remove(note);
            }
        }
    }

    public void Undo()
    {
        lock (Chart)
        {
            foreach (Note note in Notes)
                Chart.Notes.Add(note);
        }
    }
}

internal class RemoveHoldNote : NoteOperation
{
    private readonly Note nextNote;
    private readonly NoteType nextNoteType;
    private readonly Note prevNote;
    private readonly NoteType prevNoteType;

    public RemoveHoldNote(Chart chart, List<Note> multiSelect, Note item) : base(chart, multiSelect, item)
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
        {
            Chart.Notes.Remove(Note);
            MultiSelect.Remove(Note);
        }
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