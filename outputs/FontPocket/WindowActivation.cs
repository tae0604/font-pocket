using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FontPocket {
    static class WindowActivation {
        [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
        [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        [DllImport("user32.dll")]static extern bool AllowSetForegroundWindow(uint process);
        internal static void GrantToReceiver(IntPtr receiver) {uint process;GetWindowThreadProcessId(receiver,out process);if(process!=0)AllowSetForegroundWindow(process);}
        internal static void BringForward(Form form) {
            if(form==null||form.IsDisposed)return;
            // File activation restores only the viewer, never its minimized main window.
            if(form.WindowState==FormWindowState.Minimized)ShowWindow(form.Handle,9);
            if(!form.Visible)form.Show();
            form.BringToFront();form.Activate();SetForegroundWindow(form.Handle);
        }
    }
}
