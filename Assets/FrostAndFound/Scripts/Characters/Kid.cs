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
        float m_LastPoseTime = -10f, m_LastHitLine = -10f, m_NextBark;
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
            m_Prints = gameObject.AddComponent<FootprintStamper>();
            m_Prints.kid = this;
            HomePosition = transform.position;
            HomeRotation = transform.rotation;
            m_LastPos = transform.position;
            m_NextBark = Time.time + Random.Range(18f, 40f);
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
                float d = FrostUtil.FlatDistance(transform.position, player.Head.position);
                if (d < 2.6f)
                {
                    var face = FrostUtil.FlatLookRotation(transform.position, player.Head.position);
                    transform.rotation = Ease.Damp(transform.rotation, face, 2.5f, Time.deltaTime);
                }
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
