using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Heathen.Editor
{
    /// <summary>
    /// The single shared build hook for all settings generators (so no tool writes its own). Runtime-asset
    /// generators are regenerated when stale (safe to write data during build); source-code generators are
    /// guarded, because emitting <c>.cs</c> here would not compile in for this build, so a stale one fails
    /// the build with a clear message to regenerate first.
    /// </summary>
    internal sealed class SettingsBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            SettingsGenerators.GenerateStale(GeneratorOutput.RuntimeAsset);

            var stale = SettingsGenerators.StaleNames(GeneratorOutput.SourceCode);
            if (stale.Count > 0)
                throw new BuildFailedException(
                    "[GameFramework] Generated code is stale for: " + string.Join(", ", stale) +
                    ". Regenerate before building (Tools ▸ Heathen ▸ Game Framework ▸ Generate All Settings).");
        }
    }
}
