namespace Heathen
{
    /// <summary>
    /// Declares who is authoritative over a construct, so a replication layer can honour it. The
    /// framework only <i>declares</i> authority; it enforces nothing and ships no networking. An optional
    /// Replication Framework / HLAPI adapter reads this to decide ownership and replication direction.
    /// </summary>
    public enum Authority
    {
        /// <summary>Authoritative on the server; replicated read-only to others. GameState/GameMode default.</summary>
        Server,
        /// <summary>Authoritative on the owning peer; replicated to observers. PlayerState default.</summary>
        Owner,
        /// <summary>Local / client-side only; not authoritative and not replicated.</summary>
        Client,
    }
}
