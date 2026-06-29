using System;
using System.Collections.Generic;
using System.Reflection;

namespace Heathen
{
    /// <summary>
    /// Reflects over loaded assemblies to find concrete <see cref="Subsystem"/> types by
    /// <see cref="SubsystemScope"/>, using the scope declared on each type's
    /// <see cref="SubsystemAttribute"/>. Reflection is used (rather than an editor TypeCache) so the
    /// same path works in player builds; it runs once per scope at boot / world creation.
    /// </summary>
    internal static class SubsystemDiscovery
    {
        // Cached per scope. Subsystem types cannot change without a domain reload, which resets these
        // statics, so the cache never needs invalidating (and stays valid across enter-play-without-
        // domain-reload). This keeps repeated world creation off the assembly-scan path.
        private static readonly Dictionary<SubsystemScope, List<Type>> _cache = new();

        internal static IReadOnlyList<Type> GlobalSubsystemTypes() => TypesForScope(SubsystemScope.Global);

        internal static IReadOnlyList<Type> TypesForScope(SubsystemScope scope)
        {
            if (_cache.TryGetValue(scope, out var cached)) return cached;

            var result   = new List<Type>();
            var baseType = typeof(Subsystem);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; } // tolerate partially-loadable assemblies

                if (types == null) continue;

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !baseType.IsAssignableFrom(t)) continue;

                    var attr = t.GetCustomAttribute<SubsystemAttribute>(false);
                    if (attr == null || attr.Scope != scope) continue;

                    result.Add(t);
                }
            }

            _cache[scope] = result;
            return result;
        }
    }
}
