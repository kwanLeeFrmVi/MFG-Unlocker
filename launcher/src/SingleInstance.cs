using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
namespace MfgLauncher
{
    internal sealed class SingleInstance : IDisposable
    {
        readonly Mutex mutex;
        public bool IsFirst { get; private set; }
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window, int command);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        public SingleInstance()
        {
            mutex = new Mutex(false, "Local\\MFGLauncher-SingleInstance");
            try { IsFirst = mutex.WaitOne(0); } catch (AbandonedMutexException) { IsFirst = true; }
            if (IsFirst) return;
            int current = Process.GetCurrentProcess().Id;
            for (int attempt = 0; attempt < 15; attempt++) {
                foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Application.ExecutablePath))) using (process) {
                    try {
                        if (process.Id == current || process.MainWindowHandle == IntPtr.Zero) continue;
                        if (IsIconic(process.MainWindowHandle)) ShowWindowAsync(process.MainWindowHandle, 9);
                        SetForegroundWindow(process.MainWindowHandle); return;
                    } catch (InvalidOperationException) { }
                }
                Thread.Sleep(100);
            }
        }
        public void Dispose() { if (IsFirst) mutex.ReleaseMutex(); mutex.Dispose(); }
    }
}
