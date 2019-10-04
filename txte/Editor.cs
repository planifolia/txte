using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace txte
{
    class TemporaryValue<T>
    {
        public TemporaryValue()
        {
            this.value = default!;
        }

        public bool HasValue { get; private set; }
        public T Value => this.HasValue ? this.value : throw new InvalidOperationException();

        T value;

        public DisposingToken SetTemporary(T value)
        {
            this.HasValue = true;
            this.value = value;
            return new DisposingToken(this);
        }

        public class DisposingToken : IDisposable
        {
            public DisposingToken(TemporaryValue<T> source)
            {
                this.source = source;
            }

            TemporaryValue<T> source;

            #region IDisposable Support
            private bool disposedValue = false;

            protected virtual void Dispose(bool disposing)
            {
                if (!this.disposedValue)
                {
                    if (disposing)
                    {
                        this.source.HasValue = false;
                        this.source.value = default!;
                    }

                    this.source = null!;

                    disposedValue = true;
                }
            }

            public void Dispose()
            {
                Dispose(true);
            }
            #endregion
        }
    }

    enum KeyProcessingResults
    {
        Running,
        Quit,
        Unhandled,
    }

    class TemporaryMessage
    {
        public TemporaryMessage(string value, DateTime time)
        {
            this.Value = value;
            this.Time = time;
        }

        public string Value { get; }
        public DateTime Time { get; }
    }

    interface IPrompt
    {
        IEnumerable<StyledString> ToStyledString();
    }
    interface IPrompt<TResult> : IPrompt where TResult: class
    {
        (KeyProcessingResults, TResult?) ProcessKey(ConsoleKeyInfo keyInfo);
    }

    interface IChoice
    {
        string Name { get; }
        char Shortcut { get; } 
    }

    class Choice : IChoice
    {
        public Choice(string name, char shortcut)
        {
            this.Name = name;
            this.Shortcut = shortcut;
        }
        public string Name { get; }
        public char Shortcut { get; } 
    }

    class ChoosePromptInfo : IPrompt, IPrompt<IChoice>
    {
        public ChoosePromptInfo(string message, IReadOnlyList<IChoice> choices, IChoice? default_choice = null)
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

    class InputPromptInfo : IPrompt, IPrompt<string>
    {
        public InputPromptInfo(string message)
        {
            this.Message = message;
            this.input = new StringBuilder();
        }

        public string Message { get; }

        private StringBuilder input;

        public string Current => this.input.ToString();

        public (KeyProcessingResults, string?) ProcessKey(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo)
            {
                case { Key: ConsoleKey.Backspace, Modifiers: (ConsoleModifiers)0 }:
                    input.Length = (input.Length - 1).AtMin(0);
                    return (KeyProcessingResults.Running, default);
                case { Key: ConsoleKey.Escape, Modifiers: (ConsoleModifiers)0 }:
                    return (KeyProcessingResults.Quit, default);
                case { Key: ConsoleKey.Enter, Modifiers: (ConsoleModifiers)0 }:
                    return (KeyProcessingResults.Quit, input.ToString());
                default:
                    if (!char.IsControl(keyInfo.KeyChar))
                    { 
                        input.Append(keyInfo.KeyChar);
                    }
                    return (KeyProcessingResults.Running, default);
            }
        }

        public IEnumerable<StyledString> ToStyledString()
        {
            var styled = new List<StyledString>();
            styled.Add(new StyledString(this.Message, ColorSet.SystemMessage));
            styled.Add(new StyledString(" "));
            styled.Add(new StyledString(this.Current));
            return styled;
        }
    }

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
                new MenuItem(new ConsoleKeyInfo((char)0x0, ConsoleKey.Q, false, false, true), "Quit", setting),
                new MenuItem(new ConsoleKeyInfo((char)0x0, ConsoleKey.S, false, false, true), "Save", setting),
                new MenuItem(new ConsoleKeyInfo((char)0x0, ConsoleKey.L, false, false, true), "Refresh", setting),
                new MenuItem(new ConsoleKeyInfo((char)0x0, ConsoleKey.E, true, false, true), "Change East Asian Width", setting),
                new MenuItem(new ConsoleKeyInfo((char)0x0, ConsoleKey.L, true, false, true), "Change End of Line sequence", setting),
            };
            return items;
        }
    }

    class EditorSetting
    {
        public bool IsFullWidthAmbiguous { get; set; } = false;
        public int TabSize { get; set; } = 4;
    }

    class Editor
    {
        public static string Version = "0.0.1";


        public Editor(IConsole console, EditorSetting setting, Document document)
        {
            this.console = console;
            this.setting = setting;
            this.document = document;
            this.prompt = new TemporaryValue<IPrompt>();
            this.menu = new Menu(setting);
        }

        readonly IConsole console;
        readonly List<TemporaryMessage> messages = new List<TemporaryMessage>();
        readonly Menu menu;

        Document document;
        TemporaryValue<IPrompt> prompt;
        EditorSetting setting;

        Size editArea => 
            new Size(
                this.console.Size.Width,
                this.console.Size.Height
                - (this.prompt.HasValue ? 1 : 0)
                - this.messages.Count
                - 1
            );

        public void AddMessage(string value)
        {
            this.messages.Add(new TemporaryMessage(value, DateTime.Now));
        }

        IEnumerable<TemporaryMessage> DiscardMessages()
        {
            var now = DateTime.Now;
            var expireds = 
                this.messages.Where(x => now - x.Time > TimeSpan.FromSeconds(5)).ToArray();
            foreach (var x in expireds)
            {
                this.messages.Remove(x);
            }
            return expireds;
        }

        public async Task Run()
        {
            this.RefreshScreen(0);
            while (true)
            {
                (var eventType, var keyInfo) = await this.console.ReadKeyOrTimeoutAsync();
                if (eventType == EventType.UserAction) {
                    switch (await this.ProcessKeyPress(keyInfo))
                    {
                        case KeyProcessingResults.Quit:
                            return;
                        default:
                            break;
                    }
                    this.RefreshScreen(0);
                }
                else
                {
                    var editAreaHeight = this.editArea.Height;
                    if (this.DiscardMessages().Any())
                    {
                        this.RefreshScreen(editAreaHeight.AtMin(0));
                    }

                }
            }
        }

        void RefreshScreen(int from)
        {
            this.document.UpdateOffset(this.editArea, this.setting);
            this.console.RefreshScreen(
                from,
                this.setting,
                this.RenderScreen,
                this.document.Cursor);
        }

        void RenderScreen(IScreen screen, int from)
        {
            this.DrawEditorRows(screen, from);
            this.DrawSatausBar(screen);
            this.DrawPromptBar(screen);
            this.DrawMessageBar(screen);
        }

        void DrawEditorRows(IScreen screen, int from)
        {
            bool ambiguousSetting = this.setting.IsFullWidthAmbiguous;
            for (int y = from; y < this.editArea.Height; y++)
            {
                var docRow = y + this.document.Offset.Y;
                if (this.menu.IsShown)
                {
                    this.DrawMenu(screen, y);
                }
                else if (docRow < this.document.Rows.Count)
                {
                    this.DrawDocumentRow(screen, ambiguousSetting, docRow);
                }
                else
                {
                    this.DrawOutofBounds(screen, y);
                }
            }
        }

        void DrawDocumentRow(IScreen screen, bool ambiguousSetting, int docRow)
        {
            var render = this.document.Rows[docRow].Render;
            var sublenderLength =
                (render.GetConsoleLength(ambiguousSetting) - this.document.Offset.X)
                .Clamp(0, this.console.Width);
            if (sublenderLength > 0)
            {
                var (subrender, startIsFragmented, endIsFragmented) =
                    render.SubConsoleString(this.document.Offset.X, sublenderLength, ambiguousSetting);

                var spans = new List<StyledString>();
                if (startIsFragmented) { spans.Add(new StyledString("]", ColorSet.Fragment)); }
                spans.Add(new StyledString(subrender));
                if (endIsFragmented) { spans.Add(new StyledString("[", ColorSet.Fragment)); }

                screen.AppendRow(spans);
            }
            else
            {
                screen.AppendRow("");
            }
        }
        
        void DrawMenu(IScreen screen, int y)
        {
            var keyJoiner = " + ";
            var separator = "  ";
            var titleRowCount = 3;

            if (y - titleRowCount >= this.menu.Items.Count)
            {
                screen.AppendRow(new[]
                { 
                    // new StyledString(new string(' ', this.console.Width), ColorSet.SystemMessage),
                    new StyledString("~", ColorSet.OutOfBounds),
                });
            }
            else if (y == 0)
            {
                var message = "Menu / Shortcuts";
                var messageLength = message.Length;
                var leftPadding = (this.console.Width - messageLength) / 2 - 1;
                screen.AppendRow(new[]
                { 
                    new StyledString("~", ColorSet.OutOfBounds),
                    new StyledString(new string(' ', leftPadding), ColorSet.OutOfBounds),
                    new StyledString(message, ColorSet.OutOfBounds),
                });
            }
            else if (y == 1)
            {
                var message1 = "hint: You can omit ";
                var key = "Crtl";
                var message2 = " on the menu screen.";
                var messageLength = message1.Length + key.Length + message2.Length;
                var leftPadding = (this.console.Width - messageLength) / 2 - 1;
                screen.AppendRow(new[]
                { 
                    new StyledString("~", ColorSet.OutOfBounds),
                    new StyledString(new string(' ', leftPadding), ColorSet.OutOfBounds),
                    new StyledString(message1, ColorSet.OutOfBounds),
                    new StyledString(key, ColorSet.KeyExpression),
                    new StyledString(message2, ColorSet.OutOfBounds),
                    // new StyledString(new string(' ', this.console.Width - message1.Length - key.Length - message2.Length), ColorSet.SystemMessage),
                });
            }
            else if (y == 2)
            {
                screen.AppendRow(new[]
                { 
                    // new StyledString(new string(' ', this.console.Width), ColorSet.SystemMessage),
                    new StyledString("~", ColorSet.OutOfBounds),
                });
                return;
            }
            else
            {
                var item = this.menu.Items[y - titleRowCount];

                var leftLength = item.effect.Length;
                var rightLength = item.keys.Select(x => x.Length).Sum() + (item.keys.Length - 1) * keyJoiner.Length;
                var leftPadding = (this.console.Width - separator.Length) / 2 - leftLength - 1;
                // var rightPadding = this.console.Width - leftLength - rightLength - leftPadding - 2;
                var spans = new List<StyledString>();
                spans.Add(new StyledString("~", ColorSet.OutOfBounds));
                spans.Add(new StyledString(new string(' ', leftPadding), ColorSet.OutOfBounds));
                spans.Add(new StyledString(item.effect, ColorSet.OutOfBounds));
                spans.Add(new StyledString(separator, ColorSet.OutOfBounds));
                int i = 0;
                foreach (var key in item.keys)
                {
                    if (i != 0) {
                        spans.Add(new StyledString(keyJoiner, ColorSet.OutOfBounds));
                    }
                    spans.Add(new StyledString(key, ColorSet.KeyExpression));
                    i++;
                }
                // spans.Add(new StyledString(new string(' ', rightPadding), ColorSet.SystemMessage));
                screen.AppendRow(spans);
            }
        }

        void DrawOutofBounds(IScreen screen, int y)
        {
            if (this.document.IsUntouched && y == this.editArea.Height / 3)
            {
                var welcome = $"txte -- version {Version}";
                var welcomeLength = welcome.Length.AtMax(this.console.Width);
                var padding = (this.console.Width - welcomeLength) / 2;
                var rowBuffer = new StringBuilder();
                if (padding > 0)
                {
                    rowBuffer.Append("~");
                }
                for (int i = 1; i < padding; i++)
                {
                    rowBuffer.Append(" ");
                }
                rowBuffer.Append(welcome.Substring(0, welcomeLength));

                screen.AppendOuterRow(rowBuffer.ToString());
            }
            else
            {
                screen.AppendOuterRow("~");
            }
        }
        void DrawPromptBar(IScreen screen)
        {
            if (this.prompt.HasValue)
            {
                screen.AppendRow(this.prompt.Value.ToStyledString());
            }
        }
        void DrawSatausBar(IScreen screen)
        {
            var fileName = this.document.Path != null ? Path.GetFileName(this.document.Path) : "[New File]";
            var fileNameLength = fileName.Length.AtMax(20);
            (var clippedFileName, _, _) = 
                fileName.SubConsoleString(0, fileNameLength, this.setting.IsFullWidthAmbiguous);
            var fileInfo = $"{clippedFileName}{(this.document.IsModified ? "(*)" : "")}";
            var positionInfo = $"{this.document.Cursor.Y}:{this.document.Cursor.X} {this.document.NewLineFormat.Name}";
            var padding = this.console.Width - fileInfo.Length - positionInfo.Length;

            screen.AppendRow(new[] { new StyledString(fileInfo + new string(' ', padding) + positionInfo, ColorSet.SystemMessage) });
        }
        void DrawMessageBar(IScreen screen)
        {
            foreach (var message in this.messages)
            {
                var text = message.Value;
                var textLength = text.Length.AtMax(this.console.Width);
                screen.AppendRow(text.Substring(0, textLength));
            }
        }
        
        async Task<TResult?> Prompt<TResult>(IPrompt<TResult> prompt) where TResult: class
        {
            using var _ = this.prompt.SetTemporary(prompt);
            this.RefreshScreen(0);
            while (true)
            {
                (var eventType, var keyInfo) = await this.console.ReadKeyOrTimeoutAsync();
                if (eventType == EventType.Timeout) { continue; }
                (var state, var input) = prompt.ProcessKey(keyInfo);
                if (state == KeyProcessingResults.Quit) { return input; }
                this.RefreshScreen(this.editArea.Height);
            }
        }
        

        private async Task<KeyProcessingResults> OpenMenu()
        {
            this.menu.IsShown = true;
            this.RefreshScreen(0);
            try
            {
                while (true)
                {
                    (var eventType, var keyInfo) = await this.console.ReadKeyOrTimeoutAsync();
                    if (eventType == EventType.Timeout) { continue; }
                    if (keyInfo.Key == ConsoleKey.Escape) { return KeyProcessingResults.Running; }
                    this.menu.IsShown = false;
                    switch (keyInfo.Modifiers)
                    {
                        case ConsoleModifiers.Control | ConsoleModifiers.Shift:
                        case ConsoleModifiers.Shift:
                        {
                            if (await this.ProcessOptionShiftKeyPress(keyInfo) is var result && result != KeyProcessingResults.Unhandled)
                            {
                                return result;
                            }
                            break;
                        }
                        case ConsoleModifiers.Control:
                        case (ConsoleModifiers)0:
                        {
                            if (await this.ProcessOptionKeyPress(keyInfo) is var result && result != KeyProcessingResults.Unhandled)
                            {
                                return result;
                            }
                            break;
                        }
                        default:
                            break;
                    }
                    this.menu.IsShown = true;
                    this.RefreshScreen(0);
                }
            }
            finally
            {
                this.menu.IsShown = false;
            }
        }

        async Task<KeyProcessingResults> ProcessKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Modifiers)
            {
                case ConsoleModifiers.Control | ConsoleModifiers.Shift:
                    return await this.ProcessOptionShiftKeyPress(keyInfo);
                case ConsoleModifiers.Control:
                    return await this.ProcessOptionKeyPress(keyInfo);
                case ConsoleModifiers.Shift:
                    return this.ProcessShiftKeyPress(keyInfo);
                default:
                    return await this.ProcessSingleKeyPress(keyInfo);
            }
        }

        async Task<KeyProcessingResults> ProcessOptionShiftKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.E:
                    return this.SwitchAmbiguousWidth();

                case ConsoleKey.L:
                    return await this.SelectNewLine();

                default:
                    return KeyProcessingResults.Unhandled;
            }
        }

        async Task<KeyProcessingResults> ProcessOptionKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Q:
                    return await this.Quit();

                case ConsoleKey.L:
                    return this.DelegateProcessing(this.console.Clear);

                case ConsoleKey.S:
                    return await this.Save();

                default:
                    return KeyProcessingResults.Unhandled;
            }
        }

        KeyProcessingResults ProcessShiftKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Home:
                    return this.DelegateProcessing(this.document.MoveStartOfFile);
                case ConsoleKey.End:
                    return this.DelegateProcessing(this.document.MoveEndOfFile);

                default:
                    if (char.IsControl(keyInfo.KeyChar))
                    {
                        return KeyProcessingResults.Unhandled;
                    }
                    return this.InsertChar(keyInfo.KeyChar);
            }
        }

        async Task<KeyProcessingResults> ProcessSingleKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Escape:
                    return await this.OpenMenu();
                case ConsoleKey.Home:
                    return this.DelegateProcessing(this.document.MoveHome);
                case ConsoleKey.End:
                    return this.DelegateProcessing(this.document.MoveEnd);

                case ConsoleKey.PageUp:
                case ConsoleKey.PageDown:
                    return this.MovePage(keyInfo.Key);

                case ConsoleKey.UpArrow:
                case ConsoleKey.DownArrow:
                case ConsoleKey.LeftArrow:
                case ConsoleKey.RightArrow:
                    return this.MoveCursor(keyInfo.Key);

                case ConsoleKey.Enter:
                    return this.DelegateProcessing(() => this.document.InsertNewLine(this.setting));

                case ConsoleKey.Backspace:
                    return this.DelegateProcessing(() => this.document.BackSpace(this.setting));
                case ConsoleKey.Delete:
                    return this.DelegateProcessing(() => this.document.DeleteChar(this.setting));
                        
                default:
                    if (char.IsControl(keyInfo.KeyChar))
                    {
                        return KeyProcessingResults.Unhandled;
                    }
                    return this.InsertChar(keyInfo.KeyChar);
            }
        }

        KeyProcessingResults DelegateProcessing(Action action)
        {
            action();
            return KeyProcessingResults.Running;
        }

        async Task<KeyProcessingResults> Quit()
        {
            if (!this.document.IsModified) { return KeyProcessingResults.Quit; }

            var yes = new Choice("Yes", 'y');
            var no = new Choice("No", 'n');
            var confirm = 
                await Prompt(
                    new ChoosePromptInfo(
                        "File has unsaved changes. Quit without saving?",
                        new[] { yes, no }
                    )
                );
            if ((confirm ?? no) == yes)
            {
                return KeyProcessingResults.Quit;
            }
            else
            {
                return KeyProcessingResults.Running;
            }
        }

        async Task<KeyProcessingResults> SelectNewLine()
        {
            var selection = 
                await Prompt(
                    new ChoosePromptInfo(
                        "Change end of line sequence:",
                        NewLineFormat.All,
                        this.document.NewLineFormat
                    )
                );
            if (selection is NewLineFormat newLineFormat)
            {
                this.document.NewLineFormat = newLineFormat;
                return KeyProcessingResults.Running;
            }

            return KeyProcessingResults.Running;
        }

        async Task<KeyProcessingResults> Save()
        {
            try
            {
                var savePath = 
                    this.document.Path ?? await this.Prompt(new InputPromptInfo("Save as (Esc to cancel):"));
                if (savePath == null)
                {
                    this.AddMessage("Save is cancelled");
                    return KeyProcessingResults.Running;
                }
                this.document.Path = savePath;
                this.document.Save();
                this.AddMessage("File is saved");
            }
            catch (IOException ex)
            {
                this.AddMessage(ex.Message);
            }
            return KeyProcessingResults.Running;
        }

        KeyProcessingResults InsertChar(char c)
        {
            this.document.InsertChar(c, this.setting);
            return KeyProcessingResults.Running;
        }

        KeyProcessingResults MovePage(ConsoleKey key)
        {
            switch (key)
            {
                case ConsoleKey.PageUp:
                    this.document.MovePageUp(this.editArea.Height);
                    break;
                case ConsoleKey.PageDown:
                    this.document.MovePageDown(this.editArea.Height);
                    break;
            }
            return KeyProcessingResults.Running;
        }

        KeyProcessingResults MoveCursor(ConsoleKey key)
        {
            switch (key)
            {
                case ConsoleKey.LeftArrow:
                    this.document.MoveLeft();
                    break;
                case ConsoleKey.RightArrow:
                    this.document.MoveRight();
                    break;
                case ConsoleKey.UpArrow:
                    this.document.MoveUp();
                    break;
                case ConsoleKey.DownArrow:
                    this.document.MoveDown();
                    break;
            }
            return KeyProcessingResults.Running;
        }

        KeyProcessingResults SwitchAmbiguousWidth()
        {
            this.setting.IsFullWidthAmbiguous = !this.setting.IsFullWidthAmbiguous;
            var ambiguousSize =
                this.setting.IsFullWidthAmbiguous ? "Full Width"
                : "Half Width";
            this.AddMessage($"East Asian Width / Ambiguous = {ambiguousSize}");
            return KeyProcessingResults.Running;
        }
    }
}
