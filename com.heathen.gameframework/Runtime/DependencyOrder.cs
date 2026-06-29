using System;
using System.Collections.Generic;
using UnityEngine;

namespace Heathen
{
    /// <summary>
    /// Orders a set of subsystem instances so that every subsystem is initialised after the
    /// subsystems it <see cref="Subsystem.DependsOn"/>. Shared by Global boot and (P3) per-world
    /// creation. Dependencies that are not present in the set are ignored with a warning; cycles are
    /// reported and broken (the offending subsystems still come out, in an arbitrary-but-stable order).
    /// </summary>
    internal static class DependencyOrder
    {
        internal static List<Subsystem> Sort(IReadOnlyDictionary<Type, Subsystem> set)
        {
            var result   = new List<Subsystem>(set.Count);
            var state    = new Dictionary<Type, Mark>(set.Count); // DFS colouring

            // Deterministic iteration: process roots by full type name so the order is stable run to run.
            var roots = new List<Type>(set.Keys);
            roots.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

            foreach (var t in roots)
                Visit(t, set, state, result);

            return result;
        }

        private enum Mark { Visiting, Done }

        private static void Visit(Type type, IReadOnlyDictionary<Type, Subsystem> set,
                                  Dictionary<Type, Mark> state, List<Subsystem> result)
        {
            if (state.TryGetValue(type, out var m))
            {
                if (m == Mark.Visiting)
                    Debug.LogError($"[GameFramework] Dependency cycle involving subsystem '{type.Name}'; " +
                                   "initialisation order for the cycle is undefined.");
                return; // Done, or a cycle we have already reported.
            }

            state[type] = Mark.Visiting;

            var deps = set[type].DependsOn;
            if (deps != null)
            {
                foreach (var dep in deps)
                {
                    if (dep == null) continue;
                    if (!set.ContainsKey(dep))
                    {
                        Debug.LogWarning($"[GameFramework] Subsystem '{type.Name}' depends on '{dep.Name}', " +
                                         "which is not a registered subsystem in this scope; ignoring.");
                        continue;
                    }
                    Visit(dep, set, state, result);
                }
            }

            state[type] = Mark.Done;
            result.Add(set[type]);
        }
    }
}
