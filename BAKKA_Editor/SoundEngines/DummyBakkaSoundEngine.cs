using System.IO;

namespace BAKKA_Editor.SoundEngines;

public class DummyBakkaSoundEngine : IBakkaSoundEngine
{
    public IBakkaSound Play2D(Stream soundFilename, bool playLooped, bool startPaused)
    {
        return null!;
    }

    public IBakkaSound Play2D(string soundFilename, bool playLooped, bool startPaused)
    {
        return null!;
    }

    public IBakkaSample? LoadSample(string soundFilename)
    {
        return null;
    }

    public double GetLatency()
    {
        return 0.0;
    }
}