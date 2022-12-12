namespace BAKKA_Editor.SoundEngines;

public class DummyBakkaSound : IBakkaSound
{
    public float Volume { get; set; } = 1.0f;
    public bool Paused { get; set; } = true;
    public float PlaybackSpeed { get; set; } = 1.0f;
    public uint PlayPosition { get; set; } = 0;
    public uint PlayLength { get; set; } = uint.MaxValue;
}