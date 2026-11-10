using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OuterRingSetup
{
    [MenuItem("LuckyPinball/Install Outer Ring Relay")]
    public static void Install()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Pinball.unity") scene = EditorSceneManager.OpenScene("Assets/Scenes/Pinball.unity");
        ApplyToScene(Object.FindAnyObjectByType<GameManager>());
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Outer ring relay saved: raised inner field, physical lowered gutter and crossed exits.");
    }

    public static void ApplyToScene(GameManager gm)
    {
        SideReturnSetup.ApplyToScene(gm);
        var board = gm.board;
        var existing = board.Find("OuterRingTerrain");
        OuterRingSurface surface;
        if (existing) surface = existing.GetComponent<OuterRingSurface>();
        else
        {
            var go = new GameObject("OuterRingTerrain", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.transform.SetParent(board, false);
            surface = go.AddComponent<OuterRingSurface>();
        }
        surface.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Mat_2D303CFF.mat");
        surface.gutterMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Mat_OuterGutter.mat");
        surface.fieldHeight = .32f;
        surface.entryRampLength = 3f;
        surface.fieldArcHeight = 3.6f;
        surface.flatSides = true;
        surface.lipHeight = 0f;
        surface.lipWidth = .6f;
        surface.upperLipExtraHeight = 0f;
        surface.upperLipStartZ = 1f;
        surface.upperLipFullZ = 5f;
        surface.topTransitionStartZ = 7f;
        surface.topTransitionEndZ = 7.6f;
        surface.Rebuild();
        var sphere = gm.ball.GetComponent<SphereCollider>();
        var ballScale = gm.ball.transform.lossyScale;
        float ballRadius = sphere.radius * Mathf.Max(Mathf.Abs(ballScale.x), Mathf.Abs(ballScale.y), Mathf.Abs(ballScale.z));
        gm.spawning.ballRadius = ballRadius;
        gm.launchLocal.y = Mathf.Max(gm.launchLocal.y, ballRadius + .03f);
        var glass = board.Find("Glass");
        var glassPosition = glass.localPosition;
        glassPosition.y = Mathf.Max(glassPosition.y, surface.MaximumHeight + ballRadius * 2f + .3f + glass.localScale.y * .5f);
        glass.localPosition = glassPosition;
        var wallMaterial = board.Find("WallL").GetComponent<Renderer>().sharedMaterial;
        for (int side = -1; side <= 1; side += 2)
        {
            string suffix = side < 0 ? "L" : "R";
            var wall = board.Find("Wall" + suffix);
            wall.localPosition = new Vector3(side * 8.15f, .5f, 1.75f);
            wall.localScale = new Vector3(.3f, 1f, 10.5f);
            var lower = board.Find("WallLower" + suffix);
            lower.localPosition = new Vector3(side * 8.15f, .5f, -7.9f);
            lower.localScale = new Vector3(.3f, 1f, 6.2f);
            var lane = board.Find("SideReturn" + suffix).GetComponent<SideReturnLane>();
            lane.outlet = new Vector3(side * 5.45f, surface.HeightAt(side * 5.45f, 8.5f) + ballRadius + .04f, 8.5f);
            lane.exitDirection = new Vector2(-side * .84f, .54f);
            lane.path = new[] { new Vector3(side * 8.9f, .3f, -4.15f), new Vector3(side * 8.9f, .3f, 6.6f),
                new Vector3(side * 8.9f, 1.4f, 8.5f), new Vector3(lane.outlet.x, 1.4f, lane.outlet.z), lane.outlet };
            // Leave the inner section beside the flipper free of powered transport.
            var guide = board.Find("Guide" + suffix);
            lane.transform.localPosition = guide.localPosition;
            lane.transform.localRotation = guide.localRotation;
            lane.transform.localScale = Vector3.one;
            var trigger = lane.GetComponent<BoxCollider>();
            float innerClearance = guide.localScale.x * (2f / 5f);
            float outwardSign = side;
            trigger.center = new Vector3(outwardSign * innerClearance * .5f, .15f, 0f);
            trigger.size = new Vector3(guide.localScale.x - innerClearance, 1.7f, .38f);
            trigger.isTrigger = true;
            var poweredMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Mat_PoweredReturn.mat");
            guide.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            var endA = board.InverseTransformPoint(guide.TransformPoint(new Vector3(-.5f, .52f, 0)));
            var endB = board.InverseTransformPoint(guide.TransformPoint(new Vector3(.5f, .52f, 0)));
            var outerEnd = Mathf.Abs(endA.x) > Mathf.Abs(endB.x) ? endA : endB;
            var innerEnd = Mathf.Abs(endA.x) > Mathf.Abs(endB.x) ? endB : endA;
            innerEnd = Vector3.MoveTowards(innerEnd, outerEnd, innerClearance);
            // A capture anywhere on the guide travels outward before joining the side pipe.
            var route = new System.Collections.Generic.List<Vector3> { outerEnd };
            route.AddRange(lane.path);
            lane.path = route.ToArray();
            var strip = lane.GetComponent<LineRenderer>();
            strip.sharedMaterial = poweredMaterial;
            strip.widthMultiplier = .38f;
            strip.positionCount = lane.path.Length + 1;
            strip.SetPosition(0, lane.transform.InverseTransformPoint(board.TransformPoint(innerEnd)));
            for (int i = 0; i < lane.path.Length; i++)
                strip.SetPosition(i + 1, lane.transform.InverseTransformPoint(board.TransformPoint(lane.path[i])));
            var spring = board.Find("Spring" + suffix).GetComponent<SpringBumper>();
            spring.transform.localPosition = new Vector3(side * 5.9f, surface.HeightAt(side * 5.9f, 7.8f) + .25f, 7.8f);
            spring.transform.localScale = new Vector3(.9f, .25f, .9f);
            spring.radius = .45f;
            spring.guideAlongGutter = true;
            spring.gutterDirection = new Vector3(side * .12f, 0, -1);
            spring.kickSpeed = 11f;
            // Visible collector rim; collection itself uses the lane's non-solid trigger.
            string rimName = "CollectorRim" + suffix;
            var rim = board.Find(rimName);
            if (!rim) { var go = new GameObject(rimName); rim = go.transform; rim.SetParent(board, false); }
            var line = rim.GetComponent<LineRenderer>();
            if (!line) line = rim.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.sharedMaterial = wallMaterial; line.widthMultiplier = .07f;
            rim.gameObject.SetActive(false); // The full guide replaces the old small collector.
        }
        // Spawn only on the upper inner surface. Straight descent and breach timing remain unchanged.
        gm.spawning.boardHalfWidth = surface.fieldHalfWidth;
        gm.spawning.arcHorizontalRadius = surface.fieldHalfWidth;
        gm.spawning.arcRadius = surface.fieldArcHeight;
        gm.spawning.arcCenterZ = surface.arcCenterZ;
        gm.spawning.specialMinZ = Mathf.Max(gm.spawning.specialMinZ, surface.bottomZ + gm.specialEnemy.radius + gm.specialEnemy.wallClearance);
        gm.launchSpeed = Mathf.Max(gm.launchSpeed, 32f); // Full launch must cross the ramp and reach the upper battlefield.
        Physics.SyncTransforms();
    }
}
