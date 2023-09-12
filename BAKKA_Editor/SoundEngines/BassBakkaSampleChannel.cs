using ManagedBass;

namespace BAKKA_Editor.SoundEngines;

public class BassBakkaSampleChannel : IBakkaSampleChannel
{
    private int bassChannel = 0;

    internal BassBakkaSampleChannel(int channel)
    {
        bassChannel = channel;
    }

    public bool Loaded => bassChannel != 0;

    public void SetVolume(float volume)
    {
        if (!Loaded)
            return;
        Bass.ChannelSetAttribute(bassChannel, ChannelAttribute.Volume, volume);
    }

    public void Play(bool restart = false)
    {
        if (!Loaded)
            return;
        Bass.ChannelPlay(bassChannel, restart);
    }

    public void Reset()
    {
        bassChannel = 0;
    }
}