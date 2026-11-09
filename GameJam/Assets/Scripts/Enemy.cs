using UnityEngine;

// Board circle: normal enemy (hidden target, 5 HP), special enemy (111/222/333, 5 HP, 30 s life) or bumper.
// Drawn as a ring (the cylinder) around a darker cap; its on-screen label is drawn by Hud.
public class Enemy : MonoBehaviour
{
    public enum Kind { Normal, Special, Bumper }

    public const float Radius = 0.75f;

    // 28-46 inclusive plus 74, 148, 185, 259, 296: 24 targets. 111/222/333 are special-only.
    public static readonly int[] NormalTargets = BuildNormalTargets();
    public static readonly int[] SpecialTargets = { 111, 222, 333 };

    const float HitCooldown = 0.2f, VisibleTime = 20f, SpecialLifetime = 30f, FlashTime = 0.12f;
    const int EnergyPerHit = 25;

    public float kickSpeed = 20f;
    public Kind kind;
    public int target, hp, maxHp;

    float visibleLeft, lifeLeft, flashLeft;
    float lastHit = -1f;
    Rigidbody body;
    Material ring, cap;
    Color ringColor, capColor;

    public bool Visible => kind == Kind.Special || visibleLeft > 0f;
    public float VisibleLeft => visibleLeft;
    public float LifeLeft => lifeLeft;
    public int Multiplier => target == 111 ? 10 : target == 222 ? 2 : 3;

    // Creates the circle under the board at a board-local XZ position.
    public static Enemy Create(Transform board, Kind kind, int target, Vector3 local)
    {
        // Runtime primitives get a built-in material that renders magenta in URP player builds; reuse the board's URP material.
        var urp = board.GetComponentInChildren<Renderer>().sharedMaterial;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = kind.ToString();
        Destroy(go.GetComponent<Collider>()); // default capsule would act like a sphere
        go.AddComponent<MeshCollider>().convex = true;
        go.GetComponent<Renderer>().sharedMaterial = urp;
        go.transform.SetParent(board, false);
        // Flat disc 0.5 tall, centred at the ball's centre height so hits land on its side.
        go.transform.localPosition = new Vector3(local.x, 0.25f, local.z);
        go.transform.localScale = new Vector3(Radius * 2f, 0.25f, Radius * 2f);

        var capGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        capGo.name = "Cap";
        Destroy(capGo.GetComponent<Collider>());
        capGo.GetComponent<Renderer>().sharedMaterial = urp;
        capGo.transform.SetParent(go.transform, false);
        capGo.transform.localPosition = new Vector3(0f, 1.01f, 0f); // primitive cylinder top is at local y = 1
        capGo.transform.localScale = new Vector3(0.8f, 0.02f, 0.8f);

        var e = go.AddComponent<Enemy>();
        e.Init(kind, target, capGo.GetComponent<Renderer>().material);
        return e;
    }

    void Init(Kind k, int targetNumber, Material capMaterial)
    {
        kind = k;
        target = targetNumber;
        maxHp = hp = 5;
        lifeLeft = SpecialLifetime;
        ring = GetComponent<Renderer>().material;
        cap = capMaterial;
        ringColor = k == Kind.Normal ? Hud.EnemyRing : k == Kind.Special ? Hud.Gold : Hud.BumperRing;
        capColor = k == Kind.Normal ? Hud.EnemyFill : k == Kind.Special ? Hud.SpecialFill : Hud.BumperFill;
        ring.color = ringColor;
        cap.color = capColor;
        if (k != Kind.Special)
        {
            body = gameObject.AddComponent<Rigidbody>(); // kinematic body so a moving collider is not re-baked as static
            body.isKinematic = true;
        }
    }

    // Normal enemies and bumpers descend; FixedUpdate stops on its own while timeScale is 0.
    void FixedUpdate()
    {
        if (!body) return;
        var board = transform.parent;
        var p = board.InverseTransformPoint(body.position);
        p.z -= Spawner.DescendSpeed * Time.fixedDeltaTime;
        if (p.z <= Spawner.LineZ)
        {
            if (kind == Kind.Normal && hp > 0) GameManager.Instance.LoseLife(); // no kill credit, no relaunch
            Destroy(gameObject);
            return;
        }
        body.MovePosition(board.TransformPoint(p));
    }

    // Scaled time: visibility and special lifetime freeze during the launch wait and pause.
    void Update()
    {
        if (kind == Kind.Normal && visibleLeft > 0f) visibleLeft = Mathf.Max(0f, visibleLeft - Time.deltaTime);
        if (kind == Kind.Special)
        {
            lifeLeft -= Time.deltaTime;
            if (lifeLeft <= 0f) { Destroy(gameObject); return; } // timeout: no reward
        }

        // Ring turns green when the current number hits it, blue when the dragged card's preview would.
        // Hidden targets never light up.
        var r = ringColor;
        if (kind != Kind.Bumper && Visible)
        {
            if (target == GameManager.Instance.CurrentNumber) r = Hud.Green;
            else if (Hud.Instance && Hud.Instance.PreviewNumber == target) r = Hud.Blue;
        }
        ring.color = r;
        if (flashLeft > 0f) flashLeft -= Time.unscaledDeltaTime;
        cap.color = flashLeft > 0f ? Color.white : capColor;
    }

    void OnCollisionEnter(Collision c)
    {
        if (!GameManager.Instance.IsPlaying || hp <= 0 || !c.collider.CompareTag("Player") || Time.time - lastHit < HitCooldown) return;
        lastHit = Time.time;

        if (kind == Kind.Bumper)
        {
            var dir = Vector3.ProjectOnPlane(c.rigidbody.position - transform.position, transform.up).normalized;
            c.rigidbody.linearVelocity = dir * kickSpeed;
            return;
        }

        var gm = GameManager.Instance;
        bool matched = target == gm.CurrentNumber;
        ApplyHit(matched);
        if (matched)
        {
            // A pulse is ordinary damage: it cannot trigger another match or pulse.
            foreach (var enemy in FindObjectsByType<Enemy>())
                if (enemy != this) enemy.ApplyHit(false);
        }
        // Only the physical ball hit grants energy, after all kills and reveals resolve.
        gm.AddEnergy(EnergyPerHit);
    }

    void ApplyHit(bool matched)
    {
        // Destroy is deferred until frame end, so dead enemies must reject further hits.
        if (kind == Kind.Bumper || hp <= 0) return;
        var gm = GameManager.Instance;
        var hud = Hud.Instance;
        if (kind == Kind.Normal) visibleLeft = VisibleTime;
        hp = matched ? 0 : hp - 1;
        flashLeft = FlashTime;
        if (hud) hud.Popup(transform.position, matched ? "MATCH!" : "-1", matched ? Hud.Green : Color.white);

        if (hp <= 0)
        {
            if (kind == Kind.Normal)
            {
                long reward = gm.OnNormalKill(matched);
                if (hud) hud.Popup(transform.position, "+" + reward.ToString("N0"), Hud.Green);
            }
            else
            {
                long before = gm.Score;
                gm.OnSpecialKill(Multiplier, matched);
                if (hud) hud.Popup(transform.position, (matched ? "MATCH! " : "") + "+" + (gm.Score - before).ToString("N0"), Hud.Gold);
            }
            Destroy(gameObject);
        }
    }

    static int[] BuildNormalTargets()
    {
        var list = new System.Collections.Generic.List<int>();
        for (int n = 28; n <= 46; n++) list.Add(n);
        list.AddRange(new[] { 74, 148, 185, 259, 296 });
        return list.ToArray();
    }
}
