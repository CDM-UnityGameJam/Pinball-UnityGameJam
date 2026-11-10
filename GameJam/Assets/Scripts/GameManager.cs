using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    enum RunState { Waiting, Playing, GameOver }

    [Header("Run / Cards / Scoring")]
    [Min(1)] public int handSize = 5;
    [Min(0f)] public float launchWait = 3f;
    [Min(0)] public int normalKillPoints = 50;
    [Min(0)] public int breachLifeCost = 1;
    [Min(0)] public int drainLifeCost = 1;
    [Min(0.001f)] public float physicsStep = 0.01f;
    public LuckTuning luck = new LuckTuning();
    [Header("Spawn Layout / Probability / Targets")]
    public SpawnTuning spawning = new SpawnTuning();
    [Header("Normal Enemy")]
    public EnemyTuning normalEnemy = new EnemyTuning();
    [Header("Special Enemy")]
    public EnemyTuning specialEnemy = new EnemyTuning {
        ringColor = new Color32(233, 185, 73, 255), fillColor = new Color32(110, 78, 28, 255),
        killPopupColor = new Color32(233, 185, 73, 255) };
    [Header("Bumper")]
    public EnemyTuning bumperEnemy = new EnemyTuning {
        ringColor = new Color32(169, 173, 184, 255), fillColor = new Color32(58, 63, 76, 255) };
    [Header("HUD")]
    public HudTuning hud = new HudTuning();
    public int HandSize => hand == null ? Mathf.Max(1, handSize) : hand.Length;
    public EnemyTuning EnemySettings(Enemy.Kind kind) => kind == Enemy.Kind.Normal ? normalEnemy :
        kind == Enemy.Kind.Special ? specialEnemy : bumperEnemy;

    public static GameManager Instance;

    public Transform board;
    public Rigidbody ball;
    // Between and just below the flipper tips (tips sit near z -8.3, x ±0.6).
    public Vector3 launchLocal = new Vector3(0f, 0.3f, -9f);
    public float launchSpeed = 18f;
    public float launchBiasDegrees = 8f;
    public float gravity = 60f;
    [Min(1)] public int maxLives = 3;
    [Header("Enemy Timing")]
    [Min(0.1f), Tooltip("Seconds between normal-enemy spawn attempts.")]
    public float spawnInterval = 5f;
    [Min(0.1f), Tooltip("Seconds for normal enemies to reach the danger line.")]
    public float enemyDescendTime = 40f;
    RunState state;
    bool paused;
    float countdown, activeTime;
    int lives, currentNumber;
    long score;
    Card[] hand; // null = empty slot
    readonly List<Rigidbody> balls = new List<Rigidbody>();
    Rigidbody ballTemplate;
    public IReadOnlyList<Rigidbody> Balls => balls;
    public int BallCount => balls.Count;

    void Awake()
    {
        Instance = this;
        handSize = 5;
        hand = new Card[] { new Card(6, Card.Equipment.Spark), new Card(6, Card.Equipment.Shockwave), new Card(2, Card.Equipment.Arc), new Card(4, Card.Equipment.Arc), new Card(5, Card.Equipment.Spark) };
        currentNumber = Random.Range(1, 7);
        nextLuckyAt = Mathf.Max(.01f, luck.luckyRefreshInterval);
        Time.fixedDeltaTime = Mathf.Max(0.001f, physicsStep);
        Physics.gravity = Vector3.down * gravity;
    }

    void Start()
    {
        lives = maxLives;
        ballTemplate = Instantiate(ball);
        ballTemplate.name = "BallTemplate";
        ballTemplate.gameObject.SetActive(false);
        balls.Add(ball);
        Vfx = gameObject.AddComponent<CombatVfx>();
        AttachBallVisuals(ball);
        gameObject.AddComponent<Spawner>();
        new GameObject("Hud").AddComponent<Hud>();
        BeginWaiting();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (state != RunState.GameOver && kb != null && kb.rKey.wasPressedThisFrame) SetPaused(!paused);
        if (paused) return;

        if (state == RunState.Waiting)
        {
            // Clamp load hitches so they cannot consume the entire launch wait.
            countdown -= Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime);
            if (countdown <= 0f) Launch();
        }
    }

    void FixedUpdate()
    {
        if(!IsPlaying) return;
        TickGameplay(Time.fixedDeltaTime);
        EnemyMovement.Step(this, Time.fixedDeltaTime);
    }
    void AttachBallVisuals(Rigidbody target)
    {
        if(target && !target.GetComponent<BallEnchantmentVisuals>()) target.gameObject.AddComponent<BallEnchantmentVisuals>();
    }
    public void RefreshBallEnchantments()
    {
        foreach(var target in balls) if(target && target.TryGetComponent<BallEnchantmentVisuals>(out var visuals)) visuals.Refresh();
        HandChanged?.Invoke();
    }

    // timeScale 0 freezes physics, movement and every scaled timer; card input must use unscaled time.
    void BeginWaiting()
    {
        state = RunState.Waiting;
        countdown = launchWait;
        Time.timeScale = 0f;
        if (!ball) ball = CreateBall();
        ball.isKinematic = true;
        ball.position = board.TransformPoint(launchLocal);
        ball.transform.position = ball.position;
    }

    void Launch()
    {
        state = RunState.Playing;
        Time.timeScale = 1f;
        LaunchBall(ball);
    }

    Rigidbody CreateBall()
    {
        var added = Instantiate(ballTemplate);
        added.name = "Ball";
        added.position = board.TransformPoint(launchLocal);
        added.gameObject.SetActive(true);
        balls.Add(added);
        AttachBallVisuals(added);
        return added;
    }

    void LaunchBall(Rigidbody launched)
    {
        var returnState = launched.GetComponent<BallReturnState>();
        if (!returnState) returnState = launched.gameObject.AddComponent<BallReturnState>();
        var band = board.GetComponentInChildren<ReturnBand>();
        returnState.BeginLaunch(band ? band.returnsPerLaunch : 0);
        launched.position = board.TransformPoint(launchLocal);
        launched.isKinematic = false;
        launched.angularVelocity = Vector3.zero;
        var dir = Quaternion.Euler(0f, Random.Range(-launchBiasDegrees, launchBiasDegrees), 0f) * Vector3.forward;
        launched.linearVelocity = board.TransformDirection(dir) * launchSpeed;
    }

    public void SetPaused(bool value)
    {
        paused = value;
        Time.timeScale = !paused && state == RunState.Playing ? 1f : 0f;
    }

    public void Restart()
    {
        Debug.Log("Luck v0.3 run stats (before restart): "+Metrics.Summary());
        Time.timeScale = 1f; // survives scene load
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public bool UseCard(int slot)
    {
        if(!IsPlaying || resolving || slot<0 || slot>=HandSize || hand[slot]==null) return false;
        var card=hand[slot];
        int lockedLucky=currentNumber;
        bool wasFull=IsHandFull;
        // Snapshot all living enemies BEFORE removal and drawing Q.
        var snapshot=EquipmentResolver.Targets(this);
        resolving=true;
        try {
            SlotReplaced?.Invoke(slot);
            hand[slot]=null;
            CompactHand();
            if(wasFull || double.IsPositiveInfinity(nextRefillAt)) nextRefillAt=CombatTime+Mathf.Max(.01f,luck.refillInterval);
            nextRefillAt+=Mathf.Max(0,luck.playRefillDelay);
            LastPlayedNumber=card.number; LastLockedLucky=lockedLucky;
            LastPlayWasLucky=card.number==lockedLucky;
            int quality=NextOutcome(LastPlayWasLucky);
            LastOutcome=quality;
            var plan=luck.Plan(card.equipment,quality);
            var targets=new List<Enemy>();
            if(quality>0) for(int i=0;i<snapshot.Count;i++)
                if(plan.allTargets || i<plan.targetCount) targets.Add(snapshot[i]);
            LastCast=new SkillCast(card,quality,plan,targets);
            LastTargetCount=targets.Count; LastEventKills=0;
            Metrics.plays++;
            if(quality<0) {
                Metrics.shortCircuits++;
                int type=(int)card.equipment;
                shortCircuitUntil[type]=System.Math.Max(shortCircuitUntil[type],CombatTime+luck.ShortCircuitDuration(quality));
            } else if(quality==0) Metrics.misfires++;
            else {
                if(quality<20) Metrics.lowQuality++; else if(quality<60) Metrics.mediumQuality++; else Metrics.highQuality++;
                LastCast.FireWave(this);
                if(!LastCast.Complete) casts.Add(LastCast);
            }
            RefreshBallEnchantments();
            if(Hud.Instance) Hud.Instance.ShowCast(LastCast,LastPlayWasLucky);
        } finally { resolving=false; }
        return true;
    }

    // Keep the dragged card attached to its new slot when another card is used.
    public event System.Action<int, int> CardShifted;

    void CompactHand()
    {
        int next = 0;
        for (int i = 0; i < HandSize; i++)
        {
            if (hand[i] == null) continue;
            if (i != next)
            {
                hand[next] = hand[i];
                hand[i] = null;
                CardShifted?.Invoke(i, next);
            }
            next++;
        }
    }

    // Raised before a slot changes, so a drag cannot submit a different card.
    public event System.Action<int> SlotReplaced;

    public Card GetCard(int slot) => slot >= 0 && slot < HandSize ? hand[slot] : null;
    public int CurrentNumber => currentNumber;
    public bool IsPlaying => state == RunState.Playing && !paused;
    public bool IsWaiting => state == RunState.Waiting;
    public bool IsGameOver => state == RunState.GameOver;
    public bool Paused => paused;
    public float Countdown => countdown;
    public float ActiveTime => activeTime;
    public long Score => score;
    public int Lives => lives;
    public int Energy => 0; // Compatibility for existing return-structure checks.

    double combatTime, nextLuckyAt, nextRefillAt=double.PositiveInfinity;
    readonly double[] shortCircuitUntil=new double[3];
    readonly List<SkillCast> casts=new List<SkillCast>();
    bool resolving;
    public double CombatTime => combatTime;
    public CombatVfx Vfx { get; private set; }
    public readonly RunTelemetry Metrics=new RunTelemetry();
    public event System.Action HandChanged;
    public int CardCount { get { int count=0; if(hand!=null) foreach(var c in hand) if(c!=null) count++; return count; } }
    public bool IsHandFull => CardCount==HandSize;
    public int HeldCount(Card.Equipment type) { int count=0; foreach(var c in hand) if(c!=null && c.equipment==type) count++; return count; }
    public int HeldCount(Card.Equipment type,int number) {
        int count=0;
        foreach(var c in hand) if(c!=null && c.equipment==type && c.number==number) count++;
        return count;
    }
    public int EnabledCount(Card.Equipment type,int number) => ShortCircuitRemaining(type)>0 ? 0 : HeldCount(type,number);
    public int EnabledCount(Card.Equipment type) => ShortCircuitRemaining(type)>0 ? 0 : HeldCount(type);
    public float ShortCircuitRemaining(Card.Equipment type) => (float)System.Math.Max(0,shortCircuitUntil[(int)type]-combatTime);
    public float RefillRemaining => IsHandFull ? 0 : (float)System.Math.Max(0,nextRefillAt-combatTime);
    public float RefillAfterPlay => (IsHandFull ? Mathf.Max(.01f,luck.refillInterval) : RefillRemaining)+Mathf.Max(0,luck.playRefillDelay);
    public float LuckyRemaining => (float)System.Math.Max(0,nextLuckyAt-combatTime);
    public int? LastOutcome { get; private set; }
    public int LastPlayedNumber { get; private set; }
    public int LastLockedLucky { get; private set; }
    public bool LastPlayWasLucky { get; private set; }
    public int LastTargetCount { get; private set; }
    public int LastEventKills { get; private set; }
    public SkillCast LastCast { get; private set; }
    public int PendingCasts => casts.Count;
    public string EnchantmentSummary(Card.Equipment type,int count)
    {
        if(count<=0) return "Enchantment lost";
        if(type==Card.Equipment.Arc) return $"{luck.ArcTargets(count)} other matching foes in {luck.passiveArcRadius:0.#}m: {luck.passiveArcDamage} damage each";
        if(type==Card.Equipment.Spark) return $"Ignite matching hit target + {luck.ExtraSparkTargets(count)} nearby matches";
        return $"Matching foes in {luck.ShockRadius(count):0.##}m shockwave";
    }
    public void RegisterBallHit(Enemy direct,Vector3 point)
    {
        if(!IsPlaying || resolving) return;
        Metrics.validBallHits++;
        EquipmentResolver.BallHit(this,direct,point);
    }
    // All combat clocks share effective game time and stop together during pause / launch wait.
    public void TickGameplay(float elapsed)
    {
        if(!IsPlaying || resolving || elapsed<=0 || float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return;
        double end=combatTime+elapsed;
        const double epsilon=.000001;
        while(combatTime+epsilon<end && IsPlaying) {
            var enemies=EquipmentResolver.Targets(this);
            double next=System.Math.Min(end,nextLuckyAt);
            if(!IsHandFull) next=System.Math.Min(next,nextRefillAt);
            foreach(var cast in casts) next=System.Math.Min(next,cast.NextWaveAt);
            foreach(var e in enemies) next=System.Math.Min(next,e.NextBurnTickAt);
            next=System.Math.Max(combatTime,next);
            Metrics.Tick((float)(next-combatTime),CardCount);
            combatTime=next; activeTime=(float)combatTime;
            foreach(var e in enemies) if(e.Alive) e.TickStatus();
            for(int i=casts.Count-1;i>=0;i--) {
                var cast=casts[i];
                if(cast.NextWaveAt<=combatTime+epsilon) cast.FireWave(this);
                if(cast.Complete) casts.RemoveAt(i);
            }
            if(nextLuckyAt<=combatTime+epsilon) {
                currentNumber=(currentNumber-1+Random.Range(1,6))%6+1;
                nextLuckyAt=combatTime+Mathf.Max(.01f,luck.luckyRefreshInterval);
            }
            if(!IsHandFull && nextRefillAt<=combatTime+epsilon) {
                AddCard(Card.Random());
                nextRefillAt=IsHandFull ? double.PositiveInfinity : combatTime+Mathf.Max(.01f,luck.refillInterval);
            }
        }
        RefreshBallEnchantments();
    }
    bool AddCard(Card card)
    {
        if(card==null || IsHandFull) return false;
        for(int i=0;i<HandSize;i++) if(hand[i]==null) {
            SlotReplaced?.Invoke(i); hand[i]=card;
            if(IsHandFull) nextRefillAt=double.PositiveInfinity;
            else if(double.IsPositiveInfinity(nextRefillAt)) nextRefillAt=combatTime+Mathf.Max(.01f,luck.refillInterval);
            RefreshBallEnchantments(); return true;
        }
        return false;
    }

    int NextOutcome(bool lucky)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (fixedNextOutcome.HasValue) {
            int value = fixedNextOutcome.Value;
            fixedNextOutcome = null;
            return value;
        }
#endif
        return luck.Roll(lucky);
    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    int? fixedNextOutcome;
    public int? FixedNextOutcome => fixedNextOutcome;
    public bool DebugSetLuckyNumber(int number)
    {
        if (number < 1 || number > 6 || IsGameOver) return false;
        currentNumber = number;
        return true; // No attacks and no timer reset.
    }
    public bool DebugFixNextOutcome(int result)
    {
        if (result < -10 || result > 100) return false;
        fixedNextOutcome = result;
        return true;
    }
    public void DebugClearFixedOutcome() => fixedNextOutcome = null;
    public bool DebugAddCard(int number, Card.Equipment equipment) => !IsGameOver && AddCard(new Card(number, equipment));
#endif

    // Number is selected once at spawn and remains independent of health.
    public Enemy SpawnEnemy(Enemy.Kind kind, int target, Vector3 local) => Enemy.Create(board, kind, target, local);

    public long OnNormalKill(AttackRecord source = null)
    {
        long reward = Mathf.Max(0, normalKillPoints);
        if(IsPlaying) {
            score=AddClamped(score,reward); Metrics.kills++;
            if(source!=null) {
                source.Kills++; LastEventKills=source.Kills;
                if(source.Kills>=2 && Hud.Instance) Hud.Instance.ShowMultiKill(source);
            }
        }
        return reward;
    }

    public static long AddClamped(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
    public static long MultiplyClamped(long a, int m) => m <= 0 ? 0 : a > long.MaxValue / m ? long.MaxValue : a * m;

    public void LoseLife() { Metrics.breaches++; LoseLives(breachLifeCost,"Enemy breach"); }

    void LoseLives(int amount,string cause)
    {
        if (!IsPlaying) return;
        lives -= Mathf.Max(0, amount);
        if (lives > 0) return;
        state = RunState.GameOver;
        Metrics.deathCause=cause;
        Debug.Log("Luck v0.3 run stats: "+Metrics.Summary());
        Time.timeScale = 0f;
        foreach (var activeBall in balls) if (activeBall) activeBall.isKinematic = true;
    }

    public void OnDrain(Rigidbody drained)
    {
        if (!IsPlaying || !drained || !balls.Remove(drained)) return;
        drained.gameObject.SetActive(false);
        Destroy(drained.gameObject);
        if (balls.Count > 0) { ball = balls[0]; return; }
        ball = null;
        Metrics.drains++;
        LoseLives(drainLifeCost,"Ball drain");
        if (state == RunState.Playing) BeginWaiting();
    }
}
