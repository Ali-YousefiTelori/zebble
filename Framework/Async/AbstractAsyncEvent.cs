namespace Zebble
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Olive;

    public interface IAsyncEvent { bool IsHandled(); }

    public abstract partial class AbstractAsyncEvent : IAsyncEvent, IDisposable
    {
        public TimeSpan? Timeout { get; set; }

        protected WeakReference<object> OwnerReference;
        protected string DeclaringFile, EventName;
        protected bool IsDisposing;

        internal ConcurrentList<AsyncEventHandler> handlers = new ConcurrentList<AsyncEventHandler>(2);

        protected AbstractAsyncEvent(string eventName, string declaringFile)
        {
            if (UIRuntime.IsDebuggerAttached)
            {
                EventName = eventName;
                DeclaringFile = declaringFile;
            }
        }

        public string GetName() => EventName;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConcurrentList<AsyncEventHandler> GetOrCreateHandlers()
        {
            if (handlers is null) handlers = new ConcurrentList<AsyncEventHandler>(2);
            return handlers;
        }

        public int HandlersCount => handlers?.Count ?? 0;

        public virtual bool HasDirectHandler() => Event != null;

        public void SetOwner(object owner) => OwnerReference = owner.GetWeakReference();

        public object Owner => OwnerReference.GetTargetOrDefault();

        [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IAsyncEventHandler FindHandler(Delegate handlerFunction)
        {
            if (handlers is null) return null;
            return handlers?.FirstOrDefault(x => handlerFunction == x.Handler) as IAsyncEventHandler;
        }

        [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool HasHandler(Delegate handlerFunction)
        {
            if (handlers is null) return false;
            return handlers?.Any(x => handlerFunction == x.Handler) == true;
        }

        [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsHandled()
        {
            if (HasDirectHandler()) return true;
            if (handlers is null) return false;
            return handlers?.Any() == true;
        }

        protected string DeclaringType => DeclaringFile.OrEmpty().Split(Path.DirectorySeparatorChar).LastOrDefault().TrimEnd(".cs", caseSensitive: false).Split('.').FirstOrDefault();

        public override string ToString()
        {
            var owner = Owner.ToStringOrEmpty();
            if (owner.IsEmpty() && Owner == null) owner = "{NULL}";

            return EventName + " of → " + owner;
        }

        [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveHandler<TActionFunction>(AsyncEventHandler handler)
        {
            if (handlers is null) return;

            lock (handlers)
                try { handlers?.Remove(handler); }
                catch (ArgumentOutOfRangeException)
                {
                    // No logging is needed.
                    // Why does it happen on a single thread?!
                }
        }

        /// <summary>Removes all current handlers from this event.</summary>
        [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void ClearHandlers()
        {
            Event = null;
            if (handlers is null) return;
            lock (handlers) handlers.Clear();
        }

        /// <summary>
        /// Returns a tasks that completes once as soon as this event is fired.
        /// </summary>
        [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task AwaitRaiseCompletion()
        {
            var completionTask = new TaskCompletionSource<bool>();

            void waiter()
            {
                completionTask.TrySetResult(result: true);
                this.RemoveActionHandler(waiter);
            }

            this.HandleWith(waiter);
            return completionTask.Task;
        }

        [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected TReturn DoHandleOn<TArg, TReturn>(BaseThread thread, Func<TArg, Task> handlerTask,
            Action<TArg> handlerAction, string callerFile, int line, int? index = null)
            where TReturn : AbstractAsyncEvent
        {
            if (handlerTask == null && handlerAction is null) return (TReturn)this;

            var caller = UIRuntime.IsDebuggerAttached ? $"{callerFile}:{line}" : string.Empty;
            var handlerIndex = index ?? HandlersCount;

            if (handlerTask != null && !HasHandler(handlerTask))
            {
                var handler = new AsyncEventTaskHandler<TArg>
                {
                    Action = handlerTask,
                    Event = this,
                    Thread = thread,
                    Caller = caller
                };
                var handlers = GetOrCreateHandlers();
                lock (handlers) handlers.Insert(handlerIndex, handler);
            }

            if (handlerAction != null && !HasHandler(handlerTask))
            {
                var handler = new AsyncEventActionHandler<TArg>
                {
                    Action = handlerAction,
                    Event = this,
                    Thread = thread,
                    Caller = caller
                };
                var handlers = GetOrCreateHandlers();
                lock (handlers)
                    handlers.Insert(handlerIndex, handler);
            }

            return (TReturn)this;
        }

        [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            IsDisposing = true;
            ClearHandlers();
        }
    }
}