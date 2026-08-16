using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MicroJam.Game.Tests
{
    public sealed class DinosaurNavigationPlayModeTests
    {
        [UnityTest]
        public IEnumerator RestoredSceneAndSpawnerUseSharedGridAndOutsidePerimeter()
        {
            yield return LoadGame();

            GameObject game = GameObject.Find("Game");
            DinosaurNavigationGrid navigation = Object.FindFirstObjectByType<DinosaurNavigationGrid>();
            DinosaurSpawner spawner = Object.FindFirstObjectByType<DinosaurSpawner>();
            SpawnPerimeterProvider perimeter = Object.FindFirstObjectByType<SpawnPerimeterProvider>();

            Assert.That(game.transform.Find("Systems/BuildingSystem"), Is.Not.Null);
            Assert.That(game.transform.Find("Runtime/Buildings"), Is.Not.Null);
            Assert.That(game.transform.Find("UI/WorldInteraction/BuildingPopup"), Is.Not.Null);
            Assert.That(game.transform.Find("UI/WorldInteraction/CampfirePopup"), Is.Not.Null);
            Assert.That(navigation, Is.Not.Null);
            Assert.That(navigation.WorldGrid, Is.SameAs(Object.FindFirstObjectByType<WorldGridService>()));
            Assert.That(navigation.Occupancy, Is.SameAs(Object.FindFirstObjectByType<GridOccupancyService>()));
            Assert.That(spawner.HasConfiguredWaves, Is.True);
            Assert.That(spawner.MaximumAlive, Is.EqualTo(10));

            foreach (SpawnSide side in System.Enum.GetValues(typeof(SpawnSide)))
            {
                Vector2 spawn = perimeter.GetPosition(side, 0.5f);
                Assert.That(navigation.WorldGrid.Config.PlayableWorldBounds.Contains(spawn), Is.False);
                Assert.That(navigation.WorldGrid.Config.IsCellInsidePlayableArea(navigation.WorldGrid.WorldToCell(spawn)), Is.False);
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FreeCampfireRouteIgnoresResourceOccupancyAndDoesNotSelectWall()
        {
            yield return LoadGame();
            DinosaurNavigationGrid navigation = Object.FindFirstObjectByType<DinosaurNavigationGrid>();
            Health campfire = GameObject.Find("Campfire").GetComponent<Health>();
            GridOccupancyService occupancy = navigation.Occupancy;
            Vector2Int resourceCell = navigation.WorldGrid.WorldToCell(new Vector2(-8f, 0f));
            GameObject logicalResource = new("Logical Resource Test Occupant");
            Assert.That(occupancy.TryRegister(logicalResource, resourceCell), Is.True);

            Assert.That(navigation.TryFindPathToTarget(new Vector2(-20f, 0f), campfire, 1.5f, false,
                out List<Vector2> path, out BuildingInstance blocker), Is.True);
            Assert.That(path.Count, Is.GreaterThan(0));
            Assert.That(blocker, Is.Null);

            occupancy.Unregister(logicalResource);
            Object.Destroy(logicalResource);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EnclosedCampfireUsesGeneralizedObstacleAndRemovalInvalidatesPaths()
        {
            yield return LoadGame();
            DinosaurNavigationGrid navigation = Object.FindFirstObjectByType<DinosaurNavigationGrid>();
            BuildingSystem buildingSystem = Object.FindFirstObjectByType<BuildingSystem>();
            Health campfire = GameObject.Find("Campfire").GetComponent<Health>();
            List<BuildingInstance> ring = CreateRing(buildingSystem, navigation.WorldGrid.WorldToCell(campfire.transform.position), 2);
            int revisionBeforeRemoval = navigation.Revision;

            Assert.That(navigation.TryFindPathToTarget(new Vector2(-20f, 0f), campfire, 1.5f, false,
                out _, out _), Is.False, "A complete wall ring unexpectedly left a free Campfire route.");
            Assert.That(navigation.TryFindPathToTarget(new Vector2(-20f, 0f), campfire, 1.5f, true,
                out _, out BuildingInstance blocker), Is.True);
            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker.BlocksDinosaur, Is.True);
            Vector2Int releasedCell = blocker.OccupiedCells[0];
            Assert.That(blocker.TryDestroyWithoutRefund(), Is.True);
            yield return null;
            Assert.That(navigation.Occupancy.IsCellOccupied(releasedCell), Is.False);
            Assert.That(navigation.Revision, Is.GreaterThan(revisionBeforeRemoval));
            Assert.That(navigation.TryFindPathToTarget(new Vector2(-20f, 0f), campfire, 1.5f, false,
                out _, out _), Is.True, "Opening the ring did not restore a free route.");

            foreach (BuildingInstance wall in ring) if (wall != null) Object.Destroy(wall.gameObject);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PlayerAggroRequiresFreePathCancelsWhenBlockedAndTimesOutAfterTenSeconds()
        {
            yield return LoadGame();
            DinosaurNavigationGrid navigation = Object.FindFirstObjectByType<DinosaurNavigationGrid>();
            BuildingSystem buildingSystem = Object.FindFirstObjectByType<BuildingSystem>();
            GameObject playerObject = GameObject.Find("Game/Actors/Player");
            Health player = playerObject.GetComponent<Health>();
            Rigidbody2D playerBody = playerObject.GetComponent<Rigidbody2D>();
            Vector2Int clearCenter = FindClearCenter(buildingSystem, 2);
            MoveBody(playerObject, playerBody, navigation.WorldGrid.CellToWorldCenter(clearCenter));
            DinosaurAgent dinosaur = CreateTestDinosaur(new Vector2(-8f, 0f), 0.1f);

            Assert.That(dinosaur.Targeting.TryAggroPlayer(player), Is.True);
            Assert.That(dinosaur.Targeting.State, Is.EqualTo(DinosaurTargetState.ChasingPlayer));

            Vector2Int playerCell = clearCenter;
            List<BuildingInstance> ring = CreateRing(buildingSystem, playerCell, 2);
            dinosaur.Targeting.Tick();
            Assert.That(dinosaur.Targeting.State, Is.Not.EqualTo(DinosaurTargetState.ChasingPlayer),
                "Player aggro used building-breaking fallback after the free route closed.");
            Assert.That(dinosaur.Targeting.TryAggroPlayer(player), Is.False);

            foreach (BuildingInstance wall in ring) if (wall != null) Object.Destroy(wall.gameObject);
            yield return null;
            MoveBody(playerObject, playerBody, new Vector2(24f, 20f));
            dinosaur.transform.position = new Vector2(-24f, -20f);
            dinosaur.GetComponent<Rigidbody2D>().position = dinosaur.transform.position;
            Physics2D.SyncTransforms();
            Assert.That(dinosaur.Targeting.TryAggroPlayer(player), Is.True);

            yield return new WaitForSeconds(0.12f);
            Assert.That(dinosaur.Targeting.State, Is.Not.EqualTo(DinosaurTargetState.ChasingPlayer));
            Assert.That(dinosaur.Targeting.RetaliatingPlayer, Is.Null);

            Object.Destroy(dinosaur.gameObject);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator OpenWorldDinosaurStopsAndDamagesCampfire()
        {
            yield return LoadGame();
            Health campfire = GameObject.Find("Campfire").GetComponent<Health>();
            DinosaurAgent dinosaur = CreateTestDinosaur((Vector2)campfire.transform.position + Vector2.left * 2.3f, 10f);
            float healthBefore = campfire.CurrentHealth;

            dinosaur.Targeting.Tick();

            Assert.That(dinosaur.Targeting.State, Is.EqualTo(DinosaurTargetState.Campfire));
            Assert.That(dinosaur.Targeting.CurrentTarget, Is.SameAs(campfire));
            Assert.That(campfire.CurrentHealth, Is.LessThan(healthBefore));
            Assert.That(dinosaur.Movement.HasPath, Is.False, "Dinosaur kept moving while in Campfire attack range.");
            Object.Destroy(dinosaur.gameObject);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CampfireEndpointsAndPhysicalApproachesUseTheSameSurfaceReach()
        {
            yield return LoadGame();
            DinosaurNavigationGrid navigation = Object.FindFirstObjectByType<DinosaurNavigationGrid>();
            Health campfire = GameObject.Find("Campfire").GetComponent<Health>();
            Vector2 center = campfire.transform.position;
            Vector2[] directions =
            {
                Vector2.left, Vector2.right, Vector2.up, Vector2.down,
                new Vector2(-1f, -1f).normalized, new Vector2(1f, 1f).normalized
            };
            DinosaurAgent[] dinosaurs = new DinosaurAgent[directions.Length];
            bool[] attacked = new bool[directions.Length];

            for (int i = 0; i < directions.Length; i++)
            {
                dinosaurs[i] = CreateTestDinosaur(center + directions[i] * 6f, 10f);
                int captured = i;
                dinosaurs[i].Attack.SuccessfulAttack += target =>
                {
                    if (target == campfire) attacked[captured] = true;
                };

                Assert.That(navigation.TryFindPathToTarget(
                    dinosaurs[i].transform.position,
                    campfire,
                    dinosaurs[i].Attack,
                    dinosaurs[i].Movement.WaypointTolerance,
                    false,
                    out List<Vector2> path,
                    out _), Is.True);
                Assert.That(path, Is.Not.Empty);
                Vector2 endpoint = path[path.Count - 1];
                Assert.That(dinosaurs[i].Attack.CanAttackFrom(
                    endpoint, campfire, dinosaurs[i].Movement.WaypointTolerance), Is.True,
                    $"Path endpoint {endpoint} from direction {directions[i]} did not reserve enough physical attack reach.");
            }

            float deadline = Time.time + 5f;
            while (Time.time < deadline && System.Array.Exists(attacked, value => !value))
            {
                yield return new WaitForFixedUpdate();
            }

            for (int i = 0; i < dinosaurs.Length; i++)
            {
                Assert.That(attacked[i], Is.True, $"Approach {directions[i]} never attacked without external pushing.");
                Assert.That(dinosaurs[i].Attack.IsWithinRange(campfire), Is.True);
                Assert.That(dinosaurs[i].Movement.HasPath, Is.False);
            }

            Vector2[] settledPositions = new Vector2[dinosaurs.Length];
            for (int i = 0; i < dinosaurs.Length; i++) settledPositions[i] = dinosaurs[i].Movement.Position;
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < dinosaurs.Length; i++)
            {
                Assert.That(Vector2.Distance(settledPositions[i], dinosaurs[i].Movement.Position), Is.LessThan(0.03f),
                    "A Dinosaur jittered while holding Campfire attack range.");
                Object.Destroy(dinosaurs[i].gameObject);
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BlockingWallIsApproachedAndAttackedWithoutPush()
        {
            yield return LoadGame();
            DinosaurNavigationGrid navigation = Object.FindFirstObjectByType<DinosaurNavigationGrid>();
            BuildingSystem system = Object.FindFirstObjectByType<BuildingSystem>();
            Health campfire = GameObject.Find("Campfire").GetComponent<Health>();
            Vector2Int center = navigation.WorldGrid.WorldToCell(campfire.transform.position);
            List<BuildingInstance> ring = CreateRing(system, center, 2);
            DinosaurAgent dinosaur = CreateTestDinosaur(
                navigation.WorldGrid.CellToWorldCenter(center + Vector2Int.left * 7), 10f);

            float deadline = Time.time + 5f;
            while (Time.time < deadline && ring.TrueForAll(building =>
                       building == null || Mathf.Approximately(building.Health.CurrentHealth, building.Health.MaxHealth)))
            {
                yield return new WaitForFixedUpdate();
            }

            BuildingInstance damagedWall = ring.Find(building => building != null &&
                building.Health.CurrentHealth < building.Health.MaxHealth);
            Assert.That(damagedWall, Is.Not.Null, "Dinosaur reached the Wall route but never entered physical attack reach.");
            Assert.That(dinosaur.Attack.IsWithinRange(damagedWall.Health), Is.True);
            Assert.That(dinosaur.Movement.HasPath, Is.False);

            foreach (BuildingInstance wall in ring) if (wall != null) Object.Destroy(wall.gameObject);
            Object.Destroy(dinosaur.gameObject);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DoorIsAGeneralizedBreakTargetAndDinosaurIsDamageInstigator()
        {
            yield return LoadGame();
            DinosaurNavigationGrid navigation = Object.FindFirstObjectByType<DinosaurNavigationGrid>();
            BuildingSystem system = Object.FindFirstObjectByType<BuildingSystem>();
            Health campfire = GameObject.Find("Campfire").GetComponent<Health>();
            Vector2Int center = navigation.WorldGrid.WorldToCell(campfire.transform.position);
            List<BuildingInstance> ring = CreateRing(system, center, 2);
            Vector2Int doorCell = center + Vector2Int.left * 2;
            BuildingInstance replacedWall = ring.Find(building => building != null && building.OccupiedCells[0] == doorCell);
            Assert.That(replacedWall, Is.Not.Null);
            replacedWall.TryDestroyWithoutRefund();
            yield return null;
            BuildingInstance door = CreateBuilding(system, system.DoorDefinition, doorCell);
            DinosaurAgent dinosaur = CreateTestDinosaur(navigation.WorldGrid.CellToWorldCenter(doorCell) + Vector2.left * 4f, 10f);
            GameObject receivedSource = null;
            door.Health.DamageReceived += damage => receivedSource = damage.Source;

            dinosaur.Targeting.Tick();

            float deadline = Time.time + 4f;
            while (Time.time < deadline && Mathf.Approximately(door.Health.CurrentHealth, door.Health.MaxHealth))
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(dinosaur.Targeting.State, Is.EqualTo(DinosaurTargetState.BreakingBuilding));
            Assert.That(dinosaur.Targeting.CurrentTarget, Is.SameAs(door.Health));
            Assert.That(door.Health.CurrentHealth, Is.LessThan(door.Health.MaxHealth));
            Assert.That(receivedSource, Is.SameAs(dinosaur.gameObject));
            foreach (BuildingInstance building in ring) if (building != null) Object.Destroy(building.gameObject);
            if (door != null) Object.Destroy(door.gameObject);
            Object.Destroy(dinosaur.gameObject);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PlayerChaseEndsInColliderAwareBiteWithoutExcessiveGap()
        {
            yield return LoadGame();
            DinosaurNavigationGrid navigation = Object.FindFirstObjectByType<DinosaurNavigationGrid>();
            BuildingSystem system = Object.FindFirstObjectByType<BuildingSystem>();
            GameObject playerObject = GameObject.Find("Game/Actors/Player");
            Health player = playerObject.GetComponent<Health>();
            Rigidbody2D playerBody = playerObject.GetComponent<Rigidbody2D>();
            PlayerInputController input = playerObject.GetComponent<PlayerInputController>();
            if (input != null) input.enabled = false;
            Vector2Int clearCenter = FindClearCenter(system, 2);
            Vector2 playerPosition = navigation.WorldGrid.CellToWorldCenter(clearCenter);
            MoveBody(playerObject, playerBody, playerPosition);
            DinosaurAgent dinosaur = CreateTestDinosaur(playerPosition + Vector2.left * 5f, 10f);
            float healthBefore = player.CurrentHealth;

            Assert.That(dinosaur.Targeting.TryAggroPlayer(player), Is.True);
            float deadline = Time.time + 4f;
            while (Time.time < deadline && Mathf.Approximately(player.CurrentHealth, healthBefore))
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(player.CurrentHealth, Is.LessThan(healthBefore));
            Assert.That(dinosaur.Attack.IsWithinRange(player), Is.True);
            Assert.That(dinosaur.Attack.GetSurfaceDistance(player),
                Is.LessThanOrEqualTo(dinosaur.Attack.AttackRange + dinosaur.Attack.RangeTolerance + 0.001f));
            Assert.That(Vector2.Distance(dinosaur.Movement.Position, playerBody.position), Is.LessThan(2.35f),
                "Player bite occurred beyond the two 0.4-radius colliders plus authored surface reach.");

            Object.Destroy(dinosaur.gameObject);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ScreenUiUsesSquareViewportAnchorsAndResponsiveScalers()
        {
            yield return LoadGame();
            GameObject game = GameObject.Find("Game");
            Transform hudRoot = game.transform.Find("UI/Canvas");
            Transform interactionRoot = game.transform.Find("UI/WorldInteraction");
            Canvas hudCanvas = hudRoot.GetComponent<Canvas>();
            CanvasScaler hudScaler = hudRoot.GetComponent<CanvasScaler>();
            CanvasScaler interactionScaler = interactionRoot.GetComponent<CanvasScaler>();
            Camera gameplayCamera = Object.FindFirstObjectByType<SquareGameplayViewport>().GetComponent<Camera>();

            Assert.That(hudCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(hudCanvas.worldCamera, Is.SameAs(gameplayCamera));
            AssertResponsiveScaler(hudScaler);
            AssertResponsiveScaler(interactionScaler);
            AssertRect(game.transform.Find("UI/Canvas/DayNightUI/DayNightText").GetComponent<RectTransform>(), new Vector2(0f, 1f));
            RectTransform dayNightBar = game.transform.Find("UI/Canvas/DayNightUI/DayNightBar").GetComponent<RectTransform>();
            RectTransform markerZone = dayNightBar.Find("MarkerZone").GetComponent<RectTransform>();
            RectTransform progressMarker = markerZone.Find("ProgressMarker").GetComponent<RectTransform>();
            Assert.That(dayNightBar.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(dayNightBar.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(markerZone.anchorMin.x, Is.EqualTo(1f / 6f).Within(0.001f));
            Assert.That(markerZone.anchorMax.x, Is.EqualTo(5f / 6f).Within(0.001f));
            Assert.That(progressMarker.anchorMin.x, Is.InRange(0f, 1f));
            Assert.That(progressMarker.anchorMax.x, Is.EqualTo(progressMarker.anchorMin.x).Within(0.001f));
            AssertRect(game.transform.Find("UI/Canvas/WaveInfo/WaveInfoText").GetComponent<RectTransform>(), new Vector2(0.5f, 1f));
            AssertRect(game.transform.Find("UI/Canvas/PointsInfo/PointsInfoUI").GetComponent<RectTransform>(), new Vector2(1f, 1f));
            AssertRect(game.transform.Find("UI/WorldInteraction/BuildingPopup").GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f));
            AssertRect(game.transform.Find("UI/WorldInteraction/CampfirePopup").GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f));

            Rect wide = SquareGameplayViewport.CalculateSquareViewport(1920, 1080);
            Rect hd = SquareGameplayViewport.CalculateSquareViewport(1280, 720);
            Rect small = SquareGameplayViewport.CalculateSquareViewport(640, 360);
            Rect sixteenTen = SquareGameplayViewport.CalculateSquareViewport(1280, 800);
            Rect tall = SquareGameplayViewport.CalculateSquareViewport(800, 1280);
            Assert.That(wide.width * 1920f, Is.EqualTo(1080f).Within(0.01f));
            Assert.That(hd.width * 1280f, Is.EqualTo(720f).Within(0.01f));
            Assert.That(small.width * 640f, Is.EqualTo(360f).Within(0.01f));
            Assert.That(sixteenTen.width * 1280f, Is.EqualTo(800f).Within(0.01f));
            Assert.That(tall.height * 1280f, Is.EqualTo(800f).Within(0.01f));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DeadPlayerImmediatelyClearsAggroAndRestoresCampfireObjective()
        {
            yield return LoadGame();
            GameObject playerObject = GameObject.Find("Game/Actors/Player");
            Health player = playerObject.GetComponent<Health>();
            DinosaurAgent dinosaur = CreateTestDinosaur((Vector2)playerObject.transform.position + Vector2.left * 4f, 10f);
            Assert.That(dinosaur.Targeting.TryAggroPlayer(player), Is.True);

            Assert.That(player.TryTakeDamage(new DamageContext(player.CurrentHealth, dinosaur.gameObject)), Is.True);

            Assert.That(player.IsDead, Is.True);
            Assert.That(dinosaur.Targeting.RetaliatingPlayer, Is.Null);
            Assert.That(dinosaur.Targeting.State, Is.Not.EqualTo(DinosaurTargetState.ChasingPlayer));
            Assert.That(dinosaur.Targeting.CurrentTarget, Is.SameAs(dinosaur.Targeting.CampfireHealth));
            Object.Destroy(dinosaur.gameObject);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WallLineWithOpeningKeepsFreeCampfireRouteAndIsNotTargeted()
        {
            yield return LoadGame();
            DinosaurNavigationGrid navigation = Object.FindFirstObjectByType<DinosaurNavigationGrid>();
            BuildingSystem system = Object.FindFirstObjectByType<BuildingSystem>();
            Health campfire = GameObject.Find("Campfire").GetComponent<Health>();
            Vector2Int center = navigation.WorldGrid.WorldToCell(campfire.transform.position);
            List<BuildingInstance> line = new();
            for (int y = -4; y <= 4; y++)
            {
                if (y == 3) continue;
                line.Add(CreateBuilding(system, system.WallDefinition, center + new Vector2Int(-6, y)));
            }

            Assert.That(navigation.TryFindPathToTarget(new Vector2(-20f, 0f), campfire, 1.5f, false,
                out List<Vector2> path, out BuildingInstance blocker), Is.True);
            Assert.That(path.Count, Is.GreaterThan(0));
            Assert.That(blocker, Is.Null, "A Wall was selected even though the line had a free opening/route.");
            foreach (BuildingInstance wall in line) Object.Destroy(wall.gameObject);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MultipleDinosaursShareObstacleThenBothRepathWhenItIsDestroyed()
        {
            yield return LoadGame();
            DinosaurNavigationGrid navigation = Object.FindFirstObjectByType<DinosaurNavigationGrid>();
            BuildingSystem system = Object.FindFirstObjectByType<BuildingSystem>();
            Health campfire = GameObject.Find("Campfire").GetComponent<Health>();
            Vector2Int center = navigation.WorldGrid.WorldToCell(campfire.transform.position);
            List<BuildingInstance> ring = CreateRing(system, center, 2);
            Vector2 start = navigation.WorldGrid.CellToWorldCenter(center + Vector2Int.left * 4);
            DinosaurAgent first = CreateTestDinosaur(start, 10f);
            DinosaurAgent second = CreateTestDinosaur(start, 10f);

            first.Targeting.Tick();
            second.Targeting.Tick();
            Assert.That(first.Targeting.State, Is.EqualTo(DinosaurTargetState.BreakingBuilding));
            Assert.That(second.Targeting.State, Is.EqualTo(DinosaurTargetState.BreakingBuilding));
            Assert.That(first.Targeting.CurrentTarget, Is.SameAs(second.Targeting.CurrentTarget));
            BuildingInstance shared = first.Targeting.CurrentTarget.GetComponent<BuildingInstance>();
            Assert.That(shared, Is.Not.Null);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.DinosaurIndex, GameLayers.DinosaurIndex), Is.True);

            shared.TryDestroyWithoutRefund();
            yield return null;
            first.Targeting.Tick();
            second.Targeting.Tick();
            Assert.That(first.Targeting.State, Is.EqualTo(DinosaurTargetState.Campfire));
            Assert.That(second.Targeting.State, Is.EqualTo(DinosaurTargetState.Campfire));
            Assert.That(first.Targeting.CurrentTarget, Is.SameAs(campfire));
            Assert.That(second.Targeting.CurrentTarget, Is.SameAs(campfire));

            foreach (BuildingInstance building in ring) if (building != null) Object.Destroy(building.gameObject);
            Object.Destroy(first.gameObject);
            Object.Destroy(second.gameObject);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private static List<BuildingInstance> CreateRing(BuildingSystem system, Vector2Int center, int radius)
        {
            List<BuildingInstance> result = new();
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;
                    Vector2Int cell = center + new Vector2Int(x, y);
                    result.Add(CreateBuilding(system, system.WallDefinition, cell));
                }
            }

            Physics2D.SyncTransforms();
            return result;
        }

        private static BuildingInstance CreateBuilding(BuildingSystem system, BuildingDefinition definition, Vector2Int cell)
        {
            GameObject owner = Object.Instantiate(definition.Prefab,
                system.WorldGrid.CellToWorldCenter(cell), Quaternion.identity, system.RuntimeBuildingParent);
            BuildingInstance building = owner.GetComponent<BuildingInstance>();
            Assert.That(building.InitializePlacement(definition, system.Occupancy, new[] { cell }), Is.True,
                $"Could not register {definition.DisplayName} at {cell}.");
            return building;
        }

        private static DinosaurAgent CreateTestDinosaur(Vector2 position, float chaseTimeout)
        {
            GameObject owner = new("Dinosaur Navigation Test Agent") { layer = GameLayers.DinosaurIndex };
            owner.transform.position = position;
            Rigidbody2D body = owner.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            owner.AddComponent<CircleCollider2D>().radius = 0.4f;
            Health health = owner.AddComponent<Health>();
            health.Configure(75f);
            DinosaurMovement movement = owner.AddComponent<DinosaurMovement>();
            movement.Configure(body, null);
            DinosaurAttack attack = owner.AddComponent<DinosaurAttack>();
            attack.Configure(body, null, null, null);
            DinosaurTargeting targeting = owner.AddComponent<DinosaurTargeting>();
            targeting.Configure(health, movement, attack, chaseTimeout);
            DinosaurAgent agent = owner.AddComponent<DinosaurAgent>();
            agent.Configure(health, movement, attack, targeting);
            agent.Initialize();
            return agent;
        }

        private static Vector2Int FindClearCenter(BuildingSystem system, int radius)
        {
            foreach (Vector2Int center in system.WorldGrid.Config.BuildZoneCellRect.allPositionsWithin)
            {
                bool valid = true;
                for (int y = -radius; y <= radius && valid; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;
                        Vector2Int cell = center + new Vector2Int(x, y);
                        if (!system.WorldGrid.Config.IsCellInsideBuildZone(cell) ||
                            system.WorldGrid.Config.IsCellProtectedFromBuilding(cell) ||
                            system.Occupancy.IsCellOccupied(cell))
                        {
                            valid = false;
                            break;
                        }
                    }
                }

                if (valid) return center;
            }

            throw new AssertionException("Could not find a clear ring center.");
        }

        private static void MoveBody(GameObject owner, Rigidbody2D body, Vector2 position)
        {
            owner.transform.position = position;
            body.position = position;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        private static void AssertResponsiveScaler(CanvasScaler scaler)
        {
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(683f, 683f)));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.001f));
        }

        private static void AssertRect(RectTransform rect, Vector2 expectedAnchor)
        {
            Assert.That(rect.anchorMin, Is.EqualTo(expectedAnchor));
            Assert.That(rect.anchorMax, Is.EqualTo(expectedAnchor));
            if (expectedAnchor == new Vector2(0.5f, 0.5f))
            {
                Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(rect.rect.width, Is.LessThanOrEqualTo(280.01f));
                Assert.That(rect.rect.height, Is.LessThanOrEqualTo(220.01f));
            }
        }

        private static IEnumerator LoadGame()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            Physics2D.SyncTransforms();
        }
    }
}
