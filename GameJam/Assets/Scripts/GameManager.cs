using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    enum RunState { Waiting, Playing, GameOver }

    const int LuckyNumber = Card.Lucky;
    public const int EnergyPerCard = 100;
    const float LaunchWait = 3f;
    public const int HandSize = 5;
    const int StartCards = 3;
    const int KillPoints = 50, SurvivalPoints = 5;
    const float SurvivalInterval = 5f;

    public static GameManager Instance;

    public Transform board;
    public Rigidbody ball;
    // Between and just below the flipper tips (tips sit near z -8.3, x ±0.6).
    public Vector3 launchLocal = new Vector3(0f, 0.3f, -9f);
    public float launchSpeed = 18f;
    public float launchBiasDegrees = 8f;
    public float gravity = 60f;
    public int maxLives = 3;
    [Min(1), Tooltip("匹配秒杀时，本次击杀奖励的倍数（普通和特殊怪物共用）。")]
    public int matchKillMultiplier = 10;
    public int MatchKillMultiplier => Mathf.Max(1, matchKillMultiplier);

    RunState state;
    bool paused;
    float countdown, activeTime, survivalTimer;
    int lives, energy, currentNumber = LuckyNumber;
    long score; // long: special kills multiply the total repeatedly
    readonly Card?[] hand = new Card?[HandSize]; // null = empty slot

    void Awake()
    {
        Instance = this;
        Time.fixedDeltaTime = 0.01f;
        Physics.gravity = Vector3.down * gravity;
    }

    void Start()
    {
        lives = maxLives;
        for (int i = 0; i < StartCards; i++) hand[i] = Card.Random();
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
            activeTime += Time.deltaTime;
            survivalTimer += Time.deltaTime;
            while (survivalTimer >= SurvivalInterval)
            {
                survivalTimer -= SurvivalInterval;
                score = AddClamped(score, SurvivalPoints);
            }
        }
        else if (state == RunState.Waiting)
        {
            // Clamped so a load hitch cannot eat the 3 s wait.
            countdown -= Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime);
            if (countdown <= 0f) Launch();
        }
    }

    // timeScale 0 freezes physics, movement and every scaled timer; card input must use unscaled time.
    void BeginWaiting()
    {
        state = RunState.Waiting;
        countdown = LaunchWait;
        Time.timeScale = 0f;
        ball.isKinematic = true;
        ball.position = board.TransformPoint(launchLocal);
        ball.transform.position = ball.position;
    }

    void Launch()
    {
        state = RunState.Playing;
        Time.timeScale = 1f;
        ball.isKinematic = false;
        var dir = Quaternion.Euler(0f, Random.Range(-launchBiasDegrees, launchBiasDegrees), 0f) * Vector3.forward;
        ball.linearVelocity = board.TransformDirection(dir) * launchSpeed;
    }

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

    // Consumes the card; its result replaces the current number until the next card is used.
    public bool UseCard(int slot)
    {
        if (paused || state == RunState.GameOver || hand[slot] == null) return false;
        currentNumber = hand[slot].Value.Apply(LuckyNumber);
        hand[slot] = null;
        return true;
    }

    // Insertion move: the card lands in slot `to` and the slots between shift by one, empties included.
    public void MoveCard(int from, int to)
    {
        if (from == to || paused || state == RunState.GameOver) return;
        var c = hand[from];
        if (from < to) for (int i = from; i < to; i++) hand[i] = hand[i + 1];
        else for (int i = from; i > to; i--) hand[i] = hand[i - 1];
        hand[to] = c;
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

        var card = Card.Draw(currentNumber, hand, targets);
        int slot = HandSize - 1; // full hand: replace slot 5
        for (int i = 0; i < HandSize; i++) if (hand[i] == null) { slot = i; break; }
        if (hand[slot] != null) SlotReplaced?.Invoke(slot);
        hand[slot] = card;
    }

    // The spawner decides kind, target and board-local position.
    public Enemy SpawnEnemy(Enemy.Kind kind, int target, Vector3 local) => Enemy.Create(board, kind, target, local);

    public long OnNormalKill(bool matched = false)
    {
        long reward = KillPoints * (matched ? (long)MatchKillMultiplier : 1L);
        if (state == RunState.Playing) score = AddClamped(score, reward);
        return reward;
    }

    // 111 / 222 / 333 multiply the total by 10 / 2 / 3; no flat points.
    public void OnSpecialKill(int multiplier, bool matched = false)
    {
        if (state == RunState.Playing) score = AddClamped(score, SpecialKillReward(score, multiplier, matched, MatchKillMultiplier));
    }

    // Bonus applies to this kill's gain, preserving the existing total.
    public static long SpecialKillReward(long currentScore, int multiplier, bool matched, int matchMultiplier = 10) =>
        MultiplyClamped(MultiplyClamped(currentScore, multiplier - 1), matched ? Mathf.Max(1, matchMultiplier) : 1);

    // Repeated x10 kills could pass long.MaxValue, so scores saturate instead of wrapping negative.
    public static long AddClamped(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
    public static long MultiplyClamped(long a, int m) => a > long.MaxValue / m ? long.MaxValue : a * m;

    public void LoseLife()
    {
        if (state != RunState.Playing) return;
        lives--;
        if (lives > 0) return;
        state = RunState.GameOver;
        Time.timeScale = 0f;
        ball.isKinematic = true;
    }

    public void OnDrain()
    {
        if (state != RunState.Playing) return;
        LoseLife();
        if (state == RunState.Playing) BeginWaiting();
    }
}
