using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Heathen.Editor
{
    /// <summary>
    /// Registry of every <see cref="ISettingsGenerator"/> in the project (discovered by type). Drives the
    /// build hook and on-demand generation. Each generator owns its own bake; this only orchestrates.
    /// </summary>
    public static class SettingsGenerators
    {
        private static List<ISettingsGenerator> _all;

        /// <summary>All registered generators (discovered once per domain load).</summary>
        public static IReadOnlyList<ISettingsGenerator> All => _all ??= Discover();

        /// <summary>Regenerate every generator, then refresh the AssetDatabase.</summary>
        public static void GenerateAll()
        {
            foreach (var g in All) SafeGenerate(g);
            AssetDatabase.Refresh();
        }

        /// <summary>Regenerate only stale generators (optionally of one output kind). Returns the count.</summary>
        public static int GenerateStale(GeneratorOutput? filter = null)
        {
            int count = 0;
            foreach (var g in All)
            {
                if (filter.HasValue && g.Output != filter.Value) continue;
                if (SafeIsStale(g)) { SafeGenerate(g); count++; }
            }
            if (count > 0) AssetDatabase.Refresh();
            return count;
        }

        /// <summary>Names of stale generators (optionally of one output kind), for nudges / build messages.</summary>
        public static List<string> StaleNames(GeneratorOutput? filter = null)
        {
            var names = new List<string>();
            foreach (var g in All)
            {
                if (filter.HasValue && g.Output != filter.Value) continue;
                if (SafeIsStale(g)) names.Add(g.Name);
            }
            return names;
        }

        private static List<ISettingsGenerator> Discover()
        {
            var list = new List<ISettingsGenerator>();
            foreach (var t in TypeCache.GetTypesDerivedFrom<ISettingsGenerator>())
            {
                if (t.IsAbstract || t.IsInterface) continue;
                try { list.Add((ISettingsGenerator)Activator.CreateInstance(t)); }
                catch (Exception e) { Debug.LogError($"[GameFramework] Could not construct generator '{t.FullName}': {e}"); }
            }
            return list;
        }

        private static void SafeGenerate(ISettingsGenerator g)
        {
            try { g.Generate(); }
            catch (Exception e) { Debug.LogError($"[GameFramework] Generator '{g.Name}' failed: {e}"); }
        }

        private static bool SafeIsStale(ISettingsGenerator g)
        {
            try { return g.IsStale(); }
            catch (Exception e) { Debug.LogError($"[GameFramework] Generator '{g.Name}'.IsStale threw: {e}"); return false; }
        }
    }
}
