﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using txte.Settings;
using txte.State;
using txte.Input;
using txte.Text;
using txte.ConsoleInterface;
using txte.TextDocument;
using txte.Prompts;
using txte.SyntaxHighlight;

namespace txte.TextEditor
{
    class Editor
    {
        public static string Version =
            Assembly.GetEntryAssembly()!
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion
            .ToString();


        public Editor(IConsole console, Setting setting)
        {
            this.console = console;
            this.setting = setting;
            this.message = new Message(new ColoredString(this.setting, ""));
            this.document = new Document(this.setting);
            this.menu = new Menu(this.SetupShortcuts());
            this.editKeyBinds = this.SetupEditKeyBindings();
        }

        readonly IConsole console;
        readonly Setting setting;
        readonly Menu menu;
        readonly KeyBindSet editKeyBinds;
        readonly Temporary<IPrompt> prompt = new Temporary<IPrompt>();

        Document document;
        Message message;

        Size editArea =>
            new Size(
                this.console.Size.Width,
                this.console.Size.Height
                - (this.prompt.HasValue ? 1 : 0)
                - (this.message.IsValid ? 1 : 0)
                - 1
            );

        public async Task RunAsync()
        {
            this.RefreshScreen(this.document.UpdateCursor(this.editArea));
            await foreach (var (eventType, keyInfo) in this.console.ReadKeysOrTimeoutAsync())
            {
                if (eventType == EventType.Timeout)
                {
                    this.FadeMessage(this.document.UpdateCursor(this.editArea));
                    continue;
                }
                switch (await this.ProcessKeyPressAsync(keyInfo))
                {
                    case ProcessResult.Quit:
                        return;
                    default:
                        break;
                }
                if (this.console.KeyAvailable)
                {
                    continue; //skip if muitiple chars are imput by IME, etc.
                }
                this.RefreshScreen(this.document.UpdateCursor(this.editArea));
            }
        }

        public async Task OpenDocumentAsync(string path)
        {
            if (!this.document.IsBlank) throw
                new InvalidOperationException("Document has already opened and other document is opened");

            try
            {
                this.document = await Document.OpenAsync(path, this.setting);
            }
            catch (Exception)
            {
                this.document = new Document(setting) { Path = path };
            }
        }

        public void ShowMessage(Message message) => this.message = message;

        KeyBindSet SetupShortcuts() =>
            new KeyBindSet
            {
                [new KeyBind(new KeyCombination(ConsoleKey.Q, false, true), "Quit")] =
                    this.Quit,

                [new KeyBind(new KeyCombination(ConsoleKey.O, false, true), "Open")] =
                    this.Open,
                [new KeyBind(new KeyCombination(ConsoleKey.S, false, true), "Save")] =
                    this.Save,
                [new KeyBind(new KeyCombination(ConsoleKey.S, true, true), "Save as")] =
                    this.SaveAs,
                [new KeyBind(new KeyCombination(ConsoleKey.W, false, true), "Close")] =
                    this.Close,
                [new KeyBind(new KeyCombination(ConsoleKey.F, false, true), "Search")] =
                    this.Find,
                [new KeyBind(new KeyCombination(ConsoleKey.G, false, true), "Go to line")] =
                    this.GoToLine,

                [new KeyBind(new KeyCombination(ConsoleKey.Home, false, true), "Move to start of file")] =
                    () => this.DelegateTask(this.document.MoveStartOfFile),
                [new KeyBind(new KeyCombination(ConsoleKey.End, false, true), "Move to end of file")] =
                    () => this.DelegateTask(this.document.MoveEndOfFile),

                [new KeyBind(new KeyCombination(ConsoleKey.L, false, true), "Refresh")] =
                    () => this.DelegateTask(this.console.Clear),
                [new KeyBind(new KeyCombination(ConsoleKey.L, true, true), "Change End of Line sequence")] =
                    this.ChangeEndOfLine,
                [new KeyBind(new KeyCombination(ConsoleKey.E, true, true), "Change East Asian Width")] =
                    this.SwitchAmbiguousWidth,
            };

