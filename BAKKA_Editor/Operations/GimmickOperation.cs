using BAKKA_Editor.Enums;

namespace BAKKA_Editor.Operations;

internal abstract class GimmickOperation : IOperation
{
    public GimmickOperation(Chart chart, Gimmick item)
    {
        Chart = chart;
        Gimmick = item;
    }

    protected Gimmick Gimmick { get; }
    protected Chart Chart { get; }
    public abstract string Description { get; }

    public abstract void Redo();

    public abstract void Undo();
}

internal class InsertGimmick : GimmickOperation
{
    public InsertGimmick(Chart chart, Gimmick item) : base(chart, item)
    {
    }

    public override string Description => "Insert gimmick";

    public override void Redo()
    {
        Chart.Gimmicks.Add(Gimmick);
        Chart.RecalcTime();
    }

    public override void Undo()
    {
        Chart.Gimmicks.Remove(Gimmick);
        Chart.RecalcTime();
    }
}

internal class RemoveGimmick : GimmickOperation
{
    public RemoveGimmick(Chart chart, Gimmick item) : base(chart, item)
    {
    }

    public override string Description => "Remove gimmick";

    public override void Redo()
    {
        Chart.Gimmicks.Remove(Gimmick);
        Chart.RecalcTime();
    }

    public override void Undo()
    {
        Chart.Gimmicks.Add(Gimmick);
        Chart.RecalcTime();
    }
}

internal class EditGimmick : IOperation
{
    public EditGimmick(Gimmick baseGimmick, Gimmick newGimmick, Chart chart)
    {
        Base = baseGimmick;
        OldGimmick = new Gimmick(baseGimmick);
        NewGimmick = new Gimmick(newGimmick);
        Chart = chart;
    }

    protected Gimmick Base { get; }
    protected Gimmick OldGimmick { get; }
    protected Gimmick NewGimmick { get; }
    protected Chart Chart { get; }
    public string Description => "Edit gimmick";

    public void Redo()
    {
        Base.BeatInfo = new BeatInfo(NewGimmick.BeatInfo);
        switch (Base.GimmickType)
        {
            case GimmickType.BpmChange:
                Base.BPM = NewGimmick.BPM;
                break;
            case GimmickType.TimeSignatureChange:
                Base.TimeSig = new TimeSignature(NewGimmick.TimeSig);
                break;
            case GimmickType.HiSpeedChange:
                Base.HiSpeed = NewGimmick.HiSpeed;
                break;
        }

        Chart.RecalcTime();
    }

    public void Undo()
    {
        Base.BeatInfo = new BeatInfo(OldGimmick.BeatInfo);
        switch (Base.GimmickType)
        {
            case GimmickType.BpmChange:
                Base.BPM = OldGimmick.BPM;
                break;
            case GimmickType.TimeSignatureChange:
                Base.TimeSig = new TimeSignature(OldGimmick.TimeSig);
                break;
            case GimmickType.HiSpeedChange:
                Base.HiSpeed = OldGimmick.HiSpeed;
                break;
        }

        Chart.RecalcTime();
    }
}