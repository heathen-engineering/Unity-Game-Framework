using System;

namespace Heathen
{
    /// <summary>
    /// Marks a plain serialisable class as a framework settings type and declares how it is located and
    /// delivered. The class itself is an ordinary POCO (Newtonsoft serialises it); this attribute tells
    /// the settings store where the JSON lives.
    ///
    /// <para>Example:
    /// <code>
    /// [Settings(Location = SettingsLocation.Assets, Extension = "helex", Delivery = SettingsDelivery.Runtime)]
    /// public sealed class LexiconSettings { /* fields */ }
    /// </code></para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class SettingsAttribute : Attribute
    {
        /// <summary>File base name (no extension). Defaults to the type name when null/empty.</summary>
        public string Name { get; set; }

        /// <summary>Where the file is stored. Defaults to <see cref="SettingsLocation.ProjectSettings"/>.</summary>
        public SettingsLocation Location { get; set; } = SettingsLocation.ProjectSettings;

        /// <summary>File extension (no dot). Defaults to <c>json</c>. Must be unique per type for <see cref="SettingsLocation.Assets"/>.</summary>
        public string Extension { get; set; } = "json";

        /// <summary>How the type reaches runtime. Defaults to <see cref="SettingsDelivery.BuildTime"/>.</summary>
        public SettingsDelivery Delivery { get; set; } = SettingsDelivery.BuildTime;

        /// <summary>
        /// For <see cref="SettingsLocation.ProjectFolder"/>, the top-level folder name (defaults to
        /// <see cref="Name"/>). For <see cref="SettingsLocation.Assets"/>, the project-relative directory
        /// a new file is created in when none exists yet (defaults to <c>Assets/Settings</c>); an existing
        /// file is always written back wherever it currently lives.
        /// </summary>
        public string Folder { get; set; }
    }
}
