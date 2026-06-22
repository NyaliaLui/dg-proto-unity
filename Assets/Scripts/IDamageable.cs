namespace DgProto
{
    /// <summary>Anything that can receive damage — characters, destructibles, etc.</summary>
    public interface IDamageable
    {
        void TakeDamage(int amount);
    }
}
