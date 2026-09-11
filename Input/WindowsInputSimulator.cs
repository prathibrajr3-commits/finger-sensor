using System;
using System.Runtime.InteropServices;
using AirGestureAI.Utilities;

namespace AirGestureAI.Input
{
    /// <summary>
    /// Production implementation of IInputSimulator that uses the native Windows SendInput API
    /// to simulate keyboard and mouse scroll events.
    /// </summary>
    public class WindowsInputSimulator : IInputSimulator
    {
        // ── Win32 P/Invoke Declarations ───────────────────────────────────────

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        private const int INPUT_MOUSE = 0;
        private const int INPUT_KEYBOARD = 1;

        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const ushort VK_SPACE = 0x20;
        private const int WHEEL_DELTA = 120;

        private const uint WM_MOUSEWHEEL = 0x020A;
        private const uint WM_VSCROLL = 0x0115;
        private static readonly IntPtr SB_LINEUP = IntPtr.Zero;
        private static readonly IntPtr SB_LINEDOWN = (IntPtr)1;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        // ── Diagnostics & Telemetry Hook ──────────────────────────────────────
        /// <summary>
        /// Occurs when a scroll action is dispatched (delta = +120 for up, -120 for down).
        /// Used by unit tests and telemetry verification.
        /// </summary>
        public event Action<int>? ScrollDispatched;

        // ── Debounce and State Guards ─────────────────────────────────────────
        private DateTime _lastScrollTime = DateTime.MinValue;
        private DateTime _lastSpaceTime = DateTime.MinValue;
        private readonly object _inputLock = new object();

        // 50ms debounce for mouse scrolls to prevent duplicate input flooding
        private const double ScrollDebounceMs = 50.0;
        // 300ms debounce for Space key (Play/Pause)
        private const double SpaceDebounceMs = 300.0;

        /// <inheritdoc/>
        public void ScrollUp()
        {
            lock (_inputLock)
            {
                if ((DateTime.UtcNow - _lastScrollTime).TotalMilliseconds < ScrollDebounceMs)
                {
                    return; // Throttled
                }
                _lastScrollTime = DateTime.UtcNow;
            }

            Logger.Info("Simulating Windows Input: Scroll Up");
            DispatchScroll(WHEEL_DELTA);
            ScrollDispatched?.Invoke(WHEEL_DELTA);
        }

        /// <inheritdoc/>
        public void ScrollDown()
        {
            lock (_inputLock)
            {
                if ((DateTime.UtcNow - _lastScrollTime).TotalMilliseconds < ScrollDebounceMs)
                {
                    return; // Throttled
                }
                _lastScrollTime = DateTime.UtcNow;
            }

            Logger.Info("Simulating Windows Input: Scroll Down");
            DispatchScroll(-WHEEL_DELTA);
            ScrollDispatched?.Invoke(-WHEEL_DELTA);
        }

        /// <summary>
        /// Dispatches a scroll action using SendInput and targets the foreground window's
        /// focused control so the intended application receives the wheel event regardless
        /// of physical cursor parking.
        /// </summary>
        private void DispatchScroll(int delta)
        {
            // 1. Native hardware-level mouse wheel event via SendInput
            INPUT[] inputs = new INPUT[1];
            inputs[0] = new INPUT
            {
                type = INPUT_MOUSE,
                U = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dwFlags = MOUSEEVENTF_WHEEL,
                        mouseData = unchecked((uint)delta),
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            SendInputWithVerification(inputs);

            // 2. Direct message routing to foreground window / focused control
            try
            {
                IntPtr fgHwnd = GetForegroundWindow();
                if (fgHwnd != IntPtr.Zero)
                {
                    uint threadId = GetWindowThreadProcessId(fgHwnd, out _);

                    IntPtr targetHwnd = fgHwnd;
                    if (threadId != 0)
                    {
                        var gui = new GUITHREADINFO();
                        gui.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
                        if (GetGUIThreadInfo(threadId, ref gui) && gui.hwndFocus != IntPtr.Zero)
                        {
                            targetHwnd = gui.hwndFocus;
                        }
                    }

                    if (GetWindowRect(targetHwnd, out RECT rc))
                    {
                        int centerX = rc.Left + (rc.Right - rc.Left) / 2;
                        int centerY = rc.Top + (rc.Bottom - rc.Top) / 2;
                        IntPtr lParam = (IntPtr)((centerY << 16) | (centerX & 0xFFFF));
                        IntPtr wParam = (IntPtr)((delta << 16) & 0xFFFF0000);

                        PostMessage(targetHwnd, WM_MOUSEWHEEL, wParam, lParam);
                        if (targetHwnd != fgHwnd)
                        {
                            PostMessage(fgHwnd, WM_MOUSEWHEEL, wParam, lParam);
                        }

                        IntPtr vscrollCmd = delta > 0 ? SB_LINEUP : SB_LINEDOWN;
                        PostMessage(targetHwnd, WM_VSCROLL, vscrollCmd, IntPtr.Zero);
                        if (targetHwnd != fgHwnd)
                        {
                            PostMessage(fgHwnd, WM_VSCROLL, vscrollCmd, IntPtr.Zero);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Exception while routing scroll message to foreground window", ex);
            }
        }

        /// <inheritdoc/>
        public void PressSpace()
        {
            lock (_inputLock)
            {
                if ((DateTime.UtcNow - _lastSpaceTime).TotalMilliseconds < SpaceDebounceMs)
                {
                    return; // Throttled
                }
                _lastSpaceTime = DateTime.UtcNow;
            }

            Logger.Info("Simulating Windows Input: Press and Release Spacebar");

            // We combine KeyDown and KeyUp in a single SendInput call to guarantee they are processed sequentially
            // and prevent stuck key scenarios where KeyUp is dropped.
            INPUT[] inputs = new INPUT[2];
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VK_SPACE,
                        wScan = 0,
                        dwFlags = 0, // Key Down
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VK_SPACE,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP, // Key Up
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            SendInputWithVerification(inputs);
        }

        private void SendInputWithVerification(INPUT[] inputs)
        {
            try
            {
                uint result = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
                if (result != inputs.Length)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    Logger.Warn($"SendInput sent {result}/{inputs.Length} events. Win32 Error Code: {errorCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Exception occurred during SendInput execution", ex);
            }
        }
    }
}
