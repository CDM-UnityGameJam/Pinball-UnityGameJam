using Pinball.Core;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Pinball.Gameplay
{
    // Composition root: one per game scene. Creates the systems in a known order, wires them together and
    // exposes them read-only. Nothing else creates systems or uses static instances.
    [DefaultExecutionOrder(-100)]
    public sealed class GameContext : MonoBehaviour
    {
        [Required, AssetsOnly] public GameConfig config;
        [Required, SceneObjectsOnly] public BoardRoot board;
        [Required, AssetsOnly] public Ball ballPrefab;
        [Tooltip("Parent for spawned balls; defaults to the board.")]
        public Transform ballParent;

        [Title("Input")]
        public InputActionReference pauseAction;
        public InputActionReference restartAction;

        public RunStateMachine Run { get; private set; }
        public CombatClock Clock { get; private set; }
        public BallSystem Balls { get; private set; }
        public EnemyRegistry Enemies { get; private set; }
        public EnemySpawner Spawner { get; private set; }
        public EnemyMarch March { get; private set; }
        public ScoreSystem Score { get; private set; }
        public RunStats Stats { get; private set; }
        public HandSystem Hand { get; private set; }
        public CastScheduler Casts { get; private set; }
        public CombatResolver Combat { get; private set; }
        public MultiballSystem Multiball { get; private set; }
        public BallRelaunch Relaunch { get; private set; }
        public CombatFeedback Feedback { get; } = new CombatFeedback();
        public IRandom Random { get; private set; } = UnityRandom.Shared;
        public BoardRoot Board => board;

        public bool IsInitialized => Run != null;

        // Tests and tools: when true, nothing advances the combat clock, marches enemies or spawns on its own;
        // call Clock.Advance / March.Step yourself. Physics still runs.
        public bool DriveTimeManually { get; set; }

        void Awake()
        {
            Run = new RunStateMachine(config.lives, config.launchCountdown);
            Clock = new CombatClock();
            Balls = new BallSystem(this, ballPrefab, ballParent ? ballParent : board.transform);
            Stats = new RunStats();
            // Every combat step, in this order (the v0.6 TickGameplay): score (time points, combo timeout),
            // spawning, then hand stats and natural charge.
            Score = new ScoreSystem(this);
            Enemies = new EnemyRegistry(board);
            Spawner = new EnemySpawner(this, board.transform);
            March = new EnemyMarch(this);
            Casts = new CastScheduler(this);
            Hand = new HandSystem(this);
            Combat = new CombatResolver(this);
            Multiball = new MultiballSystem(this);
            Relaunch = new BallRelaunch(this);

            // When several deadlines fall on the same moment they fire in this order (old TickGameplay):
            // enemy statuses (front first), skill waves, then a replacement ball.
            Clock.Register(Enemies);
            Clock.Register(Casts);
            Clock.Register(Relaunch);

            Run.LivesLost += OnLivesLost;
            Run.StateChanged += _ => ApplyTimeFlow();
            Run.PauseChanged += _ => ApplyTimeFlow();
            Run.LaunchDue += LaunchWaitingBall;
            board.drain.Drained += OnDrained;
            foreach (var lane in board.returnLanes) if (lane) lane.Initialize(this);
        }

        void OnEnable()
        {
            Enable(pauseAction);
            Enable(restartAction);
        }

        void Start()
        {
            BeginLaunch();
            Spawner.SpawnOpening();
        }

        void Update()
        {
            if (Pressed(restartAction)) { Restart(); return; }
            if (Pressed(pauseAction)) Run.TogglePause();
            // Clamp load hitches so they can't eat the whole launch countdown.
            Run.Tick(Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime));
            if (!DriveTimeManually) Multiball.Tick(Time.deltaTime);
        }

        // Combat clock first, then enemy movement (the old FixedUpdate order).
        void FixedUpdate()
        {
            if (DriveTimeManually) return;
            Clock.Advance(Time.fixedDeltaTime);
            March.Step(Time.fixedDeltaTime);
        }

        void OnDestroy()
        {
            if (board && board.drain) board.drain.Drained -= OnDrained;
            Time.timeScale = 1f;
        }

        // Physics and the combat clock only run while playing; the countdown uses unscaled time.
        void ApplyTimeFlow()
        {
            Clock.Running = Run.IsPlaying;
            Time.timeScale = Run.IsPlaying ? 1f : 0f;
            if (Run.State != RunState.GameOver) return;
            Balls.FreezeAll();
            Stats.deathCause = Run.EndCause;
            Debug.Log("Run stats: " + Stats.Summary());
        }

        void BeginLaunch()
        {
            if (Balls.Count == 0) Balls.Spawn(board.launchPoint.Position);
            else foreach (var ball in Balls.Active) ball.Park(board.launchPoint.Position);
            Run.BeginLaunch();
            ApplyTimeFlow();
        }

        void LaunchWaitingBall()
        {
            foreach (var ball in Balls.Active)
            {
                if (!ball.Body.isKinematic || ball.InReturnLane || Multiball.IsHeld(ball)) continue;
                Launch(ball);
            }
        }

        // A new ball straight from the launch point (replacement after a drain).
        internal Ball LaunchNewBall()
        {
            var ball = Balls.Spawn(board.launchPoint.Position);
            Launch(ball);
            return ball;
        }

        void Launch(Ball ball)
        {
            var direction = board.launchPoint.Direction(config.launchSpreadDegrees, Random.Value());
            ball.Launch(board.launchPoint.Position, direction * config.launchSpeed);
        }

        // Drains never cost lives. When the last ball goes, the run keeps playing and a replacement launches
        // after the countdown, unless a multiball reward is already on its way.
        void OnDrained(Ball ball)
        {
            if (!Run.IsPlaying || !Balls.Remove(ball)) return;
            if (Balls.Count > 0) return;
            Stats.drains++;
            if (Multiball.Pending > 0) return;
            Relaunch.Begin(config.launchCountdown);
        }

        // Every life lost clears the field (no rewards); the last one also pays the running combo.
        void OnLivesLost(int amount)
        {
            Enemies.DespawnAll();
            if (Run.Lives <= 0)
            {
                Score.FinishCombo();
                Relaunch.Cancel();
            }
        }

        [Button, DisableInEditorMode]
        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        static void Enable(InputActionReference reference)
        {
            if (reference && reference.action != null) reference.action.Enable();
        }

        static bool Pressed(InputActionReference reference) =>
            reference && reference.action != null && reference.action.WasPressedThisFrame();
    }
}
