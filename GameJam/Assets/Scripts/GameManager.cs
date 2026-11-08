using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    enum RunState { Waiting, Playing, GameOver }

    const int LuckyNumber = 37;
    const float LaunchWait = 3f;
    const int HandSize = 5, StartCards = 3;
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
        BeginWaiting();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (state != RunState.GameOver && kb != null && kb.rKey.wasPressedThisFrame) SetPaused(!paused);
        if (paused) return;

        // shortcut: keys 1-5 use a card until system 8 adds drag-to-playfield.
        if (kb != null && state != RunState.GameOver)
            for (int i = 0; i < HandSize; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) UseCard(i);

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

    void SetPaused(bool value)
    {
        paused = value;
        Time.timeScale = !paused && state == RunState.Playing ? 1f : 0f;
    }

    void Restart()
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

    public void OnNormalKill()
    {
        if (state == RunState.Playing) score = AddClamped(score, KillPoints);
    }

    // 111 / 222 / 333 multiply the total by 10 / 2 / 3; no flat points.
    public void OnSpecialKill(int multiplier)
    {
        if (state == RunState.Playing) score = MultiplyClamped(score, multiplier);
    }

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

    void OnGUI()
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(20, 15, 400, 30), "Score: " + score, s);
        GUI.Label(new Rect(20, 45, 400, 30), "Lives: " + lives + " / " + maxLives, s);
        GUI.Label(new Rect(20, 75, 400, 30), "Time: " + FormatTime(activeTime), s);
        GUI.Label(new Rect(20, Screen.height - 75, 400, 30), LuckyNumber + " > " + currentNumber, s);
        GUI.Label(new Rect(20, Screen.height - 45, 400, 30), "Energy: " + energy + " / 100", s);
        for (int i = 0; i < HandSize; i++)
        {
            var c = hand[i];
            var text = (i + 1) + "\n" + (c == null ? "-" : c.Value + "\n= " + c.Value.Apply(LuckyNumber));
            GUI.Box(new Rect(Screen.width / 2f - 275 + i * 110, Screen.height - 110, 100, 90), text, new GUIStyle(GUI.skin.box) { fontSize = 20 });
        }

        var big = new GUIStyle(s) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
        var center = new Rect(0, Screen.height / 2f - 110, Screen.width, 160);
        var button = new GUIStyle(GUI.skin.button) { fontSize = 24 };
        float midX = Screen.width / 2f, buttonY = Screen.height / 2f + 60;

        if (state == RunState.GameOver)
        {
            GUI.Box(center, "");
            GUI.Label(center, "GAME OVER\n<size=24>Score " + score + "   Time " + FormatTime(activeTime) + "</size>", new GUIStyle(big) { richText = true });
            if (GUI.Button(new Rect(midX - 80, buttonY, 160, 50), "Restart", button)) Restart();
        }
        else if (paused)
        {
            GUI.Box(center, "");
            GUI.Label(center, "Restart?", big);
            if (GUI.Button(new Rect(midX - 170, buttonY, 160, 50), "Yes", button)) Restart();
            if (GUI.Button(new Rect(midX + 10, buttonY, 160, 50), "No", button)) SetPaused(false);
        }
        else if (state == RunState.Waiting) GUI.Label(center, Mathf.CeilToInt(countdown).ToString(), big);
    }

    static string FormatTime(float t) => Mathf.FloorToInt(t / 60f) + ":" + Mathf.FloorToInt(t % 60f).ToString("00");
}
