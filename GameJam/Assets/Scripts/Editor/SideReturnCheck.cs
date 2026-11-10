using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SideReturnCheck
{
    static void Check(bool value, string reason) { if (!value) throw new Exception("Side return: " + reason); }

    [MenuItem("LuckyPinball/Check Side Returns (Play Mode)")]
    public static void Run()
    {
        Check(Application.isPlaying, "enter Play Mode first");
        var gm = GameManager.Instance;
        var lanes = gm.board.GetComponentsInChildren<SideReturnLane>();
        Check(lanes.Length == 2 && gm.board.GetComponentsInChildren<ReturnBand>().Length == 0, "two lanes and no active band");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var stateField = typeof(GameManager).GetField("state", flags);
        var pauseField = typeof(GameManager).GetField("paused", flags);
        object savedState = stateField.GetValue(gm), savedPause = pauseField.GetValue(gm);
        var balls = new GameObject[2];
        var blocker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blocker.tag = "Player";
        blocker.transform.position = new Vector3(1000, 1000, 1000);
        try
        {
            stateField.SetValue(gm, Enum.Parse(stateField.FieldType, "Playing"));
            pauseField.SetValue(gm, false);
            long score = gm.Score; int energy = gm.Energy;
            for (int i = 0; i < lanes.Length; i++)
            {
                var lane = lanes[i];
                balls[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                balls[i].transform.localScale = Vector3.one * .5f;
                balls[i].tag = "Player";
                var body = balls[i].AddComponent<Rigidbody>();
                body.useGravity = true;
                body.position = gm.board.TransformPoint(new Vector3(0, .3f, -2));
                Physics.SyncTransforms();
                Check(!lane.TryCapture(balls[i].GetComponent<Collider>()), "central ball must not be captured");
                var entrance = lane.GetComponent<BoxCollider>().center; entrance.x -= lane.side * .2f;
                body.position = lane.transform.TransformPoint(entrance);
                Physics.SyncTransforms();
                Check(lane.TryCapture(balls[i].GetComponent<Collider>()), "side entrance captures ball");
                Check(body.isKinematic && !body.detectCollisions, "transport isolates combat collisions");
            }
            Check(balls[0].GetComponent<SideReturnBall>().InTransit && balls[1].GetComponent<SideReturnBall>().InTransit,
                "multiple balls can travel independently");
            // Probe both ends, every section and both contact edges of each diagonal.
            foreach (var lane in lanes)
            {
                var trigger = lane.GetComponent<BoxCollider>();
                for (int segment = 0; segment <= 12; segment++)
                foreach (float edge in new[] { -1f, 0f, 1f })
                {
                    var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    try
                    {
                        probe.tag = "Player";
                        probe.transform.localScale = gm.ball.transform.lossyScale;
                        probe.GetComponent<SphereCollider>().radius = gm.ball.GetComponent<SphereCollider>().radius;
                        float radius = probe.GetComponent<SphereCollider>().radius * probe.transform.lossyScale.x;
                        var local = trigger.center + new Vector3(
                            Mathf.Lerp(-trigger.size.x * .5f, trigger.size.x * .5f, segment / 12f),
                            0f, edge * (trigger.size.z * .5f + radius * .8f));
                        probe.transform.position = lane.transform.TransformPoint(local);
                        probe.AddComponent<Rigidbody>().useGravity = false;
                        Physics.SyncTransforms();
                        Check(lane.TryCapture(probe.GetComponent<Collider>()),
                            "full guide coverage " + lane.name + " section=" + segment + " edge=" + edge);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(probe); lane.Advance(0); }
                }
            }
            var first = balls[0].GetComponent<Rigidbody>();
            var position = first.position;
            pauseField.SetValue(gm, true);
            lanes[0].Advance(1f);
            Check(first.position == position, "pause freezes transport");
            pauseField.SetValue(gm, false);
            blocker.transform.position = gm.board.TransformPoint(lanes[0].outlet);
            Physics.SyncTransforms();
            lanes[0].Advance(5f);
            Check(first.isKinematic && balls[0].GetComponent<SideReturnBall>().InTransit, "occupied outlet holds ball");
            blocker.transform.position = new Vector3(1000, 1000, 1000);
            Physics.SyncTransforms();
            for (int i = 0; i < lanes.Length; i++)
            {
                lanes[i].Advance(5f);
                var body = balls[i].GetComponent<Rigidbody>();
                Check(!body.isKinematic && body.detectCollisions && body.useGravity && !balls[i].GetComponent<SideReturnBall>().InTransit,
                    "release restores dynamic physics");
                Check(Vector3.Distance(body.position, gm.board.TransformPoint(lanes[i].outlet)) < .01f, "correct outlet");
                Check(Vector3.Dot(body.linearVelocity, gm.board.right) * lanes[i].side < 0f && Vector3.Dot(body.linearVelocity, gm.board.forward) > 0,
                    "release aims inward and upward");
                body.position = lanes[i].transform.TransformPoint(lanes[i].GetComponent<BoxCollider>().center);
                Physics.SyncTransforms();
                Check(!lanes[i].TryCapture(balls[i].GetComponent<Collider>()), "reentry cooldown prevents loop");
                Check(lanes[i].BlocksSpawn(new Vector3(lanes[i].outlet.x, 0, 9.5f), 1.05f, true, -5f), "descent reserves outlet");
            }
            Check(gm.Score == score && gm.Energy == energy, "transport grants no reward");
            File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/side-return-check.txt"),
                "PASS: two lanes; band disabled; central passthrough; 78 full-guide endpoint/section/edge captures; independent multiball; pause; blocked-outlet wait; physics restoration; inward/upward launch; cooldown; spawn outlet reservation; no rewards.\n");
            Debug.Log("Side return checks passed.");
        }
        finally
        {
            foreach (var ball in balls) if (ball) UnityEngine.Object.DestroyImmediate(ball);
            UnityEngine.Object.DestroyImmediate(blocker);
            // Remove destroyed test trips before returning control to ordinary gameplay.
            foreach (var lane in lanes) lane.Advance(0);
            stateField.SetValue(gm, savedState); pauseField.SetValue(gm, savedPause);
        }
    }
}
