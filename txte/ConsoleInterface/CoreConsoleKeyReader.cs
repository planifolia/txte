using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace txte.ConsoleInterface
{

    class CoreConsoleKeyReader : IDisposable
    {
        public CoreConsoleKeyReader(int timeoutMillisec)
        {
            this.timeoutMillisec = timeoutMillisec;
            this.isAlive = true;
        }

        readonly int timeoutMillisec;
        bool isAlive;

        public async IAsyncEnumerable<InputEventArgs> ReadKeysOrTimeoutAsync()
        {
            while (this.isAlive)
            {
                var timeout = DateTime.Now + TimeSpan.FromMilliseconds(timeoutMillisec);
                while (true)
                {
                    if (Console.KeyAvailable) yield return
                        new InputEventArgs(EventType.UserAction, Console.ReadKey(true));
                    if (DateTime.Now >= timeout) break;
                    await Task.Delay(10);
                }
                yield return new InputEventArgs(EventType.Timeout, default);
            }
            yield break;
        }

        #region IDisposable Support
        bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing) { }

                this.isAlive = false;

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
