using System.Threading.Tasks;

namespace BAKKA_Editor.Views;

public interface IPassSetting
{
    public Task OpenChartSettings_OnClick();
    public Task NewMenuItem_OnClick();
    public Task OpenMenuItem_OnClick();
    public Task SaveMenuItem_OnClick();
    public Task SaveAsMenuItem_OnClick();
    public Task ExitMenuItem_OnClick();
    public void UndoMenuItem_OnClick();
    public void RedoMenuItem_OnClick();
    public void SetShowCursor(bool value);
    public void SetShowCursorDuringPlayback(bool value);
    public void SetHighlightViewedNote(bool value);
    public void SetSelectLastInsertedNote(bool value);
    public void SetShowGimmicksInCircleView(bool value);
    public void SetShowGimmicksDuringPlaybackInCircleView(bool value);
}