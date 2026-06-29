namespace Heathen
{
    /// <summary>
    /// Base for per-player data (one per <see cref="PlayerId"/>: name, score, loadout, ...). <b>Plain
    /// data, no logic.</b> Lives on the <see cref="World"/> (not the server-only <see cref="GameMode"/>),
    /// keyed by <see cref="Id"/>. Per-owner authority and replicated to observers by default.
    ///
    /// <para>A mutator must call <see cref="MarkChanged"/> after changing replicated fields. Constructors
    /// must be trivial (the framework may construct via <c>AddPlayer&lt;T&gt;()</c>).</para>
    /// </summary>
    public abstract class PlayerState
    {
        /// <summary>This player's id. Set by the framework in <see cref="World.AddPlayer"/>.</summary>
        public PlayerId Id { get; internal set; }

        /// <summary>The owning world. Set by the framework when added.</summary>
        public World World { get; internal set; }

        /// <summary>Replication authority (default <see cref="Authority.Owner"/>); override to change.</summary>
        public virtual Authority Authority => Authority.Owner;

        /// <summary>Monotonic change counter, the replication seam. Bumped by <see cref="MarkChanged"/>.</summary>
        public ulong Revision { get; private set; }

        /// <summary>Call from a mutator after changing replicated data.</summary>
        protected void MarkChanged() => Revision++;
    }
}
