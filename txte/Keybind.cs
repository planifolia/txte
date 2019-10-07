using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace txte
{
    class KeyBind
    {
        public IReadOnlyList<Shortcut> Shortcuts => this.shortcuts;

        readonly List<Shortcut> shortcuts = new List<Shortcut>();
        readonly Dictionary<ShortcutKey, Func<Task<KeyProcessingResults>>> functions =
            new Dictionary<ShortcutKey, Func<Task<KeyProcessingResults>>>();

        public Func<Task<KeyProcessingResults>> this[Shortcut shortcut]
        {
            set {
                this.shortcuts.Add(shortcut);
                this.functions[shortcut.shortcutKey] = value;
            }
        }
        
        public Func<Task<KeyProcessingResults>>? this[ShortcutKey shortcutKey]
        {
            get =>
                (this.functions.TryGetValue(shortcutKey, out var function)) ? function
                : (Func<Task<KeyProcessingResults>>?)null;
        }
    }
}