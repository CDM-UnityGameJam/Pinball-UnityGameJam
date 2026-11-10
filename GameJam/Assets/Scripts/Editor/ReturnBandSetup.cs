using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ReturnBandSetup
{
    [MenuItem("LuckyPinball/Install Return Band")]
    public static void Install()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Pinball.unity") scene = EditorSceneManager.OpenScene("Assets/Scenes/Pinball.unity");
        var gm = Object.FindAnyObjectByType<GameManager>();
        AddToBoard(gm.board, gm.spawning.boardHalfWidth);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Return band installed and saved: two returns per manual hit.");
    }

    public static void AddToBoard(Transform board, float halfWidth)
    {
        var existing = board.Find("ReturnBand");
        if (existing) { existing.localPosition = new Vector3(0f, .4f, -3.5f); existing.GetComponent<ReturnBand>().returnSpeed = 22f; return; }
        var go = new GameObject("ReturnBand");
        go.transform.SetParent(board, false);
        go.transform.localPosition = new Vector3(0f, .4f, -3.5f);
        var trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(halfWidth * 2f, 1.4f, .8f);
        go.AddComponent<ReturnBand>();
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Mat_ReturnBand.mat");
        line.useWorldSpace = false;
        line.widthMultiplier = .12f;
        line.positionCount = 2;
        line.SetPosition(0, new Vector3(-halfWidth + .1f, -.36f, 0f));
        line.SetPosition(1, new Vector3(halfWidth - .1f, -.36f, 0f));
    }
}
