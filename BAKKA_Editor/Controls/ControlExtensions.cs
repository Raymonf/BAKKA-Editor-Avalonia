using Avalonia;
using Avalonia.Controls;

namespace BAKKA_Editor.Controls;

public static class ControlExtensions
{
    public static string? L(this object control, string key)
    {
        var resource = Application.Current?.FindResource(key);
        if (resource == null)
            return key;
        return resource as string;
    }
}