        KeyBindSet SetupEditKeyBindings() =>
            new KeyBindSet
            {
                [new KeyBind(new KeyCombination(ConsoleKey.Home, false, false), "Move to start of line")] =
                    () => this.DelegateTask(this.document.MoveHome),
                [new KeyBind(new KeyCombination(ConsoleKey.End, false, false), "Move to end of line")] =
                    () => this.DelegateTask(this.document.MoveEnd),

                [new KeyBind(new KeyCombination(ConsoleKey.PageUp, false, false), "Scroll to previous page")] =
                    () => this.DelegateTask(() => this.document.MovePageUp(this.editArea.Height)),
                [new KeyBind(new KeyCombination(ConsoleKey.PageDown, false, false), "Scroll to next page")] =
                    () => this.DelegateTask(() => this.document.MovePageDown(this.editArea.Height)),

                [new KeyBind(new KeyCombination(ConsoleKey.UpArrow, false, true), "Move cursor up quarter page")] =
                    () => this.DelegateTask(() => this.document.MoveUp(this.editArea.Height / 4)),
                [new KeyBind(new KeyCombination(ConsoleKey.DownArrow, false, true), "Move cursor down quarter page")] =
                    () => this.DelegateTask(() => this.document.MoveDown(this.editArea.Height / 4)),

                [new KeyBind(new KeyCombination(ConsoleKey.UpArrow, false, false), "Move cursor up")] =
                    () => this.DelegateTask(this.document.MoveUp),
                [new KeyBind(new KeyCombination(ConsoleKey.DownArrow, false, false), "Move cursor down")] =
                    () => this.DelegateTask(this.document.MoveDown),
                [new KeyBind(new KeyCombination(ConsoleKey.LeftArrow, false, false), "Move cursor left")] =
                    () => this.DelegateTask(this.document.MoveLeft),
                [new KeyBind(new KeyCombination(ConsoleKey.RightArrow, false, false), "Move cursor right")] =
                    () => this.DelegateTask(this.document.MoveRight),

                [new KeyBind(new KeyCombination(ConsoleKey.Enter, false, false), "Break line")] =
                    () => this.DelegateTask(this.document.InsertNewLine),

                [new KeyBind(new KeyCombination(ConsoleKey.Backspace, false, false), "Delete a left letter")] =
                    () => this.DelegateTask(this.document.BackSpace),
                [new KeyBind(new KeyCombination(ConsoleKey.Delete, false, false), "Delete a right letter")] =
                    () => this.DelegateTask(this.document.DeleteChar),

                [new KeyBind(new KeyCombination(ConsoleKey.Tab, false, false), "Insert tab letter")] =
                    () => this.DelegateTask(() => this.document.InsertChar('\t')),
            };

        Task<ProcessResult> DelegateTask(Action action)
        {
            action();
            return ProcessTaskResult.Running;
        }

        void FadeMessage(CursorPosition? cursor)
        {
            var beforeFading = this.editArea.Height;
            this.message.CheckExpiration(DateTime.Now);
            if (this.editArea.Height != beforeFading)
            {
                this.RefreshScreen(cursor, beforeFading);
            }
        }

        void RefreshScreen(CursorPosition? cursor) => this.RefreshScreen(cursor, 0);

        void RefreshScreen(CursorPosition? cursor, int from)
        {
            this.console.RefreshScreen(
                from.AtMin(0),
                this.setting,
                this.RenderScreen,
                cursor);
        }

        void RenderScreen(IScreen screen, int from)
        {
            this.DrawEditorLines(screen, from);
            this.DrawMessageBar(screen);
            this.DrawSatausBar(screen);
            this.DrawPromptBar(screen);
        }

        void DrawEditorLines(IScreen screen, int from)
        {
            for (int line = from; line < this.editArea.Height; line++)
            {
                if (this.menu.IsShown)
                {
                    this.DrawMenu(screen, line);
                }
                else if (this.document.IsBlank)
                {
                    this.DrawWelcome(screen, line);
                }
                else
                {
                    this.document.DrawLine(screen, line + this.document.Offset.Line);
                }
            }
        }

        void DrawDocumentLine(IScreen screen, int docLine)
        {
            this.document.DrawLine(screen, docLine);
        }

