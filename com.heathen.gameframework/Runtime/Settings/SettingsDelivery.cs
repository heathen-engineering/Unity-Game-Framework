namespace Heathen
{
    /// <summary>
    /// How a settings type reaches runtime. Authoring location (see <see cref="SettingsLocation"/>) is a
    /// separate question: <c>ProjectSettings/</c> and loose <c>Assets/</c> JSON are not loaded at runtime,
    /// so a runtime-readable type must be delivered (baked or compiled to a loadable artefact). The
    /// delivery step itself is per-tool / handled by the generator registry, not by the locate+serialise core.
    /// </summary>
    public enum SettingsDelivery
    {
        /// <summary>Consumed only in the editor / at build time (e.g. codegen input); the baked output runs.</summary>
        BuildTime,

        /// <summary>Must be readable at runtime; the owning tool delivers it (bake / loadable artefact).</summary>
        Runtime,
    }
}
