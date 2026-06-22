namespace DgProto
{
    /// <summary>
    /// Identifiers for every sound effect the game can play. Keep in sync with
    /// the entries on the <see cref="SoundBank"/> ScriptableObject — each id
    /// resolves to a (1..N)-clip array there.
    /// </summary>
    public enum SfxId
    {
        // Combat — AttackSwing is the generic swing whoosh; Kick and Stab are
        // the combo finishers (Q step 3 = Kick, E step 2 = Stab).
        AttackSwing,
        Kick,
        Stab,
        PlayerHurt,

        // Locomotion
        Footstep,
        Jump,
        Land,

        // Progression
        LevelUp,
        GameStart,
        GameOver,

        // World pickups
        DroppableSpawn,
        DroppablePickup,
    }
}
