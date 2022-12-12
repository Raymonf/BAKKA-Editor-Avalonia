using CommunityToolkit.Mvvm.ComponentModel;

namespace BAKKA_Editor.ViewModels;

public partial class GimmickMeasureInputViewModel : ObservableObject
{
    [ObservableProperty] private decimal measure = 0;
    [ObservableProperty] private decimal beat1 = 0;
    [ObservableProperty] private decimal beat2 = 16;
}