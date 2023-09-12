namespace BAKKA_Editor.SoundEngines;

public interface IBakkaSample
{
    public bool Loaded { get; }
    public IBakkaSampleChannel? GetChannel();
}