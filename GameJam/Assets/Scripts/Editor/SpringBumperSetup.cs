using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SpringBumperSetup
{
    [MenuItem("LuckyPinball/Add Upper Springs")]
    public static void Install()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before editing springs.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Pinball.unity");
        var board = GameObject.Find("Board").transform;
        AddToBoard(board);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Upper springs installed; existing scene tuning preserved.");
    }

    public static void AddToBoard(Transform board)
    {
        Create(board, "SpringL", -3.9f);
        Create(board, "SpringR", 3.9f);
    }

    static void Create(Transform board, string name, float x)
    {
        if (board.Find(name)) return;
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = name;
        pad.transform.SetParent(board, false);
        pad.transform.localPosition = new Vector3(x, 0.25f, 9.7f);
        pad.transform.localScale = new Vector3(1.1f, 0.25f, 1.1f);
        Object.DestroyImmediate(pad.GetComponent<Collider>());
        pad.AddComponent<MeshCollider>().convex = true;
        pad.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Mat_F2D33AFF.mat");
        var spring = pad.AddComponent<SpringBumper>();
        spring.coilMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Mat_A9ADB8FF.mat");
        spring.EnsureVisual();
    }
}
