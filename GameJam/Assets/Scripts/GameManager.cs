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
    [Header("Multiball")]
    [Min(1), Tooltip("Most balls on the table at once, capped at three including pending rewards.")]
    public int maxBalls = 3;
    [Tooltip("One attack killing two or more enemies launches an extra ball.")]
    public bool ballOnMultiKill = true;
    [Min(0f), Tooltip("Seconds an extra ball is held at its entry point before it drops in.")]
    public float extraBallHold = 1f;
    [Tooltip("Board-local entry point for extra balls: top centre, above the enemies. Y is replaced by the floor height.")]
    public Vector3 extraBallLocal = new Vector3(0f, 0f, 11f);
    [Min(0f), Tooltip("Speed an extra ball drops in at, down the table.")]
    public float extraBallDropSpeed = 4f;
    int pendingBalls; // earned but not yet created
    bool launchingBalls;
    [Header("Enemy Timing")]
    [Min(0.1f), Tooltip("Seconds between normal-enemy spawn attempts.")]
    public float spawnInterval = 5f;
    [Min(0.1f), Tooltip("Seconds for normal enemies to reach the danger line.")]
    public float enemyDescendTime = 40f;
    RunState state;
    bool paused;
    float countdown, activeTime;
    int lives;
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
        // Transform too: activation re-reads the body pose from the transform (the template's scene position).
        added.transform.position = added.position = board.TransformPoint(launchLocal);
        added.gameObject.SetActive(true);
        balls.Add(added);
        AttachBallVisuals(added);
        return added;
    }

    // Queues an extra ball, up to maxBalls including queued ones. Drained extras cost no life (see OnDrain).
    public bool AddBall()
    {
        if (!IsPlaying || balls.Count + pendingBalls >= Mathf.Clamp(maxBalls, 1, 3)) return false;
        pendingBalls++;
        if (!launchingBalls) StartCoroutine(LaunchPendingBalls());
        return true;
    }

    // Extra balls drop in from the top: a bottom launch only reaches mid-table and falls straight back
    // between the flippers while the player is busy with another ball. Dropping from the top passes through
    // the enemies and reaches the flippers with time to react. One at a time so balls never start overlapped.
    System.Collections.IEnumerator LaunchPendingBalls()
    {
        launchingBalls = true;
        var surface = board.GetComponentInChildren<OuterRingSurface>();
        var entry = extraBallLocal;
        entry.y = (surface ? surface.HeightAt(entry.x, entry.z) : 0f) + spawning.ballRadius + .02f;
        var entryWorld = board.TransformPoint(entry);
        while (pendingBalls > 0)
        {
            var held = CreateBall();
            pendingBalls--; // now counted in balls
            held.isKinematic = true;
            held.transform.position = held.position = entryWorld;
            if (Hud.Instance) Hud.Instance.Popup(entryWorld, "+BALL", Hud.Gold);
            // Scaled time: the hold freezes with pause and the launch wait.
            yield return new WaitForSeconds(extraBallHold);
            while (held && (!IsPlaying || !SpotClear(entryWorld, held))) { if (IsGameOver) yield break; yield return new WaitForSeconds(.1f); }
            if (!held) continue;
            LaunchBall(held); // shared launch bookkeeping, then replace pose and velocity
            held.position = entryWorld;
            var dir = Quaternion.Euler(0f, Random.Range(-launchBiasDegrees, launchBiasDegrees), 0f) * Vector3.back;
            held.linearVelocity = board.TransformDirection(dir) * extraBallDropSpeed;
        }
        launchingBalls = false;
    }

    bool SpotClear(Vector3 world, Rigidbody self)
    {
        foreach (var hit in Physics.OverlapSphere(world, spawning.ballRadius * 2f + .1f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.attachedRigidbody && hit.attachedRigidbody != self && hit.CompareTag("Player")) return false;
        return true;
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
        Debug.Log("Luck v0.6 run stats (before restart): "+Metrics.Summary());
        Time.timeScale = 1f; // survives scene load
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    double combatTime;
    readonly RechargeAccount recharge=new RechargeAccount();
    readonly List<SkillCast> casts=new List<SkillCast>();
    bool resolving;
    public double CombatTime=>combatTime;
    public CombatVfx Vfx {get;private set;}
    public readonly RunTelemetry Metrics=new RunTelemetry();
    public event System.Action HandChanged;
    public event System.Action<int> SlotReplaced;
    public event System.Action<int,int> CardShifted;
    public event System.Action<DiceBatch> BatchSubmitted;
    public DiceBatch PendingBatch {get;private set;}
    public DiceBatch LastBatch {get;private set;}
    public int LastEventKills {get;private set;}
    public int PendingCasts=>casts.Count;
    public bool IsPlaying=>state==RunState.Playing && !paused;
    public bool IsWaiting=>state==RunState.Waiting;
    public bool IsGameOver=>state==RunState.GameOver;
    public bool Paused=>paused;
    public float Countdown=>countdown;
    public float ActiveTime=>activeTime;
    public long Score=>score;
    public int Lives=>lives;
    public int Energy=>0;
    public int EmpoweredHits {get;private set;}
    public bool CanSubmit=>IsPlaying && !resolving && PendingBatch==null;
    public int CardCount {get {int n=0;foreach(var c in hand)if(c!=null)n++;return n;}}
    public bool IsHandFull=>CardCount==HandSize;
    public Card GetCard(int slot)=>slot>=0 && slot<HandSize?hand[slot]:null;
    public IList<Card> HandSnapshot=>System.Array.AsReadOnly((Card[])hand.Clone());
    public bool ContainsCard(Card card){foreach(var c in hand)if(ReferenceEquals(c,card))return true;return false;}
    public int HeldCount(Card.Equipment type){int n=0;foreach(var c in hand)if(c!=null && c.equipment==type)n++;return n;}
    public int EnabledCount(Card.Equipment type)=>HeldCount(type);
    public int NumberCount(int number){int n=0;foreach(var c in hand)if(c!=null && c.number==number)n++;return n;}
    public double RechargeProgress=>recharge.Progress;
    public double RechargeRequired=>recharge.Required;
    public double RechargeWindowUsed=>recharge.WindowUsed(CombatTime);
    public double RechargeAfterBatch(int count)=>(IsHandFull?luck.rechargeBaseCost:recharge.Required)+count*luck.rechargeCastCost;
    public bool UseCard(int slot)=>UseCards(new Card[]{GetCard(slot)});
    public bool UseCards(IList<Card> selected)
    {
        if(!CanSubmit || !DiceRules.ValidSelection(selected,hand,c=>c.number))return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(fixedDice!=null && fixedDice.Length!=selected.Count)return false;
#endif
        var cards=new Card[selected.Count];selected.CopyTo(cards,0);
        var snapshot=EquipmentResolver.Snapshot(this);
        bool wasFull=IsHandFull;resolving=true;
        try {
            for(int i=0;i<hand.Length;i++)foreach(var c in cards)if(ReferenceEquals(hand[i],c)){SlotReplaced?.Invoke(i);hand[i]=null;break;}
            CompactHand();
            if(wasFull)Metrics.StartRechargeInterval(CombatTime);
            recharge.PlayBatch(wasFull,cards.Length,luck.rechargeBaseCost,luck.rechargeCastCost);
            EmpoweredHits=(int)System.Math.Min(int.MaxValue,(long)EmpoweredHits+cards.Length);
            Metrics.empoweredGranted+=cards.Length;
            RefreshBallEnchantments();
            PendingBatch=new DiceBatch(cards,RollDice(cards.Length),CombatTime+Mathf.Max(0,luck.diceRevealDelay),snapshot,luck);
            LastBatch=PendingBatch;Metrics.RecordBatch(cards.Length);BatchSubmitted?.Invoke(PendingBatch);
        }finally {resolving=false;}
        return true;
    }
    int[] RollDice(int count)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(fixedDice!=null){var result=(int[])fixedDice.Clone();fixedDice=null;return result;}
#endif
        var resultDice=new int[count];for(int i=0;i<count;i++)resultDice[i]=Random.Range(1,7);return resultDice;
    }
    void RevealBatch()
    {
        var batch=PendingBatch;if(batch==null)return;resolving=true;
        try {
            batch.Revealed=true;
            foreach(var skill in batch.Skills){skill.FireWave(this);if(!skill.Complete)casts.Add(skill);}
            Metrics.RecordReveal(batch);PendingBatch=null;
            if(Hud.Instance)Hud.Instance.ShowBatch(batch);
        }finally {resolving=false;}
    }
    void CompactHand()
    {
        int next=0;for(int i=0;i<HandSize;i++)if(hand[i]!=null){if(i!=next){hand[next]=hand[i];hand[i]=null;CardShifted?.Invoke(i,next);}next++;}
    }
    public string EnchantmentSummary(Card.Equipment type,int count)
    {
        if(count<=0)return "Enchantment lost";
        if(type==Card.Equipment.Arc)return $"{luck.ArcTargets(count)} other non-immune foes in {luck.passiveArcRadius:0.#}m";
        if(type==Card.Equipment.Spark)return $"Ignite hit target + {luck.ExtraSparkTargets(count)} nearby non-immune foes";
        return $"Shockwave radius: {luck.ShockRadius(count):0.##}m";
    }
    public void RegisterBallHit(Enemy direct,Vector3 point,bool damageAllowed)
    {
        if(!IsPlaying || resolving)return;
        bool missing=!IsHandFull;Metrics.validBallHits++;
        if(missing)Metrics.RecordRechargeHit(true);
        resolving=true;
        try {
            if(damageAllowed) {
                var source=new AttackRecord("Ball collision and enchantments");
                int damage=EmpoweredHits>0?luck.empoweredCollisionDamage:luck.collisionDamage;
                if(damage>0 && EmpoweredHits>0){EmpoweredHits--;Metrics.empoweredConsumed++;}
                direct.TakeDamage(damage,source);
                Metrics.physicalDamageHits++;
                EquipmentResolver.BallHit(this,direct,point,source);
            } else Metrics.cooldownContacts++;
            // New cards never participate in this contact's damage/enchantments.
            if(missing)EarnCombatRecharge(point,false);
        }finally{resolving=false;}
    }
    public double EarnCombatRecharge(Vector3 point,bool burn)
    {
        double actual=EarnRecharge(luck.rechargeHit,false);
        if(actual>0){if(burn){Metrics.burnChargeEvents++;Metrics.burnCharge+=actual;}else {Metrics.collisionChargeEvents++;Metrics.collisionCharge+=actual;}}
        double shown=System.Math.Floor(actual*100+1e-8)/100;
        if(shown>0 && Hud.Instance)Hud.Instance.Popup(point-board.forward*1.2f,"+"+shown.ToString("0.##")+(burn?" BURN":""),Hud.Blue);
        return actual;
    }
    double EarnRecharge(double amount,bool natural)
    {
        if(!IsPlaying || IsHandFull)return 0;
        recharge.StartMissing(luck.rechargeBaseCost);
        double actual=natural?recharge.EarnNatural(amount):recharge.EarnCombat(CombatTime,amount,luck.rechargeCombatBudget,luck.rechargeCombatMinimum);
        Metrics.RecordRecharge(amount,actual,natural);
        while(!IsHandFull && recharge.Consume(luck.rechargeBaseCost)){AddCard(Card.Random());Metrics.RecordRefill(CombatTime);}
        return actual;
    }
    public void TickGameplay(float elapsed)
    {
        if(!IsPlaying || resolving || elapsed<=0 || float.IsNaN(elapsed) || float.IsInfinity(elapsed))return;
        double end=combatTime+elapsed;const double epsilon=.000001;
        while(combatTime<end && IsPlaying){
            var enemies=EquipmentResolver.Targets(this);double next=System.Math.Min(end,combatTime+.01);
            bool missing=!IsHandFull;double before=combatTime;
            if(PendingBatch!=null)next=System.Math.Min(next,PendingBatch.RevealAt);
            foreach(var cast in casts)next=System.Math.Min(next,cast.NextWaveAt);
            foreach(var e in enemies)next=System.Math.Min(next,e.NextBurnTickAt);
            next=System.Math.Max(combatTime,next);Metrics.Tick((float)(next-combatTime),CardCount);
            if(missing)Metrics.TickRecharge((float)(next-combatTime),enemies.Count);
            combatTime=next;activeTime=(float)combatTime;
            foreach(var e in enemies)if(e.Alive)e.TickStatus();
            if(PendingBatch!=null && PendingBatch.RevealAt<=combatTime+epsilon)RevealBatch();
            for(int i=casts.Count-1;i>=0;i--){var cast=casts[i];if(cast.NextWaveAt<=combatTime+epsilon)cast.FireWave(this);if(cast.Complete)casts.RemoveAt(i);}
            if(missing)EarnRecharge(luck.rechargePerSecond*(combatTime-before),true);
        }
        RefreshBallEnchantments();
    }
    bool AddCard(Card card)
    {
        if(card==null || IsHandFull)return false;
        for(int i=0;i<HandSize;i++)if(hand[i]==null){
            SlotReplaced?.Invoke(i);hand[i]=card;
            if(IsHandFull)recharge.FullHand();else recharge.StartMissing(luck.rechargeBaseCost);
            RefreshBallEnchantments();return true;
        }
        return false;
    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    int[] fixedDice;
    public string FixedDice=>fixedDice==null?"Random":string.Join(", ",fixedDice);
    public bool DebugSetDice(int[] dice)
    {
        if(dice==null || !DiceRules.ValidFaces(dice,dice.Length))return false;
        fixedDice=(int[])dice.Clone();return true;
    }
    public void DebugClearDice()=>fixedDice=null;
    public bool DebugAddCard(int number,Card.Equipment equipment)=>!IsGameOver && AddCard(new Card(number,equipment));
#endif
    public Enemy SpawnEnemy(Card.Equipment element,Vector3 local)=>Enemy.Create(board,Enemy.Kind.Normal,element,local);
    public long OnNormalKill(AttackRecord source = null)
    {
        long reward = Mathf.Max(0, normalKillPoints);
        if(IsPlaying) {
            score=AddClamped(score,reward); Metrics.kills++;
            if(source!=null) {
                source.Kills++; LastEventKills=source.Kills;
                if(source is DiceBatch)Metrics.batchKills++;
                if(source.Kills>=2 && Hud.Instance) Hud.Instance.ShowMultiKill(source);
                if(source.Kills==2 && ballOnMultiKill) AddBall(); // ==2: once per attack, not per extra kill
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
        Debug.Log("Luck v0.6 run stats: "+Metrics.Summary());
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
