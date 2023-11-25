using BAKKA_Editor.Enums;
using BAKKA_Editor.SoundEngines;
using BAKKA_Editor.Views;
using System;
using System.Collections.Generic;

namespace BAKKA_Editor.Audio;

internal class Hitsounds
{
    private IBakkaSampleChannel? hitsoundChannel;
    private IBakkaSampleChannel? hitsoundSwipeChannel;
    private IBakkaSampleChannel? hitsoundBonusChannel;
    private IBakkaSampleChannel? hitsoundFlairChannel;

    private IBakkaSample? hitsoundSample;
    private IBakkaSample? hitsoundSwipeSample;
    private IBakkaSample? hitsoundBonusSample;
    private IBakkaSample? hitsoundFlairSample;
    private UserSettings userSettings;
    private MainView mainView;

    public Hitsounds(UserSettings settings, MainView main)
    {
        userSettings = settings;
        mainView = main;
    }

    public void LoadSamples(string hitsoundPath, string hitsoundSwipePath, string hitsoundBonusPath, string hitsoundFlairPath)
    {
        hitsoundSample = new BassBakkaSample(hitsoundPath);
        hitsoundSwipeSample = new BassBakkaSample(hitsoundSwipePath);
        hitsoundBonusSample = new BassBakkaSample(hitsoundBonusPath);
        hitsoundFlairSample = new BassBakkaSample(hitsoundFlairPath);

        if (hitsoundSample.Loaded) hitsoundChannel = hitsoundSample.GetChannel();
        if (hitsoundSwipeSample.Loaded) hitsoundSwipeChannel = hitsoundSwipeSample.GetChannel();
        if (hitsoundBonusSample.Loaded) hitsoundBonusChannel = hitsoundBonusSample.GetChannel();
        if (hitsoundFlairSample.Loaded) hitsoundFlairChannel = hitsoundFlairSample.GetChannel();
    }

    public bool LoadError()
    {
        if (userSettings.SoundSettings.HitsoundEnabled && !hitsoundSample.Loaded
            ||(userSettings.SoundSettings.HitsoundSwipeEnabled && !hitsoundSwipeSample.Loaded)
            || (userSettings.SoundSettings.HitsoundBonusEnabled && !hitsoundBonusSample.Loaded)
            || (userSettings.SoundSettings.HitsoundFlairEnabled && !hitsoundFlairSample.Loaded))
            return true;

        return false;
    }

    public bool ChannelsExist()
    {
        if ((userSettings.SoundSettings.HitsoundEnabled && hitsoundChannel != null)
            || (userSettings.SoundSettings.HitsoundSwipeEnabled && hitsoundSwipeChannel != null)
            || (userSettings.SoundSettings.HitsoundBonusEnabled && hitsoundBonusChannel != null)
            || (userSettings.SoundSettings.HitsoundFlairEnabled && hitsoundFlairChannel != null))
            return true;

        return false;
    }

    public void SetVolume(float volume)
    {
        hitsoundChannel?.SetVolume(volume);
        hitsoundSwipeChannel?.SetVolume(volume);
        hitsoundBonusChannel?.SetVolume(volume);
        hitsoundFlairChannel?.SetVolume(volume);
    }

    public void Play(Note note, float lastMeasure)
    {
        if (note.NoteType is NoteType.EndOfChart or NoteType.MaskAdd or NoteType.MaskRemove or NoteType.HoldJoint) return;

        if (note.BeatInfo.MeasureDecimal > lastMeasure)
        {
            if ((note.IsSnap || note.IsSlide) && userSettings.SoundSettings.HitsoundSwipeEnabled) hitsoundSwipeChannel?.Play(true);
            else if (userSettings.SoundSettings.HitsoundEnabled) hitsoundChannel?.Play(true);
            if (note.IsBonus && userSettings.SoundSettings.HitsoundBonusEnabled) hitsoundBonusChannel?.Play(true);
            if (note.IsFlair && userSettings.SoundSettings.HitsoundFlairEnabled) hitsoundFlairChannel?.Play(true);
        }
    }
}

