namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Marks a collider with <see cref="ProtectionLevel.Basic"/> or <see cref="ProtectionLevel.Strong"/>.
    /// The package classifies the hit; it does not subtract HP.
    /// </summary>
    public interface IProjectileProtection
    {
        ProtectionLevel Level { get; }
    }
}
