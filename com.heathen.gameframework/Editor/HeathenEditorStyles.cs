using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Heathen.Editor
{
    /// <summary>
    /// Shared IMGUI editor styling for Heathen tools — boxed section "cards", collapsible card headers with a
    /// count and inline add button, the amber "needs build / attention" button, small add/remove buttons and a
    /// common accent palette. Lives in the framework so every tool page (and the Subsystems overview) share one
    /// consistent look instead of each re-inventing it.
    /// </summary>
    public static class HeathenEditorStyles
    {
        // ── Palette ──────────────────────────────────────────────────────────────
        public static readonly Color Accent    = new(0.36f, 0.62f, 0.96f); // links / selection
        public static readonly Color Amber      = new(1f, 0.72f, 0.10f);    // needs build / attention
        public static readonly Color AddGreen   = new(0.45f, 0.85f, 0.45f);
        public static readonly Color RemoveRed   = new(1f, 0.42f, 0.42f);

        /// <summary>A cached 1x1 solid-colour texture (editor-lifetime, not saved).</summary>
        public static Texture2D SolidTexture(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        // ── Card + header styles ───────────────────────────────────────────────────
        private static GUIStyle _card;
        /// <summary>A padded box used to group a section (built on <see cref="EditorStyles.helpBox"/>).</summary>
        public static GUIStyle Card => _card ??= new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(8, 8, 6, 8),
            margin  = new RectOffset(0, 0, 3, 3),
        };

        private static GUIStyle _cardTitle;
        public static GUIStyle CardTitle => _cardTitle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };

        // Status colours for the Build button (solid toolbar buttons).
        public static readonly Color StatusGreen = new(0.30f, 0.72f, 0.35f); // up to date / "Ready"
        public static readonly Color StatusRed    = new(0.85f, 0.32f, 0.32f); // error

        // ── Solid-colour toolbar buttons (cached per colour) ───────────────────────
        private static readonly Dictionary<Color, GUIStyle> _solidButtons = new();

        /// <summary>
        /// A toolbar button filled with a solid full-opacity colour and bold dark left-aligned text — used for the
        /// unmistakable Build/status button (amber "Update", green "Ready", red "Error"). Cached per colour.
        /// </summary>
        public static GUIStyle SolidToolbarButton(Color bg)
        {
            if (_solidButtons.TryGetValue(bg, out var s) && s != null && s.normal.background != null) return s;

            var tex = SolidTexture(bg);
            s = new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            foreach (var st in new[] { s.normal, s.onNormal, s.hover, s.onHover, s.focused, s.active, s.onActive })
            {
                st.background = tex;
                st.textColor  = Color.black;
            }
            _solidButtons[bg] = s;
            return s;
        }

        /// <summary>Solid full-opacity amber toolbar button — the "needs build / attention" state.</summary>
        public static GUIStyle AmberButton() => SolidToolbarButton(Amber);

        private static GUIStyle _plainStatus;
        private static GUIStyle PlainStatusButton => _plainStatus ??=
            new GUIStyle(EditorStyles.toolbarButton) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };

        // ── Standard "build status" button ─────────────────────────────────────────

        /// <summary>The generated-output state a tool's Build button reflects.</summary>
        public enum BuildStatus { UpToDate, NotBuilt, Dirty, Error }

        /// <summary>Map an <see cref="ISettingsGenerator"/> to a status (Dirty / UpToDate / Error). Tools that can
        /// tell "never built" apart from "stale" should compute <see cref="BuildStatus"/> themselves.</summary>
        public static BuildStatus StatusOf(ISettingsGenerator generator)
        {
            try   { return generator.IsStale() ? BuildStatus.Dirty : BuildStatus.UpToDate; }
            catch { return BuildStatus.Error; }
        }

        /// <summary>
        /// The standard Heathen Build/status toolbar button — always icon + label, colour by state
        /// (Ready green / Build default / Update amber / Error red). Returns true when clicked (the caller runs its
        /// own generate). Shared so every tool's Build affordance looks and reads the same.
        /// </summary>
        public static bool BuildStatusButton(BuildStatus status, float width = 84f)
        {
            (string label, GUIStyle style, string tip) = status switch
            {
                BuildStatus.UpToDate => ("Ready",  SolidToolbarButton(StatusGreen), "Generated output is up to date. Click to regenerate."),
                BuildStatus.Dirty    => ("Update", AmberButton(),                   "Changes since the last build. Click to rebuild."),
                BuildStatus.Error    => ("Error",  SolidToolbarButton(StatusRed),   "Could not determine the build state. Click to try building."),
                _                    => ("Build",  PlainStatusButton,               "Nothing has been built yet. Click to build."),
            };
            var icon = EditorGUIUtility.IconContent("BuildSettings.Editor.Small").image;
            return GUILayout.Button(new GUIContent(" " + label, icon, tip), style, GUILayout.Width(width));
        }

        // ── Layout helpers ─────────────────────────────────────────────────────────

        /// <summary>Open a card: <c>using (HeathenEditorStyles.BeginCard()) { ... }</c>.</summary>
        public static EditorGUILayout.VerticalScope BeginCard() => new EditorGUILayout.VerticalScope(Card);

        /// <summary>
        /// Indents a block of controls by a fixed pixel amount that actually works inside row/horizontal layouts
        /// (unlike <see cref="EditorGUI.indentLevel"/>, which control rows ignore). Use as
        /// <c>using (HeathenEditorStyles.Indent()) { ... }</c>. Nestable.
        /// </summary>
        public readonly struct IndentScope : IDisposable
        {
            private readonly EditorGUILayout.HorizontalScope _h;
            private readonly EditorGUILayout.VerticalScope   _v;

            public IndentScope(float pixels)
            {
                _h = new EditorGUILayout.HorizontalScope();
                GUILayout.Space(pixels);
                _v = new EditorGUILayout.VerticalScope();
            }

            public void Dispose()
            {
                _v.Dispose();
                _h.Dispose();
            }
        }

        public static IndentScope Indent(float pixels = 14f) => new IndentScope(pixels);

        /// <summary>
        /// A collapsible card header: content-sized foldout ("Title  (count)") on the left, an optional
        /// right-aligned green add button. Returns the new expanded state.
        /// </summary>
        public static bool CardHeader(bool expanded, string title, int count, Action onAdd = null, string addTooltip = "Add")
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = GUILayout.Toggle(expanded, $"{title}  ({count})", EditorStyles.foldout, GUILayout.ExpandWidth(false));
                if (onAdd != null)
                {
                    GUILayout.FlexibleSpace();
                    var c = GUI.contentColor;
                    GUI.contentColor = AddGreen;
                    if (GUILayout.Button(new GUIContent("+", addTooltip), EditorStyles.miniButton, GUILayout.Width(26)))
                        onAdd();
                    GUI.contentColor = c;
                }
            }
            return expanded;
        }

        /// <summary>A small right-aligned red "×" remove button. Returns true when clicked.</summary>
        public static bool RemoveButton(string tooltip = "Remove")
        {
            var c = GUI.contentColor;
            GUI.contentColor = RemoveRed;
            bool clicked = GUILayout.Button(new GUIContent("×", tooltip), EditorStyles.miniButton, GUILayout.Width(22));
            GUI.contentColor = c;
            return clicked;
        }

        /// <summary>A faint full-width horizontal divider.</summary>
        public static void HorizontalLine(float height = 1f)
        {
            var r = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.25f));
        }
    }
}
