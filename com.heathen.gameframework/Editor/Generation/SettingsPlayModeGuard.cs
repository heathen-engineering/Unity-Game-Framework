using UnityEditor;

namespace Heathen.Editor
{
    /// <summary>
    /// Keeps generated settings up to date before the editor enters Play mode, so you always test what would
    /// ship rather than a stale or unbuilt artifact. This is the play-mode analogue of
    /// <see cref="SettingsBuildPreprocessor"/>, and a fundamental piece every tool inherits: any
    /// <see cref="ISettingsGenerator"/> participates automatically.
    /// <para>
    /// Runtime-asset generators are regenerated silently (safe to write during the transition). Source-code
    /// generators require a recompile, so they are never emitted mid-transition — Play is held and the dev is
    /// offered <b>Build &amp; Play</b> / <b>Play Anyway</b> / <b>Cancel</b>. <b>Build &amp; Play</b> generates in
    /// edit mode (where a recompile is clean) and resumes into Play once the reload completes.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    internal static class SettingsPlayModeGuard
    {
        private const string AutoPlayKey = "Heathen.GameFramework.AutoPlayAfterBuild";

        // Set when the dev chose "Play Anyway" (or we resume after a build): skip the check for that one
        // transition so entering Play doesn't immediately re-trigger the prompt (an infinite loop).
        private static bool _bypassNext;

        static SettingsPlayModeGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            // Resume into Play after a "Build & Play" recompile + domain reload has completed.
            if (SessionState.GetBool(AutoPlayKey, false))
            {
                SessionState.SetBool(AutoPlayKey, false);
                EditorApplication.delayCall += () =>
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        _bypassNext = true;
                        EditorApplication.isPlaying = true;
                    }
                };
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;

            if (_bypassNext) { _bypassNext = false; return; }

            // Data assets are safe to (re)write during the transition.
            SettingsGenerators.GenerateStale(GeneratorOutput.RuntimeAsset);

            var stale = SettingsGenerators.StaleNames(GeneratorOutput.SourceCode);
            if (stale.Count == 0) return;

            // Emitting .cs now would not be compiled into this session, so bounce out of the transition,
            // then ask + build in edit mode where the recompile is clean.
            EditorApplication.isPlaying = false;
            string names = string.Join(", ", stale);
            EditorApplication.delayCall += () => Prompt(names);
        }

        private static void Prompt(string names)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Unbuilt changes",
                "Generated code is stale for: " + names +
                ".\n\nBuild before entering Play so you test what will ship?",
                "Build & Play",  // 0
                "Cancel",        // 1
                "Play Anyway");  // 2

            switch (choice)
            {
                case 0:
                    // Generate in edit mode; the resulting recompile + domain reload resumes Play (see ctor).
                    SessionState.SetBool(AutoPlayKey, true);
                    if (SettingsGenerators.GenerateStale(GeneratorOutput.SourceCode) == 0)
                        SessionState.SetBool(AutoPlayKey, false); // nothing regenerated → no reload to resume on
                    break;
                case 2:
                    _bypassNext = true;                 // don't re-prompt for this entry
                    EditorApplication.isPlaying = true; // run the existing baked code as-is
                    break;
                // case 1: Cancel — stay in edit mode.
            }
        }
    }
}
