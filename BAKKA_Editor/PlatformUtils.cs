using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;

namespace BAKKA_Editor;

class PlatformUtils
{
    private static OperatingSystemType? _osType;
    public static OperatingSystemType OsType
    {
        get
        {
            if (_osType == null)
                _osType = AvaloniaLocator.Current.GetService<IRuntimePlatform>()?.GetRuntimeInfo().OperatingSystem;
            if (_osType == null)
                return OperatingSystemType.Unknown;
            return _osType.Value;
        }
    }

    public static string GetTempPath()
    {
        if (OsType == OperatingSystemType.iOS)
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
        return OsType == OperatingSystemType.iOS ? Path.Combine(GetTempPath(), Path.GetFileName(file)) : file;
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
