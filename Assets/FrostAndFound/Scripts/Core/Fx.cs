using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Pooled, palette-coloured one-shot effects (puffs, bursts, confetti, glints) built on a handful of shared
    /// ParticleSystems that are re-aimed and Emit()-ed — no Instantiate/Destroy per event.
    /// </summary>
    public static class Fx
    {
        static ParticleSystem s_Puff, s_Burst, s_Glint, s_Confetti, s_Chunks, s_Glitter;
        static Transform s_Root;

        static Transform Root
        {
            get
            {
                if (!s_Root) s_Root = new GameObject("[Frost FX]").transform;
                return s_Root;
            }
        }

        public static void Reset() { s_Puff = s_Burst = s_Glint = s_Confetti = s_Chunks = s_Glitter = null; s_Root = null; }

        public static ParticleSystem MakeSystem(string name, Transform parent, Texture tex, bool additive, int max, bool worldSpace = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = max;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Palette.Particle(tex, additive, name);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.View;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.minParticleSize = 0f;
            r.maxParticleSize = 2f;
            return ps;
        }

        static ParticleSystem Puff
        {
            get
            {
                if (s_Puff) return s_Puff;
                s_Puff = MakeSystem("Snow Puff", Root, FxTextures.SoftDot, false, 400);
                var main = s_Puff.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
                main.gravityModifier = 0.35f;
                var sol = s_Puff.sizeOverLifetime; sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
                var col = s_Puff.colorOverLifetime; col.enabled = true;
                col.color = Fade(Palette.Snow);
                return s_Puff;
            }
        }

        /// <summary>Clumps of packed snow that fly off an impact, fall, bounce once on whatever's below and melt away.</summary>
        static ParticleSystem Chunks
        {
            get
            {
                if (s_Chunks) return s_Chunks;
                s_Chunks = MakeSystem("Snow Chunks", Root, FxTextures.Clump, false, 300);
                var main = s_Chunks.main;
                main.gravityModifier = 1f;
                var sol = s_Chunks.sizeOverLifetime; sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.7f, 0.9f), new Keyframe(1, 0)));
                var rol = s_Chunks.rotationOverLifetime; rol.enabled = true;
                rol.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
                var col = s_Chunks.collision; col.enabled = true;
                col.type = ParticleSystemCollisionType.World;
                col.mode = ParticleSystemCollisionMode.Collision3D;
                col.quality = ParticleSystemCollisionQuality.Medium;
                col.collidesWith = Layers.Mask(Layers.Default, Layers.Snow, Layers.Room);
                col.enableDynamicColliders = false;
                col.bounce = 0.18f;
                col.dampen = 0.55f;
                col.radiusScale = 0.5f;
                return s_Chunks;
            }
        }

        /// <summary>Fine ice crystals that hang in the air after an impact and twinkle as they settle.</summary>
        static ParticleSystem Glitter
        {
            get
            {
                if (s_Glitter) return s_Glitter;
                s_Glitter = MakeSystem("Snow Glitter", Root, FxTextures.Flake, false, 200);
                var main = s_Glitter.main;
                main.gravityModifier = 0.05f;
                var lv = s_Glitter.limitVelocityOverLifetime; lv.enabled = true;
                lv.limit = 0.15f; lv.dampen = 0.12f;   // air drag: a quick burst, then a slow drift
                var sol = s_Glitter.sizeOverLifetime; sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(0.15f, 1f), new Keyframe(0.55f, 0.6f), new Keyframe(0.7f, 1f), new Keyframe(1, 0)));
                var rol = s_Glitter.rotationOverLifetime; rol.enabled = true;
                rol.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
                return s_Glitter;
            }
        }

        static ParticleSystem Burst
        {
            get
            {
                if (s_Burst) return s_Burst;
                s_Burst = MakeSystem("Burst", Root, FxTextures.SoftDot, false, 400);
                var main = s_Burst.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
                main.gravityModifier = 0.6f;
                var sol = s_Burst.sizeOverLifetime; sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
                return s_Burst;
            }
        }

        static ParticleSystem Glint
        {
            get
            {
                if (s_Glint) return s_Glint;
                s_Glint = MakeSystem("Glints", Root, FxTextures.Star, true, 200);
                var main = s_Glint.main;
                main.startLifetime = 0.45f;
                main.startSpeed = 0f;
                main.startSize = 0.05f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 0.5f);
                var sol = s_Glint.sizeOverLifetime; sol.enabled = true;
                var curve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.35f, 1f), new Keyframe(1, 0));
                sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
                var rol = s_Glint.rotationOverLifetime; rol.enabled = true; rol.z = 1.2f;
                return s_Glint;
            }
        }

        static ParticleSystem Confetti
        {
            get
            {
                if (s_Confetti) return s_Confetti;
                s_Confetti = MakeSystem("Confetti", Root, FxTextures.SoftDot, false, 500);
                var main = s_Confetti.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                main.gravityModifier = 0.5f;
                var sol = s_Confetti.sizeOverLifetime; sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
                return s_Confetti;
            }
        }

        static ParticleSystem.MinMaxGradient Fade(Color c)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) },
                      new[] { new GradientAlphaKey(0.95f, 0), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0, 1) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        /// <summary>Snow kicked up by a scoop, a footstep or a snowball impact.</summary>
        public static void SnowPuff(Vector3 pos, Vector3 normal, int count = 14, float spread = 0.6f)
        {
            var ps = Puff;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                var dir = (normal + Random.insideUnitSphere * spread).normalized;
                p.position = pos + Random.insideUnitSphere * 0.02f;
                p.velocity = dir * Random.Range(0.4f, 1.6f);
                p.startSize = Random.Range(0.02f, 0.055f);
                p.startColor = Color.Lerp(Palette.Snow, Palette.SnowShadow, Random.value * 0.4f);
                p.startLifetime = Random.Range(0.35f, 0.75f);
                ps.Emit(p, 1);
            }
        }

        /// <summary>
        /// A snowball bursting on something: packed clumps thrown off (mostly back out along the surface, carrying
        /// some of the ball's momentum), a ring of powder skidding over the surface, a soft cloud and a few twinkling
        /// crystals. <paramref name="velocity"/> is the ball's speed into the hit; <paramref name="onSnow"/> sinks most
        /// of it into the snow, which makes a crater instead.
        /// </summary>
        public static void SnowImpact(Vector3 pos, Vector3 normal, Vector3 velocity, bool onSnow = false, float scale = 1f)
        {
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;
            normal.Normalize();
            float speed = velocity.magnitude;
            var inDir = speed > 0.05f ? velocity / speed : -normal;
            float k = Mathf.Clamp(speed / 7f, 0.55f, 1.4f) * scale;
            // what carries on past the hit: the ball's motion along the surface
            var slide = Vector3.ProjectOnPlane(inDir, normal);
            var t1 = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var t2 = Vector3.Cross(normal, t1);
            var origin = pos + normal * 0.012f;
            var p = new ParticleSystem.EmitParams();

            // packed clumps
            var chunks = Chunks;
            int n = Mathf.RoundToInt((onSnow ? 6 : 12) * k);
            for (int i = 0; i < n; i++)
            {
                var dir = normal * Random.Range(0.5f, 1.1f) + slide * Random.Range(0.3f, 0.9f) + Random.insideUnitSphere * 0.7f;
                if (Vector3.Dot(dir, normal) < 0.15f) dir += normal * (0.15f - Vector3.Dot(dir, normal));
                p.position = origin + Random.insideUnitSphere * 0.015f;
                p.velocity = dir.normalized * Random.Range(0.7f, 2.4f) * k;
                p.startSize = i < 2 ? Random.Range(0.034f, 0.046f) * scale : Random.Range(0.012f, 0.03f) * scale;
                p.startColor = Color.Lerp(Palette.Snow, Palette.SnowShadow, Random.value * 0.3f);
                p.startLifetime = Random.Range(0.8f, 1.4f);
                p.rotation = Random.Range(0f, 360f);
                chunks.Emit(p, 1);
            }

            // a ring of powder skidding out over the surface, pushed further on the side the ball was heading
            var puff = Puff;
            int ring = Mathf.RoundToInt(12 * Mathf.Min(k, 1.2f));
            for (int i = 0; i < ring; i++)
            {
                float a = (i + Random.value * 0.6f) / ring * Mathf.PI * 2f;
                var along = Mathf.Cos(a) * t1 + Mathf.Sin(a) * t2;
                float push = 1f + 0.8f * Mathf.Max(0f, Vector3.Dot(along, slide));
                p.position = origin + along * 0.02f;
                p.velocity = (along * Random.Range(0.55f, 0.95f) * push + normal * Random.Range(0.05f, 0.25f)) * Mathf.Sqrt(k);
                p.startSize = Random.Range(0.025f, 0.05f) * scale;
                p.startColor = Color.Lerp(Palette.Snow, Palette.SnowShadow, Random.value * 0.25f);
                p.startLifetime = Random.Range(0.35f, 0.6f);
                p.rotation = 0f;
                puff.Emit(p, 1);
            }

            // the soft cloud
            SnowPuff(origin, normal, onSnow ? 12 : 10, 0.75f);

            // a few crystals catching the light
            var glitter = Glitter;
            int g = onSnow ? 3 : 6;
            for (int i = 0; i < g; i++)
            {
                p.position = origin + Random.insideUnitSphere * 0.03f;
                p.velocity = (normal * Random.Range(0.4f, 1.0f) + Random.insideUnitSphere * 0.6f) * k;
                p.startSize = Random.Range(0.01f, 0.018f) * scale;
                p.startColor = new Color(0.92f, 0.97f, 1f, 1f);
                p.startLifetime = Random.Range(0.9f, 1.5f);
                p.rotation = Random.Range(0f, 60f);
                glitter.Emit(p, 1);
            }
        }

        public static void ColorBurst(Vector3 pos, Color a, Color b, int count = 12, float speed = 2f)
        {
            var ps = Burst;
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                p.position = pos;
                p.velocity = Random.onUnitSphere * Random.Range(speed * 0.5f, speed) + Vector3.up * 0.6f;
                p.startColor = Color.Lerp(a, b, Random.value);
                p.startSize = Random.Range(0.018f, 0.04f);
                p.startLifetime = Random.Range(0.4f, 0.8f);
                ps.Emit(p, 1);
            }
        }

        /// <summary>The faint glint that marks a clue (subtle enough that you still have to look for it).</summary>
        public static void GlintAt(Vector3 pos, float size = 0.05f, Color? tint = null)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = Vector3.zero,
                startSize = size,
                startColor = tint ?? Palette.GlintWhite,
                startLifetime = Random.Range(0.35f, 0.55f),
                rotation = Random.Range(0f, 90f)
            };
            Glint.Emit(p, 1);
        }

        public static void ConfettiBurst(Vector3 pos, int count = 45)
        {
            var ps = Confetti;
            Color[] cols = { Palette.TonyRed, Palette.SerenaPurple, Palette.LucyBlue, Palette.Carrot, Palette.Glint, Palette.ShermanBlue };
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                p.position = pos;
                var dir = (Vector3.up * 1.4f + Random.insideUnitSphere).normalized;
                p.velocity = dir * Random.Range(2f, 3.6f);
                p.startColor = cols[i % cols.Length];
                p.startSize = Random.Range(0.03f, 0.06f);
                p.startLifetime = Random.Range(1f, 1.6f);
                ps.Emit(p, 1);
            }
        }
    }
}
