using System.IO;

namespace BAKKA_Editor.SoundEngines;

public interface IBakkaSoundEngine
{
    public IBakkaSound Play2D(Stream soundFilename, bool playLooped, bool startPaused);
    public IBakkaSound Play2D(string soundFilename, bool playLooped, bool startPaused);

    public IBakkaSample? LoadSample(string soundFilename);
}