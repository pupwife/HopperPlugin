using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Hopper
{
    public sealed class Plugin : IDalamudPlugin
    {
        private IDalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        private bool InFlight => Service.Condition[ConditionFlag.InFlight];

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
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

        private const int INPUT_KEYBOARD = 1;
        private const int VK_SPACE = 0x20;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private readonly IntPtr ffxivWindowHandle;
        private int frameCounter = 0;
        private const int JUMP_INTERVAL = 3; // Jump every 3 frames to avoid spamming too fast

        public Plugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.PluginInterface.Create<Service>(this);

            this.ffxivWindowHandle = Process.GetCurrentProcess().MainWindowHandle;

            Service.Framework.Update += onFrameworkUpdate;
        }

        private void SendSpacebarPress()
        {
            // Use SendInput for better compatibility with concurrent key presses
            // Send keydown and keyup separately with a small delay to avoid blocking other inputs
            INPUT keyDown = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VK_SPACE,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            
            INPUT keyUp = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VK_SPACE,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            
            // Send keydown
            INPUT[] downArray = new INPUT[] { keyDown };
            SendInput(1, downArray, Marshal.SizeOf(typeof(INPUT)));
            
            // Small delay to allow other inputs to be processed
            System.Threading.Thread.Sleep(1);
            
            // Send keyup
            INPUT[] upArray = new INPUT[] { keyUp };
            SendInput(1, upArray, Marshal.SizeOf(typeof(INPUT)));
        }

        public void onFrameworkUpdate(IFramework framework)
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == this.ffxivWindowHandle)
            {
                // Check if spacebar is currently pressed (bit 15 is set when key is down)
                bool isSpacePressed = (GetAsyncKeyState(VK_SPACE) & 0x8000) != 0;
                
                // Continue jumping as long as spacebar is held, regardless of other key presses
                if (isSpacePressed && !InFlight)
                {
                    frameCounter++;
                    // Only send jump input every few frames to avoid spamming
                    if (frameCounter >= JUMP_INTERVAL)
                    {
                        // Send spacebar keypress using SendInput (better for concurrent keys)
                        SendSpacebarPress();
                        frameCounter = 0; // Reset counter
                    }
                }
                else
                {
                    // Reset counter when spacebar is released or in flight
                    frameCounter = 0;
                }
            }
        }

        public void Dispose()
        {
            Service.Framework.Update -= onFrameworkUpdate;
        }
    }
}
