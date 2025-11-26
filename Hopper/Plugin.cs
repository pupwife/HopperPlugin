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
        private bool wasInFlight = false;
        private bool isHoppingEnabled = false;
        private bool wasSpacePressed = false;

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
            INPUT[] inputs = new INPUT[2];
            
            // Key down
            inputs[0] = new INPUT
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
            
            // Key up
            inputs[1] = new INPUT
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
            
            // Send both events at once
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public void onFrameworkUpdate(IFramework framework)
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == this.ffxivWindowHandle)
            {
                // Check if spacebar is currently pressed
                bool isSpacePressed = (GetAsyncKeyState(VK_SPACE) & 0x8000) != 0;
                
                // Detect when spacebar is first pressed (edge detection)
                bool spaceJustPressed = isSpacePressed && !wasSpacePressed;
                
                // Enable hopping when spacebar is pressed
                if (isSpacePressed)
                {
                    isHoppingEnabled = true;
                    
                    // If spacebar was just pressed and character is on ground, trigger initial jump
                    if (spaceJustPressed && !InFlight)
                    {
                        SendSpacebarPress();
                        wasInFlight = false; // Reset flight state
                    }
                }
                else
                {
                    // Disable hopping when spacebar is released
                    isHoppingEnabled = false;
                    wasInFlight = false;
                }
                
                // If hopping is enabled, detect landing and auto-jump
                if (isHoppingEnabled)
                {
                    bool currentlyInFlight = InFlight;
                    
                    // Detect landing: was in flight, now not in flight = just landed
                    if (wasInFlight && !currentlyInFlight)
                    {
                        // Just landed, trigger jump automatically
                        SendSpacebarPress();
                    }
                    
                    // Update flight state for next frame
                    wasInFlight = currentlyInFlight;
                }
                
                // Update previous spacebar state
                wasSpacePressed = isSpacePressed;
            }
        }

        public void Dispose()
        {
            Service.Framework.Update -= onFrameworkUpdate;
        }
    }
}
