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
                        // Send spacebar keypress
                        keybd_event(VK_SPACE, 0, 0, UIntPtr.Zero);
                        keybd_event(VK_SPACE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
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
