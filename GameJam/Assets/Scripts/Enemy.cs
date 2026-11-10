using UnityEngine;

// Board circle: normal enemy, stationary special enemy or bumper; each kind has independent tuning.
// Drawn as a ring (the cylinder) around a darker cap; its on-screen label is drawn by Hud.
public class Enemy : MonoBehaviour
{
    public enum Kind { Normal, Special, Bumper }

    EnemyTuning settings;
    public EnemyTuning Settings => settings;
    public float Radius => settings.radius;
    public Kind kind;
    public int target, hp, maxHp;

    float visibleLeft, lifeLeft, flashLeft;
    float lastHit = float.NegativeInfinity;
    Rigidbody body;
    Collider hitCollider;
    OuterRingSurface surface;
    Material ring, cap;
    Color ringColor, capColor;

    public bool Visible => kind == Kind.Special || visibleLeft > 0f;
    public float VisibleLeft => visibleLeft;
    public float LifeLeft => lifeLeft;
    public int Multiplier
    {
        get
        {
            foreach (var entry in GameManager.Instance.spawning.specialTargets)
                if (entry != null && entry.target == target) return Mathf.Max(1, entry.scoreMultiplier);
            return 1;
        }
    }

    // Creates the circle under the board at a board-local XZ position.
    public static Enemy Create(Transform board, Kind kind, int target, Vector3 local)
    {
        // Runtime primitives get a built-in material that renders magenta in URP player builds; reuse the board's URP material.
        var settings = GameManager.Instance.EnemySettings(kind);
        var urp = board.GetComponentInChildren<Renderer>().sharedMaterial;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = kind.ToString();
        go.GetComponent<Collider>().enabled = false;
        Destroy(go.GetComponent<Collider>()); // default capsule would act like a sphere
        go.AddComponent<MeshCollider>().convex = true;
        go.GetComponent<Renderer>().sharedMaterial = urp;
        go.transform.SetParent(board, false);
        // Flat disc 0.5 tall, centred at the ball's centre height so hits land on its side.
        var terrain = board.GetComponentInChildren<OuterRingSurface>();
        go.transform.localPosition = new Vector3(local.x, settings.centerHeight + (terrain ? terrain.HeightAt(local.x, local.z) : 0f), local.z);
        go.transform.localScale = new Vector3(settings.radius * 2f, settings.halfHeight, settings.radius * 2f);

        var capGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        capGo.name = "Cap";
        Destroy(capGo.GetComponent<Collider>());
        capGo.GetComponent<Renderer>().sharedMaterial = urp;
        capGo.transform.SetParent(go.transform, false);
        capGo.transform.localPosition = settings.capPosition; // primitive cylinder top is at local y = 1
        capGo.transform.localScale = settings.capScale;

        var e = go.AddComponent<Enemy>();
        e.Init(kind, target, capGo.GetComponent<Renderer>().material);
        return e;
    }

    void Init(Kind k, int targetNumber, Material capMaterial)
    {
        kind = k;
        settings = GameManager.Instance.EnemySettings(k);
        surface = transform.parent.GetComponentInChildren<OuterRingSurface>();
        target = targetNumber;
        hitCollider = GetComponent<MeshCollider>();
        maxHp = hp = Mathf.Max(1, settings.health);
        lifeLeft = settings.lifetime;
        ring = GetComponent<Renderer>().material;
        cap = capMaterial;
        ringColor = settings.ringColor;
        capColor = settings.fillColor;
        ring.color = ringColor;
        cap.color = capColor;
        if (k != Kind.Special)
        {
            body = gameObject.AddComponent<Rigidbody>(); // kinematic body so a moving collider is not re-baked as static
            body.isKinematic = true;
        }
        RefreshBallCollisions();
    }

