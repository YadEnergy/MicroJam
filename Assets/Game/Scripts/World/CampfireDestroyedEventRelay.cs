using UnityEngine;

namespace MicroJam.Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class CampfireDestroyedEventRelay : MonoBehaviour
    {
        private Health health;
        private bool eventSent;

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= OnDied;
            }
        }

        private void OnDied(DeathEvent _)
        {
            if (eventSent) return;

            eventSent = true;
            GameEvents.RaiseCampfireDestroyed();
        }
    }
}
