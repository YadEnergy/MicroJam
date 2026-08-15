using UnityEngine;

namespace MicroJam.Game
{
    public static class GameLayers
    {
        public const string Player = "Player";
        public const string Dinosaur = "Dinosaur";
        public const string Building = "Building";
        public const string Resource = "Resource";
        public const string WorldBoundary = "WorldBoundary";
        public const string Door = "Door";

        public static int PlayerIndex => LayerMask.NameToLayer(Player);
        public static int DinosaurIndex => LayerMask.NameToLayer(Dinosaur);
        public static int BuildingIndex => LayerMask.NameToLayer(Building);
        public static int ResourceIndex => LayerMask.NameToLayer(Resource);
        public static int WorldBoundaryIndex => LayerMask.NameToLayer(WorldBoundary);
        public static int DoorIndex => LayerMask.NameToLayer(Door);
    }
}
