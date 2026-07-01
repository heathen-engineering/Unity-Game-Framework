using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace Heathen.Editor
{
    /// <summary>
    /// A Scene-view overlay chip that appears only while a subsystem needs attention (Warning or worse, per
    /// <see cref="SubsystemHealth"/>). Always visible while editing so setup problems are not missed; clicking it
    /// opens <c>Project ▸ Subsystems</c>. Hidden when everything is healthy. Polled on a light cadence so it does
    /// not recompute health every frame.
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "Subsystem Attention", defaultDisplay = false)]
    [Icon("console.warnicon")]
    public class SubsystemAttentionOverlay : Overlay
    {
        private const string OverlayId = "heathen-subsystem-attention";
        private const double PollSeconds = 2.0;

        private Button _button;
        private double _lastCheck;
        private SubsystemHealthSeverity _worst = SubsystemHealthSeverity.Ok;

        // Runs whether or not the panel content is currently built, so the chip can show itself when a problem
        // first appears (content is created lazily once it becomes visible).
        public override void OnCreated()      => EditorApplication.update += Tick;
        public override void OnWillBeDestroyed() => EditorApplication.update -= Tick;

        public override VisualElement CreatePanelContent()
        {
            _button = new Button(() => SettingsService.OpenProjectSettings("Project/Subsystems"));
            _button.style.unityFontStyleAndWeight = FontStyle.Bold;
            var root = new VisualElement();
            root.Add(_button);
            UpdateLabel();
            return root;
        }

        private void Tick()
        {
            if (EditorApplication.timeSinceStartup - _lastCheck < PollSeconds) return;
            _lastCheck = EditorApplication.timeSinceStartup;

            _worst = SubsystemHealth.Worst();
            bool attention = _worst >= SubsystemHealthSeverity.Warning;
            if (displayed != attention) displayed = attention;
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (_button == null) return;
            _button.text = _worst == SubsystemHealthSeverity.Error
                ? "⛔  Subsystems need attention"
                : "⚠  Subsystems need attention";
        }
    }
}
