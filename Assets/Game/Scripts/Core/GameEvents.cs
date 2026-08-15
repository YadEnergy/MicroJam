using System;

namespace MicroJam.Game
{
    public static class GameEvents
    {
        public static event Action CampfireDestroyed;

        public static void RaiseCampfireDestroyed() => CampfireDestroyed?.Invoke();
    }
}
