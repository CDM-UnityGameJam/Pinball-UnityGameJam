using UnityEngine;

// Ordinary enemy; legacy kind values remain for serialized scene compatibility.
// Drawn as a ring (the cylinder) around a darker cap; its on-screen label is drawn by Hud.
public class Enemy : MonoBehaviour
{
    public enum Kind { Normal, Special, Bumper }

    static long nextSpawnOrder;
    public long SpawnOrder { get; private set; }
    public bool Alive => kind == Kind.Normal && hp > 0 && gameObject.activeInHierarchy;
    EnemyTuning settings;
    public EnemyTuning Settings => settings;
    public float Radius => settings.radius;
    public Kind kind;
    public int Number { get; private set; }
    public int hp, maxHp;
    public bool PreviewTarget { get; set; }

    float flashLeft;
    double lastHit = double.NegativeInfinity;
    Rigidbody body;
    Collider hitCollider;
    OuterRingSurface surface;
    Material ring, cap;
    Color ringColor, capColor;

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
        SpawnOrder = nextSpawnOrder++;
        kind = k;
        settings = GameManager.Instance.EnemySettings(k);
        surface = transform.parent.GetComponentInChildren<OuterRingSurface>();
        Number = Mathf.Clamp(targetNumber, 1, 6);
        hitCollider = GetComponent<MeshCollider>();
        maxHp = hp = Mathf.Max(1, settings.health);
        ring = GetComponent<Renderer>().material;
        cap = capMaterial;
        ringColor = settings.ringColor;
        capColor = settings.fillColor;
        ring.color = ringColor;
        cap.color = capColor;
        {
            body = gameObject.AddComponent<Rigidbody>(); // kinematic body so a moving collider is not re-baked as static
            body.isKinematic = true;
        }
    }

    public Vector3 LocalPosition => transform.parent.InverseTransformPoint(body ? body.position : transform.position);
    public void MoveToLocal(Vector3 local)
    {
        if(surface) local.y=settings.centerHeight+surface.HeightAt(local.x,local.z);
        body.MovePosition(transform.parent.TransformPoint(local));
    }
    public void Breach()
    {
        if(!Alive || !GameManager.Instance.IsPlaying) return;
        hp=0;
        GameManager.Instance.LoseLife();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    void Update()
    {
        ring.color = PreviewTarget ? Hud.Gold : ringColor;
        if (flashLeft > 0f) flashLeft -= Time.unscaledDeltaTime;
        cap.color = flashLeft > 0f ? settings.flashColor : IsBurning ? new Color(1,.32f,.03f) : IsStunned ? new Color(.3f,.7f,1f) : capColor;
    }

    void OnCollisionEnter(Collision c)
    {
        NudgeSlowBall(c);
        TryBallHit(c.rigidbody, c.contactCount>0 ? c.GetContact(0).point : transform.position);
    }

    public bool TryBallHit(Rigidbody ball) => TryBallHit(ball,transform.position);
    public bool TryBallHit(Rigidbody ball,Vector3 collisionPoint)
    {
        var gm = GameManager.Instance;
        if (!gm || !gm.IsPlaying || !Alive || !ball || ball.isKinematic || !ball.CompareTag("Player") || gm.CombatTime - lastHit < settings.hitCooldown - .000001) return false;
        lastHit = gm.CombatTime;
        TakeDamage(gm.luck.collisionDamage);
        gm.RegisterBallHit(this,collisionPoint);
        return true;
    }

    // Persistent contacts need their own push: OnCollisionEnter only runs once.
    // This never counts as another hit.
    void OnCollisionStay(Collision c) => NudgeSlowBall(c);

    void NudgeSlowBall(Collision c)
    {
        var gm = GameManager.Instance;
        var ball = c.rigidbody;
        if (!gm || !gm.IsPlaying || hp <= 0 || !ball || ball.isKinematic || !c.collider.CompareTag("Player")) return;

        var board = gm.board;
        var velocity = Vector3.ProjectOnPlane(ball.linearVelocity, board.up);
        if (velocity.sqrMagnitude >= settings.unstickSpeed * settings.unstickSpeed) return;

        var away = Vector3.ProjectOnPlane(ball.position - transform.position, board.up).normalized;
        // A shared small diagonal bias keeps opposing enemies from cancelling perfectly.
        var direction = (away + board.forward * settings.unstickForwardBias + board.right * settings.unstickSideBias).normalized;
        ball.AddForce(direction * settings.unstickAcceleration, ForceMode.Acceleration);
    }

    public void TakeDamage(int damage,AttackRecord source = null)
    {
        if (!Alive || !GameManager.Instance.IsPlaying) return;
        damage = Mathf.Max(0, damage);
        if (damage == 0) return;
        hp = Mathf.Max(0, hp - damage);
        flashLeft = settings.flashDuration;
        if (Hud.Instance) Hud.Instance.Popup(transform.position, "-" + Mathf.Max(0, damage), settings.hitPopupColor);
        if (hp > 0) return;
        hitCollider.enabled = false;
        long reward = GameManager.Instance.OnNormalKill(source);
        if (Hud.Instance) Hud.Instance.Popup(transform.position, "+" + reward, settings.killPopupColor);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
    double passiveBurnUntil=double.NegativeInfinity, activeBurnUntil=double.NegativeInfinity;
    double nextBurnTick=double.PositiveInfinity, stunUntil=double.NegativeInfinity, immuneUntil=double.NegativeInfinity;
    AttackRecord passiveBurnSource,activeBurnSource;
    public double NextBurnTickAt => Alive ? nextBurnTick : double.PositiveInfinity;
    double Now => GameManager.Instance.CombatTime;
    public bool IsBurning => Alive && (passiveBurnUntil>Now || activeBurnUntil>Now);
    public bool IsStunned => Alive && stunUntil>Now+.000001;
    public float StunRemaining => (float)System.Math.Max(0,stunUntil-Now);
    public float PassiveImmunityRemaining => IsStunned ? 0 : (float)System.Math.Max(0,immuneUntil-Now);
    public float PassiveBurnRemaining => (float)System.Math.Max(0,passiveBurnUntil-Now);
    public float ActiveBurnRemaining => (float)System.Math.Max(0,activeBurnUntil-Now);
    public void Ignite(bool active,float duration,AttackRecord source)
    {
        if(!Alive || !GameManager.Instance.IsPlaying || duration<=0) return;
        if(!IsBurning) nextBurnTick=Now+Mathf.Max(.01f,GameManager.Instance.luck.burnTickInterval);
        double until=Now+duration;
        if(active) {
            if(until>=activeBurnUntil) { activeBurnUntil=until; activeBurnSource=source; }
        } else if(until>=passiveBurnUntil) { passiveBurnUntil=until; passiveBurnSource=source; }
        if(GameManager.Instance.Vfx) GameManager.Instance.Vfx.Ignite(transform.position,active);
    }
    public bool Stun(float duration,bool active)
    {
        if(!Alive || !GameManager.Instance.IsPlaying || duration<=0) return false;
        // Agreed rule: passive stun cannot refresh an ongoing stun; active stun may extend it.
        if(!active && (IsStunned || Now<immuneUntil-.000001)) return false;
        stunUntil=System.Math.Max(stunUntil,Now+duration);
        immuneUntil=System.Math.Max(immuneUntil,stunUntil+GameManager.Instance.luck.passiveStunImmunity);
        return true;
    }
    public void TickStatus()
    {
        if(!Alive || !GameManager.Instance.IsPlaying || nextBurnTick>Now+.000001) return;
        var t=GameManager.Instance.luck;
        bool strong=activeBurnUntil>=nextBurnTick-.000001;
        bool weak=passiveBurnUntil>=nextBurnTick-.000001;
        if(strong || weak) {
            bool useActive=strong && (!weak || t.activeBurnDps>=t.passiveBurnDps);
            int dps=useActive ? t.activeBurnDps : t.passiveBurnDps;
            var source=useActive ? activeBurnSource : passiveBurnSource;
            if(GameManager.Instance.Vfx) GameManager.Instance.Vfx.BurnTick(transform.position,strong);
            TakeDamage(Mathf.FloorToInt(dps*t.burnTickInterval+.5f),source);
        }
        double following=nextBurnTick+Mathf.Max(.01f,t.burnTickInterval);
        nextBurnTick=Alive && System.Math.Max(passiveBurnUntil,activeBurnUntil)>Now+.000001 ? following : double.PositiveInfinity;
    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool DebugSetNumber(int number)
    {
        if (!Alive || number < 1 || number > 6) return false;
        Number = number;
        return true;
    }
#endif
    void OnDestroy() { if (ring) Destroy(ring); if (cap) Destroy(cap); }
}
