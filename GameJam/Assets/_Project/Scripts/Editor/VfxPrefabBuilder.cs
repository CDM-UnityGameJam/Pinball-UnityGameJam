using System;
using Pinball.Feedback;
using Pinball.Gameplay;
using Pinball.Presentation;
using UnityEditor;
using UnityEngine;

namespace Pinball.Editor
{
    // Builds the element VFX prefabs from the baked shape sprites (VfxShapeBaker), in a flat colour-block
    // style: hard-edged flames, shards, bolts, chunky rings and rocks, with a little glow.
    //
    // Held cards (ball auras, Prefabs/Ball): Aura_Spark (fire), Aura_Arc (lightning), Aura_Shockwave (tremor).
    // Played cards (Prefabs/VFX, per target): Hit_Fire / Hit_Lightning / Hit_Tremor when no die matched, and
    // the bigger *_Matched versions when a die matched the card's number.
    //
    // Pinball > VFX > Build Element Effects rebuilds everything (bakes missing textures, creates materials,
    // overwrites the prefabs in place so their GUIDs survive, and points the element cues at them).
    // Tune by editing the numbers below and re-running, or edit the prefabs directly and stop using the menu.
    public static class VfxPrefabBuilder
    {
        const string Textures = "Assets/_Project/Art/Textures/VFX";
        const string Materials = "Assets/_Project/Art/Materials/VFX";
        const string VfxPrefabs = "Assets/_Project/Prefabs/VFX";
        const string BallPrefabs = "Assets/_Project/Prefabs/Ball";
        const string Cues = "Assets/_Project/Data/Feedback";
        const string Elements = "Assets/_Project/Data/Elements";

        static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

        // Fire, lightning and tremor palettes, matched to the card art.
        static readonly Color FireOrange = Hex("FF8A5C"), FirePink = Hex("F45BD0"), FireYellow = Hex("FFF26B"), FireMint = Hex("86F5B4");
        static readonly Color BoltCyan = Hex("5CF6FF"), BoltViolet = Hex("B66BFF"), BoltBlue = Hex("3B4BFF"), BoltWhite = Hex("E9FFFF");
        static readonly Color QuakeYellow = Hex("FFD24A"), QuakeOrange = Hex("FF9A3C"), QuakePink = Hex("FF6FB5"), QuakeCream = Hex("FFF6C8");

        [MenuItem("Pinball/VFX/Build Element Effects")]
        public static void BuildAll()
        {
            if (!AssetDatabase.LoadAssetAtPath<Texture2D>($"{Textures}/Vfx_Flame.png")) VfxShapeBaker.BakeAll();
            System.IO.Directory.CreateDirectory(Materials);

            Save($"{BallPrefabs}/Aura_Spark.prefab", FireAura());
            Save($"{BallPrefabs}/Aura_Arc.prefab", LightningAura());
            Save($"{BallPrefabs}/Aura_Shockwave.prefab", TremorAura());

            var fire = Save($"{VfxPrefabs}/Hit_Fire.prefab", FireHit(false));
            var fireMatched = Save($"{VfxPrefabs}/Hit_Fire_Matched.prefab", FireHit(true));
            var bolt = Save($"{VfxPrefabs}/Hit_Lightning.prefab", LightningHit(false));
            var boltMatched = Save($"{VfxPrefabs}/Hit_Lightning_Matched.prefab", LightningHit(true));
            var tremor = Save($"{VfxPrefabs}/Hit_Tremor.prefab", TremorHit(false));
            var tremorMatched = Save($"{VfxPrefabs}/Hit_Tremor_Matched.prefab", TremorHit(true));

            Wire("Spark", "Vfx_Inferno", fire, "Vfx_InfernoMatched", fireMatched, "Aura_Spark");
            Wire("Arc", "Vfx_Thunderstorm", bolt, "Vfx_ThunderstormMatched", boltMatched, "Aura_Arc");
            Wire("Shockwave", "Vfx_Impact", tremor, "Vfx_ImpactMatched", tremorMatched, "Aura_Shockwave");
            AssetDatabase.SaveAssets();
            Debug.Log("Built element VFX prefabs and wired the element cues.");
        }

