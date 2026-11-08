using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;
[assembly: System.Reflection.AssemblyTitle("MFG Launcher")]
[assembly: System.Reflection.AssemblyDescription("Steam game discovery and MFG Unlocker desktop manager")]
[assembly: System.Reflection.AssemblyCompany("MFG Launcher community project")]
[assembly: System.Reflection.AssemblyProduct("MFG Launcher")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
namespace MfgLauncher
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread] static void Main(string[] args)
        {
            if (SelfUpdater.ApplyIfRequested(args)) return; SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e) { MessageBox.Show(e.Exception.Message, "MFG Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error); };
            Application.Run(new MainForm());
        }
    }
}

