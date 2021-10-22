using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Diagnostics;




namespace OnTopReplica {

    public static class WindowsServices {
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int GWL_EXSTYLE = (-20);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
        public static extern IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);


/*[DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);*/

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        public static extern int GetWindowLong(IntPtr hWnd, GWL nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        public static extern int SetWindowLong(IntPtr hWnd, GWL nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetLayeredWindowAttributes")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, int crKey, byte alpha, LWA dwFlags);

        /*
        public static void SetWindowExTransparent(IntPtr hwnd) {
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }

        public static void SetWindowExNotTransparent(IntPtr hwnd) {
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }
        */
    }

    public enum GWL {
        ExStyle = -20
    }

    public enum WS_EX {
        Transparent = 0x20,
        Layered = 0x80000
    }

    public enum LWA {
        ColorKey = 0x1,
        Alpha = 0x2
    }




    partial class MainForm {

        //SidePanel _currentSidePanel = null;
        SidePanelContainer _sidePanelContainer = null;

        /// <summary>
        /// Opens a new side panel.
        /// </summary>
        /// <param name="panel">The side panel to embed.</param>
        public void SetSidePanel(SidePanel panel) {
            if (IsSidePanelOpen) {
                CloseSidePanel();
            }

            _sidePanelContainer = new SidePanelContainer(this);
            _sidePanelContainer.SetSidePanel(panel);
            _sidePanelContainer.Location = ComputeSidePanelLocation(_sidePanelContainer);
            _sidePanelContainer.Show(this);
        }

        /// <summary>
        /// Closes the current side panel.
        /// </summary>
        public void CloseSidePanel() {
            if (_sidePanelContainer == null || _sidePanelContainer.IsDisposed) {
                _sidePanelContainer = null;
                return;
            }

            _sidePanelContainer.Hide();
            _sidePanelContainer.FreeSidePanel();
        }

        /// <summary>
        /// Gets whether a side panel is currently shown.
        /// </summary>
        public bool IsSidePanelOpen {
            get {
                if (_sidePanelContainer == null)
                    return false;
                if (_sidePanelContainer.IsDisposed) {
                    _sidePanelContainer = null;
                    return false;
                }

                return _sidePanelContainer.Visible;
            }
        }

        /// <summary>
        /// Moves the side panel based on the main form's current location.
        /// </summary>
        protected void AdjustSidePanelLocation() {
            if (!IsSidePanelOpen)
                return;

            _sidePanelContainer.Location = ComputeSidePanelLocation(_sidePanelContainer);
        }

        /// <summary>
        /// Computes the target location of a side panel form that ensures it is visible on the current
        /// screen that contains the main form.
        /// </summary>
        private Point ComputeSidePanelLocation(Form sidePanel) {
            //Check if moving the panel on the form's right would put it off-screen
            var screen = Screen.FromControl(this);
            if (Location.X + Width + sidePanel.Width > screen.WorkingArea.Right) {
                return new Point(Location.X - sidePanel.Width, Location.Y);
            }
            else {
                return new Point(Location.X + Width, Location.Y);
            }
        }

        void SidePanel_RequestClosing(object sender, EventArgs e) {
            CloseSidePanel();
        }

        void Thumbnail_CloneClick(object sender, CloneClickEventArgs e) {
            // Win32Helper.InjectFakeMouseClick(CurrentThumbnailWindowHandle.Handle, e);
            //var windowHwnd = Process.GetCurrentProcess().MainWindowHandle;
            //var Handle = WindowsServices.FindWindowByCaption(IntPtr.Zero, "Calculatrice");
            //WindowsServices.SetWindowExTransparent(Handle);

            int wl = WindowsServices.GetWindowLong(this.Handle, GWL.ExStyle);
            wl = wl | 0x80000 | 0x20;
            WindowsServices.SetWindowLong(this.Handle, GWL.ExStyle, wl);


        }

        public  static void AddPassT() {

        }



    }

}
