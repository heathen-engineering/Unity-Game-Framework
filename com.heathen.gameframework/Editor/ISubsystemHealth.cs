using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Heathen.Editor
{
    /// <summary>How much a subsystem issue should draw the developer's eye. Ordered least → most severe.</summary>
    public enum SubsystemHealthSeverity { Ok, Info, Warning, Error }

    /// <summary>
    /// One thing a subsystem wants the developer to know about its edit-time setup — informational up to blocking.
    /// An optional action lets the report offer a one-click fix (e.g. "Build", "Open settings").
    /// </summary>
    public readonly struct SubsystemIssue
    {
        public readonly SubsystemHealthSeverity Severity;
        public readonly string Message;
        public readonly string ActionLabel;
        public readonly Action Action;

        public SubsystemIssue(SubsystemHealthSeverity severity, string message, string actionLabel = null, Action action = null)
        {
            Severity    = severity;
            Message     = message;
            ActionLabel = actionLabel;
            Action      = action;
        }
    }

    /// <summary>
    /// Editor contract a tool implements so it can report that its subsystem needs developer attention (not set
    /// up, needs a regenerate, misconfigured, …). Surfaced as badges on <c>Project ▸ Subsystems</c>, in the
    /// play-mode guard, and on the Scene-view attention overlay. Discovered by type via <c>TypeCache</c> (the same
    /// way <see cref="ISubsystemConfigEditor"/> is), so the framework needs no reference to the tool's assembly.
    /// </summary>
    public interface ISubsystemHealth
    {
        /// <summary>The concrete <see cref="Subsystem"/> type this reporter describes.</summary>
        Type SubsystemType { get; }

        /// <summary>
        /// The current issues. Called fresh each time it is needed (so it reflects live edits); keep it cheap or
        /// let the caller throttle. Return an empty sequence (or only <see cref="SubsystemHealthSeverity.Ok"/>
        /// entries) when healthy.
        /// </summary>
        IEnumerable<SubsystemIssue> GetIssues();
    }

    /// <summary>
    /// Aggregates every <see cref="ISubsystemHealth"/> reporter in the project. Shared by the Subsystems overview,
    /// the play-mode guard, and the Scene-view overlay so they agree on what needs attention. Reporter instances
    /// are discovered once per domain; <see cref="GetIssues"/> is re-queried each call.
    /// </summary>
    public static class SubsystemHealth
    {
        private static List<ISubsystemHealth> _reporters;

        /// <summary>All discovered reporters (built once per domain load).</summary>
        public static IReadOnlyList<ISubsystemHealth> Reporters => _reporters ??= Discover();

        /// <summary>Drop the cached reporter list so it is rediscovered (rarely needed — statics reset on reload).</summary>
        public static void Refresh() => _reporters = null;

        /// <summary>Every non-Ok issue across all reporters, paired with the reporter that raised it.</summary>
        public static IEnumerable<(ISubsystemHealth reporter, SubsystemIssue issue)> AllIssues()
        {
            foreach (var r in Reporters)
            {
                List<SubsystemIssue> issues = null;
                try
                {
                    var got = r.GetIssues();
                    if (got != null) issues = new List<SubsystemIssue>(got);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameFramework] ISubsystemHealth '{r.GetType().FullName}'.GetIssues threw: {e}");
                }
                if (issues == null) continue;
                foreach (var issue in issues)
                    if (issue.Severity != SubsystemHealthSeverity.Ok)
                        yield return (r, issue);
            }
        }

        /// <summary>Issues for a single subsystem type (non-Ok only).</summary>
        public static IEnumerable<SubsystemIssue> IssuesFor(Type subsystemType)
        {
            foreach (var (reporter, issue) in AllIssues())
                if (reporter.SubsystemType == subsystemType)
                    yield return issue;
        }

        /// <summary>The most severe issue anywhere (Ok when everything is healthy).</summary>
        public static SubsystemHealthSeverity Worst()
        {
            var worst = SubsystemHealthSeverity.Ok;
            foreach (var (_, issue) in AllIssues())
                if (issue.Severity > worst) worst = issue.Severity;
            return worst;
        }

        /// <summary>True when at least one subsystem reports a Warning or worse.</summary>
        public static bool AnyAttention() => Worst() >= SubsystemHealthSeverity.Warning;

        private static List<ISubsystemHealth> Discover()
        {
            var list = new List<ISubsystemHealth>();
            foreach (var t in TypeCache.GetTypesDerivedFrom<ISubsystemHealth>())
            {
                if (t.IsAbstract || t.IsInterface) continue;
                try { list.Add((ISubsystemHealth)Activator.CreateInstance(t)); }
                catch (Exception e) { Debug.LogWarning($"[GameFramework] Could not create ISubsystemHealth '{t.FullName}': {e.Message}"); }
            }
            return list;
        }
    }
}
