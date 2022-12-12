using System;

namespace BAKKA_Editor.SoundEngines;

public interface IBakkaSound
{
    public float Volume { get; set; }
    public bool Paused { get; set; }
    public float PlaybackSpeed { get; set; }
    public uint PlayPosition { get; set; }
    public uint PlayLength { get; }
}