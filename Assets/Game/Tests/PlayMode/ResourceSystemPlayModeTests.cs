using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MicroJam.Game.Tests
{
    public sealed class ResourceSystemPlayModeTests
    {
        [UnityTest]
        public IEnumerator FreshRunsSpawnRandomValidPrefabPopulations()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;

            ResourcePopulationManager firstManager = UnityEngine.Object.FindFirstObjectByType<ResourcePopulationManager>();
            AssertPopulationAndPlacement(firstManager);
            HashSet<Vector2Int> firstLayout = GetAllCells(firstManager);

            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;

            ResourcePopulationManager secondManager = UnityEngine.Object.FindFirstObjectByType<ResourcePopulationManager>();
            AssertPopulationAndPlacement(secondManager);
            HashSet<Vector2Int> secondLayout = GetAllCells(secondManager);
            Assert.That(secondLayout.SetEquals(firstLayout), Is.False, "Two fresh runs unexpectedly produced the identical randomized 30-cell layout.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SuccessfulPlayerDamageGathersHealsAndProcessesFinalHitsOnce()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;

            ResourcePopulationManager manager = UnityEngine.Object.FindFirstObjectByType<ResourcePopulationManager>();
            GameObject player = GameObject.Find("Game/Actors/Player");
            PlayerInputController input = player.GetComponent<PlayerInputController>();
            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            PlayerFacing facing = player.GetComponent<PlayerFacing>();
            PlayerResourceWallet wallet = player.GetComponent<PlayerResourceWallet>();
            wallet.Configure(20, 20);
            Health playerHealth = player.GetComponent<Health>();
            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            input.enabled = false;
            playerHealth.ResetHealth();

            Assert.That(wallet.Wood, Is.EqualTo(20));
            Assert.That(wallet.Stone, Is.EqualTo(20));
            Assert.That(Enum.GetNames(typeof(PlayerResourceType)), Is.EquivalentTo(new[] { "Wood", "Stone" }), "No Berry resource may be introduced.");
            int walletNotifications = 0;
            wallet.ResourceChanged += _ => walletNotifications++;

            FindClearHorizontalPair(manager, out Vector2Int playerCell, out Vector2Int targetCell);
            Vector2 playerPosition = manager.WorldGrid.CellToWorldCenter(playerCell);
            player.transform.position = playerPosition;
            playerBody.position = playerPosition;
            playerBody.linearVelocity = Vector2.zero;
            facing.SetFacingDirection(Vector2.right);
            Physics2D.SyncTransforms();

            Assert.That(manager.TrySpawnAtCell(ResourceNodeType.Tree, targetCell, false, out ResourceNode tree), Is.True);
            Physics2D.SyncTransforms();
            yield return null;
            HealthBar treeBar = tree.GetComponentInChildren<HealthBar>(true);
            Assert.That(tree.Health.CurrentHealth, Is.EqualTo(50f));
            Assert.That(treeBar.IsVisible, Is.False);
            Assert.That(combat.TryAttackNow(out int treeHits), Is.True);
            Assert.That(treeHits, Is.EqualTo(1));
            Assert.That(tree.Health.CurrentHealth, Is.EqualTo(45f));
            Assert.That(wallet.Wood, Is.EqualTo(21));
            Assert.That(treeBar.IsVisible, Is.True);

            yield return new WaitForSeconds(combat.AttackCooldown + 0.01f);
            facing.SetFacingDirection(Vector2.left);
            Assert.That(combat.TryAttackNow(out int missHits), Is.True);
            Assert.That(missHits, Is.Zero);
            Assert.That(wallet.Wood, Is.EqualTo(21), "A miss must not gather Wood.");
            Assert.That(wallet.Stone, Is.EqualTo(20), "A miss must not gather Stone.");
            facing.SetFacingDirection(Vector2.right);

            GameObject nonPlayer = new("Non-Player Damage Source");
            Assert.That(tree.Health.TryTakeDamage(new DamageContext(5f, nonPlayer)), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(21), "Non-Player damage must not gather Wood.");
            Assert.That(tree.Health.TryTakeDamage(new DamageContext(100f, player)), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(22), "Final killing hit must still grant exactly one Wood.");
            Assert.That(tree.Health.TryTakeDamage(new DamageContext(5f, player)), Is.False);
            Assert.That(wallet.Wood, Is.EqualTo(22), "Dead Tree must not reward again.");
            yield return null;
            Assert.That(tree == null, Is.True, "Dead Tree was not destroyed at end of frame.");

            Assert.That(manager.TrySpawnAtCell(ResourceNodeType.Rock, targetCell, false, out ResourceNode rock), Is.True);
            Assert.That(rock.Health.CurrentHealth, Is.EqualTo(50f));
            Assert.That(rock.Health.TryTakeDamage(new DamageContext(5f, nonPlayer)), Is.True);
            Assert.That(wallet.Stone, Is.EqualTo(20), "Non-Player damage must not gather Stone.");
            Assert.That(rock.Health.TryTakeDamage(new DamageContext(5f, player)), Is.True);
            Assert.That(wallet.Stone, Is.EqualTo(21));
            Assert.That(rock.GetComponentInChildren<HealthBar>(true).IsVisible, Is.True);
            Assert.That(rock.Health.TryTakeDamage(new DamageContext(100f, player)), Is.True);
            Assert.That(wallet.Stone, Is.EqualTo(22), "Final Rock hit must grant exactly one Stone.");
            yield return null;
            Assert.That(rock == null, Is.True);

            Assert.That(manager.TrySpawnAtCell(ResourceNodeType.Bush, targetCell, false, out ResourceNode bush), Is.True);
            Assert.That(playerHealth.TryTakeDamage(new DamageContext(50f)), Is.True);
            Assert.That(bush.Health.TryTakeDamage(new DamageContext(5f, player)), Is.True);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60f), "Bush should heal 10% of Player Max Health.");
            Assert.That(bush.GetComponentInChildren<HealthBar>(true).IsVisible, Is.True, "Bush HealthBar should respond to successful damage.");

            playerHealth.ResetHealth();
            Assert.That(playerHealth.TryTakeDamage(new DamageContext(5f)), Is.True);
            Assert.That(bush.Health.TryTakeDamage(new DamageContext(5f, player)), Is.True);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f), "Bush healing must clamp to maximum Health.");
            Assert.That(bush.Health.TryTakeDamage(new DamageContext(5f, player)), Is.True);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f), "Full-health Player must remain at maximum while Bush still takes damage.");

            Assert.That(bush.Health.TryTakeDamage(new DamageContext(5f, nonPlayer)), Is.True);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f), "Non-Player Bush damage must not heal Player.");
            playerHealth.ResetHealth();
            Assert.That(playerHealth.TryTakeDamage(new DamageContext(50f)), Is.True);
            Assert.That(bush.Health.TryTakeDamage(new DamageContext(100f, player)), Is.True);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(60f), "Final Bush hit must still apply its heal.");
            yield return null;
            Assert.That(bush == null, Is.True);

            Assert.That(walletNotifications, Is.EqualTo(4), "Each successful Tree/Rock Player hit should emit one wallet notification.");
            UnityEngine.Object.Destroy(nonPlayer);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MinimumPopulationRestoresOnlyBelowFiveAndOnlyOutsideBuildZone()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;

            ResourcePopulationManager manager = UnityEngine.Object.FindFirstObjectByType<ResourcePopulationManager>();
            GameObject nonPlayer = new("Population Test Damage Source");
            foreach (ResourceNodeType type in Enum.GetValues(typeof(ResourceNodeType)))
            {
                Assert.That(manager.GetActiveCount(type), Is.EqualTo(10));
                ResourceNode[] initial = manager.GetActiveNodesSnapshot(type);
                for (int i = 0; i < 5; i++)
                {
                    Assert.That(initial[i].Health.TryTakeDamage(new DamageContext(1000f, nonPlayer)), Is.True);
                }

                Assert.That(manager.GetActiveCount(type), Is.EqualTo(5), $"{type} should decline from 10 to 5 without replacement.");
                Assert.That(manager.GetActiveNodesSnapshot(type).Any(node => node.IsReplacementSpawn), Is.False);

                ResourceNode sixth = manager.GetActiveNodesSnapshot(type)[0];
                Assert.That(sixth.Health.TryTakeDamage(new DamageContext(1000f, nonPlayer)), Is.True);
                Assert.That(manager.GetActiveCount(type), Is.EqualTo(5), $"{type} should immediately restore from 4 to minimum 5.");
                ResourceNode replacement = manager.GetActiveNodesSnapshot(type).Single(node => node.IsReplacementSpawn);
                Assert.That(manager.WorldGrid.Config.IsCellInsidePlayableArea(replacement.OccupiedCell), Is.True);
                Assert.That(manager.WorldGrid.IsCellInsideBuildZone(replacement.OccupiedCell), Is.False, $"Replacement {type} spawned inside Build Zone.");
                Assert.That(manager.Occupancy.TryGetOccupant(replacement.OccupiedCell, out UnityEngine.Object occupant), Is.True);
                Assert.That(occupant, Is.SameAs(replacement));
                Assert.That(replacement.transform.position, Is.EqualTo((Vector3)manager.WorldGrid.CellToWorldCenter(replacement.OccupiedCell)));
            }

            yield return null;
            Assert.That(manager.ActiveTreeCount, Is.EqualTo(5));
            Assert.That(manager.ActiveRockCount, Is.EqualTo(5));
            Assert.That(manager.ActiveBushCount, Is.EqualTo(5));
            Assert.That(manager.Occupancy.OccupiedCellCount, Is.EqualTo(15));
            UnityEngine.Object.Destroy(nonPlayer);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertPopulationAndPlacement(ResourcePopulationManager manager)
        {
            Assert.That(manager, Is.Not.Null);
            Assert.That(manager.transform.parent.name, Is.EqualTo("Systems"));
            Assert.That(manager.ActiveTreeCount, Is.EqualTo(10));
            Assert.That(manager.ActiveRockCount, Is.EqualTo(10));
            Assert.That(manager.ActiveBushCount, Is.EqualTo(10));
            Assert.That(manager.ActiveNodeCount, Is.EqualTo(30));
            Assert.That(manager.Occupancy.OccupiedCellCount, Is.EqualTo(30));

            HashSet<Vector2Int> uniqueCells = new();
            foreach (ResourceNodeType type in Enum.GetValues(typeof(ResourceNodeType)))
            {
                ResourceNode[] nodes = manager.GetActiveNodesSnapshot(type);
                Assert.That(nodes.Length, Is.EqualTo(10));
                foreach (ResourceNode node in nodes)
                {
                    Assert.That(node.NodeType, Is.EqualTo(type));
                    Assert.That(node.IsRegistered, Is.True);
                    Assert.That(node.PopulationManager, Is.SameAs(manager));
                    Assert.That(node.IsReplacementSpawn, Is.False);
                    Assert.That(node.Health.CurrentHealth, Is.EqualTo(node.Health.MaxHealth));
                    Assert.That(node.GetComponentInChildren<HealthBar>(true).IsVisible, Is.False);
                    Assert.That(node.GetComponent<Collider2D>().isTrigger, Is.True);
                    Assert.That(manager.WorldGrid.Config.IsCellInsidePlayableArea(node.OccupiedCell), Is.True);
                    Assert.That(manager.WorldGrid.Config.ProtectedCampfireCellRect.Contains(node.OccupiedCell), Is.False);
                    Assert.That(node.transform.position, Is.EqualTo((Vector3)manager.WorldGrid.CellToWorldCenter(node.OccupiedCell)));
                    Assert.That(uniqueCells.Add(node.OccupiedCell), Is.True, "Two resources occupied the same grid cell.");
                    Assert.That(manager.Occupancy.TryGetOccupant(node.OccupiedCell, out UnityEngine.Object occupant), Is.True);
                    Assert.That(occupant, Is.SameAs(node));
                    Assert.That(node.transform.parent, Is.SameAs(GetParent(manager, type)));
                }
            }

            Assert.That(UnityEngine.Object.FindObjectsByType<ResourcePopulationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<GridOccupancyService>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        private static HashSet<Vector2Int> GetAllCells(ResourcePopulationManager manager)
        {
            HashSet<Vector2Int> cells = new();
            foreach (ResourceNodeType type in Enum.GetValues(typeof(ResourceNodeType)))
            {
                foreach (ResourceNode node in manager.GetActiveNodesSnapshot(type))
                {
                    cells.Add(node.OccupiedCell);
                }
            }

            return cells;
        }

        private static Transform GetParent(ResourcePopulationManager manager, ResourceNodeType type)
        {
            return type switch
            {
                ResourceNodeType.Tree => manager.Tree.RuntimeParent,
                ResourceNodeType.Rock => manager.Rock.RuntimeParent,
                ResourceNodeType.Bush => manager.Bush.RuntimeParent,
                _ => null
            };
        }

        private static void FindClearHorizontalPair(ResourcePopulationManager manager, out Vector2Int playerCell, out Vector2Int targetCell)
        {
            RectInt playable = manager.WorldGrid.Config.PlayableCellRect;
            for (int y = playable.yMin + 3; y < playable.yMax - 3; y++)
            {
                for (int x = playable.xMin + 3; x < playable.xMax - 4; x++)
                {
                    Vector2Int candidate = new(x, y);
                    bool clear = true;
                    for (int offsetY = -2; offsetY <= 2 && clear; offsetY++)
                    {
                        for (int offsetX = -2; offsetX <= 3; offsetX++)
                        {
                            Vector2Int nearby = candidate + new Vector2Int(offsetX, offsetY);
                            if (manager.Occupancy.IsCellOccupied(nearby) || manager.WorldGrid.Config.ProtectedCampfireCellRect.Contains(nearby))
                            {
                                clear = false;
                                break;
                            }
                        }
                    }

                    if (clear)
                    {
                        playerCell = candidate;
                        targetCell = candidate + Vector2Int.right;
                        return;
                    }
                }
            }

            throw new AssertionException("Could not find a clear test pair in the 50x50 resource grid.");
        }
    }
}
