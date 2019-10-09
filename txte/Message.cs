using System;

namespace txte
{
    class Message
    {
        static readonly TimeSpan expiration = TimeSpan.FromSeconds(5);

        public Message(string value, DateTime? createdTime = null)
        {
            this.Value = value;
            this.IsValid = true;
            this.createdTime = createdTime ?? DateTime.Now;
        }

        public string Value { get; }
        public bool IsValid { get; private set; }
        readonly DateTime createdTime;

        public void Expire()
        {
            this.IsValid = false;
        }

        public void CheckExpiration(DateTime now)
        {
            if (this.createdTime + Message.expiration < now)
            {
                this.Expire();
            }
        }
    }

    class TemporaryMessage : Message, IDisposable
    {
        public TemporaryMessage(string value, DateTime? createdTime = null) : base(value, createdTime) { }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.Expire();
                }

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