using System;
using UnityEngine;

namespace MicroJam.Game
{
    public static class PlayerPoints
    {
        public static int Current { get; private set; }

        public static event Action<int> Changed;

        public static void Reset()
        {
            Current = 0;
            Changed?.Invoke(Current);
        }

        public static void Add(int amount)
        {
            if (amount <= 0) return;

            Current += amount;
            Changed?.Invoke(Current);
        }
    }
}