        // ---------- Held cards: ball auras ----------

        static GameObject FireAura()
        {
            var root = Root("Aura_Spark");
            Emit(root, "Flames", "Vfx_Flame", p =>
            {
                p.world = true; p.rate = 22; p.life = (0.35f, 0.5f); p.size = (0.35f, 0.55f); p.speed = (0.4f, 0.9f);
                p.sphere = 0.15f; p.riseUp = 1.6f; p.spin = (-15f, 15f); p.shrink = true;
            });
            Emit(root, "Embers", "Vfx_Shard", p =>
            {
                p.world = true; p.rate = 14; p.life = (0.3f, 0.45f); p.size = (0.07f, 0.13f); p.speed = (1f, 2f);
                p.sphere = 0.2f; p.riseUp = 1f; p.colorA = FireYellow; p.colorB = FirePink; p.spinSpeed = 540f; p.shrink = true;
            });
            root.AddComponent<ParticleAura>();
            return root;
        }

        static GameObject LightningAura()
        {
            var root = Root("Aura_Arc");
            Emit(root, "Glow", "Vfx_Glow", p =>
            {
                p.rate = 6; p.life = (0.3f, 0.3f); p.size = (1.2f, 1.3f); p.speed = (0f, 0f); p.colorA = p.colorB = WithAlpha(BoltCyan, 0.35f);
            });
            Emit(root, "Bolts", "Vfx_Bolt", p =>
            {
                p.rate = 18; p.life = (0.08f, 0.14f); p.size = (0.26f, 0.4f); p.speed = (0f, 0f); p.ring = 0.5f; p.spin = (0f, 360f);
            });
            Emit(root, "Sparks", "Vfx_Star", p =>
            {
                p.rate = 10; p.life = (0.15f, 0.25f); p.size = (0.1f, 0.2f); p.speed = (0.5f, 1f); p.sphere = 0.45f;
                p.colorA = BoltCyan; p.colorB = BoltWhite; p.shrink = true;
            });
            root.AddComponent<ParticleAura>();
            return root;
        }

        static GameObject TremorAura()
        {
            var root = Root("Aura_Shockwave");
            Emit(root, "Rings", "Vfx_Ring", p =>
            {
                p.flat = true; p.rate = 2.5f; p.life = (0.6f, 0.6f); p.size = (0.7f, 0.7f); p.speed = (0f, 0f);
                p.grow = 2.4f; p.colorA = QuakeYellow; p.colorB = QuakeYellow; p.fadeTo = QuakeOrange;
            });
            Emit(root, "Rocks", "Vfx_Rock", p =>
            {
                p.world = true; p.rate = 6; p.life = (0.4f, 0.55f); p.size = (0.13f, 0.2f); p.speed = (0.3f, 0.7f);
                p.ring = 0.45f; p.riseUp = 0.8f; p.spinSpeed = 360f; p.shrink = true;
            });
            root.AddComponent<ParticleAura>();
            return root;
        }

        // ---------- Played cards: per-target hits ----------

