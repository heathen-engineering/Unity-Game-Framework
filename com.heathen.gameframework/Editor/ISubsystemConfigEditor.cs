using System;

namespace Heathen.Editor
{
    /// <summary>
    /// Editor contract a tool implements so the <c>Project ▸ Subsystems</c> page can edit a subsystem's
    /// <see cref="SubsystemStartMode"/> from one standard place. The framework owns the UI; the tool owns where
    /// the value is stored and how it reaches runtime (e.g. baked into generated code) — this just exposes a
    /// get/set plus an optional apply hint. Implementers are discovered by type via <c>TypeCache</c>, the same
    /// way the framework discovers its settings metadata providers, so the framework needs no reference to the
    /// tool's assembly.
    /// </summary>
    public interface ISubsystemConfigEditor
    {
        /// <summary>The concrete <see cref="Subsystem"/> type this editor configures.</summary>
        Type SubsystemType { get; }

        /// <summary>
        /// The developer-selected start mode, read and written through the tool's own storage (the setter
        /// should persist the change). The overview binds a dropdown to this.
        /// </summary>
        SubsystemStartMode StartMode { get; set; }

        /// <summary>
        /// Optional note shown beside the control — e.g. how to make the change take effect at runtime
        /// ("regenerate code"). Return <c>null</c> or empty for none.
        /// </summary>
        string ApplyHint { get; }
    }
}
