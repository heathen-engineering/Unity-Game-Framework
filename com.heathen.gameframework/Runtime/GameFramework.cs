using System;
using System.Collections.Generic;
using UnityEngine;

namespace Heathen
{
    /// <summary>
    /// Entry point and registry for <see cref="SubsystemScope.Global"/> subsystems. Boots at
    /// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/> (every play, including
    /// enter-play-mode-without-domain-reload, which is why boot starts by clearing static state),
    /// discovers every Global subsystem type, brings them up in dependency order, and tears them down
    /// in reverse on application quit.
    ///
    /// <para>World-scoped subsystems are owned by a <c>World</c> (P3), not by this class.</para>
    /// </summary>
    public static class GameFramework
    {
        // Global subsystems keyed by concrete type, plus the resolved init order (deinit is its reverse).
        private static readonly Dictionary<Type, Subsystem> _global = new();
        private static readonly List<Subsystem> _order = new();

        /// <summary><c>true</c> once Global subsystems have been booted for the current session.</summary>
        public static bool IsBooted { get; private set; }

        /// <summary>The Global subsystems in their initialisation order.</summary>
        public static IReadOnlyList<Subsystem> GlobalSubsystems => _order;

        /// <summary>
        /// Resolve a Global subsystem. Returns the exact type if registered, otherwise the first
        /// registered subsystem assignable to <typeparamref name="T"/> (so an abstract base or
        /// interface-style request resolves to its single implementation). <c>null</c> if none.
        /// </summary>
        public static T Get<T>() where T : Subsystem
        {
            if (_global.TryGetValue(typeof(T), out var exact)) return (T)exact;
            foreach (var s in _order)
                if (s is T match) return match;
            return null;
        }

        /// <summary>The primary world (see <see cref="WorldManagerSubsystem.Main"/>). May be null before boot.</summary>
        public static World MainWorld => Get<WorldManagerSubsystem>()?.Main;

        /// <summary>Create a new world (see <see cref="WorldManagerSubsystem.Create"/>).</summary>
        public static World CreateWorld(string name = null) => Get<WorldManagerSubsystem>()?.Create(name);

        // ── Boot / shutdown ────────────────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Boot()
        {
            // Clean slate first: under "enter play mode without domain reload" the static collections
            // survive from the previous session and would otherwise retain stale instances.
            Shutdown();

            // Construct every Global candidate (constructors must be trivial), drop opt-outs, then
            // initialise in dependency order.
            var instances = new Dictionary<Type, Subsystem>();
            foreach (var type in SubsystemDiscovery.GlobalSubsystemTypes())
            {
                Subsystem instance;
                try { instance = (Subsystem)Activator.CreateInstance(type); }
                catch (Exception e)
                {
                    Debug.LogError($"[GameFramework] Could not construct subsystem '{type.FullName}': {e}");
                    continue;
                }
                if (!instance.ShouldCreate()) continue;
                instances[type] = instance;
            }

            foreach (var instance in DependencyOrder.Sort(instances))
            {
                _global[instance.GetType()] = instance;
                _order.Add(instance);
            }

            foreach (var s in _order)
            {
                try { s.DoInitialize(); }
                catch (Exception e)
                {
                    Debug.LogError($"[GameFramework] '{s.GetType().Name}'.Initialize threw: {e}");
                }
            }

            // Register tick listeners and inject the PlayerLoop phases once subsystems are up.
            foreach (var s in _order) SubsystemTicker.Register(s);
            SubsystemTicker.Install();

            IsBooted = true;
            Application.quitting += Shutdown;

            // Globals are fully up; bring up the convenience default world (hybrid boundary). World
            // subsystems may therefore assume the entire global layer is initialised.
            Get<WorldManagerSubsystem>()?.CreateDefaultWorld();
        }

        private static void Shutdown()
        {
            Application.quitting -= Shutdown;

            // Stop dispatching before tearing subsystems down.
            SubsystemTicker.Uninstall();
            SubsystemTicker.Clear();

            // Deinitialise in reverse of initialisation order so dependencies outlive their dependents.
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                try { _order[i].DoDeinitialize(); }
                catch (Exception e)
                {
                    Debug.LogError($"[GameFramework] '{_order[i].GetType().Name}'.Deinitialize threw: {e}");
                }
            }

            _global.Clear();
            _order.Clear();
            IsBooted = false;
        }
    }
}
