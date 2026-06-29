namespace Heathen
{
    // Opt-in tick contracts. A subsystem implements only the phases it needs; the framework drives
    // them via the PlayerLoop (see P2). These are pure contracts: the framework dispatches the calls,
    // it never puts game logic inside them.
    //
    // Phase order within a frame:
    //   BeforeFixed -> [Unity FixedUpdate] -> OnFixed -> AfterFixed   (per physics step, may repeat)
    //   BeforeUpdate -> [Unity Update]     -> OnUpdate -> AfterUpdate (once per frame)

    /// <summary>Called immediately before Unity's FixedUpdate phase.</summary>
    public interface IBeforeFixed { void BeforeFixed(float deltaTime); }

    /// <summary>Called within Unity's FixedUpdate phase.</summary>
    public interface IOnFixed { void OnFixed(float deltaTime); }

    /// <summary>Called immediately after Unity's FixedUpdate phase.</summary>
    public interface IAfterFixed { void AfterFixed(float deltaTime); }

    /// <summary>Called immediately before Unity's Update phase.</summary>
    public interface IBeforeUpdate { void BeforeUpdate(float deltaTime); }

    /// <summary>Called within Unity's Update phase.</summary>
    public interface IOnUpdate { void OnUpdate(float deltaTime); }

    /// <summary>Called immediately after Unity's Update phase.</summary>
    public interface IAfterUpdate { void AfterUpdate(float deltaTime); }
}
