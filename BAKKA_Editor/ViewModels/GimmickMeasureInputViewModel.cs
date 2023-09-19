using CommunityToolkit.Mvvm.ComponentModel;

namespace BAKKA_Editor.ViewModels;

public partial class GimmickMeasureInputViewModel : ObservableObject
{
    [ObservableProperty] private decimal beat1;
    [ObservableProperty] private decimal beat2 = 16;
    [ObservableProperty] private decimal measure;
}