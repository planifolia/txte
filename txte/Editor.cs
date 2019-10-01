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

    class ChoosePromptInfo : IPrompt
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
            styled.Add(new StyledString(this.message, ColorSet.PromptMessage));
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

    class InputPromptInfo : IPrompt
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
            styled.Add(new StyledString(this.Message, ColorSet.PromptMessage));
            styled.Add(new StyledString(" "));
            styled.Add(new StyledString(this.Current));
            return styled;
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
        }

        readonly IConsole console;
        readonly List<TemporaryMessage> messages = new List<TemporaryMessage>();

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
            this.DrawPromptBar(screen);
            this.DrawSatausBar(screen);
            this.DrawMessageBar(screen);
        }

        void DrawEditorRows(IScreen screen, int from)
        {
            bool ambiguousSetting = this.setting.IsFullWidthAmbiguous;
            for (int y = from; y < this.editArea.Height; y++)
            {
                var docRow = y + this.document.Offset.Y;
                if (docRow < this.document.Rows.Count)
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

            screen.AppendRow(new[] { new StyledString(fileInfo + new string(' ', padding) + positionInfo, ColorSet.Reversed) });
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

        async Task<IChoice?> ChoicePrompt(
            string prompt,
            IReadOnlyList<IChoice> choices,
            IChoice? default_choice = null
        )
        {
            var choicePrompt = new ChoosePromptInfo(prompt, choices, default_choice);
            using var temporary = this.prompt.SetTemporary(choicePrompt);
            
            this.RefreshScreen(0);
            while (true)
            {
                (var eventType, var keyInfo) = await this.console.ReadKeyOrTimeoutAsync();
                if (eventType == EventType.Timeout) { continue; }
                (var state, var choosen) = choicePrompt.ProcessKey(keyInfo);
                if (state == KeyProcessingResults.Quit) { return choosen; }
                this.RefreshScreen(this.editArea.Height);
            }
        }

        async Task<string?> InputPrompt(string prompt)
        {
            var inputPrompt = new InputPromptInfo(prompt);
            using var temporary = this.prompt.SetTemporary(inputPrompt);
            this.RefreshScreen(0);
            while (true)
            {
                (var eventType, var keyInfo) = await this.console.ReadKeyOrTimeoutAsync();
                if (eventType == EventType.Timeout) { continue; }
                (var state, var input) = inputPrompt.ProcessKey(keyInfo);
                if (state == KeyProcessingResults.Quit) { return input; }
                this.RefreshScreen(this.editArea.Height);
            }
        }

        async Task<KeyProcessingResults> ProcessKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Modifiers)
            {
                case ConsoleModifiers.Control | ConsoleModifiers.Alt | ConsoleModifiers.Shift:
                case ConsoleModifiers.Control | ConsoleModifiers.Shift:
                case ConsoleModifiers.Alt | ConsoleModifiers.Shift:
                    return await this.ProcessOptionShiftKeyPress(keyInfo);
                case ConsoleModifiers.Control | ConsoleModifiers.Alt:
                case ConsoleModifiers.Control:
                case ConsoleModifiers.Alt:
                    return await this.ProcessOptionKeyPress(keyInfo);
                case ConsoleModifiers.Shift:
                    return this.ProcessShiftKeyPress(keyInfo);
                default:
                    return this.ProcessSingleKeyPress(keyInfo);
            }
        }

        async Task<KeyProcessingResults> ProcessOptionShiftKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.E:
                    return this.SwitchAmbiguousWidth();

                case ConsoleKey.N:
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

        KeyProcessingResults ProcessSingleKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
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
                await ChoicePrompt(
                    "File has unsaved changes. Quit without saving file?",
                    new[] { yes, no }
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
                await ChoicePrompt(
                    "Change dnd of line sequence:",
                    NewLineFormat.All,
                    this.document.NewLineFormat
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
                var savePath = this.document.Path ?? await this.InputPrompt("Save as (Esc to cancel):");
                if (savePath == null)
                {
                    this.AddMessage("Save cancelled");
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
