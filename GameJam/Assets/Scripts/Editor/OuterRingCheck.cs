using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OuterRingCheck
{
    static void Check(bool value, string message) { if (!value) throw new Exception("Outer ring: " + message); }

    [MenuItem("LuckyPinball/Check Outer Ring Relay (Play Mode)")]
    public static void Run()
    {
        Check(Application.isPlaying, "enter Play Mode first");
        SideReturnCheck.Run();
        var gm = GameManager.Instance;
        var surface = gm.board.GetComponentInChildren<OuterRingSurface>();
        float actualRadius = gm.ball.GetComponent<SphereCollider>().radius * gm.ball.transform.lossyScale.x;
        var ceiling = gm.board.Find("Glass");
        Check(ceiling.localPosition.y - ceiling.localScale.y * .5f - surface.MaximumHeight >= actualRadius * 2f + .2f,
            "ceiling clearance must fit the actual scene ball");
        Check(surface && surface.GetComponent<MeshCollider>().sharedMesh, "physical terrain installed");
        Check(Mathf.Abs(surface.LipHeightAt(0f) - .08f) < .001f && Mathf.Abs(surface.LipHeightAt(6f) - .14f) < .001f,
            "upper lip raised while lower lip remains unchanged");
        Check(surface.LipHeightAt(9f) == 0f && Mathf.Abs(surface.HeightAt(2f, 11.55f) - surface.fieldHeight) < .001f,
            "top has no lip or lowered gutter");
        Check(Mathf.Abs(surface.HeightAt(0, 3) - surface.fieldHeight) < .001f && surface.HeightAt(7.45f, 3) < .001f,
            "inner field above side gutter");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var stateField = typeof(GameManager).GetField("state", flags);
        var pauseField = typeof(GameManager).GetField("paused", flags);
        object savedState = stateField.GetValue(gm), savedPause = pauseField.GetValue(gm);
        var scene = SceneManager.CreateScene("Outer ring physical checks", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        try
        {
            stateField.SetValue(gm, Enum.Parse(stateField.FieldType, "Playing")); pauseField.SetValue(gm, false);
            foreach (var source in gm.board.GetComponentsInChildren<Collider>())
            {
                if (!source.enabled || source.isTrigger || source.CompareTag("Player") || source.GetComponentInParent<Enemy>() ||
                    source.GetComponentInParent<Flipper>()) continue;
                var go = new GameObject("Test " + source.name);
                SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                go.transform.localScale = source.transform.lossyScale;
                if (source is BoxCollider box)
                { var c = go.AddComponent<BoxCollider>(); c.center = box.center; c.size = box.size; c.sharedMaterial = box.sharedMaterial; }
                else if (source is MeshCollider mesh)
                { var c = go.AddComponent<MeshCollider>(); c.sharedMesh = mesh.sharedMesh; c.convex = mesh.convex; c.sharedMaterial = mesh.sharedMaterial; }
            }
            var originals = gm.board.GetComponentsInChildren<SideReturnLane>();
            var lanes = new SideReturnLane[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                var go = new GameObject("Test collector"); SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.SetPositionAndRotation(originals[i].transform.position, originals[i].transform.rotation);
                lanes[i] = go.AddComponent<SideReturnLane>();
                lanes[i].side = originals[i].side; lanes[i].path = originals[i].path; lanes[i].outlet = originals[i].outlet;
                var collider = go.GetComponent<BoxCollider>(); var source = originals[i].GetComponent<BoxCollider>();
                collider.center = source.center; collider.size = source.size; collider.isTrigger = true;
            }
            var physics = scene.GetPhysicsScene();
            Rigidbody MakeBall(Vector3 local, Vector3 velocity)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); SceneManager.MoveGameObjectToScene(go, scene);
                go.name = "Physical probe ball"; go.tag = "Player"; go.transform.localScale = gm.ball.transform.lossyScale;
                go.GetComponent<SphereCollider>().radius = gm.ball.GetComponent<SphereCollider>().radius;
                go.transform.position = gm.board.TransformPoint(local);
                go.GetComponent<Collider>().sharedMaterial = gm.ball.GetComponent<Collider>().sharedMaterial;
                var body = go.AddComponent<Rigidbody>(); body.mass = .1f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity = gm.board.TransformDirection(velocity);
                Physics.SyncTransforms(); return body;
            }
            bool ReachCollector(Vector3 local)
            {
                var ball = MakeBall(local, Vector3.zero);
                bool result = false;
                for (int step = 0; step < 1000; step++)
                {
                    physics.Simulate(.01f);
                    if (ball.TryGetComponent<SideReturnBall>(out var state) && state.InTransit) { result = true; break; }
                }
                if (!result) File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/outer-ring-probe.txt"),
                    "Start " + local + " end " + gm.board.InverseTransformPoint(ball.position) + " velocity " + gm.board.InverseTransformDirection(ball.linearVelocity));
                UnityEngine.Object.DestroyImmediate(ball.gameObject); return result;
            }
            foreach (float angle in new[] { -8f, 0f, 8f })
            {
                var velocity = Quaternion.Euler(0, angle, 0) * Vector3.forward * gm.launchSpeed;
                var launched = MakeBall(gm.launchLocal, velocity);
                float maxZ = gm.launchLocal.z;
                for (int step = 0; step < 400; step++)
                {
                    physics.Simulate(.01f);
                    maxZ = Mathf.Max(maxZ, gm.board.InverseTransformPoint(launched.position).z);
                    if (maxZ >= 3f) break;
                }
                Check(maxZ >= 3f, "launch blocked before upper field, angle " + angle + " maxZ " + maxZ);
                UnityEngine.Object.DestroyImmediate(launched.gameObject);
            }
            var paddleShot = MakeBall(new Vector3(0, actualRadius + .03f, -7.5f), new Vector3(0, 0, 22));
            float paddleMax = -7.5f;
            for (int step = 0; step < 300; step++)
            {
                physics.Simulate(.01f);
                paddleMax = Mathf.Max(paddleMax, gm.board.InverseTransformPoint(paddleShot.position).z);
                if (paddleMax >= -3.8f) break;
            }
            Check(paddleMax >= -3.8f, "paddle-area shot cannot cross entry ramp: " + paddleMax);
            UnityEngine.Object.DestroyImmediate(paddleShot.gameObject);
            Check(ReachCollector(new Vector3(-7.45f, actualRadius + .03f, 5.7f)), "left gutter must roll into lower collector by gravity");
            Check(ReachCollector(new Vector3(7.45f, actualRadius + .03f, 5.7f)), "right gutter must roll into lower collector by gravity");
            var topBall = MakeBall(new Vector3(2f, surface.fieldHeight + actualRadius + .03f, 11.55f), Vector3.zero);
            for (int step = 0; step < 100; step++) physics.Simulate(.01f);
            Check(gm.board.InverseTransformPoint(topBall.position).z < 9.5f, "top ball must roll back into battlefield without sticking");
            UnityEngine.Object.DestroyImmediate(topBall.gameObject);
            var transitionBall = MakeBall(new Vector3(7.35f, surface.fieldHeight + actualRadius + .03f, 7.7f), new Vector3(0, 0, -2));
            for (int step = 0; step < 100; step++) physics.Simulate(.01f);
            Check(gm.board.InverseTransformPoint(transitionBall.position).z < 5.5f, "short side transition must remain traversable");
            UnityEngine.Object.DestroyImmediate(transitionBall.gameObject);
            foreach (float side in new[] { -1f, 1f })
            {
                var slowBall = MakeBall(new Vector3(side * 5.85f, surface.fieldHeight + actualRadius + .01f, 3), new Vector3(side * 2f, 0, 0));
                bool turnedInward = false;
                float maximumX = 0f;
                for (int step = 0; step < 80; step++)
                {
                    physics.Simulate(.01f);
                    maximumX = Mathf.Max(maximumX, Mathf.Abs(gm.board.InverseTransformPoint(slowBall.position).x));
                    if (gm.board.InverseTransformDirection(slowBall.linearVelocity).x * side < -.1f) turnedInward = true;
                }
                var remainingVelocity = gm.board.InverseTransformDirection(slowBall.linearVelocity);
                Check(maximumX < surface.fieldHalfWidth && (turnedInward || Mathf.Abs(remainingVelocity.z) > 1f),
                    "gentle contact must stay inside and keep moving: " + side + " maxX=" + maximumX + " velocity=" + remainingVelocity);
                UnityEngine.Object.DestroyImmediate(slowBall.gameObject);
            }
            var lipBall = MakeBall(new Vector3(5.85f, surface.fieldHeight + actualRadius + .03f, 3), new Vector3(9, 0, -.5f));
            bool descended = false;
            for (int i = 0; i < 200; i++)
            {
                physics.Simulate(.01f);
                var local = gm.board.InverseTransformPoint(lipBall.position);
                if (local.x > 7.05f && local.y < actualRadius + .15f) { descended = true; break; }
            }
            Check(descended, "ball crosses rounded lip into lower gutter without attraction");
            UnityEngine.Object.DestroyImmediate(lipBall.gameObject);
            // No generated mesh triangles should face downward.
            foreach (var normal in surface.GetComponent<MeshFilter>().sharedMesh.normals) Check(normal.y > 0f, "terrain winding");
            File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/outer-ring-check.txt"),
                "PASS with actual ball radius=" + actualRadius + ": flat top without lip/gutter; top ball rolls back into field; short side transition traversable; unchanged lower lip; ceiling clearance; side collection; launch/ramp and return regression.\n");
            Debug.Log("Outer ring physical checks passed.");
        }
        finally
        {
            stateField.SetValue(gm, savedState); pauseField.SetValue(gm, savedPause);
            SceneManager.UnloadSceneAsync(scene);
        }
    }
}