        void DrawMenu(IScreen screen, int line)
        {
            var keyJoiner = " + ";
            var separator = "   ";
            var titleLineCount = 2;

            if (line - titleLineCount >= this.menu.KeyBinds.KeyBinds.Count)
            {
                screen.AppendLine(new[]
                {
                    new StyledString("~", ColorSet.OutOfBounds),
                });
            }
            else if (line == 0)
            {
                var message = "     Menu / Shortcuts";
                var messageLength = message.Length;
                var leftPadding = (this.console.Width - messageLength) / 2 - 1;
                screen.AppendLine(new[]
                {
                    new StyledString("~", ColorSet.OutOfBounds),
                    new StyledString(new string(' ', leftPadding), ColorSet.OutOfBounds),
                    new StyledString(message, ColorSet.OutOfBounds),
                });
            }
            else if (line == 1)
            {
                screen.AppendLine(new[]
                {
                    new StyledString("~", ColorSet.OutOfBounds),
                });
                return;
            }
            else
            {
                var item = this.menu.KeyBinds.KeyBinds[line - titleLineCount];

                var leftLength = item.explanation.Length;
                var rightLength = item.keys.Select(x => x.Length).Sum() + (item.keys.Length - 1) * keyJoiner.Length;
                var leftPadding = (this.console.Width - separator.Length) / 2 - leftLength - 1;
                var spans = new List<StyledString>();
                spans.Add(new StyledString("~", ColorSet.OutOfBounds));
                spans.Add(new StyledString(new string(' ', leftPadding), ColorSet.OutOfBounds));
                spans.Add(new StyledString(item.explanation, ColorSet.OutOfBounds));
                spans.Add(new StyledString(separator, ColorSet.OutOfBounds));
                int i = 0;
                foreach (var key in item.keys)
                {
                    if (i != 0)
                    {
                        spans.Add(new StyledString(keyJoiner, ColorSet.OutOfBounds));
                    }
                    spans.Add(new StyledString(key, ColorSet.KeyExpression));
                    i++;
                }
                screen.AppendLine(spans);
            }
        }

        void DrawWelcome(IScreen screen, int line)
        {
            if (line == this.editArea.Height / 3)
            {
                var editorWidth = this.console.Width - this.document.LineNumberWidth - 1;
                var welcome = $"txte -- version {Version}";
                var welcomeLength = welcome.Length.AtMax(editorWidth);
                var padding = (editorWidth - welcomeLength) / 2;
                var displayLine = new string(' ', padding) + welcome.Substring(0, welcomeLength);
                screen.AppendLine(new[] {
                    new StyledString(new string(' ', this.document.LineNumberWidth) + "|", ColorSet.LineNumber),
                    new StyledString(displayLine, ColorSet.OutOfBounds),
                });
            }
            else
            {
                screen.AppendLine(new[] { new StyledString(new string(' ', this.document.LineNumberWidth) + "|", ColorSet.LineNumber) });
            }
        }
        void DrawPromptBar(IScreen screen)
        {
            if (this.prompt.HasValue)
            {
                screen.AppendLine(this.prompt.Value.ToStyledString());
            }
        }
        void DrawSatausBar(IScreen screen)
        {
            var fileName = (this.document.Path != null ? Path.GetFileName(this.document.Path) : "");
            var fileNameLength = fileName.Length.AtMax(20);
            (var clippedFileName, _, _) =
                fileName.SubRenderString(0, fileNameLength, this.setting.AmbiguousCharIsFullWidth);
            var fileInfo =$"{clippedFileName}{(this.document.IsNew ? "[New File]" : "")}{(this.document.IsModified ? "(*)" : "")}";
            var positionInfo =
                $"{this.document.RenderPosition.Line + 1}:{this.document.RenderPosition.Column + 1}"
                + $"  {this.document.NewLineFormat.Name}  {this.document.LanguageHighLighter.Language}";
            var padding = this.console.Width - fileInfo.Length - positionInfo.Length;

            screen.AppendLine(new[] { new StyledString(fileInfo + new string(' ', padding) + positionInfo, ColorSet.SystemMessage) });
        }
        void DrawMessageBar(IScreen screen)
        {
            if (!this.message.IsValid) return;

            StyledString left;
            int displayWidth;
            if (this.menu.IsShown)
            {
                left = new StyledString("~", ColorSet.OutOfBounds);
                displayWidth = this.console.Width - 1;
            }
            else
            {
                left = new StyledString(new string(' ', this.document.LineNumberWidth) + "|", ColorSet.LineNumber);
                displayWidth = this.console.Width - this.document.LineNumberWidth - 1;
            }

            var text = this.message.Value;
            var textLength = text.GetRenderLength();
            var displayLength = textLength.AtMax(displayWidth);
            var render = text.SubRenderString(textLength - displayLength, displayLength);
            var prefix =
                new[]
                {
                    left,
                    new StyledString(new string(' ', displayWidth - displayLength)),
                };
            screen.AppendLine(prefix.Concat(render.ToStyledStrings()));
        }

