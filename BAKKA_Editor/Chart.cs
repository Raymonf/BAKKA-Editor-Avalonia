using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BAKKA_Editor.Enums;

namespace BAKKA_Editor;

internal class Chart
{
    private static readonly CultureInfo _defaultParsingCulture = CultureInfo.InvariantCulture;

    public Chart()
    {
        Notes = new List<Note>();
        Gimmicks = new List<Gimmick>();
        Offset = 0;
        MovieOffset = 0;
        SongFileName = "";
        IsSaved = true;
    }

    public List<Note> Notes { get; set; }
    public List<Gimmick> Gimmicks { get; set; }

    /// <summary>
    ///     Offset in seconds.
    /// </summary>
    public double Offset { get; set; }

    /// <summary>
    ///     Movie offset in seconds
    /// </summary>
    public double MovieOffset { get; set; }

    private string SongFileName { get; set; }
    public string? EditorSongFileName { get; set; }
    private List<Gimmick>? TimeEvents { get; set; }

    public bool HasInitEvents
    {
        get
        {
            return TimeEvents != null &&
                   TimeEvents.Count > 0 &&
                   Gimmicks.Any(x => x.Measure == 0 && x.GimmickType == GimmickType.BpmChange) &&
                   Gimmicks.Any(x => x.Measure == 0 && x.GimmickType == GimmickType.TimeSignatureChange);
        }
    }

    public bool IsSaved { get; set; }

    public bool ParseFile(Stream? stream)
    {
        if (stream == null)
            return false;

        var file = PlatformUtils.ReadAllStreamLines(stream);

        if (file.Count < 1) return false;

        var index = 0;

        do
        {
            var line = file[index];

            var path = Utils.GetTag(line, "#MUSIC_FILE_PATH ");
            if (path != null)
                SongFileName = path;

            var editorSongName = Utils.GetTag(line, "#X_BAKKA_MUSIC_FILENAME ");
            if (editorSongName != null)
                EditorSongFileName = editorSongName;

            var offset = Utils.GetTag(line, "#OFFSET");
            if (offset != null)
                Offset = Convert.ToDouble(offset, _defaultParsingCulture);

            offset = Utils.GetTag(line, "#MOVIEOFFSET");
            if (offset != null)
                MovieOffset = Convert.ToDouble(offset, _defaultParsingCulture);

            if (line.Contains("#BODY"))
            {
                index++;
                break;
            }
        } while (++index < file.Count);

        int lineNum;
        Gimmick gimmickTemp;
        Note noteTemp;
        Dictionary<int, Note> notesByLine = new();
        Dictionary<int, int> refByLine = new();
        for (var i = index; i < file.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(file[i]))
                continue;

            var parsed = file[i].Split(new[] {" "},
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            NoteBase temp = new();
            temp.BeatInfo = new BeatInfo(Convert.ToInt32(parsed[0], _defaultParsingCulture),
                Convert.ToInt32(parsed[1], _defaultParsingCulture));
            temp.GimmickType = (GimmickType) Convert.ToInt32(parsed[2], _defaultParsingCulture);

            switch (temp.GimmickType)
            {
                case GimmickType.NoGimmick:
                    noteTemp = new Note(temp.BeatInfo);
                    noteTemp.NoteType = (NoteType) Convert.ToInt32(parsed[3], _defaultParsingCulture);
                    lineNum = Convert.ToInt32(parsed[4], _defaultParsingCulture);
                    noteTemp.Position = Convert.ToInt32(parsed[5], _defaultParsingCulture);
                    noteTemp.Size = Convert.ToInt32(parsed[6], _defaultParsingCulture);
                    noteTemp.HoldChange = Convert.ToBoolean(Convert.ToInt32(parsed[7], _defaultParsingCulture));
                    if (noteTemp.NoteType == NoteType.MaskAdd || noteTemp.NoteType == NoteType.MaskRemove)
                        noteTemp.MaskFill = (MaskType) Convert.ToInt32(parsed[8], _defaultParsingCulture);
                    else if (noteTemp.NoteType == NoteType.HoldStartNoBonus ||
                             noteTemp.NoteType == NoteType.HoldJoint ||
                             noteTemp.NoteType == NoteType.HoldStartBonusFlair)
                        refByLine[lineNum] = Convert.ToInt32(parsed[8], _defaultParsingCulture);
                    Notes.Add(noteTemp);
                    notesByLine[lineNum] = Notes.Last();
                    break;
                case GimmickType.BpmChange:
                    gimmickTemp = new Gimmick(temp.BeatInfo, temp.GimmickType);
                    gimmickTemp.BPM = Convert.ToDouble(parsed[3], _defaultParsingCulture);
                    Gimmicks.Add(gimmickTemp);
                    break;
                case GimmickType.TimeSignatureChange:
                    gimmickTemp = new Gimmick(temp.BeatInfo, temp.GimmickType);
                    gimmickTemp.TimeSig = new TimeSignature
                    {
                        Upper = Convert.ToInt32(parsed[3], _defaultParsingCulture),
                        Lower = parsed.Length == 5 ? Convert.ToInt32(parsed[4], _defaultParsingCulture) : 4
                    };
                    Gimmicks.Add(gimmickTemp);
                    break;
                case GimmickType.HiSpeedChange:
                    gimmickTemp = new Gimmick(temp.BeatInfo, temp.GimmickType);
                    gimmickTemp.HiSpeed = Convert.ToDouble(parsed[3], _defaultParsingCulture);
                    Gimmicks.Add(gimmickTemp);
                    break;
                case GimmickType.ReverseStart:
                case GimmickType.ReverseMiddle:
                case GimmickType.ReverseEnd:
                case GimmickType.StopStart:
                case GimmickType.StopEnd:
                default:
                    Gimmicks.Add(new Gimmick(temp.BeatInfo, temp.GimmickType));
                    break;
            }
        }

        // Generate hold references
        var errors = new List<string>();

        for (var i = 0; i < Notes.Count; i++)
        {
            if (refByLine.ContainsKey(i))
            {
                if (!notesByLine.ContainsKey(refByLine[i]))
                {
                    errors.Add($"Broken note found: referenced note index {refByLine[i]} (at index {i}) was not found in notesByLine (max = {notesByLine.Count - 1})");
                    continue;
                }

                Notes[i].NextReferencedNote = notesByLine[refByLine[i]];
                Notes[i].NextReferencedNote.PrevReferencedNote = Notes[i];
            }
        }

        // error if we have any broken notes
        if (errors.Count > 0)
            throw new Exception(string.Join("\n", errors));

        RecalcTime();

        IsSaved = true;
        return true;
    }

