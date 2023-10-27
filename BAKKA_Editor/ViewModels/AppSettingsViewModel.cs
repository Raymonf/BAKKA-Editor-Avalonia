using System.Collections.Generic;
using System.Linq;
using BAKKA_Editor.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;

namespace BAKKA_Editor.ViewModels;

public partial class AppSettingsViewModel : ViewModelBase
{
    private UserSettings? UserSettings { get; }
    private Localizer? Localizer { get; }
    public ContentDialog? Dialog { get; set; }

    private Dictionary<string, string> SupportedLanguages { get; } = new()
    {
        {"en-US", "English"},
        {"zh-Hant", "繁體中文"}
    };

    public AppSettingsViewModel()
    {
    }

    public AppSettingsViewModel(UserSettings userSettings)
    {
        UserSettings = userSettings;
        Localizer = new Localizer();
        selectedLanguage = SupportedLanguages.First();
        SetLanguage(selectedLanguage.Key);
        Localizer.SetLanguage(selectedLanguage.Key); // initial call
    }

    [ObservableProperty] private bool showBeatVisualSettings = true;

    partial void OnShowBeatVisualSettingsChanged(bool show)
    {
        if (UserSettings != null)
        {
            UserSettings.ViewSettings.ShowBeatVisualSettings = show;
        }
    }

    [ObservableProperty] private KeyValuePair<string, string> selectedLanguage;

    partial void OnSelectedLanguageChanged(KeyValuePair<string, string> kv)
    {
        Localizer?.SetLanguage(kv.Key);
        if (UserSettings != null)
            UserSettings.ViewSettings.Language = kv.Key;

        if (Dialog != null)
        {
            Dialog.Title = this.L("L.Settings.SettingsHeader");
            Dialog.CloseButtonText = this.L("L.Generic.CloseButtonText");
        }
    }

    public bool SetLanguage(string language)
    {
        if (!SupportedLanguages.ContainsKey(language))
        {
            return false;
        }

        SelectedLanguage = SupportedLanguages.First(x => x.Key == language);
        return true;
    }
}