        async Task<ModalProcessResult<TResult>> Prompt<TResult>(IPrompt<TResult> prompt)
        {
            using (this.prompt.SetTemporary(prompt))
            {
                this.document.UpdateOffset(this.editArea);
                this.RefreshScreen(prompt.Cursor.OffsetPrompt(this.console.Size.Height - 1));
                await foreach (var (eventType, keyInfo) in this.console.ReadKeysOrTimeoutAsync())
                {
                    if (eventType == EventType.Timeout)
                    {
                        this.FadeMessage(prompt.Cursor.OffsetPrompt(this.console.Size.Height - 1));
                        continue;
                    }

                    var state = prompt.ProcessKey(keyInfo);
                    if (state is IModalOk || state is IModalCancel)
                    {
                        return state;
                    }

                    if (state is IModalNeedsRefreash)
                    {
                        this.document.UpdateOffset(this.editArea);
                        this.RefreshScreen(prompt.Cursor.OffsetPrompt(this.console.Size.Height - 1));
                    }
                    else
                    {
                        this.document.UpdateOffset(this.editArea);
                        this.RefreshScreen(prompt.Cursor.OffsetPrompt(this.console.Size.Height - 1), this.editArea.Height);
                    }
                }
            }

            // Maybe Console is dead.
            return ModalCancel.Default;
        }


        private async Task<ProcessResult> OpenMenu()
        {
            Func<Task<ProcessResult>>? followingCommand = null;
            using (this.menu.ShowWhileModal())
            {
                using var message =
                    new TemporaryMessage(ColoredString.Concat(this.setting,
                        ("hint: Menu commands can perform on ", ColorSet.OutOfBounds),
                        ("Crtl", ColorSet.KeyExpression),
                        (" + keys or ", ColorSet.OutOfBounds),
                        ("Esc", ColorSet.KeyExpression),
                        (" -> keys", ColorSet.OutOfBounds)
                    ));

                this.message = message;

                this.RefreshScreen(null);

                await foreach (var (eventType, keyInfo) in this.console.ReadKeysOrTimeoutAsync())
                {
                    if (eventType == EventType.Timeout)
                    {
                        this.FadeMessage(null);
                        continue;
                    }

                    if (keyInfo.Key == ConsoleKey.Escape) return ProcessResult.Running;

                    if (this.menu.KeyBinds[keyInfo.ToKeyCombination().WithControl()] is { } function)
                    {
                        followingCommand = function;
                        break;
                    }

                    this.RefreshScreen(null);
                }
            }

            return await (
                followingCommand?.Invoke()
                ?? Task.FromResult(ProcessResult.Quit) // Maybe Console is dead.
            );
        }

        async Task<ProcessResult> ProcessKeyPressAsync(ConsoleKeyInfo keyInfo)
        {
            if (keyInfo is { Key: ConsoleKey.Escape, Modifiers: 0 })
            {
                return await this.OpenMenu();
            }

            var KeyCombination = keyInfo.ToKeyCombination();
            if (this.menu.KeyBinds[KeyCombination] is { } shortcut)
            {
                return await shortcut();
            }
            if (this.editKeyBinds[KeyCombination] is { } editKey)
            {
                return await editKey();
            }

            var keyChar = keyInfo.KeyChar;
            if (!char.IsControl(keyChar))
            {
                this.document.InsertChar(keyChar);
                return ProcessResult.Running;
            }

            return ProcessResult.Unhandled;
        }

        async Task<ProcessResult> Quit()
        {
            if (!this.document.IsModified) return ProcessResult.Quit;

            var promptResult =
                await this.Prompt(
                    ChoosePrompt.Create(
                        "File has unsaved changes. Quit without saving?",
                        new[] { Choice.No, Choice.Yes }
                    )
                );

            if (promptResult is ModalOk<Choice>(var confirm) && confirm == Choice.Yes)
            {
                return ProcessResult.Quit;
            }
            else
            {
                return ProcessResult.Running;
            }
        }

        async Task<ProcessResult> Open()
        {
            // tab mode is not supported
            if (!this.document.IsBlank) return ProcessResult.Running;

            var promptInput = await this.Prompt(new InputPrompt("Open:", this.setting));
            if (promptInput is ModalOk<string>(var path))
            {
                await this.OpenDocumentAsync(path);
            }

            return ProcessResult.Running;
        }


        async Task<ProcessResult> Save() =>
            await ((this.document.Path == null) ? this.SaveAs() : this.SaveWithConfirm());

        async Task<ProcessResult> SaveAs()
        {
            var promptInput = await this.Prompt(new InputPrompt("Save as:", this.setting));
            if (promptInput is ModalOk<string>(var path))
            {
                this.document.Path = path;
                await this.SaveWithConfirm();
            }

            return ProcessResult.Running;
        }

