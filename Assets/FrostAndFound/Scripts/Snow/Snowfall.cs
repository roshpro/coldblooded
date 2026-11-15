using UnityEngine;

namespace FrostAndFound
{
    /// <summary>Light snowfall over the play space, from just under the ceiling, gently drifting.</summary>
    public class Snowfall : MonoBehaviour
    {
        ParticleSystem m_Ps, m_Near;

        public static Snowfall Create(RoomInfo room, float density = 1f)
        {
            var go = new GameObject("Snowfall");
            var s = go.AddComponent<Snowfall>();
            s.Build(room, density);
            return s;
        }

        void Build(RoomInfo room, float density)
        {
            float top = Mathf.Min(room.CeilingY - 0.05f, room.FloorY + 2.6f);
            transform.position = new Vector3(room.Bounds.center.x, top, room.Bounds.center.y);
            m_Ps = Fx.MakeSystem("Flakes", transform, FxTextures.Flake, false, 1500);
            var flakeMat = new Material(m_Ps.GetComponent<ParticleSystemRenderer>().sharedMaterial) { name = "Snowfall" };
            flakeMat.SetFloat("_NearFade", 1.1f);
            m_Ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = flakeMat;
            var main = m_Ps.main;
            main.loop = true;
            main.playOnAwake = true;
            float fall = top - room.FloorY;
            main.startSpeed = 0f;
            main.startLifetime = fall / 0.32f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.009f, 0.034f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(Palette.Snow, Palette.SkyHorizon);
            main.gravityModifier = 0f;
            main.prewarm = true;

            var em = m_Ps.emission;
            em.enabled = true;
            float area = room.Width * room.Depth;
            em.rateOverTime = Mathf.Clamp(area * 15f * density, 60f, 420f);

            var shape = m_Ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(room.Width + 0.4f, 0.05f, room.Depth + 0.4f);

            var vel = m_Ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.08f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.38f, -0.26f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);

            var noise = m_Ps.noise;
            noise.enabled = true;
            noise.strength = 0.18f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.2f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var col = m_Ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Palette.Snow, 0), new GradientColorKey(Palette.Snow, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 0.08f), new GradientAlphaKey(0.9f, 0.93f), new GradientAlphaKey(0, 1) });
            col.color = g;

            var rot = m_Ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            m_Ps.gameObject.layer = Layers.Default;
            m_Ps.Play();
            BuildNear(density);
        }

        // A few big, soft, slow flakes drifting close around your head: the depth-of-field look of the snow
        // reference without a (passthrough-breaking) post effect. Sparse and faint so they never get in the way.
        void BuildNear(float density)
        {
            m_Near = Fx.MakeSystem("Near Flakes", null, FxTextures.SoftDot, false, 80);
            var mat = new Material(m_Near.GetComponent<ParticleSystemRenderer>().sharedMaterial) { name = "Near Flakes" };
            mat.SetFloat("_NearFade", 0.4f);
            m_Near.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            var main = m_Near.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.028f, 0.055f);
            main.startColor = Palette.Snow.WithAlpha(0.38f);
            main.gravityModifier = 0f;
            main.prewarm = true;
            var em = m_Near.emission;
            em.rateOverTime = 7f * Mathf.Clamp(density, 0.3f, 1.5f);
            var shape = m_Near.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.2f;
            shape.radiusThickness = 0.55f;   // a shell 0.55-1.2 m out: never right in your eyes
            var vel = m_Near.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.06f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.2f, -0.12f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            var noise = m_Near.noise;
            noise.enabled = true;
            noise.strength = 0.1f;
            noise.frequency = 0.4f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var col = m_Near.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = g;
            m_Near.gameObject.layer = Layers.Default;
            m_Near.Play();
        }

        void LateUpdate()
        {
            var rig = PlayerRig.Active;
            if (m_Near && rig && rig.Head) m_Near.transform.position = rig.Head.position;
        }

        void OnDestroy()
        {
            if (m_Near) Destroy(m_Near.gameObject);
        }

        public void SetIntensity(float k)
        {
            if (!m_Ps) return;
            var em = m_Ps.emission;
            em.rateOverTimeMultiplier = Mathf.Max(0f, k) * em.rateOverTimeMultiplier;
        }

        /// <summary>Stop the snow (and clear the air), or start it falling from the top fresh, not mid-air.</summary>
        public void SetFalling(bool on)
        {
            var systems = new System.Collections.Generic.List<ParticleSystem>(GetComponentsInChildren<ParticleSystem>(true));
            if (m_Near) { systems.Add(m_Near); m_Near.gameObject.SetActive(on); }
            foreach (var ps in systems)
            {
                var main = ps.main;
                main.prewarm = false;
                if (on) { ps.Clear(); ps.Play(); }
                else { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            }
            gameObject.SetActive(on);
        }

        /// <summary>A short heavy flurry (used for the reveal).</summary>
        public void Flurry(int count = 220)
        {
            if (m_Ps) m_Ps.Emit(count);
        }
    }

    /// <summary>Leaves the player's own boot prints in the snow (and crunches) as they walk.</summary>
    public class FootprintTracker : MonoBehaviour
    {
        public float strideLength = 0.5f;
        public float footSpacing = 0.11f;
        public float bootLength = 0.29f;
        Vector3 m_Last;
        bool m_LeftNext = true;
        bool m_Init;

        void Update()
        {
            var rig = PlayerRig.Active;
            var snow = SnowSurface.Instance;
            if (!rig || !snow) return;
            var feet = rig.FeetPosition;
            if (!m_Init) { m_Last = feet; m_Init = true; return; }
            var delta = (feet - m_Last).Flat();
            if (delta.magnitude < strideLength) return;
            var dir = delta.normalized;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var side = Vector3.Cross(Vector3.up, dir) * (m_LeftNext ? -footSpacing : footSpacing);
            var pos = feet + side;
            snow.Stamp(pos, yaw, m_LeftNext ? StampShape.BootLeft : StampShape.BootRight, bootLength, 0.6f);   // soft prints that fill back in
            if (snow.HasSnowAt(pos))
            {
                if (!SatieSound.Fire("step", pos + Vector3.up * 0.05f, SatieSound.Snow)) AudioDirector.PlayAt(Sfx.Footstep, pos + Vector3.up * 0.05f, 0.55f, Random.Range(0.9f, 1.1f));
                if (Random.value < 0.5f) Fx.SnowPuff(pos + Vector3.up * 0.04f, Vector3.up, 4, 0.9f);
            }
            m_LeftNext = !m_LeftNext;
            m_Last = feet;
        }
    }
}
