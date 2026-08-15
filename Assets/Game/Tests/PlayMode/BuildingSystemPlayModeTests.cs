using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MicroJam.Game.Tests
{
    public sealed class BuildingSystemPlayModeTests
    {
        [UnityTest]
        public IEnumerator BuildModePreviewAndExistingGameplayRemainLive()
        {
            yield return LoadGame();

            BuildingSystem system = UnityEngine.Object.FindFirstObjectByType<BuildingSystem>();
            GameObject player = GameObject.Find("Game/Actors/Player");
            PlayerInputController playerInput = player.GetComponent<PlayerInputController>();
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            InputActionMap buildingMap = system.InputActions.FindActionMap("Building", true);

            Assert.That(system, Is.Not.Null);
            Assert.That(system.transform.parent.name, Is.EqualTo("Systems"));
            Assert.That(system.RuntimeBuildingParent.name, Is.EqualTo("Buildings"));
            Assert.That(system.HasValidInputActions, Is.True);
            Assert.That(buildingMap.FindAction("SelectWall", true).enabled, Is.True);
            Assert.That(buildingMap.FindAction("SelectDoor", true).enabled, Is.True);
            Assert.That(buildingMap.FindAction("Place", true).enabled, Is.True);
            Assert.That(playerInput.enabled, Is.True);
            Assert.That(playerInput.InputActions.FindAction("Player/Move", true).enabled, Is.True);
            Assert.That(playerInput.InputActions.FindAction("Player/Attack", true).enabled, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Build mode must not pause play.");

            system.SelectBuildMode(BuildSelection.Wall);
            Assert.That(system.Selection, Is.EqualTo(BuildSelection.Wall));
            system.SelectBuildMode(BuildSelection.Door);
            Assert.That(system.Selection, Is.EqualTo(BuildSelection.Door));
            system.CancelBuildMode();
            Assert.That(system.Selection, Is.EqualTo(BuildSelection.None));
            Assert.That(system.PlacementPreview.IsVisible, Is.False);

            Vector2Int cell = FindValidCell(system, system.WallDefinition);
            Vector2 cellCenter = system.WorldGrid.CellToWorldCenter(cell);
            Vector2 screen = Camera.main.WorldToScreenPoint(cellCenter);
            system.SelectBuildMode(BuildSelection.Wall);
            Assert.That(system.UpdateTargetFromScreen(screen), Is.EqualTo(BuildPlacementStatus.Valid));
            Assert.That(system.TargetCell, Is.EqualTo(cell));
            Assert.That(system.PlacementPreview.IsVisible, Is.True);
            Assert.That(system.PlacementPreview.ShowsValidPlacement, Is.True);
            Assert.That(system.PlacementPreview.transform.position, Is.EqualTo((Vector3)cellCenter));
            Assert.That(system.PlacementPreview.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(system.PlacementPreview.GetComponent<Collider2D>(), Is.Null);
            Assert.That(system.PlacementPreview.GetComponent<Health>(), Is.Null);
            Assert.That(system.Occupancy.IsCellOccupied(cell), Is.False, "A ghost must never reserve occupancy.");

            int woodBeforeUiBlock = system.PlayerWallet.Wood;
            Assert.That(system.UpdateTargetFromScreen(screen, true), Is.EqualTo(BuildPlacementStatus.PointerOverUi));
            Assert.That(system.PlacementPreview.IsVisible, Is.True);
            Assert.That(system.PlacementPreview.ShowsValidPlacement, Is.False);
            Assert.That(system.TryPlaceTargeted(out _), Is.False);
            Assert.That(system.PlayerWallet.Wood, Is.EqualTo(woodBeforeUiBlock));
            Assert.That(system.Occupancy.IsCellOccupied(cell), Is.False);

            Rect viewport = system.GameplayViewport.PixelGameplayViewport;
            Vector2 outside = new(viewport.xMin - 1f, viewport.center.y);
            Assert.That(system.UpdateTargetFromScreen(outside), Is.EqualTo(BuildPlacementStatus.OutsideViewport));
            Assert.That(system.HasTargetCell, Is.False);
            Assert.That(system.PlacementPreview.IsVisible, Is.False);

            system.SelectBuildMode(BuildSelection.Door);
            movement.SetMoveInput(Vector2.up);
            yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f), "Player movement stopped in build mode.");
            movement.SetMoveInput(Vector2.zero);
            Assert.That(combat.TryAttackNow(out _), Is.True, "E/melee behavior must remain usable in build mode.");
            Assert.That(system.Selection, Is.EqualTo(BuildSelection.Door));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PlacementIsTransactionalPersistentDamageableAndReleasesOccupancy()
        {
            yield return LoadGame();

            BuildingSystem system = UnityEngine.Object.FindFirstObjectByType<BuildingSystem>();
            PlayerResourceWallet wallet = system.PlayerWallet;
            int initialOccupied = system.Occupancy.OccupiedCellCount;
            int initialStone = wallet.Stone;
            List<Vector2Int> cells = FindValidCells(system, system.WallDefinition, 4);

            system.SelectBuildMode(BuildSelection.Wall);
            Assert.That(system.TryPlaceAtCell(system.WallDefinition, cells[0], out BuildingInstance wallOne), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(15));
            Assert.That(system.Selection, Is.EqualTo(BuildSelection.Wall), "A successful placement must keep build selection active.");
            AssertPlacedBuilding(system, wallOne, system.WallDefinition, cells[0], 150f, GameLayers.BuildingIndex);

            int childCountAfterFirst = system.RuntimeBuildingParent.childCount;
            Assert.That(system.TryPlaceAtCell(system.WallDefinition, cells[0], out _), Is.False);
            Assert.That(system.PlacementStatus, Is.EqualTo(BuildPlacementStatus.Occupied));
            Assert.That(wallet.Wood, Is.EqualTo(15), "Invalid placement spent Wood.");
            Assert.That(system.RuntimeBuildingParent.childCount, Is.EqualTo(childCountAfterFirst), "Invalid placement created an object.");

            Assert.That(system.TryPlaceAtCell(system.WallDefinition, cells[1], out BuildingInstance wallTwo), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(10));
            Assert.That(system.Selection, Is.EqualTo(BuildSelection.Wall));
            AssertPlacedBuilding(system, wallTwo, system.WallDefinition, cells[1], 150f, GameLayers.BuildingIndex);

            system.SelectBuildMode(BuildSelection.Door);
            Assert.That(system.TryPlaceAtCell(system.DoorDefinition, cells[2], out BuildingInstance door), Is.True);
            Assert.That(wallet.Wood, Is.Zero);
            Assert.That(system.Selection, Is.EqualTo(BuildSelection.Door));
            AssertPlacedBuilding(system, door, system.DoorDefinition, cells[2], 100f, GameLayers.DoorIndex);
            Assert.That(system.Occupancy.OccupiedCellCount, Is.EqualTo(initialOccupied + 3));

            Assert.That(system.TryPlaceAtCell(system.DoorDefinition, cells[3], out _), Is.False);
            Assert.That(system.PlacementStatus, Is.EqualTo(BuildPlacementStatus.InsufficientWood));
            Assert.That(system.TryPlaceAtCell(system.WallDefinition, cells[3], out _), Is.False);
            Assert.That(system.PlacementStatus, Is.EqualTo(BuildPlacementStatus.InsufficientWood));
            Assert.That(wallet.Wood, Is.Zero);
            Assert.That(wallet.Stone, Is.EqualTo(initialStone), "Building costs must never consume Stone.");
            Assert.That(system.Occupancy.IsCellOccupied(cells[3]), Is.False);

            GameObject damageSource = new("Building Damage Test Source");
            Assert.That(wallOne.Health.TryTakeDamage(new DamageContext(5f, damageSource)), Is.True);
            Assert.That(wallOne.Health.CurrentHealth, Is.EqualTo(145f));
            Assert.That(wallOne.GetComponentInChildren<HealthBar>(true).IsVisible, Is.True);
            Assert.That(door.Health.TryTakeDamage(new DamageContext(5f, damageSource)), Is.True);
            Assert.That(door.Health.CurrentHealth, Is.EqualTo(95f));
            Assert.That(door.GetComponentInChildren<HealthBar>(true).IsVisible, Is.True);

            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.PlayerIndex, GameLayers.BuildingIndex), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.DinosaurIndex, GameLayers.BuildingIndex), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.PlayerIndex, GameLayers.DoorIndex), Is.True);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.DinosaurIndex, GameLayers.DoorIndex), Is.False);

            UnityEngine.Object.Destroy(wallOne.gameObject);
            UnityEngine.Object.Destroy(damageSource);
            yield return null;
            Assert.That(system.Occupancy.IsCellOccupied(cells[0]), Is.False, "Destroyed building did not release logical occupancy.");
            Assert.That(system.Occupancy.OccupiedCellCount, Is.EqualTo(initialOccupied + 2));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ValidationRejectsZoneProtectionResourcesAndDynamicActorsAcrossTheWholeGrid()
        {
            yield return LoadGame();

            BuildingSystem system = UnityEngine.Object.FindFirstObjectByType<BuildingSystem>();
            ResourcePopulationManager resources = UnityEngine.Object.FindFirstObjectByType<ResourcePopulationManager>();
            WorldGridConfig config = system.WorldGrid.Config;
            RectInt buildZone = config.BuildZoneCellRect;
            RectInt protectedZone = config.ProtectedCampfireCellRect;

            Vector2Int outsideBuildZone = new(buildZone.xMin - 1, buildZone.yMin);
            Assert.That(system.EvaluatePlacement(system.WallDefinition, outsideBuildZone), Is.EqualTo(BuildPlacementStatus.OutsideBuildZone));
            Assert.That(system.EvaluatePlacement(system.DoorDefinition, outsideBuildZone), Is.EqualTo(BuildPlacementStatus.OutsideBuildZone));
            Assert.That(system.EvaluatePlacement(system.WallDefinition, config.CampfireCellRect.position), Is.EqualTo(BuildPlacementStatus.ProtectedCampfire));
            Vector2Int protectedBorder = new(protectedZone.xMin, protectedZone.yMin);
            Assert.That(config.CampfireCellRect.Contains(protectedBorder), Is.False, "Test cell must exercise padding, not the footprint.");
            Assert.That(system.EvaluatePlacement(system.WallDefinition, protectedBorder), Is.EqualTo(BuildPlacementStatus.ProtectedCampfire));

            foreach (ResourceNodeType type in new[] { ResourceNodeType.Tree, ResourceNodeType.Rock, ResourceNodeType.Bush })
            {
                Vector2Int resourceCell = FindValidCell(system, system.WallDefinition);
                Assert.That(resources.TrySpawnAtCell(type, resourceCell, false, out ResourceNode node), Is.True);
                Assert.That(system.EvaluatePlacement(system.WallDefinition, resourceCell), Is.EqualTo(BuildPlacementStatus.Occupied), $"{type} did not block building occupancy.");
                UnityEngine.Object.Destroy(node.gameObject);
                yield return null;
                Assert.That(system.Occupancy.IsCellOccupied(resourceCell), Is.False);
            }

            GameObject player = GameObject.Find("Game/Actors/Player");
            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            Vector2Int playerCell = FindValidCell(system, system.WallDefinition);
            MoveBody(player, playerBody, system.WorldGrid.CellToWorldCenter(playerCell));
            Assert.That(system.EvaluatePlacement(system.WallDefinition, playerCell), Is.EqualTo(BuildPlacementStatus.DynamicOverlap));

            Vector2Int dinosaurCell = FindRawClearBuildCell(system, playerCell);
            GameObject dinosaur = new("Dynamic Dinosaur Placement Blocker") { layer = GameLayers.DinosaurIndex };
            dinosaur.transform.position = system.WorldGrid.CellToWorldCenter(dinosaurCell);
            dinosaur.AddComponent<CircleCollider2D>().radius = 0.4f;
            Physics2D.SyncTransforms();
            Assert.That(system.EvaluatePlacement(system.DoorDefinition, dinosaurCell), Is.EqualTo(BuildPlacementStatus.DynamicOverlap));

            MoveBody(player, playerBody, new Vector2(3f, 0f));
            UnityEngine.Object.Destroy(dinosaur);
            yield return null;

            Vector2Int farEdge = FindFarBuildEdgeCell(system);
            float distance = Vector2.Distance(player.transform.position, system.WorldGrid.CellToWorldCenter(farEdge));
            Assert.That(distance, Is.GreaterThan(10f), "Test setup did not exercise unrestricted build distance.");
            Assert.That(system.TryPlaceAtCell(system.WallDefinition, farEdge, out BuildingInstance farWall), Is.True);
            Assert.That(farWall.transform.position, Is.EqualTo((Vector3)system.WorldGrid.CellToWorldCenter(farEdge)));

            Vector2Int[] footprint = BuildingSystem.GetFootprintCells(new Vector2Int(4, 7), new Vector2Int(2, 3));
            Assert.That(footprint, Is.EquivalentTo(new[]
            {
                new Vector2Int(4, 7), new Vector2Int(5, 7),
                new Vector2Int(4, 8), new Vector2Int(5, 8),
                new Vector2Int(4, 9), new Vector2Int(5, 9)
            }), "Future multi-cell footprint expansion is not enumerating every cell.");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            UnityEngine.Object.FindFirstObjectByType<PlayerResourceWallet>()?.Configure(20, 20);
            Physics2D.SyncTransforms();
        }

        private static void AssertPlacedBuilding(
            BuildingSystem system,
            BuildingInstance instance,
            BuildingDefinition definition,
            Vector2Int cell,
            float maxHealth,
            int layer)
        {
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.transform.parent, Is.SameAs(system.RuntimeBuildingParent));
            Assert.That(instance.Definition, Is.SameAs(definition));
            Assert.That(instance.IsRegistered, Is.True);
            Assert.That(instance.OccupiedCells, Is.EquivalentTo(new[] { cell }));
            Assert.That(instance.Footprint.SizeInCells, Is.EqualTo(Vector2Int.one));
            Assert.That(instance.transform.position, Is.EqualTo((Vector3)system.WorldGrid.CellToWorldCenter(cell)));
            Assert.That(instance.gameObject.layer, Is.EqualTo(layer));
            Assert.That(instance.GetComponent<BoxCollider2D>().isTrigger, Is.False);
            Assert.That(instance.Health.MaxHealth, Is.EqualTo(maxHealth));
            Assert.That(instance.Health.CurrentHealth, Is.EqualTo(maxHealth));
            Assert.That(instance.GetComponentInChildren<HealthBar>(true).IsVisible, Is.False);
            Assert.That(system.Occupancy.TryGetOccupant(cell, out UnityEngine.Object occupant), Is.True);
            Assert.That(occupant, Is.SameAs(instance));
        }

        private static Vector2Int FindValidCell(BuildingSystem system, BuildingDefinition definition)
        {
            foreach (Vector2Int cell in system.WorldGrid.Config.BuildZoneCellRect.allPositionsWithin)
            {
                if (system.EvaluatePlacement(definition, cell) == BuildPlacementStatus.Valid)
                {
                    return cell;
                }
            }

            throw new AssertionException("Could not find a valid build cell.");
        }

        private static List<Vector2Int> FindValidCells(BuildingSystem system, BuildingDefinition definition, int count)
        {
            List<Vector2Int> result = new();
            foreach (Vector2Int cell in system.WorldGrid.Config.BuildZoneCellRect.allPositionsWithin)
            {
                if (system.EvaluatePlacement(definition, cell) == BuildPlacementStatus.Valid &&
                    result.TrueForAll(existing => Mathf.Abs(existing.x - cell.x) + Mathf.Abs(existing.y - cell.y) > 1))
                {
                    result.Add(cell);
                    if (result.Count == count)
                    {
                        return result;
                    }
                }
            }

            throw new AssertionException($"Could not find {count} valid build cells.");
        }

        private static Vector2Int FindRawClearBuildCell(BuildingSystem system, Vector2Int excluded)
        {
            foreach (Vector2Int cell in system.WorldGrid.Config.BuildZoneCellRect.allPositionsWithin)
            {
                if (cell != excluded && !system.WorldGrid.Config.IsCellProtectedFromBuilding(cell) &&
                    !system.Occupancy.IsCellOccupied(cell) &&
                    Physics2D.OverlapBox(system.WorldGrid.CellToWorldCenter(cell), Vector2.one * 0.8f, 0f,
                        system.DynamicOccupantLayers) == null)
                {
                    return cell;
                }
            }

            throw new AssertionException("Could not find a raw clear build cell.");
        }

        private static Vector2Int FindFarBuildEdgeCell(BuildingSystem system)
        {
            RectInt zone = system.WorldGrid.Config.BuildZoneCellRect;
            Vector2Int[] corners =
            {
                new(zone.xMin, zone.yMin),
                new(zone.xMax - 1, zone.yMin),
                new(zone.xMin, zone.yMax - 1),
                new(zone.xMax - 1, zone.yMax - 1)
            };
            foreach (Vector2Int cell in corners)
            {
                if (system.EvaluatePlacement(system.WallDefinition, cell) == BuildPlacementStatus.Valid)
                {
                    return cell;
                }
            }

            throw new AssertionException("No far build-zone edge cell was valid.");
        }

        private static void MoveBody(GameObject owner, Rigidbody2D body, Vector2 position)
        {
            owner.transform.position = position;
            body.position = position;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }
    }
}
