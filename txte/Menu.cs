using System;
using System.Collections.Generic;

namespace txte
{
    class Menu
    {
        public Menu(EditorSetting setting, KeyBind keyBind)
        {
            this.setting = setting;
            this.KeyBind = keyBind;
        }

        public readonly RecoverableValue<bool> IsShown = new RecoverableValue<bool>();
        public readonly KeyBind KeyBind;
        readonly EditorSetting setting;

        IReadOnlyList<Shortcut> MakeMenuItems(EditorSetting setting)
        {
            var items = new[] {
                new Shortcut(new ShortcutKey(ConsoleKey.F, false, true), "Find", setting),
                new Shortcut(new ShortcutKey(ConsoleKey.Q, false, true), "Quit", setting),
                new Shortcut(new ShortcutKey(ConsoleKey.S, false, true), "Save", setting),
                new Shortcut(new ShortcutKey(ConsoleKey.S, true, true), "Save As", setting),
                new Shortcut(new ShortcutKey(ConsoleKey.L, false, true), "Refresh", setting),
                new Shortcut(new ShortcutKey(ConsoleKey.E, true, true), "Change East Asian Width", setting),
                new Shortcut(new ShortcutKey(ConsoleKey.L, true, true), "Change End of Line sequence", setting),
            };
            return items;
        }
    }
}