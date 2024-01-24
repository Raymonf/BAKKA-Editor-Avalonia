using System;
using BAKKA_Editor.Enums;
using System.Collections.Generic;
using BAKKA_Editor.Operations;

namespace BAKKA_Editor
{
    internal class BakeHold
    {
        public static void StepSymmetric(Chart chart, List<Note> multiSelectNotes, Note startNote, Note endNote, float length, int positionChange,
            int sizeChange, OperationManager operationManager)
        {
            decimal interval = (decimal) (1 / (1 / length * Math.Abs(positionChange)));
            int positionStep = (Math.Abs(positionChange) > Math.Abs(sizeChange) ? 2 : 1) * Math.Sign(positionChange);
            int sizeStep = (Math.Abs(positionChange) > Math.Abs(sizeChange) ? 1 : 2) * Math.Sign(sizeChange);

            int newPosition = startNote.Position;
            int newSize = startNote.Size;

            var lastNote = startNote;
            List<Note> segmentList = new List<Note>();

            bool select = multiSelectNotes.Contains(startNote);

            lock (chart)
            {
                for (decimal i = (decimal) startNote.Measure + interval; i < (decimal) endNote.Measure; i += interval)
                {
                    // avoid decimal/floating point errors that would
                    // otherwise cause two segments on the same beat
                    // if i is just *barely* less than endNote.Measure.
                    var info0 = new BeatInfo((float) i);
                    var info1 = new BeatInfo(endNote.Measure);
                    if (info0.Measure == info1.Measure && info0.Beat == info1.Beat) break;

                    newPosition += positionStep;
                    newSize += sizeStep;

                    var newNote = new Note()
                    {
                        BeatInfo = new((float) i),
                        NoteType = NoteType.HoldJoint,
                        Position = (newPosition + 60) % 60,
                        Size = newSize,
                        HoldChange = true,
                        PrevReferencedNote = lastNote,
                        NextReferencedNote = endNote
                    };

                    lastNote.NextReferencedNote = newNote;
                    endNote.PrevReferencedNote = newNote;

                    // this may be pure brainfuck, let me explain.
                    // give new note reference to last placed note. will be startNote if the loop just started.
                    // give new note reference to end note.
                    // give last note reference to new note. this will be overwritten by every iteration to always keep the connections correct.
                    // give end note reference to new note. same as above ^

                    lastNote = newNote;
                    segmentList.Add(newNote);

                    chart.Notes.Add(newNote);
                    if (select) multiSelectNotes.Add(newNote);
                    chart.IsSaved = false;
                }
            }

            operationManager.Push(new BakeHoldNote(chart, multiSelectNotes, startNote, endNote, segmentList));
        }

        public static void StepAsymmetric(Chart chart, List<Note> multiSelectNotes, Note startNote, Note endNote, float length, int positionChange,
            int sizeChange, OperationManager operationManager)
        {
            int ratio = sizeChange != 0 && positionChange != 0 ? int.Abs(positionChange / sizeChange) : 1;

            decimal interval = (decimal) (1 / (1 / length * Math.Max(1 / ratio * Math.Abs(positionChange), Math.Abs(sizeChange))));

            int positionStep = ratio * int.Sign(positionChange);
            int sizeStep = int.Sign(sizeChange);

            int newPosition = startNote.Position;
            int newSize = startNote.Size;

            var lastNote = startNote;
            List<Note> segmentList = new List<Note>();

            bool select = multiSelectNotes.Contains(startNote);

            lock (chart)
            {
                for (decimal i = (decimal) startNote.Measure + interval; i < (decimal) endNote.Measure; i += interval)
                {
                    var info0 = new BeatInfo((float) i);
                    var info1 = new BeatInfo(endNote.Measure);
                    if (info0.Measure == info1.Measure && info0.Beat == info1.Beat) break;

                    newPosition += positionStep;
                    newSize += sizeStep;

                    var newNote = new Note()
                    {
                        BeatInfo = new((float) i),
                        NoteType = NoteType.HoldJoint,
                        Position = (newPosition + 60) % 60,
                        Size = newSize,
                        HoldChange = true,
                        PrevReferencedNote = lastNote,
                        NextReferencedNote = endNote
                    };

                    lastNote.NextReferencedNote = newNote;
                    endNote.PrevReferencedNote = newNote;

                    lastNote = newNote;
                    segmentList.Add(newNote);

                    chart.Notes.Add(newNote);
                    if (select) multiSelectNotes.Add(newNote);
                    chart.IsSaved = false;
                }
            }

            operationManager.Push(new BakeHoldNote(chart, multiSelectNotes, startNote, endNote, segmentList));
        }

        public static void LerpRound(Chart chart, List<Note> multiSelectNotes, Note startNote, Note endNote, OperationManager operationManager)
        {
            decimal interval = 0.015625m;

            int newPosition = startNote.Position;
            int newSize = startNote.Size;

            var startPos = startNote.Position;
            var endPos = endNote.Position;
            var startSize = startNote.Size;
            var endSize = endNote.Size;

            var lastNote = startNote;
            List<Note> segmentList = new List<Note>();

            bool select = multiSelectNotes.Contains(startNote);
            bool shortPos = (int.Abs(endPos - startPos) > 30);

            lock (chart)
            {
                for (decimal i = (decimal) startNote.Measure + interval; i < (decimal) endNote.Measure; i += interval)
                {
                    var info0 = new BeatInfo((float) i);
                    var info1 = new BeatInfo(endNote.Measure);
                    if (info0.Measure == info1.Measure && info0.Beat == info1.Beat) break;

                    float lerpTime = ((float) i - startNote.Measure) / (endNote.Measure - startNote.Measure);

                    newPosition = (int)MathF.Round(ShortLerp(shortPos, startPos, endPos, lerpTime));
                    newSize = (int)MathF.Round(ShortLerp(false, startSize, endSize, lerpTime));

                    var newNote = new Note()
                    {
                        BeatInfo = new((float) i),
                        NoteType = NoteType.HoldJoint,
                        Position = (newPosition + 60) % 60,
                        Size = (newSize + 60) % 60,
                        HoldChange = false,
                        PrevReferencedNote = lastNote,
                        NextReferencedNote = endNote
                    };

                    lastNote.NextReferencedNote = newNote;
                    endNote.PrevReferencedNote = newNote;

                    lastNote = newNote;
                    segmentList.Add(newNote);

                    chart.Notes.Add(newNote);
                    if (select) multiSelectNotes.Add(newNote);
                    chart.IsSaved = false;
                }
            }

            operationManager.Push(new BakeHoldNote(chart, multiSelectNotes, startNote, endNote, segmentList));
        }

        private static float ShortLerp(bool shortPath, int a, int b, float t)
        {
            if (shortPath)
            {
                if (a > b)
                    a -= 60;
                else
                    b -= 60;
            }

            return (1 - t) * a + t * b;
        }
    }
}