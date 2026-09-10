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

        private const int INPUT_MOUSE = 0;
        private const int INPUT_KEYBOARD = 1;

        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const ushort VK_SPACE = 0x20;
        private const int WHEEL_DELTA = 120;

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

            INPUT[] inputs = new INPUT[1];
            inputs[0] = new INPUT
            {
                type = INPUT_MOUSE,
                U = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dwFlags = MOUSEEVENTF_WHEEL,
                        mouseData = (uint)WHEEL_DELTA,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            SendInputWithVerification(inputs);
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

            INPUT[] inputs = new INPUT[1];
            inputs[0] = new INPUT
            {
                type = INPUT_MOUSE,
                U = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dwFlags = MOUSEEVENTF_WHEEL,
                        // Cast negative delta to uint (unchecked) to represent negative scroll direction
                        mouseData = unchecked((uint)(-WHEEL_DELTA)),
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            SendInputWithVerification(inputs);
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
