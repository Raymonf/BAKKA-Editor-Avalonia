using Avalonia;
using Avalonia.Controls;

namespace BAKKA_Editor.Controls;

public static class ControlExtensions
{
    /// <summary>
    /// Get localized string from application resources
    /// </summary>
    /// <param name="control">The current object</param>
    /// <param name="key">The key to pull from resources</param>
    /// <returns>A string or null</returns>
    public static string? L(this object control, string key)
    {
        var resource = Application.Current?.FindResource(key);
        if (resource == null)
            return key;
        return resource as string;
    }
}