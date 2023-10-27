using System;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using BAKKA_Editor.ViewModels;

namespace BAKKA_Editor;

// TODO: this NEEDS to change
public class Localizer
{
    private const string DefaultLanguage = "en-US"; // TODO

    private string GetLanguageAssetPath(string languageCode)
    {
        return $"avares://BAKKA_Editor/Assets/locales/{languageCode}.axaml";
    }

    public void SetLanguage(string language)
    {
        var translations = Application.Current?.Resources.MergedDictionaries.OfType<ResourceInclude>()
            .FirstOrDefault(x => x.Source?.OriginalString?.Contains("/locales/") ?? false);
        if (translations != null)
            Application.Current?.Resources.MergedDictionaries.Remove(translations);

        Application.Current?.Resources.MergedDictionaries.Add(
            new ResourceInclude(new Uri(GetLanguageAssetPath(DefaultLanguage)))
            {
                Source = new Uri(GetLanguageAssetPath(DefaultLanguage))
            });

        if (language != DefaultLanguage)
        {
            Application.Current?.Resources.MergedDictionaries.Add(
                new ResourceInclude(new Uri(GetLanguageAssetPath(language)))
                {
                    Source = new Uri(GetLanguageAssetPath(language))
                });
        }
    }
}