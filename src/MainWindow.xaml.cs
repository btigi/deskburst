using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace DeskBurst
{
    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID = 1;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_ALT = 0x0001;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;
        private readonly NotifyIcon _notifyIcon;
        private readonly List<FireworksWindow> _activeFireworksWindows = [];
        private readonly HotkeyConfig _hotkeyConfig;

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool UnregisterHotKey(IntPtr hWnd, int id);

        public MainWindow()
        {
            InitializeComponent();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            _hotkeyConfig = configuration.GetSection("Hotkey").Get<HotkeyConfig>() ?? new HotkeyConfig();

            _notifyIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico")),
                Visible = true,
                Text = "DeskBurst"
            };

            var contextMenu = new System.Windows.Controls.ContextMenu();
            var exitMenuItem = new System.Windows.Controls.MenuItem
            {
                Header = "Exit"
            };
            exitMenuItem.Click += (s, e) => System.Windows.Application.Current.Shutdown();
            contextMenu.Items.Add(exitMenuItem);
            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => System.Windows.Application.Current.Shutdown());

            _notifyIcon.DoubleClick += (s, e) =>
            {
                this.Show();
                this.WindowState = WindowState.Normal;
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(HwndHook);
            RegisterHotkey();
        }

        private void RegisterHotkey()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            
            int modifiers = 0;
            if (_hotkeyConfig.Modifiers.Control) modifiers |= MOD_CONTROL;
            if (_hotkeyConfig.Modifiers.Alt) modifiers |= MOD_ALT;
            if (_hotkeyConfig.Modifiers.Shift) modifiers |= MOD_SHIFT;
            if (_hotkeyConfig.Modifiers.Windows) modifiers |= MOD_WIN;

            int vk = (int)Enum.Parse(typeof(Keys), _hotkeyConfig.Key);

            if (!RegisterHotKey(hwnd, HOTKEY_ID, modifiers, vk))
            {
                System.Windows.MessageBox.Show(
                    $"Failed to register hotkey: {GetHotkeyDescription()}. The application may not work as expected.", 
                    "Warning", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
            }
        }

        private string GetHotkeyDescription()
        {
            var parts = new List<string>();
            if (_hotkeyConfig.Modifiers.Control) parts.Add("Ctrl");
            if (_hotkeyConfig.Modifiers.Alt) parts.Add("Alt");
            if (_hotkeyConfig.Modifiers.Shift) parts.Add("Shift");
            if (_hotkeyConfig.Modifiers.Windows) parts.Add("Win");
            parts.Add(_hotkeyConfig.Key);
            return string.Join("+", parts);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                if (_activeFireworksWindows.Count > 0)
                {
                    var windowsToClose = _activeFireworksWindows.ToList();
                    foreach (var window in windowsToClose)
                    {
                        window.Close();
                    }
                    _activeFireworksWindows.Clear();
                }
                else
                {
                    foreach (Screen screen in Screen.AllScreens)
                    {
                        var fireworksWindow = new FireworksWindow(screen);
                        fireworksWindow.Closed += (s, e) => 
                        {
                            if (s is FireworksWindow window)
                            {
                                _activeFireworksWindows.Remove(window);
                            }
                        };
                        _activeFireworksWindows.Add(fireworksWindow);
                        fireworksWindow.Show();
                    }
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            _notifyIcon.Dispose();
            var hwnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hwnd, HOTKEY_ID);
            base.OnClosed(e);
        }
    }
}