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
    /// How a subsystem comes up. A fundamental, tool-agnostic affordance: the package being present no longer
    /// implies the external system must run. Lets a developer ship a tool's assemblies but keep it dormant
    /// (e.g. include Steamworks in an itch.io build without ever initialising Steam), or defer activation until
    /// the game decides.
    /// </summary>
    public enum SubsystemStartMode
    {
        /// <summary>Not created at all this session. The default <see cref="Subsystem.ShouldCreate"/> returns false.</summary>
        Disabled,
        /// <summary>Created and lifecycle-managed, but the subsystem defers activating its external system in
        /// <see cref="Subsystem.Initialize"/> until something explicitly asks it to start.</summary>
        OnDemand,
        /// <summary>Created and activates its external system in <see cref="Subsystem.Initialize"/> (default).</summary>
        Automatic,
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
        /// How this subsystem comes up (default <see cref="SubsystemStartMode.Automatic"/>). Override to read a
        /// developer-set, runtime-readable value (e.g. baked into generated code) so a tool can be disabled or
        /// deferred without removing its package. <see cref="SubsystemStartMode.Disabled"/> means the default
        /// <see cref="ShouldCreate"/> declines to create it at all; <see cref="SubsystemStartMode.OnDemand"/>
        /// vs <see cref="SubsystemStartMode.Automatic"/> is honoured by the subsystem inside <see cref="Initialize"/>.
        /// </summary>
        public virtual SubsystemStartMode StartMode => SubsystemStartMode.Automatic;

        /// <summary>
        /// Return <c>false</c> to opt this subsystem out for the current session/world. The default declines
        /// only when <see cref="StartMode"/> is <see cref="SubsystemStartMode.Disabled"/> (the package being
        /// present is otherwise "enabled"); override for additional conditional creation. Evaluated after
        /// construction, before <see cref="Initialize"/>.
        /// </summary>
        public virtual bool ShouldCreate() => StartMode != SubsystemStartMode.Disabled;

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
