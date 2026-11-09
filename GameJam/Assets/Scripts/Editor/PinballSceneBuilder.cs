using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds Assets/Scenes/Pinball.unity from primitives. Board-local: X = width, Z = up the table.
public static class PinballSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Pinball.unity";

    [MenuItem("LuckyPinball/Build Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var board = new GameObject("Board").transform;
        board.rotation = Quaternion.Euler(-6.5f, 0f, 0f); // +Z end is higher

        Box("Floor", board, new Vector3(0f, -0.1f, 0.5f), new Vector3(10.6f, 0.2f, 24f), 0f, Hud.BoardFloor);
        // Invisible ceiling so bumper kicks cannot throw the ball off the table.
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Glass";
        ceiling.transform.SetParent(board, false);
        ceiling.transform.localPosition = new Vector3(0f, 1.2f, 0.5f);
        ceiling.transform.localScale = new Vector3(10.6f, 0.1f, 24f);
        Object.DestroyImmediate(ceiling.GetComponent<Renderer>());

        var wallColor = Hud.BumperRing; // mockup wall grey
        Box("WallL", board, new Vector3(-5.15f, 0.5f, -2f), new Vector3(0.3f, 1f, 18f), 0f, wallColor);
        Box("WallR", board, new Vector3(5.15f, 0.5f, -2f), new Vector3(0.3f, 1f, 18f), 0f, wallColor);

        // Curved top: half circle, radius 5, centered at z = 7.
        const int segs = 14; const float r = 5.15f;
        for (int i = 0; i < segs; i++)
        {
            float t = (i + 0.5f) / segs * Mathf.PI;
            var pos = new Vector3(Mathf.Cos(t) * r, 0.5f, 7f + Mathf.Sin(t) * r);
            float len = 2f * r * Mathf.Sin(Mathf.PI / segs / 2f) + 0.15f;
            Box("Arc" + i, board, pos, new Vector3(len, 1f, 0.3f), -(t * Mathf.Rad2Deg + 90f), wallColor);
        }

        SpringBumperSetup.AddToBoard(board);

        // Inlane guides from side walls to flipper pivots.
        Seg("GuideL", board, new Vector2(-5f, -4.5f), new Vector2(-2.4f, -7.4f), wallColor);
        Seg("GuideR", board, new Vector2(5f, -4.5f), new Vector2(2.4f, -7.4f), wallColor);

        Flipper(board, true, new Vector3(-2.3f, 0.25f, -7.5f));
        Flipper(board, false, new Vector3(2.3f, 0.25f, -7.5f));

        var drain = new GameObject("Drain");
        drain.transform.SetParent(board, false);
        drain.transform.localPosition = new Vector3(0f, 0.5f, -11f);
        var dc = drain.AddComponent<BoxCollider>();
        dc.isTrigger = true;
        dc.size = new Vector3(10.6f, 2f, 1f);
        drain.AddComponent<Drain>();

        // Ball
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball";
        ball.tag = "Player";
        ball.transform.localScale = Vector3.one * 0.5f;
        ball.transform.position = board.TransformPoint(new Vector3(0f, 0.3f, 10.5f));
        SetColor(ball, Color.white);
        var bmat = new PhysicsMaterial("Ball") { bounciness = 0.5f, dynamicFriction = 0.05f, staticFriction = 0.05f,
            bounceCombine = PhysicsMaterialCombine.Maximum, frictionCombine = PhysicsMaterialCombine.Minimum };
        AssetDatabase.CreateAsset(bmat, "Assets/Scenes/BallPhysics.physicMaterial");
        ball.GetComponent<Collider>().sharedMaterial = bmat;
        var rb = ball.AddComponent<Rigidbody>();
        rb.mass = 0.1f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.board = board;
        gm.ball = rb;

        var cam = Camera.main.transform;
        // Narrow FOV frames the board as large as it can be between the HUD's top bar and bottom bar,
        // with room for the side panels (found by searching camera placements against the HUD layout).
        cam.position = new Vector3(0f, 48f, -48f);
        cam.LookAt(new Vector3(0f, 0f, -0.5f));
        Camera.main.fieldOfView = 25f;
        Camera.main.backgroundColor = Hud.Background;
        Camera.main.clearFlags = CameraClearFlags.SolidColor;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("Pinball scene built: " + ScenePath);
    }

    static void Flipper(Transform board, bool left, Vector3 pivotPos)
    {
        var pivot = new GameObject(left ? "FlipperL" : "FlipperR");
        pivot.transform.SetParent(board, false);
        pivot.transform.localPosition = pivotPos;
        var f = pivot.AddComponent<Flipper>();
        f.isLeft = left;
        f.restAngle = left ? 25f : -25f;
        f.upAngle = left ? -25f : 25f;
        pivot.transform.localRotation = Quaternion.Euler(0f, f.restAngle, 0f);

        const float len = 1.9f;
        Box("Bat", pivot.transform, new Vector3(left ? len / 2f : -len / 2f, 0f, 0f), new Vector3(len, 0.5f, 0.35f), 0f, Hud.Yellow);

        var rb = pivot.GetComponent<Rigidbody>(); // added by Flipper's RequireComponent
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    static void Seg(string name, Transform parent, Vector2 a, Vector2 b, Color c)
    {
        var d = b - a;
        var mid = (a + b) / 2f;
        Box(name, parent, new Vector3(mid.x, 0.5f, mid.y), new Vector3(d.magnitude, 1f, 0.3f), Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg, c);
    }

    static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, float yRot, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
        go.transform.localScale = scale;
        SetColor(go, c);
        return go;
    }

    static void SetColor(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        string path = "Assets/Scenes/Mat_" + ColorUtility.ToHtmlStringRGBA(c) + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(r.sharedMaterial) { color = c };
            AssetDatabase.CreateAsset(m, path);
        }
        r.sharedMaterial = m;
    }
}
