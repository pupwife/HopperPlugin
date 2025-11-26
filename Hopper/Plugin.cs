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
                
                if (isSpacePressed && !InFlight)
                {
                    // Send spacebar keypress
                    keybd_event(VK_SPACE, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_SPACE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            }
        }

        public void Dispose()
        {
            Service.Framework.Update -= onFrameworkUpdate;
        }
    }
}
