using System.Collections.Generic;
using Newtonsoft.Json;

namespace Heathen.Editor
{
    /// <summary>
    /// A serialise-based undo/redo history for plain authoring data that is not a <see cref="UnityEngine.Object"/>
    /// at the point of edit (so Unity's <c>Undo</c>, which requires a UnityEngine.Object, cannot be used). Each
    /// <see cref="Push"/> stores a JSON snapshot of the working object; <see cref="Undo"/>/<see cref="Redo"/>
    /// return a fresh deserialised copy of the neighbouring snapshot. Works with anything Newtonsoft can
    /// round-trip (the Unity value-type converters from <see cref="UnityJson"/> are applied), so it is reusable
    /// across every tool that edits a POCO/JSON model (e.g. the Ogham graph editor, the HATE Forge).
    ///
    /// <para>Snapshots are whole-document copies, so memory is O(history × document size): right for authoring
    /// documents, not for very large data. The editor decides granularity by choosing when to <see cref="Push"/>
    /// (after a committed field edit, an add/remove, a drag end — not per keystroke), and wires its own
    /// Ctrl+Z / Ctrl+Y to <see cref="Undo"/>/<see cref="Redo"/>, replacing its working object with the result
    /// and repainting.</para>
    /// </summary>
    public sealed class UndoHistory<T> where T : class
    {
        private static readonly JsonSerializerSettings DefaultSettings = new()
        {
            Formatting = Formatting.None,
            Converters = UnityJson.Converters,
        };

        private readonly List<string> _snapshots = new();
        private readonly int _capacity;
        private readonly JsonSerializerSettings _settings;
        private int _cursor = -1; // index of the current snapshot, or -1 when empty

        /// <param name="capacity">Maximum snapshots retained; the oldest is dropped past this. Minimum 1.</param>
        /// <param name="settings">Optional Newtonsoft settings; defaults to compact JSON with Unity converters.</param>
        public UndoHistory(int capacity = 100, JsonSerializerSettings settings = null)
        {
            _capacity = capacity < 1 ? 1 : capacity;
            _settings = settings ?? DefaultSettings;
        }

        /// <summary>The number of snapshots currently retained.</summary>
        public int Count => _snapshots.Count;

        /// <summary>True when there is a prior snapshot to <see cref="Undo"/> to.</summary>
        public bool CanUndo => _cursor > 0;

        /// <summary>True when there is a later snapshot to <see cref="Redo"/> to.</summary>
        public bool CanRedo => _cursor >= 0 && _cursor < _snapshots.Count - 1;

        /// <summary>
        /// Records the current state as a new snapshot. Any redo tail (snapshots ahead of the cursor) is
        /// discarded, matching standard undo semantics. Pushing a fresh edit after an undo branches from there.
        /// </summary>
        public void Push(T state)
        {
            if (state == null) return;

            // Drop any redo tail.
            if (_cursor < _snapshots.Count - 1)
                _snapshots.RemoveRange(_cursor + 1, _snapshots.Count - _cursor - 1);

            _snapshots.Add(JsonConvert.SerializeObject(state, _settings));

            // Enforce capacity by dropping the oldest.
            while (_snapshots.Count > _capacity)
                _snapshots.RemoveAt(0);

            _cursor = _snapshots.Count - 1;
        }

        /// <summary>Steps back one snapshot and returns a fresh copy of it, or null when <see cref="CanUndo"/> is false.</summary>
        public T Undo()
        {
            if (!CanUndo) return null;
            _cursor--;
            return Deserialize(_snapshots[_cursor]);
        }

        /// <summary>Steps forward one snapshot and returns a fresh copy of it, or null when <see cref="CanRedo"/> is false.</summary>
        public T Redo()
        {
            if (!CanRedo) return null;
            _cursor++;
            return Deserialize(_snapshots[_cursor]);
        }

        /// <summary>A fresh copy of the current snapshot, or null when the history is empty.</summary>
        public T Current => _cursor >= 0 ? Deserialize(_snapshots[_cursor]) : null;

        /// <summary>Clears all history.</summary>
        public void Clear()
        {
            _snapshots.Clear();
            _cursor = -1;
        }

        private T Deserialize(string json) => JsonConvert.DeserializeObject<T>(json, _settings);
    }
}
