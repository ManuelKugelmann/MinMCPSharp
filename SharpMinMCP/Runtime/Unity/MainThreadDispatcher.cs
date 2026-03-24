#if UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SharpMinMCP
{
    /// <summary>
    /// Dispatches work to Unity's main thread via Update().
    /// Attach to a GameObject or let McpServerBehaviour create one.
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[SharpMinMCP-Dispatcher]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<MainThreadDispatcher>();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Queue an action to run on the main thread. Fire-and-forget.
        /// </summary>
        public static void Enqueue(Action action)
        {
            _queue.Enqueue(action);
        }

        /// <summary>
        /// Queue a Func, block caller until it completes on main thread.
        /// Use from background threads that need Unity API results.
        /// </summary>
        public static Task<T> EnqueueAsync<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
            _queue.Enqueue(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        /// <summary>
        /// Matches Func&lt;Func&lt;Task&lt;string&gt;&gt;, Task&lt;string&gt;&gt; for McpServer.DispatchWrapper.
        /// </summary>
        public static Task<string> EnqueueAsync(Func<Task<string>> func)
        {
            var tcs = new TaskCompletionSource<string>();
            _queue.Enqueue(async () =>
            {
                try
                {
                    var result = await func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        private void Update()
        {
            while (_queue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
#endif
