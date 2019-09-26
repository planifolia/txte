using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace txte
{
    interface IConsole : IDisposable
    {
        int Height { get; }
        int Width { get; }
        Size Size { get; }

        Task<InputEventArgs> ReadKeyOrTimeoutAsync();
        void RefreshScreen(
            EditorSetting setting,
            Action<IScreen> drawEditorRows,
            Action<IScreen> drawStatusBar,
            Action<IScreen> drawMessageBar,
            Point cursor);
        void Clear();
    }

    interface IScreen
    {
        void AppendRow(string value);
        void AppendRow(IEnumerable<StyledString> spans);
        void AppendOuterRow(string value);
    }


    abstract class ConsoleBase : IConsole
    {
        public ConsoleBase()
        {
            this.eventQueue = new EventQueue();
            this.cancellationController = new CancellationTokenSource();
            this.StartListen();
        }

        readonly EventQueue eventQueue;
        readonly CancellationTokenSource cancellationController;

        public abstract int Height { get; }
        public abstract int Width { get; }
        public abstract Size Size { get; }

        public async Task<InputEventArgs> ReadKeyOrTimeoutAsync() =>
            await this.eventQueue.RecieveReadKeyEventAsync();

        public abstract void RefreshScreen(
            EditorSetting setting,
            Action<IScreen> drawEditorRows,
            Action<IScreen> drawStatusBar,
            Action<IScreen> drawMessageBar,
            Point cursor);
        public abstract void Clear();

        protected abstract void ResetColor();
        protected abstract ConsoleKeyInfo ReadKey();
        
        void StartListen()
        {
            var userInput = GenerateEventAsync(
                EventType.UserAction,
                this.ReadKeyAsync,
                this.cancellationController.Token
            );
            var timeout = GenerateEventAsync(
                EventType.Timeout,
                this.TimeOutAsync,
                this.cancellationController.Token
            );
        }

        async Task<ConsoleKeyInfo> TimeOutAsync()
        {
            await Task.Delay(1000);
            return default;
        }
        async Task<ConsoleKeyInfo> ReadKeyAsync()
        {
            await Task.Yield();
            return this.ReadKey();
        }

        async Task GenerateEventAsync(
            EventType eventType,
            Func<Task<ConsoleKeyInfo>> inputGenerator,
            CancellationToken cancellationToken
        )
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var keyInfo = await inputGenerator();
                if (cancellationToken.IsCancellationRequested) { return; }
                this.eventQueue.PostEvent(new InputEventArgs(eventType, keyInfo));
            }
        }

        void StopListen()
        {
            this.cancellationController.Cancel();
        }

        #region IDisposable Support
        bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                this.StopListen();
                this.ResetColor();
                this.Clear();

                if (disposing)
                {
                    this.cancellationController.Dispose();
                }
                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ConsoleBase()
        {
            this.Dispose(false);
        }
        #endregion

    }

}
