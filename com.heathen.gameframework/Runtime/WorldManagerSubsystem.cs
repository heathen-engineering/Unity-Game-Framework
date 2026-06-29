using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Heathen
{
    /// <summary>
    /// The Global subsystem that owns the set of live <see cref="World"/>s. The World system is therefore
    /// not a special case: it is the first consumer of the subsystem structure. Worlds are created and
    /// destroyed through this manager; a world's <see cref="World.Dispose"/> routes back here so it is
    /// also removed from <see cref="Worlds"/>.
    ///
    /// <para>Hybrid boundary model: a convenience default world (bound to the active scene as a hint) is
    /// created once the global layer is up, so a single-world game needs no setup, while multi-world
    /// games call <see cref="Create"/> explicitly (a pause world, per-player worlds, ...).</para>
    /// </summary>
    [Subsystem(SubsystemScope.Global)]
    public sealed class WorldManagerSubsystem : Subsystem
    {
        private readonly List<World> _worlds = new();

        /// <summary>All live worlds, in creation order.</summary>
        public IReadOnlyList<World> Worlds => _worlds;

        /// <summary>The primary world (the first created), for the common single-world case. May be null.</summary>
        public World Main { get; private set; }

        /// <summary>Create a new world and bring up its World-scoped subsystems.</summary>
        public World Create(string name = null, Scene? scene = null)
        {
            var world = new World(this, name ?? $"World{_worlds.Count}", scene);
            _worlds.Add(world);
            world.Initialize();
            if (Main == null) Main = world;
            return world;
        }

        /// <summary>Destroy a world and tear down its subsystems. Safe to call more than once.</summary>
        public void Destroy(World world)
        {
            if (world == null || !_worlds.Remove(world)) return;
            world.DisposeInternal();
            if (Main == world) Main = _worlds.Count > 0 ? _worlds[0] : null;
        }

        // Called by GameFramework after the global layer is fully initialised (so world subsystems can
        // assume every global is ready). Creates the convenience world if the game has not made one.
        internal void CreateDefaultWorld()
        {
            if (_worlds.Count == 0) Create("Main", SceneManager.GetActiveScene());
        }

        protected override void Deinitialize()
        {
            for (int i = _worlds.Count - 1; i >= 0; i--) _worlds[i].DisposeInternal();
            _worlds.Clear();
            Main = null;
        }
    }
}
