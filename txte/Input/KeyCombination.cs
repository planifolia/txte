using System;

namespace txte.Input
{
    struct KeyCombination
    {
        public KeyCombination(ConsoleKey key, bool shifted, bool controled)
        : this(key, shifted, controled, false) { }

        public KeyCombination(ConsoleKey key, bool shifted, bool controled, bool alted)
        {
            this.Key = key;
            this.Shifted = shifted;
            this.Controled = controled;
            this.Alted = alted;
        }

        public readonly ConsoleKey Key;
        public readonly bool Shifted;
        public readonly bool Controled;
        public readonly bool Alted;

        public KeyCombination WithControl() =>
            new KeyCombination(this.Key, this.Shifted, true, this.Alted);
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