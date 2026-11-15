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
        /// <summary>Face texture swapper (null for kids without painted face variants).</summary>
        public KidFaces Faces { get; private set; }
        public KidAnimator Anim { get; private set; }
        public Vector3 Velocity { get; private set; }
        public bool Busy { get; set; }
        /// <summary>Seeing stars after a snowball or a punch: they stand there dizzy and do nothing else (walks pause,
        /// a throw in progress is dropped, no new throws or poses) until it wears off.</summary>
        public bool IsStunned => Time.time < m_StunnedUntil;
        public const float StunSeconds = 2.4f;
        float m_StunnedUntil = -1f;
        Coroutine m_Throw;
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
        /// <summary>Kids get on with their own thing while you investigate (see <see cref="KidBrain"/>).</summary>
        protected virtual bool WandersWhenIdle => true;
        public float wanderSpeed = 0.65f;
        KidBrain m_Brain;
        /// <summary>What the idle AI is up to (for debugging).</summary>
        public string Doing => m_Brain != null ? m_Brain.Doing : "";
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
            // painted face variants (mouth shapes, dizzy eyes) for rigged models that have them
            if (Anim.Animator && Anim.Animator.avatar && Anim.Animator.avatar.isHuman)
                Faces = KidFaces.Attach(this, GetComponentInChildren<SkinnedMeshRenderer>());
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
            m_Brain = new KidBrain(this);
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
            if (player && m_Move == null && CanMove && !Busy && !IsStunned)
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
            // their own little lives: wander the room, visit friends and Frosty, come and see you, poke about, run
            var gm = GameManager.I;
            bool calm = gm && !gm.IsPaused && !DialogueRunner.IsSpeaking(id) && !(Anim && Anim.IsThrowing) && !SnowballFight.Active
                        && (gm.State == GameState.Investigating || gm.State == GameState.Title);
            if (WandersWhenIdle && player && CanMove && !Busy && !IsStunned && calm && gm.State == GameState.Investigating) m_Brain?.Tick(player);
            else m_Brain?.Reset();
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
            // (one bit of chatter at a time across all the kids, and never straight after someone else has spoken)
            if (player && Time.time > m_NextBark && Time.time > s_NextAnyBark && DialogueRunner.QuietFor > 6f && !Busy && !SnowballFight.Active
                && GameManager.I && GameManager.I.State == GameState.Investigating)
            {
                m_NextBark = Time.time + Random.Range(28f, 50f);
                if (FrostUtil.FlatDistance(transform.position, player.Head.position) < 2.8f)
                {
                    s_NextAnyBark = Time.time + Random.Range(10f, 18f);
                    Bark();
                }
            }
        }

        string m_LastBark;
        static float s_NextAnyBark;

        protected virtual void Bark()
        {
            var line = Lines.Bark(id, m_LastBark);
            if (line == null) return;
            m_LastBark = line.Id;
            DialogueRunner.Say(line, this, false);
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

        /// <summary>Look at something (a friend, Frosty, a tree) for a while when standing about.</summary>
        public void Attend(Vector3 at, float seconds)
        {
            m_Attend = at;
            m_AttendUntil = Time.time + seconds;
        }

        internal bool SpotOk(Vector3 p) => WanderSpotOk(p);

        /// <summary>Walk somewhere by a planned route round the furniture, trees and props, easing in and out and
        /// stepping round the others and you on the way. False if there's no way there.</summary>
        public bool WalkTo(Vector3 target, float speed, System.Action arrived = null)
        {
            var path = KidNav.FindPath(transform.position, target);
            if (path == null || path.Count == 0) return false;
            if (m_Move != null) StopCoroutine(m_Move);
            m_MoveTarget = path[path.Count - 1];
            m_Move = StartCoroutine(PathRoutine(path, speed, arrived));
            return true;
        }

        IEnumerator PathRoutine(List<Vector3> path, float speed, System.Action arrived)
        {
            var room = RoomService.Current;
            float v = 0f, checkAt = Time.time + 1.2f;
            var checkPos = transform.position;
            int i = 0;
            bool ok = false;
            while (i < path.Count)
            {
                while (IsStunned) { v = 0f; checkAt = Time.time + 1.2f; yield return null; }
                var target = path[i];
                if (room != null) target.y = room.FloorY;
                var to = (target - transform.position).Flat();
                float dist = to.magnitude;
                bool last = i == path.Count - 1;
                if (!last && dist < 0.25f) { i++; continue; }
                if (last && dist < 0.05f) { ok = true; break; }
                var want = to / dist;
                var right = Vector3.Cross(Vector3.up, want);

                // step round the others and you: sidestep (keeping right) anyone just ahead; slow down if they're right in the way
                Vector3 avoid = Vector3.zero; float slow = 1f;
                void Around(Vector3 other, float radius)
                {
                    var rel = (other - transform.position).Flat();
                    float d = rel.magnitude;
                    if (d > radius || d < 1e-3f) return;
                    float ahead = Vector3.Dot(rel / d, want);
                    if (ahead < 0.15f) return;
                    float side = Vector3.Dot(rel, right) >= 0f ? -1f : 1f;
                    avoid += right * side * (1f - d / radius) * ahead * 1.6f;
                    if (ahead > 0.7f && d < radius * 0.6f) slow = Mathf.Min(slow, Mathf.Clamp01((d - 0.3f) / (radius * 0.3f)) + 0.15f);
                }
                foreach (var k in All) if (k && k != this) Around(k.transform.position, 0.85f);
                var player = PlayerRig.Active;
                if (player) Around(player.Head.position, 0.9f);
                var dir = (want + avoid).Flat();
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : want;
                if (!KidNav.Walkable(transform.position + dir * 0.25f) && KidNav.Walkable(transform.position + want * 0.25f)) dir = want;   // don't sidestep into a tree

                // turn on the spot first if it's well behind us, then set off; ease in, ease to a stop at the end
                float angle = Vector3.Angle(transform.forward.Flat(), dir);
                transform.rotation = Ease.Damp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 8f, Time.deltaTime);
                float targetV = speed * slow * (angle > 75f ? 0.1f : Mathf.Lerp(1f, 0.5f, angle / 75f));
                if (last) targetV *= Mathf.Clamp01(dist / 0.45f) * 0.8f + 0.2f;
                v = Mathf.MoveTowards(v, targetV, (targetV > v ? 1.8f : 3.5f) * Time.deltaTime);
                transform.position += dir * Mathf.Min(v * Time.deltaTime, dist);

                // stuck behind someone for too long: give up and think again
                if (Time.time > checkAt)
                {
                    if (FrostUtil.FlatDistance(transform.position, checkPos) < 0.08f) break;
                    checkPos = transform.position;
                    checkAt = Time.time + 1.2f;
                }
                yield return null;
            }
            m_Move = null;
            if (ok) arrived?.Invoke();
        }

        IEnumerator MoveRoutine(Vector3 target, float speed, System.Action done)
        {
            var room = RoomService.Current;
            if (room != null) target.y = room.FloorY;
            while (true)
            {
                while (IsStunned) yield return null;   // dizzy: the walk waits
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
            if (!Anim || !Anim.IsHumanoid || Anim.IsThrowing || Busy || IsStunned) return false;
            StopMoving();
            m_Throw = StartCoroutine(ThrowRoutine(target));
            return true;
        }

        IEnumerator ThrowRoutine(Vector3 target)
        {
            Busy = true;
            yield return Anim.ScoopAndThrow(this, target);
            Busy = false;
            m_Throw = null;
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
            if (Busy || IsStunned || Time.time - m_LastPoseTime < 6f) return;
            m_LastPoseTime = Time.time;
            Anim.StrikePose(PoseFor(), 1.3f);
            if (Faces) Faces.Show(FaceExpression.O, 0.6f);
            var line = Lines.Get(Cast.Key(id) + "_pose");
            if (line != null && !DialogueRunner.AnyoneSpeaking) DialogueRunner.Say(line, this, false);
        }

        protected virtual Pose PoseFor() => Pose.Cheer;
        /// <summary>Their photo pose (Sherman's peace signs, Serena's hair flip...), e.g. on seeing themselves in a picture.</summary>
        public Pose SignaturePose => PoseFor();

        // ------------------------------------------------------------------ snowballs (just for fun, required only in the finale)
        public virtual void OnSnowballHit(Snowball ball, Vector3 point, Vector3 velocity, bool headshot) => TakeHit(point, velocity, headshot, true, ball && !ball.Thrower);

        /// <summary>Punched by your hand (superhot-style): the same stagger and daze as a snowball, minus the splat.</summary>
        public virtual void OnPunched(Vector3 point, Vector3 velocity, bool headshot) => TakeHit(point, velocity, headshot, false);

        protected void TakeHit(Vector3 point, Vector3 velocity, bool headshot, bool snowball, bool playerSnowball = false)
        {
            Anim.HitReact();
            if (Faces) Faces.Dizzy(StunSeconds);
            m_StunnedUntil = Time.time + StunSeconds;
            if (m_Throw != null) { StopCoroutine(m_Throw); m_Throw = null; Busy = false; Anim.CancelThrow(); }
            SatieSound.Fire("kid_stunned", HeadPoint);
            if (snowball)
            {
                // (with Satie the snowball's own burst is the sound)
                if (!SatieSound.On) AudioDirector.PlayAt(headshot ? Sfx.Bonk : Sfx.Splat, point, headshot ? 0.7f : 0.9f, Random.Range(0.95f, 1.08f));
            }
            else
            {
                if (!SatieSound.Fire("punch_hit", point)) AudioDirector.PlayAt(Sfx.Bonk, point, headshot ? 0.85f : 0.65f, Random.Range(1.02f, 1.15f));
                Fx.SnowPuff(point, velocity.sqrMagnitude > 1e-6f ? -velocity.normalized : Vector3.up, 8, 0.45f);   // frost knocked off the coat
            }
            HitLine(playerSnowball);
            CaseDirector.I?.NotifyActivity();
        }

        /// <summary>The line for a hit: their usual yelp, or, if you keep hitting the same kid with snowballs, a
        /// complaint, then "don't you DARE", then "that's IT!" and a snowball fight (see SnowballFight).</summary>
        void HitLine(bool playerSnowball)
        {
            int n = playerSnowball && SnowballFight.I ? SnowballFight.I.RegisterHit(this) : 0;
            string key = Cast.Key(id);
            if (n >= 2 || (Time.time - m_LastHitLine > 4f && !Busy))
            {
                m_LastHitLine = Time.time;
                if (n <= 1) HitGag();   // the gag (books dropped, hat hop...) is the first hit's
                string lineId = n == 2 ? key + "_complain" : n == 3 ? key + "_warn" : n >= 4 ? key + "_fight_start" : key + "_hit";
                var line = Lines.Get(lineId) ?? Lines.Get(key + "_hit");
                if (line != null) DialogueRunner.Say(line, this, true);
            }
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
            snow.Stamp(p + side, yaw, m_Left ? StampShape.KidBootLeft : StampShape.KidBootRight, 0.19f, 0.45f);
            if (!SatieSound.Fire("kid_step", p, SatieSound.Snow) && Random.value < 0.35f) AudioDirector.PlayAt(Sfx.Footstep, p, 0.25f, Random.Range(1.15f, 1.35f));
            m_Left = !m_Left;
            m_Last = p;
        }
    }
}