        async Task<ProcessResult> SaveWithConfirm()
        {
            try
            {
                if (File.Exists(this.document.Path))
                {
                    var promptResult =
                        await this.Prompt(
                            ChoosePrompt.Create(
                                "Override?",
                                new[] { Choice.No, Choice.Yes }
                            )
                        );
                    if (!(promptResult is ModalOk<Choice>(var confirm) && confirm == Choice.Yes))
                    {
                        return ProcessResult.Running;
                    }
                }
                this.document.Save();
                this.message =
                    new Message(ColoredString.Concat(this.setting,
                        ("File is saved", ColorSet.OutOfBounds)));
            }
            catch (IOException ex)
            {
                this.message =
                    new Message(ColoredString.Concat(this.setting,
                        (ex.Message, ColorSet.Default)));
            }

            return ProcessResult.Running;
        }

        async Task<ProcessResult> Close()
        {
            if (!this.document.IsModified)
            {
                this.document = new Document(this.setting);
                return ProcessResult.Running;
            }

            var promptResult =
                await this.Prompt(
                    ChoosePrompt.Create(
                        "File has unsaved changes. Close without saving?",
                        new[] { Choice.No, Choice.Yes }
                    )
                );

            if (promptResult is ModalOk<Choice>(var confirm) && confirm == Choice.Yes)
            {
                this.document = new Document(this.setting);
            }

            return ProcessResult.Running;
        }

        async Task<ProcessResult> Find()
        {
            if (this.document.IsBlank) return ProcessResult.Running;

            using var message =
                new TemporaryMessage(ColoredString.Concat(this.setting,
                    ("hint: ", ColorSet.OutOfBounds),
                    ("(", ColorSet.OutOfBounds),
                    ("Shift", ColorSet.KeyExpression),
                    (" +) ", ColorSet.OutOfBounds),
                    ("Tab", ColorSet.KeyExpression),
                    (" to explor", ColorSet.OutOfBounds)
                ));
            this.message = message;

            var finder = new TextFinder(this.document.Lines);

            using (this.document.OverlapHilighter.SetTemporary(new FindingHilighter(finder)))
            {
                await this.Prompt(new FindPrompt("Search:", finder, this.document, this.setting));
            }

            return ProcessResult.Running;
        }

        async Task<ProcessResult> GoToLine()
        {
            if (this.document.IsBlank) return ProcessResult.Running;

            var state = new GoToLineState();

            using (this.document.OverlapHilighter.SetTemporary(new GoToLineHilighter(state)))
            {
                await this.Prompt(new GoToLinePrompt("Go to:", state, this.document, this.setting));
            }

            return ProcessResult.Running;
        }

        async Task<ProcessResult> ChangeEndOfLine()
        {
            var promptResult =
                await this.Prompt(
                    ChoosePrompt.Create(
                        "Change End of Line sequence:",
                        EndOfLineFormat.All,
                        this.document.NewLineFormat
                    )
                );
            if (promptResult is ModalOk<EndOfLineFormat>(var selection))
            {
                this.document.NewLineFormat = selection;
            }

            return ProcessResult.Running;
        }

        async Task<ProcessResult> SwitchAmbiguousWidth()
        {
            var estimatedWidth = (this.console.ShowsAmbiguousCharAsFullWidth) ? "Full-Width" : "Half-Width";
            using var message =
                new TemporaryMessage(ColoredString.Concat(this.setting,
                    (
                        $"hint: Set according to your terminal font. Default is estimated to {estimatedWidth}",
                        ColorSet.OutOfBounds
                    )
                ));
            this.message = message;
            var modalResult =
                await this.Prompt(
                    ChoosePrompt.Create(
                        "Change East Asian Width - Ambiguous:",
                        EAWAmbiguousFormat.All,
                        this.setting.AmbiguousFormat
                    )
                );
            if (!(modalResult is ModalOk<EAWAmbiguousFormat>(var selection))) return ProcessResult.Running;
            
            this.setting.SetAmbiguousCharWidthFormat(this.console, selection);
            if (selection.IsFullWidth.HasValue)
            {
                this.message =
                    new Message(ColoredString.Concat(this.setting,
                        ($"East Asian Width - Ambiguous = {selection}", ColorSet.OutOfBounds)));
            }
            else
            {
                this.message =
                    new Message(ColoredString.Concat(this.setting,
                        (
                            $"East Asian Width - Ambiguous = Default "
                            + $"(Estimated to {estimatedWidth})",
                            ColorSet.OutOfBounds
                        )
                    ));
            }

            return ProcessResult.Running;
        }
    }
}
