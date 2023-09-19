using ManagedBass;

namespace BAKKA_Editor.SoundEngines;

public class BassBakkaSample : IBakkaSample
{
    private const int HitsoundChannelCount = 1;

    private int channelIndex;
    private readonly IBakkaSampleChannel[] channels = new IBakkaSampleChannel[HitsoundChannelCount];

    private int sample;

    public BassBakkaSample(string path)
    {
        sample = Bass.SampleLoad(path, 0, 0, HitsoundChannelCount, BassFlags.Default);
        if (!Loaded)
            return;
        for (var i = 0; i < HitsoundChannelCount; i++)
            channels[i] = new BassBakkaSampleChannel(Bass.SampleGetChannel(sample));
    }

    public bool Loaded => sample != 0;

    public IBakkaSampleChannel? GetChannel()
    {
        if (!Loaded)
            return null;
        var idx = ++channelIndex % HitsoundChannelCount;
        return channels[idx];
    }

    public void Free()
    {
        if (!Loaded)
            return;
        foreach (var channel in channels) channel.Reset();
        Bass.SampleFree(sample);
        sample = 0;
    }
}