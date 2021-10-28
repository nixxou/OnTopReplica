using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using OnTopReplica.Native;
using OnTopReplica.Properties;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace OnTopReplica.MessagePumpProcessors {

    public static class WindowsServices {
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int GWL_EXSTYLE = (-20);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
        public static extern IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);


        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        public static extern int GetWindowLong(IntPtr hWnd, GWL nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        public static extern int SetWindowLong(IntPtr hWnd, GWL nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetLayeredWindowAttributes")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, int crKey, byte alpha, LWA dwFlags);

        [DllImport("user32.dll")]
        static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn,
             IntPtr lParam);

        delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        public static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId) {
            var handles = new List<IntPtr>();

            foreach(ProcessThread thread in Process.GetProcessById(processId).Threads)
                EnumThreadWindows(thread.Id,
                    (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }

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

    /// <summary>
    /// HotKey registration helper.
    /// </summary>
    class HotKeyManager : BaseMessagePumpProcessor {

        public Dictionary<int, int> originalstyle = new Dictionary<int, int>();
        public int selforiginalstyle = 0;
        public bool passt = false;

        public HotKeyManager() {
            Enabled = true;
        }

        delegate void HotKeyHandler();

        /// <summary>
        /// Wraps hot key handler registration data.
        /// </summary>
        private class HotKeyHandlerRegistration : IDisposable {
            private HotKeyHandlerRegistration() {
            }

            private HotKeyHandlerRegistration(IntPtr hwnd, int key, HotKeyHandler handler) {
                if (hwnd == IntPtr.Zero)
                    throw new ArgumentException();
                if (handler == null)
                    throw new ArgumentNullException();

                _hwnd = hwnd;
                RegistrationKey = key;
                Handler = handler;
            }

            static int _lastUsedKey = 0;

            /// <summary>
            /// Registers a new hotkey and returns a handle to the registration.
            /// </summary>
            /// <returns>Returns null on failure.</returns>
            public static HotKeyHandlerRegistration Register(Form owner, int keyCode, int modifiers, HotKeyHandler handler) {
                var key = ++_lastUsedKey;

                if (!HotKeyMethods.RegisterHotKey(owner.Handle, key, modifiers, keyCode)) {
                    Log.Write("Failed to create hotkey on key {0} with modifiers {1}", keyCode, modifiers);
                    return null;
                }

                return new HotKeyHandlerRegistration(owner.Handle, key, handler);
            }

            IntPtr _hwnd;
            public int RegistrationKey { get; private set; }
            public HotKeyHandler Handler { get; private set; }

            public void Dispose() {
                if (!HotKeyMethods.UnregisterHotKey(_hwnd, RegistrationKey)) {
                    Log.Write("Failed to unregister hotkey #{0}", RegistrationKey);
                }
            }
        }

        Dictionary<int, HotKeyHandlerRegistration> _handlers = new Dictionary<int, HotKeyHandlerRegistration>();

        public override void Initialize(MainForm form) {
            base.Initialize(form);

            RefreshHotkeys();
        }

        public override bool Process(ref Message msg) {
            if (Enabled && msg.Msg == HotKeyMethods.WM_HOTKEY) {
                int keyId = msg.WParam.ToInt32();
                if (!_handlers.ContainsKey(keyId))
                    return false;

                _handlers[keyId].Handler.Invoke();
            }

            return false;
        }

        public bool Enabled { get; set; }

        /// <summary>
        /// Refreshes registered hotkeys from Settings.
        /// </summary>
        /// <remarks>
        /// Application settings contain hotkey registration strings that are used
        /// automatically by this registration process.
        /// </remarks>
        public void RefreshHotkeys() {
            ClearHandlers();

            RegisterHandler(Settings.Default.HotKeyCloneCurrent, HotKeyCloneHandler);
            RegisterHandler(Settings.Default.HotKeyPassT, HotKeyPassTHandler);

            RegisterHandler(Settings.Default.HotKeyShowHide, HotKeyShowHideHandler);
        }

        private void RegisterHandler(string spec, HotKeyHandler handler) {
            if (string.IsNullOrEmpty(spec))
                return; //this can happen and is allowed => simply don't register
            if (handler == null)
                throw new ArgumentNullException();

            int modifiers = 0, keyCode = 0;

            try {
                HotKeyMethods.TranslateStringToKeyValues(spec, out modifiers, out keyCode);
            }
            catch (ArgumentException) {
                //TODO: swallowed exception
                return;
            }

            var reg = HotKeyHandlerRegistration.Register(Form, keyCode, modifiers, handler);
            if(reg != null)
                _handlers.Add(reg.RegistrationKey, reg);
        }

        private void ClearHandlers() {
            foreach (var hotkey in _handlers) {
                hotkey.Value.Dispose();
            }
            _handlers.Clear();
        }

        protected override void Shutdown() {
            ClearHandlers();
        }

        #region Hotkey callbacks

        /// <summary>
        /// Handles "show/hide" hotkey. Ensures the form is in restored state and switches
        /// between shown and hidden states.
        /// </summary>
        void HotKeyShowHideHandler() {
            Form.FullscreenManager.SwitchBack();

            if (!Program.Platform.IsHidden(Form)) {
                Program.Platform.HideForm(Form);
            }
            else {
                Form.EnsureMainFormVisible();
                if(this.passt) {
                    int wl = 0;
                    wl = WindowsServices.GetWindowLong(Form.Handle, GWL.ExStyle);
                    wl = wl | 0x80000 | 0x20;
                    WindowsServices.SetWindowLong(Form.Handle, GWL.ExStyle, wl);
                }
            }
        }

        /// <summary>
        /// Handles the "clone current" hotkey.
        /// </summary>
        void HotKeyCloneHandler() {
            var handle = Win32Helper.GetCurrentForegroundWindow();
            if (handle.Handle == Form.Handle)
                return;

            Form.SetThumbnail(handle, null);
        }

        /// <summary>
        /// Handles the "clone current" hotkey.
        /// </summary>
        void HotKeyPassTHandler() {

            if(this.passt == false) {
                this.selforiginalstyle = WindowsServices.GetWindowLong(Form.Handle, GWL.ExStyle);
                int selfIdForm = Form.Handle.ToInt32();
                int wl = 0;
                Process[] processlist = System.Diagnostics.Process.GetProcesses();
                wl = WindowsServices.GetWindowLong(Form.Handle, GWL.ExStyle);
                wl = wl | 0x80000 | 0x20;
                WindowsServices.SetWindowLong(Form.Handle, GWL.ExStyle, wl);
                foreach(Process process in processlist) {
                    if(!String.IsNullOrEmpty(process.MainWindowTitle)) {
                        if(process.MainWindowTitle == "OnTopReplica") {
                            IntPtr PidAsIntPtr = new IntPtr(process.Id);
                            wl = 0;
                            foreach(var handle in WindowsServices.EnumerateProcessWindowHandles(process.Id)) {
                                if(handle.ToInt32() == selfIdForm) break;
                                if(this.originalstyle.ContainsKey(handle.ToInt32())) this.originalstyle[handle.ToInt32()] = WindowsServices.GetWindowLong(handle, GWL.ExStyle);
                                else this.originalstyle.Add(handle.ToInt32(), WindowsServices.GetWindowLong(handle, GWL.ExStyle));
                                wl = WindowsServices.GetWindowLong(handle, GWL.ExStyle);
                                wl = wl | 0x80000 | 0x20;
                                WindowsServices.SetWindowLong(handle, GWL.ExStyle, wl);
                                break;
                            }
                        }
                    }
                }
                this.passt = true;

            }
            else {
                int selfIdForm = Form.Handle.ToInt32();
                int wl = this.selforiginalstyle;
                Process[] processlist = System.Diagnostics.Process.GetProcesses();
                WindowsServices.SetWindowLong(Form.Handle, GWL.ExStyle, wl);
                foreach(Process process in processlist) {
                    if(!String.IsNullOrEmpty(process.MainWindowTitle)) {
                        if(process.MainWindowTitle == "OnTopReplica") {
                            IntPtr PidAsIntPtr = new IntPtr(process.Id);
                            foreach(var handle in WindowsServices.EnumerateProcessWindowHandles(process.Id)) {
                                if(handle.ToInt32() == selfIdForm) break;
                                if(this.originalstyle.ContainsKey(handle.ToInt32())) wl = this.originalstyle[handle.ToInt32()];

                                WindowsServices.SetWindowLong(handle, GWL.ExStyle, wl);
                                break;
                            }
                        }
                    }
                }
                this.passt = false;
            }




        }

        #endregion

    }

}
