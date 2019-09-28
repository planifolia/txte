using System;
using System.Threading;
using System.Threading.Tasks;

namespace txte
{

    class CoreConsoleKeyReader : IDisposable
    {
        public CoreConsoleKeyReader()
        {
            this.eventQueue = new EventQueue();
            this.cancellationController = new CancellationTokenSource();
            this.StartListen();
        }

        readonly EventQueue eventQueue;
        readonly CancellationTokenSource cancellationController;

        public async Task<InputEventArgs> ReadKeyOrTimeoutAsync() =>
            await this.eventQueue.RecieveReadKeyEventAsync();
        
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
            return Console.ReadKey(true);
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
                if (disposing)
                {
                    this.StopListen();
                }

                this.cancellationController.Dispose();

                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CoreConsoleKeyReader()
        {
            this.Dispose(false);
        }
        #endregion

    }

}
