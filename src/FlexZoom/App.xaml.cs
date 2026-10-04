using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace FlexZoom;

public partial class App : Application
{
    internal static bool FollowDiagnostics { get; private set; }
    private Mutex? instance;
    private EventWaitHandle? showRequest;
    private EventWaitHandle? updateQuitRequest;
    private DispatcherTimer? requests;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        FollowDiagnostics = Array.Exists(e.Args, x => x == "--diagnostics");
        if (Array.Exists(e.Args, x => x == "--test-surface"))
        {
            var surface = SelfTest.CreateSurface(); MainWindow = surface;
            surface.Closed += (_, _) => Shutdown(); surface.Show(); return;
        }
        bool headlessTest = Array.Exists(e.Args, x => x == "--self-test-headless");
        if (headlessTest || Array.Exists(e.Args, x => x == "--self-test"))
        {
            int code = SelfTest.Run(!headlessTest); Shutdown(code); return;
        }
        instance = new Mutex(true, "Local\\FlexZoom.Instance", out bool first);
        showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\FlexZoom.ShowSettings");
        // A delayed Windows sign-in launch must not reopen an already running app.
        if (!first) { if (!Array.Exists(e.Args, x => x == "--startup")) showRequest.Set(); Shutdown(); return; }
        // Path-specific: a build may only shut down the executable it is replacing.
        string executable = Path.GetFullPath(Environment.ProcessPath!).ToUpperInvariant();
        string pathHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(executable)));
        updateQuitRequest = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\FlexZoom.UpdateQuit." + pathHash);
        var window = new MainWindow();
        MainWindow = window;
        if (window.Preferences.StartInTray) new WindowInteropHelper(window).EnsureHandle();
        else window.Show();
        requests = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        requests.Tick += (_, _) =>
        {
            if (updateQuitRequest.WaitOne(0)) { window.QuitForUpdate(); return; }
            if (showRequest.WaitOne(0)) window.ShowSettings();
        };
        requests.Start();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        requests?.Stop(); showRequest?.Dispose(); updateQuitRequest?.Dispose(); instance?.Dispose(); base.OnExit(e);
    }
}
