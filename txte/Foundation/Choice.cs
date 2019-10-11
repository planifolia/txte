namespace txte
{
    interface IChoice
    {
        string Name { get; }
        char Shortcut { get; } 
    }

    class Choice : IChoice
    {
        public static readonly Choice Yes = new Choice("Yes", 'y');
        public static readonly Choice No = new Choice("No", 'n');
        
        public Choice(string name, char shortcut)
        {
            this.Name = name;
            this.Shortcut = shortcut;
        }
        public string Name { get; }
        public char Shortcut { get; } 
    }
}