namespace Davidvdveerdonk.ComVeerdonkProjectileSystem
{
    /// <summary>
    /// Game/sandbox contract for applying a status. The package never calls this;
    /// listeners map <see cref="IProjectileWorld.StatusApplied"/> to <see cref="Apply"/>.
    /// </summary>
    public interface IStatusReceiver
    {
        void Apply(IStatusEffect effect);
    }
}
