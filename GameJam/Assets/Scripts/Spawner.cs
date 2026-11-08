using UnityEngine;

// Spawns board circles and draws the red line. Board-local XZ: X = width, Z = up the table.
public class Spawner : MonoBehaviour
{
    public const float SpawnZ = 10f, LineZ = -5f, DescendTime = 40f;
    public const float DescendSpeed = (SpawnZ - LineZ) / DescendTime;

    const float Interval = 5f;
    const float Radius = 0.5f, Gap = 0.4f, WallGap = 0.3f, BallRadius = 0.25f;
    const float SpecialMinZ = LineZ + 1.5f, SpecialMaxZ = 9f;
    const int StartSpawns = 3, Tries = 30;

    float normalTimer, specialTimer;

    void Start()
    {
        MakeRedLine();
        for (int i = 0; i < StartSpawns; i++) TrySpawnNormal();
    }

    // Scaled time, and only while Playing: frozen during the launch wait, pause and game over.
    void Update()
    {
        var gm = GameManager.Instance;
        if (!gm.IsPlaying) return;
        normalTimer += Time.deltaTime;
        specialTimer += Time.deltaTime;
        while (normalTimer >= Interval) { normalTimer -= Interval; TrySpawnNormal(); }
        while (specialTimer >= Interval)
        {
            specialTimer -= Interval;
            if (Random.value < SpecialChance(gm.ActiveTime)) TrySpawnSpecial();
        }
    }

    // 5% + 2 percentage points per full minute of active play, capped at 15%.
    public static float SpecialChance(float activeTime) => Mathf.Min(0.05f + 0.02f * Mathf.Floor(activeTime / 60f), 0.15f);

    // 111 / 222 / 333 weighted 10 / 45 / 45.
    public static int SpecialTarget(float roll) => roll < 0.1f ? 111 : roll < 0.55f ? 222 : 333;

    // 24 normal targets + 1 bumper, 4% each. Blocked attempts are skipped, never queued.
    void TrySpawnNormal()
    {
        int entry = Random.Range(0, Enemy.NormalTargets.Length + 1);
        bool bumper = entry == Enemy.NormalTargets.Length;
        var others = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        for (int i = 0; i < Tries; i++)
        {
            var p = new Vector3(Random.Range(-HalfWidth(SpawnZ), HalfWidth(SpawnZ)), 0f, SpawnZ);
            if (!Clear(p, others) || PathHitsSpecial(p.x, others)) continue;
            GameManager.Instance.SpawnEnemy(bumper ? Enemy.Kind.Bumper : Enemy.Kind.Normal, bumper ? 0 : Enemy.NormalTargets[entry], p);
            return;
        }
    }

    // First pass: only spots outside every descending path. Second pass: spots above descenders,
    // which are moving away from it. Either way no descender above it ever reaches it.
    void TrySpawnSpecial()
    {
        var others = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < Tries; i++)
            {
                float z = Random.Range(SpecialMinZ, SpecialMaxZ);
                var p = new Vector3(Random.Range(-HalfWidth(z), HalfWidth(z)), 0f, z);
                if (!Clear(p, others) || !SpecialFits(p, others, pass == 0)) continue;
                GameManager.Instance.SpawnEnemy(Enemy.Kind.Special, SpecialTarget(Random.value), p);
                return;
            }
    }

    // Equal-speed descenders keep their spacing forever, so spawn-time clearance is enough between them.
    bool Clear(Vector3 p, Enemy[] others)
    {
        foreach (var e in others)
            if (Flat(e.transform.localPosition, p) < 2f * Radius + Gap) return false;
        var ball = GameManager.Instance.board.InverseTransformPoint(GameManager.Instance.ball.position);
        return Flat(ball, p) >= Radius + BallRadius + Gap;
    }

    static bool PathHitsSpecial(float x, Enemy[] others)
    {
        foreach (var e in others)
            if (e.kind == Enemy.Kind.Special && Mathf.Abs(e.transform.localPosition.x - x) < 2f * Radius + Gap) return true;
        return false;
    }

    static bool SpecialFits(Vector3 p, Enemy[] others, bool strict)
    {
        foreach (var e in others)
        {
            if (e.kind == Enemy.Kind.Special) continue;
            var d = e.transform.localPosition;
            if (Mathf.Abs(d.x - p.x) >= 2f * Radius + Gap) continue; // different column
            if (strict || d.z > p.z - (2f * Radius + Gap)) return false; // it would descend into the special
        }
        return true;
    }

    // Usable half width at height z: side walls at x ±5, top arc radius 5 centered at z 7.
    static float HalfWidth(float z)
    {
        float w = z > 7f ? Mathf.Sqrt(Mathf.Max(0f, 25f - (z - 7f) * (z - 7f))) : 5f;
        return w - Radius - WallGap;
    }

    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    void MakeRedLine()
    {
        var board = GameManager.Instance.board;
        var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "RedLine";
        Destroy(line.GetComponent<Collider>());
        var r = line.GetComponent<Renderer>();
        r.sharedMaterial = board.GetComponentInChildren<Renderer>().sharedMaterial; // URP-safe material, see SpawnEnemy
        r.material.color = Color.red;
        line.transform.SetParent(board, false);
        line.transform.localPosition = new Vector3(0f, 0.01f, LineZ);
        line.transform.localScale = new Vector3(10f, 0.02f, 0.08f);
    }
}
