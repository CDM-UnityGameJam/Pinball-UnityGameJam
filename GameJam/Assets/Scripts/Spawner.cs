using UnityEngine;

// Each population has its own timer, placement settings and probability.
public class Spawner : MonoBehaviour
{
    GameManager gm;
    SpawnTuning S => gm.spawning;
    float normalTimer, specialTimer, bumperTimer;
    SpringBumper[] springs;

    void Start()
    {
        gm = GameManager.Instance;
        springs = gm.board.GetComponentsInChildren<SpringBumper>();
        MakeRedLine();
        int count = Mathf.Max(0, S.openingNormalCount);
        var columns = new float[count];
        for (int i = 0; i < count; i++) columns[i] = (i - (count - 1) / 2f) * S.openingColumnSpacing;
        for (int i = 0; i < count; i++)
        {
            int j = Random.Range(i, columns.Length);
            (columns[i], columns[j]) = (columns[j], columns[i]);
            TrySpawnDescending(Enemy.Kind.Normal, columns[i]);
        }
    }

    void Update()
    {
        if (!gm.IsPlaying) return;
        normalTimer += Time.deltaTime;
        bumperTimer += Time.deltaTime;
        float normalInterval = Mathf.Max(0.01f, gm.spawnInterval);
        float bumperInterval = Mathf.Max(0.01f, gm.bumperSpawnInterval);
        float specialInterval = Mathf.Max(0.01f, gm.specialSpawnInterval);
        while (normalTimer >= normalInterval)
        {
            normalTimer -= normalInterval;
            if (Random.value < S.normalChancePercent / 100f) TrySpawnDescending(Enemy.Kind.Normal);
        }
        while (bumperTimer >= bumperInterval)
        {
            bumperTimer -= bumperInterval;
            if (Random.value < S.bumperChancePercent / 100f) TrySpawnDescending(Enemy.Kind.Bumper);
        }
        // Delay uses active play time and never accumulates a backlog of missed rolls.
        if (gm.ActiveTime < S.specialStartDelay) return;
        specialTimer += Time.deltaTime;
        while (specialTimer >= specialInterval)
        {
            specialTimer -= specialInterval;
            if (Random.value < S.SpecialChance(gm.ActiveTime)) TrySpawnSpecial();
        }
    }

    void TrySpawnDescending(Enemy.Kind kind, float? fixedX = null)
    {
        bool normal = kind == Enemy.Kind.Normal;
        if (normal && (S.normalTargets == null || S.normalTargets.Length == 0)) return;
        float z = normal ? S.normalSpawnZ : S.bumperSpawnZ;
        int attempts = normal ? S.normalAttempts : S.bumperAttempts;
        var others = FindObjectsByType<Enemy>();
        for (int i = 0; i < attempts; i++)
        {
            float width = HalfWidth(z, kind);
            if (width < 0f || (fixedX.HasValue && Mathf.Abs(fixedX.Value) > width)) return;
            var p = new Vector3(fixedX ?? Random.Range(-width, width), 0f, z);
            if (!Clear(p, others, kind)) continue;
            gm.SpawnEnemy(kind, normal ? S.normalTargets[Random.Range(0, S.normalTargets.Length)] : 0, p);
            return;
        }
    }

    // Specials reserve their current space only; no descending column is blocked.
    void TrySpawnSpecial()
    {
        var entry = S.PickSpecial(Random.value);
        if (entry == null) return;
        var others = FindObjectsByType<Enemy>();
        for (int i = 0; i < S.specialAttempts; i++)
        {
            float z = Random.Range(Mathf.Min(S.specialMinZ, S.specialMaxZ), Mathf.Max(S.specialMinZ, S.specialMaxZ));
            float width = HalfWidth(z, Enemy.Kind.Special);
            if (width < 0f) continue;
            var p = new Vector3(Random.Range(-width, width), 0f, z);
            if (!Clear(p, others, Enemy.Kind.Special)) continue;
            gm.SpawnEnemy(Enemy.Kind.Special, entry.target, p);
            return;
        }
    }

    bool Clear(Vector3 p, Enemy[] others, Enemy.Kind kind)
    {
        var settings = gm.EnemySettings(kind);
        foreach (var spring in springs)
            if (spring && spring.BlocksSpawn(p, settings.radius, settings.wallClearance,
                kind != Enemy.Kind.Special, S.dangerZ)) return false;
        foreach (var e in others)
        {
            float side = settings.radius + e.Radius + settings.horizontalGap +
                (kind == Enemy.Kind.Special && e.kind == Enemy.Kind.Special ? S.specialPairExtraGap : 0f);
            float stack = settings.radius + e.Radius + settings.verticalGap +
                (kind == Enemy.Kind.Special && e.kind == Enemy.Kind.Special ? S.specialPairVerticalExtraGap : 0f);
            var d = e.transform.localPosition - p;
            if (Mathf.Abs(d.x) < side && Mathf.Abs(d.z) < stack) return false;
        }
        foreach (var ball in gm.Balls)
        {
            if (!ball) continue;
            var local = gm.board.InverseTransformPoint(ball.position);
            if (Vector2.Distance(new Vector2(local.x, local.z), new Vector2(p.x, p.z)) <
                settings.radius + S.ballRadius + settings.ballClearance) return false;
        }
        return true;
    }

    float HalfWidth(float z, Enemy.Kind kind)
    {
        float dz = z - S.arcCenterZ;
        float width = dz > 0f ? Mathf.Sqrt(Mathf.Max(0f, S.arcRadius * S.arcRadius - dz * dz)) : S.boardHalfWidth;
        var settings = gm.EnemySettings(kind);
        return width - settings.radius - settings.wallClearance;
    }

    void MakeRedLine()
    {
        var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "RedLine";
        Destroy(line.GetComponent<Collider>());
        var r = line.GetComponent<Renderer>();
        r.sharedMaterial = gm.board.GetComponentInChildren<Renderer>().sharedMaterial;
        r.material.color = S.dangerLineColor;
        line.transform.SetParent(gm.board, false);
        line.transform.localPosition = new Vector3(S.dangerLineOffset.x, S.dangerLineOffset.y, S.dangerZ);
        line.transform.localScale = S.dangerLineScale;
    }
}
