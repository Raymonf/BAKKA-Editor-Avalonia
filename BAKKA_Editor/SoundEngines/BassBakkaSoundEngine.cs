using System;
using System.IO;
using ManagedBass;
using ManagedBass.Flac;
using ManagedBass.Fx;

namespace BAKKA_Editor.SoundEngines;

public class BassBakkaSoundEngine : IBakkaSoundEngine
{
    private static bool BassInitialized;
    private byte[]? soundData;

    public BassBakkaSoundEngine()
    {
        if (!BassInitialized)
        {
            if (!Bass.Init(Flags: DeviceInitFlags.Latency)) throw new Exception("Couldn't initialize BASS");

            BassInitialized = true;
        }
    }

    // for iOS _ONLY_ :/
    public IBakkaSound Play2D(Stream soundStream, bool playLooped, bool startPaused)
    {
        // create decode channel
        soundData = new byte[soundStream.Length];
        if (soundStream.Read(soundData, 0, (int) soundStream.Length) != soundStream.Length)
            throw new Exception("not all bytes were read?");
        soundStream.Close();

        // can't get fx working on iOS??? 
        var bassChannel = Bass.CreateStream(soundData, 0, soundData.Length, /*BassFlags.Decode |*/ BassFlags.Float);
        var sound = new BassBakkaSound(bassChannel);
        if (playLooped)
            Bass.ChannelAddFlag(bassChannel, BassFlags.Loop);
        if (!startPaused)
            sound.Paused = false;
        return sound;
    }

    public IBakkaSound Play2D(string soundFilename, bool playLooped, bool startPaused)
    {
        // try loading as flac first to avoid the buggy flac implementation
        var decodeChannel = BassFlac.CreateStream(soundFilename, 0, 0, BassFlags.Decode);
        if (decodeChannel == 0 && Bass.LastError == Errors.FileFormat)
            // try loading normally if we get a file format error
            decodeChannel = Bass.CreateStream(soundFilename, 0, 0, BassFlags.Decode);

        if (decodeChannel == 0)
        {
            throw new Exception($"Couldn't load selected audio file: {Bass.LastError}");
            return null; // irrKlang behavior
        }

        var bassChannel = BassFx.TempoCreate(decodeChannel, BassFlags.FxFreeSource);
        if (bassChannel == 0)
        {
            Bass.StreamFree(decodeChannel);
            return null;
        }

        var sound = new BassBakkaSound(bassChannel);
        if (playLooped)
            Bass.ChannelAddFlag(bassChannel, BassFlags.Loop);
        if (!startPaused)
            sound.Paused = false;
        return sound;
    }

    public IBakkaSample? LoadSample(string soundFilename)
    {
        var sample = new BassBakkaSample(soundFilename);
        if (Bass.LastError != Errors.OK) throw new Exception($"Couldn't load sample file: {Bass.LastError}");
        return !sample.Loaded ? null : sample;
    }

    public double GetLatency()
    {
        var hasBassInfo = Bass.GetInfo(out var bassInfo);
        if (hasBassInfo)
            return bassInfo.Latency / 1000.0; // ms to seconds
        return 0.0;
    }
}