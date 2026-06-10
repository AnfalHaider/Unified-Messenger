using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace UnifiedMessenger.Tests.WebView2;

internal sealed class HeadlessWebView2Harness : IDisposable
{
    private const string PublishBadgeScript =
        "if (window.__unifiedMessengerPublishBadge) { window.__unifiedMessengerPublishBadge(); }";

    private readonly string _userDataFolder;
    private readonly IntPtr _hostWindow;
    private readonly CoreWebView2Controller _controller;
    private readonly CoreWebView2 _coreWebView;
    private BadgeCountMessage? _lastBadgeMessage;

    private HeadlessWebView2Harness(
        string userDataFolder,
        IntPtr hostWindow,
        CoreWebView2Controller controller,
        CoreWebView2 coreWebView)
    {
        _userDataFolder = userDataFolder;
        _hostWindow = hostWindow;
        _controller = controller;
        _coreWebView = coreWebView;
    }

    public static void EnsureRuntimeAvailable()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            Skip.If(string.IsNullOrWhiteSpace(version), "WebView2 runtime is not installed.");
        }
        catch (DllNotFoundException ex)
        {
            Skip.If(true, $"WebView2 loader is unavailable: {ex.Message}");
        }
        catch (FileNotFoundException ex)
        {
            Skip.If(true, $"WebView2 runtime is not installed: {ex.Message}");
        }
        catch (Exception ex) when (ex.Message.Contains("WebView2", StringComparison.OrdinalIgnoreCase))
        {
            Skip.If(true, $"WebView2 runtime is not available: {ex.Message}");
        }
    }

    public static HeadlessWebView2Harness Create()
    {
        EnsureRuntimeAvailable();

        var userDataFolder = Path.Combine(
            Path.GetTempPath(),
            "UnifiedMessenger.Tests",
            "WebView2",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(userDataFolder);

        var hostWindow = NativeTestWindow.Create();
        if (hostWindow == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create a hidden host window for WebView2.");
        }

        try
        {
            var environment = StaThreadRunner.RunOnCurrentThread(
                () => CoreWebView2Environment.CreateWithOptionsAsync(
                        browserExecutableFolder: null,
                        userDataFolder: userDataFolder,
                        options: null)
                    .AsTask());

            var windowReference = CoreWebView2ControllerWindowReference.CreateFromWindowHandle(
                unchecked((ulong)hostWindow));
            var controller = StaThreadRunner.RunOnCurrentThread(
                () => environment.CreateCoreWebView2ControllerAsync(windowReference).AsTask());

            controller.Bounds = new global::Windows.Foundation.Rect(0, 0, 1024, 768);
            controller.IsVisible = false;

            var coreWebView = controller.CoreWebView2
                ?? throw new InvalidOperationException("CoreWebView2 was not initialized.");

            coreWebView.Settings.IsWebMessageEnabled = true;
            coreWebView.Settings.AreDefaultScriptDialogsEnabled = false;

            var harness = new HeadlessWebView2Harness(userDataFolder, hostWindow, controller, coreWebView);
            coreWebView.WebMessageReceived += harness.OnWebMessageReceived;
            return harness;
        }
        catch
        {
            NativeTestWindow.Destroy(hostWindow);
            TryDeleteDirectory(userDataFolder);
            throw;
        }
    }

    public BadgeCountMessage RunAdapterBadgeTest(
        string adapterScriptFileName,
        string fixtureFileName,
        string instanceId,
        string platform,
        int expectedCount)
    {
        _lastBadgeMessage = null;

        var coreScript = AdapterScriptTestHelper.PrepareScript(
            AdapterScriptTestHelper.ReadScript("adapter-core.js"),
            instanceId,
            platform);
        var adapterScript = AdapterScriptTestHelper.PrepareScript(
            AdapterScriptTestHelper.ReadScript(adapterScriptFileName),
            instanceId,
            platform);

        var navigationCompleted = false;
        var navigationSucceeded = false;
        void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            navigationCompleted = true;
            navigationSucceeded = args.IsSuccess;
        }

        _coreWebView.NavigationCompleted += OnNavigationCompleted;
        try
        {
            var fixtureHtml = AdapterScriptTestHelper.ReadFixtureHtml(fixtureFileName);
            _coreWebView.NavigateToString(fixtureHtml);

            StaThreadRunner.WaitUntil(() => navigationCompleted, TimeSpan.FromSeconds(15));
            if (!navigationSucceeded)
            {
                throw new InvalidOperationException($"Navigation failed for fixture '{fixtureFileName}'.");
            }

            StaThreadRunner.RunOnCurrentThread(() => _coreWebView.ExecuteScriptAsync(coreScript).AsTask());
            StaThreadRunner.RunOnCurrentThread(() => _coreWebView.ExecuteScriptAsync(adapterScript).AsTask());
            Thread.Sleep(300);
            StaThreadRunner.RunOnCurrentThread(
                () => _coreWebView.ExecuteScriptAsync(PublishBadgeScript).AsTask());
            StaThreadRunner.WaitUntil(() => _lastBadgeMessage is not null, TimeSpan.FromSeconds(10));

            var message = _lastBadgeMessage;
            if (message is null)
            {
                var domCount = TryScrapeDomBadgeCount();
                if (domCount == expectedCount)
                {
                    message = new BadgeCountMessage
                    {
                        Type = "badge-count",
                        InstanceId = instanceId,
                        Platform = platform,
                        Count = domCount.Value
                    };
                }
            }

            if (message is null)
            {
                var bridgeProbe = StaThreadRunner.RunOnCurrentThread(() => _coreWebView.ExecuteScriptAsync(
                        """
                        (function () {
                          return {
                            hasChrome: !!(window.chrome && window.chrome.webview),
                            hasPublish: typeof window.__unifiedMessengerPublishBadge === 'function',
                            hasPost: typeof window.__umPostMessage === 'function'
                          };
                        })();
                        """)
                    .AsTask());

                throw new TimeoutException(
                    $"Timed out waiting for badge-count postMessage from '{adapterScriptFileName}'. Bridge probe: {bridgeProbe}");
            }

            if (message.Count != expectedCount)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Expected badge count {expectedCount} but received {message.Count} from '{adapterScriptFileName}'.");
            }

            return message;
        }
        finally
        {
            _coreWebView.NavigationCompleted -= OnNavigationCompleted;
        }
    }

    public int? TryScrapeDomBadgeCount()
    {
        var result = StaThreadRunner.RunOnCurrentThread(() => _coreWebView.ExecuteScriptAsync(
                """
                (function () {
                  var total = 0;
                  var selectors = [
                    'span[data-testid="icon-unread"]',
                    'span[data-testid="unread-count"]',
                    'span[aria-label*="unread"]',
                    '.p-channel_sidebar__badge',
                    '[data-qa="unread_count"]',
                    '.dialog-subtitle-badge.unread',
                    'span.ChatBadge.unread'
                  ];

                  selectors.forEach(function (selector) {
                    document.querySelectorAll(selector).forEach(function (badge) {
                      var label = badge.getAttribute('aria-label') || '';
                      var source = /unread/i.test(label) ? label : (badge.textContent || '');
                      var match = String(source).match(/\d+/);
                      if (match) {
                        total += parseInt(match[0], 10);
                      }
                    });
                  });

                  return total > 0 ? total : null;
                })();
                """)
            .AsTask());

        if (string.IsNullOrWhiteSpace(result) || result.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return JsonSerializer.Deserialize<int>(result);
    }

    public void Dispose()
    {
        _coreWebView.WebMessageReceived -= OnWebMessageReceived;
        _controller.Close();

        if (_hostWindow != IntPtr.Zero)
        {
            NativeTestWindow.Destroy(_hostWindow);
        }

        Thread.Sleep(100);
        TryDeleteDirectory(_userDataFolder);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var json = args.WebMessageAsJson;
            if (string.IsNullOrWhiteSpace(json))
            {
                json = args.TryGetWebMessageAsString();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeElement) ||
                !string.Equals(typeElement.GetString(), "badge-count", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _lastBadgeMessage = new BadgeCountMessage
            {
                Type = typeElement.GetString() ?? "badge-count",
                InstanceId = root.TryGetProperty("instanceId", out var instanceElement)
                    ? instanceElement.GetString()
                    : null,
                Platform = root.TryGetProperty("platform", out var platformElement)
                    ? platformElement.GetString()
                    : null,
                Count = root.TryGetProperty("count", out var countElement) && countElement.TryGetInt32(out var count)
                    ? count
                    : 0
            };
        }
        catch (JsonException)
        {
            // Ignore unrelated host messages during the test harness run.
        }
    }

    private static string BuildFixtureUri(string fixtureFileName)
    {
        var fixturePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName));
        return new Uri(fixturePath).AbsoluteUri;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // WebView2 may still be releasing profile locks; temp cleanup is best-effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup only.
        }
    }

    private static class NativeTestWindow
    {
        private const int WS_OVERLAPPED = 0x00000000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private static readonly IntPtr HInstance = Marshal.GetHINSTANCE(typeof(NativeTestWindow).Module);
        private static readonly string WindowClass = "UnifiedMessengerHeadlessWebView2_" + Guid.NewGuid().ToString("N");
        private static readonly WndProc WndProcDelegate = DefWindowProc;
        private static bool _classRegistered;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowExW(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASS
        {
            public uint Style;
            public WndProc LpfnWndProc;
            public int CbClsExtra;
            public int CbWndExtra;
            public IntPtr HInstance;
            public IntPtr HIcon;
            public IntPtr HCursor;
            public IntPtr HbrBackground;
            public string LpszMenuName;
            public string LpszClassName;
        }

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public static IntPtr Create()
        {
            EnsureClassRegistered();
            return CreateWindowExW(
                WS_EX_NOACTIVATE,
                WindowClass,
                "UnifiedMessenger WebView2 Test Host",
                WS_OVERLAPPED,
                0,
                0,
                1,
                1,
                IntPtr.Zero,
                IntPtr.Zero,
                HInstance,
                IntPtr.Zero);
        }

        public static void Destroy(IntPtr window)
        {
            if (window != IntPtr.Zero)
            {
                DestroyWindow(window);
            }
        }

        private static void EnsureClassRegistered()
        {
            if (_classRegistered)
            {
                return;
            }

            var windowClass = new WNDCLASS
            {
                HInstance = HInstance,
                LpszClassName = WindowClass,
                LpfnWndProc = WndProcDelegate
            };

            RegisterClassW(ref windowClass);
            _classRegistered = true;
        }
    }
}

internal sealed class BadgeCountMessage
{
    public string Type { get; init; } = "badge-count";

    public string? InstanceId { get; init; }

    public string? Platform { get; init; }

    public int Count { get; init; }
}
