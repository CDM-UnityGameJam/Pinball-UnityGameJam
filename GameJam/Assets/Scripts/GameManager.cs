using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public Transform board;
    public Rigidbody ball;
    public Vector3 launchLocal = new Vector3(0f, 0.3f, 10.5f);
    public float gravity = 60f;
    public int maxHp = 3;
    public int finalWave = 4;

    // Spawn zone in board-local XZ (upper ~60% of the table, clear of flippers and inlanes).
    public float spawnMinZ = -0.5f, spawnMaxZ = 8.5f, arcCenterZ = 7f, arcRadius = 5f;
    const float CircleRadius = 0.5f, MinGap = 1.5f;

    int hp, wave;
    float countdown;
    bool won, lost;
    readonly List<MysteryCircle> circles = new List<MysteryCircle>();

    int EnemiesLeft { get { int n = 0; foreach (var c in circles) if (c && c.isEnemy) n++; return n; } }

    void Awake()
    {
        Instance = this;
        Time.fixedDeltaTime = 0.01f;
        Physics.gravity = Vector3.down * gravity;
    }

    void Start()
    {
        hp = maxHp;
        StartWave(1);
        StartCoroutine(Launch());
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void StartWave(int n)
    {
        wave = n;
        int count = Mathf.Min(n + 1, 5);
        for (int i = 0; i < count; i++) Spawn(true);
        for (int i = 0; i < count; i++) Spawn(false);
    }

    void Spawn(bool enemy)
    {
        for (int tries = 0; tries < 200; tries++)
        {
            float z = Random.Range(spawnMinZ, spawnMaxZ);
            float halfW = Mathf.Sqrt(arcRadius * arcRadius - Mathf.Pow(Mathf.Max(0f, z - arcCenterZ), 2)) - 0.9f;
            var local = new Vector3(Random.Range(-halfW, halfW), 0f, z);
            if (!IsFree(local)) continue;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Circle";
            Destroy(go.GetComponent<Collider>()); // default capsule would act like a sphere
            go.AddComponent<MeshCollider>().convex = true;
            // Runtime primitives get a built-in material that renders magenta in URP player builds; reuse the board's URP material.
            go.GetComponent<Renderer>().sharedMaterial = board.GetComponentInChildren<Renderer>().sharedMaterial;
            go.transform.SetParent(board, false);
            go.transform.localPosition = local + Vector3.up * 0.5f;
            go.transform.localScale = new Vector3(CircleRadius * 2f, 0.5f, CircleRadius * 2f);
            var mc = go.AddComponent<MysteryCircle>();
            mc.isEnemy = enemy;
            circles.Add(mc);
            return;
        }
        Debug.LogWarning("No free spot for circle");
    }

    bool IsFree(Vector3 local)
    {
        foreach (var c in circles)
        {
            if (!c) continue;
            var p = c.transform.localPosition; p.y = 0f;
            if (Vector3.Distance(p, local) < MinGap) return false;
        }
        var b = board.InverseTransformPoint(ball.position); b.y = 0f;
        return Vector3.Distance(b, local) > MinGap;
    }

    public void OnEnemyKilled(MysteryCircle enemy)
    {
        circles.Remove(enemy);
        if (won || lost || EnemiesLeft > 0) return;

        foreach (var c in circles) if (c) Destroy(c.gameObject);
        circles.Clear();
        if (wave >= finalWave) { won = true; ball.isKinematic = true; return; }
        StartWave(wave + 1);
    }

    public void OnDrain()
    {
        if (won || lost || countdown > 0f) return;
        hp--;
        if (hp <= 0) { lost = true; ball.isKinematic = true; return; }
        StartCoroutine(Launch());
    }

    IEnumerator Launch()
    {
        ball.isKinematic = true;
        ball.position = board.TransformPoint(launchLocal);
        ball.transform.position = ball.position;
        countdown = 1f;
        while (countdown > 0f) { countdown -= Time.deltaTime; yield return null; }
        countdown = 0f;
        if (won || lost) yield break;
        ball.isKinematic = false;
        ball.linearVelocity = board.TransformDirection(new Vector3(Random.Range(-3f, 3f), 0f, -2f));
    }

    void OnGUI()
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(20, 15, 400, 30), "HP: " + hp + " / " + maxHp, s);
        GUI.Label(new Rect(20, 45, 400, 30), "Wave: " + wave + " / " + finalWave, s);
        GUI.Label(new Rect(20, 75, 400, 30), "Enemies left: " + EnemiesLeft, s);

        var big = new GUIStyle(s) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
        var center = new Rect(0, Screen.height / 2f - 80, Screen.width, 160);
        if (countdown > 0f && !won && !lost) GUI.Label(center, Mathf.CeilToInt(countdown).ToString(), big);
        if (won || lost)
        {
            GUI.Box(center, "");
            GUI.Label(center, (won ? "YOU WIN!" : "YOU LOSE") + "\n<size=24>Press R to restart</size>", new GUIStyle(big) { richText = true });
        }
    }
}
