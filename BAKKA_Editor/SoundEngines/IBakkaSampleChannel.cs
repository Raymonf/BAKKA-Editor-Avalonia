namespace BAKKA_Editor.SoundEngines;

public interface IBakkaSampleChannel
{
    public void Play(bool restart = false);
    public void SetVolume(float volume);
    public void Reset();
    public bool Loaded { get; }
}