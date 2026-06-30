using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Heathen.Editor
{
    /// <summary>
    /// Locates, reads and writes a <see cref="SettingsAttribute"/>-annotated POCO as JSON, from whichever
    /// of the three <see cref="SettingsLocation"/>s the type declares. Editor-only: <c>ProjectSettings/</c>
    /// and the top-level project folder are not in player builds, and the <see cref="SettingsLocation.Assets"/>
    /// resolution uses the AssetDatabase. Runtime delivery of <see cref="SettingsDelivery.Runtime"/> types
    /// is a separate, per-tool concern (bake / loadable artefact).
    /// </summary>
    public static class SettingsStore
    {
        // Converters applied to every settings read/write. Seeded with the Unity type converters; a tool adds
        // its own domain value-type converters via AddConverter (e.g. a type whose data lives in a private
        // field, or one that should serialise as a single scalar). Mutating the list is reflected on the next
        // load/save because JsonConvert builds a serializer from these settings per call.
        private static readonly List<JsonConverter> _converters = new(UnityJson.Converters);

        private static readonly JsonSerializerSettings Json = new()
        {
            Formatting        = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Converters        = _converters,
        };

        /// <summary>
        /// Register an extra <see cref="JsonConverter"/> applied to all settings load and save. Idempotent.
        /// Call from an editor <c>[InitializeOnLoad]</c> so it is in place before any settings are read — this
        /// is how a tool teaches the store to serialise a domain value-type (for example one backed by a
        /// private field that the default contract would drop, or that carries API-backed computed properties).
        /// </summary>
        public static void AddConverter(JsonConverter converter)
        {
            if (converter != null && !_converters.Contains(converter))
                _converters.Add(converter);
        }

        // Cached AssetDatabase GUID per Assets-located type. The GUID follows the file across moves and
        // renames, so a cached entry keeps resolving correctly until the file is deleted.
        private static readonly Dictionary<Type, string> _assetGuid = new();

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        // ── Load / save ─────────────────────────────────────────────────────────────────────

        /// <summary>Read the settings, or a fresh default instance if no file exists yet.</summary>
        public static T Load<T>() where T : class, new() => (T)Load(typeof(T));

        /// <summary>Non-generic <see cref="Load{T}"/>.</summary>
        public static object Load(Type type)
        {
            var meta = Meta.For(type);
            var path = ResolvePath(meta, forWrite: false);

            if (path == null || !File.Exists(path))
                return Activator.CreateInstance(type);

            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject(json, type, Json) ?? Activator.CreateInstance(type);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameFramework] Failed to read settings '{type.Name}' from '{path}': {e}");
                return Activator.CreateInstance(type);
            }
        }

        /// <summary>Write the settings back to its file, creating it (and folders) if needed.</summary>
        public static void Save<T>(T value) where T : class => Save(typeof(T), value);

        /// <summary>Non-generic <see cref="Save{T}"/>.</summary>
        public static void Save(Type type, object value)
        {
            var meta = Meta.For(type);
            var path = ResolvePath(meta, forWrite: true);
            if (path == null) return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(value, Json));

                if (meta.Location == SettingsLocation.Assets)
                {
                    var rel = ToProjectRelative(path);
                    if (rel != null)
                    {
                        AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);
                        _assetGuid[type] = AssetDatabase.AssetPathToGUID(rel);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameFramework] Failed to write settings '{type.Name}' to '{path}': {e}");
            }
        }

        // ── Queries ─────────────────────────────────────────────────────────────────────────

        /// <summary>True if a file currently exists for the type.</summary>
        public static bool Exists<T>() => Exists(typeof(T));

        /// <summary>True if a file currently exists for the type.</summary>
        public static bool Exists(Type type)
        {
            var path = ResolvePath(Meta.For(type), forWrite: false);
            return path != null && File.Exists(path);
        }

        /// <summary>The absolute path the type resolves to (an existing file, or where one would be created).</summary>
        public static string GetPath<T>() => GetPath(typeof(T));

        /// <summary>The absolute path the type resolves to (an existing file, or where one would be created).</summary>
        public static string GetPath(Type type) => ResolvePath(Meta.For(type), forWrite: true);

        // ── Path resolution ───────────────────────────────────────────────────────────────────

        // Returns the absolute path. For read: null when no file exists (so Load returns a default).
        // For write: always a path (an existing file's current location, else a fresh default location).
        private static string ResolvePath(Meta meta, bool forWrite)
        {
            switch (meta.Location)
            {
                case SettingsLocation.ProjectSettings:
                    return Combine(ProjectRoot, "ProjectSettings", meta.FileName);

                case SettingsLocation.ProjectFolder:
                    return Combine(ProjectRoot, meta.Folder ?? meta.Name, meta.FileName);

                case SettingsLocation.Assets:
                    return ResolveAssetsPath(meta, forWrite);

                default:
                    return null;
            }
        }

        private static string ResolveAssetsPath(Meta meta, bool forWrite)
        {
            // 1. A cached GUID that still resolves wins (respects moves/renames).
            if (_assetGuid.TryGetValue(meta.Type, out var guid))
            {
                var rel = AssetDatabase.GUIDToAssetPath(guid);
                var full = ToFull(rel);
                if (full != null && File.Exists(full)) return full;
                _assetGuid.Remove(meta.Type);
            }

            // 2. Scan Assets for the type's unique extension.
            var matches = Directory.GetFiles(Application.dataPath, "*." + meta.Extension, SearchOption.AllDirectories);
            if (matches.Length > 0)
            {
                Array.Sort(matches, StringComparer.Ordinal);
                if (matches.Length > 1)
                    Debug.LogWarning($"[GameFramework] Multiple '*.{meta.Extension}' files found for settings " +
                                     $"'{meta.Type.Name}'; using '{matches[0]}'. Give the type a unique extension.");

                var full = matches[0].Replace('\\', '/');
                var rel  = ToProjectRelative(full);
                if (rel != null) _assetGuid[meta.Type] = AssetDatabase.AssetPathToGUID(rel);
                return full;
            }

            // 3. None found.
            if (!forWrite) return null;
            var dir = (meta.Folder ?? "Assets/Settings").TrimEnd('/');
            return Combine(ProjectRoot, null, dir + "/" + meta.FileName);
        }

        // ── Path helpers ──────────────────────────────────────────────────────────────────────

        private static string Combine(string root, string sub, string file)
        {
            var p = sub == null ? $"{root}/{file}" : $"{root}/{sub}/{file}";
            return p.Replace('\\', '/');
        }

        private static string ToFull(string projectRelative)
        {
            if (string.IsNullOrEmpty(projectRelative)) return null;
            return $"{ProjectRoot}/{projectRelative}".Replace('\\', '/');
        }

        private static string ToProjectRelative(string fullPath)
        {
            var data = Application.dataPath.Replace('\\', '/');
            fullPath = fullPath.Replace('\\', '/');
            if (fullPath.StartsWith(data, StringComparison.Ordinal))
                return "Assets" + fullPath.Substring(data.Length);
            return null;
        }

        // ── Attribute metadata ──────────────────────────────────────────────────────────────────

        private readonly struct Meta
        {
            public readonly Type             Type;
            public readonly string           Name;
            public readonly string           Extension;
            public readonly SettingsLocation Location;
            public readonly SettingsDelivery Delivery;
            public readonly string           Folder;

            public string FileName => $"{Name}.{Extension}";

            private Meta(Type type, SettingsAttribute a)
            {
                Type      = type;
                Name      = string.IsNullOrEmpty(a?.Name) ? type.Name : a.Name;
                Extension = (string.IsNullOrEmpty(a?.Extension) ? "json" : a.Extension).TrimStart('.');
                Location  = a?.Location ?? SettingsLocation.ProjectSettings;
                Delivery  = a?.Delivery ?? SettingsDelivery.BuildTime;
                Folder    = a?.Folder;
            }

            public static Meta For(Type type) => new Meta(type, type.GetCustomAttribute<SettingsAttribute>(true));
        }
    }
}
