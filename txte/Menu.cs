using System;
using System.Collections.Generic;

namespace txte
{
    class MenuItem
    {
        public MenuItem(ConsoleKeyInfo keyInfo, string effect, EditorSetting setting)
        {
            this.keyInfo = keyInfo;
            var keys = new List<string>();
            if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
            {
                keys.Add("Ctrl");
            }
            if ((keyInfo.Modifiers & ConsoleModifiers.Shift) != 0)
            {
                keys.Add("Shift");
            }
            keys.Add(keyInfo.Key.ToString());

            this.keys = keys.ToArray();
            this.effect = effect;
            this.setting = setting;
        }
        
        public readonly ConsoleKeyInfo keyInfo;
        public readonly string[] keys;
        public readonly string effect;
        readonly EditorSetting setting;
    }

    class Menu
    {
        public Menu(EditorSetting setting)
        {
            this.setting = setting;
            this.Items = MakeMenuItems(this.setting);
        }

        public bool IsShown { get; set; }
        public IReadOnlyList<MenuItem> Items;
        private readonly EditorSetting setting;

        IReadOnlyList<MenuItem> MakeMenuItems(EditorSetting setting)
        {
            var items = new[] {
                new MenuItem(new ConsoleKeyInfo((char)0x0, ConsoleKey.F, false, false, true), "Find", setting),
                new MenuItem(new ConsoleKeyInfo((char)0x0, ConsoleKey.Q, false, false, true), "Quit", setting),
                new MenuItem(new ConsoleKeyInfo((char)0x0, ConsoleKey.S, false, false, true), "Save", setting),
                new MenuItem(new ConsoleKeyInfo((char)0x0, ConsoleKey.S, true, false, true), "Save As", setting),
                new MenuItem(new ConsoleKeyInfo((char)0x0, ConsoleKey.L, false, false, true), "Refresh", setting),
                new MenuItem(new ConsoleKeyInfo((char)0x0, ConsoleKey.E, true, false, true), "Change East Asian Width", setting),
                new MenuItem(new ConsoleKeyInfo((char)0x0, ConsoleKey.L, true, false, true), "Change End of Line sequence", setting),
            };
            return items;
        }
    }
}