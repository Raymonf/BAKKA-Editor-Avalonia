using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;

namespace BAKKA_Editor;

class PlatformUtils
{
    private static FormFactorType? _formFactorType;
    public static FormFactorType FormFactorType
    {
        get
        {
            if (_formFactorType == null)
                _formFactorType = AvaloniaLocator.Current.GetService<IRuntimePlatform>()?.GetRuntimeInfo().FormFactor;
            if (_formFactorType == null)
                return FormFactorType.Unknown;
            return _formFactorType.Value;
        }
    }

    public static string GetTempPath()
    {
        if (FormFactorType == FormFactorType.Mobile)
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documents, "..", "tmp");
        }
        
        return Path.GetTempPath();
    }
    
    public static string GetTempFileName()
    {
        var file = Path.GetTempFileName();
        File.Delete(file);
        return FormFactorType == FormFactorType.Mobile ? Path.Combine(GetTempPath(), Path.GetFileName(file)) : file;
    }

    public static List<string> ReadAllStreamLines(Stream stream)
    {
        var lines = new List<string>();
        using var sr = new StreamReader(stream);
        while (!sr.EndOfStream)
            lines.Add(sr.ReadLine());
        return lines;
    }
}
