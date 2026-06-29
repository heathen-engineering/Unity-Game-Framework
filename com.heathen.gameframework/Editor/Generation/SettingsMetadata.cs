using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Heathen.Editor
{
    /// <summary>
    /// Cross-tool metadata registry. Holds every <see cref="ISettingsMetadataProvider"/> in the project
    /// (discovered by type) and lets a consumer query them by any domain-contract interface the providers
    /// implement, so e.g. a HATE Forge can read tag vocabularies without referencing the tag tool or
    /// re-scanning files. The framework stays domain-agnostic: it knows nothing of the contract types.
    /// </summary>
    public static class SettingsMetadata
    {
        private static List<ISettingsMetadataProvider> _providers;

        /// <summary>All registered providers (discovered once per domain load).</summary>
        public static IReadOnlyList<ISettingsMetadataProvider> Providers => _providers ??= Discover();

        /// <summary>Every provider that implements the contract <typeparamref name="T"/>.</summary>
        public static IEnumerable<T> All<T>() where T : class
        {
            foreach (var p in Providers)
                if (p is T t) yield return t;
        }

        /// <summary>The first provider implementing <typeparamref name="T"/>, or null.</summary>
        public static T First<T>() where T : class => All<T>().FirstOrDefault();

        private static List<ISettingsMetadataProvider> Discover()
        {
            var list = new List<ISettingsMetadataProvider>();
            foreach (var t in TypeCache.GetTypesDerivedFrom<ISettingsMetadataProvider>())
            {
                if (t.IsAbstract || t.IsInterface) continue;
                try { list.Add((ISettingsMetadataProvider)Activator.CreateInstance(t)); }
                catch (Exception e) { Debug.LogError($"[GameFramework] Could not construct metadata provider '{t.FullName}': {e}"); }
            }
            return list;
        }
    }
}