        static GameObject FireHit(bool matched)
        {
            var root = Root(matched ? "Hit_Fire_Matched" : "Hit_Fire");
            if (matched)
            {
                Emit(root, "Pillar", "Vfx_Flame", p => { p.burst = 1; p.life = (0.7f, 0.7f); p.size = (2.2f, 2.2f); p.speed = (0f, 0f); p.ahead = 0.9f; p.lift = 0.3f; p.swell = true; });
                Emit(root, "Ring", "Vfx_Ring", p => { p.flat = true; p.burst = 1; p.life = (0.35f, 0.35f); p.size = (0.6f, 0.6f); p.speed = (0f, 0f); p.grow = 4f; p.colorA = p.colorB = FireOrange; p.fadeTo = FirePink; });
                Emit(root, "Blobs", "Vfx_Blob", p => { p.burst = 6; p.life = (0.4f, 0.6f); p.size = (0.22f, 0.38f); p.speed = (2f, 3.2f); p.cone = 45f; p.shrink = true; });
            }
            Emit(root, "Flames", "Vfx_Flame", p =>
            {
                p.burst = matched ? 14 : 6; p.life = (0.4f, 0.6f); p.size = matched ? (0.6f, 1f) : (0.45f, 0.75f);
                p.speed = matched ? (2f, 4f) : (1.5f, 2.5f); p.cone = matched ? 35f : 25f; p.spin = (-20f, 20f); p.swell = true;
            });
            Emit(root, "Embers", "Vfx_Shard", p =>
            {
                p.burst = matched ? 24 : 10; p.life = (0.3f, 0.5f); p.size = (0.08f, 0.16f); p.speed = matched ? (3f, 6f) : (2f, 4f);
                p.sphere = 0.2f; p.colorA = FireYellow; p.colorB = FirePink; p.spinSpeed = 720f; p.shrink = true;
            });
            if (matched)
                Emit(root, "Mint", "Vfx_Shard", p => { p.burst = 8; p.life = (0.4f, 0.55f); p.size = (0.12f, 0.2f); p.speed = (1.5f, 3f); p.cone = 30f; p.colorA = p.colorB = FireMint; p.spinSpeed = 360f; p.shrink = true; });
            return root;
        }

        static GameObject LightningHit(bool matched)
        {
            var root = Root(matched ? "Hit_Lightning_Matched" : "Hit_Lightning");
            Emit(root, "Bolt", "Vfx_Bolt", p =>
            {
                p.burst = 1; p.repeats = matched ? 3 : 1; p.repeatGap = 0.08f; p.life = matched ? (0.22f, 0.26f) : (0.16f, 0.2f);
                p.size3D = matched ? new Vector2(1.6f, 3.2f) : new Vector2(1.2f, 2.2f); p.speed = (0f, 0f);
                p.ahead = matched ? 1.6f : 1.1f; p.lift = 0.3f; p.box = matched ? 0.6f : 0f; p.flicker = true;
            });
            if (matched)
            {
                Emit(root, "Glow", "Vfx_Glow", p => { p.burst = 1; p.life = (0.3f, 0.3f); p.size = (2.6f, 2.6f); p.speed = (0f, 0f); p.colorA = p.colorB = WithAlpha(BoltCyan, 0.6f); p.shrink = true; });
                Emit(root, "Ring", "Vfx_Ring", p => { p.flat = true; p.burst = 1; p.life = (0.3f, 0.3f); p.size = (0.5f, 0.5f); p.speed = (0f, 0f); p.grow = 5f; p.colorA = p.colorB = BoltCyan; p.fadeTo = BoltViolet; });
                Emit(root, "Shards", "Vfx_Shard", p => { p.burst = 16; p.life = (0.25f, 0.4f); p.size = (0.1f, 0.18f); p.speed = (3f, 5f); p.sphere = 0.2f; p.colorA = BoltViolet; p.colorB = BoltBlue; p.spinSpeed = 720f; p.shrink = true; });
            }
            Emit(root, "Sparks", "Vfx_Star", p =>
            {
                p.burst = matched ? 14 : 6; p.life = (0.2f, 0.3f); p.size = matched ? (0.22f, 0.4f) : (0.18f, 0.3f);
                p.speed = matched ? (2.5f, 4.5f) : (2f, 3f); p.sphere = 0.15f; p.colorA = BoltCyan; p.colorB = BoltWhite; p.shrink = true;
            });
            return root;
        }

