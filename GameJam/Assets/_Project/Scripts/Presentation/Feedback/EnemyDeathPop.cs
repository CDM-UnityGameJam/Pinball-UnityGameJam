using System.Collections.Generic;
using Pinball.Feedback;
using Pinball.Gameplay;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Presentation
{
    // Killed enemies pop instead of vanishing. The enemy leaves play at once, so a stand-in copy of its monster
    // sprite does the animation: it flashes white and swells, then squashes flat and fades. A burst (popCue,
    // Death_Pop: flash, ring, stars, puffs) plays on the same spot. Only kills pop; breaches and the field clear
    // after a lost life don't. Put it on the scene's Feedback object.
    public sealed class EnemyDeathPop : MonoBehaviour
    {
        [Required, SceneObjectsOnly] public GameContext context;
        [Tooltip("Burst played where the enemy died.")] public VfxCue popCue;

        [Title("Sprite pop")]
        [MinValue(0.01f), SuffixLabel("s"), Tooltip("Swell and flash.")] public float swellTime = 0.07f;
        [MinValue(0.01f), SuffixLabel("s"), Tooltip("Squash flat and fade.")] public float squashTime = 0.16f;
        [MinValue(1f)] public float swellScale = 1.3f;
        [MinValue(1f), Tooltip("Width at the end of the squash.")] public float squashWidth = 1.7f;

        static readonly int Flash = Shader.PropertyToID("_Flash");

        sealed class Ghost
        {
            public SpriteRenderer renderer;
            public Vector3 scale;
            public float age;
            public Color color;
        }

        readonly List<Ghost> active = new List<Ghost>();
        readonly Stack<Ghost> pool = new Stack<Ghost>();
        MaterialPropertyBlock block;
        Transform root;

        public int ActiveCount => active.Count;

        void Start()
        {
            block = new MaterialPropertyBlock();
            if (context && context.IsInitialized) context.Score.Killed += OnKilled;
        }

        void OnDestroy()
        {
            if (context && context.IsInitialized) context.Score.Killed -= OnKilled;
        }

        void OnKilled(Enemy enemy, long reward, AttackRecord source)
        {
            if (!enemy) return;
            var position = enemy.transform.position;
            if (popCue) popCue.Play(VfxRequest.At(position, context.Board.transform.rotation));
            var view = enemy.GetComponent<EnemyView>();
            var body = view ? view.body : null;
            if (!body || !body.sprite) return;
            var ghost = Get();
            var r = ghost.renderer;
            r.sprite = body.sprite;
            r.sharedMaterial = body.sharedMaterial;
            r.sortingOrder = body.sortingOrder;
            r.flipX = body.flipX;
            r.color = Color.white;
            r.transform.SetPositionAndRotation(body.transform.position, body.transform.rotation);
            ghost.scale = body.transform.lossyScale;
            ghost.color = Color.white;
            ghost.age = 0f;
            r.transform.localScale = ghost.scale;
            r.gameObject.SetActive(true);
            active.Add(ghost);
        }

        Ghost Get()
        {
            if (pool.Count > 0) return pool.Pop();
            if (!root) root = new GameObject("Death Pops").transform;
            var go = new GameObject("DeathPop", typeof(SpriteRenderer), typeof(FaceCamera));
            go.transform.SetParent(root, false);
            var r = go.GetComponent<SpriteRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return new Ghost { renderer = r };
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var g = active[i];
                g.age += dt;
                float x, y, flash, alpha = 1f;
                if (g.age < swellTime)
                {
                    float t = g.age / swellTime;
                    x = y = Mathf.Lerp(1f, swellScale, 1f - (1f - t) * (1f - t));
                    flash = 1f;
                }
                else
                {
                    float t = Mathf.Clamp01((g.age - swellTime) / squashTime);
                    float e = t * t; // accelerate into the squash
                    x = Mathf.Lerp(swellScale, squashWidth, e);
                    y = Mathf.Lerp(swellScale, 0f, e);
                    flash = 1f - t;
                    alpha = 1f - e;
                    if (t >= 1f)
                    {
                        g.renderer.gameObject.SetActive(false);
                        active.RemoveAt(i);
                        pool.Push(g);
                        continue;
                    }
                }
                var r = g.renderer;
                r.transform.localScale = new Vector3(g.scale.x * x, g.scale.y * y, g.scale.z);
                var c = g.color; c.a = alpha; r.color = c;
                r.GetPropertyBlock(block);
                block.SetFloat(Flash, flash);
                r.SetPropertyBlock(block);
            }
        }
    }
}
