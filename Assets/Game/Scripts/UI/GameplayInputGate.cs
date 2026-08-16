using System.Collections.Generic;
using UnityEngine;

namespace MicroJam.Game
{
    public static class GameplayInputGate
    {
        private static readonly HashSet<int> Blockers = new();

        public static bool IsBlocked => Blockers.Count > 0;
        public static int BlockerCount => Blockers.Count;

        public static void SetBlocked(Object owner, bool blocked)
        {
            if (owner == null) return;
            int id = owner.GetInstanceID();
            if (blocked) Blockers.Add(id);
            else Blockers.Remove(id);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Blockers.Clear();
    }
}
