using System.Runtime.InteropServices;

namespace UnifiedMessenger.Tests.WebView2;

/// <summary>
/// WebView2/COM initialization requires a single-threaded apartment (STA) with a Win32 message pump.
/// xUnit test threads are MTA by default, so adapter integration tests marshal here.
/// </summary>
internal static class StaThreadRunner
{
    private const uint PM_REMOVE = 0x0001;
    private static readonly Thread StaThread = CreateStaThread();
    private static readonly AutoResetEvent WorkAvailable = new(false);
    private static readonly object Gate = new();
    private static Action? _syncWork;
    private static Func<Task>? _asyncWork;
    private static Exception? _error;

    public static Task RunAsync(Action work)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try
            {
                work();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        return completion.Task;
    }

    public static T Run<T>(Func<Task<T>> work)
    {
        T? result = default;
        Exception? error = null;
        Post(() =>
        {
            try
            {
                result = work().ConfigureAwait(true).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        if (error is not null)
        {
            throw error;
        }

        return result!;
    }

    public static void Run(Func<Task> work)
    {
        Run(async () =>
        {
            await work().ConfigureAwait(true);
            return true;
        });
    }

    public static void WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (!condition())
        {
            if (Environment.TickCount64 >= deadline)
            {
                return;
            }

            PumpMessages();
            Thread.Sleep(1);
        }
    }

    public static T RunOnCurrentThread<T>(Func<Task<T>> work)
    {
        var task = work();
        while (!task.IsCompleted)
        {
            PumpMessages();
            Thread.Sleep(1);
        }

        return task.ConfigureAwait(true).GetAwaiter().GetResult();
    }

    public static void RunOnCurrentThread(Func<Task> work)
    {
        RunOnCurrentThread(async () =>
        {
            await work().ConfigureAwait(true);
            return true;
        });
    }

    private static void Post(Action work)
    {
        lock (Gate)
        {
            _syncWork = work;
            _asyncWork = null;
            _error = null;
        }

        WorkAvailable.Set();
        WaitForWorkCompletion();
    }

    private static void WaitForWorkCompletion()
    {
        while (true)
        {
            Exception? error;
            lock (Gate)
            {
                error = _error;
                if (_syncWork is null && _asyncWork is null)
                {
                    if (error is not null)
                    {
                        throw error;
                    }

                    return;
                }
            }

            if (error is not null)
            {
                throw error;
            }

            Thread.Sleep(1);
        }
    }

    private static Thread CreateStaThread()
    {
        var thread = new Thread(StaThreadMain)
        {
            IsBackground = true,
            Name = "UnifiedMessenger.WebView2.STA"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return thread;
    }

    private static void StaThreadMain()
    {
        while (true)
        {
            WorkAvailable.WaitOne();
            Action? syncWork;
            Func<Task>? asyncWork;
            lock (Gate)
            {
                syncWork = _syncWork;
                asyncWork = _asyncWork;
            }

            try
            {
                if (syncWork is not null)
                {
                    syncWork();
                }
                else if (asyncWork is not null)
                {
                    asyncWork().ConfigureAwait(true).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                lock (Gate)
                {
                    _error = ex;
                }
            }
            finally
            {
                lock (Gate)
                {
                    _syncWork = null;
                    _asyncWork = null;
                }
            }

            PumpMessages();
        }
    }

    private static void PumpMessages()
    {
        while (PeekMessage(out var message, IntPtr.Zero, 0, 0, PM_REMOVE))
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(
        out MSG message,
        IntPtr hWnd,
        uint wMsgFilterMin,
        uint wMsgFilterMax,
        uint wRemoveMsg);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG message);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PtX;
        public int PtY;
    }
}
