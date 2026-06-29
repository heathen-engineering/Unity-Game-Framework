using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Heathen.Editor
{
    /// <summary>
    /// Project Settings ▸ Subsystems. The overview page for every <see cref="Subsystem"/> the project defines:
    /// name, scope (Global = one per session; World = one per World), the tick phases it opts into, and its
    /// declared dependencies (with warnings for a missing or cross-scope dependency). Discovered at edit time by
    /// type, so it lists subsystems whether or not they have any configurable settings. Each subsystem that
    /// <em>does</em> have settings registers its own page under this one (<c>Project/Subsystems/&lt;Name&gt;</c>),
    /// so the config appears as a child in the left nav. Live state at runtime is the Subsystem Debug window.
    /// </summary>
    public sealed class SubsystemsSettingsProvider : SettingsProvider
    {
        private static readonly (string name, Type iface)[] PhaseIfaces =
        {
            ("BeforeFixed",  typeof(IBeforeFixed)),
            ("OnFixed",      typeof(IOnFixed)),
            ("AfterFixed",   typeof(IAfterFixed)),
            ("BeforeUpdate", typeof(IBeforeUpdate)),
            ("OnUpdate",     typeof(IOnUpdate)),
            ("AfterUpdate",  typeof(IAfterUpdate)),
        };

        private struct Info
        {
            public Type           Type;
            public SubsystemScope Scope;
            public string         Phases;
            public Type[]         DependsOn;
            public bool           CtorInspectable;
        }

        private List<Info> _all;
        private Dictionary<Type, SubsystemScope> _scopeOf;
        private Vector2 _scroll;

        public SubsystemsSettingsProvider() : base("Project/Subsystems", SettingsScope.Project) { }

        [SettingsProvider]
        public static SettingsProvider Create() => new SubsystemsSettingsProvider
        {
            keywords = new HashSet<string>(new[] { "subsystem", "subsystems", "heathen", "framework" }),
        };

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
            => _all = null; // rebuild on (re)open so newly added subsystems appear

        public override void OnGUI(string searchContext)
        {
            if (_all == null) Discover();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(
                "Every subsystem this project defines. A subsystem's configurable settings (if any) appear as a " +
                "child page under Subsystems. To inspect the live subsystems while playing, use " +
                "Tools ▸ Heathen ▸ Game Framework ▸ Subsystem Debug.", MessageType.Info);

            DrawScope("Global Subsystems", SubsystemScope.Global);
            DrawScope("World Subsystems",  SubsystemScope.World);

            EditorGUILayout.EndScrollView();
        }

        private void DrawScope(string title, SubsystemScope scope)
        {
            var items = _all.Where(i => i.Scope == scope).ToList();

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
                EditorGUILayout.LabelField($"{title} ({items.Count})", EditorStyles.whiteLabel, GUILayout.ExpandWidth(true));

            if (items.Count == 0)
            {
                EditorGUILayout.HelpBox("None.", MessageType.None);
                return;
            }

            foreach (var info in items)
                DrawRow(info);
        }

        private void DrawRow(Info info)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(info.Type.Name, EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("Type", info.Type.FullName);
                EditorGUILayout.LabelField("Tick phases", info.Phases.Length > 0 ? info.Phases : "—");

                if (info.DependsOn.Length > 0)
                    EditorGUILayout.LabelField("Depends on", string.Join(", ", info.DependsOn.Select(d => d.Name)));

                if (!info.CtorInspectable)
                    EditorGUILayout.HelpBox(
                        "Constructor could not be inspected — a subsystem constructor must be trivial and " +
                        "side-effect-free (do real work in Initialize).", MessageType.Warning);

                foreach (var dep in info.DependsOn)
                {
                    if (!_scopeOf.TryGetValue(dep, out var depScope))
                        EditorGUILayout.HelpBox($"Missing dependency: '{dep.Name}' is not a known subsystem.", MessageType.Warning);
                    else if (depScope != info.Scope)
                        EditorGUILayout.HelpBox(
                            $"Cross-scope dependency: '{dep.Name}' is {depScope} but this subsystem is {info.Scope}. " +
                            "Dependencies are resolved within the same scope.", MessageType.Warning);
                }
            }
        }

        // ── Discovery ──────────────────────────────────────────────────────────

        private void Discover()
        {
            _all     = new List<Info>();
            _scopeOf = new Dictionary<Type, SubsystemScope>();

            foreach (var t in TypeCache.GetTypesDerivedFrom<Subsystem>())
            {
                if (t.IsAbstract) continue;
                var scope = t.GetCustomAttribute<SubsystemAttribute>(false)?.Scope ?? SubsystemScope.Global;

                Type[] deps = Array.Empty<Type>();
                bool   ok   = true;
                // The Subsystem contract requires a trivial, side-effect-free constructor specifically so a
                // type can be constructed purely to inspect (e.g. read DependsOn) — so this is sanctioned.
                try
                {
                    var instance = (Subsystem)Activator.CreateInstance(t);
                    deps = instance.DependsOn ?? Array.Empty<Type>();
                }
                catch { ok = false; }

                _all.Add(new Info { Type = t, Scope = scope, Phases = PhaseSummary(t), DependsOn = deps, CtorInspectable = ok });
                _scopeOf[t] = scope;
            }

            _all.Sort((a, b) => string.CompareOrdinal(a.Type.Name, b.Type.Name));
        }

        private static string PhaseSummary(Type t)
        {
            var list = new List<string>();
            foreach (var (name, iface) in PhaseIfaces)
                if (iface.IsAssignableFrom(t)) list.Add(name);
            return string.Join(", ", list);
        }
    }
}