    public bool WriteFile(string path, bool setSave = true)
    {
        return WriteFile(File.OpenWrite(path), setSave);
    }

    public bool WriteFile(Stream stream, bool setSave = true)
    {
        stream.SetLength(0);
        using (var sw = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            // LF line ending
            sw.NewLine = "\n";

            sw.WriteLine("#MUSIC_SCORE_ID 0");
            sw.WriteLine("#MUSIC_SCORE_VERSION 0");
            sw.WriteLine("#GAME_VERSION ");
            sw.WriteLine($"#MUSIC_FILE_PATH {SongFileName}");
            if (EditorSongFileName != null)
                sw.WriteLine($"#X_BAKKA_MUSIC_FILENAME {EditorSongFileName}");
            sw.WriteLine($"#OFFSET {Offset:F6}");
            sw.WriteLine($"#MOVIEOFFSET {MovieOffset:F6}");
            sw.WriteLine("#BODY");

            foreach (var gimmick in Gimmicks)
            {
                sw.Write(
                    $"{gimmick.BeatInfo.Measure,4:F0}{gimmick.BeatInfo.Beat,5:F0}{(int) gimmick.GimmickType,5:F0}");
                switch (gimmick.GimmickType)
                {
                    case GimmickType.BpmChange:
                        sw.WriteLine($" {gimmick.BPM:F6}");
                        break;
                    case GimmickType.TimeSignatureChange:
                        sw.WriteLine($"{gimmick.TimeSig.Upper,5:F0}{gimmick.TimeSig.Lower,5:F0}");
                        break;
                    case GimmickType.HiSpeedChange:
                        sw.WriteLine($" {gimmick.HiSpeed:F6}");
                        break;
                    default:
                        sw.WriteLine("");
                        break;
                }
            }

            foreach (var note in Notes)
            {
                sw.Write(
                    $"{note.BeatInfo.Measure,4:F0}{note.BeatInfo.Beat,5:F0}{(int) note.GimmickType,5:F0}{(int) note.NoteType,5:F0}");
                sw.Write(
                    $"{Notes.IndexOf(note),5:F0}{note.Position,5:F0}{note.Size,5:F0}{Convert.ToInt32(note.HoldChange, _defaultParsingCulture),5:F0}");
                if (note.IsMask)
                    sw.Write($"{(int) note.MaskFill,5:F0}");
                if (note.NextReferencedNote != null)
                    sw.Write($"{Notes.IndexOf(note.NextReferencedNote),5:F0}");
                sw.WriteLine("");
            }
        }

        IsSaved = setSave;
        return true;
    }

