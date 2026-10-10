using StardewModdingAPI;

namespace FarmingCapitalist.Integrations;

// Minimal API surface from Generic Mod Config Menu's public interface:
// https://github.com/spacechase0/StardewValleyMods/blob/develop/framework/GenericModConfigMenu/IGenericModConfigMenuApi.cs
public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
    void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue,
        Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
}
