using System;
using ManagedBass;

namespace BAKKA_Editor.SoundEngines;

public class BassBakkaSound : IBakkaSound
{
    private int bassChannel = 0;

    private float volume = 1.0f;
    public float Volume
    {
        get => volume;
        set
        {
            if (volume != value)
                Bass.ChannelSetAttribute(bassChannel, ChannelAttribute.Volume, volume);
            volume = value;
        }
    }

    private float playbackSpeed = 1.0f;

    public float PlaybackSpeed
    {
        get => playbackSpeed;
        set
        {
            if (playbackSpeed != value)
                SetSpeed(value);
            playbackSpeed = value;
        }
    }

    private void SetSpeed(float value)
    {
        // 0-255
        var intSpeed = Math.Abs(value - 1.0f) < 0.0001 ? 0 : (int)((value - 1.0f) * 100.0f);
        Bass.ChannelSetAttribute(bassChannel, ChannelAttribute.Tempo, intSpeed);
    }

    private bool paused = true;
    public bool Paused
    {
        get => paused;
        set
        {
            SetPauseState(value);
            paused = value;
        }
    }

    private uint _playPosition = 0;
    public uint PlayPosition
    {
        get
        {
            if (paused)
                return _playPosition;
            return (uint) GetPosition();
        }
        set
        {
            if (paused)
                _playPosition = value;
            else if (!SetPosition(value))
                throw new Exception($"bruh: {Bass.LastError}");
        }
    }

    public uint PlayLength { get; } = uint.MaxValue;

    public BassBakkaSound(int channel)
    {
        bassChannel = channel;

        // set play length
        var length = Bass.ChannelBytes2Seconds(bassChannel, Bass.ChannelGetLength(bassChannel));
        if (length < 0)
            throw new Exception($"Error getting song length: {Bass.LastError}");
        PlayLength = (uint)(length * 1000.0);
    }

    private bool SetPosition(uint position)
    {
        return Bass.ChannelSetPosition(bassChannel, Bass.ChannelSeconds2Bytes(bassChannel, position / 1000.0));
    }

    private uint GetPosition()
    {
        return (uint) (Bass.ChannelBytes2Seconds(bassChannel, Bass.ChannelGetPosition(bassChannel)) * 1000.0);
    }
    
    private void SetPauseState(bool newPaused)
    {
        if (newPaused)
        {
            if (!Bass.ChannelPause(bassChannel))
            {
                // throw new Exception($"Error pausing: {Bass.LastError}");
            }

            _playPosition = GetPosition();
        }
        else
        {
            SetPosition(_playPosition);
            if (!Bass.ChannelPlay(bassChannel))
            {
                // throw new Exception($"Error playing: {Bass.LastError}");
            }
        }
    }
}