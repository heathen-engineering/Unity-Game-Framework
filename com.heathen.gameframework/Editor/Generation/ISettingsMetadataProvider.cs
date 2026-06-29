namespace Heathen.Editor
{
    /// <summary>
    /// Marker for a type that exposes editor-time metadata about a settings source, so other editor tools
    /// can read it through <see cref="SettingsMetadata"/> without re-scanning the project or referencing
    /// the owning tool. A provider additionally implements one or more <i>domain contract</i> interfaces
    /// (defined by whoever needs them, not by this framework, which stays domain-agnostic), and consumers
    /// query by that contract. For example a tag tool's provider implements both this marker and a
    /// <c>ITagVocabulary { IEnumerable&lt;string&gt; Tags }</c> contract; a consumer calls
    /// <c>SettingsMetadata.First&lt;ITagVocabulary&gt;()</c>.
    ///
    /// <para>Discovered by type; provider methods should read live data on call (e.g. via
    /// <see cref="SettingsStore"/>) so results reflect current edits.</para>
    /// </summary>
    public interface ISettingsMetadataProvider { }
}
