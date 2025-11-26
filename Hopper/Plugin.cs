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
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int VK_SPACE = 0x20;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private readonly IntPtr ffxivWindowHandle;
        private bool wasInFlight = false;
        private bool isHoppingActive = false; // Only set to true when spacebar is held, false when released

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
            // Use keybd_event - simpler and more reliable
            keybd_event(VK_SPACE, 0, 0, UIntPtr.Zero);
            keybd_event(VK_SPACE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public void onFrameworkUpdate(IFramework framework)
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == this.ffxivWindowHandle)
            {
                // ONLY check spacebar - ignore all other keys completely
                bool isSpacePressed = (GetAsyncKeyState(VK_SPACE) & 0x8000) != 0;
                bool currentlyInFlight = InFlight;
                
                // Enable hopping when spacebar is pressed, disable when released
                // This state persists regardless of other key presses
                if (isSpacePressed)
                {
                    if (!isHoppingActive)
                    {
                        // Spacebar just pressed - activate hopping
                        isHoppingActive = true;
                        wasInFlight = currentlyInFlight;
                        
                        // If we're on the ground when spacebar is first pressed, jump immediately
                        if (!currentlyInFlight)
                        {
                            SendSpacebarPress();
                        }
                    }
                }
                else
                {
                    // Spacebar released - deactivate hopping
                    isHoppingActive = false;
                    wasInFlight = false;
                }
                
                // If hopping is active (spacebar held), detect landing and auto-jump
                // This ONLY responds to spacebar state and landing events - ignores all other keys
                if (isHoppingActive)
                {
                    // Detect landing: transition from in-flight to on-ground
                    if (wasInFlight && !currentlyInFlight)
                    {
                        // Just landed - jump immediately
                        SendSpacebarPress();
                    }
                    
                    // Update flight state for next frame
                    wasInFlight = currentlyInFlight;
                }
            }
        }

        public void Dispose()
        {
            Service.Framework.Update -= onFrameworkUpdate;
        }
    }
}
