namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Marks a collider as <see cref="HitKind.Target"/>. The package does not apply HP.
    /// </summary>
    public interface IDamageable
    {
        TargetId TargetId { get; }
    }
}
