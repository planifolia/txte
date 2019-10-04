using System;
using System.Collections.Generic;
using System.Linq;

namespace txte
{
    interface IChoice
    {
        string Name { get; }
        char Shortcut { get; } 
    }

    class Choice : IChoice
    {
        public static Choice Yes = new Choice("Yes", 'y');
        public static Choice No = new Choice("No", 'n');
        
        public Choice(string name, char shortcut)
        {
            this.Name = name;
            this.Shortcut = shortcut;
        }
        public string Name { get; }
        public char Shortcut { get; } 
    }

    class ChoosePrompt : IPrompt<IChoice>
    {
        public ChoosePrompt(string message, IReadOnlyList<IChoice> choices, IChoice? default_choice = null)
        {
            this.message = message;
            this.choices = choices;
            if (default_choice == null)
            {
                this.choosenIndex = 0;
            }
            else
            {
                this.choosenIndex = 
                    this.choices
                    .Select((c, i) => (c, i))
                    .Where(x => x.c == default_choice)
                    .Select(x => x.i)
                    .First();
            }
        }

        readonly string message;
        readonly IReadOnlyList<IChoice> choices;
        int choosenIndex;

        public IChoice Choosen => this.choices[this.choosenIndex];

        public (KeyProcessingResults, IChoice?) ProcessKey(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo)
            {
                case { Key: ConsoleKey.LeftArrow, Modifiers: (ConsoleModifiers)0 }:
                    this.MoveLeft();
                    return (KeyProcessingResults.Running, default);
                case { Key: ConsoleKey.RightArrow, Modifiers: (ConsoleModifiers)0 }:
                    this.MoveRight();
                    return (KeyProcessingResults.Running, default);
                case { Key: ConsoleKey.Escape, Modifiers: (ConsoleModifiers)0 }:
                    return (KeyProcessingResults.Quit, default);
                case { Key: ConsoleKey.Enter, Modifiers: (ConsoleModifiers)0 }:
                    return (KeyProcessingResults.Quit, this.Choosen);
                default:
                    if (this.AcceptShortcut(keyInfo.KeyChar) is { } choosen)
                    {
                        return (KeyProcessingResults.Quit, choosen);
                    }
                    else
                    {
                    return (KeyProcessingResults.Running, default);
                    }
            }
        }

        public IEnumerable<StyledString> ToStyledString()
        {
            var styled = new List<StyledString>();
            styled.Add(new StyledString(this.message, ColorSet.SystemMessage));
            styled.Add(new StyledString(" "));
            var choiceCount = this.choices.Count;
            for (int i = 0; i < choiceCount; i++)
            {
                if (i != 0) {
                    styled.Add(new StyledString(" / "));
                }
                var colorSet = (i == this.choosenIndex) ? ColorSet.Reversed : ColorSet.Default;
                styled.Add(new StyledString(
                    $"{this.choices[i].Name}({this.choices[i].Shortcut})",
                    colorSet
                ));
            }
            return styled;
        }

        void MoveLeft()
        {
            var choiceCount = this.choices.Count;
            this.choosenIndex = (this.choosenIndex - 1 + choiceCount) % choiceCount;
        }
        void MoveRight()
        {
            var choiceCount = this.choices.Count;
            this.choosenIndex = (this.choosenIndex + 1) % choiceCount;
        }

        IChoice? AcceptShortcut(char keyChar)
        {
            foreach (var choice in this.choices)
            {
                if (choice.Shortcut ==keyChar) { return choice; }
            }
            
            return null;
        }
    }
}