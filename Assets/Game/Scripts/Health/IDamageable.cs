namespace MicroJam.Game
{
    public interface IDamageable
    {
        bool TryTakeDamage(DamageContext context, out float appliedDamage);
    }
}
