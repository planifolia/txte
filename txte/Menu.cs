namespace txte
{
    class Menu
    {
        public Menu(EditorSetting setting, KeyBindSet keyBinds)
        {
            this.setting = setting;
            this.KeyBinds = keyBinds;
        }

        public readonly KeyBindSet KeyBinds;
        public bool IsShown => this.isShown.Value;

        readonly EditorSetting setting;
        readonly RecoverableValue<bool> isShown = new RecoverableValue<bool>();

        public void Show() => this.isShown.Value = true;

        public RecoverableValue<bool>.MementoToken ShowWhileModal()
        {
            var token = this.isShown.SaveValue();
            this.isShown.Value = true;
            return token;
        }

        public void Hide() => this.isShown.Value = false;
    }
}