using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace txte
{
    class InputEvent
    {
        public InputEvent(InputEventArgs args)
        {
            this.Args = args;
            this.IsHandled = false;
        }
        public readonly InputEventArgs Args;
        public bool IsHandled;
    }
    class InputEventArgs
    {
        public InputEventArgs(EventType eventType, ConsoleKeyInfo consoleKeyInfo)
        {
            this.EventType = eventType;
            this.ConsoleKeyInfo = consoleKeyInfo;
        }

        public readonly EventType EventType;
        public readonly ConsoleKeyInfo ConsoleKeyInfo;

        public void Deconstruct(out EventType eventType, out ConsoleKeyInfo consoleKeyInfo)
        {
            eventType = this.EventType;
            consoleKeyInfo = this.ConsoleKeyInfo;
        }
    }

    enum EventType
    {
        Timeout,
        UserAction,
    }

    class EventQueue
    {
        readonly Queue<InputEvent> queue = new Queue<InputEvent>();

        public void PostEvent(InputEventArgs eventArgs)
        {
            lock (this.queue)
            {
                if (this.queue.Count != 0)
                {
                    if (eventArgs.EventType != EventType.Timeout)
                    {
                        return;
                    }
                }
                this.queue.Enqueue(new InputEvent(eventArgs));
            }
        }
        
        public async Task<InputEventArgs> RecieveReadKeyEventAsync()
        {
            while (true)
            {
                lock (this.queue)
                {
                    while(this.queue.TryPeek(out var closableEvent) && closableEvent.IsHandled)
                    {
                        this.queue.Dequeue();
                    }
                    if (this.queue.TryPeek(out var @event))
                    {
                        @event.IsHandled = true;
                        return @event.Args;
                    }
                }
                await Task.Delay(1);
            }
        }
    }
}
