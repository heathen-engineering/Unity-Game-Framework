using System;
using System.Collections.Generic;

namespace Heathen
{
    /// <summary>
    /// Optional per-world <b>logic</b> that dictates rules and flow. Server-only and never replicated
    /// (so it has no <see cref="Authority"/> seam); it reads and mutates the world's replicated
    /// <see cref="GameState"/> and <see cref="PlayerState"/> data, which live on the <see cref="World"/>
    /// rather than here precisely so they survive on clients that have no GameMode.
    ///
    /// <para>Flow is exposed two ways, per preference: override the <c>On*</c> hooks (inheritance) and/or
    /// subscribe to the matching events (composition, "hooks you register"). Both fire. Constructors must
    /// be trivial (the framework may construct via <c>SetMode&lt;T&gt;()</c>).</para>
    /// </summary>
    public abstract class GameMode
    {
        /// <summary>The world this mode governs. Set by the framework when attached via <see cref="World.SetMode"/>.</summary>
        public World World { get; internal set; }

        /// <summary>Convenience access to the world's optional game state.</summary>
        public GameState State => World?.State;

        /// <summary>Convenience access to the world's players.</summary>
        public IReadOnlyList<PlayerState> Players => World?.Players;

        // Register-style hooks (composition). The override-style hooks below also fire.
        public event Action Started;
        public event Action Stopped;
        public event Action<PlayerState> PlayerJoined;
        public event Action<PlayerState> PlayerLeft;

        internal void Start(World world)
        {
            World = world;
            OnStart();
            Started?.Invoke();
        }

        internal void Stop()
        {
            OnStop();
            Stopped?.Invoke();
            World = null;
        }

        internal void NotifyPlayerJoined(PlayerState player)
        {
            OnPlayerJoined(player);
            PlayerJoined?.Invoke(player);
        }

        internal void NotifyPlayerLeft(PlayerState player)
        {
            OnPlayerLeft(player);
            PlayerLeft?.Invoke(player);
        }

        /// <summary>Called when this mode is attached to a live world.</summary>
        protected virtual void OnStart() { }

        /// <summary>Called when this mode is replaced or its world is disposed.</summary>
        protected virtual void OnStop() { }

        /// <summary>Called after a player is added to the world.</summary>
        protected virtual void OnPlayerJoined(PlayerState player) { }

        /// <summary>Called after a player is removed from the world.</summary>
        protected virtual void OnPlayerLeft(PlayerState player) { }
    }
}
