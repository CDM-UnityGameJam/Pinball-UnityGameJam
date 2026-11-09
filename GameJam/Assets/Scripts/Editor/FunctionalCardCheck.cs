using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class FunctionalCardCheck
{
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception("Functional card check failed: " + message);
    }

    [MenuItem("LuckyPinball/Check Functional Card Rules")]
    public static void RunRules()
    {
        Check(Card.RollFunctional(false, 0f, 1f, 0f)?.kind == Card.Kind.MatchBoost, "match roll");
        Check(Card.RollFunctional(false, 1f, 0f, 0f)?.kind == Card.Kind.ExtraBall, "ball roll");
        Check(Card.RollFunctional(false, 0f, 0f, 0.49f)?.kind == Card.Kind.MatchBoost, "tie first half");
        Check(Card.RollFunctional(false, 0f, 0f, 0.5f)?.kind == Card.Kind.ExtraBall, "tie second half");
        Check(Card.RollFunctional(false, 0.03f, 0.03f, 0f) == null, "strict 3 percent boundary");
        Check(Card.RollFunctional(true, 0f, 1f, 0f) == null, "opening excludes boost");
        Check(Card.RollFunctional(true, 0f, 0f, 0f)?.kind == Card.Kind.ExtraBall, "opening permits extra ball");
        Check(Card.RollFunctional(false, 0f, 0f, 0f, 0f, 0f) == null, "zero disables both cards");
        Check(Card.RollFunctional(false, 1f, 0f, 0f, 1f, 0f)?.kind == Card.Kind.MatchBoost, "independent match chance at 100 percent");
        Check(Card.RollFunctional(false, 0f, 1f, 0f, 0f, 1f)?.kind == Card.Kind.ExtraBall, "independent ball chance at 100 percent");
        Check(Card.RollFunctional(true, 0f, 0f, 0f, 1f, 0f) == null, "opening still excludes 100 percent boost");
        var hand = new Card?[] { new Card(Card.Kind.MatchBoost), new Card(Card.Kind.ExtraBall), null, null, null };
        Check(Card.Draw(37, hand, new List<int> { 74 }).Apply(37) == 74, "function cards do not cover number targets");
        Debug.Log("Functional card rules passed");
    }

    // Runs on a disposable Play Mode run, replacing its hand and exercising life transitions.
    [MenuItem("LuckyPinball/Check Functional Card Gameplay (Play Mode)")]
    public static void RunGameplay()
    {
        Check(Application.isPlaying, "enter Play Mode first");
        var gm = GameManager.Instance;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(GameManager);
        var hand = (Card?[])type.GetField("hand", flags).GetValue(gm);
        var state = type.GetField("state", flags);
        state.SetValue(gm, Enum.Parse(state.FieldType, "Waiting"));
        type.GetField("lives", flags).SetValue(gm, 3);
        gm.SetPaused(false);
        hand[0] = new Card('x', 2);
        gm.UseCard(0);
        hand[0] = new Card(Card.Kind.MatchBoost);
        gm.UseCard(0);
        hand[0] = new Card(Card.Kind.MatchBoost);
        gm.UseCard(0);
        Check(gm.CurrentNumber == 74 && gm.MatchBoostLeft == 20f, "stack duration and preserve number");
        hand[0] = new Card(Card.Kind.ExtraBall);
        gm.UseCard(0);
        Check(gm.QueuedBalls == 1, "queue during wait");
        gm.SetPaused(true);
        hand[0] = new Card(Card.Kind.MatchBoost);
        Check(!gm.UseCard(0), "paused card rejected");
        type.GetMethod("Update", flags).Invoke(gm, null);
        Check(gm.MatchBoostLeft == 20f && gm.QueuedBalls == 1, "pause freezes effects and queue");
        gm.SetPaused(false);
        type.GetMethod("Launch", flags).Invoke(gm, null);
        Check((float)type.GetField("queuedLaunchTimer", flags).GetValue(gm) == 1f, "one second launch spacing");
        hand[0] = new Card(Card.Kind.ExtraBall);
        gm.UseCard(0);
        Check(gm.BallCount == 2 && gm.CurrentNumber == 74, "immediate additional ball");
        // Extra cards can exceed three balls, with no hidden cap.
        for (int i = 0; i < 3; i++)
        {
            hand[0] = new Card(Card.Kind.ExtraBall);
            gm.UseCard(0);
        }
        Check(gm.BallCount == 5, "unlimited additional balls");
        // Complete one queued launch, retaining another to test cancellation on drain.
        type.GetField("queuedBalls", flags).SetValue(gm, 2);
        type.GetField("queuedLaunchTimer", flags).SetValue(gm, 0f);
        type.GetMethod("Update", flags).Invoke(gm, null);
        Check(gm.BallCount == 6 && gm.QueuedBalls == 1 &&
            (float)type.GetField("queuedLaunchTimer", flags).GetValue(gm) == 1f, "queued launch resets one second spacing");
        while (gm.BallCount > 2) gm.OnDrain(gm.Balls[0]);
        var first = gm.Balls[0];
        gm.OnDrain(first);
        gm.OnDrain(first);
        Check(gm.BallCount == 1 && gm.Lives == 3 && gm.HasMatchBoost, "partial drain and duplicate drain");
        gm.LoseLife();
        Check(gm.Lives == 2 && gm.HasMatchBoost && gm.QueuedBalls == 1, "breach preserves effect and queue");

        var normal = gm.SpawnEnemy(Enemy.Kind.Normal, 40, new Vector3(-3, 0, 5));
        var special = gm.SpawnEnemy(Enemy.Kind.Special, 111, new Vector3(3, 0, 5));
        Check(gm.Matches(normal) && !gm.Matches(special), "boost excludes special");
        var hit = typeof(Enemy).GetMethod("ApplyHit", flags);
        hit.Invoke(special, new object[] { false });
        Check(special.hp == 5, "special immune to nonmatching hits and pulses");
        hit.Invoke(normal, new object[] { true });
        Check(normal.hp == 0, "boost kills normal");
        hand[0] = Card.For(111);
        gm.UseCard(0);
        Check(gm.Matches(special), "exact special match allowed");
        hit.Invoke(special, new object[] { gm.Matches(special) });
        Check(special.hp == 0, "exact special match kills");

        gm.OnDrain(gm.Balls[0]);
        Check(gm.IsWaiting && gm.Lives == 1 && !gm.HasMatchBoost && gm.QueuedBalls == 0 && gm.BallCount == 1,
            "last drain clears effects and queued balls, then relaunch wait");
        type.GetMethod("Launch", flags).Invoke(gm, null);
        gm.OnDrain(gm.Balls[0]);
        Check(gm.IsGameOver && gm.BallCount == 0, "final life ends run");
        Debug.Log("Functional card gameplay passed");
    }
}
