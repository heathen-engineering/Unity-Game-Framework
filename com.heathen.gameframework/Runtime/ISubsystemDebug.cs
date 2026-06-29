using System.Collections.Generic;

namespace Heathen
{
    /// <summary>
    /// Optional contract a <see cref="Subsystem"/> implements to surface its own live state in the Subsystem
    /// Debug window as label/value rows (e.g. GameplayTags → registered-tag count; Storyteller → session count).
    /// A pure runtime contract with no editor dependency, so any package's subsystem can implement it; the
    /// debug window discovers it by type at runtime. Return cheap-to-compute values — it is read each repaint.
    /// </summary>
    public interface ISubsystemDebug
    {
        /// <summary>Live label/value rows describing this subsystem's current state.</summary>
        IEnumerable<(string label, string value)> GetDebugInfo();
    }
}