    // Ignore only ball pairs: unmatched specials remain visible and allow balls through.
    // Called on spawn, number changes and every extra-ball creation, before the next physics step.
    public void RefreshBallCollisions()
    {
        var gm = GameManager.Instance;
        if (kind != Kind.Special || !hitCollider || !gm) return;
        bool ignore = !gm.Matches(this);
        foreach (var activeBall in gm.Balls)
        {
            if (!activeBall) continue;
            foreach (var ballCollider in activeBall.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(hitCollider, ballCollider, ignore);
        }
    }

    // Normal enemies and bumpers descend; FixedUpdate stops on its own while timeScale is 0.
    void FixedUpdate()
    {
        if (!body) return;
        var board = transform.parent;
        var p = board.InverseTransformPoint(body.position);
        var gm = GameManager.Instance;
        float origin = kind == Kind.Bumper ? gm.spawning.bumperSpawnZ : gm.spawning.normalSpawnZ;
        float duration = kind == Kind.Bumper ? gm.bumperDescendTime : gm.enemyDescendTime;
        p.z -= (origin - gm.spawning.dangerZ) / Mathf.Max(0.01f, duration) * Time.fixedDeltaTime;
        if (p.z <= GameManager.Instance.spawning.dangerZ)
        {
            if (kind == Kind.Normal && hp > 0) GameManager.Instance.LoseLife(); // no kill credit, no relaunch
            Destroy(gameObject);
            return;
        }
        if (surface) p.y = settings.centerHeight + surface.HeightAt(p.x, p.z);
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
            if (GameManager.Instance.Matches(this)) r = settings.matchColor;
            else if (Hud.Instance && Hud.Instance.PreviewNumber == target) r = settings.previewColor;
        }
        ring.color = r;
        if (flashLeft > 0f) flashLeft -= Time.unscaledDeltaTime;
        cap.color = flashLeft > 0f ? settings.flashColor : capColor;
    }

    void OnCollisionEnter(Collision c)
    {
        NudgeSlowBall(c);
        if (kind == Kind.Special && !GameManager.Instance.Matches(this)) return;
        if (!GameManager.Instance.IsPlaying || hp <= 0 || !c.collider.CompareTag("Player") || Time.time - lastHit < settings.hitCooldown) return;
        lastHit = Time.time;

        if (kind == Kind.Bumper)
        {
            var dir = Vector3.ProjectOnPlane(c.rigidbody.position - transform.position, transform.up).normalized;
            c.rigidbody.linearVelocity = dir * settings.kickSpeed;
            return;
        }

        var gm = GameManager.Instance;
        bool matched = gm.Matches(this);
        ApplyHit(matched);
        if (matched)
        {
            // A pulse is ordinary damage: it cannot trigger another match or pulse.
            foreach (var enemy in FindObjectsByType<Enemy>())
                if (enemy != this) enemy.ApplyDamage(false, settings.pulseDamage);
        }
        // Only the physical ball hit grants energy, after all kills and reveals resolve.
        gm.AddEnergy(settings.hitEnergy);
    }

    // Persistent contacts need their own push: OnCollisionEnter only runs once.
    // This never counts as another hit or grants energy.
    void OnCollisionStay(Collision c) => NudgeSlowBall(c);

    void NudgeSlowBall(Collision c)
    {
        var gm = GameManager.Instance;
        var ball = c.rigidbody;
        if (!gm || !gm.IsPlaying || hp <= 0 || !ball || ball.isKinematic || !c.collider.CompareTag("Player")) return;
        if (kind == Kind.Special && !gm.Matches(this)) return;

        var board = gm.board;
        var velocity = Vector3.ProjectOnPlane(ball.linearVelocity, board.up);
        if (velocity.sqrMagnitude >= settings.unstickSpeed * settings.unstickSpeed) return;

        var away = Vector3.ProjectOnPlane(ball.position - transform.position, board.up).normalized;
        // A shared small diagonal bias keeps opposing enemies from cancelling perfectly.
        var direction = (away + board.forward * settings.unstickForwardBias + board.right * settings.unstickSideBias).normalized;
        ball.AddForce(direction * settings.unstickAcceleration, ForceMode.Acceleration);
    }

    void ApplyHit(bool matched) => ApplyDamage(matched, settings.hitDamage);

    void ApplyDamage(bool matched, int damage)
    {
        // Destroy is deferred until frame end, so dead enemies must reject further hits.
        if (kind == Kind.Bumper || hp <= 0) return;
        var gm = GameManager.Instance;
        var hud = Hud.Instance;
        if (kind == Kind.Special && !matched) return;
        if (kind == Kind.Normal) visibleLeft = settings.revealDuration;
        hp = matched ? 0 : Mathf.Max(0, hp - Mathf.Max(0, damage));
        flashLeft = settings.flashDuration;
        if (hud) hud.Popup(transform.position, matched ? "MATCH!" : "-" + Mathf.Max(0, damage), matched ? settings.matchColor : settings.hitPopupColor);

        if (hp <= 0)
        {
            if (kind == Kind.Normal)
            {
                long reward = gm.OnNormalKill(matched);
                if (hud) hud.Popup(transform.position, "+" + reward.ToString("N0"), settings.killPopupColor);
            }
            else
            {
                long before = gm.Score;
                gm.OnSpecialKill(Multiplier, matched);
                if (hud) hud.Popup(transform.position, (matched ? "MATCH! " : "") + "+" + (gm.Score - before).ToString("N0"), settings.killPopupColor);
            }
            Destroy(gameObject);
        }
    }

}
