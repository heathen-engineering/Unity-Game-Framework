using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Heathen
{
    /// <summary>
    /// Drives the six opt-in tick phases (<see cref="IBeforeFixed"/> ... <see cref="IAfterUpdate"/>) by
    /// injecting nodes into Unity's <see cref="PlayerLoop"/>. The framework is purely the dispatcher: it
    /// calls the subsystem's tick methods at the right point, the subsystem supplies the logic.
    ///
    /// <para>The nodes are inserted as <b>children of the FixedUpdate / Update systems</b>, not as siblings
    /// in the root loop, because only the FixedUpdate subtree is re-run per physics step; a root sibling
    /// would tick once per frame. Within each phase the layout is:
    /// <c>[Before, On, ...engine work (MB FixedUpdate/Update, physics)..., After]</c>, so Before/On run
    /// ahead of the engine's own work for that cadence and After runs once it has completed.</para>
    ///
    /// <para>Install strips any of our own nodes first, so it is idempotent and self-heals across
    /// enter-play-mode-without-domain-reload. Uninstall (on quit / exiting play mode) removes them and
    /// restores whatever else was in the loop (DOTS, other injectors) untouched.</para>
    /// </summary>
    internal static class SubsystemTicker
    {
        // Marker types: a PlayerLoopSystem's identity is its `type`, so each node needs a unique one.
        private struct BeforeFixedPhase { }
        private struct OnFixedPhase { }
        private struct AfterFixedPhase { }
        private struct BeforeUpdatePhase { }
        private struct OnUpdatePhase { }
        private struct AfterUpdatePhase { }

        private static readonly Type[] Markers =
        {
            typeof(BeforeFixedPhase), typeof(OnFixedPhase), typeof(AfterFixedPhase),
            typeof(BeforeUpdatePhase), typeof(OnUpdatePhase), typeof(AfterUpdatePhase),
        };

        // Per-phase listener lists, derived from the registered subsystems' implemented interfaces.
        private static readonly List<IBeforeFixed>  _beforeFixed  = new();
        private static readonly List<IOnFixed>      _onFixed      = new();
        private static readonly List<IAfterFixed>   _afterFixed   = new();
        private static readonly List<IBeforeUpdate> _beforeUpdate = new();
        private static readonly List<IOnUpdate>     _onUpdate     = new();
        private static readonly List<IAfterUpdate>  _afterUpdate  = new();

        private static bool _installed;

        // Deferred add/remove: while a phase is dispatching we must not mutate the list being iterated
        // (a subsystem may dispose its world mid-tick, which unregisters world subsystems). Ops queued
        // during dispatch are applied after the phase's loop completes.
        private struct PendingOp { public Subsystem Subsystem; public bool Add; }
        private static readonly List<PendingOp> _pending = new();
        private static bool _dispatching;

        // ── Registration (Global boot + per-world create/destroy) ───────────────────────────

        internal static void Register(Subsystem s)
        {
            if (_dispatching) { _pending.Add(new PendingOp { Subsystem = s, Add = true }); return; }
            AddNow(s);
        }

        internal static void Unregister(Subsystem s)
        {
            if (_dispatching) { _pending.Add(new PendingOp { Subsystem = s, Add = false }); return; }
            RemoveNow(s);
        }

        private static void AddNow(Subsystem s)
        {
            if (s is IBeforeFixed bf)  _beforeFixed.Add(bf);
            if (s is IOnFixed of)      _onFixed.Add(of);
            if (s is IAfterFixed af)   _afterFixed.Add(af);
            if (s is IBeforeUpdate bu) _beforeUpdate.Add(bu);
            if (s is IOnUpdate ou)     _onUpdate.Add(ou);
            if (s is IAfterUpdate au)  _afterUpdate.Add(au);
        }

        private static void RemoveNow(Subsystem s)
        {
            if (s is IBeforeFixed bf)  _beforeFixed.Remove(bf);
            if (s is IOnFixed of)      _onFixed.Remove(of);
            if (s is IAfterFixed af)   _afterFixed.Remove(af);
            if (s is IBeforeUpdate bu) _beforeUpdate.Remove(bu);
            if (s is IOnUpdate ou)     _onUpdate.Remove(ou);
            if (s is IAfterUpdate au)  _afterUpdate.Remove(au);
        }

        private static void FlushPending()
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                var op = _pending[i];
                if (op.Add) AddNow(op.Subsystem); else RemoveNow(op.Subsystem);
            }
            _pending.Clear();
        }

        internal static void Clear()
        {
            _beforeFixed.Clear(); _onFixed.Clear(); _afterFixed.Clear();
            _beforeUpdate.Clear(); _onUpdate.Clear(); _afterUpdate.Clear();
            _pending.Clear();
            _dispatching = false;
        }

        // ── Install / uninstall ─────────────────────────────────────────────────────────────

        internal static void Install()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            StripOurNodes(ref loop); // idempotent: clear any prior insertion first

            WrapPhase(ref loop, typeof(FixedUpdate),
                Node<BeforeFixedPhase>(DispatchBeforeFixed),
                Node<OnFixedPhase>(DispatchOnFixed),
                Node<AfterFixedPhase>(DispatchAfterFixed));

            WrapPhase(ref loop, typeof(Update),
                Node<BeforeUpdatePhase>(DispatchBeforeUpdate),
                Node<OnUpdatePhase>(DispatchOnUpdate),
                Node<AfterUpdatePhase>(DispatchAfterUpdate));

            PlayerLoop.SetPlayerLoop(loop);
            _installed = true;

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

        internal static void Uninstall()
        {
            if (!_installed) return;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            StripOurNodes(ref loop);
            PlayerLoop.SetPlayerLoop(loop);
            _installed = false;

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
        }

