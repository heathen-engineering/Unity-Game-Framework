namespace Heathen.Editor
{
    /// <summary>
    /// Documentation link for the framework's own World Manager subsystem. It has no separate settings page (the
    /// Subsystems overview is its home), so it supplies only the header <c>?</c> help button, not a page link.
    /// </summary>
    public sealed class WorldManagerSubsystemDocumentation : ISubsystemDocumentation
    {
        public System.Type SubsystemType => typeof(WorldManagerSubsystem);
        public string DocumentationUrl => "https://heathen.group/kb/framework-welcome/";
    }
}
