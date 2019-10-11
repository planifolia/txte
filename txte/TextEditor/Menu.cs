using txte.Input;

namespace txte.TextEditor
{
    class Menu
    {
        public Menu(KeyBindSet keyBinds)
        {
            this.KeyBinds = keyBinds;
        }

        public readonly KeyBindSet KeyBinds;
        public bool IsShown => this.isShown.Value;

        readonly RestorableValue<bool> isShown = new RestorableValue<bool>();

        public RestorableValue<bool>.MementoToken ShowWhileModal()
        {
            var token = this.isShown.SaveValue();
            this.isShown.Value = true;
            return token;
        }

        public void Hide() => this.isShown.Value = false;
    }
}