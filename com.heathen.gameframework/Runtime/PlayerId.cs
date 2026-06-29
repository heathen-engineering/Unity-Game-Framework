using System;

namespace Heathen
{
    /// <summary>
    /// Opaque 64-bit player identity. Stored as a <see cref="ulong"/> for compatibility with the
    /// other id systems used across Heathen tooling (tag path hashes, network ids, ...), so a
    /// network layer or session manager can mint ids in whatever space it likes. The value
    /// <c>0</c> is reserved for <see cref="None"/>.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        /// <summary>The raw 64-bit identity. Assigned by the session/network layer; opaque here.</summary>
        public readonly ulong Value;

        public PlayerId(ulong value) => Value = value;

        /// <summary>The unassigned player id. <see cref="Value"/> is <c>0</c>.</summary>
        public static readonly PlayerId None = new PlayerId(0);

        /// <summary><c>true</c> when this is not <see cref="None"/>.</summary>
        public bool IsValid => Value != 0;

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId o && Equals(o);
        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(PlayerId a, PlayerId b) => a.Value == b.Value;
        public static bool operator !=(PlayerId a, PlayerId b) => a.Value != b.Value;

        public override string ToString() => IsValid ? $"Player({Value})" : "Player(None)";
    }
}
