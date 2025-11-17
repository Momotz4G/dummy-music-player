using System;
using System.Runtime.InteropServices;

namespace Dummy_Music_Player
{
    internal static class NativeMethods
    {
        // ----- For AddHook (WM_APPCOMMAND) - Good to keep for when focused -----
        public const int WM_APPCOMMAND = 0x0319;
        public const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;
        public const int APPCOMMAND_MEDIA_NEXTTRACK = 11;
        public const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;
        public const int APPCOMMAND_MEDIA_PLAY = 46;
        public const int APPCOMMAND_MEDIA_PAUSE = 47;

        // ----- For RegisterHotKey (WM_HOTKEY) - For global, unfocused -----
        public const int WM_HOTKEY = 0x0312;

        // Hotkey IDs we will use
        public const int HOTKEY_ID_PLAY_PAUSE = 9000;
        public const int HOTKEY_ID_NEXT = 9001;
        public const int HOTKEY_ID_PREV = 9002;

        // Modifiers (we want no modifier, just the key)
        public const uint MOD_NOREPEAT = 0x4000; // (Optional: tells Windows not to send rapid-fire messages if key is held down)

        // Virtual Key codes for media keys
        public const uint VK_MEDIA_PLAY_PAUSE = 0xB3;
        public const uint VK_MEDIA_NEXT_TRACK = 0xB0;
        public const uint VK_MEDIA_PREV_TRACK = 0xB1;


        // ----- API Functions -----

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}