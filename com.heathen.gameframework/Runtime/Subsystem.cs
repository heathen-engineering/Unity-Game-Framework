using System;
using System.Reflection;

namespace Heathen
{
    /// <summary>The lifetime a subsystem is bound to.</summary>
    public enum SubsystemScope
    {
        /// <summary>One instance per process, created at framework boot, lives for the whole session.</summary>
        Global,
        /// <summary>One instance per <see cref="World"/>, created and destroyed with that world.</summary>
        World,
    }

    /// <summary>
    /// Declares a type as a framework subsystem and fixes its <see cref="SubsystemScope"/>. The scope
    /// is read at discovery time (before any instance is constructed), which is why it lives on an
    /// attribute rather than only on a virtual property: the framework must know a type's scope without
    /// constructing it, so that <see cref="SubsystemScope.World"/> subsystems are never built globally.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class SubsystemAttribute : Attribute
    {
        public SubsystemScope Scope { get; }
        public SubsystemAttribute(SubsystemScope scope = SubsystemScope.Global) => Scope = scope;
    }

    /// <summary>
    /// Base for a statically-accessible, lifecycle-managed singleton with no GameObject and no scene
    /// presence (modelled on Unreal's subsystem family). A <see cref="SubsystemScope.Global"/> subsystem
    /// is reached via <see cref="GameFramework.Get{T}"/>; a <see cref="SubsystemScope.World"/> subsystem
    /// via <c>world.Get&lt;T&gt;()</c>.
    ///
    /// <para><b>Constructors must be trivial and side-effect-free.</b> The framework may construct a type
    /// purely to inspect it; all real work belongs in <see cref="Initialize"/> / <see cref="Deinitialize"/>.</para>
    /// </summary>
    public abstract class Subsystem
    {
        /// <summary>The scope declared by this type's <see cref="SubsystemAttribute"/> (default Global).</summary>
        public SubsystemScope Scope =>
            GetType().GetCustomAttribute<SubsystemAttribute>(false)?.Scope ?? SubsystemScope.Global;

        /// <summary>
        /// The owning <see cref="World"/> when <see cref="Scope"/> is
        /// <see cref="SubsystemScope.World"/>; <c>null</c> for Global subsystems. Set by the framework
        /// after construction and before <see cref="ShouldCreate"/> / <see cref="Initialize"/>, so both
        /// may read it (e.g. to opt out for a particular world).
        /// </summary>
        public World World { get; internal set; }

        /// <summary>
        /// Other subsystem types that must be initialised before this one. Within the same scope the
        /// framework brings dependencies up first (and tears them down after). Override to declare an
        /// ordering requirement, e.g. HATE requiring the GameplayTags subsystem.
        /// </summary>
        public virtual Type[] DependsOn => Array.Empty<Type>();

        /// <summary>
        /// Return <c>false</c> to opt this subsystem out for the current session/world (the package being
        /// present is the default "enabled"; override for conditional creation). Evaluated after
        /// construction, before <see cref="Initialize"/>.
        /// </summary>
        public virtual bool ShouldCreate() => true;

        /// <summary><c>true</c> between a successful <see cref="Initialize"/> and its <see cref="Deinitialize"/>.</summary>
        public bool IsInitialised { get; private set; }

        internal void DoInitialize()
        {
            if (IsInitialised) return;
            Initialize();
            IsInitialised = true;
        }

        internal void DoDeinitialize()
        {
            if (!IsInitialised) return;
            Deinitialize();
            IsInitialised = false;
        }

        /// <summary>Bring the subsystem up. Acquire resources, register tags, etc. Called once.</summary>
        protected virtual void Initialize() { }

        /// <summary>Tear the subsystem down. Release everything acquired in <see cref="Initialize"/>.</summary>
        protected virtual void Deinitialize() { }
    }
}
