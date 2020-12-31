using System;
using System.Collections.Generic;
using System.Linq;
using txte.State;
using txte.Text;

namespace txte.Prompts
{
    static class ChoosePrompt
    {
        public static ChoosePrompt<TChoice> Create<TChoice>(
            string message, IReadOnlyList<TChoice> choices, TChoice? default_choice = null
        ) where TChoice : class, IChoice =>
            new ChoosePrompt<TChoice>(message, choices, default_choice);

    }

    class ChoosePrompt<TChoice> : IPrompt<TChoice> where TChoice : class, IChoice
    {
        public ChoosePrompt(string message, IReadOnlyList<TChoice> choices, TChoice? default_choice = null)
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
        readonly IReadOnlyList<TChoice> choices;
        int choosenIndex;

        public TChoice Choosen => this.choices[this.choosenIndex];

        public ModalProcessResult<TChoice> ProcessKey(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo)
            {
                case { Key: ConsoleKey.LeftArrow, Modifiers: (ConsoleModifiers)0 }:
                    this.MoveLeft();
                    return ModalRunning.Default;
                case { Key: ConsoleKey.RightArrow, Modifiers: (ConsoleModifiers)0 }:
                    this.MoveRight();
                    return ModalRunning.Default;
                case { Key: ConsoleKey.Escape, Modifiers: (ConsoleModifiers)0 }:
                    return ModalCancel.Default;
                case { Key: ConsoleKey.Enter, Modifiers: (ConsoleModifiers)0 }:
                    return ModalOk.Create(this.Choosen);
                default:
                    if (this.AcceptShortcut(keyInfo.KeyChar) is { } choosen)
                    {
                        return ModalOk.Create(choosen);
                    }
                    else
                    {
                        return ModalUnhandled.Default;
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
                if (i != 0)
                {
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

        TChoice? AcceptShortcut(char keyChar)
        {
            foreach (var choice in this.choices)
            {
                if (choice.Shortcut == keyChar) return choice;
            }

            return null;
        }
    }
}