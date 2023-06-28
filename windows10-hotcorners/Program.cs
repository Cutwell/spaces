using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace windows10_hotcorners
{
    internal static class Program
    {
        private static POINT previousMousePosition;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, MonitorOptions dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        private enum MonitorOptions
        {
            MONITOR_DEFAULTTONULL = 0,
            MONITOR_DEFAULTTOPRIMARY = 1,
            MONITOR_DEFAULTTONEAREST = 2
        }

        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }

        private class MainForm : Form
        {
            private bool isCursorInCorner = false;
            private readonly System.Windows.Forms.Label positionLabel;
            private readonly System.Windows.Forms.ComboBox topLeftComboBox;
            private readonly System.Windows.Forms.ComboBox topRightComboBox;
            private readonly System.Windows.Forms.ComboBox bottomLeftComboBox;
            private readonly System.Windows.Forms.ComboBox bottomRightComboBox;
            private readonly Timer timer;
            private readonly NotifyIcon notifyIcon;
            private readonly ContextMenu contextMenu;
            private readonly MenuItem settingsMenuItem;
            private readonly MenuItem quitMenuItem;
            private readonly System.Windows.Forms.Button startupButton;
            private bool isStartupEnabled;
            private string settingsFilePath;
            private readonly Font Normal = new Font("Tahoma", 10, FontStyle.Regular);
            private const int WH_MOUSE_LL = 14;
            private const int WH_MOUSE = 7;
            private const int WM_MOUSEMOVE = 0x0200;
            private readonly LowLevelMouseProc mouseProc;
            private readonly IntPtr mouseHookHandle;

            public MainForm()
            {
                Text = "Windows10 Hotcorners";
                Width = 512;
                Height = 256;
                BackColor = Color.White;
                ForeColor = Color.Black;

                FormBorderStyle = FormBorderStyle.FixedSingle;
                MaximizeBox = false;
                MinimizeBox = true;


                // Get the relative path to the icon file
                string iconPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "full-screen.ico");
                string notifyIconPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "full-screen-white.ico");

                // Check if the icon file exists
                if (File.Exists(iconPath))
                {
                    // Set the custom icon for the form
                    Icon = new Icon(iconPath);
                }

                // Subscribe to the Paint event
                Paint += MainForm_Paint;

                // Create the NotifyIcon control
                notifyIcon = new NotifyIcon
                {
                    Text = "Windows10 Hotcorners",
                    Visible = true
                };

                // Check if the icon file exists
                if (File.Exists(iconPath))
                {
                    // Set the custom icon for the form
                    notifyIcon.Icon = new Icon(notifyIconPath);
                }

                // Create the context menu and settings & quit menu items
                contextMenu = new ContextMenu();
                settingsMenuItem = new MenuItem("Settings", SettingsMenuItem_Click);
                quitMenuItem = new MenuItem("Quit", QuitMenuItem_Click);

                // Add the menu items to the context menu
                _ = contextMenu.MenuItems.Add(settingsMenuItem);
                _ = contextMenu.MenuItems.Add(quitMenuItem);

                // Assign the context menu to the NotifyIcon
                notifyIcon.ContextMenu = contextMenu;

                // Add a double-click event handler for the NotifyIcon
                notifyIcon.DoubleClick += NotifyIcon_DoubleClick;

                // Add same event handler for NotifyIcon single-click
                notifyIcon.Click += NotifyIcon_DoubleClick;

                // Add the Resize event handler for the form
                Resize += MainForm_Resize;

                // Add the FormClosing event handler for the form
                FormClosing += MainForm_FormClosing;

                positionLabel = new System.Windows.Forms.Label
                {
                    Text = "Set actions for each corner: ",
                    AutoSize = true,
                    Font = Normal,
                    Location = new Point(180 - 152, 64 - 56)
                };

                topLeftComboBox = CreateComboBox(new Point(180 - 152, 64 - 23));
                topRightComboBox = CreateComboBox(new Point(180 + 130, 64 - 23));
                bottomLeftComboBox = CreateComboBox(new Point(180 - 152, 64 + 74));
                bottomRightComboBox = CreateComboBox(new Point(180 + 130, 64 + 74));

                Controls.Add(positionLabel);
                Controls.Add(topLeftComboBox);
                Controls.Add(topRightComboBox);
                Controls.Add(bottomLeftComboBox);
                Controls.Add(bottomRightComboBox);

                // Load the saved values from the settings file
                LoadSettings();

                // Create the startup button
                startupButton = new System.Windows.Forms.Button
                {
                    Text = "Add to Windows Startup",
                    Location = new System.Drawing.Point(180 - 152, 64 + 112),
                    Width = 232 // Set the desired width
                };
                startupButton.Click += StartupButton_Click;
                startupButton.Font = Normal;
                startupButton.FlatStyle = FlatStyle.Flat;
                startupButton.BackColor = Color.WhiteSmoke;
                Controls.Add(startupButton);

                // Check if the program is currently set to start on startup
                isStartupEnabled = IsStartupEnabled();

                // Update the button text based on the current startup status
                UpdateStartupButtonText();

                // Set up the low-level mouse hook
                mouseProc = MouseHookCallback;
                mouseHookHandle = SetMouseHook(mouseProc);

                // Set the initial window state
                WindowState = FormWindowState.Normal;
                ShowInTaskbar = true;
            }

            private void LoadSettings()
            {
                // Get the path of the settings file
                settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.txt");

                // Check if the settings file exists
                if (File.Exists(settingsFilePath))
                {
                    try
                    {
                        // Load the settings from the file
                        using (StreamReader reader = new StreamReader(settingsFilePath))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                string[] parts = line.Split('=');
                                if (parts.Length == 2)
                                {
                                    string key = parts[0].Trim();
                                    string value = parts[1].Trim();

                                    // Assign the loaded values to the ComboBoxes
                                    switch (key)
                                    {
                                        case "TopLeft":
                                            topLeftComboBox.SelectedItem = value;
                                            break;
                                        case "TopRight":
                                            topRightComboBox.SelectedItem = value;
                                            break;
                                        case "BottomLeft":
                                            bottomLeftComboBox.SelectedItem = value;
                                            break;
                                        case "BottomRight":
                                            bottomRightComboBox.SelectedItem = value;
                                            break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show("Error loading settings: " + ex.Message);
                    }
                }
            }

            private void SaveSettings()
            {
                try
                {
                    // Save the settings to the file
                    using (StreamWriter writer = new StreamWriter(settingsFilePath))
                    {
                        writer.WriteLine("TopLeft=" + topLeftComboBox.SelectedItem);
                        writer.WriteLine("TopRight=" + topRightComboBox.SelectedItem);
                        writer.WriteLine("BottomLeft=" + bottomLeftComboBox.SelectedItem);
                        writer.WriteLine("BottomRight=" + bottomRightComboBox.SelectedItem);
                    }
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show("Error saving settings: " + ex.Message);
                }
            }

            private bool IsStartupEnabled()
            {
                using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    return registryKey.GetValue(Application.ProductName) != null;
                }
            }

            private void SetStartupEnabled(bool enabled)
            {
                using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (enabled)
                    {
                        registryKey.SetValue(Application.ProductName, Application.ExecutablePath);
                    }
                    else
                    {
                        registryKey.DeleteValue(Application.ProductName, false);
                    }
                }
            }

            private void UpdateStartupButtonText()
            {
                startupButton.Text = isStartupEnabled ? "Remove from Windows Startup" : "Add to Windows Startup";
            }

            private void StartupButton_Click(object sender, EventArgs e)
            {
                isStartupEnabled = !isStartupEnabled;
                SetStartupEnabled(isStartupEnabled);
                UpdateStartupButtonText();
            }

            private void NotifyIcon_DoubleClick(object sender, EventArgs e)
            {
                // Restore the form when double-clicking the NotifyIcon
                ShowInTaskbar = true;
                Show();
                WindowState = FormWindowState.Normal;
                BringToFront();
            }

            private void MainForm_Resize(object sender, EventArgs e)
            {
                // Minimize the form to the system tray when minimizing
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                    ShowInTaskbar = false;
                }
            }

            private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
            {
                // Cleanup resources when the form is closing
                notifyIcon.Dispose();
            }

            private void SettingsMenuItem_Click(object sender, EventArgs e)
            {
                // Show the form when the "Settings" menu item is clicked
                ShowInTaskbar = true;
                Show();
                WindowState = FormWindowState.Normal;
                BringToFront();
            }

            private void QuitMenuItem_Click(object sender, EventArgs e)
            {
                // Cleanup resources when the form is closing
                notifyIcon.Dispose();
                Application.Exit();
            }

            private void MainForm_Paint(object sender, PaintEventArgs e)
            {
                // Create a Graphics object from the event arguments
                Graphics g = e.Graphics;

                // Set the box dimensions and location
                int boxWidth = 128;
                int boxHeight = 72;
                int boxX = 180;
                int boxY = 64;

                // Create a pale blue brush
                Brush brush = new SolidBrush(Color.FromArgb(200, 200, 255));

                // Draw the pale blue box
                g.FillRectangle(brush, boxX, boxY, boxWidth, boxHeight);

                // Dispose of the brush
                brush.Dispose();
            }

            private System.Windows.Forms.ComboBox CreateComboBox(Point location)
            {
                System.Windows.Forms.ComboBox comboBox = new System.Windows.Forms.ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                comboBox.Items.AddRange(new string[] { "", "Lock", "Show Desktop", "Show Open Windows", "Show Start Menu" });
                comboBox.Location = location;
                comboBox.Size = new Size(150, 21);
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.Font = Normal;
                comboBox.BackColor = Color.WhiteSmoke;

                return comboBox;
            }

            protected override void OnClosed(EventArgs e)
            {
                // Remove the low-level mouse hook when the form is closed
                RemoveMouseHook(mouseHookHandle);

                base.OnClosed(e);
            }

            private IntPtr SetMouseHook(LowLevelMouseProc proc)
            {
                using (ProcessModule currentModule = Process.GetCurrentProcess().MainModule)
                {
                    // Set the low-level mouse hook
                    IntPtr hookHandle = SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(currentModule.ModuleName), 0);

                    // Set the normal mouse hook to track mouse events globally
                    _ = SetWindowsHookEx(WH_MOUSE, proc, IntPtr.Zero, GetCurrentThreadId());

                    return hookHandle;
                }
            }

            private void RemoveMouseHook(IntPtr hookHandle)
            {
                // Remove the low-level mouse hook
                _ = UnhookWindowsHookEx(hookHandle);

                // Remove the normal mouse hook
                _ = UnhookWindowsHookEx(mouseHookHandle);
            }

            private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
            {
                if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEMOVE)
                {
                    // Extract the mouse position from the lParam parameter
                    int x = Cursor.Position.X;
                    int y = Cursor.Position.Y;

                    POINT currentMousePosition;
                    currentMousePosition.X = x;
                    currentMousePosition.Y = y;

                    if (currentMousePosition.X != previousMousePosition.X || currentMousePosition.Y != previousMousePosition.Y)
                    {
                        (_, string action, bool cursorFlag) = GetScreenCorner(currentMousePosition, topLeftComboBox, topRightComboBox, bottomLeftComboBox, bottomRightComboBox);

                        if (cursorFlag && !isCursorInCorner)
                        {
                            ExecuteAction(action);
                        }

                        isCursorInCorner = cursorFlag;

                        //positionLabel.Text = $"Mouse Position: X: {currentMousePosition.X}, Y: {currentMousePosition.Y}, Corner: {corner}, Action: {action}, InCorner: {isCursorInCorner}";

                        previousMousePosition = currentMousePosition;
                    }
                }

                return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }

            // Low-level mouse hook delegate
            private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

            // Windows API function declarations
            [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
            private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
            private static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
            private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern uint GetCurrentThreadId();

            private void ExecuteAction(string action)
            {
                switch (action)
                {
                    case "Show Open Windows":
                        // Perform the action to show open windows
                        ShowWindows();
                        break;
                    case "Lock":
                        // Perform the action to lock the computer
                        LockDesktop();
                        break;
                    case "Sleep":
                        // Perform the action to put the computer to sleep
                        SetSuspendState();
                        break;
                    case "Show Desktop":
                        // Perform the action to show the desktop
                        ShowDesktop();
                        break;
                    case "Show Start Menu":
                        // Perform the action to show the Start menu
                        ShowStartMenu();
                        break;
                    default:
                        // Invalid action, do nothing or handle the case accordingly
                        break;
                }
            }

            // P/Invoke to execute the Sleep action
            [DllImport("Powrprof.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
            public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

            // Method to execute the Sleep action
            private void SetSuspendState()
            {
                _ = SetSuspendState(false, true, false);
            }

            // P/Invoke to execute the Show desktop action
            [DllImport("user32.dll")]
            public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

            [DllImport("user32.dll")]
            public static extern bool LockWorkStation();

            // Method to execute the Show desktop action
            private void ShowDesktop()
            {
                const int VK_LWIN = 0x5B;
                const int VK_D = 0x44;

                // Simulate pressing Win key
                keybd_event(VK_LWIN, 0, 0, 0);

                // Simulate pressing D key
                keybd_event(VK_D, 0, 0, 0);

                // Simulate releasing D key
                keybd_event(VK_D, 0, 0x2, 0);

                // Simulate releasing Win key
                keybd_event(VK_LWIN, 0, 0x2, 0);
            }

            // Method to execute the Show Start Menu action
            private void ShowStartMenu()
            {
                const int SW_SHOW = 5;
                IntPtr hWnd = FindWindow("Shell_TrayWnd", null);
                _ = ShowWindow(hWnd, SW_SHOW);
                keybd_event(0x5B, 0, 0, 0); // Press Win key
                keybd_event(0x5B, 0, 0x2, 0); // Release Win key
            }

            // Method to execute the Lock Desktop action
            private void LockDesktop()
            {
                _ = LockWorkStation();
            }

            // Method to execute the Show Windows action
            private void ShowWindows()
            {
                const int VK_LWIN = 0x5B;
                const int VK_TAB = 0x09;

                // Simulate pressing Win key
                keybd_event(VK_LWIN, 0, 0, 0);

                // Simulate pressing Tab key
                keybd_event(VK_TAB, 0, 0, 0);

                // Simulate releasing Tab key
                keybd_event(VK_TAB, 0, 0x2, 0);

                // Simulate releasing Win key
                keybd_event(VK_LWIN, 0, 0x2, 0);
            }


            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                // Save the settings when the form is closing
                SaveSettings();
                base.OnFormClosing(e);
            }

            private static (ScreenCorner, string, bool) GetScreenCorner(POINT mousePosition, System.Windows.Forms.ComboBox topLeftComboBox, System.Windows.Forms.ComboBox topRightComboBox, System.Windows.Forms.ComboBox bottomLeftComboBox, System.Windows.Forms.ComboBox bottomRightComboBox)
            {
                IntPtr monitorHandle = MonitorFromPoint(mousePosition, MonitorOptions.MONITOR_DEFAULTTONEAREST);
                MONITORINFO monitorInfo = new MONITORINFO
                {
                    cbSize = Marshal.SizeOf(typeof(MONITORINFO))
                };

                if (GetMonitorInfo(monitorHandle, monitorInfo))
                {
                    Rectangle monitorRect = new Rectangle(monitorInfo.rcMonitor.Left, monitorInfo.rcMonitor.Top, monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left, monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top);

                    bool isInCorner;

                    // Check if mouse is within 2 pixels of the corner
                    if (Math.Abs(mousePosition.X - monitorRect.Left) <= 2 && Math.Abs(mousePosition.Y - monitorRect.Top) <= 2)
                    {
                        isInCorner = true;
                        string action = GetAssociatedAction(ScreenCorner.TopLeft, topLeftComboBox);
                        return (ScreenCorner.TopLeft, action, isInCorner);
                    }
                    else if (Math.Abs(mousePosition.X - monitorRect.Right) <= 2 && Math.Abs(mousePosition.Y - monitorRect.Top) <= 2)
                    {
                        isInCorner = true;
                        string action = GetAssociatedAction(ScreenCorner.TopRight, topRightComboBox);
                        return (ScreenCorner.TopRight, action, isInCorner);
                    }
                    else if (Math.Abs(mousePosition.X - monitorRect.Left) <= 2 && Math.Abs(mousePosition.Y - monitorRect.Bottom) <= 2)
                    {
                        isInCorner = true;
                        string action = GetAssociatedAction(ScreenCorner.BottomLeft, bottomLeftComboBox);
                        return (ScreenCorner.BottomLeft, action, isInCorner);
                    }
                    else if (Math.Abs(mousePosition.X - monitorRect.Right) <= 2 && Math.Abs(mousePosition.Y - monitorRect.Bottom) <= 2)
                    {
                        isInCorner = true;
                        string action = GetAssociatedAction(ScreenCorner.BottomRight, bottomRightComboBox);
                        return (ScreenCorner.BottomRight, action, isInCorner);
                    }
                }

                return (ScreenCorner.Unknown, "", false);
            }

            private static string GetAssociatedAction(ScreenCorner corner, System.Windows.Forms.ComboBox comboBox)
            {
                string action = "";

                switch (corner)
                {
                    case ScreenCorner.TopLeft:
                    case ScreenCorner.TopRight:
                    case ScreenCorner.BottomLeft:
                    case ScreenCorner.BottomRight:
                        action = comboBox.SelectedItem?.ToString();
                        break;
                }

                return action ?? "";
            }
        }

        private enum ScreenCorner
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            Unknown
        }
    }
}