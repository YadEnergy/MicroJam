using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MicroJam.Game.Tests
{
    public sealed class HealthPlayModeTests
    {
        [UnityTest]
        public IEnumerator HealthAndPrefabOwnedBarsFollowPhaseTwoRules()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;

            Health[] persistentHealth = Object.FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(persistentHealth.Length, Is.EqualTo(2), "Only scene-bound Player and Campfire should exist before dynamic spawning.");

            GameObject game = GameObject.Find("Game");
            Health scenePlayer = game.transform.Find("Actors/Player").GetComponent<Health>();
            HealthBar scenePlayerBar = scenePlayer.GetComponentInChildren<HealthBar>(true);
            Health sceneCampfire = game.transform.Find("World/Campfire").GetComponent<Health>();
            HealthBar sceneCampfireBar = sceneCampfire.GetComponentInChildren<HealthBar>(true);

            AssertAlwaysVisibleFriendly(scenePlayer, scenePlayerBar, 100f);
            AssertAlwaysVisibleFriendly(sceneCampfire, sceneCampfireBar, 500f);

            GameObject playerInstance = CreateTestEntity(
                "Test Player",
                100f,
                scenePlayerBar.Settings,
                HealthBarVisibilityMode.AlwaysVisible,
                HealthBarColorRole.Friendly,
                out Health playerHealth,
                out HealthBar playerBar);
            GameObject instigator = new("Damage Instigator");

            int healthChangedCount = 0;
            int damageCount = 0;
            int healingCount = 0;
            int deathCount = 0;
            DamageReceivedEvent lastDamage = default;
            DeathEvent lastDeath = default;
            playerHealth.HealthChanged += _ => healthChangedCount++;
            playerHealth.DamageReceived += damage =>
            {
                damageCount++;
                lastDamage = damage;
            };
            playerHealth.HealingReceived += _ => healingCount++;
            playerHealth.Died += death =>
            {
                deathCount++;
                lastDeath = death;
            };

            Assert.That(playerHealth.TryTakeDamage(new DamageContext(-1f, instigator), out float invalidDamage), Is.False);
            Assert.That(invalidDamage, Is.Zero);
            Assert.That(playerHealth.TryTakeDamage(new DamageContext(25f, instigator), out float appliedDamage), Is.True);
            Assert.That(appliedDamage, Is.EqualTo(25f));
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(75f));
            Assert.That(lastDamage.Source, Is.SameAs(instigator));
            Assert.That(lastDamage.AppliedAmount, Is.EqualTo(25f));
            Assert.That(damageCount, Is.EqualTo(1));
            Assert.That(playerBar.transform.Find("Fill").localScale.x / playerBar.BarSize.x, Is.EqualTo(0.75f).Within(0.001f));

            Assert.That(playerHealth.TryHeal(100f, out float appliedHealing), Is.True);
            Assert.That(appliedHealing, Is.EqualTo(25f));
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f));
            Assert.That(healingCount, Is.EqualTo(1));
            Assert.That(playerHealth.TryHeal(1f), Is.False, "Full-health healing must do nothing.");

            Assert.That(playerHealth.TryTakeDamage(new DamageContext(1000f, instigator), out float lethalDamage), Is.True);
            Assert.That(lethalDamage, Is.EqualTo(100f));
            Assert.That(playerHealth.CurrentHealth, Is.Zero);
            Assert.That(playerHealth.IsDead, Is.True);
            Assert.That(deathCount, Is.EqualTo(1));
            Assert.That(lastDeath.Source, Is.SameAs(instigator));
            Assert.That(playerHealth.TryTakeDamage(new DamageContext(1f, instigator)), Is.False);
            Assert.That(deathCount, Is.EqualTo(1), "Death must only fire once.");
            Assert.That(playerHealth.TryHeal(10f), Is.False, "Normal healing must not revive a dead object.");
            Assert.That(playerInstance, Is.Not.Null, "Health must not destroy its owner on death.");

            Assert.That(playerHealth.Revive(20f), Is.True);
            Assert.That(playerHealth.IsDead, Is.False);
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(20f));
            playerHealth.ResetHealth();
            Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f));
            Assert.That(playerHealth.IsDead, Is.False);
            Assert.That(healthChangedCount, Is.GreaterThanOrEqualTo(5));

            Object.Destroy(playerInstance);
            Object.Destroy(instigator);

            yield return VerifyDamageVisibilityTimer(scenePlayerBar.Settings);
            VerifyIndependentShowAfterDamageEntities(scenePlayerBar.Settings);

            yield return null;
            Assert.That(Object.FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(2),
                "Temporary test instances must clean up without duplicating persistent scene objects.");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator VerifyDamageVisibilityTimer(HealthBarSettings settings)
        {
            GameObject dinosaur = CreateTestEntity(
                "Test Dinosaur",
                75f,
                settings,
                HealthBarVisibilityMode.ShowAfterDamage,
                HealthBarColorRole.Enemy,
                out Health health,
                out HealthBar bar);

            Assert.That(bar.ColorRole, Is.EqualTo(HealthBarColorRole.Enemy));
            Assert.That(bar.FillColor.r, Is.GreaterThan(bar.FillColor.g));
            Assert.That(bar.IsVisible, Is.False);
            Assert.That(health.TryTakeDamage(new DamageContext(5f)), Is.True);
            Assert.That(bar.IsVisible, Is.True);
            float firstDeadline = bar.VisibleUntilTime;

            yield return new WaitForSeconds(0.5f);
            Assert.That(health.TryTakeDamage(new DamageContext(5f)), Is.True);
            float resetDeadline = bar.VisibleUntilTime;
            Assert.That(resetDeadline, Is.GreaterThan(firstDeadline + 0.4f), "Repeated damage must reset the timer.");

            yield return new WaitForSeconds(2.7f);
            Assert.That(bar.IsVisible, Is.True, "Bar hid before three seconds elapsed from the latest damage.");
            yield return new WaitForSeconds(0.5f);
            Assert.That(bar.IsVisible, Is.False, "Bar did not hide after its configured damaged-visible duration.");
            Object.Destroy(dinosaur);
        }

        private static void VerifyIndependentShowAfterDamageEntities(HealthBarSettings settings)
        {
            (string Name, float MaxHealth)[] definitions =
            {
                ("Wall", 150f),
                ("Door", 100f),
                ("Tree", 50f),
                ("Rock", 50f),
                ("Bush", 50f)
            };

            foreach ((string name, float maxHealth) in definitions)
            {
                GameObject damagedInstance = CreateTestEntity(
                    $"Damaged {name}", maxHealth, settings, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly,
                    out Health damagedHealth, out HealthBar damagedBar);
                GameObject untouchedInstance = CreateTestEntity(
                    $"Untouched {name}", maxHealth, settings, HealthBarVisibilityMode.ShowAfterDamage, HealthBarColorRole.Friendly,
                    out Health untouchedHealth, out HealthBar untouchedBar);

                Assert.That(damagedBar.IsVisible, Is.False, $"{name} bar should start hidden.");
                Assert.That(untouchedBar.IsVisible, Is.False, $"{name} bar should start hidden.");
                Assert.That(damagedBar.ColorRole, Is.EqualTo(HealthBarColorRole.Friendly));
                Assert.That(damagedBar.FillColor.g, Is.GreaterThan(damagedBar.FillColor.r));
                Assert.That(damagedHealth.TryTakeDamage(new DamageContext(1f)), Is.True);
                Assert.That(damagedBar.IsVisible, Is.True);
                Assert.That(untouchedHealth.CurrentHealth, Is.EqualTo(untouchedHealth.MaxHealth));
                Assert.That(untouchedBar.IsVisible, Is.False, "Changing one object must not affect another instance.");

                Object.Destroy(damagedInstance);
                Object.Destroy(untouchedInstance);
            }
        }

        private static GameObject CreateTestEntity(
            string name,
            float maxHealth,
            HealthBarSettings settings,
            HealthBarVisibilityMode mode,
            HealthBarColorRole role,
            out Health health,
            out HealthBar bar)
        {
            GameObject root = new(name);
            health = root.AddComponent<Health>();
            health.Configure(maxHealth);

            GameObject anchor = new("HealthBarAnchor");
            anchor.transform.SetParent(root.transform, false);
            GameObject barObject = new("HealthBar");
            barObject.transform.SetParent(anchor.transform, false);
            GameObject backgroundObject = new("Background");
            backgroundObject.transform.SetParent(barObject.transform, false);
            GameObject fillObject = new("Fill");
            fillObject.transform.SetParent(barObject.transform, false);

            SpriteRenderer background = backgroundObject.AddComponent<SpriteRenderer>();
            SpriteRenderer fill = fillObject.AddComponent<SpriteRenderer>();
            bar = barObject.AddComponent<HealthBar>();
            bar.Configure(health, settings, mode, role, background, fill, new Vector2(1f, 0.12f));
            return root;
        }

        private static void AssertAlwaysVisibleFriendly(Health health, HealthBar bar, float expectedMaxHealth)
        {
            Assert.That(health, Is.Not.Null);
            Assert.That(bar, Is.Not.Null);
            Assert.That(health.MaxHealth, Is.EqualTo(expectedMaxHealth));
            Assert.That(health.CurrentHealth, Is.EqualTo(expectedMaxHealth));
            Assert.That(bar.VisibilityMode, Is.EqualTo(HealthBarVisibilityMode.AlwaysVisible));
            Assert.That(bar.ColorRole, Is.EqualTo(HealthBarColorRole.Friendly));
            Assert.That(bar.FillColor.g, Is.GreaterThan(bar.FillColor.r));
            Assert.That(bar.IsVisible, Is.True);
        }
    }
}
