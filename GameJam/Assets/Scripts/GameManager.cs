using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    enum RunState { Waiting, Playing, GameOver }

    const int LuckyNumber = 37;
    const float LaunchWait = 3f;

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
    float countdown, activeTime;
    int lives, energy, currentNumber = LuckyNumber;
    long score; // long: special kills multiply the total repeatedly

    void Awake()
    {
        Instance = this;
        Time.fixedDeltaTime = 0.01f;
        Physics.gravity = Vector3.down * gravity;
    }

    void Start()
    {
        lives = maxLives;
        BeginWaiting();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (state != RunState.GameOver && kb != null && kb.rKey.wasPressedThisFrame) SetPaused(!paused);
        if (paused) return;

        if (state == RunState.Playing) activeTime += Time.deltaTime;
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
