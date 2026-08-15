using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MicroJam.Game.Tests
{
    public sealed class PhaseSixInteractionPlayModeTests
    {
        [UnityTest]
        public IEnumerator WorldClicksRetargetOneLivePopupAndBuildModeUiHavePriority()
        {
            yield return LoadGame();

            WorldInteractionController controller = UnityEngine.Object.FindFirstObjectByType<WorldInteractionController>();
            BuildingSystem buildingSystem = UnityEngine.Object.FindFirstObjectByType<BuildingSystem>();
            CampfireInteraction campfire = UnityEngine.Object.FindFirstObjectByType<CampfireInteraction>();
            GameObject player = GameObject.Find("Game/Actors/Player");
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.transform.name, Is.EqualTo("WorldInteraction"));
            Assert.That(EventSystem.current, Is.Not.Null);
            Assert.That(controller.HasValidInputActions, Is.True);
            Assert.That(controller.InputActions.FindAction("WorldInteraction/Interact", true).enabled, Is.True);
            Assert.That(controller.HasOpenPopup, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            Vector2Int wallCell = FindValidCell(buildingSystem, buildingSystem.WallDefinition);
            buildingSystem.SelectBuildMode(BuildSelection.Wall);
            Assert.That(buildingSystem.TryPlaceAtCell(buildingSystem.WallDefinition, wallCell, out BuildingInstance wall), Is.True);
            Assert.That(controller.HasOpenPopup, Is.False);
            Vector2 wallScreen = Camera.main.WorldToScreenPoint(wall.transform.position);
            Assert.That(controller.TryInteractAtScreen(wallScreen), Is.False, "Build Mode must suppress world interaction clicks.");

            buildingSystem.CancelBuildMode();
            Assert.That(controller.TryInteractAtScreen(wallScreen), Is.True);
            Assert.That(controller.OpenPopupType, Is.EqualTo(WorldInteractionPopupType.Building));
            Assert.That(controller.BuildingPopup.Target, Is.SameAs(wall));
            Assert.That(controller.BuildingPopup.HealthText.text, Does.Contain("150 / 150"));

            GameObject source = new("Popup Live Health Test Source");
            Assert.That(wall.Health.TryTakeDamage(new DamageContext(5f, source)), Is.True);
            Assert.That(controller.BuildingPopup.HealthText.text, Does.Contain("145 / 150"), "Building popup did not react to HealthChanged.");

            Vector2 campfireScreen = Camera.main.WorldToScreenPoint(campfire.transform.position);
            Assert.That(controller.TryInteractAtScreen(campfireScreen), Is.True);
            Assert.That(controller.OpenPopupType, Is.EqualTo(WorldInteractionPopupType.Campfire));
            Assert.That(controller.BuildingPopup.IsOpen, Is.False);
            Assert.That(controller.CampfirePopup.Target, Is.SameAs(campfire));

            Assert.That(controller.TryInteractAtScreen(wallScreen, true), Is.False, "A UI-owned click passed through into the world.");
            Assert.That(controller.OpenPopupType, Is.EqualTo(WorldInteractionPopupType.Campfire));

            movement.SetMoveInput(Vector2.up);
            yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f), "Player movement stopped while a popup was open.");
            movement.SetMoveInput(Vector2.zero);
            Assert.That(combat.TryAttackNow(out _), Is.True, "Melee stopped while a popup was open.");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Popup paused gameplay.");

            buildingSystem.SelectBuildMode(BuildSelection.Door);
            Assert.That(controller.HasOpenPopup, Is.False, "Entering Build Mode did not close the interaction popup.");
            buildingSystem.CancelBuildMode();

            Assert.That(controller.TryInteractAtScreen(campfireScreen), Is.True);
            ResourceNode resource = UnityEngine.Object.FindFirstObjectByType<ResourceNode>();
            Assert.That(resource, Is.Not.Null);
            Vector2 resourceScreen = Camera.main.WorldToScreenPoint(resource.transform.position);
            Assert.That(controller.TryInteractAtScreen(resourceScreen), Is.False, "Resource click should do nothing.");
            Assert.That(controller.HasOpenPopup, Is.False, "Empty/non-interactable click should close the prior popup.");

            UnityEngine.Object.Destroy(source);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ConfirmedRemovalRefundsOnceReleasesImmediatelyAndAllowsRebuild()
        {
            yield return LoadGame();

            WorldInteractionController controller = UnityEngine.Object.FindFirstObjectByType<WorldInteractionController>();
            BuildingSystem system = UnityEngine.Object.FindFirstObjectByType<BuildingSystem>();
            PlayerResourceWallet wallet = system.PlayerWallet;
            Vector2Int cell = FindValidCell(system, system.WallDefinition);

            system.SelectBuildMode(BuildSelection.Wall);
            Assert.That(system.TryPlaceAtCell(system.WallDefinition, cell, out BuildingInstance wall), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(15));
            system.CancelBuildMode();
            controller.OpenBuilding(wall);
            float healthBeforeClose = wall.Health.CurrentHealth;
            controller.BuildingPopup.CloseButton.onClick.Invoke();
            Assert.That(controller.BuildingPopup.IsOpen, Is.False);
            Assert.That(wall, Is.Not.Null);
            Assert.That(wallet.Wood, Is.EqualTo(15), "Close/cancel incorrectly refunded Wood.");
            Assert.That(system.Occupancy.IsCellOccupied(cell), Is.True);
            Assert.That(wall.Health.CurrentHealth, Is.EqualTo(healthBeforeClose));

            BuildingRemovalEvent removalEvent = default;
            int removalEvents = 0;
            wall.Removing += value =>
            {
                removalEvent = value;
                removalEvents++;
            };
            controller.OpenBuilding(wall);
            controller.BuildingPopup.RemoveButton.onClick.Invoke();
            Assert.That(removalEvents, Is.EqualTo(1));
            Assert.That(removalEvent.Reason, Is.EqualTo(BuildingRemovalReason.PlayerRemoval));
            Assert.That(removalEvent.RefundedWood, Is.EqualTo(3));
            Assert.That(wallet.Wood, Is.EqualTo(18));
            Assert.That(system.Occupancy.IsCellOccupied(cell), Is.False, "Removal did not release occupancy immediately.");
            Assert.That(controller.BuildingPopup.IsOpen, Is.False);
            Assert.That(controller.BuildingPopup.RemoveSelectedBuilding(), Is.False, "Removal could be confirmed twice.");
            Assert.That(wallet.Wood, Is.EqualTo(18));
            yield return null;
            Assert.That(wall == null, Is.True);

            system.SelectBuildMode(BuildSelection.Door);
            Assert.That(system.TryPlaceAtCell(system.DoorDefinition, cell, out BuildingInstance door), Is.True,
                "Released Wall cell could not be rebuilt with a Door.");
            Assert.That(wallet.Wood, Is.EqualTo(8));
            system.CancelBuildMode();
            controller.OpenBuilding(door);
            Assert.That(controller.BuildingPopup.RemovalPromptText.text, Does.Contain("5 Wood"));
            Assert.That(controller.BuildingPopup.RemoveSelectedBuilding(), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(13));
            Assert.That(system.Occupancy.IsCellOccupied(cell), Is.False);
            yield return null;
            Assert.That(door == null, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RegenerationResetsOnDamageAndLethalDamageCleansUpWithoutRefund()
        {
            yield return LoadGame();

            WorldInteractionController controller = UnityEngine.Object.FindFirstObjectByType<WorldInteractionController>();
            BuildingSystem system = UnityEngine.Object.FindFirstObjectByType<BuildingSystem>();
            PlayerResourceWallet wallet = system.PlayerWallet;
            Vector2Int wallCell = FindValidCell(system, system.WallDefinition);
            system.SelectBuildMode(BuildSelection.Wall);
            Assert.That(system.TryPlaceAtCell(system.WallDefinition, wallCell, out BuildingInstance wall), Is.True);
            Vector2Int doorCell = FindValidCell(system, system.DoorDefinition);
            system.SelectBuildMode(BuildSelection.Door);
            Assert.That(system.TryPlaceAtCell(system.DoorDefinition, doorCell, out BuildingInstance door), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(5));
            system.CancelBuildMode();

            BuildingRegeneration wallRegen = wall.GetComponent<BuildingRegeneration>();
            BuildingRegeneration doorRegen = door.GetComponent<BuildingRegeneration>();
            Assert.That(wallRegen.RegenerationDelay, Is.EqualTo(10f));
            Assert.That(wallRegen.RegenerationPerSecond, Is.EqualTo(10f));
            Assert.That(doorRegen.RegenerationDelay, Is.EqualTo(10f));
            Assert.That(doorRegen.RegenerationPerSecond, Is.EqualTo(10f));
            wallRegen.Configure(wall.Health, 0.15f, 40f);
            doorRegen.Configure(door.Health, 0.15f, 40f);

            GameObject source = new("Regeneration Test Source");
            Assert.That(wall.Health.TryTakeDamage(new DamageContext(20f, source)), Is.True);
            float wallDamaged = wall.Health.CurrentHealth;
            yield return new WaitForSeconds(0.08f);
            Assert.That(wall.Health.CurrentHealth, Is.EqualTo(wallDamaged).Within(0.01f), "Wall regenerated before its delay.");
            yield return new WaitForSeconds(0.16f);
            Assert.That(wall.Health.CurrentHealth, Is.GreaterThan(wallDamaged), "Wall did not begin regenerating after its delay.");
            Assert.That(wallRegen.IsRegenerating, Is.True);

            Assert.That(wall.Health.TryTakeDamage(new DamageContext(5f, source)), Is.True);
            float wallDamagedAgain = wall.Health.CurrentHealth;
            Assert.That(wallRegen.IsRegenerating, Is.False, "Damage did not stop regeneration immediately.");
            yield return new WaitForSeconds(0.08f);
            Assert.That(wall.Health.CurrentHealth, Is.EqualTo(wallDamagedAgain).Within(0.01f), "Wall regenerated before the reset delay elapsed.");
            yield return new WaitForSeconds(0.16f);
            Assert.That(wall.Health.CurrentHealth, Is.GreaterThan(wallDamagedAgain));

            Assert.That(door.Health.TryTakeDamage(new DamageContext(20f, source)), Is.True);
            float doorDamaged = door.Health.CurrentHealth;
            yield return new WaitForSeconds(0.08f);
            Assert.That(door.Health.CurrentHealth, Is.EqualTo(doorDamaged).Within(0.01f));
            yield return new WaitForSeconds(0.16f);
            Assert.That(door.Health.CurrentHealth, Is.GreaterThan(doorDamaged), "Door did not regenerate.");

            controller.OpenBuilding(wall);
            Assert.That(wall.Health.TryTakeDamage(new DamageContext(1000f, source)), Is.True);
            Assert.That(system.Occupancy.IsCellOccupied(wallCell), Is.False);
            Assert.That(controller.HasOpenPopup, Is.False, "Selected dead building did not close its popup.");
            Assert.That(wallet.Wood, Is.EqualTo(5), "Lethal damage incorrectly refunded Wood.");
            Assert.That(wallRegen.IsRegenerating, Is.False);

            Assert.That(door.Health.TryTakeDamage(new DamageContext(1000f, source)), Is.True);
            Assert.That(system.Occupancy.IsCellOccupied(doorCell), Is.False);
            Assert.That(wallet.Wood, Is.EqualTo(5));
            yield return null;
            Assert.That(wall == null, Is.True);
            Assert.That(door == null, Is.True);

            UnityEngine.Object.Destroy(source);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CampfireRepairIsLivePercentageBasedTransactionalAndNeverRegeneratesOrRevives()
        {
            yield return LoadGame();

            WorldInteractionController controller = UnityEngine.Object.FindFirstObjectByType<WorldInteractionController>();
            CampfireInteraction campfire = UnityEngine.Object.FindFirstObjectByType<CampfireInteraction>();
            PlayerResourceWallet wallet = controller.PlayerWallet;
            Health health = campfire.Health;
            GameObject source = new("Campfire Repair Test Source");

            Assert.That(campfire.GetComponent<BuildingRegeneration>(), Is.Null);
            controller.OpenCampfire(campfire);
            Assert.That(controller.CampfirePopup.IsOpen, Is.True);
            Assert.That(controller.CampfirePopup.RepairButton.interactable, Is.False, "Repair must be disabled at full HP.");
            Assert.That(controller.CampfirePopup.RepairCampfire(), Is.False);
            Assert.That(wallet.Wood, Is.EqualTo(20));

            Assert.That(health.TryTakeDamage(new DamageContext(250f, source)), Is.True);
            Assert.That(controller.CampfirePopup.HealthText.text, Does.Contain("250 / 500"));
            Assert.That(controller.CampfirePopup.RepairButton.interactable, Is.True);
            Assert.That(controller.CampfirePopup.RepairCampfire(), Is.True);
            Assert.That(wallet.Wood, Is.Zero);
            Assert.That(health.CurrentHealth, Is.EqualTo(300f), "Repair must restore 10% of Max Health.");
            Assert.That(controller.CampfirePopup.RepairButton.interactable, Is.False, "Repair must disable when Wood becomes insufficient.");

            Assert.That(wallet.AddWood(19), Is.True);
            Assert.That(controller.CampfirePopup.RepairButton.interactable, Is.False);
            Assert.That(wallet.AddWood(1), Is.True);
            Assert.That(controller.CampfirePopup.RepairButton.interactable, Is.True,
                "Wallet ResourceChanged did not enable Repair after reaching 20 Wood.");

            health.ResetHealth();
            Assert.That(health.TryTakeDamage(new DamageContext(25f, source)), Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(475f));
            Assert.That(controller.CampfirePopup.RepairCampfire(), Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(500f), "Partial overheal did not clamp at Max Health.");
            Assert.That(wallet.Wood, Is.Zero, "Partial overheal must still spend the full 20 Wood.");
            Assert.That(controller.CampfirePopup.RepairButton.interactable, Is.False);

            Assert.That(health.TryTakeDamage(new DamageContext(50f, source)), Is.True);
            float nonRegeneratingHealth = health.CurrentHealth;
            Time.timeScale = 20f;
            float noRegenStartTime = Time.time;
            yield return new WaitForSeconds(10.5f);
            float elapsedNoRegenTime = Time.time - noRegenStartTime;
            Time.timeScale = 1f;
            Assert.That(elapsedNoRegenTime, Is.GreaterThanOrEqualTo(10f), "No-regeneration check did not span the default building delay.");
            Assert.That(health.CurrentHealth, Is.EqualTo(nonRegeneratingHealth), "Campfire regenerated automatically.");

            health.ResetHealth();
            wallet.AddWood(20);
            Assert.That(health.TryTakeDamage(new DamageContext(1000f, source)), Is.True);
            Assert.That(health.IsDead, Is.True);
            Assert.That(campfire, Is.Not.Null, "Dead Campfire must remain for future Game Over handling.");
            Assert.That(controller.CampfirePopup.RepairButton.interactable, Is.False);
            Assert.That(controller.CampfirePopup.RepairCampfire(), Is.False, "Normal Repair revived a dead Campfire.");
            Assert.That(health.IsDead, Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(20));

            controller.CampfirePopup.CloseButton.onClick.Invoke();
            Assert.That(controller.CampfirePopup.IsOpen, Is.False);
            UnityEngine.Object.Destroy(source);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadGame()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            UnityEngine.Object.FindFirstObjectByType<PlayerResourceWallet>()?.Configure(20, 20);
            Physics2D.SyncTransforms();
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
    }
}
