namespace DgProto
{
    /// <summary>Something that can be briefly stunned — e.g. by a special attack.</summary>
    public interface IStunnable
    {
        void ApplyStun(float duration);
        bool IsStunned { get; }
    }
}
