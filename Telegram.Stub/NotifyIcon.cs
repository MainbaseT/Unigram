using System.Runtime.InteropServices;

namespace Telegram.Stub
{
    internal class NotifyIcon
    {
        private const int WM_DESTROY = 0x0002;
        private const int WM_USER = 0x0400;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_COMMAND = 0x0111;

        private const int MENU_OPEN = 1001;
        private const int MENU_EXIT = 1002;

        private IntPtr _hwnd;
        private uint _taskbarRestart;
        private IntPtr _menu;
        private readonly WndProc _wndProcDelegate;
        private readonly CancellationTokenSource _cts;

        public NotifyIcon()
        {
            _wndProcDelegate = WndProc2;
            _cts = new CancellationTokenSource();

            _icon = "Resources\\Default.ico";

            Thread messageThread = new Thread(() =>
            {
                AppDomain.CurrentDomain.ProcessExit += (_, _) => RemoveTrayIcon();

                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                CreateMessageWindow();
                CreateTrayIcon();
                CreateContextMenu();

                MSG msg;
                while (GetMessage(out msg, IntPtr.Zero, 0, 0))
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }

                _cts.Cancel();
            });

            messageThread.SetApartmentState(ApartmentState.STA);
            messageThread.IsBackground = false;
            messageThread.Start();
        }

        private void CreateMessageWindow()
        {
            string className = "MyHiddenTrayWindowClass";

            WNDCLASS wc = new WNDCLASS
            {
                lpszClassName = className,
                lpfnWndProc = _wndProcDelegate,
                hInstance = IntPtr.Zero //Marshal.GetHINSTANCE(typeof(Program).Module)
            };

            ushort classAtom = RegisterClass(ref wc);
            if (classAtom == 0)
            {
                throw new Exception($"RegisterClass failed with error: {Marshal.GetLastWin32Error()}");
            }

            _hwnd = CreateWindowEx(
                0, className, "",
                0, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                throw new Exception($"CreateWindowEx failed with error: {Marshal.GetLastWin32Error()}");
            }

            _taskbarRestart = RegisterWindowMessage("TaskbarCreated");
        }

        private void CreateTrayIcon()
        {
            _iconHandle = LoadImage(IntPtr.Zero, _icon, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE | LR_DEFAULTCOLOR);

            NOTIFYICONDATA data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = 0x00000004 | 0x00000002 | 0x00000001, // NIF_TIP | NIF_ICON | NIF_MESSAGE
                szTip = "Test",
                hIcon = _iconHandle,
                uCallbackMessage = WM_USER
            };

            if (!Shell_NotifyIcon(0x00000000, ref data)) // NIM_ADD
            {
                throw new Exception($"Shell_NotifyIcon failed with error: {Marshal.GetLastWin32Error()}");
            }
        }

        private void CreateContextMenu()
        {
            _menu = CreatePopupMenu();

            AppendMenu(_menu, 0, MENU_OPEN, "Open");
            AppendMenu(_menu, 0, MENU_EXIT, "Exit");
        }

        private IntPtr WndProc2(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == _taskbarRestart)
            {
                CreateTrayIcon();
            }

            switch (msg)
            {
                case WM_USER:
                    if ((int)lParam == WM_RBUTTONUP)
                    {
                        ShowContextMenu();
                    }
                    else if ((int)lParam == WM_LBUTTONDBLCLK)
                    {
                        // Handle double-click on tray icon
                        OnOpen();
                    }
                    break;

                case WM_COMMAND:
                    // Handle menu item clicks
                    int menuId = (int)wParam;
                    switch (menuId)
                    {
                        case MENU_OPEN:
                            OnOpen();
                            break;
                        case MENU_EXIT:
                            OnExit();
                            break;
                    }
                    break;

                case WM_DESTROY:
                    RemoveTrayIcon();
                    PostQuitMessage(0);
                    break;
            }

            return DefWindowProc(hwnd, msg, wParam, lParam);
        }

        private void OnOpen()
        {
            Click?.Invoke(this, EventArgs.Empty);
        }

        private void OnExit()
        {
            Dispose();
        }

        const uint MF_BYCOMMAND = 0x00000000;
        const uint MF_STRING = 0x00000000;

        public void UpdateOpenText(string text)
        {
            ModifyMenu(_menu, MENU_OPEN, MF_BYCOMMAND | MF_STRING, MENU_OPEN, text);
        }

        public void UpdateExitText(string text)
        {
            ModifyMenu(_menu, MENU_EXIT, MF_BYCOMMAND | MF_STRING, MENU_EXIT, text);
        }

        public event EventHandler? Click;

        public event EventHandler? Closed;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyIcon(IntPtr hIcon);

        const uint IMAGE_ICON = 1;
        const uint LR_DEFAULTCOLOR = 0x0;
        const uint LR_DEFAULTSIZE = 0x40;
        const uint LR_LOADFROMFILE = 0x00000010;

        private IntPtr _iconHandle;

        private string _icon;
        public string Icon
        {
            get => _icon;
            set
            {
                if (_icon != value)
                {
                    _icon = value;
                    _iconHandle = LoadImage(IntPtr.Zero, value, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE | LR_DEFAULTCOLOR);

                    var data = new NOTIFYICONDATA();
                    data.cbSize = Marshal.SizeOf<NOTIFYICONDATA>();
                    data.hWnd = _hwnd;
                    data.uID = 1;
                    data.uFlags = 0x00000002; // NIF_ICON | NIF_TIP;
                    data.hIcon = _iconHandle;
                    //data.szTip = "My tooltip";

                    // NIM_MODIFY
                    Shell_NotifyIcon(0x00000001, ref data);
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            RemoveTrayIcon();
            PostQuitMessage(0);

            Closed?.Invoke(this, EventArgs.Empty);
        }

        private void ShowContextMenu()
        {
            POINT pt;
            GetCursorPos(out pt);

            SetForegroundWindow(_hwnd);

            // TPM_RETURNCMD (0x0100) returns the selected menu item ID
            // TPM_RIGHTBUTTON (0x0002) allows right-click to select
            int cmd = TrackPopupMenu(_menu, 0x0100 | 0x0002, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);

            // If TPM_RETURNCMD is used, handle the command directly
            if (cmd > 0)
            {
                HandleMenuCommand(cmd);
            }

            PostMessage(_hwnd, 0, IntPtr.Zero, IntPtr.Zero); // Dismiss menu properly
        }

        private void HandleMenuCommand(int menuId)
        {
            switch (menuId)
            {
                case MENU_OPEN:
                    OnOpen();
                    break;
                case MENU_EXIT:
                    OnExit();
                    break;
            }
        }

        private void RemoveTrayIcon()
        {
            if (_hwnd != IntPtr.Zero)
            {
                NOTIFYICONDATA data = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = _hwnd,
                    uID = 1
                };
                Shell_NotifyIcon(0x00000001, ref data); // NIM_DELETE
            }
        }

        [DllImport("user32.dll")]
        static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern ushort RegisterClass([In] ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam
        );

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool PostQuitMessage(int nExitCode);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA pnid);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr iconName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, int uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool ModifyMenu(IntPtr hMenu, uint uPosition, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int reserved, IntPtr hWnd, IntPtr rect);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hWnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pt_x;
            public int pt_y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WNDCLASS
        {
            public uint style;
            public WndProc lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
        }

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }
    }
}
