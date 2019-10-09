using System;
using System.Collections.Generic;

namespace txte
{
    struct KeyCombination
    {
        public KeyCombination(ConsoleKey key, bool shifted, bool controled)
        : this(key, shifted, false, controled) { }

        public KeyCombination(ConsoleKey key, bool shifted, bool alted, bool controled) =>
            (this.Key, this.Shifted, this.Alted, this.Controled) = (key, shifted, alted, controled);

        public readonly ConsoleKey Key;
        public readonly bool Shifted;
        public readonly bool Alted;
        public readonly bool Controled;

        public KeyCombination WithControl() => 
            new KeyCombination(this.Key, this.Shifted, this.Alted, true);
    }

    static class KeyCombinationExtensions
    {
        public static KeyCombination ToKeyCombination(this ConsoleKeyInfo keyInfo) =>
            new KeyCombination(
                keyInfo.Key,
                shifted: (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
                alted: (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
                controled: (keyInfo.Modifiers & ConsoleModifiers.Control) != 0
            );
    }
}