        static GameObject TremorHit(bool matched)
        {
            var root = Root(matched ? "Hit_Tremor_Matched" : "Hit_Tremor");
            Emit(root, "Rings", "Vfx_Ring", p =>
            {
                p.flat = true; p.burst = 1; p.repeats = matched ? 3 : 1; p.repeatGap = 0.1f; p.life = matched ? (0.45f, 0.45f) : (0.35f, 0.35f);
                p.size = (0.5f, 0.6f); p.speed = (0f, 0f); p.grow = matched ? 6.5f : 4.4f;
                p.colorA = QuakeYellow; p.colorB = matched ? QuakePink : QuakeYellow; p.fadeTo = QuakeOrange;
            });
            Emit(root, "Rocks", "Vfx_Rock", p =>
            {
                p.burst = matched ? 18 : 8; p.life = (0.45f, 0.65f); p.size = matched ? (0.2f, 0.36f) : (0.18f, 0.3f);
                p.speed = matched ? (3f, 5f) : (2f, 3.5f); p.cone = 70f; p.gravity = 0.03f; p.spinSpeed = 540f;
            });
            if (matched)
            {
                Emit(root, "Glow", "Vfx_Glow", p => { p.burst = 1; p.life = (0.35f, 0.35f); p.size = (2.6f, 2.6f); p.speed = (0f, 0f); p.colorA = p.colorB = WithAlpha(QuakeCream, 0.55f); p.shrink = true; });
                Emit(root, "Chips", "Vfx_Shard", p => { p.burst = 10; p.life = (0.3f, 0.5f); p.size = (0.1f, 0.18f); p.speed = (2.5f, 4.5f); p.cone = 60f; p.colorA = QuakePink; p.colorB = QuakeOrange; p.spinSpeed = 720f; p.shrink = true; });
            }
            return root;
        }

        // ---------- Particle building ----------

        sealed class Spec
        {
            public bool world, flat, shrink, swell, flicker;
            public float rate;
            public int burst, repeats = 1;
            public float repeatGap;
            public (float, float) life = (0.5f, 0.5f), size = (0.3f, 0.3f), speed = (1f, 1f), spin = (0f, 0f);
            public Vector2 size3D;
            public float sphere, ring, cone, box, lift, ahead, riseUp, spinSpeed, grow, gravity;
            public Color colorA = Color.white, colorB = Color.white;
            public Color? fadeTo;
        }

        // Overall size of each family on the board (the shapes are authored around a 1 m enemy).
        const float HitScale = 1.8f, AuraScale = 1.5f;

        static GameObject Root(string name)
        {
            var go = new GameObject(name);
            go.transform.localScale = Vector3.one * (name.StartsWith("Aura") ? AuraScale : HitScale);
            return go;
        }

        static void Emit(GameObject root, string name, string texture, Action<Spec> configure)
        {
            var spec = new Spec();
            configure(spec);
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = spec.rate > 0f;
            main.loop = spec.rate > 0f;
            main.duration = spec.rate > 0f ? 1f : Mathf.Max(0.05f, spec.repeats * spec.repeatGap + 0.01f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(spec.life.Item1, spec.life.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(spec.speed.Item1, spec.speed.Item2);
            if (spec.size3D != Vector2.zero)
            {
                main.startSize3D = true;
                main.startSizeX = spec.size3D.x;
                main.startSizeY = spec.size3D.y;
                main.startSizeZ = 1f;
            }
            else main.startSize = new ParticleSystem.MinMaxCurve(spec.size.Item1, spec.size.Item2);
            main.startRotation = new ParticleSystem.MinMaxCurve(spec.spin.Item1 * Mathf.Deg2Rad, spec.spin.Item2 * Mathf.Deg2Rad);
            main.startColor = new ParticleSystem.MinMaxGradient(spec.colorA, spec.colorB);
            main.simulationSpace = spec.world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.gravityModifier = spec.gravity;
            main.maxParticles = 200;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.rateOverTime = spec.rate;
            if (spec.burst > 0)
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)spec.burst, (short)spec.burst, spec.repeats, Mathf.Max(0.01f, spec.repeatGap)) });
            else emission.SetBursts(new ParticleSystem.Burst[0]);

