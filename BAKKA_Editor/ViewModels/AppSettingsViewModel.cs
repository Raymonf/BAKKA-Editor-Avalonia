using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BAKKA_Editor.ViewModels;

public partial class AppSettingsViewModel : ViewModelBase
{

    private UserSettings? UserSettings { get; }
    private Localizer? Localizer { get; }

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

    [ObservableProperty] private KeyValuePair<string, string> selectedLanguage;

    partial void OnSelectedLanguageChanged(KeyValuePair<string, string> kv)
    {
        Localizer?.SetLanguage(kv.Key);
        if (UserSettings != null)
            UserSettings.ViewSettings.Language = kv.Key;
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