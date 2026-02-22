using IPA.Config.Stores;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace BetterMusicPacks.Configuration;

internal class PluginConfig
{
    public static PluginConfig Instance { get; set; } = null!;
    public virtual bool Enabled { get; set; } = true;
}