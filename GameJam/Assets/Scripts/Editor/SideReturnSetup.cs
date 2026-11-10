using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SideReturnSetup
{
    public const float MainHalfWidth = 8f;
    [MenuItem("LuckyPinball/Install Side Return Lanes")]
    public static void Install()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Pinball.unity") scene = EditorSceneManager.OpenScene("Assets/Scenes/Pinball.unity");
        var gm = Object.FindAnyObjectByType<GameManager>();
        if (gm.board.GetComponentInChildren<OuterRingSurface>()) OuterRingSetup.ApplyToScene(gm);
        else ApplyToScene(gm);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Side return lanes saved; center return band disabled.");
    }

    public static void ApplyToScene(GameManager gm)
    {
        var board = gm.board;
        float expansion = MainHalfWidth - 6.3f;
        float X(float x) => Mathf.Sign(x) * (Mathf.Abs(x) + expansion);
        float totalWidth = 16.8f + expansion * 2f;
        if (board.Find("ReturnBand")) board.Find("ReturnBand").gameObject.SetActive(false);
        var wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Mat_A9ADB8FF.mat");
        var trackMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Mat_ReturnBand.mat");
        Transform Box(string name, Vector3 position, Vector3 size, float yaw = 0f)
        {
            var t = board.Find(name);
            if (!t) { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; t = go.transform; t.SetParent(board, false); }
            t.localPosition = position; t.localScale = size; t.localRotation = Quaternion.Euler(0, yaw, 0);
            t.GetComponent<Renderer>().sharedMaterial = wallMat;
            return t;
        }
        void Segment(string name, Vector2 a, Vector2 b)
        {
            var d = b - a; var mid = (a + b) * .5f;
            Box(name, new Vector3(mid.x, .5f, mid.y), new Vector3(d.magnitude, 1, .3f), Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg);
        }
        board.Find("Floor").localScale = new Vector3(totalWidth, .2f, 24);
        board.Find("Glass").localScale = new Vector3(totalWidth, .1f, 24);
        board.Find("Drain").GetComponent<BoxCollider>().size = new Vector3(totalWidth, 2, 1);
        for (int side = -1; side <= 1; side += 2)
        {
            string suffix = side < 0 ? "L" : "R";
            Box("Wall" + suffix, new Vector3(side * X(6.45f), .5f, 2.9f), new Vector3(.3f, 1, 8.2f));
            Box("WallLower" + suffix, new Vector3(side * X(6.45f), .5f, -6.9f), new Vector3(.3f, 1, 8.2f));
            Box("ReturnOuterRail" + suffix, new Vector3(side * X(8.1f), .5f, .5f), new Vector3(.3f, 1, 24));
            Segment("Guide" + suffix, new Vector2(side * MainHalfWidth, -4.5f), new Vector2(side * 2.4f, -7.4f));
            var spring = board.Find("Spring" + suffix);
            if (spring) spring.localPosition = new Vector3(side * X(5.1f), .25f, 9.5f);
            var existing = board.Find("SideReturn" + suffix);
            var lane = existing ? existing.GetComponent<SideReturnLane>() : new GameObject("SideReturn" + suffix).AddComponent<SideReturnLane>();
            if (!existing) lane.transform.SetParent(board, false);
            lane.transform.localPosition = Vector3.zero;
            lane.transform.localRotation = Quaternion.identity;
            lane.transform.localScale = Vector3.one;
            lane.side = side;
            lane.outlet = new Vector3(side * X(4.3f), .3f, 8.3f);
            lane.path = new[] { new Vector3(side * X(7.2f), .3f, -2), new Vector3(side * X(7.2f), .3f, 6.6f),
                new Vector3(side * X(7.2f), 1.4f, 8.3f), new Vector3(side * X(4.3f), 1.4f, 8.3f), lane.outlet };
            var trigger = lane.GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(side * X(6.1f), .4f, -2f);
            trigger.size = new Vector3(1.6f, 1.4f, 1.6f);
            var strip = lane.GetComponent<LineRenderer>();
            if (!strip) strip = lane.gameObject.AddComponent<LineRenderer>();
            strip.sharedMaterial = trackMat; strip.useWorldSpace = false; strip.widthMultiplier = 1.05f;
            strip.numCornerVertices = 8; strip.numCapVertices = 6;
            strip.positionCount = lane.path.Length + 1;
            strip.SetPosition(0, new Vector3(side * X(5.9f), .3f, -2));
            for (int i = 0; i < lane.path.Length; i++) strip.SetPosition(i + 1, lane.path[i]);
        }
        Box("ReturnOuterTop", new Vector3(0, .5f, 12.35f), new Vector3(totalWidth - .3f, 1, .3f));
        for (int i = 0; i < 14; i++)
        {
            float a = i * Mathf.PI / 14, b = (i + 1) * Mathf.PI / 14;
            Segment("Arc" + i, new Vector2(Mathf.Cos(a) * X(6.45f), 7 + Mathf.Sin(a) * 5.15f),
                new Vector2(Mathf.Cos(b) * X(6.45f), 7 + Mathf.Sin(b) * 5.15f));
            var t = board.Find("Arc" + i); var scale = t.localScale; scale.x += .15f; t.localScale = scale;
        }
        gm.spawning.boardHalfWidth = MainHalfWidth;
        gm.spawning.arcHorizontalRadius = MainHalfWidth;
        gm.spawning.dangerLineScale.x = MainHalfWidth * 2f;
        gm.hud.overPlayfield.halfWidth = totalWidth * .5f;
        gm.hud.refreshBoardLabels.dangerLabelX = -X(6.9f);
        Camera.main.fieldOfView = 31f;
        Physics.SyncTransforms();
    }
}
