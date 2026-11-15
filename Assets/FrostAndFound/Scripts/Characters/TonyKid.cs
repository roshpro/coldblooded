using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Tony "Turbo" Ramirez — the culprit. After the sled photo he refuses to be photographed and runs: he
    /// zig-zags around the far side of the room, ducks / dives / hides behind his sled whenever he's in your
    /// viewfinder (so any photo comes out as a useless blur), stops to taunt you, and only a snowball BONK sits
    /// him down dazed for ~4 s. If he still hasn't been hit ~25 s after bolting he trips over his own sled.
    /// </summary>
    public class TonyKid : Kid
    {
        public enum TonyState { Idle, Fleeing, Taunting, Dazed, Caught }
        public TonyState State { get; private set; } = TonyState.Idle;
        public Sled Sled { get; private set; }
        public bool IsDazed => State == TonyState.Dazed;
        public bool IsDodging => m_DodgeUntil > Time.time;
        public bool WasEverHit { get; private set; }
        public float FleeStartTime { get; private set; }
        public override bool AssistEligible => State != TonyState.Caught;

        Transform m_Goggles;
        float m_DodgeUntil, m_NextDodge, m_NextTaunt, m_TripAt, m_LastNearMissLine, m_SpeedNow;
        Vector3 m_Waypoint;
        bool m_HasWaypoint;
        // ducking behind your real furniture (only when Space Setup has some)
        float m_CoverUntil, m_NextCover;
        RoomFurniture m_CoverPiece;
        bool m_CoverPending;
        int m_WaypointsSinceTaunt;
        Coroutine m_DazeRoutine;
        DazeBirdies m_Birdies;
        GameObject m_FaceSnow;
        readonly System.Random m_Rng = new System.Random(5);

        protected override void Build()
        {
            base.Build();
            m_Goggles = Rig.Head ? Rig.Head.Find("Goggles") : null;
            Sled = Sled.Create(this);
            Snowball.Landed += OnSnowballLanded;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Snowball.Landed -= OnSnowballLanded;
        }

        protected override Pose PoseFor() => Pose.Flex;
        protected override bool WandersWhenIdle => State == TonyState.Idle;
        // he keeps away from his own sled so it doesn't give him away
        protected override bool WanderSpotOk(Vector3 p) => !Sled || FrostUtil.FlatDistance(p, Sled.transform.position) > 1.4f;

        public override void OnPhotographed(PhotoData photo)
        {
            if (State == TonyState.Idle) base.OnPhotographed(photo); // "Get my good side." — flexes into the shot
        }

        // ------------------------------------------------------------------ finale entry
        public IEnumerator Confronted()
        {
            Busy = true;
            // the chase doesn't wait on the speech: goggles come down a beat into his excuse (or as it ends), and he's
            // already running as he shouts his defence
            var excuse = Lines.Get("tony_excuse");
            if (excuse != null) DialogueRunner.Say(excuse, this, true);
            yield return null;
            for (float t = 0f; t < 2.2f && DialogueRunner.IsSpeaking(id); t += Time.deltaTime) yield return null;
            yield return GogglesDown();
            var defence = Lines.Get("tony_defense");
            if (defence != null) DialogueRunner.Say(defence, this, true);
            Busy = false;
            Bolt();
        }

        IEnumerator GogglesDown()
        {
            if (!m_Goggles) yield break;
            var from = m_Goggles.localRotation;
            var to = Quaternion.Euler(4f, 0, 0);
            var fromP = m_Goggles.localPosition;
            var toP = new Vector3(0, 0.215f, 0.0f);
            AudioDirector.PlayAt(Sfx.Boing, HeadPoint, 0.4f, 1.6f);
            yield return Ease.Tween(0.18f, t => { m_Goggles.localRotation = Quaternion.Slerp(from, to, t); m_Goggles.localPosition = Vector3.Lerp(fromP, toP, t); }, Ease.OutBack, false);
        }

        void Bolt()
        {
            State = TonyState.Fleeing;
            FleeStartTime = Time.time;
            m_TripAt = Time.time + FrostTuning.I.tonyTripAfterSeconds;
            m_NextTaunt = Time.time + 4f;
            m_HasWaypoint = false;
            CanMove = false; // movement is driven here, not by Kid idling
            Anim.LookTarget = PlayerRig.Active ? PlayerRig.Active.Head : null;
        }

        // ------------------------------------------------------------------ per frame
        protected override void Update()
        {
            base.Update();
            if (IsStunned) return;   // seeing stars: no running, taunting or dodging
            if (State == TonyState.Fleeing) UpdateFlee();
            else if (State == TonyState.Taunting) { var p = PlayerRig.Active; if (p) transform.rotation = Ease.Damp(transform.rotation, FrostUtil.FlatLookRotation(transform.position, p.Head.position), 6f, Time.deltaTime); }
        }

        void UpdateFlee()
        {
            var rig = PlayerRig.Active;
            var room = RoomService.Current;
            if (!rig || room == null) return;
            var tune = FrostTuning.I;
            var player = rig.FeetPosition;

            // safety net: trips over his own sled (and again every 20 s after he's back up, so the finale can't stall)
            if (Time.time > m_TripAt) { StartCoroutine(Trip()); return; }

            float distToPlayer = FrostUtil.FlatDistance(transform.position, player);

            // crouched behind your sofa / cabinet / bed, peeking at you: the real furniture hides him (Depth API)
            if (Time.time < m_CoverUntil)
            {
                m_SpeedNow = 0f;
                transform.rotation = Ease.Damp(transform.rotation, FrostUtil.FlatLookRotation(transform.position, rig.Head.position), 6f, Time.deltaTime);
                if (distToPlayer < 1.4f) { m_CoverUntil = 0f; m_HasWaypoint = false; }   // you came round it: bolt!
                return;
            }

            // dodge the camera whenever he is in the viewfinder
            if (InViewfinder() && Time.time > m_NextDodge && !IsDodging) Dodge();
            float speed = distToPlayer < 1.2f ? tune.tonyPanicSpeed : tune.tonyRunSpeed;
            if (IsDodging) speed *= 0.25f;
            m_SpeedNow = Ease.Damp(m_SpeedNow, speed, 6f, Time.deltaTime);

            if (!m_HasWaypoint || FrostUtil.FlatDistance(transform.position, m_Waypoint) < 0.12f || (distToPlayer < 1.0f && FrostUtil.FlatDistance(m_Waypoint, player) < 1.4f))
            {
                if (m_HasWaypoint && m_CoverPending && FrostUtil.FlatDistance(transform.position, m_Waypoint) < 0.12f)
                {
                    m_CoverPending = false;
                    TakeCover();
                    return;
                }
                if (m_HasWaypoint) m_WaypointsSinceTaunt++;
                if (m_HasWaypoint && m_WaypointsSinceTaunt >= 2 && Time.time > m_NextTaunt && distToPlayer > 1.6f && !IsDodging)
                {
                    StartCoroutine(Taunt());
                    return;
                }
                m_Waypoint = PickWaypoint(room, player);
                m_HasWaypoint = true;
            }
            // run
            var to = (m_Waypoint - transform.position).Flat();
            if (to.sqrMagnitude > 1e-4f)
            {
                var dir = to.normalized;
                transform.rotation = Ease.Damp(transform.rotation, Quaternion.LookRotation(dir), 9f, Time.deltaTime);
                transform.position = KeepApart(transform.position, transform.position + dir * Mathf.Min(to.magnitude, m_SpeedNow * Time.deltaTime));
            }
        }

        Vector3 PickWaypoint(RoomInfo room, Vector3 player)
        {
            m_CoverPending = false;
            // now and then, make for the far side of a real piece of furniture and duck down behind it
            if (Time.time > m_NextCover && room.Furniture.Count > 0 && m_Rng.NextDouble() < 0.55 && TryCoverSpot(room, player, out var cover))
            {
                m_CoverPending = true;
                m_NextCover = Time.time + 6f + (float)m_Rng.NextDouble() * 4f;
                return cover;
            }
            var frosty = FrostyMarker.Position;
            Vector3 best = transform.position; float bestScore = float.MinValue;
            var away = (transform.position - player).Flat().normalized;
            for (int i = 0; i < 24; i++)
            {
                var p = room.RandomFreePoint(0.25f, 0.45f, m_Rng, 12);
                float dPlayer = FrostUtil.FlatDistance(p, player);
                float dSelf = FrostUtil.FlatDistance(p, transform.position);
                if (dSelf < 0.7f || dSelf > 2.6f) continue;
                if (frosty.HasValue && (SegmentDistance(transform.position.XZ(), p.XZ(), frosty.Value.XZ()) < Frosty.FootprintRadius + 0.1f
                    || FrostUtil.FlatDistance(p, frosty.Value) < Frosty.FootprintRadius + 0.3f)) continue;   // round him, never through him
                if (SegmentDistance(transform.position.XZ(), p.XZ(), player.XZ()) < 0.8f) continue;
                if (!KidNav.LineClear(transform.position, p)) continue;   // never straight through the sofa or a tree
                // far side of the room, sideways to his current heading (zig-zag)
                var dir = (p - transform.position).Flat().normalized;
                float zig = 1f - Mathf.Abs(Vector3.Dot(dir, transform.forward));
                float score = dPlayer * 1.5f + zig * 0.8f + Vector3.Dot(dir, away) * 0.3f + (float)m_Rng.NextDouble() * 0.3f;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            if (bestScore == float.MinValue) best = room.RandomFreePoint(0.25f, 0.45f, m_Rng);
            return best;
        }

        /// <summary>The far side of a real sofa, cabinet, bed or table from you, reachable in a straight dash.</summary>
        bool TryCoverSpot(RoomInfo room, Vector3 player, out Vector3 spot)
        {
            spot = default;
            float best = float.MinValue;
            var frosty = FrostyMarker.Position;
            foreach (var f in room.Furniture)
            {
                if (f.Height < 0.45f || f.Kind == FurnitureKind.Lamp || f.Kind == FurnitureKind.Plant || f.Kind == FurnitureKind.Screen) continue;
                // anywhere round it that the furniture hides from you: behind it, or tucked round the end of a sofa
                for (int a = 0; a < 12; a++)
                {
                    var dir = Quaternion.Euler(0f, a * 30f, 0f) * f.Forward;
                    var p = f.EdgePoint(f.Center + dir * 10f, 0.3f, room.FloorY);
                    // hidden means both his shoulders are behind it, not just a corner clipping the line
                    var side = Vector3.Cross(Vector3.up, (p - player).Flat().normalized) * 0.16f;
                    if (!f.Blocks(player, p + side) || !f.Blocks(player, p - side)) continue;
                    float dSelf = FrostUtil.FlatDistance(p, transform.position), dPlayer = FrostUtil.FlatDistance(p, player);
                    if (dSelf < 0.6f || dSelf > 4.2f || dPlayer < 1.9f) continue;
                    if (!KidNav.Walkable(p) || !KidNav.LineClear(transform.position, p)) continue;
                    if (SegmentDistance(transform.position.XZ(), p.XZ(), player.XZ()) < 0.9f) continue;   // not past your nose
                    if (frosty.HasValue && SegmentDistance(transform.position.XZ(), p.XZ(), frosty.Value.XZ()) < Frosty.FootprintRadius + 0.1f) continue;
                    // taller and wider cover hides more of him; closer is quicker to reach
                    float score = Mathf.Min(f.Height, 1.1f) * 2f + Mathf.Min(f.HalfWidth, f.HalfDepth) - dSelf * 0.35f + (float)m_Rng.NextDouble() * 0.4f;
                    if (score > best) { best = score; spot = p; m_CoverPiece = f; }
                }
            }
            return best > float.MinValue;
        }

        void TakeCover()
        {
            float dur = 1.6f + (float)m_Rng.NextDouble() * 1.1f;
            m_CoverUntil = Time.time + dur;
            if (Anim) Anim.Duck(dur);
            Debug.Log("[Frost] Tony ducks behind the " + (m_CoverPiece != null ? m_CoverPiece.Kind.ToString().ToLowerInvariant() : "furniture"));
            if (m_Rng.NextDouble() < 0.4 && !DialogueRunner.AnyoneSpeaking)
                DialogueRunner.Say(Lines.Get("tony_dodge_" + (1 + m_Rng.Next(3))), this, false);
        }

        static float SegmentDistance(Vector2 a, Vector2 b, Vector2 p)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        public bool InViewfinder()
        {
            var cam = PolaroidCamera.Instance;
            if (!cam || !cam.Lens) return false;
            var vp = cam.Lens.WorldToViewportPoint(ChestPoint);
            return vp.z > 0.1f && vp.z < 6f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.15f && vp.y < 1.15f;
        }

        void Dodge()
        {
            var tune = FrostTuning.I;
            float dur = 0.65f + (float)m_Rng.NextDouble() * 0.3f;
            m_DodgeUntil = Time.time + dur;
            m_NextDodge = Time.time + dur + tune.tonyDodgeCooldown;
            // a quick sidestep, staying upright (no crouching or diving while he runs)
            StartCoroutine(Hop(Random.value < 0.5f ? -1f : 1f, 0.35f));
            if (m_Rng.NextDouble() < 0.35 && !DialogueRunner.AnyoneSpeaking)
                DialogueRunner.Say(Lines.Get("tony_dodge_" + (1 + m_Rng.Next(3))), this, false);
            AudioDirector.PlayAt(Sfx.Whoosh, ChestPoint, 0.35f, 1.4f);
        }

        IEnumerator Hop(float side, float dist)
        {
            var room = RoomService.Current;
            var from = transform.position;
            var to = from + transform.right * side * dist;
            if (room != null) to = room.ClampInside(to, 0.35f);
            yield return Ease.Tween(0.2f, t => transform.position = Vector3.Lerp(from, to, t), Ease.OutCubic, false);
        }

        IEnumerator Taunt()
        {
            State = TonyState.Taunting;
            m_WaypointsSinceTaunt = 0;
            m_NextTaunt = Time.time + Random.Range(5f, 8f);
            Anim.StrikePose(Random.value < 0.5f ? Pose.Flex : Pose.ArmsCrossed, 1.3f);
            DialogueRunner.Say(Lines.Get("tony_taunt_" + (1 + m_Rng.Next(3))), this, false);
            float until = Time.time + 1.6f;
            while (Time.time < until && State == TonyState.Taunting)
            {
                if (InViewfinder() && !IsDodging && Time.time > m_NextDodge) Dodge();
                yield return null;
            }
            if (State == TonyState.Taunting) State = TonyState.Fleeing;
        }

        // ------------------------------------------------------------------ snowballs
        public override void OnSnowballHit(Snowball ball, Vector3 point, Vector3 velocity, bool headshot)
        {
            if (State == TonyState.Idle) { base.OnSnowballHit(ball, point, velocity, headshot); return; }
            Bonked(point, velocity);
        }

        /// <summary>Catch him and punch him: the same daze as a snowball.</summary>
        public override void OnPunched(Vector3 point, Vector3 velocity, bool headshot)
        {
            if (State == TonyState.Idle) { base.OnPunched(point, velocity, headshot); return; }
            Bonked(point, velocity);
        }

        void Bonked(Vector3 point, Vector3 velocity)
        {
            if (State == TonyState.Dazed || State == TonyState.Caught) { Anim.HitReact(); return; }
            // BONK
            WasEverHit = true;
            AudioDirector.PlayAt(Sfx.Bonk, HeadPoint, 1f, 1f);
            Fx.SnowPuff(point, -velocity.normalized, 22, 0.8f);
            if (m_DazeRoutine != null) StopCoroutine(m_DazeRoutine);
            m_DazeRoutine = StartCoroutine(Daze(FrostTuning.I.tonyDazeSeconds, false));
            CaseDirector.I?.OnTonyHit();
        }

        void OnSnowballLanded(Snowball ball, Vector3 point, ISnowballTarget target)
        {
            if (State != TonyState.Fleeing && State != TonyState.Taunting) return;
            if (ReferenceEquals(target, this)) return;
            if (FrostUtil.FlatDistance(point, transform.position) < 1.6f && Time.time - m_LastNearMissLine > 4f)
            {
                m_LastNearMissLine = Time.time;
                StartCoroutine(NearMiss());
            }
        }

        IEnumerator NearMiss()
        {
            yield return new WaitForSeconds(0.25f);
            if (State == TonyState.Fleeing || State == TonyState.Taunting)
                DialogueRunner.Say(Lines.Get("tony_taunt_1"), this, true); // "Can't catch the Turbo!"
        }

        IEnumerator Trip()
        {
            WasEverHit = true;
            State = TonyState.Dazed;
            DialogueRunner.Say(Lines.Get("tony_trip"), this, true);
            Anim.Dive(0f, 0.3f);
            yield return new WaitForSeconds(0.25f);
            AudioDirector.PlayAt(Sfx.Bonk, transform.position, 0.8f, 0.8f);
            Fx.SnowPuff(transform.position + transform.forward * 0.3f, Vector3.up, 20, 0.9f);
            m_DazeRoutine = StartCoroutine(Daze(FrostTuning.I.tonyDazeSeconds + 2.5f, true));
        }

        float m_DazeBonus;

        /// <summary>A photo of him dazed that didn't count (too far, something in the way): a moment longer on the
        /// ground to try again (at most 3 s extra per daze).</summary>
        public void GiveMoreTime(float seconds)
        {
            if (State == TonyState.Dazed) m_DazeBonus = Mathf.Min(3f, m_DazeBonus + seconds);
        }

        IEnumerator Daze(float seconds, bool tripped)
        {
            State = TonyState.Dazed;
            m_DazeBonus = 0f;
            Anim.SetSitting(true);
            if (!m_Birdies) m_Birdies = DazeBirdies.Create(Rig.Head, HeadPoint);
            if (!tripped && !m_FaceSnow)
            {
                // snow on his face (it shows in the GUILTY photo)
                m_FaceSnow = ProcMesh.Part("Face Snow", Rig.Head, ProcMesh.Sphere(10, 8, 0.15f, 2), Palette.Toon(Palette.Snow, true, 0.3f),
                    new Vector3(0.03f, 0.2f, 0.19f), new Vector3(0.16f, 0.12f, 0.06f));
                ProcMesh.Part("Face Snow Bit", m_FaceSnow.transform, ProcMesh.Sphere(8, 6), Palette.Toon(Palette.Snow, true, 0.3f), new Vector3(-0.6f, 0.3f, 0.2f), Vector3.one * 0.4f);
            }
            float end = Time.time + seconds;
            while (Time.time < end + m_DazeBonus)
            {
                if (State == TonyState.Caught) yield break;
                yield return null;
            }
            if (State == TonyState.Caught) yield break;
            // back up — "Ha! Still got it."
            if (m_Birdies) { m_Birdies.Stop(); m_Birdies = null; }
            Anim.SetSitting(false);
            yield return new WaitForSeconds(0.35f);
            DialogueRunner.Say(Lines.Get("tony_up"), this, true);
            State = TonyState.Fleeing;
            m_TripAt = Time.time + 20f;
            WasEverHit = true;
            m_HasWaypoint = false;
            m_DazeRoutine = null;
        }

        public void Caught()
        {
            State = TonyState.Caught;
            if (m_DazeRoutine != null) StopCoroutine(m_DazeRoutine);
            Anim.SetSitting(true);
        }

        public void ClearDaze()
        {
            if (m_Birdies) { m_Birdies.Stop(); m_Birdies = null; }
        }
    }

    /// <summary>Tony's red sled with a big fresh dent. It stands planted upright in the snow somewhere in the room, so
    /// it isn't obviously his until the clues lead there; when it moves it leaves runner tracks.</summary>
    public class Sled : MonoBehaviour
    {
        bool m_Planted;
        const float Size = 0.75f;   // a kid's sled, not a toboggan

        /// <summary>Stand it up on its tail in the snow at pos, deck and dent facing into the room.</summary>
        public void Plant(Vector3 pos, Vector3 faceDir)
        {
            m_Planted = true;
            if (m_Rope) m_Rope.enabled = false;
            faceDir = faceDir.Flat().sqrMagnitude > 1e-4f ? faceDir.Flat().normalized : Vector3.forward;
            var snow = SnowSurface.Instance;
            float snowY = snow ? snow.SurfaceHeightAt(pos) : pos.y;
            // nose up, the back half buried, leaning back a little as if it was jammed in; only the curl (with the
            // dent) and the top of the deck show, so it takes a look to spot
            transform.rotation = Quaternion.AngleAxis(-12f, Vector3.Cross(Vector3.up, faceDir)) * Quaternion.LookRotation(Vector3.up, faceDir);
            transform.localScale = Vector3.one * Size;
            transform.position = new Vector3(pos.x, snowY + (0.43f - 0.4f) * Size, pos.z);
            // and snow heaped up round it
            snow?.AddDrift(pos, 0.34f, 0.55f, 0.8f, Vector3.SignedAngle(Vector3.forward, faceDir, Vector3.up), 77);
            m_Last = transform.position;
        }
        TonyKid m_Tony;
        LineRenderer m_Rope;
        Transform m_RopeEnd;
        float m_HideUntil;
        Vector3 m_HideFrom;
        Vector3 m_Last;
        public Clue DentClue { get; private set; }

        public static Sled Create(TonyKid tony)
        {
            var go = new GameObject("Red Sled");
            go.layer = Layers.Kid;
            var s = go.AddComponent<Sled>();
            s.m_Tony = tony;
            s.Build();
            go.transform.position = tony.transform.position + tony.transform.right * 0.45f + tony.transform.forward * 0.05f;
            go.transform.rotation = tony.transform.rotation * Quaternion.Euler(0, -70, 0);
            s.m_Last = go.transform.position;
            return s;
        }

        void Build()
        {
            var custom = FrostAssets.Instance ? FrostAssets.Instance.sledModel : null;
            var red = Palette.Toon(Palette.SledRed, true, 0.35f);
            var dark = Palette.Toon(Palette.TonyRedDark, true, 0.2f);
            var metal = Palette.Toon(Palette.CameraBody * 0.7f + Palette.Ink * 0.3f, true, 0.5f);
            var t = transform;
            if (custom) Instantiate(custom, t, false).SetLayerRecursive(Layers.Kid);
            else
            {
                ProcMesh.Part("Deck", t, ProcMesh.RoundedBox(0.2f, 3), red, new Vector3(0, 0.09f, -0.03f), new Vector3(0.42f, 0.05f, 0.78f));
                ProcMesh.Part("Curl", t, ProcMesh.Torus(0.42f, 0.11f, 18, 8), red, new Vector3(0, 0.17f, 0.38f), new Vector3(0.42f, 0.42f, 0.3f), new Vector3(0, 0, 90));
                ProcMesh.Part("Rail L", t, ProcMesh.RoundedBox(0.3f, 2), dark, new Vector3(-0.2f, 0.13f, -0.05f), new Vector3(0.035f, 0.05f, 0.72f));
                ProcMesh.Part("Rail R", t, ProcMesh.RoundedBox(0.3f, 2), dark, new Vector3(0.2f, 0.13f, -0.05f), new Vector3(0.035f, 0.05f, 0.72f));
                ProcMesh.Part("Runner L", t, ProcMesh.RoundedBox(0.4f, 2), metal, new Vector3(-0.15f, 0.025f, 0.0f), new Vector3(0.03f, 0.03f, 0.86f));
                ProcMesh.Part("Runner R", t, ProcMesh.RoundedBox(0.4f, 2), metal, new Vector3(0.15f, 0.025f, 0.0f), new Vector3(0.03f, 0.03f, 0.86f));
                for (int i = 0; i < 3; i++)
                    ProcMesh.Part("Strut", t, ProcMesh.Cylinder(8), metal, new Vector3(0, 0.055f, -0.3f + i * 0.28f), new Vector3(0.025f, 0.06f, 0.025f));
                ProcMesh.Part("Stripe", t, ProcMesh.RoundedBox(0.2f, 1), Palette.Toon(Palette.Paper, false), new Vector3(0, 0.117f, -0.1f), new Vector3(0.05f, 0.006f, 0.5f));
            }
            // the big fresh dent on the front curl (oversized so it reads in photos), with snowman-white scuffs
            var dentRoot = ProcMesh.Pivot("Dent", t, new Vector3(0.0f, 0.2f, 0.44f));
            ProcMesh.Part("Dent Hollow", dentRoot, ProcMesh.Sphere(12, 8), Palette.Toon(Palette.TonyRedDark * 0.8f + Palette.Ink * 0.2f, false, 0.05f), Vector3.zero, new Vector3(0.18f, 0.12f, 0.05f), new Vector3(-20, 0, 8));
            for (int i = 0; i < 4; i++)
                ProcMesh.Part("Crease", dentRoot, ProcMesh.RoundedBox(0.3f, 1), Palette.Toon(Palette.Ink, false), new Vector3(-0.06f + i * 0.04f, 0.0f, 0.022f), new Vector3(0.008f, 0.1f - Mathf.Abs(i - 1.5f) * 0.02f, 0.006f), new Vector3(-20, 0, -30 + i * 20));
            for (int i = 0; i < 3; i++)
                ProcMesh.Part("Snow Scuff", dentRoot, ProcMesh.Sphere(8, 6), Palette.Toon(Palette.Snow, false), new Vector3(-0.05f + i * 0.05f, 0.04f - i * 0.02f, 0.028f), new Vector3(0.03f, 0.012f, 0.008f), new Vector3(0, 0, 15 * i));
            DentClue = dentRoot.gameObject.AddComponent<Clue>();
            DentClue.id = ClueId.DentedSled;
            DentClue.radius = 0.12f;
            DentClue.owner = transform;
            DentClue.useFacing = false;
            DentClue.glintTint = Palette.GlintWhite;

            // rope
            m_RopeEnd = ProcMesh.Pivot("Rope Tie", t, new Vector3(0, 0.15f, 0.42f));
            var ropeGo = new GameObject("Rope");
            ropeGo.transform.SetParent(t, false);
            m_Rope = ropeGo.AddComponent<LineRenderer>();
            m_Rope.positionCount = 8;
            m_Rope.widthMultiplier = 0.012f;
            m_Rope.sharedMaterial = Palette.Unlit(Palette.Manila);
            m_Rope.useWorldSpace = true;
            m_Rope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.12f, 0); col.size = new Vector3(0.45f, 0.2f, 0.9f);
            MeshMerger.Merge(transform);
        }

        public void HideBehind(Vector3 threat, float seconds)
        {
            if (m_Planted) return;   // it stays stuck in the snow
            m_HideUntil = Time.time + seconds;
            m_HideFrom = threat;
        }

        void LateUpdate()
        {
            if (!m_Tony || m_Planted) return;
            var tony = m_Tony.transform;
            var hand = m_Tony.Rig.HandL ? m_Tony.Rig.HandL.position : tony.position + Vector3.up * 0.4f;
            bool chasing = m_Tony.State != TonyKid.TonyState.Idle;
            if (Time.time < m_HideUntil)
            {
                // tip the sled up between Tony and the camera
                var dir = (m_HideFrom - tony.position).Flat().normalized;
                var target = tony.position + dir * 0.35f;
                transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 14f);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.up, -dir) * Quaternion.Euler(0, 0, 0), Time.deltaTime * 14f);
            }
            else if (chasing)
            {
                // dragged on a rope ~0.75 m behind
                var toSled = (transform.position - tony.position).Flat();
                float len = 0.75f;
                if (toSled.magnitude > len || m_Tony.IsMoving)
                {
                    var p = tony.position + toSled.normalized * len;
                    var room = RoomService.Current;
                    if (room != null) p.y = room.FloorY;
                    transform.position = Vector3.Lerp(transform.position, p, Time.deltaTime * 10f);
                }
                var face = (tony.position - transform.position).Flat();
                if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face.normalized), Time.deltaTime * 8f);
            }
            // runner tracks
            var snow = SnowSurface.Instance;
            if (snow && (transform.position - m_Last).Flat().magnitude > 0.2f)
            {
                var d = (transform.position - m_Last).Flat();
                snow.Stamp(transform.position, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, StampShape.SledTrack, 0.5f, 0.6f);
                m_Last = transform.position;
            }
            // rope sag
            if (m_Rope)
            {
                bool ropeOn = chasing || FrostUtil.FlatDistance(hand, m_RopeEnd.position) < 1.2f;
                m_Rope.enabled = ropeOn;
                if (ropeOn)
                {
                    var a = m_RopeEnd.position; var b = hand;
                    for (int i = 0; i < m_Rope.positionCount; i++)
                    {
                        float u = i / (float)(m_Rope.positionCount - 1);
                        var p = Vector3.Lerp(a, b, u);
                        p.y -= Mathf.Sin(u * Mathf.PI) * 0.12f;
                        m_Rope.SetPosition(i, p);
                    }
                }
            }
        }
    }

    /// <summary>Birdies (and stars) circling a dazed head, tweeting.</summary>
    public class DazeBirdies : MonoBehaviour
    {
        readonly List<Transform> m_Things = new List<Transform>();
        AudioSource m_Tweets;
        float m_Born;
        bool m_Stopping;

        public static DazeBirdies Create(Transform head, Vector3 headPoint)
        {
            var go = new GameObject("Birdies");
            go.transform.SetParent(head, false);
            go.transform.position = headPoint + Vector3.up * 0.28f;
            var b = go.AddComponent<DazeBirdies>();
            b.Build();
            return b;
        }

        void Build()
        {
            m_Born = Time.time;
            for (int i = 0; i < 3; i++)
            {
                var bird = new GameObject("Birdie").transform;
                bird.SetParent(transform, false);
                ProcMesh.Part("Body", bird, ProcMesh.Sphere(10, 8), Palette.Toon(Palette.BirdieYellow, true, 0.3f), Vector3.zero, new Vector3(0.06f, 0.055f, 0.07f));
                ProcMesh.Part("Head", bird, ProcMesh.Sphere(10, 8), Palette.Toon(Palette.BirdieYellow, true, 0.3f), new Vector3(0, 0.03f, 0.03f), Vector3.one * 0.045f);
                ProcMesh.Part("Beak", bird, ProcMesh.Cone(6), Palette.Toon(Palette.Carrot, false), new Vector3(0, 0.028f, 0.058f), new Vector3(0.015f, 0.025f, 0.015f), new Vector3(90, 0, 0));
                ProcMesh.Part("Wing", bird, ProcMesh.Sphere(8, 6), Palette.Toon(Palette.Goggles, true), new Vector3(0.03f, 0.005f, -0.005f), new Vector3(0.015f, 0.035f, 0.05f), new Vector3(0, 0, -30));
                ProcMesh.Part("Wing", bird, ProcMesh.Sphere(8, 6), Palette.Toon(Palette.Goggles, true), new Vector3(-0.03f, 0.005f, -0.005f), new Vector3(0.015f, 0.035f, 0.05f), new Vector3(0, 0, 30));
                ProcMesh.Part("Eye", bird, ProcMesh.Sphere(6, 4), Palette.Toon(Palette.Ink, false), new Vector3(0.016f, 0.04f, 0.045f), Vector3.one * 0.01f);
                ProcMesh.Part("Eye", bird, ProcMesh.Sphere(6, 4), Palette.Toon(Palette.Ink, false), new Vector3(-0.016f, 0.04f, 0.045f), Vector3.one * 0.01f);
                m_Things.Add(bird);
            }
            for (int i = 0; i < 3; i++)
            {
                m_Things.Add(StunStar.Create(transform, 0.075f, i).transform);   // the sprite-sheet stars
            }
            m_Tweets = AudioDirector.Loop(Sfx.Tweet, transform, 0.5f);
            MeshMerger.Merge(transform, new[] { "Star" }, 0f);   // stars orbit individually
            foreach (var r in GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            transform.localScale = Vector3.zero;
        }

        public void Stop() { if (!m_Stopping) StartCoroutine(Out()); }

        IEnumerator Out()
        {
            m_Stopping = true;
            yield return Ease.Tween(0.25f, t => transform.localScale = Vector3.one * (1f - t), Ease.InCubic, false);
            Destroy(gameObject);
        }

        void Update()
        {
            if (!m_Stopping) transform.localScale = Vector3.one * Ease.OutBack(Mathf.Clamp01((Time.time - m_Born) / 0.3f)) / Mathf.Max(0.01f, transform.parent ? transform.parent.lossyScale.x : 1f);
            float t = Time.time * 2.6f;
            for (int i = 0; i < m_Things.Count; i++)
            {
                float a = t + i * Mathf.PI * 2f / m_Things.Count;
                var p = new Vector3(Mathf.Cos(a) * 0.2f, Mathf.Sin(a * 2f) * 0.025f, Mathf.Sin(a) * 0.2f);
                m_Things[i].localPosition = p;
                m_Things[i].localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));
                if (i < 3) m_Things[i].localPosition += Vector3.up * Mathf.Abs(Mathf.Sin(t * 6f + i)) * 0.02f;
            }
        }
    }

    /// <summary>Where Frosty is (Tony's path finding avoids him).</summary>
    public static class FrostyMarker
    {
        public static Vector3? Position;
    }
}
