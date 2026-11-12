using UnityEngine;

// Only ordinary enemies spawn; placement/chance rules are retained, cadence uses combat time.
public class Spawner : MonoBehaviour
{
    GameManager gm;
    SpawnTuning S => gm.spawning;
    readonly SpawnProgress progress=new SpawnProgress();
    public double GenerationProgress=>progress.Value;
    public int PeriodicAttempts {get;private set;}
    SpringBumper[] springs;
    SideReturnLane[] returnLanes;

    void Start()=>Initialize(GameManager.Instance);
    public void Initialize(GameManager manager)
    {
        if(gm)return;
        gm = manager;
        springs = gm.board.GetComponentsInChildren<SpringBumper>();
        returnLanes = gm.board.GetComponentsInChildren<SideReturnLane>();
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

    public void TickCombat(double elapsed)
    {
        if (!gm || !gm.IsPlaying) return;
        progress.Advance(elapsed,gm.CurrentDifficulty.interval,()=>{
            PeriodicAttempts++;
            if (Random.value < S.normalChancePercent / 100f) TrySpawnDescending(Enemy.Kind.Normal);
        });
    }

    void TrySpawnDescending(Enemy.Kind kind, float? fixedX = null)
    {
        bool normal = kind == Enemy.Kind.Normal;
        float z = normal ? S.normalSpawnZ : S.bumperSpawnZ;
        int attempts = normal ? S.normalAttempts : S.bumperAttempts;
        var others = FindObjectsByType<Enemy>();
        for (int i = 0; i < attempts; i++)
        {
            float width = HalfWidth(z, kind);
            if (width < 0f || (fixedX.HasValue && Mathf.Abs(fixedX.Value) > width)) return;
            var p = new Vector3(fixedX ?? Random.Range(-width, width), 0f, z);
            if (!Clear(p, others, kind)) continue;
            gm.SpawnEnemy((Card.Equipment)Random.Range(0,3),p);
            return;
        }
    }

    bool Clear(Vector3 p, Enemy[] others, Enemy.Kind kind)
    {
        var settings = gm.EnemySettings(kind);
        foreach (var lane in returnLanes)
            if (lane.BlocksSpawn(p, settings.radius + settings.wallClearance, kind != Enemy.Kind.Special, S.dangerZ)) return false;
        foreach (var spring in springs)
            if (spring && spring.BlocksSpawn(p, settings.radius, settings.wallClearance,
                kind != Enemy.Kind.Special, S.dangerZ)) return false;
        foreach (var e in others)
        {
            if (!e.Alive) continue;
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
        if (dz > 0f && S.arcHorizontalRadius > 0f) width *= S.arcHorizontalRadius / S.arcRadius;
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
