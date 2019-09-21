using System;
using System.Collections.Generic;
using System.Text;

namespace txte
{
    class InputEventArgs
    {
        public InputEventArgs(EventType eventType, ConsoleKeyInfo consoleKeyInfo)
        {
            this.EventType = eventType;
            this.ConsoleKeyInfo = consoleKeyInfo;
        }

        public EventType EventType;
        public ConsoleKeyInfo ConsoleKeyInfo;
    }

    enum EventType
    {
        Timeout,
        UserAction,
    }

    class EventQueue
    {
        public EventQueue(Func<InputEventArgs, EditorProcessingResults> dispachedMethod)
        {
            this.dispachedMethod = dispachedMethod;
        }

        readonly Func<InputEventArgs, EditorProcessingResults> dispachedMethod;

        readonly Queue<InputEventArgs> queue = new Queue<InputEventArgs>();

        public EditorProcessingResults PostEvent(InputEventArgs eventArgs)
        {
            lock (this.queue)
            {
                if (this.queue.Count != 0)
                {
                    if (eventArgs.EventType != EventType.Timeout)
                    {
                        this.queue.Enqueue(eventArgs);
                    }
                    return EditorProcessingResults.Queued;
                }
                this.queue.Enqueue(eventArgs);

            }
            while (true)
            {
                var result = this.dispachedMethod(eventArgs);
                if (result == EditorProcessingResults.Quit)
                {
                    return result;
                }
                lock (this.queue)
                {
                    this.queue.Dequeue();
                    if (this.queue.Count == 0)
                    {
                        return result;
                    }
                }
            }

        }
    }
}
