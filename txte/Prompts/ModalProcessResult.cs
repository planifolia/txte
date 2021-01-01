namespace txte.State
{
    abstract class ModalProcessResult<T>
    {
        public static implicit operator ModalProcessResult<T>(ModalNeedsRefreash untyped) =>
            ModalNeedsRefreash<T>.Default;
        public static implicit operator ModalProcessResult<T>(ModalRunning untyped) =>
            ModalRunning<T>.Default;
        public static implicit operator ModalProcessResult<T>(ModalCancel untyped) =>
            ModalCancel<T>.Default;
        public static implicit operator ModalProcessResult<T>(ModalUnhandled untyped) =>
            ModalUnhandled<T>.Default;
    }

    interface IModalOk { }
    class ModalOk<T> : ModalProcessResult<T>, IModalOk
    {
        public ModalOk(T result) => this.Result = result;
        public readonly T Result;

        public void Deconstruct(out T result) => result = this.Result;
    }
    static class ModalOk
    {
        public static ModalOk<T> Create<T>(T result) => new ModalOk<T>(result);
    }

    interface IModalNeedsRefreash { }
    class ModalNeedsRefreash<T> : ModalProcessResult<T>, IModalNeedsRefreash
    {
        public static ModalNeedsRefreash<T> Default = new ModalNeedsRefreash<T>();
    }
    class ModalNeedsRefreash
    {
        public static ModalNeedsRefreash Default = new ModalNeedsRefreash();
    }

    interface IModalRunning { }
    class ModalRunning<T> : ModalProcessResult<T>, IModalRunning
    {
        public static ModalRunning<T> Default = new ModalRunning<T>();
    }
    class ModalRunning
    {
        public static ModalRunning Default = new ModalRunning();
    }

    interface IModalCancel { }
    class ModalCancel<T> : ModalProcessResult<T>, IModalCancel
    {
        public static ModalCancel<T> Default = new ModalCancel<T>();
    }
    class ModalCancel
    {
        public static ModalCancel Default = new ModalCancel();
    }

    interface IModalUnhandled { }
    class ModalUnhandled<T> : ModalProcessResult<T>, IModalUnhandled
    {
        public static ModalUnhandled<T> Default = new ModalUnhandled<T>();
    }
    class ModalUnhandled
    {
        public static ModalUnhandled Default = new ModalUnhandled();
    }
}