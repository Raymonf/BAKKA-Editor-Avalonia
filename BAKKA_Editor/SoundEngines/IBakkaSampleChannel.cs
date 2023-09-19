namespace BAKKA_Editor.SoundEngines;

public interface IBakkaSampleChannel
{
    public bool Loaded { get; }
    public void Play(bool restart = false);
    public void SetVolume(float volume);
    public void Reset();
}