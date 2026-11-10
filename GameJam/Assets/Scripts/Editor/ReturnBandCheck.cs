using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ReturnBandCheck
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception("Return band: " + message); }

    [MenuItem("LuckyPinball/Check Return Band (Play Mode)")]
    public static void Run()
    {
        Check(Application.isPlaying, "enter Play Mode first");
        var gm = GameManager.Instance;
        var band = gm.board.GetComponentInChildren<ReturnBand>();
        Check(band && band.GetComponent<BoxCollider>().isTrigger, "non-solid band installed");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var stateField = typeof(GameManager).GetField("state", flags);
        var pausedField = typeof(GameManager).GetField("paused", flags);
        var savedState = stateField.GetValue(gm);
        var savedPause = pausedField.GetValue(gm);
        float cooldown = band.cooldown;
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var second = new GameObject("Second ball budget check");
        try
        {
            stateField.SetValue(gm, Enum.Parse(stateField.FieldType, "Playing"));
            pausedField.SetValue(gm, false);
            ball.tag = "Player";
            ball.transform.position = gm.board.TransformPoint(new Vector3(0f, .3f, 0f));
            var body = ball.AddComponent<Rigidbody>();
            body.useGravity = false;
            var budget = ball.AddComponent<BallReturnState>();
            budget.BeginLaunch(2);
            var otherBudget = second.AddComponent<BallReturnState>();
            otherBudget.BeginLaunch(2);
            var collider = ball.GetComponent<Collider>();
            long score = gm.Score;
            int energy = gm.Energy;
            body.linearVelocity = gm.board.forward * 8f;
            Check(!band.TryReturn(collider) && budget.Remaining == 2, "ascending ball must pass freely");
            body.linearVelocity = -gm.board.forward * 8f;
            pausedField.SetValue(gm, true);
            Check(!band.TryReturn(collider) && budget.Remaining == 2, "pause freezes band");
            pausedField.SetValue(gm, false);
            Check(band.TryReturn(collider) && budget.Remaining == 1 && Vector3.Dot(body.linearVelocity, gm.board.forward) > 0,
                "descending ball returns upward and spends exactly one charge");
            body.linearVelocity = -gm.board.forward;
            Check(!band.TryReturn(collider) && budget.Remaining == 1, "cooldown prevents duplicate returns");
            band.cooldown = 0f;
            Check(band.TryReturn(collider) && budget.Remaining == 0, "second return consumes final charge");
            body.linearVelocity = -gm.board.forward;
            Check(!band.TryReturn(collider), "third return must fail");
            Check(otherBudget.Remaining == 2, "multiball budgets independent");
            Check(gm.Score == score && gm.Energy == energy, "band grants no score or energy");
            budget.BeginLaunch(2);
            ball.tag = "Untagged";
            Check(!band.TryReturn(collider) && budget.Remaining == 2, "non-player bodies ignored");
            File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/return-band-check.txt"),
                "PASS: trigger setup; upward return; two-charge limit; upward passthrough; cooldown; pause; independent multiball budgets; no score/energy; non-player ignored.\n");
            Debug.Log("Return band checks passed.");
        }
        finally
        {
            band.cooldown = cooldown;
            stateField.SetValue(gm, savedState);
            pausedField.SetValue(gm, savedPause);
            UnityEngine.Object.DestroyImmediate(ball);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }
}
