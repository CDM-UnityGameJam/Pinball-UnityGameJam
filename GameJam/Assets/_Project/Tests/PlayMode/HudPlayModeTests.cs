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
    // The HUD shows live state and card selection follows the v0.6 rules (click to select same-number cards,
    // Cast to roll one die per card).
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
            ctx.Hand.SetHand(new CardSpec(Spark, 6), new CardSpec(Shock, 6), new CardSpec(Arc, 2), new CardSpec(Arc, 4), new CardSpec(Spark, 5));
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
            Assert.That(top.lives.text, Is.EqualTo("3 / 3"));
            Assert.That(top.score.text, Is.EqualTo("0"));
            Assert.That(top.time.text, Is.EqualTo("00:00"));
            var cast = Find<CastPanelView>();
            Assert.That(cast.status.text, Is.EqualTo("Select cards"));
            Assert.That(cast.lastCast.text, Is.EqualTo("No cast yet"));
            Assert.That(cast.charge.text, Is.EqualTo("Equipment full"));
            Assert.That(Find<ElementStatusView>().text.text, Is.EqualTo("BALLS 1 · POWER 0 · Arc ×2 · Spark ×2 · Shockwave ×1"));

            var e = ctx.Spawner.Spawn(grunt, Arc, new Vector3(0, 0, 4), health: 4);
            e.Health.TakeDamage(4);
            ctx.Clock.Advance(65f);
            yield return null;
            Assert.That(top.score.text, Is.EqualTo((50 + 13 * 5).ToString()), "a kill plus 13 time rewards");
            Assert.That(top.time.text, Is.EqualTo("01:05"));
        }

        [UnityTest]
        public IEnumerator SlotsShowTheHand()
        {
            yield return StartPlaying();
            var slot0 = hand.slots[0];
            Assert.That(slot0.title.text, Is.EqualTo("6 · Spark"));
            Assert.That(slot0.description.text, Is.EqualTo("Burn: 0.75 DPS · 6s\nFractions add up per tick"), "two Spark cards held");
            Assert.That(slot0.caption.text, Is.EqualTo(Spark.symbol + "  PAIR"), "another #6 is held");
            Assert.That(hand.slots[2].description.text, Is.EqualTo("Arc: 4 dmg · Hit + 4 nearby\n5m · Arc foes immune"));
            Assert.That(slot0.edge.color, Is.EqualTo(Spark.color));
        }

        [UnityTest]
        public IEnumerator SelectionLocksTheNumberAndCasts()
        {
            yield return StartPlaying();
            Assert.That(hand.Toggle(0), "select #6 Spark");
            Assert.That(hand.Toggle(2), Is.False, "#2 is blocked while #6 is selected");
            Assert.That(hand.Toggle(1), "select #6 Shockwave");
            yield return null;
            Assert.That(hand.slots[2].edge.color, Is.EqualTo(hud.theme.panelEdge), "blocked cards are greyed");
            Assert.That(hand.slots[0].edge.color, Is.EqualTo(hud.theme.gold));
            Assert.That(hand.castLabel.text, Is.EqualTo("Cast 2 · Roll 2 dice"));
            Assert.That(Find<CastPanelView>().status.text, Is.EqualTo("Match #6 · 2 selected"));
            Assert.That(hand.Toggle(1), "click again to unselect");
            Assert.That(hand.Selection.Count, Is.EqualTo(1));

            ctx.Hand.ForceNextDice(new[] { 6 });
            Assert.That(hand.Cast());
            Assert.That(hand.Selection, Is.Empty, "casting clears the selection");
            Assert.That(ctx.Hand.Count, Is.EqualTo(4));
            Assert.That(ctx.Hand.EmpoweredHits, Is.EqualTo(1));
            Assert.That(hand.Toggle(0), Is.False, "nothing can be selected while the dice roll");
        }

        [UnityTest]
        public IEnumerator SelectionShowsThePreviewAndHighlightsTargets()
        {
            yield return StartPlaying();
            var arcFoe = ctx.Spawner.Spawn(grunt, Arc, new Vector3(-2, 0, 4));
            var sparkFoe = ctx.Spawner.Spawn(grunt, Spark, new Vector3(2, 0, 4));
            hand.Toggle(4); // #5 Spark
            yield return null;
            yield return null;
            var preview = Find<SelectionPreviewView>();
            Assert.That(preview.panel.gameObject.activeSelf);
            Assert.That(preview.title.text, Is.EqualTo("1 selected · #5"));
            Assert.That(preview.details.text, Does.Contain("Upgrade chance: 16.7%"));
            Assert.That(preview.details.text, Does.Contain("Possible targets: 1 · Power +1"));
            Assert.That(preview.details.text, Does.Contain("Spark Base:3/6s · Hit:5/6s"));
            Assert.That(preview.details.text, Does.Contain("Spark: held 2 → 1"));
            Assert.That(preview.details.text, Does.Contain("Charge required: 100 → 100"));
            Assert.That(arcFoe.GetComponent<EnemyView>().Previewed, "Spark can burn an Arc foe");
            Assert.That(sparkFoe.GetComponent<EnemyView>().Previewed, Is.False, "Spark foes are immune");
            Assert.That(Find<EnemyLabelLayer>().LabelOf(arcFoe).number.color, Is.EqualTo(hud.theme.gold));
            hand.Toggle(4);
            yield return null;
            Assert.That(preview.panel.gameObject.activeSelf, Is.False);
            Assert.That(arcFoe.GetComponent<EnemyView>().Previewed, Is.False);
        }

        [UnityTest]
        public IEnumerator DiceRevealShowsFacesThenTheResult()
        {
            yield return StartPlaying();
            ctx.Spawner.Spawn(grunt, Arc, new Vector3(0, 0, 4), health: 100);
            hand.Toggle(0);
            hand.Toggle(1);
            ctx.Hand.ForceNextDice(new[] { 6, 2 });
            hand.Cast();
            yield return null;
            var dice = Find<DiceRevealView>();
            Assert.That(dice.panel.activeSelf);
            Assert.That(dice.faces.text, Is.EqualTo("[ ? ]   [ ? ]"));
            Assert.That(dice.result.text, Is.EqualTo("Rolling to match #6"));
            ctx.Clock.Advance(0.5f);
            yield return null;
            Assert.That(dice.faces.text, Does.Contain("[ 6 ]").And.Contain("[ 2 ]"));
            Assert.That(dice.result.text, Does.StartWith("Full Field · 1 targets"));
            Assert.That(Find<PopupLayer>().Texts, Has.Member("Full Field · 1 targets"));
            ctx.Clock.Advance(3f);
            yield return null;
            Assert.That(dice.panel.activeSelf, Is.False, "hidden a while after the reveal");
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
            hand.Toggle(2);
            hand.Cast();
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
            Assert.That(texts, Has.Member("+13"), "contact charge while a card is missing");
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

        // The real EventSystem path: a click on a CardView selects it, a click on Cast plays the selection.
        [UnityTest]
        public IEnumerator PointerClicksSelectAndCast()
        {
            yield return StartPlaying();
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(hand.slots[2].gameObject, data, ExecuteEvents.pointerClickHandler);
            Assert.That(hand.Selection.Count, Is.EqualTo(1));
            yield return null;
            Assert.That(hand.castButton.interactable);
            ExecuteEvents.Execute(hand.castButton.gameObject, data, ExecuteEvents.pointerClickHandler);
            Assert.That(ctx.Hand.Count, Is.EqualTo(4), "cast");
            Assert.That(ctx.Casts.PendingBatch, Is.Not.Null);
        }
    }
}
