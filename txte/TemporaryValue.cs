using System;

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
}