    public void RecalcTime()
    {
        Gimmicks = Gimmicks.OrderBy(x => x.Measure).ToList();
        var timeSig =
            Gimmicks.FirstOrDefault(x => x.GimmickType == GimmickType.TimeSignatureChange && x.Measure == 0.0f);
        var bpm = Gimmicks.FirstOrDefault(x => x.GimmickType == GimmickType.BpmChange && x.Measure == 0.0f);
        if (timeSig == null || bpm == null)
            return; // Cannot calculate times without either starting value

        TimeEvents = new List<Gimmick>();
        for (var i = 0; i < Gimmicks.Count; i++)
        {
            var evt = TimeEvents.FirstOrDefault(x => x.BeatInfo.MeasureDecimal == Gimmicks[i].BeatInfo.MeasureDecimal);

            if (Gimmicks[i].GimmickType == GimmickType.BpmChange)
            {
                if (evt == null)
                    TimeEvents.Add(new Gimmick
                    {
                        BeatInfo = new BeatInfo(Gimmicks[i].BeatInfo),
                        BPM = Gimmicks[i].BPM,
                        TimeSig = new TimeSignature(timeSig.TimeSig)
                    });
                else
                    evt.BPM = Gimmicks[i].BPM;
                bpm = Gimmicks[i];
            }

            if (Gimmicks[i].GimmickType == GimmickType.TimeSignatureChange)
            {
                if (evt == null)
                    TimeEvents.Add(new Gimmick
                    {
                        BeatInfo = new BeatInfo(Gimmicks[i].BeatInfo),
                        BPM = bpm.BPM,
                        TimeSig = new TimeSignature(Gimmicks[i].TimeSig)
                    });
                else
                    evt.TimeSig = new TimeSignature(Gimmicks[i].TimeSig);
                timeSig = Gimmicks[i];
            }
        }

        // Run through all time events and generate valid start times
        TimeEvents[0].StartTime = Offset * 1000.0;
        for (var i = 1; i < TimeEvents.Count; i++)
            TimeEvents[i].StartTime =
                (TimeEvents[i].Measure - TimeEvents[i - 1].Measure) *
                (4.0f * TimeEvents[i - 1].TimeSig.Ratio * (60000.0 / TimeEvents[i - 1].BPM)) +
                TimeEvents[i - 1].StartTime;
    }
    /*
    ((60000.0 / evt.BPM) * 4.0 * evt.TimeSig.Ratio) * measure = time
    time / ((60000.0 / evt.BPM) * 4.0 * evt.TimeSig.Ratio) = measure
    */

    /// <summary>
    ///     Translate milliseconds to BeatInfo
    /// </summary>
    /// <param name="time">Timestamp in milliseconds</param>
    /// <returns></returns>
    public BeatInfo GetBeatInfoFromTime(double time)
    {
        if (TimeEvents == null || TimeEvents.Count == 0)
            return new BeatInfo(-1, 0);

        var evt = TimeEvents.LastOrDefault(x => time >= x.StartTime) ?? TimeEvents[0];
        return new BeatInfo((float) ((time - evt.StartTime) / (60000.0 / evt.BPM * 4.0f * evt.TimeSig.Ratio) +
                                     evt.Measure));
    }

