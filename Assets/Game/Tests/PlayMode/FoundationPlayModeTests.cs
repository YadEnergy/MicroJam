using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MicroJam.Game.Tests
{
    public sealed class FoundationPlayModeTests
    {
        [UnityTest]
        public IEnumerator GameSceneEntersPlayModeWithValidFoundation()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;

            WorldGridService grid = Object.FindFirstObjectByType<WorldGridService>();
            SpawnPerimeterProvider spawn = Object.FindFirstObjectByType<SpawnPerimeterProvider>();
            SquareGameplayViewport viewport = Object.FindFirstObjectByType<SquareGameplayViewport>();

            Assert.That(grid, Is.Not.Null);
            Assert.That(grid.Config.PlayableSize, Is.EqualTo(new Vector2Int(50, 50)));
            Assert.That(grid.Config.BuildZoneSize, Is.EqualTo(new Vector2Int(30, 30)));
            Assert.That(viewport, Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Camera.main.orthographicSize, Is.EqualTo(25f).Within(0.001f));

            Rect pixelViewport = Camera.main.pixelRect;
            Assert.That(pixelViewport.width, Is.EqualTo(pixelViewport.height).Within(1f));

            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.DinosaurIndex, GameLayers.DinosaurIndex), Is.True);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.PlayerIndex, GameLayers.DoorIndex), Is.True);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.PlayerIndex, GameLayers.ResourceIndex), Is.True);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.DinosaurIndex, GameLayers.ResourceIndex), Is.True);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.PlayerIndex, GameLayers.BuildingIndex), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.DinosaurIndex, GameLayers.BuildingIndex), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.DinosaurIndex, GameLayers.DoorIndex), Is.False);

            foreach (SpawnSide side in System.Enum.GetValues(typeof(SpawnSide)))
            {
                Vector2 position = spawn.GetPosition(side, 0.5f);
                Assert.That(grid.Config.PlayableWorldBounds.Contains(position), Is.False);
            }

            LogAssert.NoUnexpectedReceived();
        }
    }
}
