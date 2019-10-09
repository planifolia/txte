using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace txte
{
    class KeyBind
    {
        public KeyBind(KeyCombination combination, string explanation)
        {
            this.combination = combination;
            var keys = new List<string>();
            if (combination.Controled)
            {
                keys.Add("Ctrl");
            }
            if (combination.Shifted)
            {
                keys.Add("Shift");
            }
            keys.Add(combination.Key.ToString());

            this.keys = keys.ToArray();
            this.explanation = explanation;
        }
        
        public readonly KeyCombination combination;
        public readonly string[] keys;
        public readonly string explanation;
    }

    class KeyBindSet
    {
        public IReadOnlyList<KeyBind> KeyBinds => this.keyBinds;

        readonly List<KeyBind> keyBinds = new List<KeyBind>();
        readonly Dictionary<KeyCombination, Func<Task<KeyProcessingResults>>> functions =
            new Dictionary<KeyCombination, Func<Task<KeyProcessingResults>>>();

        public Func<Task<KeyProcessingResults>> this[KeyBind keyBind]
        {
            set {
                this.keyBinds.Add(keyBind);
                this.functions[keyBind.combination] = value;
            }
        }
        
        public Func<Task<KeyProcessingResults>>? this[KeyCombination shortcutKey]
        {
            get =>
                (this.functions.TryGetValue(shortcutKey, out var function)) ? function
                : (Func<Task<KeyProcessingResults>>?)null;
        }
    }
}