    /// <summary>
    ///     Translate MeasureDecimal to beats
    /// </summary>
    /// <param name="time">Timestamp in milliseconds</param>
    /// <returns></returns>
    public float GetMeasureDecimalFromTime(double time)
    {
        if (TimeEvents == null || TimeEvents.Count == 0)
        {
            return BeatInfo.GetMeasureDecimal(-1, 0);
        }
        var evt = TimeEvents.LastOrDefault(x => time >= x.StartTime) ?? TimeEvents[0];
        return (float) ((time - evt.StartTime) / (60000.0 / evt.BPM * 4.0f * evt.TimeSig.Ratio) +
                        evt.Measure);
    }

    /// <summary>
    ///     Translate MeasureDecimals into milliseconds
    /// </summary>
    /// <param name="measureDecimal">Timestamp in MeasureDecimals</param>
    /// <returns></returns>
    public int GetTime(float measureDecimal)
    {
        if (TimeEvents == null || TimeEvents.Count == 0)
            return 0;

        var evt = TimeEvents.LastOrDefault(x => measureDecimal >= x.Measure);
        if (evt == null)
            evt = TimeEvents[0];
        return (int) (60000.0 / evt.BPM * 4.0f * evt.TimeSig.Ratio * (measureDecimal - evt.Measure) +
                      evt.StartTime);
    }

    /// <summary>
    ///     Translate BeatInfo into milliseconds
    /// </summary>
    /// <param name="beat">BeatInfo</param>
    /// <returns></returns>
    public int GetTime(BeatInfo beatInfo)
    {
        return GetTime(beatInfo.MeasureDecimal);
    }

    /// <summary>
    /// Returns MeasureDecimal scaled by all previous HiSpeed changes and Time Signatures.
    /// <br>Avoid using this in realtime, it's very expensive.</br>
    /// </summary>
    /// <param name="chart">Current Chart</param>
    /// <param name="measureDecimal">Position in MeasureDecimals</param>
    public static float GetScaledMeasurePosition(Chart chart, float measureDecimal)
    {
        float scaledPosition = measureDecimal;

        // scaleChange = HiSpeed or TimeSig change
        var previousScaleChanges = chart.Gimmicks.Where(x =>
            x.Measure < measureDecimal &&
            x.GimmickType is GimmickType.HiSpeedChange or GimmickType.TimeSignatureChange).ToList();

        if (previousScaleChanges.Count > 0)
        {
            for (var i = 0; i < previousScaleChanges.Count; i++)
            {
                var currentScaleChangePosition = previousScaleChanges[i].Measure;

                var timeSigChange = previousScaleChanges.LastOrDefault(x => x.Measure <= currentScaleChangePosition && x.GimmickType is GimmickType.TimeSignatureChange);
                var timeSigValue = timeSigChange != null ? (float)timeSigChange.TimeSig.Ratio : 1;

                var hiSpeedChange = previousScaleChanges.LastOrDefault(x => x.Measure <= currentScaleChangePosition && x.GimmickType is GimmickType.HiSpeedChange);
                var hiSpeedValue = hiSpeedChange != null ? (float)hiSpeedChange.HiSpeed : 1;

                // if it's not the last Scale change in the list
                if (i != previousScaleChanges.Count - 1)
                {
                    // get distance from current to next Scale change
                    var nextScaleChangePosition = (float)previousScaleChanges[i + 1].Measure;
                    var distance = nextScaleChangePosition - currentScaleChangePosition;

                    // scale distance and apply change to scaledPosition
                    scaledPosition += (distance * hiSpeedValue * timeSigValue) - distance;
                }

                // if it's the last Scale change in the list
                else
                {
                    // get distance from current Scale change to position that should be scaled
                    var distance = measureDecimal - currentScaleChangePosition;

                    // scale distance and apply change to scaledPosition
                    scaledPosition += (distance * hiSpeedValue * timeSigValue) - distance;
                }
            }
        }

        return scaledPosition;
    }
}