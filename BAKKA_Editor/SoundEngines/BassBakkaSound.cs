using System;
using ManagedBass;

namespace BAKKA_Editor.SoundEngines;

public class BassBakkaSound : IBakkaSound
{
    private uint _playPosition;
    private readonly int bassChannel;

    private bool paused = true;

    private float playbackSpeed = 1.0f;

    private float volume = 1.0f;

    public BassBakkaSound(int channel)
    {
        bassChannel = channel;

        // set play length
        var length = Bass.ChannelBytes2Seconds(bassChannel, Bass.ChannelGetLength(bassChannel));
        if (length < 0)
            throw new Exception($"Error getting song length: {Bass.LastError}");
        PlayLength = (uint) (length * 1000.0);
    }

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

    public bool Paused
    {
        get => paused;
        set
        {
            SetPauseState(value);
            paused = value;
        }
    }

    public uint PlayPosition
    {
        get
        {
            if (paused)
                return _playPosition;
            return GetPosition();
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

    private void SetSpeed(float value)
    {
        // 0-255
        var intSpeed = Math.Abs(value - 1.0f) < 0.0001 ? 0 : (int) ((value - 1.0f) * 100.0f);
        Bass.ChannelSetAttribute(bassChannel, ChannelAttribute.Tempo, intSpeed);
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
            Bass.ChannelSetAttribute(bassChannel, ChannelAttribute.Volume, volume);
            SetPosition(_playPosition);
            if (!Bass.ChannelPlay(bassChannel))
            {
                // throw new Exception($"Error playing: {Bass.LastError}");
            }
        }
    }
}