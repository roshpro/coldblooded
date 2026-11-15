using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>Static facts about each suspect (doc §4).</summary>
    public static class Cast
    {
        public static string Name(CastId id) => id switch
        {
            CastId.Dexter => "Dexter",
            CastId.Sherman => "Sherman",
            CastId.Serena => "Serena",
            CastId.Lucy => "Lucy",
            _ => "Tony",
        };

        public static string FullName(CastId id) => id switch
        {
            CastId.Dexter => "Dexter Park, 9",
            CastId.Sherman => "Sherman Barnes, 7",
            CastId.Serena => "Serena Pruitt, 10",
            CastId.Lucy => "Lucy Whitmore, 11",
            _ => "Tony \"Turbo\" Ramirez, 10",
        };

        /// <summary>Colour-coded to their clue: Tony red, Serena purple, Dexter brown, Sherman blue, Lucy bright blue.</summary>
        public static Color Color(CastId id) => id switch
        {
            CastId.Dexter => Palette.DexterBrown,
            CastId.Sherman => Palette.ShermanBlue,
            CastId.Serena => Palette.SerenaPurple,
            CastId.Lucy => Palette.LucyBlue,
            _ => Palette.TonyRed,
        };

        public static float Scale(CastId id) => id switch
        {
            CastId.Sherman => 0.9f,
            CastId.Lucy => 1.06f,
            CastId.Dexter => 0.97f,
            _ => 1f,
        };

        public static string Key(CastId id) => Name(id).ToLowerInvariant();
    }

    /// <summary>
    /// A suspect. Walks, looks at you, talks with a speech bubble, leans in to squint at photos, poses for the
    /// camera, and reacts to snowballs. Subclasses add each kid's gag; TonyKid adds the finale chase.
    /// </summary>
    public class Kid : MonoBehaviour, ISnowballTarget
    {
        public static readonly List<Kid> All = new List<Kid>();

        public CastId id;
        public KidRig Rig { get; private set; } = new KidRig();
        public KidAnimator Anim { get; private set; }
        public Vector3 Velocity { get; private set; }
        public bool Busy { get; set; }
        public bool CanMove { get; set; } = true;
        public Vector3 HomePosition { get; set; }
        public Quaternion HomeRotation { get; set; }

        public string DisplayName => Cast.Name(id);
        public Color Color => Cast.Color(id);
        public Vector3 ChestPoint => Rig.Spine ? Rig.Spine.position + Vector3.up * 0.12f * Rig.Scale : transform.position + Vector3.up * 0.55f;
        public Vector3 HeadPoint => Rig.Head ? Rig.Head.position : transform.position + Vector3.up * 0.85f;
        public virtual Vector3 AimPoint => Vector3.Lerp(ChestPoint, HeadPoint, 0.3f);
        public virtual bool AssistEligible => true;
        public Transform BubbleAnchor => Rig.BubbleAnchor ? Rig.BubbleAnchor : transform;

        Vector3 m_LastPos;
        Coroutine m_Move;
        float m_LastPoseTime = -10f, m_LastHitLine = -10f, m_NextBark, m_NextWander;
        /// <summary>Idle kids stroll to a free spot near home now and then (only where there's room).</summary>
        protected virtual bool WandersWhenIdle => true;
        public float wanderRadius = 1.6f, wanderSpeed = 0.65f;
        float m_NextFidget, m_AttendUntil, m_NextSeparate;
        Vector3 m_MoveTarget;
        /// <summary>Where this kid is walking to (or standing, if not walking).</summary>
        public Vector3 Destination => m_Move != null ? m_MoveTarget : transform.position;
        Vector3 m_Attend;
        /// <summary>Raised when this kid lets go of a snowball (for a future snowball fight).</summary>
        public static event System.Action<Kid, Snowball> ThrewSnowball;
        protected FootprintStamper m_Prints;

        public static Kid Find(CastId id)
        {
            foreach (var k in All) if (k && k.id == id) return k;
            return null;
        }

        public static T Spawn<T>(CastId id, Vector3 pos, Quaternion rot) where T : Kid
        {
            var go = new GameObject(Cast.Name(id));
            go.transform.SetPositionAndRotation(pos, rot);
            go.layer = Layers.Kid;
            var kid = go.AddComponent<T>();
            kid.id = id;
            kid.Build();
            return kid;
        }

        protected virtual void Build()
        {
            var model = FrostAssets.ModelFor(id);
            Rig = KidBuilder.Build(this, model);
            Anim = gameObject.AddComponent<KidAnimator>();
            Anim.Rig = Rig;
            Anim.Animator = GetComponentInChildren<Animator>();
            // physics: kinematic body + capsule for the torso, sphere for the head (head-shots BONK louder)
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            var cap = gameObject.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 0.36f * Rig.Scale, 0);
            cap.height = 0.72f * Rig.Scale;
            cap.radius = 0.19f * Rig.Scale;
            var headCol = new GameObject("Head Collider");
            headCol.layer = Layers.Kid;
            headCol.transform.SetParent(Rig.Head, false);
            headCol.transform.localPosition = new Vector3(0, 0.13f, 0);
            var sc = headCol.AddComponent<SphereCollider>();
            sc.radius = 0.2f;
            headCol.name = "Head Collider";
            if (Anim.Animator && Anim.Animator.avatar && Anim.Animator.avatar.isHuman)
            {
                // a real head bone: its axes and scale aren't the mockup's, so place and size in world terms
                headCol.transform.position = Rig.Head.position + Vector3.up * 0.08f * Rig.Height / 1.05f;
                sc.radius = 0.15f * Rig.Height / 1.05f / KidBuilder.WorldScale(headCol.transform);
                cap.center = new Vector3(0, Rig.Height * 0.36f, 0);
                cap.height = Rig.Height * 0.72f;
                cap.radius = 0.15f * Rig.Height;
            }
            m_Prints = gameObject.AddComponent<FootprintStamper>();
            m_Prints.kid = this;
            HomePosition = transform.position;
            HomeRotation = transform.rotation;
            m_LastPos = transform.position;
            m_NextBark = Time.time + Random.Range(18f, 40f);
            m_NextWander = Time.time + Random.Range(2f, 6f);
            m_NextFidget = Time.time + Random.Range(1.5f, 5f);
        }

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); SnowballTargets.Register(this); }
        protected virtual void OnDisable() { All.Remove(this); SnowballTargets.Unregister(this); }

        protected virtual void Update()
        {
            var dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Velocity = (transform.position - m_LastPos) / dt;
            m_LastPos = transform.position;
            if (Anim)
            {
                Anim.Velocity = Velocity;
                Anim.Talking = DialogueRunner.IsSpeaking(id);
                var rig = PlayerRig.Active;
                if (rig && !Anim.LookTarget) Anim.LookTarget = rig.Head;
            }
            // idle: turn to face the player if they're near and we're not walking
            var player = PlayerRig.Active;
            if (player && m_Move == null && CanMove && !Busy)
            {
                // whatever has our attention (the friend we walked over to, Frosty) for a while, else you if you're near
                float d = FrostUtil.FlatDistance(transform.position, player.Head.position);
                bool attending = Time.time < m_AttendUntil && d > 1.2f;
                if (attending || d < 2.6f)
                {
                    var face = FrostUtil.FlatLookRotation(transform.position, attending ? m_Attend : player.Head.position);
                    transform.rotation = Ease.Damp(transform.rotation, face, 2.5f, Time.deltaTime);
                }
                if (Anim && attending) Anim.LookOverride = m_Attend;
                else if (Anim) Anim.LookOverride = null;
            }
            // wander: every so often stroll to a free spot near home, if there's space for it
            var gm = GameManager.I;
            bool calm = gm && !gm.IsPaused && !DialogueRunner.IsSpeaking(id) && !(Anim && Anim.IsThrowing)
                        && (gm.State == GameState.Investigating || gm.State == GameState.Title);
            if (WandersWhenIdle && player && m_Move == null && CanMove && !Busy && calm && gm.State == GameState.Investigating && Time.time > m_NextWander)
            {
                m_NextWander = Time.time + Random.Range(3.5f, 8f);
                if (FrostUtil.FlatDistance(transform.position, player.Head.position) > 0.9f && TryWanderSpot(player, out var spot, out var attend))
                    MoveTo(spot, wanderSpeed, () =>
                    {
                        if (attend == Vector3.zero) return;
                        m_Attend = attend;
                        m_AttendUntil = Time.time + Random.Range(3f, 6f);
                    });
            }
            // personal space: if two of us have ended up shoulder to shoulder, one steps away
            if (player && m_Move == null && CanMove && !Busy && calm && Time.time > m_NextSeparate)
            {
                m_NextSeparate = Time.time + 0.5f;
                Kid near = null; float nd = 0.6f;
                foreach (var k in All) if (k && k != this) { float d = FrostUtil.FlatDistance(transform.position, k.transform.position); if (d < nd) { nd = d; near = k; } }
                var room2 = RoomService.Current;
                if (near && !near.IsMoving && (int)id < (int)near.id && room2 != null)
                {
                    var away = (transform.position - near.transform.position).Flat();
                    away = away.sqrMagnitude > 1e-4f ? away.normalized : transform.right;
                    var p = transform.position + away * 0.6f;
                    if (room2.EdgeDistance(new Vector2(p.x, p.z)) > 0.25f && (room2.IsBlocked == null || !room2.IsBlocked(p, 0.2f))) MoveTo(p, wanderSpeed);
                }
            }
            // little human things while standing about: look round, rub cold hands, scratch, stretch, wave...
            if (Anim && Anim.IsHumanoid && player && m_Move == null && !Busy && calm && Time.time > m_NextFidget)
            {
                m_NextFidget = Time.time + Random.Range(3f, 7f);
                Anim.Fidget(FidgetLook(player));
            }
            // ambient barks when the player wanders close and nothing else is happening
            if (player && Time.time > m_NextBark && !Busy && !DialogueRunner.AnyoneSpeaking && GameManager.I && GameManager.I.State == GameState.Investigating)
            {
                m_NextBark = Time.time + Random.Range(35f, 60f);
                if (FrostUtil.FlatDistance(transform.position, player.Head.position) < 2.2f) Bark();
            }
        }

        protected virtual void Bark()
        {
            var line = Lines.Get(Cast.Key(id) + "_bark");
            if (line != null) DialogueRunner.Say(line, this, false);
        }

        // ------------------------------------------------------------------ movement
        public void MoveTo(Vector3 target, float speed, System.Action done = null)
        {
            if (m_Move != null) StopCoroutine(m_Move);
            m_MoveTarget = target;
            m_Move = StartCoroutine(MoveRoutine(target, speed, done));
        }

        public void StopMoving()
        {
            if (m_Move != null) StopCoroutine(m_Move);
            m_Move = null;
        }

        public bool IsMoving => m_Move != null;

        IEnumerator MoveRoutine(Vector3 target, float speed, System.Action done)
        {
            var room = RoomService.Current;
            if (room != null) target.y = room.FloorY;
            while (true)
            {
                var to = (target - transform.position).Flat();
                float dist = to.magnitude;
                if (dist < 0.05f) break;
                var dir = to / dist;
                var face = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Ease.Damp(transform.rotation, face, 10f, Time.deltaTime);
                float step = Mathf.Min(dist, speed * Time.deltaTime);
                transform.position += dir * step;
                yield return null;
            }
            m_Move = null;
            done?.Invoke();
        }

        /// <summary>Somewhere to stroll: a free spot in our part of the room, over to stand with a friend, or round to
        /// have a look at Frosty. Only where there's room: inside the walls, off the furniture, clear of you, Frosty and
        /// the others, and with a clear straight walk there.</summary>
        bool TryWanderSpot(PlayerRig player, out Vector3 spot, out Vector3 attend)
        {
            spot = default; attend = Vector3.zero;
            var room = RoomService.Current;
            if (room == null) return false;
            var frosty = Frosty.Instance ? Frosty.Instance.transform.position : new Vector3(1e4f, 0f, 1e4f);
            float kind = Random.value;
            float bestGap = -1f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 p, look = Vector3.zero;
                if (kind < 0.12f && All.Count > 1)
                {
                    var other = All[Random.Range(0, All.Count)];
                    if (!other || other == this) continue;
                    var away = (transform.position - other.transform.position).Flat();
                    if (away.sqrMagnitude < 1e-4f) away = Vector3.forward;
                    p = other.transform.position + Quaternion.Euler(0, Random.Range(-70f, 70f), 0) * away.normalized * 1.1f;
                    look = other.HeadPoint;
                }
                else if (kind < 0.32f && Frosty.Instance)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    p = frosty + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(1.1f, 1.35f);
                    look = frosty + Vector3.up * 0.1f;
                }
                else
                {
                    var c = Random.insideUnitCircle * wanderRadius;
                    p = new Vector3(HomePosition.x + c.x, 0f, HomePosition.z + c.y);
                }
                p.y = room.FloorY;
                if (FrostUtil.FlatDistance(p, HomePosition) > wanderRadius + 0.6f) continue;   // stay in our part of the room
                if (FrostUtil.FlatDistance(p, transform.position) < 0.4f) continue;
                if (room.EdgeDistance(new Vector2(p.x, p.z)) < 0.3f) continue;
                if (room.IsBlocked != null && room.IsBlocked(p, 0.22f)) continue;
                if (FrostUtil.FlatDistance(p, player.Head.position) < 0.8f) continue;
                if (FrostUtil.FlatDistance(p, frosty) < 0.95f) continue;
                float gap = 10f;   // from where the others stand and where they're walking to
                foreach (var k in All) if (k && k != this) gap = Mathf.Min(gap, Mathf.Min(FrostUtil.FlatDistance(p, k.transform.position), FrostUtil.FlatDistance(p, k.Destination)));
                if (gap < (look != Vector3.zero ? 1.0f : 0.95f)) continue;   // keep our distance from the others
                if (!WanderSpotOk(p) || !PathClear(room, transform.position, p, frosty, player)) continue;
                // of the spots that work, take the roomiest (friends and Frosty visits take the first that fits)
                if (look != Vector3.zero) { spot = p; attend = look; return true; }
                if (gap > bestGap) { bestGap = gap; spot = p; attend = Vector3.zero; }
            }
            return bestGap > 0f;
        }

        bool PathClear(RoomInfo room, Vector3 from, Vector3 to, Vector3 frosty, PlayerRig player)
        {
            float len = FrostUtil.FlatDistance(from, to);
            for (float d = 0.2f; d < len; d += 0.2f)
            {
                var q = Vector3.Lerp(from, to, d / len);
                if (room.IsBlocked != null && room.IsBlocked(q, 0.2f)) return false;
                if (FrostUtil.FlatDistance(q, frosty) < 0.75f || FrostUtil.FlatDistance(q, player.Head.position) < 0.45f) return false;
                foreach (var k in All) if (k && k != this && FrostUtil.FlatDistance(q, k.transform.position) < 0.5f) return false;
            }
            return true;
        }

        protected virtual bool WanderSpotOk(Vector3 p) => true;

        /// <summary>Something to glance at: a friend, Frosty, you, or somewhere in the room.</summary>
        Vector3 FidgetLook(PlayerRig player)
        {
            float r = Random.value;
            if (r < 0.4f && All.Count > 1)
            {
                var other = All[Random.Range(0, All.Count)];
                if (other && other != this) return other.HeadPoint;
            }
            if (r < 0.6f && Frosty.Instance) return Frosty.Instance.transform.position + Vector3.up * 0.1f;
            if (r < 0.8f) return player.Head.position;
            var a = Random.Range(-100f, 100f);
            return HeadPoint + Quaternion.Euler(0, a, 0) * transform.forward * 2f + Vector3.up * Random.Range(-0.4f, 0.3f);
        }

        // ------------------------------------------------------------------ snowball fights (rigged kids)
        /// <summary>Scoop up a snowball and throw it at a world point. False if this kid can't right now.</summary>
        public bool ThrowSnowballAt(Vector3 target)
        {
            if (!Anim || !Anim.IsHumanoid || Anim.IsThrowing || Busy) return false;
            StopMoving();
            StartCoroutine(ThrowRoutine(target));
            return true;
        }

        IEnumerator ThrowRoutine(Vector3 target)
        {
            Busy = true;
            yield return Anim.ScoopAndThrow(this, target);
            Busy = false;
        }

        internal static void NotifyThrew(Kid kid, Snowball ball) => ThrewSnowball?.Invoke(kid, ball);

        public void Face(Vector3 worldPoint, float seconds = 0.4f) => StartCoroutine(FaceRoutine(worldPoint, seconds));
        IEnumerator FaceRoutine(Vector3 p, float seconds)
        {
            var from = transform.rotation;
            var to = FrostUtil.FlatLookRotation(transform.position, p);
            yield return Ease.Tween(seconds, t => transform.rotation = Quaternion.Slerp(from, to, t), Ease.OutCubic, false);
        }

        // ------------------------------------------------------------------ the camera
        /// <summary>Called when this kid is in frame of a photo — each kid has a pose (doc: Sherman throws up two peace signs, Tony flexes).</summary>
        public virtual void OnPhotographed(PhotoData photo)
        {
            if (Busy || Time.time - m_LastPoseTime < 6f) return;
            m_LastPoseTime = Time.time;
            Anim.StrikePose(PoseFor(), 1.3f);
            var line = Lines.Get(Cast.Key(id) + "_pose");
            if (line != null && !DialogueRunner.AnyoneSpeaking) DialogueRunner.Say(line, this, false);
        }

        protected virtual Pose PoseFor() => Pose.Cheer;

        // ------------------------------------------------------------------ snowballs (just for fun, required only in the finale)
        public virtual void OnSnowballHit(Snowball ball, Vector3 point, Vector3 velocity, bool headshot)
        {
            Anim.HitReact();
            AudioDirector.PlayAt(headshot ? Sfx.Bonk : Sfx.Splat, point, headshot ? 0.7f : 0.9f, Random.Range(0.95f, 1.08f));
            SplatDecal.Spawn(point, (point - ChestPoint).normalized, 0.12f, Rig.Spine);
            if (Time.time - m_LastHitLine > 4f && !Busy)
            {
                m_LastHitLine = Time.time;
                HitGag();
                var line = Lines.Get(Cast.Key(id) + "_hit");
                if (line != null) DialogueRunner.Say(line, this, true);
            }
            CaseDirector.I?.NotifyActivity();
        }

        protected virtual void HitGag() { }

        // ------------------------------------------------------------------ dialogue helpers
        public IEnumerator SayAndWait(string lineId, bool interrupt = true)
        {
            var line = Lines.Get(lineId);
            if (line == null) yield break;
            yield return DialogueRunner.SayAndWait(line, this, interrupt);
        }
    }

    /// <summary>Kids leave little boot prints when they walk (and Tony's sled leaves runner tracks).</summary>
    public class FootprintStamper : MonoBehaviour
    {
        public Kid kid;
        public float stride = 0.24f;
        Vector3 m_Last; bool m_Left; bool m_Init;

        void Update()
        {
            var snow = SnowSurface.Instance;
            if (!snow) return;
            var p = transform.position;
            if (!m_Init) { m_Last = p; m_Init = true; return; }
            var d = (p - m_Last).Flat();
            if (d.magnitude < stride * (kid ? kid.Rig.Scale : 1f)) return;
            var dir = d.normalized;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var side = Vector3.Cross(Vector3.up, dir) * (m_Left ? -0.07f : 0.07f);
            snow.Stamp(p + side, yaw, m_Left ? StampShape.KidBootLeft : StampShape.KidBootRight, 0.19f, 0.8f);
            if (Random.value < 0.35f) AudioDirector.PlayAt(Sfx.Footstep, p, 0.25f, Random.Range(1.15f, 1.35f));
            m_Left = !m_Left;
            m_Last = p;
        }
    }
}
