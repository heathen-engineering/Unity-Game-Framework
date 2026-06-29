using UnityEditor;

namespace Heathen.Editor
{
    internal static class GameFrameworkMenu
    {
        [MenuItem("Tools/Heathen/Game Framework/Generate All Settings")]
        private static void GenerateAll() => SettingsGenerators.GenerateAll();

        [MenuItem("Tools/Heathen/Game Framework/Generate Stale Settings")]
        private static void GenerateStale() => SettingsGenerators.GenerateStale();
    }
}
