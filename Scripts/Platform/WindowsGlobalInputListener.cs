using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ImmortalIdle
{
    public enum GlobalInputKind
    {
        KeyDown,
        MouseLeftDown,
        MouseRightDown,
        MouseWheel
    }

    public readonly struct GlobalInputSample
    {
        public GlobalInputKind Kind { get; init; }
        public int Data { get; init; }
    }

    /// <summary>
    /// Windows 全局键鼠监听器：仅采集真实输入，不依赖窗口焦点。
    /// </summary>
    public sealed class WindowsGlobalInputListener : IDisposable
    {
        private readonly ConcurrentQueue<GlobalInputSample> _queue = new();

        private static IntPtr _keyboardHook = IntPtr.Zero;
        private static IntPtr _mouseHook = IntPtr.Zero;
        private static HookProc _keyboardProc;
        private static HookProc _mouseProc;
        private static WindowsGlobalInputListener _current;
        private bool _started;

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MOUSEWHEEL = 0x020A;

        public bool Start()
        {
            if (_started)
            {
                return true;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            _current = this;
            _keyboardProc = KeyboardHookCallback;
            _mouseProc = MouseHookCallback;

            IntPtr module = GetCurrentModuleHandle();
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, module, 0);
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);

            _started = _keyboardHook != IntPtr.Zero && _mouseHook != IntPtr.Zero;
            if (!_started)
            {
                Stop();
            }

            return _started;
        }

        public void Stop()
        {
            if (_keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }

            if (_mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }

            _current = null;
            _started = false;
        }

        public bool TryDequeue(out GlobalInputSample sample)
        {
            return _queue.TryDequeue(out sample);
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        private void Enqueue(GlobalInputSample sample)
        {
            _queue.Enqueue(sample);
        }

        private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _current != null)
            {
                int msg = unchecked((int)(long)wParam);
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    _current.Enqueue(new GlobalInputSample
                    {
                        Kind = GlobalInputKind.KeyDown,
                        Data = vkCode
                    });
                }
            }

            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _current != null)
            {
                int msg = unchecked((int)(long)wParam);
                switch (msg)
                {
                    case WM_LBUTTONDOWN:
                        _current.Enqueue(new GlobalInputSample
                        {
                            Kind = GlobalInputKind.MouseLeftDown,
                            Data = 0
                        });
                        break;
                    case WM_RBUTTONDOWN:
                        _current.Enqueue(new GlobalInputSample
                        {
                            Kind = GlobalInputKind.MouseRightDown,
                            Data = 0
                        });
                        break;
                    case WM_MOUSEWHEEL:
                        int mouseData = Marshal.ReadInt32(lParam, 8);
                        int wheelDelta = (short)((mouseData >> 16) & 0xffff);
                        _current.Enqueue(new GlobalInputSample
                        {
                            Kind = GlobalInputKind.MouseWheel,
                            Data = wheelDelta
                        });
                        break;
                }
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private static IntPtr GetCurrentModuleHandle()
        {
            using Process currentProcess = Process.GetCurrentProcess();
            ProcessModule module = currentProcess.MainModule;
            return module != null ? GetModuleHandle(module.ModuleName) : IntPtr.Zero;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
