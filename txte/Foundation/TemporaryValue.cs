using System;

namespace txte
{
    class Temporary<T>
    {
        public Temporary()
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

        public struct DisposingToken : IDisposable
        {
            public DisposingToken(Temporary<T> source)
            {
                this.source = source;
            }

            readonly Temporary<T> source;

            public void Dispose()
            {
                this.source.HasValue = false;
                this.source.value = default!;
            }
        }
    }

    class RestorableValue<T> where T : struct
    {
        public RestorableValue()
        {
            this.Value = default!;
        }

        public T Value { get; set; }

        public MementoToken SaveValue() => new MementoToken(this);

        public struct MementoToken : IDisposable
        {
            public MementoToken(RestorableValue<T> source)
            {
                this.source = source;
                this.savedValue = source.Value;
            }

            readonly RestorableValue<T> source;
            readonly T savedValue;

            public void Restore()
            {
                this.source.Value = this.savedValue;
            }

            void IDisposable.Dispose()
            {
                this.Restore();
            }
        }
    }
}