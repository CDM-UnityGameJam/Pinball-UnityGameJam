using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class MultiballChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    [MenuItem("LuckyPinball/Luck Number/Check Multiball Integration (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new Exception("Enter Play Mode first");
        var original = GameManager.Instance; var hud = Hud.Instance;
        var rng = UnityEngine.Random.state; float scale = Time.timeScale, step = Time.fixedDeltaTime;
        var gravity = Physics.gravity;
        try {
            Hud.Instance = null;
            using (var f = new LuckPrototypeCheck.Fixture()) {
                var gm = f.gm;
                var ball = f.Ball(); gm.ball = ball;
                typeof(GameManager).GetField("ballTemplate", Private).SetValue(gm, ball);
                ((List<Rigidbody>)typeof(GameManager).GetField("balls", Private).GetValue(gm)).Add(ball);
                gm.ballOnMultiKill = true;
                f.Hand(new Card(1, Card.Equipment.Arc));
                var direct = f.Enemy(Card.Equipment.Arc, hp:4);
                f.Enemy(Card.Equipment.Spark, x:.5f, hp:4);
                Check(direct.TryBallHit(ball), "collision accepted");
                Check(gm.LastEventKills == 2 && gm.BallCount == 2, "direct and enchantment kills share one reward");
                // Independent later collision: its burn can provide the second kill later.
                f.Hand(new Card(1, Card.Equipment.Spark), new Card(2, Card.Equipment.Spark));
                var hit = f.Enemy(Card.Equipment.Arc, x:10, hp:4);
                f.Enemy(Card.Equipment.Shockwave, x:10.5f, hp:1);
                hit.TryBallHit(ball);
                Check(gm.BallCount == 2, "single kill grants no ball");
                gm.TickGameplay(.5f);
                Check(gm.LastEventKills == 2 && gm.BallCount == 2, "burn completes collision record, reward queued");
                Check(!gm.AddBall(), "two live plus pending reward reaches cap");
                gm.maxBalls = 99;
                Check(!gm.AddBall(), "hard three-ball cap survives inspector override");
                gm.maxBalls = 3;
                gm.StopAllCoroutines();
                typeof(GameManager).GetField("pendingBalls", Private).SetValue(gm, 0);
                typeof(GameManager).GetField("launchingBalls", Private).SetValue(gm, false);
                var batch = new DiceBatch(new[]{new Card(6, Card.Equipment.Arc)}, new[]{6}, 0,
                    new List<EnemySnapshot>(), gm.luck);
                for (int i=0; i<5; i++) gm.OnNormalKill(batch);
                Check(batch.Kills == 5 && gm.BallCount == 3, "five batch kills reward exactly one ball");
                // Free a slot: later kills in this batch must not produce another reward.
                gm.OnDrain(ball);
                int before = gm.BallCount;
                gm.OnNormalKill(batch);
                Check(gm.BallCount == before, "same batch never rewards again after drain");
            }
            Debug.Log("MULTIBALL INTEGRATION PASSED: collision+enchantment, delayed burn, once per batch, pending cap and hard cap.");
        } finally { GameManager.Instance=original; Hud.Instance=hud; UnityEngine.Random.state=rng;
            Time.timeScale=scale; Time.fixedDeltaTime=step; Physics.gravity=gravity; }
    }
}
