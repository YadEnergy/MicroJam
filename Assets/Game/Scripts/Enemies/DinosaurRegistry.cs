using System.Collections.Generic;

namespace MicroJam.Game
{
    public static class DinosaurRegistry
    {
        private static readonly HashSet<DinosaurAgent> Active = new();

        public static IEnumerable<DinosaurAgent> ActiveDinosaurs => Active;
        public static int Count => Active.Count;

        public static void Register(DinosaurAgent dinosaur)
        {
            if (dinosaur != null) Active.Add(dinosaur);
        }

        public static void Unregister(DinosaurAgent dinosaur)
        {
            if (dinosaur != null) Active.Remove(dinosaur);
        }
    }
}
