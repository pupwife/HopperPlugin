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
        private bool wasSpacePressed = false;
        private int jumpCooldown = 0;
        private const int JUMP_COOLDOWN_FRAMES = 5; // Wait 5 frames between jumps to avoid spamming

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
                // Check if spacebar is currently pressed
                bool isSpacePressed = (GetAsyncKeyState(VK_SPACE) & 0x8000) != 0;
                bool currentlyInFlight = InFlight;
                
                // Decrement cooldown
                if (jumpCooldown > 0)
                {
                    jumpCooldown--;
                }
                
                // If spacebar is held and we're not in flight (on ground) and cooldown is ready
                if (isSpacePressed && !currentlyInFlight && jumpCooldown == 0)
                {
                    // Send jump
                    SendSpacebarPress();
                    jumpCooldown = JUMP_COOLDOWN_FRAMES; // Set cooldown
                    wasInFlight = false; // Reset state
                }
                
                // Track flight state for debugging/edge cases
                wasInFlight = currentlyInFlight;
                wasSpacePressed = isSpacePressed;
            }
        }

        public void Dispose()
        {
            Service.Framework.Update -= onFrameworkUpdate;
        }
    }
}
