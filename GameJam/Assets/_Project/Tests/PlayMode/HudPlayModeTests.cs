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
    // Phase 6 "done when": feature parity with the old HUD. Presenters show live state and drag-to-play
    // follows the old rules.
    public class HudPlayModeTests
    {
        GameContext ctx;
        HudRoot hud;
        HandView hand;
        EnemyDefinition grunt;

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
            yield return null;
            yield return null; // let presenters refresh
        }

        Vector2 PlayfieldScreenPoint => hud.WorldCamera.WorldToScreenPoint(ctx.Board.ToWorld(new Vector3(0f, 0f, 3f)));
        Vector2 HandScreenPoint => RectTransformUtility.WorldToScreenPoint(null, hand.slots[2].transform.position);

        T Find<T>() where T : Component => hud.GetComponentInChildren<T>(true);

        [UnityTest]
        public IEnumerator CountdownShowsUntilLaunch()
        {
            var countdown = Find<CountdownView>().text;
            Assert.That(countdown.gameObject.activeSelf);
            Assert.That(countdown.text, Is.EqualTo("3"));
            yield return StartPlaying();
            Assert.That(countdown.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator BarsShowLiveState()
        {
            yield return StartPlaying();
            var top = Find<TopBarView>();
            Assert.That(top.lives.text, Is.EqualTo("3 / 3"));
            Assert.That(top.score.text, Is.EqualTo("0"));
            Assert.That(top.time.text, Is.EqualTo("00:00"));
            var luck = Find<LuckPanelView>();
            Assert.That(luck.lucky.text, Does.StartWith($"{ctx.Luck.Current}  Next"));
            Assert.That(luck.lastCast.text, Is.EqualTo("Active skill: not used"));
            Assert.That(luck.refill.text, Is.EqualTo("Equipment full"));
            Assert.That(Find<ElementStatusView>().text.text, Is.EqualTo("Arc [#2×1, #4×1] · Spark [#5×1, #6×1] · Shockwave [#6×1]"));

            var e = ctx.Spawner.Spawn(grunt, 3, new Vector3(0, 0, 4));
            e.Health.SetCurrent(4);
            e.Health.TakeDamage(4);
            ctx.Run.LoseLives(1, "test");
            ctx.Clock.Advance(65f);
            yield return null;
            Assert.That(top.score.text, Is.EqualTo("50"));
            Assert.That(top.lives.text, Is.EqualTo("2 / 3"));
            Assert.That(top.time.text, Is.EqualTo("01:05"));
        }

        [UnityTest]
        public IEnumerator SlotsShowTheHandWithLuckyGold()
        {
            yield return StartPlaying();
            ctx.Luck.SetLuckyNumber(2);
            yield return null;
            var slot0 = hand.slots[0];
            Assert.That(slot0.title.text, Is.EqualTo("6 · Spark"));
            Assert.That(slot0.description.text, Is.EqualTo("#6 burn: 4 DPS / 3s\nSame-number spread"));
            Assert.That(slot0.caption.text, Is.EqualTo("#6 stack: 1"));
            Assert.That(hand.slots[1].description.text, Is.EqualTo("#6 stun: 0.35s\nMore #6 cards: wider area"));
            Assert.That(hand.slots[2].description.text, Is.EqualTo("#2 foes in 3m\n4 dmg; excludes hit foe"));
            Assert.That(hand.slots[2].edge.color, Is.EqualTo(hud.theme.gold), "lucky number is gold");
            Assert.That(slot0.edge.color, Is.EqualTo(hud.theme.panelEdge));
        }

        [UnityTest]
        public IEnumerator DropOverThePlayfieldPlaysTheCard()
        {
            yield return StartPlaying();
            hand.Press(2, HandScreenPoint);
            Assert.That(hand.DragSlot, Is.EqualTo(2));
            Assert.That(hand.Release(PlayfieldScreenPoint), "played");
            Assert.That(ctx.Hand.Count, Is.EqualTo(4));
            Assert.That(hand.DragSlot, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator DropOverTheHudCancels()
        {
            yield return StartPlaying();
            hand.Press(2, HandScreenPoint);
            Assert.That(hand.Release(HandScreenPoint), Is.False);
            Assert.That(ctx.Hand.Count, Is.EqualTo(5), "the card goes back");
        }

        [UnityTest]
        public IEnumerator CardsCannotBePickedUpOutsidePlay()
        {
            hand.Press(2, HandScreenPoint); // still counting down
            Assert.That(hand.DragSlot, Is.EqualTo(-1));
            yield return StartPlaying();
            ctx.Run.SetPaused(true);
            hand.Press(2, HandScreenPoint);
            Assert.That(hand.DragSlot, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator DragFollowsAShiftedCardAndDropsOnReplacement()
        {
            yield return StartPlaying();
            hand.Press(4, HandScreenPoint);
            ctx.Hand.TryPlay(0);
            Assert.That(hand.DragSlot, Is.EqualTo(3), "the dragged card moved left");
            ctx.Hand.SetHand(); // every slot replaced (now empty)
            Assert.That(hand.DragSlot, Is.EqualTo(-1), "replacing the dragged slot drops the drag");
            hand.Press(0, HandScreenPoint);
            Assert.That(hand.DragSlot, Is.EqualTo(-1), "nothing to pick up");
            ctx.Hand.SetHand(new CardSpec(ctx.config.elements[1], 4));
            hand.Press(0, HandScreenPoint);
            ctx.Hand.Grant(new CardSpec(ctx.config.elements[0], 5));
            Assert.That(hand.DragSlot, Is.EqualTo(0), "a refill into another slot keeps the drag");
        }

        [UnityTest]
        public IEnumerator DraggingShowsTheGhostAndHighlightsMatchingEnemies()
        {
            yield return StartPlaying();
            var two = ctx.Spawner.Spawn(grunt, 2, new Vector3(-2, 0, 4));
            var five = ctx.Spawner.Spawn(grunt, 5, new Vector3(2, 0, 4));
            hand.Press(2, HandScreenPoint); // a #2 Arc
            yield return null;
            yield return null;
            var ghost = Find<DragGhostView>();
            Assert.That(ghost.panel.gameObject.activeSelf);
            Assert.That(ghost.title.text, Is.EqualTo("2 · Arc"));
            Assert.That(ghost.details.text, Does.Contain("Passive #2 matches: 1 foes (gold)"));
            Assert.That(ghost.details.text, Does.Contain("Next refill: 8.0 → 12.0s"));
            Assert.That(two.GetComponent<EnemyView>().Previewed);
            Assert.That(five.GetComponent<EnemyView>().Previewed, Is.False);
            var labels = Find<EnemyLabelLayer>();
            Assert.That(labels.LabelOf(two).number.color, Is.EqualTo(hud.theme.gold));
            hand.Release(HandScreenPoint);
            yield return null;
            Assert.That(ghost.panel.gameObject.activeSelf, Is.False);
            Assert.That(two.GetComponent<EnemyView>().Previewed, Is.False);
        }

        [UnityTest]
        public IEnumerator EnemyLabelsFollowEnemies()
        {
            yield return StartPlaying();
            var a = ctx.Spawner.Spawn(grunt, 3, new Vector3(-2, 0, 4));
            var b = ctx.Spawner.Spawn(grunt, 4, new Vector3(2, 0, 4));
            b.Status.Stun(1f, true);
            a.Health.TakeDamage(4);
            yield return null;
            var layer = Find<EnemyLabelLayer>();
            Assert.That(layer.Count, Is.EqualTo(2));
            Assert.That(layer.LabelOf(a).number.text, Is.EqualTo("3"));
            Assert.That(layer.LabelOf(a).health.text, Is.EqualTo("36/40"));
            Assert.That(layer.LabelOf(b).status.text, Is.EqualTo("Stunned"));
            var screen = hud.WorldToCanvas(a.transform.position + a.transform.up * a.centerHeight);
            Assert.That(Vector2.Distance(layer.LabelOf(a).number.rectTransform.anchoredPosition, screen), Is.LessThan(1f), "number sits on the enemy");
            a.Remove();
            yield return null;
            Assert.That(layer.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PopupsShowHitsKillsAndCasts()
        {
            yield return StartPlaying();
            var e = ctx.Spawner.Spawn(grunt, 3, new Vector3(0, 0, 4));
            e.Health.SetCurrent(8);
            e.Health.TakeDamage(4);
            e.Health.TakeDamage(4);
            ctx.Luck.ForceNextQuality(40);
            ctx.Hand.TryPlay(2);
            yield return null;
            var texts = Find<PopupLayer>().Texts.ToList();
            Assert.That(texts, Has.Member("-4"));
            Assert.That(texts, Has.Member("+50"));
            Assert.That(texts, Has.Member("Thunderstorm · 2 waves  Q=40"));
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

        // The real EventSystem path: pointer events on a CardView reach the HandView.
        [UnityTest]
        public IEnumerator PointerEventsOnACardDriveTheDrag()
        {
            yield return StartPlaying();
            var data = new PointerEventData(EventSystem.current) { position = HandScreenPoint, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(hand.slots[2].gameObject, data, ExecuteEvents.pointerDownHandler);
            Assert.That(hand.DragSlot, Is.EqualTo(2));
            data.position = PlayfieldScreenPoint;
            ExecuteEvents.Execute(hand.slots[2].gameObject, data, ExecuteEvents.pointerUpHandler);
            Assert.That(ctx.Hand.Count, Is.EqualTo(4), "released over the playfield: played");
        }
    }
}
