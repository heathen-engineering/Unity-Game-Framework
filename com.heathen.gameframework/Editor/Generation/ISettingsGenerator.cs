namespace Heathen.Editor
{
    /// <summary>What a generator emits, which decides how the build hook treats it.</summary>
    public enum GeneratorOutput
    {
        /// <summary>
        /// Emits C# source (<c>.cs</c>). Cannot be regenerated mid-build (no recompile happens for that
        /// build), so generation stays deliberate and the build hook guards: it fails the build if the
        /// output is stale rather than silently producing code that will not compile in.
        /// </summary>
        SourceCode,

        /// <summary>
        /// Emits a runtime-loadable data artefact (e.g. baked JSON/binary under StreamingAssets for a
        /// <see cref="SettingsDelivery.Runtime"/> settings type). Safe to (re)produce during build
        /// preprocess, so the build hook regenerates it automatically when stale.
        /// </summary>
        RuntimeAsset,
    }

    /// <summary>
    /// Implemented by a tool to participate in the shared generation pipeline (build hook + on-demand +
    /// staleness nudges). The framework owns the plumbing; the tool keeps its own bake logic in
    /// <see cref="Generate"/> (typically reading its settings via <see cref="SettingsStore"/>). Discovered
    /// by type, so a tool just needs a class implementing this with a trivial constructor.
    /// </summary>
    public interface ISettingsGenerator
    {
        /// <summary>Display name for logs, menus and the build-failure message.</summary>
        string Name { get; }

        /// <summary>What this generator emits (drives build-hook behaviour).</summary>
        GeneratorOutput Output { get; }

        /// <summary>True if the generated output is behind its source. Return true when unsure.</summary>
        bool IsStale();

        /// <summary>Run the bake. May write files; callers refresh the AssetDatabase afterwards.</summary>
        void Generate();
    }
}
