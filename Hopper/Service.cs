using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects;

namespace Hopper
{
    internal class Service
    {
        [PluginService] public static ICondition Condition { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
    }
}
