using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Heathen.Editor
{
    /// <summary>
    /// Runtime monitor for the framework's subsystems: while playing it lists the live Global subsystems and
    /// every World's subsystems, with each one's initialised state, dependency order, the tick phases it is
    /// registered for, and any custom live stats it exposes via <see cref="ISubsystemDebug"/>. A read-only view
    /// over the framework's public surface — it adds no runtime cost beyond reading current state.
    /// </summary>
    public sealed class SubsystemDebugWindow : EditorWindow
    {
        private Vector2 _scroll;
        private readonly Dictionary<string, bool> _expanded = new();

        // The six opt-in tick phases, in frame order, paired with the interface that opts a subsystem in.
        private static readonly (string name, Type iface)[] Phases =
        {
            ("BeforeFixed",  typeof(IBeforeFixed)),
            ("OnFixed",      typeof(IOnFixed)),
            ("AfterFixed",   typeof(IAfterFixed)),
            ("BeforeUpdate", typeof(IBeforeUpdate)),
            ("OnUpdate",     typeof(IOnUpdate)),
            ("AfterUpdate",  typeof(IAfterUpdate)),
        };

        [MenuItem("Tools/Heathen/Game Framework/Subsystem Debug")]
        public static void Open() => GetWindow<SubsystemDebugWindow>("Subsystem Debug");

        // Repaint a few times a second while playing so live stats stay current without per-frame cost.
        private void OnInspectorUpdate()
        {
            if (Application.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !GameFramework.IsBooted)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "The framework boots in Play Mode. Enter Play Mode to inspect the live subsystems.",
                    MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            Header($"Global Subsystems ({GameFramework.GlobalSubsystems.Count})");
            for (int i = 0; i < GameFramework.GlobalSubsystems.Count; i++)
                DrawSubsystem(GameFramework.GlobalSubsystems[i], i, "global");

            var wm = GameFramework.Get<WorldManagerSubsystem>();
            if (wm != null)
            {
                foreach (var world in wm.Worlds)
                    DrawWorld(world, wm.Main == world);
            }

            EditorGUILayout.EndScrollView();
        }

        // ── Worlds ───────────────────────────────────────────────────────────

        private void DrawWorld(World world, bool isMain)
        {
            EditorGUILayout.Space(6);
            var title = $"World: {world.Name}" + (isMain ? "  (main)" : "") + (world.IsAlive ? "" : "  [disposed]");
            Header(title);

            using (new EditorGUI.IndentLevelScope())
            {
                Row("State",   world.State?.GetType().Name ?? "(none)");
                Row("Mode",    world.Mode?.GetType().Name ?? "(none)");
                Row("Players", world.Players.Count.ToString());

                EditorGUILayout.LabelField($"Subsystems ({world.Subsystems.Count})", EditorStyles.miniBoldLabel);
                for (int i = 0; i < world.Subsystems.Count; i++)
                    DrawSubsystem(world.Subsystems[i], i, "world:" + world.Name);
            }
        }

        // ── Subsystems ─────────────────────────────────────────────────────────

        private void DrawSubsystem(Subsystem s, int order, string scopeKey)
        {
            var type = s.GetType();
            string key = scopeKey + "/" + type.FullName;

            string status = s.IsInitialised ? "●" : "○";
            string phases = PhaseSummary(s);
            string header = $"{status} {order}. {type.Name}" + (phases.Length > 0 ? $"   [{phases}]" : "");

            bool exp = _expanded.GetValueOrDefault(key, false);
            bool now = EditorGUILayout.Foldout(exp, header, true);
            if (now != exp) _expanded[key] = now;
            if (!now) return;

            using (new EditorGUI.IndentLevelScope())
            {
                Row("Type",        type.FullName);
                Row("Scope",       s.Scope.ToString());
                Row("Initialised", s.IsInitialised ? "yes" : "no");
                Row("Tick phases", phases.Length > 0 ? phases : "—");

                var deps = s.DependsOn;
                if (deps != null && deps.Length > 0)
                    Row("Depends on", string.Join(", ", Array.ConvertAll(deps, d => d.Name)));

                if (s is ISubsystemDebug dbg)
                {
                    EditorGUILayout.Space(2);
                    IEnumerable<(string label, string value)> rows = null;
                    try { rows = dbg.GetDebugInfo(); }
                    catch (Exception e) { Row("debug error", e.Message); }
                    if (rows != null)
                        foreach (var (label, value) in rows)
                            Row(label, value);
                }
            }
        }

        private static string PhaseSummary(Subsystem s)
        {
            var list = new List<string>();
            foreach (var (name, iface) in Phases)
                if (iface.IsInstanceOfType(s)) list.Add(name);
            return string.Join(", ", list);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static void Header(string text)
        {
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
                EditorGUILayout.LabelField(text, EditorStyles.whiteLabel, GUILayout.ExpandWidth(true));
        }

        private static void Row(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(140));
                EditorGUILayout.SelectableLabel(value ?? "", EditorStyles.label,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }
    }
}
