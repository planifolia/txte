using System;
using System.Collections.Generic;

namespace txte
{
    struct ShortcutKey
    {
        public ShortcutKey(ConsoleKey key, bool shifted, bool controled)
        : this(key, shifted, false, controled) { }

        public ShortcutKey(ConsoleKey key, bool shifted, bool alted, bool controled) =>
            (this.Key, this.Shifted, this.Alted, this.Controled) = (key, shifted, alted, controled);

        public readonly ConsoleKey Key;
        public readonly bool Shifted;
        public readonly bool Alted;
        public readonly bool Controled;

        public ShortcutKey WithControl() => 
            new ShortcutKey(this.Key, this.Shifted, this.Alted, true);
    }

    static class ShortcutKeyExtensions
    {
        public static ShortcutKey ToShortcutKey(this ConsoleKeyInfo keyInfo) =>
            new ShortcutKey(
                keyInfo.Key,
                shifted: (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
                alted: (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
                controled: (keyInfo.Modifiers & ConsoleModifiers.Control) != 0
            );
    }
    
    class Shortcut
    {
        public Shortcut(ShortcutKey shortcutKey, string effect, EditorSetting setting)
        {
            this.shortcutKey = shortcutKey;
            var keys = new List<string>();
            if (shortcutKey.Controled)
            {
                keys.Add("Ctrl");
            }
            if (shortcutKey.Shifted)
            {
                keys.Add("Shift");
            }
            keys.Add(shortcutKey.Key.ToString());

            this.keys = keys.ToArray();
            this.effect = effect;
            this.setting = setting;
        }
        
        public readonly ShortcutKey shortcutKey;
        public readonly string[] keys;
        public readonly string effect;
        readonly EditorSetting setting;
    }
}