using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    public interface ISnowballTarget
    {
        Transform transform { get; }
        Vector3 AimPoint { get; }
        Vector3 Velocity { get; }
        bool AssistEligible { get; }
        void OnSnowballHit(Snowball ball, Vector3 point, Vector3 velocity, bool headshot);
    }

    public static class SnowballTargets
    {
        public static readonly List<ISnowballTarget> All = new List<ISnowballTarget>();
        public static void Register(ISnowballTarget t) { if (!All.Contains(t)) All.Add(t); }
        public static void Unregister(ISnowballTarget t) => All.Remove(t);

        /// <summary>Light aim assist: a slight nudge toward a kid, not a homing missile.</summary>
        public static Vector3 Assist(Vector3 origin, Vector3 velocity)
        {
            var tune = FrostTuning.I;
            float speed = velocity.magnitude;
            if (speed < 0.5f) return velocity;
            var dir = velocity / speed;
            ISnowballTarget best = null; Vector3 bestNeeded = dir; float bestAngle = tune.aimAssistConeDegrees;
            foreach (var t in All)
            {
                if (t == null || !t.AssistEligible) continue;
                var aim = t.AimPoint;
                float dist = Vector3.Distance(origin, aim);
                if (dist < 0.4f || dist > 9f) continue;
                aim += t.Velocity * (dist / Mathf.Max(speed, 1f)) * 0.8f; // lead a moving target a bit
                var needed = Ballistics.SolveLaunch(origin, aim, speed, true).normalized;
                float ang = Vector3.Angle(dir, needed);
                if (ang < bestAngle) { bestAngle = ang; best = t; bestNeeded = needed; }
            }
            if (best == null) return velocity;
            float correction = Mathf.Min(bestAngle * tune.aimAssistStrength, tune.aimAssistMaxDegrees);
            var newDir = Vector3.RotateTowards(dir, bestNeeded, correction * Mathf.Deg2Rad, 0f);
            return newDir * speed;
        }
    }

    /// <summary>A packed snowball: rests in the palm, then flies with real physics and splats on whatever it hits.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class Snowball : MonoBehaviour
    {
        public static event System.Action<Snowball> Thrown;
        public static event System.Action<Snowball, Vector3, ISnowballTarget> Landed;

        Rigidbody m_Rb;
        bool m_Flying, m_Done;
        float m_FlightTime;
        Vector3 m_Vel;   // the velocity going into this physics step (a collision has already changed the body's)
        public Vector3 ThrowOrigin { get; private set; }
        public float ThrowSpeed { get; private set; }
        /// <summary>The kid who threw this one (null = the player's).</summary>
        public Kid Thrower { get; set; }
        /// <summary>A kid's snowball reached the player's head (for a future snowball fight).</summary>
        public static event System.Action<Snowball> HitPlayer;

        public static Snowball Create(Vector3 pos, float radius)
        {
            var go = ProcMesh.Part("Snowball", null, ProcMesh.Sphere(12, 9, 0.06f, Random.Range(0, 1000)), Palette.Toon(Palette.Snow, true, 0.35f),
                pos, Vector3.one * radius * 2f);
            go.layer = Layers.PlayerHands;
            var mr = go.GetComponent<MeshRenderer>();
            var mat = Palette.Toon(Palette.Snow, true, 0.35f);
            mr.sharedMaterial = mat;
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.15f;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var sb = go.AddComponent<Snowball>();
            sb.m_Rb = rb;
            col.enabled = false;
            return sb;
        }

        public void Hold(Transform palm)
        {
            m_Rb.isKinematic = true;
            transform.SetPositionAndRotation(palm.position, palm.rotation);
        }

        public void Launch(Vector3 velocity, Vector3 angularVelocity)
        {
            transform.SetParent(null, true);
            gameObject.layer = Layers.Snowball;
            var mr = GetComponent<MeshRenderer>();   // out of your hand: real things in its path hide it again
            if (mr) mr.renderingLayerMask &= ~DepthOcclusion.ExemptLayerBit;
            GetComponent<SphereCollider>().enabled = true;
            m_Rb.isKinematic = false;
            m_Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            m_Rb.linearVelocity = velocity;
            m_Rb.angularVelocity = Vector3.ClampMagnitude(angularVelocity, 25f);
            m_Flying = true;
            ThrowOrigin = transform.position;
            ThrowSpeed = velocity.magnitude;
            m_Vel = velocity;
            if (ThrowSpeed > 2f && !SatieSound.Fire("throw", transform.position)) AudioDirector.PlayAt(Sfx.Whoosh, transform.position, Mathf.Clamp01(ThrowSpeed / 12f), Random.Range(0.95f, 1.15f));
            Thrown?.Invoke(this);
        }

        void FixedUpdate()
        {
            if (!m_Flying) return;
            m_Vel = m_Rb.linearVelocity;
            m_FlightTime += Time.fixedDeltaTime;
            // light air drag so long throws still feel like snow, not baseballs
            m_Rb.linearVelocity *= 1f - 0.08f * Time.fixedDeltaTime;
            var room = RoomService.Current;
            if (m_FlightTime > 6f || (room != null && transform.position.y < room.FloorY - 1f)) Splat(transform.position, Vector3.up, null, false);
            // a kid's snowball that reaches you bursts on your face/chest
            var rig = PlayerRig.Active;
            if (Thrower && rig && !m_Done && m_FlightTime > 0.1f)
            {
                var head = rig.Head.position;
                var p = transform.position;
                bool hit = Vector3.Distance(p, head) < 0.22f || (p.y < head.y && p.y > head.y - 0.6f && FrostUtil.FlatDistance(p, head) < 0.17f);
                if (hit)
                {
                    HitPlayer?.Invoke(this);
                    rig.Left.Haptic(0.4f, 0.08f); rig.Right.Haptic(0.4f, 0.08f);
                    Splat(p, (p - head).normalized, null, false);
                }
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (!m_Flying || m_Done) return;
            var contact = c.GetContact(0);
            var target = c.collider.GetComponentInParent<ISnowballTarget>();
            bool head = c.collider.name.Contains("Head");
            if (target != null)
            {
                target.OnSnowballHit(this, contact.point, c.relativeVelocity, head);
            }
            Splat(contact.point, contact.normal, target, c.collider.gameObject.layer == Layers.Snow || c.collider.GetComponentInParent<SnowSurface>(), c.collider);
        }

        public bool IsFlying => m_Flying && !m_Done;

        /// <summary>For targets without a physics collider (world-space UI): hit them and burst here.</summary>
        public void HitTarget(ISnowballTarget target, Vector3 point, Vector3 normal)
        {
            if (!IsFlying) return;
            target?.OnSnowballHit(this, point, m_Rb.linearVelocity, false);
            Splat(point, normal, target, false);
        }

        void Splat(Vector3 point, Vector3 normal, ISnowballTarget target, bool onSnow, Collider hit = null)
        {
            if (m_Done) return;
            m_Done = true;
            Fx.SnowImpact(point, normal, m_Vel, onSnow, target != null ? 0.85f : 1f);
            if (!onSnow && hit) SnowSplat.Spawn(point, normal, hit);   // snow left stuck to walls, furniture, props and kids for a while
            if (!SatieSound.Fire("snowball_hit", point)) AudioDirector.PlayAt(Sfx.Splat, point, 0.8f, Random.Range(0.9f, 1.1f));
            var snow = SnowSurface.Instance;
            if (onSnow && snow) snow.Stamp(point, Random.Range(0f, 360f), StampShape.Crater, 0.16f, 0.8f);
            Landed?.Invoke(this, point, target);
            Destroy(gameObject);
        }
    }
}
