using System.Threading.Tasks;

namespace txte
{
    enum ProcessResult
    {
        Running,
        Quit,
        Unhandled,
    }
    static class ProcessTaskResult
    {
        public static readonly Task<ProcessResult> Running = 
            new ValueTask<ProcessResult>(ProcessResult.Running).AsTask();
        public static readonly Task<ProcessResult> Quit = 
            new ValueTask<ProcessResult>(ProcessResult.Quit).AsTask();
        public static readonly Task<ProcessResult> Unhandled = 
            new ValueTask<ProcessResult>(ProcessResult.Unhandled).AsTask();
    }

    enum ModalProcessResult
    {
        NeedsRefreash,
        Running,
        Ok,
        Cancel,
        Unhandled,
    }

    interface IModalProcessResult<T> { }
    class ModalNeedsRefreash<T>: IModalProcessResult<T>
    {
        public static ModalNeedsRefreash<T> Default = new ModalNeedsRefreash<T>();
    }
    class ModalRunning<T>: IModalProcessResult<T>
    {
        public static ModalRunning<T> Default = new ModalRunning<T>();
    }
    class ModalOk<T>: IModalProcessResult<T>
    {
        public ModalOk(T result) => this.Result = result;
        public readonly T Result;

        public void Deconstruct(out T result) => result = this.Result;
    }
    class ModalCancel<T>: IModalProcessResult<T>
    {
        public static ModalCancel<T> Default = new ModalCancel<T>();
    }
    
    class ModalUnhandled<T>: IModalProcessResult<T>
    {
        public static ModalUnhandled<T> Default = new ModalUnhandled<T>();
    }
}