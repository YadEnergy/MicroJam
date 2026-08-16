using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MicroJam.Game.Tests
{
    public sealed class TowerPlayModeTests
    {
        [UnityTest]
        public IEnumerator TowerAssetsControlsPlacementCostsOccupancyAndRefundsAreConfigured()
        {
            yield return LoadGame();
            BuildingSystem system = Object.FindFirstObjectByType<BuildingSystem>();
            PlayerResourceWallet wallet = system.PlayerWallet;
            BuildingDefinition bow = system.BowTowerDefinition;
            BuildingDefinition stone = system.StoneTowerDefinition;

            AssertDefinition(bow, BuildingType.BowTower, "Bow Tower", 20, 10, 100f, 30f, 5f, 0.5f, 20f);
            AssertDefinition(stone, BuildingType.StoneTower, "Stone Tower", 10, 20, 150f, 15f, 25f, 2f, 12f);
            InputActionMap map = system.InputActions.FindActionMap("Building", true);
            Assert.That(map.FindAction("SelectBowTower", true).bindings[0].effectivePath, Is.EqualTo("<Keyboard>/3"));
            Assert.That(map.FindAction("SelectStoneTower", true).bindings[0].effectivePath, Is.EqualTo("<Keyboard>/4"));
            Assert.That(system.HasValidInputActions, Is.True);

            wallet.Configure(100, 100);
            Vector2Int bowCell = FindValidCell(system, bow);
            Vector2 bowCenter = FootprintCenter(system, bowCell, bow.FootprintSize);
            system.SelectBuildMode(BuildSelection.BowTower);
            Vector2 bowAnchorCenter = system.WorldGrid.CellToWorldCenter(bowCell);
            Assert.That(system.UpdateTargetFromScreen(Camera.main.WorldToScreenPoint(bowAnchorCenter)), Is.EqualTo(BuildPlacementStatus.Valid));
            Assert.That(system.PlacementPreview.CurrentDefinition, Is.SameAs(bow));
            Assert.That(system.PlacementPreview.transform.position, Is.EqualTo((Vector3)bowCenter));
            Assert.That(system.PlacementPreview.transform.localScale, Is.EqualTo(new Vector3(2f, 2f, 1f)));
            Assert.That(system.PlacementPreview.ShowsValidPlacement, Is.True);
            Assert.That(system.TryPlaceTargeted(out BuildingInstance bowTower), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(80));
            Assert.That(wallet.Stone, Is.EqualTo(90));
            AssertTowerInstance(system, bowTower, bow, bowCell, 100f);

            Vector2Int[] bowCells = bowTower.OccupiedCells;
            Assert.That(bowTower.TryRemoveByPlayer(wallet), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(90));
            Assert.That(wallet.Stone, Is.EqualTo(95));
            yield return null;
            AssertCellsFree(system, bowCells);

            Vector2Int stoneCell = FindValidCell(system, stone);
            Assert.That(system.TryPlaceAtCell(stone, stoneCell, out BuildingInstance stoneTower), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(80));
            Assert.That(wallet.Stone, Is.EqualTo(75));
            AssertTowerInstance(system, stoneTower, stone, stoneCell, 150f);
            Vector2Int[] stoneCells = stoneTower.OccupiedCells;
            Assert.That(stoneTower.TryRemoveByPlayer(wallet), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(85));
            Assert.That(wallet.Stone, Is.EqualTo(85));
            yield return null;
            AssertCellsFree(system, stoneCells);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TowerPlacementValidatesEveryFootprintCellAndSpendingIsAtomic()
        {
            yield return LoadGame();
            BuildingSystem system = Object.FindFirstObjectByType<BuildingSystem>();
            PlayerResourceWallet wallet = system.PlayerWallet;
            Vector2Int anchor = FindValidCell(system, system.BowTowerDefinition);

            wallet.Configure(20, 9);
            Assert.That(system.EvaluatePlacement(system.BowTowerDefinition, anchor), Is.EqualTo(BuildPlacementStatus.InsufficientResources));
            Assert.That(system.TryPlaceAtCell(system.BowTowerDefinition, anchor, out _), Is.False);
            Assert.That(wallet.Wood, Is.EqualTo(20));
            Assert.That(wallet.Stone, Is.EqualTo(9));

            wallet.Configure(9, 20);
            Assert.That(system.EvaluatePlacement(system.StoneTowerDefinition, anchor), Is.EqualTo(BuildPlacementStatus.InsufficientResources));
            Assert.That(system.TryPlaceAtCell(system.StoneTowerDefinition, anchor, out _), Is.False);
            Assert.That(wallet.Wood, Is.EqualTo(9));
            Assert.That(wallet.Stone, Is.EqualTo(20));

            wallet.Configure(100, 100);
            Vector2Int occupiedCell = anchor + Vector2Int.one;
            Assert.That(system.TryPlaceAtCell(system.WallDefinition, occupiedCell, out BuildingInstance wall), Is.True);
            Assert.That(system.EvaluatePlacement(system.BowTowerDefinition, anchor), Is.EqualTo(BuildPlacementStatus.Occupied),
                "One occupied cell must invalidate the complete 2x2 footprint.");

            RectInt zone = system.WorldGrid.Config.BuildZoneCellRect;
            Vector2Int edge = new(zone.xMax - 1, zone.yMin);
            Assert.That(system.EvaluatePlacement(system.BowTowerDefinition, edge), Is.EqualTo(BuildPlacementStatus.OutsideBuildZone));

            Object.Destroy(wall.gameObject);
            yield return null;
            GameObject dinosaurBlocker = new("Tower Footprint Dinosaur Blocker") { layer = GameLayers.DinosaurIndex };
            dinosaurBlocker.transform.position = system.WorldGrid.CellToWorldCenter(anchor + Vector2Int.right);
            dinosaurBlocker.AddComponent<CircleCollider2D>().radius = 0.4f;
            Physics2D.SyncTransforms();
            Assert.That(system.EvaluatePlacement(system.StoneTowerDefinition, anchor), Is.EqualTo(BuildPlacementStatus.DynamicOverlap));
            Object.Destroy(dinosaurBlocker);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TowersUsePlayableBoundsNearestSelectionAndTargetLock()
        {
            yield return LoadGame();
            BuildingSystem system = Object.FindFirstObjectByType<BuildingSystem>();
            system.PlayerWallet.Configure(100, 100);
            Vector2Int anchor = FindLeftValidCell(system, system.BowTowerDefinition);
            Assert.That(system.TryPlaceAtCell(system.BowTowerDefinition, anchor, out BuildingInstance tower), Is.True);
            TowerCombat combat = tower.GetComponent<TowerCombat>();
            Vector2 origin = tower.transform.position;

            DinosaurAgent farther = CreateTestDinosaur(origin + Vector2.right * 8f);
            DinosaurAgent firstNearest = CreateTestDinosaur(origin + Vector2.right * 5f);
            DinosaurAgent outsidePlayable = CreateTestDinosaur(new Vector2(-25.5f, origin.y));
            Assert.That(combat.IsValidTarget(outsidePlayable), Is.False,
                "A mathematically in-range spawn-line Dinosaur must remain ineligible outside the playable world.");
            Assert.That(combat.AcquireNearestTarget(), Is.True);
            Assert.That(combat.CurrentTarget, Is.SameAs(firstNearest));

            DinosaurAgent laterCloser = CreateTestDinosaur(origin + Vector2.right * 2f);
            yield return null;
            Assert.That(combat.CurrentTarget, Is.SameAs(firstNearest), "A valid locked target was replaced by a closer arrival.");
            Assert.That(Mathf.Abs(combat.TurretPivot.eulerAngles.z), Is.LessThan(1f));

            MoveDinosaur(firstNearest, origin + Vector2.right * 31f);
            yield return null;
            Assert.That(combat.CurrentTarget, Is.SameAs(laterCloser), "Tower did not reacquire after its locked target left range.");

            TowerCombat stoneCombat = system.StoneTowerDefinition.Prefab.GetComponent<TowerCombat>();
            Assert.That(stoneCombat.AttackRange, Is.EqualTo(15f));
            Assert.That(Vector2.Distance(origin, farther.transform.position), Is.LessThan(stoneCombat.AttackRange));
            Assert.That((origin + Vector2.right * 16f - origin).magnitude, Is.GreaterThan(stoneCombat.AttackRange));
            DestroyDinosaurs(farther, firstNearest, outsidePlayable, laterCloser);
            Object.Destroy(tower.gameObject);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ProjectilesAreTargetSpecificHomeAndSurviveDeadTargetsAndTowerDestruction()
        {
            yield return LoadGame();
            BuildingSystem system = Object.FindFirstObjectByType<BuildingSystem>();
            system.PlayerWallet.Configure(100, 100);
            Vector2Int anchor = FindValidCell(system, system.BowTowerDefinition);
            Assert.That(system.TryPlaceAtCell(system.BowTowerDefinition, anchor, out BuildingInstance tower), Is.True);
            TowerCombat combat = tower.GetComponent<TowerCombat>();
            combat.enabled = false;
            Vector2 origin = tower.transform.position;

            DinosaurAgent intended = CreateTestDinosaur(origin + Vector2.right * 3f, 100f);
            DinosaurAgent wrong = CreateTestDinosaur(origin + Vector2.right * 1.5f, 100f);
            GameObject obstruction = new("Projectile Obstruction") { layer = GameLayers.BuildingIndex };
            obstruction.transform.position = origin + Vector2.right;
            obstruction.AddComponent<BoxCollider2D>().size = Vector2.one;
            TowerProjectile projectile = Object.Instantiate(combat.ProjectilePrefab, origin, Quaternion.identity);
            projectile.Initialize(intended, 5f, 20f, tower.gameObject);
            yield return WaitUntilDestroyed(projectile, 1f);
            Assert.That(intended.Health.CurrentHealth, Is.EqualTo(95f));
            Assert.That(wrong.Health.CurrentHealth, Is.EqualTo(100f), "Projectile damaged a different Dinosaur in its path.");
            Assert.That(obstruction, Is.Not.Null, "Projectile collided with an unrelated obstruction.");

            DinosaurAgent doomed = CreateTestDinosaur(origin + Vector2.right * 1.5f, 20f);
            TowerProjectile deadTargetShot = Object.Instantiate(combat.ProjectilePrefab, origin, Quaternion.identity);
            deadTargetShot.Configure(0.1f, 5f);
            deadTargetShot.Initialize(doomed, 5f, 1f, tower.gameObject);
            Vector2 remembered = deadTargetShot.LastKnownTargetPosition;
            doomed.Health.TryTakeDamage(new DamageContext(100f, wrong.gameObject));
            yield return null;
            Assert.That(deadTargetShot, Is.Not.Null, "Projectile vanished immediately when its target died.");
            Assert.That(deadTargetShot.LastKnownTargetPosition, Is.EqualTo(remembered));
            Assert.That(deadTargetShot.Target == null || deadTargetShot.Target.Health.IsDead, Is.True);
            yield return WaitUntilDestroyed(deadTargetShot, 3f);

            DinosaurAgent survivor = CreateTestDinosaur(origin + Vector2.right, 100f);
            TowerProjectile survivingShot = Object.Instantiate(combat.ProjectilePrefab, origin, Quaternion.identity);
            survivingShot.Initialize(survivor, 7f, 5f, tower.gameObject);
            Vector2Int[] cells = tower.OccupiedCells;
            Object.Destroy(tower.gameObject);
            yield return null;
            AssertCellsFree(system, cells);
            yield return WaitUntilDestroyed(survivingShot, 1f);
            Assert.That(survivor.Health.CurrentHealth, Is.EqualTo(93f), "An already-fired shot did not finish after Tower destruction.");

            Object.Destroy(obstruction);
            DestroyDinosaurs(intended, wrong, doomed, survivor);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TowerRegenerationHealthBarNavigationAndDamageDestructionUseBuildingSystems()
        {
            yield return LoadGame();
            BuildingSystem system = Object.FindFirstObjectByType<BuildingSystem>();
            DinosaurNavigationGrid navigation = Object.FindFirstObjectByType<DinosaurNavigationGrid>();
            Health campfire = GameObject.Find("Campfire").GetComponent<Health>();
            system.PlayerWallet.Configure(100, 100);
            Vector2Int anchor = FindValidCell(system, system.StoneTowerDefinition);
            Assert.That(system.TryPlaceAtCell(system.StoneTowerDefinition, anchor, out BuildingInstance tower), Is.True);
            BuildingRegeneration regeneration = tower.GetComponent<BuildingRegeneration>();
            HealthBar healthBar = tower.GetComponentInChildren<HealthBar>(true);
            Assert.That(regeneration.RegenerationDelay, Is.EqualTo(10f));
            Assert.That(regeneration.RegenerationPerSecond, Is.EqualTo(10f));
            Assert.That(healthBar.IsVisible, Is.False);
            tower.Health.TryTakeDamage(new DamageContext(20f, new GameObject("Tower Damage Source")));
            Assert.That(healthBar.IsVisible, Is.True);
            regeneration.Configure(tower.Health, 0.1f, 20f);
            float damaged = tower.Health.CurrentHealth;
            yield return new WaitForSeconds(0.05f);
            Assert.That(tower.Health.CurrentHealth, Is.EqualTo(damaged).Within(0.01f));
            yield return new WaitForSeconds(0.12f);
            Assert.That(tower.Health.CurrentHealth, Is.GreaterThan(damaged));
            tower.Health.TryTakeDamage(new DamageContext(5f, null));
            float resetDamage = tower.Health.CurrentHealth;
            yield return new WaitForSeconds(0.05f);
            Assert.That(tower.Health.CurrentHealth, Is.EqualTo(resetDamage).Within(0.01f), "Damage did not reset the regeneration delay.");

            Assert.That(navigation.TryFindPathToTarget(new Vector2(-20f, 0f), campfire, 1.5f, false,
                out List<Vector2> route, out BuildingInstance blocker), Is.True);
            Assert.That(route.Count, Is.GreaterThan(0));
            Assert.That(blocker, Is.Null, "A route-around case incorrectly selected the Tower for destruction.");

            Vector2Int[] occupied = tower.OccupiedCells;
            int wood = system.PlayerWallet.Wood;
            int stone = system.PlayerWallet.Stone;
            tower.Health.TryTakeDamage(new DamageContext(1000f, null));
            yield return null;
            AssertCellsFree(system, occupied);
            Assert.That(system.PlayerWallet.Wood, Is.EqualTo(wood));
            Assert.That(system.PlayerWallet.Stone, Is.EqualTo(stone), "Damage destruction incorrectly granted a refund.");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadGame()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            Physics2D.SyncTransforms();
        }

        private static void AssertDefinition(BuildingDefinition definition, BuildingType type, string displayName,
            int wood, int stone, float hp, float range, float damage, float cooldown, float projectileSpeed)
        {
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.BuildingType, Is.EqualTo(type));
            Assert.That(definition.DisplayName, Is.EqualTo(displayName));
            Assert.That(definition.WoodCost, Is.EqualTo(wood));
            Assert.That(definition.StoneCost, Is.EqualTo(stone));
            Assert.That(definition.RemovalRefundWood, Is.EqualTo(Mathf.CeilToInt(wood * 0.5f)));
            Assert.That(definition.RemovalRefundStone, Is.EqualTo(Mathf.CeilToInt(stone * 0.5f)));
            Assert.That(definition.FootprintSize, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(definition.BlocksPlayer, Is.True);
            Assert.That(definition.BlocksDinosaur, Is.True);
            Assert.That(definition.Prefab, Is.Not.Null);
            Assert.That(definition.Prefab.GetComponent<Health>().MaxHealth, Is.EqualTo(hp));
            TowerCombat combat = definition.Prefab.GetComponent<TowerCombat>();
            Assert.That(combat.AttackRange, Is.EqualTo(range));
            Assert.That(combat.AttackDamage, Is.EqualTo(damage));
            Assert.That(combat.AttackCooldown, Is.EqualTo(cooldown));
            Assert.That(combat.ProjectileSpeed, Is.EqualTo(projectileSpeed));
            Assert.That(combat.ProjectilePrefab, Is.Not.Null);
            Assert.That(combat.ProjectilePrefab.GetComponent<Collider2D>(), Is.Null);
        }

        private static void AssertTowerInstance(BuildingSystem system, BuildingInstance tower,
            BuildingDefinition definition, Vector2Int anchor, float hp)
        {
            Vector2Int[] expected = BuildingSystem.GetFootprintCells(anchor, new Vector2Int(2, 2));
            Assert.That(tower.Definition, Is.SameAs(definition));
            Assert.That(tower.OccupiedCells, Is.EquivalentTo(expected));
            Assert.That(tower.Footprint.SizeInCells, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(tower.transform.position, Is.EqualTo((Vector3)FootprintCenter(system, anchor, new Vector2Int(2, 2))));
            Assert.That(tower.gameObject.layer, Is.EqualTo(GameLayers.BuildingIndex));
            Assert.That(tower.GetComponent<BoxCollider2D>().size, Is.EqualTo(new Vector2(2f, 2f)));
            Assert.That(tower.Health.MaxHealth, Is.EqualTo(hp));
            Assert.That(tower.GetComponent<TowerCombat>().TurretPivot, Is.Not.Null);
            Assert.That(tower.GetComponent<TowerCombat>().ProjectileSpawnPoint, Is.Not.Null);
            Assert.That(tower.transform.Find("Visual/Base"), Is.Not.Null);
            Assert.That(tower.transform.Find("Visual/TurretPivot/TurretVisual"), Is.Not.Null);
            foreach (Vector2Int cell in expected)
            {
                Assert.That(system.Occupancy.TryGetOccupant(cell, out Object occupant), Is.True);
                Assert.That(occupant, Is.SameAs(tower));
            }
        }

        private static Vector2Int FindValidCell(BuildingSystem system, BuildingDefinition definition)
        {
            foreach (Vector2Int cell in system.WorldGrid.Config.BuildZoneCellRect.allPositionsWithin)
            {
                if (system.EvaluatePlacement(definition, cell) == BuildPlacementStatus.Valid) return cell;
            }

            throw new AssertionException("No valid Tower placement cell was found.");
        }

        private static Vector2Int FindLeftValidCell(BuildingSystem system, BuildingDefinition definition)
        {
            RectInt zone = system.WorldGrid.Config.BuildZoneCellRect;
            foreach (Vector2Int cell in zone.allPositionsWithin)
            {
                if (cell.x <= zone.xMin + 2 && system.EvaluatePlacement(definition, cell) == BuildPlacementStatus.Valid) return cell;
            }

            throw new AssertionException("No left-side Tower placement cell was found.");
        }

        private static Vector2 FootprintCenter(BuildingSystem system, Vector2Int anchor, Vector2Int size)
        {
            float tile = system.WorldGrid.Config.TileSize;
            return system.WorldGrid.CellToWorldCenter(anchor) + new Vector2((size.x - 1) * tile * 0.5f, (size.y - 1) * tile * 0.5f);
        }

        private static DinosaurAgent CreateTestDinosaur(Vector2 position, float healthValue = 100f)
        {
            GameObject owner = new("Tower Test Dinosaur") { layer = GameLayers.DinosaurIndex };
            owner.transform.position = position;
            Rigidbody2D body = owner.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            owner.AddComponent<CircleCollider2D>().radius = 0.4f;
            Health health = owner.AddComponent<Health>();
            health.Configure(healthValue);
            DinosaurMovement movement = owner.AddComponent<DinosaurMovement>();
            movement.Configure(body, null);
            DinosaurAttack attack = owner.AddComponent<DinosaurAttack>();
            attack.Configure(body, null, null, null);
            DinosaurTargeting targeting = owner.AddComponent<DinosaurTargeting>();
            targeting.Configure(health, movement, attack);
            DinosaurAgent agent = owner.AddComponent<DinosaurAgent>();
            agent.Configure(health, movement, attack, targeting);
            return agent;
        }

        private static void MoveDinosaur(DinosaurAgent dinosaur, Vector2 position)
        {
            dinosaur.transform.position = position;
            dinosaur.GetComponent<Rigidbody2D>().position = position;
            Physics2D.SyncTransforms();
        }

        private static IEnumerator WaitUntilDestroyed(Object value, float timeout)
        {
            float expires = Time.time + timeout;
            while (value != null && Time.time < expires) yield return null;
            Assert.That(value == null, Is.True, "Object did not finish before its safety timeout.");
        }

        private static void AssertCellsFree(BuildingSystem system, IEnumerable<Vector2Int> cells)
        {
            foreach (Vector2Int cell in cells) Assert.That(system.Occupancy.IsCellOccupied(cell), Is.False, $"Cell {cell} remained occupied.");
        }

        private static void DestroyDinosaurs(params DinosaurAgent[] dinosaurs)
        {
            foreach (DinosaurAgent dinosaur in dinosaurs) if (dinosaur != null) Object.Destroy(dinosaur.gameObject);
        }
    }
}
