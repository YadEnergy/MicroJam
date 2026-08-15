using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MicroJam.Game.Tests
{
    public sealed class PlayerGameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerMovementMouseFacingWalletAndDeathRulesWorkTogether()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;

            GameObject player = GameObject.Find("Game/Actors/Player");
            Assert.That(player, Is.Not.Null);
            PlayerInputController input = player.GetComponent<PlayerInputController>();
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            PlayerFacing facing = player.GetComponent<PlayerFacing>();
            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            PlayerResourceWallet wallet = player.GetComponent<PlayerResourceWallet>();
            wallet.Configure(20, 20);
            Health health = player.GetComponent<Health>();
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Assert.That(input, Is.Not.Null);
            Assert.That(movement, Is.Not.Null);
            Assert.That(facing, Is.Not.Null);
            Assert.That(combat, Is.Not.Null);
            Assert.That(wallet, Is.Not.Null);
            Assert.That(health, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(input.HasValidActions, Is.True);
            input.enabled = false;

            Assert.That(movement.MoveSpeed, Is.EqualTo(5f));
            movement.SetMoveInput(Vector2.one);
            Assert.That(movement.MoveInput.magnitude, Is.EqualTo(1f).Within(0.001f), "Diagonal movement must be normalized.");
            Assert.That(movement.DesiredVelocity.magnitude, Is.EqualTo(5f).Within(0.001f));
            Vector2 startingPosition = body.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Vector2.Distance(body.position, startingPosition), Is.GreaterThan(0.02f), "Rigidbody2D did not move from normalized input.");
            movement.SetMoveInput(Vector2.zero);

            SquareGameplayViewport viewport = facing.GameplayViewport;
            Assert.That(viewport, Is.Not.Null);
            Vector2 screenAbovePlayer = Camera.main.WorldToScreenPoint(player.transform.position + Vector3.up * 3f);
            Assert.That(facing.TrySetFacingFromScreen(screenAbovePlayer), Is.True);
            Assert.That(Vector2.Dot(facing.FacingDirection, Vector2.up), Is.GreaterThan(0.99f));
            Assert.That(combat.Facing, Is.SameAs(facing), "Combat must consume the same facing direction as the indicator.");
            Vector2 lastValidFacing = facing.FacingDirection;
            Rect pixelRect = viewport.PixelGameplayViewport;
            Vector2 outsideViewport = new(pixelRect.xMin - 10f, pixelRect.center.y);
            Assert.That(facing.TrySetFacingFromScreen(outsideViewport), Is.False);
            Assert.That(facing.FacingDirection, Is.EqualTo(lastValidFacing), "Leaving the square gameplay viewport must preserve the last valid facing.");

            Assert.That(wallet.Wood, Is.EqualTo(20));
            Assert.That(wallet.Stone, Is.EqualTo(20));
            int notifications = 0;
            ResourceWalletChangedEvent lastChange = default;
            wallet.ResourceChanged += change =>
            {
                notifications++;
                lastChange = change;
            };
            Assert.That(wallet.AddWood(7), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(27));
            Assert.That(lastChange.ResourceType, Is.EqualTo(PlayerResourceType.Wood));
            Assert.That(lastChange.Delta, Is.EqualTo(7));
            Assert.That(wallet.SpendWood(8), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(19));
            Assert.That(wallet.SpendWood(20), Is.False, "Wallet must reject overspending.");
            Assert.That(wallet.AddStone(4), Is.True);
            Assert.That(wallet.SpendStone(24), Is.True);
            Assert.That(wallet.Stone, Is.Zero);
            Assert.That(wallet.SpendStone(1), Is.False);
            Assert.That(wallet.AddWood(0), Is.False);
            Assert.That(wallet.SpendWood(-1), Is.False);
            Assert.That(notifications, Is.EqualTo(4), "Only successful mutations should notify listeners.");

            Assert.That(health.TryTakeDamage(new DamageContext(10f)), Is.True);
            Assert.That(wallet.Wood, Is.EqualTo(19), "Health changes must not reset wallet resources.");
            Assert.That(wallet.Stone, Is.Zero);
            wallet.ResetForNewRun();
            Assert.That(wallet.Wood, Is.EqualTo(20));
            Assert.That(wallet.Stone, Is.EqualTo(20));

            health.ResetHealth();
            Assert.That(health.TryTakeDamage(new DamageContext(health.MaxHealth)), Is.True);
            movement.SetMoveInput(Vector2.right);
            yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero), "Dead Player must not move.");
            Assert.That(combat.TryAttackNow(out int deadHits), Is.False, "Dead Player must not attack.");
            Assert.That(deadHits, Is.Zero);

            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.PlayerIndex, GameLayers.DinosaurIndex), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.PlayerIndex, GameLayers.BuildingIndex), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.PlayerIndex, GameLayers.WorldBoundaryIndex), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.PlayerIndex, GameLayers.DoorIndex), Is.True);
            Assert.That(Physics2D.GetIgnoreLayerCollision(GameLayers.PlayerIndex, GameLayers.ResourceIndex), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MeleeHitsAllFrontTargetsOnceAndRepeatsWhileHeld()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;

            // Isolate the melee geometry from the scene's intentionally randomized resource population.
            ResourcePopulationManager population = Object.FindFirstObjectByType<ResourcePopulationManager>();
            if (population != null) population.enabled = false;
            foreach (ResourceNode node in Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
            {
                Object.Destroy(node.gameObject);
            }
            yield return null;

            GameObject player = GameObject.Find("Game/Actors/Player");
            PlayerInputController input = player.GetComponent<PlayerInputController>();
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            PlayerFacing facing = player.GetComponent<PlayerFacing>();
            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            Health playerHealth = player.GetComponent<Health>();
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            HealthBarSettings settings = player.GetComponentInChildren<HealthBar>(true).Settings;
            input.enabled = false;
            playerHealth.ResetHealth();
            body.linearVelocity = Vector2.zero;
            facing.SetFacingDirection(Vector2.right);
            Vector2 playerPosition = player.transform.position;

            GameObject first = CreateDamageable("Front Resource With Two Colliders", playerPosition + new Vector2(1f, 0f), GameLayers.ResourceIndex, settings, true, out Health firstHealth, out HealthBar firstBar);
            GameObject second = CreateDamageable("Front Dinosaur", playerPosition + new Vector2(1.1f, 0.3f), GameLayers.DinosaurIndex, settings, false, out Health secondHealth, out HealthBar secondBar);
            GameObject behind = CreateDamageable("Behind Resource", playerPosition + new Vector2(-0.8f, 0f), GameLayers.ResourceIndex, settings, false, out Health behindHealth, out _);
            GameObject outsideRange = CreateDamageable("Distant Resource", playerPosition + new Vector2(2.2f, 0f), GameLayers.ResourceIndex, settings, false, out Health outsideRangeHealth, out _);
            GameObject outsideArc = CreateDamageable("Side Resource", playerPosition + new Vector2(0.4f, 1.1f), GameLayers.ResourceIndex, settings, false, out Health outsideArcHealth, out _);
            GameObject excludedBuilding = CreateDamageable("Excluded Wall", playerPosition + new Vector2(0.8f, -0.1f), GameLayers.BuildingIndex, settings, false, out Health excludedHealth, out _);
            Physics2D.SyncTransforms();

            int firstDamageEvents = 0;
            GameObject receivedSource = null;
            firstHealth.DamageReceived += damage =>
            {
                firstDamageEvents++;
                receivedSource = damage.Source;
            };

            Assert.That(combat.AttackDamage, Is.EqualTo(5f));
            Assert.That(combat.AttackRange, Is.EqualTo(1.5f));
            Assert.That(combat.AttackArcDegrees, Is.EqualTo(90f));
            Assert.That(combat.AttackCooldown, Is.EqualTo(0.4f));
            Assert.That(combat.TryAttackNow(out int hitCount), Is.True);
            Assert.That(hitCount, Is.EqualTo(2), "Attack should hit both eligible targets in front.");
            Assert.That(firstHealth.CurrentHealth, Is.EqualTo(95f), "Multiple colliders on one object must not multiply damage.");
            Assert.That(secondHealth.CurrentHealth, Is.EqualTo(95f));
            Assert.That(firstDamageEvents, Is.EqualTo(1));
            Assert.That(receivedSource, Is.SameAs(player), "Damage source must be the Player GameObject.");
            Assert.That(behindHealth.CurrentHealth, Is.EqualTo(100f));
            Assert.That(outsideRangeHealth.CurrentHealth, Is.EqualTo(100f));
            Assert.That(outsideArcHealth.CurrentHealth, Is.EqualTo(100f));
            Assert.That(excludedHealth.CurrentHealth, Is.EqualTo(100f), "Building/Wall layer must be excluded from Player melee.");
            Assert.That(firstBar.IsVisible, Is.True, "Existing show-after-damage health bar must respond to melee damage.");
            Assert.That(secondBar.IsVisible, Is.True);
            Assert.That(combat.AttackFeedback.enabled, Is.True, "Prefab-bound attack feedback must flash on attack.");
            yield return new WaitForSeconds(0.1f);
            Assert.That(combat.AttackFeedback.enabled, Is.False);

            yield return new WaitForSeconds(0.31f);
            int attackEvents = 0;
            combat.AttackPerformed += () => attackEvents++;
            Vector2 movementStart = body.position;
            movement.SetMoveInput(Vector2.up);
            combat.SetAttackHeld(true);
            Assert.That(attackEvents, Is.EqualTo(1), "Pressing/holding attack after cooldown should attack immediately.");
            yield return new WaitForSeconds(0.45f);
            Assert.That(attackEvents, Is.GreaterThanOrEqualTo(2), "Holding E must repeat at the configured cooldown.");
            Assert.That(Vector2.Distance(body.position, movementStart), Is.GreaterThan(0.1f), "Player must remain able to move while attacking.");
            combat.SetAttackHeld(false);
            movement.SetMoveInput(Vector2.zero);
            int attacksAfterRelease = attackEvents;
            yield return new WaitForSeconds(0.45f);
            Assert.That(attackEvents, Is.EqualTo(attacksAfterRelease), "Releasing E must stop repeated attacks.");

            Object.Destroy(first);
            Object.Destroy(second);
            Object.Destroy(behind);
            Object.Destroy(outsideRange);
            Object.Destroy(outsideArc);
            Object.Destroy(excludedBuilding);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private static GameObject CreateDamageable(
            string name,
            Vector2 position,
            int layer,
            HealthBarSettings settings,
            bool addSecondCollider,
            out Health health,
            out HealthBar bar)
        {
            GameObject root = new(name) { layer = layer };
            root.transform.position = position;
            health = root.AddComponent<Health>();
            health.Configure(100f);
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.18f;
            collider.isTrigger = true;

            if (addSecondCollider)
            {
                GameObject secondCollider = new("Second Collider") { layer = layer };
                secondCollider.transform.SetParent(root.transform, false);
                secondCollider.transform.localPosition = new Vector3(0.05f, 0.05f, 0f);
                CircleCollider2D duplicate = secondCollider.AddComponent<CircleCollider2D>();
                duplicate.radius = 0.15f;
                duplicate.isTrigger = true;
            }

            GameObject anchor = new("HealthBarAnchor") { layer = LayerMask.NameToLayer("Ignore Raycast") };
            anchor.transform.SetParent(root.transform, false);
            GameObject barObject = new("HealthBar") { layer = anchor.layer };
            barObject.transform.SetParent(anchor.transform, false);
            GameObject backgroundObject = new("Background") { layer = anchor.layer };
            backgroundObject.transform.SetParent(barObject.transform, false);
            GameObject fillObject = new("Fill") { layer = anchor.layer };
            fillObject.transform.SetParent(barObject.transform, false);
            SpriteRenderer background = backgroundObject.AddComponent<SpriteRenderer>();
            SpriteRenderer fill = fillObject.AddComponent<SpriteRenderer>();
            bar = barObject.AddComponent<HealthBar>();
            bar.Configure(health, settings, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly, background, fill, new Vector2(1f, 0.12f));
            return root;
        }
    }
}
