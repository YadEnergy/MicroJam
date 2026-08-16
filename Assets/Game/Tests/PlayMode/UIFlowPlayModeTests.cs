using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MicroJam.Game.Tests
{
    public sealed class UIFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator GameplaySceneContainsResponsiveSceneBoundUiAndFourSynchronizedToolbarSlots()
        {
            yield return Load("Game");
            BuildHotbarHintsUI hotbar = Object.FindFirstObjectByType<BuildHotbarHintsUI>();
            BuildingSystem buildings = Object.FindFirstObjectByType<BuildingSystem>();
            Assert.That(FindSceneObject("PauseMenu"), Is.Not.Null);
            Assert.That(FindSceneObject("DeathOverlay"), Is.Not.Null);
            Assert.That(FindSceneObject("SceneTransitionOverlay"), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PauseMenuController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PlayerRespawnController>(), Is.Not.Null);
            Assert.That(hotbar.WallSlot, Is.Not.Null);
            Assert.That(hotbar.DoorSlot, Is.Not.Null);
            Assert.That(hotbar.BowTowerSlot, Is.Not.Null);
            Assert.That(hotbar.StoneTowerSlot, Is.Not.Null);
            Assert.That(hotbar.transform.childCount, Is.EqualTo(4));
            RectTransform hotbarRect = hotbar.transform as RectTransform;
            Assert.That(hotbarRect.anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(hotbarRect.anchorMax, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(hotbarRect.pivot, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(hotbarRect.anchoredPosition, Is.EqualTo(new Vector2(-24f, 24f)));
            RectTransform resourceHud = FindSceneObject("ResourceHUD").transform as RectTransform;
            Assert.That(resourceHud.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(resourceHud.anchoredPosition, Is.EqualTo(new Vector2(24f, 24f)));
            Assert.That(FindSceneObject("WoodIcon").GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(70f, 70f)));
            Assert.That(FindSceneObject("StoneIcon").GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(70f, 70f)));
            Transform deathOverlay = FindSceneObject("DeathOverlay").transform;
            Transform pauseMenu = FindSceneObject("PauseMenu").transform;
            Transform endedInfo = FindSceneObject("EndedInfo").transform;
            Assert.That(deathOverlay.GetSiblingIndex(), Is.LessThan(pauseMenu.GetSiblingIndex()));
            Assert.That(pauseMenu.GetSiblingIndex(), Is.LessThan(endedInfo.GetSiblingIndex()));

            foreach (CanvasScaler scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(683f, 683f)));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.001f));
            }

            Canvas toolbarCanvas = hotbar.GetComponentInParent<Canvas>();
            Camera gameplayCamera = Object.FindFirstObjectByType<SquareGameplayViewport>().GetComponent<Camera>();
            Assert.That(toolbarCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(toolbarCanvas.worldCamera, Is.SameAs(gameplayCamera));
            Assert.That(toolbarCanvas.overrideSorting, Is.True);
            Assert.That(toolbarCanvas.sortingOrder, Is.GreaterThanOrEqualTo(200));

            RectTransform canvasRect = toolbarCanvas.transform as RectTransform;
            AssertInsideCanvas(canvasRect, FindSceneObject("DayNightBar").transform as RectTransform);
            AssertInsideCanvas(canvasRect, FindSceneObject("WaveInfoText").transform as RectTransform);
            AssertInsideCanvas(canvasRect, FindSceneObject("PointsInfoUI").transform as RectTransform);
            AssertInsideCanvas(canvasRect, resourceHud);
            AssertInsideCanvas(canvasRect, hotbarRect);
            AssertInsideCanvas(canvasRect, FindSceneObject("TutorialPanel").transform as RectTransform);

            Image[] slots = { hotbar.WallSlot, hotbar.DoorSlot, hotbar.BowTowerSlot, hotbar.StoneTowerSlot };
            BuildSelection[] selections = { BuildSelection.Wall, BuildSelection.Door, BuildSelection.BowTower, BuildSelection.StoneTower };
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].GetComponent<Button>().onClick.Invoke();
                Assert.That(buildings.Selection, Is.EqualTo(selections[i]));
                for (int j = 0; j < slots.Length; j++)
                    Assert.That(slots[j].GetComponent<UIButtonTween>().IsSelected, Is.EqualTo(i == j));
            }

            buildings.CancelBuildMode();
            Assert.That(slots.All(slot => !slot.GetComponent<UIButtonTween>().IsSelected), Is.True);
            InputAction cancel = buildings.InputActions.FindAction("Building/Cancel", true);
            Assert.That(cancel.bindings.Count, Is.EqualTo(1));
            Assert.That(cancel.bindings[0].effectivePath, Is.EqualTo("<Mouse>/rightButton"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PanelButtonPauseAndTransitionTweensUseUnscaledTime()
        {
            yield return Load("Game");
            BuildingInteractionPopup buildingPopup = Object.FindFirstObjectByType<BuildingInteractionPopup>(FindObjectsInactive.Include);
            CampfireInteractionPopup campfirePopup = Object.FindFirstObjectByType<CampfireInteractionPopup>(FindObjectsInactive.Include);
            PauseMenuController pause = Object.FindFirstObjectByType<PauseMenuController>();
            SceneTransitionController transition = Object.FindFirstObjectByType<SceneTransitionController>();
            Assert.That(buildingPopup.PanelTween, Is.Not.Null);
            Assert.That(campfirePopup.PanelTween, Is.Not.Null);

            Time.timeScale = 0f;
            buildingPopup.PanelTween.Show();
            yield return new WaitForSecondsRealtime(0.32f);
            Assert.That(buildingPopup.PanelTween.CanvasGroup.alpha, Is.EqualTo(1f).Within(0.02f));
            buildingPopup.PanelTween.Hide();
            yield return new WaitForSecondsRealtime(0.24f);
            Assert.That(buildingPopup.gameObject.activeSelf, Is.False);

            campfirePopup.PanelTween.Show();
            yield return new WaitForSecondsRealtime(0.32f);
            Assert.That(campfirePopup.PanelTween.CanvasGroup.alpha, Is.EqualTo(1f).Within(0.02f));
            campfirePopup.PanelTween.Hide();
            yield return new WaitForSecondsRealtime(0.24f);

            Time.timeScale = 1f;
            pause.Pause();
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(GameplayInputGate.IsBlocked, Is.True);
            yield return new WaitForSecondsRealtime(0.32f);
            Assert.That(pause.PausePanel.CanvasGroup.alpha, Is.EqualTo(1f).Within(0.02f));
            pause.Resume();
            yield return new WaitForSecondsRealtime(0.24f);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(GameplayInputGate.IsBlocked, Is.False);

            Time.timeScale = 0f;
            transition.FadeFromBlack();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(transition.Overlay.alpha, Is.Zero.Within(0.02f));
            Time.timeScale = 1f;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PlayerDeathPausesOnlyInputsRespawnsWithResourcesAndGrantsInvulnerability()
        {
            yield return Load("Game");
            PlayerRespawnController respawn = Object.FindFirstObjectByType<PlayerRespawnController>();
            PauseMenuController pause = Object.FindFirstObjectByType<PauseMenuController>();
            PlayerResourceWallet wallet = Object.FindFirstObjectByType<PlayerResourceWallet>();
            Health health = respawn.PlayerHealth;
            respawn.ConfigureTiming(0.5f, 0.3f);
            int wood = wallet.Wood;
            int stone = wallet.Stone;
            float worldTime = Time.time;
            Assert.That(health.TryTakeDamage(new DamageContext(health.MaxHealth + 1f, null)), Is.True);
            yield return null;
            Assert.That(respawn.IsRespawning, Is.True);
            Assert.That(GameplayInputGate.IsBlocked, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Player death paused the world.");
            Assert.That(Time.time, Is.GreaterThanOrEqualTo(worldTime));

            string beforePause = respawn.CountdownText.text;
            pause.Pause();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(respawn.CountdownText.text, Is.EqualTo(beforePause));
            pause.Resume();
            yield return new WaitForSecondsRealtime(0.24f);
            yield return new WaitForSeconds(0.65f);
            Assert.That(respawn.IsRespawning, Is.False);
            Assert.That(health.IsDead, Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
            Assert.That(wallet.Wood, Is.EqualTo(wood));
            Assert.That(wallet.Stone, Is.EqualTo(stone));
            Assert.That(respawn.IsInvulnerable, Is.True);
            Assert.That(health.TryTakeDamage(new DamageContext(5f, null)), Is.False);
            yield return new WaitForSeconds(0.35f);
            Assert.That(respawn.IsInvulnerable, Is.False);
            Assert.That(health.TryTakeDamage(new DamageContext(5f, null)), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BlockedPreferredRespawnFindsAnotherClearCampfireAdjacentPosition()
        {
            yield return Load("Game");
            PlayerRespawnController respawn = Object.FindFirstObjectByType<PlayerRespawnController>();
            Vector2 preferred = respawn.FindRespawnPosition();
            GameObject blocker = new("Respawn Test Building Blocker") { layer = GameLayers.BuildingIndex };
            blocker.transform.position = preferred;
            blocker.AddComponent<BoxCollider2D>().size = Vector2.one;
            Physics2D.SyncTransforms();
            Vector2 alternate = respawn.FindRespawnPosition();
            Assert.That(alternate, Is.Not.EqualTo(preferred));
            Assert.That(respawn.IsRespawnPositionClear(alternate, 0.4f), Is.True);
            Object.Destroy(blocker);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GameOverImmediatelyCancelsAndHidesAnActiveRespawnCountdown()
        {
            yield return Load("Game");
            PlayerRespawnController respawn = Object.FindFirstObjectByType<PlayerRespawnController>();
            Health health = respawn.PlayerHealth;
            respawn.ConfigureTiming(5f, 3f);
            Assert.That(health.TryTakeDamage(new DamageContext(health.MaxHealth + 1f, null)), Is.True);
            yield return null;
            Assert.That(respawn.IsRespawning, Is.True);
            Assert.That(respawn.DeathOverlay.gameObject.activeSelf, Is.True);

            GameEvents.RaiseCampfireDestroyed();
            yield return null;

            Assert.That(respawn.IsRespawning, Is.False);
            Assert.That(respawn.DeathOverlay.gameObject.activeSelf, Is.False);
            Assert.That(FindSceneObject("EndedInfo").activeSelf, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MainMenuIsCompleteSceneBoundAndResponsive()
        {
            yield return Load("SampleScene");
            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(GameObject.Find("MainMenuRoot"), Is.Not.Null);
            Assert.That(GameObject.Find("Title").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Cogito Ergo Sum"));
            Assert.That(menu.PlayButton, Is.Not.Null);
            Assert.That(menu.ExitButton, Is.Not.Null);
            Assert.That(menu.PlayButton.GetComponent<UIButtonTween>(), Is.Not.Null);
            Assert.That(menu.ExitButton.GetComponent<UIButtonTween>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<EventSystem>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<SceneTransitionController>(), Is.Not.Null);
            CanvasScaler scaler = Object.FindFirstObjectByType<CanvasScaler>();
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1024f, 1024f)));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator Load(string sceneName)
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            yield return null;
            Physics2D.SyncTransforms();
        }

        private static GameObject FindSceneObject(string objectName)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.scene == SceneManager.GetActiveScene() &&
                                             candidate.name == objectName)
                ?.gameObject;
        }

        private static void AssertInsideCanvas(RectTransform canvas, RectTransform target)
        {
            Assert.That(canvas, Is.Not.Null);
            Assert.That(target, Is.Not.Null);
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, target);
            Rect rect = canvas.rect;
            const float tolerance = 0.1f;
            Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(rect.xMin - tolerance), $"{target.name} extends past the left edge.");
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(rect.xMax + tolerance), $"{target.name} extends past the right edge.");
            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(rect.yMin - tolerance), $"{target.name} extends past the bottom edge.");
            Assert.That(bounds.max.y, Is.LessThanOrEqualTo(rect.yMax + tolerance), $"{target.name} extends past the top edge.");
        }
    }
}