            var shape = ps.shape;
            shape.enabled = true;
            // The camera looks down the board, so "up the screen" is the board's forward; `ahead` moves along it.
            shape.position = new Vector3(0f, spec.lift + (spec.flat ? 0.12f : 0f), spec.ahead); // flat shapes clear the playfield
            if (spec.cone > 0f)
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = spec.cone;
                shape.radius = 0.1f;
                shape.rotation = new Vector3(-45f, 0f, 0f); // emit up the screen: between the board's up and forward
            }
            else if (spec.ring > 0f)
            {
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = spec.ring;
                shape.radiusThickness = 0f;
                shape.rotation = new Vector3(90f, 0f, 0f); // lie in the board plane
            }
            else if (spec.box > 0f)
            {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(spec.box, 0f, spec.box);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = Mathf.Max(0.001f, spec.sphere);
            }

            if (spec.riseUp != 0f)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = new ParticleSystem.MinMaxCurve(0f);
                vel.y = new ParticleSystem.MinMaxCurve(spec.riseUp * 0.5f);
                vel.z = new ParticleSystem.MinMaxCurve(spec.riseUp); // up the screen
            }

            if (spec.spinSpeed > 0f)
            {
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-spec.spinSpeed * Mathf.Deg2Rad, spec.spinSpeed * Mathf.Deg2Rad);
            }

            var sizeOver = ps.sizeOverLifetime;
            if (spec.grow > 0f)
            {
                sizeOver.enabled = true;
                sizeOver.size = new ParticleSystem.MinMaxCurve(spec.grow, AnimationCurve.EaseInOut(0f, 1f / spec.grow, 1f, 1f));
            }
            else if (spec.swell)
            {
                sizeOver.enabled = true;
                sizeOver.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));
            }
            else if (spec.shrink)
            {
                sizeOver.enabled = true;
                sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            }

            var colorOver = ps.colorOverLifetime;
            colorOver.enabled = true;
            var end = spec.fadeTo ?? Color.white;
            var gradient = new Gradient();
            var alphas = spec.flicker
                ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.3f, 0.35f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) };
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(spec.fadeTo.HasValue ? end : Color.white, 1f) }, alphas);
            colorOver.color = new ParticleSystem.MinMaxGradient(gradient);

            // Flat shapes (rings) lie on the board: horizontal billboards (the board's 6.5 degree tilt doesn't show).
            renderer.renderMode = spec.flat ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = MaterialFor(texture);
            renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        static Material MaterialFor(string texture)
        {
            string path = $"{Materials}/{texture}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Textures}/{texture}.png"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f); // flat shapes lie on the board and can face either way
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Overwrite in place, so existing references (elements, cues) keep working.
        static GameObject Save(string path, GameObject root)
        {
            try { return PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void Wire(string elementName, string cueName, GameObject hit, string matchedCueName, GameObject matched, string auraName)
        {
            var element = AssetDatabase.LoadAssetAtPath<ElementDefinition>($"{Elements}/{elementName}.asset");
            if (!element) { Debug.LogWarning("Missing element " + elementName); return; }
            element.activeHitVfx = Cue(cueName, hit);
            element.luckyHitVfx = Cue(matchedCueName, matched);
            element.ballAuraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BallPrefabs}/{auraName}.prefab");
            EditorUtility.SetDirty(element);
        }

        static VfxCue Cue(string name, GameObject prefab)
        {
            string path = $"{Cues}/{name}.asset";
            var cue = AssetDatabase.LoadAssetAtPath<VfxCue>(path);
            if (!cue)
            {
                cue = ScriptableObject.CreateInstance<VfxCue>();
                AssetDatabase.CreateAsset(cue, path);
            }
            cue.prefab = prefab;
            cue.minInterval = 0f;
            EditorUtility.SetDirty(cue);
            return cue;
        }
    }
}
