using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Heathen
{
    /// <summary>
    /// A pure in-memory container for <see cref="SubsystemScope.World"/> subsystems, with no GameObject
    /// and no scene presence. Unity has no first-class world object (a <see cref="UnityEngine.SceneManagement.Scene"/>
    /// is just a GameObject container and is not 1:1 with a simulation world), so the framework defines
    /// one. Multiple worlds may exist at once (e.g. a pause world and a gameplay world).
    ///
    /// <para>Created and destroyed through <see cref="WorldManagerSubsystem"/>; on create it brings up
    /// every World-scoped subsystem (in dependency order) and registers their tick phases, and on dispose
    /// it unregisters and tears them down in reverse. All Global subsystems are guaranteed up before any
    /// world is created, so a World subsystem may assume the global layer is ready.</para>
    /// </summary>
    public sealed class World : IDisposable
    {
        /// <summary>Human-readable name, for diagnostics and <see cref="WorldManagerSubsystem"/> lookup.</summary>
        public string Name { get; }

        /// <summary>
        /// The scene active when this world was created, recorded as a hint for the convenience
        /// (scene-bound) world. Informational only: a world's lifetime is not tied to a scene.
        /// </summary>
        public Scene? Scene { get; }

        /// <summary><c>true</c> between creation and disposal.</summary>
        public bool IsAlive { get; private set; }

        private readonly WorldManagerSubsystem _manager;
        private readonly Dictionary<Type, Subsystem> _subsystems = new();
        private readonly List<Subsystem> _order = new();

        // Optional Unreal-style structure. State/Players live here (not on the server-only GameMode) so
        // they survive on a client that has no GameMode.
        private readonly List<PlayerState> _players = new();
        private readonly Dictionary<PlayerId, PlayerState> _playersById = new();
        private GameState _state;
        private GameMode _mode;

        internal World(WorldManagerSubsystem manager, string name, Scene? scene)
        {
            _manager = manager;
            Name     = name;
            Scene    = scene;
        }

        /// <summary>The world's subsystems, in initialisation order.</summary>
        public IReadOnlyList<Subsystem> Subsystems => _order;

        /// <summary>
        /// Resolve a World-scoped subsystem owned by this world. Exact type first, then the first
        /// registered subsystem assignable to <typeparamref name="T"/>; <c>null</c> if none. Global
        /// subsystems are not resolved here; use <see cref="GameFramework.Get{T}"/> for those.
        /// </summary>
        public T Get<T>() where T : Subsystem
        {
            if (_subsystems.TryGetValue(typeof(T), out var exact)) return (T)exact;
            for (int i = 0; i < _order.Count; i++)
                if (_order[i] is T match) return match;
            return null;
        }

        // ── Optional GameMode / GameState / PlayerState ─────────────────────────────────────
        // All optional; a bare world is just its subsystems. State and Players exist independently of a
        // GameMode (a client has data but no mode); GameMode, when present, is the logic over them.

        /// <summary>The optional game state, or null.</summary>
        public GameState State => _state;

        /// <summary>The optional game mode (server-only logic), or null.</summary>
        public GameMode Mode => _mode;

        /// <summary>The players in this world, keyed by <see cref="PlayerId"/>.</summary>
        public IReadOnlyList<PlayerState> Players => _players;

        /// <summary>Attach (or replace) the game state.</summary>
        public GameState SetState(GameState state)
        {
            if (!IsAlive) return null;
            if (_state != null) _state.World = null;
            _state = state;
            if (state != null) state.World = this;
            return state;
        }

        /// <summary>Attach (or replace) the game state by constructing <typeparamref name="T"/>.</summary>
        public T SetState<T>() where T : GameState, new() => (T)SetState(new T());

        /// <summary>Attach (or replace) the game mode; stops the previous mode and starts the new one.</summary>
        public GameMode SetMode(GameMode mode)
        {
            if (!IsAlive) return null;
            if (_mode != null) _mode.Stop();
            _mode = mode;
            if (mode != null) mode.Start(this);
            return mode;
        }

        /// <summary>Attach (or replace) the game mode by constructing <typeparamref name="T"/>.</summary>
        public T SetMode<T>() where T : GameMode, new() => (T)SetMode(new T());

        /// <summary>Add a player under <paramref name="id"/>. No-op (returns the existing) if already present.</summary>
        public PlayerState AddPlayer(PlayerId id, PlayerState player)
        {
            if (!IsAlive || player == null || !id.IsValid) return null;
            if (_playersById.TryGetValue(id, out var existing)) return existing;

            player.Id    = id;
            player.World = this;
            _players.Add(player);
            _playersById[id] = player;
            _mode?.NotifyPlayerJoined(player);
            return player;
        }

        /// <summary>Add a player under <paramref name="id"/> by constructing <typeparamref name="T"/>.</summary>
        public T AddPlayer<T>(PlayerId id) where T : PlayerState, new() => (T)AddPlayer(id, new T());

        /// <summary>Remove the player with <paramref name="id"/>. Returns false if not present.</summary>
        public bool RemovePlayer(PlayerId id)
        {
            if (!_playersById.TryGetValue(id, out var player)) return false;
            _playersById.Remove(id);
            _players.Remove(player);
            _mode?.NotifyPlayerLeft(player);
            player.World = null;
            return true;
        }

        /// <summary>The player with <paramref name="id"/>, or null.</summary>
        public PlayerState GetPlayer(PlayerId id) => _playersById.TryGetValue(id, out var p) ? p : null;

        // Brings up every World-scoped subsystem this world should host.
        internal void Initialize()
        {
            var instances = new Dictionary<Type, Subsystem>();
            foreach (var type in SubsystemDiscovery.TypesForScope(SubsystemScope.World))
            {
                Subsystem instance;
                try { instance = (Subsystem)Activator.CreateInstance(type); }
                catch (Exception e)
                {
                    Debug.LogError($"[GameFramework] Could not construct world subsystem '{type.FullName}': {e}");
                    continue;
                }
                instance.World = this;                 // available to ShouldCreate and Initialize
                if (!instance.ShouldCreate()) continue;
                instances[type] = instance;
            }

            foreach (var instance in DependencyOrder.Sort(instances))
            {
                _subsystems[instance.GetType()] = instance;
                _order.Add(instance);
            }

            foreach (var s in _order)
            {
                try { s.DoInitialize(); }
                catch (Exception e)
                {
                    Debug.LogError($"[GameFramework] world '{Name}' subsystem '{s.GetType().Name}'.Initialize threw: {e}");
                }
            }

            foreach (var s in _order) SubsystemTicker.Register(s);
            IsAlive = true;
        }

        /// <summary>Destroy this world and tear down its subsystems. Routed through the manager.</summary>
        public void Dispose() => _manager?.Destroy(this);

        // Actual teardown; idempotent. Called by the manager so the world is also removed from its list.
        internal void DisposeInternal()
        {
            if (!IsAlive) return;
            IsAlive = false;

            // Stop the mode and drop the optional structure before subsystems, so OnStop can still read
            // live subsystems.
            if (_mode != null) { _mode.Stop(); _mode = null; }
            _players.Clear();
            _playersById.Clear();
            if (_state != null) { _state.World = null; _state = null; }

            for (int i = _order.Count - 1; i >= 0; i--) SubsystemTicker.Unregister(_order[i]);

            for (int i = _order.Count - 1; i >= 0; i--)
            {
                try { _order[i].DoDeinitialize(); }
                catch (Exception e)
                {
                    Debug.LogError($"[GameFramework] world '{Name}' subsystem '{_order[i].GetType().Name}'.Deinitialize threw: {e}");
                }
            }

            _subsystems.Clear();
            _order.Clear();
        }
    }
}
