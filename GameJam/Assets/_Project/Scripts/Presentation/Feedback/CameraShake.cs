using System.Collections.Generic;
using Pinball.Gameplay;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Presentation
{
    // Shakes the camera when a lot of damage lands in a short time (a big skill wave, a multi-kill burst).
    // Trauma model: every point of damage dealt to enemies adds trauma, trauma drains over time, and the shake
    // grows with trauma squared, so single ball hits barely register while a Lucky cast jolts the screen.
    // Losing a life plays a different, heavier "thud": a downward kick that wobbles out at a low frequency with a
    // little roll, on unscaled time (it still plays when the last life freezes the game).
    // Put it on the camera. It only offsets the camera around its rest pose and puts it back.
    public sealed class CameraShake : MonoBehaviour
    {
        [Required, SceneObjectsOnly] public GameContext context;

        [Title("Trauma")]
        [MinValue(1), Tooltip("Total damage that fills trauma to 1 (if it lands all at once).")]
        public float damageForFullTrauma = 150f;
        [MinValue(0), SuffixLabel("/s"), Tooltip("Trauma drained per second.")]
        public float decay = 1.6f;
        [Range(0, 1), Tooltip("Trauma below this does not shake at all (filters out single ball hits).")]
        public float threshold = 0.12f;

        [Title("Shake at full trauma")]
        [MinValue(0), SuffixLabel("m")] public float maxOffset = 0.45f;
        [MinValue(0), SuffixLabel("deg")] public float maxRoll = 1.2f;
        [MinValue(0.1f), SuffixLabel("Hz")] public float frequency = 22f;

        [Title("Life lost thud")]
        [MinValue(0), SuffixLabel("m"), Tooltip("Initial downward kick along the camera's up.")]
        public float thudDrop = 0.8f;
        [MinValue(0), SuffixLabel("deg")] public float thudRoll = 2.5f;
        [MinValue(0.1f), SuffixLabel("Hz"), Tooltip("Wobble frequency (low = heavy).")]
        public float thudFrequency = 5f;
        [MinValue(0.01f), SuffixLabel("s"), Tooltip("Time for the wobble to die down to about 5%.")]
        public float thudDuration = 0.6f;

        readonly HashSet<Enemy> watched = new HashSet<Enemy>();
        int lives = -1;
        float thudAge = float.MaxValue, thudSide = 1f;
        Vector3 restPosition;
        Quaternion restRotation;
        bool offset;
        float seed;

        public float Trauma { get; private set; }

        void Start()
        {
            seed = Random.value * 100f;
            if (!context || !context.IsInitialized) return;
            context.Enemies.Added += Watch;
            lives = context.Run.Lives;
            context.Run.LivesChanged += OnLivesChanged;
            foreach (var e in context.Enemies.FrontFirst(new List<Enemy>())) Watch(e); // spawned before this Start
        }

        void OnDestroy()
        {
            if (!context || !context.IsInitialized) return;
            context.Enemies.Added -= Watch;
            context.Run.LivesChanged -= OnLivesChanged;
        }

        void Watch(Enemy enemy)
        {
            if (!watched.Add(enemy)) return;
            enemy.Health.Damaged += (amount, source) => AddDamage(amount);
        }

        void OnLivesChanged(int now)
        {
            if (now < lives) Thud();
            lives = now;
        }

        // The life-lost kick. Public so tests and tools can trigger it.
        public void Thud()
        {
            thudAge = 0f;
            thudSide = Random.value < 0.5f ? -1f : 1f;
        }

        public bool Thudding => thudAge < thudDuration;

        public void AddDamage(float amount) => Trauma = Mathf.Clamp01(Trauma + amount / damageForFullTrauma);

        void LateUpdate()
        {
            Restore();
            if (!context || !context.IsInitialized) return;

            var move = Vector3.zero;
            float roll = 0f;

            // Damage jitter: frozen while paused or after game over, like the rest of the board.
            if (context.Run.IsPlaying)
            {
                Trauma = Mathf.Max(0f, Trauma - decay * Time.deltaTime);
                float t = Mathf.Clamp01((Trauma - threshold) / (1f - threshold));
                float shake = t * t;
                if (shake > 0f)
                {
                    float time = Time.time * frequency;
                    move += new Vector3(Mathf.PerlinNoise(seed, time) * 2f - 1f, Mathf.PerlinNoise(seed + 10f, time) * 2f - 1f, 0f) * (maxOffset * shake);
                    roll += (Mathf.PerlinNoise(seed + 20f, time) * 2f - 1f) * maxRoll * shake;
                }
            }
            else Trauma = 0f;

            // Life-lost thud: a damped cosine, so it starts with the full drop and wobbles out.
            if (thudAge < thudDuration)
            {
                thudAge += Time.unscaledDeltaTime;
                float k = 3f / thudDuration; // e^-3 = 5% at the end
                float wave = Mathf.Exp(-k * thudAge) * Mathf.Cos(2f * Mathf.PI * thudFrequency * thudAge);
                move += Vector3.down * (thudDrop * wave);
                roll += thudSide * thudRoll * wave;
            }

            if (move == Vector3.zero && roll == 0f) return;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            transform.localPosition = restPosition + transform.localRotation * move;
            transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, roll);
            offset = true;
        }

        // Put the camera back before anything else reads or moves it next frame.
        void Restore()
        {
            if (!offset) return;
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
            offset = false;
        }

        void OnDisable() => Restore();
    }
}
