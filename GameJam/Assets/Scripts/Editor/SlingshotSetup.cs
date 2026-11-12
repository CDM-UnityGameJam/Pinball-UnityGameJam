using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SlingshotSetup
{
    // Turns the long side walls into kickers and switches off the powered side return lanes.
    // The angled guides above the flippers stay plain walls so balls roll onto the flippers.
    [MenuItem("LuckyPinball/Replace Side Returns With Side-Wall Kickers")]
    public static void Install()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Pinball.unity");
        var board = Object.FindAnyObjectByType<GameManager>().board;
        var wallMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Mat_A9ADB8FF.mat");
        // Same yellow as the spring pads, so every kicker on the table reads alike.
        var kickerMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Mat_F2D33AFF.mat");
        foreach (var suffix in new[] { "L", "R" })
        {
            var lane = board.Find("SideReturn" + suffix);
            if (lane) lane.gameObject.SetActive(false); // hides the brown track and stops capture; re-enable to revert

            var guide = Find(board, "Guide" + suffix);
            var old = guide.GetComponent<Slingshot>();
            if (old) Object.DestroyImmediate(old);
            if (wallMaterial) guide.GetComponent<Renderer>().sharedMaterial = wallMaterial;

            var wall = Find(board, "Wall" + suffix);
            // Close the old lane entrance: stretch the wall down to the guide's outer end so balls can't slip into the gutter.
            var endA = board.InverseTransformPoint(guide.TransformPoint(new Vector3(-.5f, 0f, 0f)));
            var endB = board.InverseTransformPoint(guide.TransformPoint(new Vector3(.5f, 0f, 0f)));
            float guideOuterZ = Mathf.Abs(endA.x) > Mathf.Abs(endB.x) ? endA.z : endB.z;
            float top = wall.localPosition.z + wall.localScale.z * .5f;
            float bottom = Mathf.Min(wall.localPosition.z - wall.localScale.z * .5f, guideOuterZ - .2f);
            wall.localPosition = new Vector3(wall.localPosition.x, wall.localPosition.y, (top + bottom) * .5f);
            wall.localScale = new Vector3(wall.localScale.x, wall.localScale.y, top - bottom);
            var kicker = wall.GetComponent<Slingshot>();
            if (!kicker) kicker = wall.gameObject.AddComponent<Slingshot>();
            kicker.innerFreeFraction = 0f; // the whole wall kicks
            kicker.kickSpeed = 28f;
            kicker.upwardBias = 2.5f;
            if (kickerMaterial) wall.GetComponent<Renderer>().sharedMaterial = kickerMaterial;
        }
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Side-wall kickers installed on WallL/WallR; side return lanes disabled.");
    }

    static Transform Find(Transform board, string name)
    {
        var t = board.Find(name);
        if (!t) throw new System.InvalidOperationException(name + " not found on the board.");
        return t;
    }
}
