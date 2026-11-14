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
        static ParticleSystem s_Puff, s_Burst, s_Glint, s_Confetti;
        static Transform s_Root;

        static Transform Root
        {
            get
            {
                if (!s_Root) s_Root = new GameObject("[Frost FX]").transform;
                return s_Root;
            }
        }

        public static void Reset() { s_Puff = s_Burst = s_Glint = s_Confetti = null; s_Root = null; }

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