#if UNITY_EDITOR
        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            // Restore the editor's player loop the moment we leave play mode; domain-reload-free play
            // would otherwise leave our nodes dispatching over empty lists in edit mode.
            if (change == PlayModeStateChange.ExitingPlayMode) Uninstall();
        }
#endif

        // ── PlayerLoop surgery ──────────────────────────────────────────────────────────────

        private static PlayerLoopSystem Node<T>(PlayerLoopSystem.UpdateFunction fn)
            => new PlayerLoopSystem { type = typeof(T), updateDelegate = fn };

        // Rebuilds the named top-level phase's children as [before, on, ...existing..., after].
        // FixedUpdate and Update are top-level systems, so no recursion is needed to find them.
        private static void WrapPhase(ref PlayerLoopSystem root, Type phaseType,
            PlayerLoopSystem before, PlayerLoopSystem on, PlayerLoopSystem after)
        {
            var subs = root.subSystemList;
            if (subs == null) return;

            for (int i = 0; i < subs.Length; i++)
            {
                if (subs[i].type != phaseType) continue;

                var phase = subs[i];
                var old   = phase.subSystemList ?? Array.Empty<PlayerLoopSystem>();
                var built = new PlayerLoopSystem[old.Length + 3];
                built[0] = before;
                built[1] = on;
                Array.Copy(old, 0, built, 2, old.Length);
                built[built.Length - 1] = after;

                phase.subSystemList = built;
                subs[i] = phase;
                root.subSystemList = subs;
                return;
            }

            Debug.LogError($"[GameFramework] PlayerLoop phase '{phaseType.Name}' not found; tick phase not installed.");
        }

        private static void StripOurNodes(ref PlayerLoopSystem system)
        {
            if (system.subSystemList == null) return;

            var kept = new List<PlayerLoopSystem>(system.subSystemList.Length);
            foreach (var child in system.subSystemList)
            {
                if (Array.IndexOf(Markers, child.type) >= 0) continue;
                var c = child;
                StripOurNodes(ref c);
                kept.Add(c);
            }
            system.subSystemList = kept.ToArray();
        }

        // ── Dispatch ────────────────────────────────────────────────────────────────────────
        // Hot path: no per-frame allocation (no closures), index iteration over the live list. The
        // `_dispatching` guard defers any (un)register raised mid-tick to FlushPending after the loop,
        // so the list is never mutated while iterated; a subsystem already deinitialised this frame
        // (its world disposed) is skipped via IsInitialised. One listener throwing cannot break the loop.

        private static void DispatchBeforeFixed()
        {
            float dt = Time.fixedDeltaTime;
            _dispatching = true;
            for (int i = 0; i < _beforeFixed.Count; i++)
            {
                if (_beforeFixed[i] is Subsystem s && !s.IsInitialised) continue;
                try { _beforeFixed[i].BeforeFixed(dt); } catch (Exception e) { Debug.LogException(e); }
            }
            _dispatching = false;
            FlushPending();
        }

        private static void DispatchOnFixed()
        {
            float dt = Time.fixedDeltaTime;
            _dispatching = true;
            for (int i = 0; i < _onFixed.Count; i++)
            {
                if (_onFixed[i] is Subsystem s && !s.IsInitialised) continue;
                try { _onFixed[i].OnFixed(dt); } catch (Exception e) { Debug.LogException(e); }
            }
            _dispatching = false;
            FlushPending();
        }

        private static void DispatchAfterFixed()
        {
            float dt = Time.fixedDeltaTime;
            _dispatching = true;
            for (int i = 0; i < _afterFixed.Count; i++)
            {
                if (_afterFixed[i] is Subsystem s && !s.IsInitialised) continue;
                try { _afterFixed[i].AfterFixed(dt); } catch (Exception e) { Debug.LogException(e); }
            }
            _dispatching = false;
            FlushPending();
        }

        private static void DispatchBeforeUpdate()
        {
            float dt = Time.deltaTime;
            _dispatching = true;
            for (int i = 0; i < _beforeUpdate.Count; i++)
            {
                if (_beforeUpdate[i] is Subsystem s && !s.IsInitialised) continue;
                try { _beforeUpdate[i].BeforeUpdate(dt); } catch (Exception e) { Debug.LogException(e); }
            }
            _dispatching = false;
            FlushPending();
        }

        private static void DispatchOnUpdate()
        {
            float dt = Time.deltaTime;
            _dispatching = true;
            for (int i = 0; i < _onUpdate.Count; i++)
            {
                if (_onUpdate[i] is Subsystem s && !s.IsInitialised) continue;
                try { _onUpdate[i].OnUpdate(dt); } catch (Exception e) { Debug.LogException(e); }
            }
            _dispatching = false;
            FlushPending();
        }

        private static void DispatchAfterUpdate()
        {
            float dt = Time.deltaTime;
            _dispatching = true;
            for (int i = 0; i < _afterUpdate.Count; i++)
            {
                if (_afterUpdate[i] is Subsystem s && !s.IsInitialised) continue;
                try { _afterUpdate[i].AfterUpdate(dt); } catch (Exception e) { Debug.LogException(e); }
            }
            _dispatching = false;
            FlushPending();
        }
    }
}
