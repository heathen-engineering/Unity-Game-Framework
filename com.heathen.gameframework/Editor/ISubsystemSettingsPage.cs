using System;

namespace Heathen.Editor
{
    /// <summary>
    /// Editor contract a tool implements so clicking a subsystem's card header on the Subsystems overview does
    /// something useful — normally jump to that tool's own settings page (the same as selecting it in the left
    /// nav), but the tool decides. The framework only reports the click; the subsystem chooses what to open or
    /// select. Discovered by type via <c>TypeCache</c>, like <see cref="ISubsystemConfigEditor"/> and
    /// <see cref="ISubsystemHealth"/>. Subsystems that don't implement it keep a plain, non-clickable header.
    /// </summary>
    public interface ISubsystemSettingsPage
    {
        /// <summary>The concrete <see cref="Subsystem"/> type whose header this responds to.</summary>
        Type SubsystemType { get; }

        /// <summary>Invoked when the subsystem's card header is clicked. The tool decides what to open/select
        /// (typically <c>SettingsService.OpenProjectSettings("Project/Subsystems/&lt;Name&gt;")</c>).</summary>
        void Open();
    }

    /// <summary>
    /// Editor contract a tool implements to add a documentation <c>?</c> help button beside a subsystem's card
    /// header on the Subsystems overview. Separate from <see cref="ISubsystemSettingsPage"/> because a subsystem
    /// may have docs but no distinct settings page (e.g. the framework's own World Manager). Discovered by type.
    /// </summary>
    public interface ISubsystemDocumentation
    {
        /// <summary>The concrete <see cref="Subsystem"/> type this documentation is for.</summary>
        Type SubsystemType { get; }

        /// <summary>Documentation URL opened by the header's <c>?</c> button.</summary>
        string DocumentationUrl { get; }
    }
}
