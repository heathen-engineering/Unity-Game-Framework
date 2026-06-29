namespace Heathen
{
    /// <summary>Where a settings file is authored and stored.</summary>
    public enum SettingsLocation
    {
        /// <summary>
        /// <c>ProjectSettings/&lt;Name&gt;.&lt;ext&gt;</c>. For project-wide "standard library" config (the
        /// default vocabulary a project assumes). Identity is the fixed path. Not included in player builds.
        /// </summary>
        ProjectSettings,

        /// <summary>
        /// A top-level <c>&lt;projectRoot&gt;/&lt;Folder&gt;/&lt;Name&gt;.&lt;ext&gt;</c> folder, outside
        /// <c>Assets/</c>. Identity is the fixed path. Not included in player builds.
        /// </summary>
        ProjectFolder,

        /// <summary>
        /// Anywhere under <c>Assets/</c>, located by its AssetDatabase GUID (discovered by a unique-extension
        /// scan, then cached). The dev may move the file freely and it is still found, so the location is
        /// respected. Requires a unique <see cref="SettingsAttribute.Extension"/> per type.
        /// </summary>
        Assets,
    }
}
