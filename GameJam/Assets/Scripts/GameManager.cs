using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    enum RunState { Waiting, Playing, GameOver }

    [Header("Run / Cards / Scoring")]
    [Min(1)] public int handSize = 5;
    [Min(1)] public int energyPerCard = 100;
    [Min(0f)] public float launchWait = 3f;
    [Min(0)] public int normalKillPoints = 50;
    [Min(0)] public int survivalPoints = 5;
    [Min(0.01f)] public float survivalInterval = 5f;
    [Min(0f)] public float matchBoostDuration = 10f;
    [Min(1)] public int extraBallsPerCard = 1;
    [Min(0.01f)] public float queuedBallInterval = 1f;
    [Min(0)] public int breachLifeCost = 1;
    [Min(0)] public int drainLifeCost = 1;
    [Min(0.001f)] public float physicsStep = 0.01f;
    public CardTuning cards = new CardTuning();
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
    public int EnergyPerCard => Mathf.Max(1, energyPerCard);
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
    [Min(1), Tooltip("Reward multiplier for a normal-enemy match kill.")]
    public int matchKillMultiplier = 10;
    public int MatchKillMultiplier => Mathf.Max(1, matchKillMultiplier);
    [Min(1)] public int specialMatchKillMultiplier = 10;
    [Min(0.01f)] public float specialSpawnInterval = 1f;
    [Min(0.01f)] public float bumperSpawnInterval = 1f;
    [Min(0.01f)] public float bumperDescendTime = 40f;
    [Header("Functional Card Chances (Percent)")]
    [Range(0f, 100f), Tooltip("Independent chance of a timed MATCH card. No cards are dealt at the start. 3 means 3%.")]
    public float matchCardChancePercent = 3f;
    [Range(0f, 100f), Tooltip("Independent chance of a BALL +1 extra-ball card. 3 means 3%.")]
    public float extraBallCardChancePercent = 3f;

    RunState state;
    bool paused;
    float countdown, activeTime, survivalTimer;
    int lives, energy, currentNumber;
    long score; // long: special kills multiply the total repeatedly
    Card?[] hand; // null = empty slot
    readonly List<Rigidbody> balls = new List<Rigidbody>();
    Rigidbody ballTemplate;
    float matchBoostLeft, queuedLaunchTimer;
    int queuedBalls;
    public IReadOnlyList<Rigidbody> Balls => balls;
    public int BallCount => balls.Count;
    public int QueuedBalls => queuedBalls;
    public float MatchBoostLeft => matchBoostLeft;
    public bool HasMatchBoost => matchBoostLeft > 0f;

    void Awake()
    {
        Instance = this;
        hand = new Card?[Mathf.Max(1, handSize)];
        currentNumber = cards.initialNumber;
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
        gameObject.AddComponent<Spawner>();
        new GameObject("Hud").AddComponent<Hud>();
        BeginWaiting();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (state != RunState.GameOver && kb != null && kb.rKey.wasPressedThisFrame) SetPaused(!paused);
        if (paused) return;

        if (state == RunState.Playing)
        {
            matchBoostLeft = Mathf.Max(0f, matchBoostLeft - Time.deltaTime);
            if (queuedBalls > 0)
            {
                queuedLaunchTimer -= Time.deltaTime;
                if (queuedLaunchTimer <= 0f)
                {
                    queuedBalls--;
                    LaunchBall(CreateBall());
                    queuedLaunchTimer = Mathf.Max(0.01f, queuedBallInterval);
                }
            }
            activeTime += Time.deltaTime;
            survivalTimer += Time.deltaTime;
            while (survivalTimer >= Mathf.Max(0.01f, survivalInterval))
            {
                survivalTimer -= Mathf.Max(0.01f, survivalInterval);
                score = AddClamped(score, survivalPoints);
            }
        }
        else if (state == RunState.Waiting)
        {
            // Clamp load hitches so they cannot consume the entire launch wait.
            countdown -= Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime);
            if (countdown <= 0f) Launch();
        }
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
        queuedLaunchTimer = Mathf.Max(0.01f, queuedBallInterval);
    }

    Rigidbody CreateBall()
    {
        var added = Instantiate(ballTemplate);
        added.name = "Ball";
        added.position = board.TransformPoint(launchLocal);
        added.gameObject.SetActive(true);
        balls.Add(added);
        RefreshSpecialCollisions();
        return added;
    }

    void LaunchBall(Rigidbody launched)
    {
        launched.position = board.TransformPoint(launchLocal);
        launched.isKinematic = false;
        launched.angularVelocity = Vector3.zero;
        var dir = Quaternion.Euler(0f, Random.Range(-launchBiasDegrees, launchBiasDegrees), 0f) * Vector3.forward;
        launched.linearVelocity = board.TransformDirection(dir) * launchSpeed;
    }

    public bool Matches(Enemy enemy) => enemy.target == currentNumber ||
        (enemy.kind == Enemy.Kind.Normal && HasMatchBoost);

    public void SetPaused(bool value)
    {
        paused = value;
        Time.timeScale = !paused && state == RunState.Playing ? 1f : 0f;
    }

    public void Restart()
    {
        Time.timeScale = 1f; // survives scene load
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Functional cards preserve the current number.
    public bool UseCard(int slot)
    {
        if (slot < 0 || slot >= HandSize || paused || state == RunState.GameOver || hand[slot] == null) return false;
        var card = hand[slot].Value;
        if (card.kind == Card.Kind.MatchBoost) matchBoostLeft += Mathf.Max(0f, matchBoostDuration);
        else if (card.kind == Card.Kind.ExtraBall)
        {
            if (state == RunState.Waiting) queuedBalls++;
            else LaunchBall(CreateBall());
        }
        else currentNumber = card.Apply(cards.baseNumber);
        SlotReplaced?.Invoke(slot);
        hand[slot] = null;
        CompactHand();
        RefreshSpecialCollisions();
        return true;
    }

    // Insertion move within the occupied hand; empty slots always stay on the right.
    public int MoveCard(int from, int to)
    {
        if (from < 0 || from >= HandSize || hand[from] == null || paused || state == RunState.GameOver) return from;
        int count = 0;
        foreach (var card in hand) if (card != null) count++;
        to = Mathf.Clamp(to, 0, count - 1);
        if (from == to) return from;
        var c = hand[from];
        if (from < to) for (int i = from; i < to; i++) hand[i] = hand[i + 1];
        else for (int i = from; i > to; i--) hand[i] = hand[i - 1];
        hand[to] = c;
        return to;
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

    void RefreshSpecialCollisions()
    {
        foreach (var enemy in FindObjectsByType<Enemy>())
            if (enemy.kind == Enemy.Kind.Special) enemy.RefreshBallCollisions();
    }

    // Raised before a draw overwrites an occupied slot, so a drag on that card can be cancelled.
    public event System.Action<int> SlotReplaced;

    public Card? GetCard(int slot) => hand[slot];
    public int CurrentNumber => currentNumber;
    public bool IsPlaying => state == RunState.Playing && !paused;
    public bool IsWaiting => state == RunState.Waiting;
    public bool IsGameOver => state == RunState.GameOver;
    public bool Paused => paused;
    public float Countdown => countdown;
    public float ActiveTime => activeTime;
    public long Score => score;
    public int Lives => lives;
    public int Energy => energy;

    // Called last in hit resolution, so a kill on this hit already removed that enemy from the coverage check.
    public void AddEnergy(int amount)
    {
        if (state != RunState.Playing) return;
        energy += amount;
        while (energy >= EnergyPerCard)
        {
            energy -= EnergyPerCard;
            DrawCard();
        }
    }

    void DrawCard()
    {
        var targets = new List<int>();
        foreach (var e in FindObjectsByType<Enemy>())
            if (e.kind != Enemy.Kind.Bumper && e.hp > 0 && e.Visible) targets.Add(e.target);

        var card = Card.Reward(currentNumber, hand, targets,
            matchCardChancePercent / 100f, extraBallCardChancePercent / 100f, cards);
        int slot = HandSize - 1; // Full hand: replace the final slot.
        for (int i = 0; i < HandSize; i++) if (hand[i] == null) { slot = i; break; }
        if (hand[slot] != null) SlotReplaced?.Invoke(slot);
        hand[slot] = card;
    }

    // The spawner decides kind, target and board-local position.
    public Enemy SpawnEnemy(Enemy.Kind kind, int target, Vector3 local) => Enemy.Create(board, kind, target, local);

    public long OnNormalKill(bool matched = false)
    {
        long reward = Mathf.Max(0, normalKillPoints) * (matched ? (long)MatchKillMultiplier : 1L);
        if (state == RunState.Playing) score = AddClamped(score, reward);
        return reward;
    }

    // Each configured special target owns its score multiplier.
    public void OnSpecialKill(int multiplier, bool matched = false)
    {
        if (state == RunState.Playing) score = AddClamped(score, SpecialKillReward(score, multiplier, matched, specialMatchKillMultiplier));
    }

    // Bonus applies to this kill's gain, preserving the existing total.
    public static long SpecialKillReward(long currentScore, int multiplier, bool matched, int matchMultiplier = 10) =>
        MultiplyClamped(MultiplyClamped(currentScore, Mathf.Max(1, multiplier) - 1), matched ? Mathf.Max(1, matchMultiplier) : 1);

    // Repeated x10 kills could pass long.MaxValue, so scores saturate instead of wrapping negative.
    public static long AddClamped(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
    public static long MultiplyClamped(long a, int m) => m <= 0 ? 0 : a > long.MaxValue / m ? long.MaxValue : a * m;

    public void LoseLife() => LoseLives(breachLifeCost);

    void LoseLives(int amount)
    {
        if (state != RunState.Playing) return;
        lives -= Mathf.Max(0, amount);
        if (lives > 0) return;
        state = RunState.GameOver;
        queuedBalls = 0;
        matchBoostLeft = 0f;
        Time.timeScale = 0f;
        foreach (var activeBall in balls) if (activeBall) activeBall.isKinematic = true;
    }

    public void OnDrain(Rigidbody drained)
    {
        if (state != RunState.Playing || !drained || !balls.Remove(drained)) return;
        drained.gameObject.SetActive(false);
        Destroy(drained.gameObject);
        if (balls.Count > 0) { ball = balls[0]; return; }
        ball = null;
        queuedBalls = 0;
        matchBoostLeft = 0f;
        LoseLife();
        if (state == RunState.Playing) BeginWaiting();
    }
}
