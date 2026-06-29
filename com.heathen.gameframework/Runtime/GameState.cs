namespace Heathen
{
    /// <summary>
    /// Base for data describing the current game (score, phase, timers, ...). <b>Plain data, no logic</b>
    /// so a replication layer only ever touches data. Lives on the <see cref="World"/>, not on the
    /// <see cref="GameMode"/>, so it still exists on a client where the (server-only) GameMode does not.
    /// Server-authoritative and replicated read-only by default.
    ///
    /// <para>A mutator must call <see cref="MarkChanged"/> after changing replicated fields so a
    /// replicator can detect the delta via <see cref="Revision"/>. Constructors must be trivial (the
    /// framework may construct via <c>SetState&lt;T&gt;()</c>).</para>
    /// </summary>
    public abstract class GameState
    {
        /// <summary>The owning world. Set by the framework when attached via <see cref="World.SetState"/>.</summary>
        public World World { get; internal set; }

        /// <summary>Replication authority (default <see cref="Authority.Server"/>); override to change.</summary>
        public virtual Authority Authority => Authority.Server;

        /// <summary>Monotonic change counter, the replication seam. Bumped by <see cref="MarkChanged"/>.</summary>
        public ulong Revision { get; private set; }

        /// <summary>Call from a mutator after changing replicated data.</summary>
        protected void MarkChanged() => Revision++;
    }
}
