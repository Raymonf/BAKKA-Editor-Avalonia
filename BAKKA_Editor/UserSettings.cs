using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Input;

namespace BAKKA_Editor
{
    internal class UserSettings
    {
        public ViewSettings ViewSettings { get; set; } = new();
        public SaveSettings SaveSettings { get; set; } = new();
        public HotkeySettings HotkeySettings { get; set; } = new();
    }

    internal class ViewSettings
    {
        public bool ShowCursor { get; set; } = true;
        public bool ShowCursorDuringPlayback { get; set; } = false;
        public bool HighlightViewedNote { get; set; } = true;
        public bool SelectLastInsertedNote { get; set; } = true;
    }

    internal class SaveSettings
    {
        /// <summary>
        /// How frequently autosave occurs (in minutes)
        /// </summary>
        public int AutoSaveInterval { get; set; } = 1;
    }

    internal class HotkeySettings
    {
        public int TouchHotkey { get; set; } = Convert.ToInt32(Key.D1);
        public int SlideLeftHotkey { get; set; } = Convert.ToInt32(Key.D2);
        public int SlideRightHotkey { get; set; } = Convert.ToInt32(Key.D3);
        public int SnapUpHotkey { get; set; } = Convert.ToInt32(Key.D4);
        public int SnapDownHotkey { get; set; } = Convert.ToInt32(Key.D5);
        public int ChainHotkey { get; set; } = Convert.ToInt32(Key.D6);
        public int HoldHotkey { get; set; } = Convert.ToInt32(Key.D7);
        public int PlayHotkey { get; set; } = Convert.ToInt32(Key.P);
    }
}
