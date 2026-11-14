using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pinball.Gameplay;
using Pinball.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pinball.Tests.PlayMode
{
    // The HUD shows live state and casting follows the v0.7 rules (whole-number groups: drag a group out of the
    // hand to cast it, or select it with its number key and double-tap).
    public class HudPlayModeTests
    {
        GameContext ctx;
        HudRoot hud;
        HandView hand;
        EnemyDefinition grunt;
        ElementDefinition Arc => ctx.config.elements[0];
        ElementDefinition Spark => ctx.config.elements[1];
        ElementDefinition Shock => ctx.config.elements[2];

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            ctx = Object.FindAnyObjectByType<GameContext>();
            hud = Object.FindAnyObjectByType<HudRoot>();
            hand = hud.GetComponentInChildren<HandView>();
            grunt = ctx.config.spawnProfile.enemies[0].enemy;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        IEnumerator StartPlaying()
        {
            ctx.DriveTimeManually = true;
            ctx.Run.Tick(60f);
            ctx.Balls.Active[0].Park(ctx.Board.launchPoint.Position);
            foreach (var e in ctx.Enemies.FrontFirst(new List<Enemy>())) e.Remove();
            // Sorted: 1 Arc, 2 Spark, 2 Shockwave, 3 Arc, 3 Spark.
            ctx.Hand.SetHand(new CardSpec(Spark, 2), new CardSpec(Shock, 2), new CardSpec(Arc, 1), new CardSpec(Arc, 3), new CardSpec(Spark, 3));
            ctx.Hand.SetLuckyNumber(0);
            yield return null;
            yield return null; // let presenters refresh
        }

        T Find<T>() where T : Component => hud.GetComponentInChildren<T>(true);

        [UnityTest]
        public IEnumerator CountdownShowsUntilLaunchThenRelaunch()
        {
            var countdown = Find<CountdownView>().text;
            Assert.That(countdown.gameObject.activeSelf);
            Assert.That(countdown.text, Is.EqualTo("3"));
            yield return StartPlaying();
            Assert.That(countdown.gameObject.activeSelf, Is.False);
            ctx.Board.drain.SendMessage("OnTriggerEnter", ctx.Balls.Active[0].GetComponent<Collider>());
            Assert.That(ctx.Balls.Count, Is.Zero, "drained");
            ctx.Clock.Advance(1.2f);
            yield return null;
            Assert.That(countdown.gameObject.activeSelf, "the replacement countdown shows");
            Assert.That(countdown.text, Does.Contain("RELAUNCH").And.EndsWith("2"));
        }

        [UnityTest]
        public IEnumerator BarsShowLiveState()
        {
            yield return StartPlaying();
            var top = Find<TopBarView>();
            if (top.lives) Assert.That(top.lives.text, Is.EqualTo("3 / 3"));
            Assert.That(top.score.text, Is.EqualTo("0"));
            if (top.time) Assert.That(top.time.text, Is.EqualTo("00:00"));
            var cast = Find<CastPanelView>();
            if (cast)
            {
                Assert.That(cast.status.text, Is.EqualTo("No lucky number · Drag a group out to cast"));
                if (cast.lastCast) Assert.That(cast.lastCast.text, Is.EqualTo("No cast yet"));
                Assert.That(cast.charge.text, Is.EqualTo("Equipment full"));
            }
            var status = Find<ElementStatusView>();
            if (status) Assert.That(status.text.text, Is.EqualTo("BALLS 1 · NO LUCKY · Arc ×2 · Spark ×2 · Shockwave ×1"));

            var e = ctx.Spawner.Spawn(grunt, Arc, new Vector3(0, 0, 4), health: 4);
            e.Health.TakeDamage(4);
            ctx.Clock.Advance(65f);
            yield return null;
            Assert.That(top.score.text, Is.EqualTo((50 + 13 * 5).ToString()), "a kill plus 13 time rewards");
            if (top.time) Assert.That(top.time.text, Is.EqualTo("01:05"));
        }

        [UnityTest]
        public IEnumerator SlotsShowTheHandSortedWithLuckyCardsMarked()
        {
            yield return StartPlaying();
            ctx.Hand.SetLuckyNumber(2);
            yield return null;
            var slot0 = hand.slots[0];
            Assert.That(slot0.title.text, Is.EqualTo("1 · Arc"));
            Assert.That(hand.slots[3].title.text, Is.EqualTo("3 · Arc"));
            Assert.That(slot0.description.text, Does.StartWith("Arc: 4 dmg"), "no lucky Arc card held");
            Assert.That(hand.slots[1].caption.text, Does.StartWith("LUCKY"), "#2 is lucky");
            Assert.That(hand.slots[1].edge.color, Is.EqualTo(hud.theme.gold));
            Assert.That(slot0.edge.color, Is.EqualTo(Arc.color));
            Assert.That(hand.slots[1].description.text, Does.Contain("0.75 DPS").Or.Contain("1.75"), "two Spark cards held, one lucky");
        }

        [UnityTest]
        public IEnumerator PressingACardSelectsItsWholeGroup()
        {
            yield return StartPlaying();
            var screen = RectTransformUtility.WorldToScreenPoint(null, hand.slots[1].transform.position);
            hand.Press(1, screen);
            Assert.That(hand.Selection.Count, Is.EqualTo(2), "both #2 cards");
            Assert.That(hand.SelectedNumber, Is.EqualTo(2));
            yield return null;
            Assert.That(hand.slots[2].edge.color, Is.EqualTo(hud.theme.gold), "the whole group is highlighted");
            Assert.That(hand.Release(screen), Is.False, "a click without a drag never casts");
            Assert.That(ctx.Hand.Count, Is.EqualTo(5));
            Assert.That(hand.Selection, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DraggingAGroupOutOfTheHandCastsIt()
        {
            yield return StartPlaying();
            var start = RectTransformUtility.WorldToScreenPoint(null, hand.slots[3].transform.position);
            hand.Press(3, start);
            hand.Move(start + Vector2.up * 20f);
            Assert.That(hand.Dragging);
            Assert.That(hand.Release(start + Vector2.up * 20f), Is.False, "released inside the hand: back in place");
            Assert.That(ctx.Hand.Count, Is.EqualTo(5));

            var outside = new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(hand.handArea, outside, null), Is.False);
            hand.Press(3, start);
            hand.Move(outside);
            Assert.That(hand.Release(outside), "released outside the hand: cast");
            Assert.That(ctx.Hand.Count, Is.EqualTo(3), "both #3 cards were cast");
            Assert.That(ctx.Casts.LastBatch.number, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator NumberKeySelectsAndDoubleTapCasts()
        {
            yield return StartPlaying();
            Assert.That(hand.PressKey(2, 10.0), Is.EqualTo(Pinball.Core.GroupKeyAction.Select));
            Assert.That(hand.Selection.Count, Is.EqualTo(2));
            yield return null;
            if (hand.keyHint) Assert.That(hand.keyHint.activeSelf);
            if (hand.keyHintText) Assert.That(hand.keyHintText.text, Is.EqualTo("Double-tap 2 to cast"));
            Assert.That(hand.PressKey(2, 11.0), Is.EqualTo(Pinball.Core.GroupKeyAction.AwaitSecondTap));
            Assert.That(hand.AdvanceKeys(11.5), Is.EqualTo(Pinball.Core.GroupKeyAction.Cancel), "a single tap cancels");
            Assert.That(hand.Selection, Is.Empty);
            Assert.That(ctx.Hand.Count, Is.EqualTo(5));

            hand.PressKey(1, 20.0);
            hand.PressKey(1, 20.5);
            Assert.That(hand.PressKey(1, 20.7), Is.EqualTo(Pinball.Core.GroupKeyAction.Cast));
            Assert.That(ctx.Hand.Count, Is.EqualTo(4));
            Assert.That(hand.Selection, Is.Empty, "casting clears the selection");
        }

        [UnityTest]
        public IEnumerator SelectionShowsThePreviewAndHighlightsTargets()
        {
            yield return StartPlaying();
            var arcFoe = ctx.Spawner.Spawn(grunt, Arc, new Vector3(-2, 0, 4));
            var sparkFoe = ctx.Spawner.Spawn(grunt, Spark, new Vector3(2, 0, 4));
            hand.PressKey(3, 1.0); // #3: Arc and Spark
            yield return null;
            yield return null;
            Assert.That(arcFoe.GetComponent<EnemyView>().Previewed, "Spark can burn an Arc foe");
            Assert.That(sparkFoe.GetComponent<EnemyView>().Previewed, "Arc can strike a Spark foe");
            var preview = Find<SelectionPreviewView>();
            if (preview)
            {
                Assert.That(preview.panel.gameObject.activeSelf);
                Assert.That(preview.title.text, Is.EqualTo("Normal · Group #3 · 2 cards"));
                Assert.That(preview.details.text, Does.Contain("Targets: 2"));
                Assert.That(preview.details.text, Does.Contain("Arc: All · 2 waves × 40"));
                Assert.That(preview.details.text, Does.Contain("Spark: held 2 → 1"));
                Assert.That(preview.details.text, Does.Contain("Charge required: 0 → 250"));
            }
            hand.PressKey(1, 2.0);
            yield return null;
            Assert.That(sparkFoe.GetComponent<EnemyView>().Previewed, "#1 Arc still reaches the Spark foe");
            Assert.That(arcFoe.GetComponent<EnemyView>().Previewed, Is.False, "but not the Arc foe");
        }

        [UnityTest]
        public IEnumerator EnemyLabelsShowElementHealthAndStatus()
        {
            yield return StartPlaying();
            var a = ctx.Spawner.Spawn(grunt, Arc, new Vector3(-2, 0, 4), health: 40);
            var b = ctx.Spawner.Spawn(grunt, Spark, new Vector3(2, 0, 4));
            b.Status.Stun(Shock, 1f, true);
            a.Health.TakeDamage(4);
            yield return null;
            var layer = Find<EnemyLabelLayer>();
            Assert.That(layer.Count, Is.EqualTo(2));
            Assert.That(layer.LabelOf(a).number.text, Is.EqualTo(Arc.symbol));
            Assert.That(layer.LabelOf(a).number.color, Is.EqualTo(Arc.color));
            Assert.That(layer.LabelOf(a).health.text, Is.EqualTo("36/40"));
            Assert.That(layer.LabelOf(b).status.text, Is.EqualTo("Stunned"));
            var screen = hud.WorldToCanvas(a.transform.position + a.transform.up * a.centerHeight);
            Assert.That(Vector2.Distance(layer.LabelOf(a).number.rectTransform.anchoredPosition, screen), Is.LessThan(1f), "symbol sits on the enemy");
            a.Remove();
            yield return null;
            Assert.That(layer.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PopupsShowHitsKillsChargeAndCombos()
        {
            yield return StartPlaying();
            var e = ctx.Spawner.Spawn(grunt, Arc, new Vector3(0, 0, 4), health: 8);
            e.Health.TakeDamage(4);
            e.Health.TakeDamage(4);
            hand.PressKey(1, 1.0);
            Assert.That(hand.Cast());
            var target = ctx.Spawner.Spawn(grunt, Spark, new Vector3(2, 0, 4), health: 1000);
            var ball = ctx.Balls.Active[0];
            ball.Body.isKinematic = false;
            for (int i = 0; i < 5; i++)
            {
                target.Contact.TryContact(ball, target.transform.position);
                ctx.Clock.Advance(0.25f);
            }
            ctx.Clock.Advance(3.1f);
            yield return null;
            var texts = Find<PopupLayer>().Texts.ToList();
            Assert.That(texts, Has.Member("-4"));
            Assert.That(texts, Has.Member("+50"));
            Assert.That(texts, Has.Member("+10"), "contact charge while a card is missing");
            Assert.That(texts, Has.Member("COMBO +50"), "five counted hits, then the timeout");
        }

        [UnityTest]
        public IEnumerator PauseDialogRestartsOrResumes()
        {
            yield return StartPlaying();
            var dialog = Find<DialogView>();
            Assert.That(dialog.panel.activeSelf, Is.False);
            ctx.Run.SetPaused(true);
            yield return null;
            Assert.That(dialog.panel.activeSelf);
            Assert.That(dialog.title.text, Is.EqualTo("PAUSED\nRestart this run?"));
            Assert.That(dialog.confirmLabel.text, Is.EqualTo("Yes"));
            Assert.That(dialog.cancel.gameObject.activeSelf);
            dialog.cancel.onClick.Invoke();
            yield return null;
            Assert.That(ctx.Run.Paused, Is.False);
            Assert.That(dialog.panel.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator GameOverDialogShowsTheResult()
        {
            yield return StartPlaying();
            ctx.Run.LoseLives(3, "Enemy breach");
            yield return null;
            var dialog = Find<DialogView>();
            Assert.That(dialog.panel.activeSelf);
            Assert.That(dialog.title.text, Does.StartWith("GAME OVER"));
            Assert.That(dialog.title.text, Does.Contain("Score </color>0"));
            Assert.That(dialog.confirmLabel.text, Is.EqualTo("Restart"));
            Assert.That(dialog.cancel.gameObject.activeSelf, Is.False);
        }

        // The real EventSystem path: pointer down / drag / up on a CardView drag its group out and cast it.
        [UnityTest]
        public IEnumerator PointerEventsDragAndCast()
        {
            yield return StartPlaying();
            var go = hand.slots[0].gameObject;
            var start = RectTransformUtility.WorldToScreenPoint(null, go.transform.position);
            var outside = new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = start };
            ExecuteEvents.Execute(go, data, ExecuteEvents.pointerDownHandler);
            Assert.That(hand.Selection.Count, Is.EqualTo(1));
            data.position = outside;
            ExecuteEvents.Execute(go, data, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(go, data, ExecuteEvents.pointerUpHandler);
            Assert.That(ctx.Hand.Count, Is.EqualTo(4), "cast");
            Assert.That(ctx.Casts.LastBatch.number, Is.EqualTo(1));
            yield return null;
        }
    }
}
