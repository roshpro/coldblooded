using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// "The Knock" (~35 s, paced to give you time to react). A door fades into your room and someone knocks: Lucy,
    /// muffled, wants you out building a snowman. The door opens and she comes in. Cheerful music; snow falls and slowly
    /// piles up over the floor while the trees and rocks rise out of it. The music fades. Silence. A LOUD thud a short
    /// way behind you: Frosty's body. Once you've turned round, Lucy jabs at him: "It's a cold-blooded MURDER!!"
    /// Sirens blast, red and blue light, the room drains to noir grey, the logo stamps in above the body, the kids pop
    /// in and the case is open. Hold the menu button to skip straight to the investigation.
    /// </summary>
    public class OpeningSequence : MonoBehaviour
    {
        public bool Done { get; private set; }

        GameManager m_Gm;
        PlayerRig m_Player;
        LayoutDirector.Result m_Layout;
        GameObject m_Scenery;
        Snowfall m_Snowfall;
        System.Action m_OnInvestigate;
        IntroDoor m_Door;
        TitleLogo m_Logo;
        AudioSource m_Jingle;
        LucyKid m_Lucy;
        Vector3 m_DoorPos, m_LucyStop;
        Vector3? m_DoorFacing;   // set when the door stands on your real one: it faces into the room
        bool m_Investigating, m_Skipped;
        readonly Dictionary<Kid, Vector3> m_KidScale = new Dictionary<Kid, Vector3>();
        float m_SceneryY;

        public static OpeningSequence Begin(GameManager gm, PlayerRig player, LayoutDirector.Result layout, GameObject scenery, Snowfall snowfall,
            Vector3 doorPos, System.Action onInvestigate, Vector3? doorFacing = null)
        {
            var s = gm.gameObject.AddComponent<OpeningSequence>();
            s.m_Gm = gm; s.m_Player = player; s.m_Layout = layout; s.m_Scenery = scenery; s.m_Snowfall = snowfall;
            s.m_DoorPos = doorPos; s.m_OnInvestigate = onInvestigate; s.m_DoorFacing = doorFacing;
            s.Stage();
            s.StartCoroutine(s.Run());
            return s;
        }

        // ------------------------------------------------------------------ staging (before 0:00)
        void Stage()
        {
            var feet = m_Player.FeetPosition;
            var room = RoomService.Current;
            // everything that will be in the room is laid out now, out of sight
            if (Frosty.Instance) Frosty.Instance.Hide();
            foreach (var kv in m_Layout.Kids)
            {
                var k = kv.Value;
                m_KidScale[k] = k.transform.localScale;
                k.SetHidden(true);
                k.Busy = true;
            }
            foreach (var p in m_Layout.Props) SetVisible(p, false);
            if (m_Scenery)
            {
                m_SceneryY = m_Scenery.transform.position.y;
                m_Scenery.SetActive(false);
            }

            // the door, in its own freestanding frame, facing you (or laid over your real door, facing into the room)
            var toYou = m_DoorFacing ?? (feet - m_DoorPos).Flat();
            toYou = toYou.sqrMagnitude > 1e-4f ? toYou.Flat().normalized : -m_Player.FlatForward;
            m_Door = IntroDoor.Create(m_DoorPos, toYou);

            // Lucy waits behind it (muffled), and stops a stride inside when she bursts in
            m_Lucy = Kid.Find(CastId.Lucy) as LucyKid;
            if (m_Lucy)
            {
                var behind = m_DoorPos - toYou * 0.45f;
                if (room != null) behind = room.ClampInside(behind, 0.2f);
                m_Lucy.StopMoving();
                m_Lucy.transform.SetPositionAndRotation(new Vector3(behind.x, room != null ? room.FloorY : behind.y, behind.z), Quaternion.LookRotation(toYou));
                m_Lucy.VoiceMuffled = true;
                float d = FrostUtil.FlatDistance(m_DoorPos, feet);
                m_LucyStop = feet + (m_DoorPos - feet).Flat().normalized * Mathf.Max(1.1f, d - 0.85f);
                m_LucyStop.y = room != null ? room.FloorY : feet.y;
                if (room != null) m_LucyStop = room.ClampInside(m_LucyStop, 0.3f);
            }
        }

        static void SetVisible(GameObject go, bool on)
        {
            if (!go) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = on;
        }

        // ------------------------------------------------------------------ the beats
        // Paced slowly (~35 s) so there's time to take each moment in.
        IEnumerator Run()
        {
            var snow = SnowSurface.Instance;
            var frosty = Frosty.Instance;

            // 1. the door fades into the quiet room; a pause; three sharp knocks; Lucy, muffled and impatient
            StartCoroutine(m_Door.Appear());
            yield return new WaitForSeconds(1.8f);
            for (int i = 0; i < 3; i++)
            {
                AudioDirector.PlayAt(Sfx.Knock, m_Door.KnockPoint, 1f, Random.Range(0.96f, 1.04f));
                m_Door.Rattle();
                yield return new WaitForSeconds(0.42f);
            }
            yield return new WaitForSeconds(0.5f);
            if (m_Lucy) yield return DialogueRunner.SayAndWait(Lines.Get("lucy_knock"), m_Lucy, true);
            yield return new WaitForSeconds(1.0f);

            // 2. the door swings open and Lucy comes in, a few steps into the room, and cheers at you
            AudioDirector.PlayAt(Sfx.DoorBang, m_Door.KnockPoint, 0.75f, 1.05f);
            StartCoroutine(m_Door.BangOpen());
            if (m_Lucy)
            {
                m_Lucy.SetHidden(false);
                m_Lucy.VoiceMuffled = false;
                bool arrived = false;
                m_Lucy.MoveTo(m_LucyStop, 1.0f, () => arrived = true);
                for (float t = 0f; !arrived && t < 4f; t += Time.deltaTime) yield return null;
                m_Lucy.Face(m_Player.Head.position, 0.3f);
                yield return new WaitForSeconds(0.3f);
                m_Lucy.Anim.StrikePose(Pose.Cheer, 1.6f);
            }
            yield return new WaitForSeconds(1.0f);

            // 3. cheerful music; snow starts falling and slowly piles up; the trees and rocks rise out of it, then the
            // sled and the bucket turn up in the drifts
            m_Jingle = AudioDirector.Play2D(Sfx.Jingle, 0.75f);
            if (m_Jingle) m_Jingle.loop = true;
            AudioDirector.Quiet = false;
            if (m_Snowfall) m_Snowfall.SetFalling(true);
            NorthernLights.Begin(false);   // the lights fade in over the room with the snow, and stay up all game
            var tracker = GetComponent<FootprintTracker>();
            if (tracker) tracker.enabled = true;
            const float pileUp = 12f;
            StartCoroutine(Settle(snow, pileUp));
            yield return new WaitForSeconds(2.5f);
            if (m_Scenery) StartCoroutine(RiseScenery(8.5f));
            yield return new WaitForSeconds(6.5f);
            foreach (var p in m_Layout.Props)
            {
                SetVisible(p, true);
                if (p) Fx.SnowPuff(p.transform.position + Vector3.up * 0.1f, Vector3.up, 14, 0.7f);
            }
            yield return new WaitForSeconds(pileUp - 9f + 1.5f);

            // 4. the music fades away; a beat of silence
            yield return FadeOut(m_Jingle, 1.6f);
            m_Jingle = null;
            yield return new WaitForSeconds(1.6f);

            // 5. a LOUD thud a short way behind you, wherever there's room for him: Frosty, toppled in the snow
            if (frosty)
            {
                PlaceFrostyBehind(frosty);
                // right down on the floor where he lands, and loud: full volume out past where you're standing
                var at = frosty.transform.position + Vector3.up * 0.03f;
                foreach (var src in new[] { AudioDirector.PlayAt(Sfx.Thud, at, 1f, 1f), AudioDirector.PlayAt(Sfx.Thud, at, 1f, 0.72f), AudioDirector.PlayAt(Sfx.Thud, at, 1f, 0.55f) })
                    if (src) { src.minDistance = 3.5f; src.maxDistance = 20f; }
                AudioDirector.Play2D(Sfx.Thud, 0.9f, 0.6f);   // felt in the chest as much as heard
                m_Player.Left.Haptic(1f, 0.3f);
                m_Player.Right.Haptic(1f, 0.3f);
                frosty.Drop();
                Fx.SnowPuff(frosty.transform.position + Vector3.up * 0.2f, Vector3.up, 40, 1.4f);

                // nobody says a word until you've turned round and seen him (or a few seconds have passed): a beat of
                // stunned silence first, then you take it in
                yield return new WaitForSeconds(1.5f);
                float look = 0f;
                for (float t = 1.5f; t < 5f; t += Time.deltaTime)
                {
                    var head = m_Player.Head;
                    if (Vector3.Angle(head.forward, frosty.AimPoint - head.position) < 32f) { look += Time.deltaTime; if (look > 0.7f) break; }
                    else look = 0f;
                    yield return null;
                }
                yield return new WaitForSeconds(0.9f);
            }
            else yield return new WaitForSeconds(2f);

            // Lucy jabs at the body
            if (m_Lucy)
            {
                if (frosty)
                {
                    m_Lucy.Face(frosty.AimPoint, 0.2f);
                    m_Lucy.JabAt(frosty.AimPoint);
                }
                DialogueRunner.Say(Lines.Get("lucy_murder"), m_Lucy, true);
                yield return new WaitForSeconds(1.6f);
            }

            // 6. sirens blast: red and blue over the room, then it drains to noir grey; the logo stamps in over him
            AudioDirector.Play2D(Sfx.Siren, 1f);
            StartCoroutine(SirensToNoir(3.4f));
            yield return new WaitForSeconds(0.5f);
            var headPos = m_Player.Head.position;
            var above = frosty ? frosty.transform.position + Vector3.up * 1.55f : headPos + m_Player.FlatForward * 2f;
            m_Logo = TitleLogo.Create(above, headPos);
            yield return m_Logo.StampIn();
            StartCoroutine(m_Door.Vanish());
            m_Door = null;
            AudioDirector.MusicGate = 1f;   // the noir score fades in
            yield return new WaitForSeconds(2.4f);

            // the game starts: the logo floats away, the kids pop in and Lucy gives you your first job
            if (m_Logo) StartCoroutine(m_Logo.FloatAway());
            StartInvestigation(true);
            yield return new WaitForSeconds(2f);
            Done = true;
        }

        static IEnumerator FadeOut(AudioSource src, float seconds)
        {
            if (!src) yield break;
            float v = src.volume;
            for (float t = 0f; t < seconds && src; t += Time.deltaTime)
            {
                src.volume = v * (1f - t / seconds);
                yield return null;
            }
            if (src) { src.Stop(); src.loop = false; }
        }

        /// <summary>Frosty lands a short way behind wherever you're facing right now, on the nearest bit of free floor
        /// big enough for him (if there's none, he stays where the layout put him).</summary>
        void PlaceFrostyBehind(Frosty frosty)
        {
            var room = RoomService.Current;
            if (room == null) return;
            var feet = m_Player.FeetPosition;
            var back = -m_Player.FlatForward;
            float fr = Frosty.FootprintRadius * 0.7f;
            float[] dists = { 1.3f, 1.6f, 1.1f, 1.9f, 2.3f };
            float[] angles = { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 80f, -80f };
            foreach (var d in dists)
                foreach (var a in angles)
                {
                    var c = feet + Quaternion.Euler(0f, a, 0f) * back * d;
                    c.y = room.FloorY;
                    if (!room.IsFree(c, fr, 0.6f)) continue;
                    if (m_Lucy && FrostUtil.FlatDistance(c, m_Lucy.transform.position) < fr + 0.5f) continue;
                    bool clear = true;
                    foreach (var p in m_Layout.Props)
                        if (p && FrostUtil.FlatDistance(c, p.transform.position) < fr + 0.35f) { clear = false; break; }
                    if (!clear) continue;

                    var delta = (c - frosty.transform.position).Flat();
                    frosty.transform.SetPositionAndRotation(c, FrostUtil.FlatLookRotation(c, feet) * Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f));
                    // the (still hidden) kids were laid out around him: they come along
                    foreach (var kv in m_Layout.Kids)
                    {
                        var k = kv.Value;
                        if (!k || k == m_Lucy) continue;
                        var q = room.ClampInside(k.transform.position + delta, 0.3f);
                        if (!room.IsFree(q, 0.2f, 0.25f) || FrostUtil.FlatDistance(q, feet) < 0.8f || FrostUtil.FlatDistance(q, c) < fr + 0.3f) continue;
                        k.transform.position = new Vector3(q.x, k.transform.position.y, q.z);
                        k.HomePosition = k.transform.position;
                    }
                    return;
                }
        }

        IEnumerator Settle(SnowSurface snow, float seconds)
        {
            if (!snow) yield break;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                snow.Coverage = Mathf.SmoothStep(0f, 1f, t / seconds);
                yield return null;
            }
            snow.Coverage = 1f;
        }

        IEnumerator RiseScenery(float seconds)
        {
            var tr = m_Scenery.transform;
            const float depth = 3.6f;
            var p = tr.position;
            tr.position = new Vector3(p.x, m_SceneryY - depth, p.z);
            m_Scenery.SetActive(true);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                tr.position = new Vector3(p.x, m_SceneryY - depth * (1f - Ease.OutCubic(t / seconds)), p.z);
                yield return null;
            }
            tr.position = new Vector3(p.x, m_SceneryY, p.z);
        }

        IEnumerator SirensToNoir(float seconds)
        {
            var red = new Color(1f, 0.15f, 0.12f);
            var blue = new Color(0.15f, 0.35f, 1f);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float u = t / seconds;
                // pulses build, peak, then the colour drains out leaving grey
                float strength = Mathf.Sin(Mathf.Clamp01(u * 1.15f) * Mathf.PI) * 0.55f;
                bool isRed = (int)(t / 0.27f) % 2 == 0;
                float beat = 0.55f + 0.45f * Mathf.Sin((t % 0.27f) / 0.27f * Mathf.PI);
                RoomTint.Siren(isRed ? red : blue, strength * beat);
                yield return null;
            }
            RoomTint.Noir(1f);
        }

        /// <summary>The kids appear, the clock starts and Lucy gives you your first job.</summary>
        void StartInvestigation(bool animate)
        {
            if (m_Investigating) return;
            m_Investigating = true;
            foreach (var kv in m_Layout.Kids)
            {
                var k = kv.Value;
                if (!k) continue;
                k.Busy = false;
                if (k == m_Lucy) continue;
                k.SetHidden(false);
                var s = m_KidScale.TryGetValue(k, out var sc) ? sc : Vector3.one;
                if (animate) StartCoroutine(PopIn(k.transform, s, Random.Range(0f, 0.45f)));
                else k.transform.localScale = s;
            }
            if (m_Lucy) { m_Lucy.Busy = false; m_Lucy.VoiceMuffled = false; m_Lucy.HomePosition = m_Lucy.transform.position; }
            m_OnInvestigate?.Invoke();
            CaseDirector.I?.BeginInvestigation();
            var gd = m_Gm ? m_Gm.GetComponent<GestureDirector>() : null;
            if (gd) gd.PromptNow();
        }

        static IEnumerator PopIn(Transform t, Vector3 scale, float delay)
        {
            t.localScale = Vector3.zero;
            yield return new WaitForSeconds(delay);
            Fx.SnowPuff(t.position + Vector3.up * 0.1f, Vector3.up, 18, 0.9f);
            yield return Ease.Tween(0.4f, k => { if (t) t.localScale = scale * Ease.OutBack(k); }, Ease.Linear, false);
        }

        // ------------------------------------------------------------------ skip
        /// <summary>Jump straight to the moment the investigation begins.</summary>
        public void Skip()
        {
            if (Done || m_Skipped) return;
            m_Skipped = true;
            StopAllCoroutines();
            if (m_Jingle) { m_Jingle.loop = false; m_Jingle.Stop(); }
            if (SnowSurface.Instance) SnowSurface.Instance.Coverage = 1f;
            if (m_Snowfall && !m_Snowfall.gameObject.activeSelf) m_Snowfall.SetFalling(true);
            NorthernLights.Begin(false);
            var tracker = GetComponent<FootprintTracker>();
            if (tracker) tracker.enabled = true;
            if (m_Scenery)
            {
                m_Scenery.SetActive(true);
                var p = m_Scenery.transform.position;
                m_Scenery.transform.position = new Vector3(p.x, m_SceneryY, p.z);
            }
            foreach (var pr in m_Layout.Props) SetVisible(pr, true);
            if (Frosty.Instance) Frosty.Instance.Drop();
            if (m_Door) { Destroy(m_Door.gameObject); m_Door = null; }
            if (m_Logo) Destroy(m_Logo.gameObject);
            if (m_Lucy && !m_Investigating)
            {
                AudioDirector.StopVoice(CastId.Lucy);
                m_Lucy.StopMoving();
                m_Lucy.SetHidden(false);
                m_Lucy.transform.position = m_LucyStop;
                var player = PlayerRig.Active;
                if (player) m_Lucy.transform.rotation = FrostUtil.FlatLookRotation(m_LucyStop, player.Head.position);
            }
            RoomTint.Noir(1f);
            AudioDirector.Quiet = false;
            AudioDirector.MusicGate = 1f;
            foreach (var kv in m_Layout.Kids) if (kv.Value && m_KidScale.TryGetValue(kv.Value, out var s)) kv.Value.transform.localScale = s;
            StartInvestigation(false);
            Done = true;
        }
    }

    /// <summary>
    /// The real room's colour (passthrough on the Quest; the stand-in room's walls on desktop): normal, washed with
    /// siren red or blue, or drained to noir grey.
    /// </summary>
    public static class RoomTint
    {
        static readonly Color[] s_Map = new Color[256];
#if FROST_META
        static OVRPassthroughLayer s_Layer;
        static OVRPassthroughLayer Layer
        {
            get
            {
                if (!s_Layer) s_Layer = Object.FindAnyObjectByType<OVRPassthroughLayer>();
                return s_Layer;
            }
        }
#endif
        static List<(Material mat, Color col)> s_Sim;

        /// <summary>Back to the room's own colours.</summary>
        public static void Normal()
        {
            // on the headset the room's colour goes through PassthroughMood, which owns the passthrough grade, so the
            // camera flash and a snowball fight's ice blend away from noir and back to it
            PassthroughMood.Set(RoomGrade.None, 0f);
            Sim(c => c);
        }

        /// <summary>0 = full colour .. 1 = noir grey (a touch more contrast, a touch darker).</summary>
        public static void Noir(float k)
        {
            k = Mathf.Clamp01(k);
            if (k >= 0.999f) PassthroughMood.Set(RoomGrade.Noir, 0f);
            else if (k <= 0.001f) PassthroughMood.Set(RoomGrade.None, 0f);
            else PassthroughMood.Hold(RoomGrade.None, RoomGrade.Noir, k);   // part-way (the colour coming back at CASE CLOSED)
            Sim(c => { float l = Lum(c); return Color.Lerp(c, new Color(l, l, l, c.a) * (1f - 0.06f * k), k); });
        }

        /// <summary>Siren light over the room: grey washed with the colour (amount 0..1).</summary>
        public static void Siren(Color tint, float amount)
        {
            amount = Mathf.Clamp01(amount);
            for (int i = 0; i < 256; i++)
            {
                float l = i / 255f;
                var grey = new Color(l, l, l, 1f);
                var lit = new Color(Mathf.Clamp01(l * tint.r * 1.5f + tint.r * 0.1f), Mathf.Clamp01(l * tint.g * 1.5f + tint.g * 0.1f), Mathf.Clamp01(l * tint.b * 1.5f + tint.b * 0.1f), 1f);
                s_Map[i] = Color.Lerp(grey, lit, amount);
            }
#if FROST_META
            if (Layer) Layer.SetColorMap(s_Map);
#endif
            Sim(c => { float l = Lum(c); var g = new Color(l, l, l, c.a); return Color.Lerp(g, new Color(l * tint.r * 1.5f, l * tint.g * 1.5f, l * tint.b * 1.5f, c.a), amount); });
        }

        static float Lum(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

        /// <summary>Desktop: recolour the stand-in room's walls, floor and furniture from their original colours.</summary>
        static void Sim(System.Func<Color, Color> f)
        {
            if (s_Sim == null)
            {
                var root = GameObject.Find("Simulated Room (desktop only)");
                if (!root) return;
                s_Sim = new List<(Material, Color)>();
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.materials)
                        if (m && m.HasProperty("_BaseColor")) s_Sim.Add((m, m.GetColor("_BaseColor")));
            }
            foreach (var (mat, col) in s_Sim) if (mat) mat.SetColor("_BaseColor", f(col));
        }

        /// <summary>Forget the cached room (a new scene).</summary>
        public static void Reset() { s_Sim = null; }
    }

    /// <summary>A cartoon front door in its own freestanding frame: knocked on, banged open, then gone.</summary>
    public class IntroDoor : MonoBehaviour
    {
        const float W = 0.92f, H = 2.0f, T = 0.05f;
        Transform m_Root, m_Leaf;
        public Vector3 KnockPoint => m_Leaf ? m_Leaf.TransformPoint(new Vector3(W * 0.5f, H * 0.62f, 0f)) : transform.position + Vector3.up * 1.2f;

        public static IntroDoor Create(Vector3 floorPos, Vector3 facing)
        {
            var go = new GameObject("Intro Door");
            go.transform.SetPositionAndRotation(floorPos, Quaternion.LookRotation(facing, Vector3.up));
            var d = go.AddComponent<IntroDoor>();
            d.Build();
            return d;
        }

        void Build()
        {
            m_Root = new GameObject("Door").transform;
            m_Root.SetParent(transform, false);
            m_Root.localScale = Vector3.zero;
            var frame = Palette.Toon(new Color(0.96f, 0.93f, 0.85f), true, 0.2f);
            var red = Palette.Toon(new Color(0.8f, 0.17f, 0.2f), true, 0.25f);
            var panel = Palette.Toon(new Color(0.68f, 0.12f, 0.16f), false, 0.2f);
            var brass = Palette.Toon(new Color(0.95f, 0.76f, 0.3f), true, 0.5f);
            var green = Palette.Toon(new Color(0.15f, 0.42f, 0.25f), true, 0.2f);
            var bow = Palette.Toon(new Color(0.9f, 0.15f, 0.2f), false, 0.3f);
            var stone = Palette.Toon(new Color(0.7f, 0.72f, 0.76f), true, 0.15f);

            // the frame and a doorstep (the front, +z, faces you)
            ProcMesh.Part("Post L", m_Root, ProcMesh.RoundedBox(0.15f, 2), frame, new Vector3(-W * 0.5f - 0.05f, (H + 0.1f) * 0.5f, 0f), new Vector3(0.1f, H + 0.1f, 0.15f));
            ProcMesh.Part("Post R", m_Root, ProcMesh.RoundedBox(0.15f, 2), frame, new Vector3(W * 0.5f + 0.05f, (H + 0.1f) * 0.5f, 0f), new Vector3(0.1f, H + 0.1f, 0.15f));
            ProcMesh.Part("Lintel", m_Root, ProcMesh.RoundedBox(0.15f, 2), frame, new Vector3(0f, H + 0.06f, 0f), new Vector3(W + 0.3f, 0.12f, 0.17f));
            ProcMesh.Part("Step", m_Root, ProcMesh.RoundedBox(0.12f, 2), stone, new Vector3(0f, 0.025f, 0.1f), new Vector3(W + 0.32f, 0.05f, 0.34f));

            // the door itself, hinged on the left
            m_Leaf = ProcMesh.Pivot("Leaf", m_Root, new Vector3(-W * 0.5f, 0.05f, 0f));
            ProcMesh.Part("Slab", m_Leaf, ProcMesh.RoundedBox(0.06f, 2), red, new Vector3(W * 0.5f, H * 0.5f - 0.03f, 0f), new Vector3(W - 0.012f, H - 0.06f, T));
            float fz = T * 0.5f + 0.006f;
            for (int side = -1; side <= 1; side += 2)
            {
                ProcMesh.Part("Panel High", m_Leaf, ProcMesh.RoundedBox(0.2f, 2), panel, new Vector3(W * 0.5f + side * 0.2f, H * 0.69f, fz), new Vector3(0.3f, 0.62f, 0.014f));
                ProcMesh.Part("Panel Low", m_Leaf, ProcMesh.RoundedBox(0.2f, 2), panel, new Vector3(W * 0.5f + side * 0.2f, H * 0.25f, fz), new Vector3(0.3f, 0.5f, 0.014f));
            }
            ProcMesh.Part("Knob", m_Leaf, ProcMesh.Sphere(12, 8), brass, new Vector3(W - 0.1f, H * 0.47f, T * 0.5f + 0.04f), Vector3.one * 0.07f);
            ProcMesh.Part("Knob Back", m_Leaf, ProcMesh.Sphere(12, 8), brass, new Vector3(W - 0.1f, H * 0.47f, -T * 0.5f - 0.04f), Vector3.one * 0.07f);
            ProcMesh.Part("Knocker", m_Leaf, ProcMesh.Torus(0.4f, 0.1f, 16, 6), brass, new Vector3(W * 0.5f, H * 0.62f, fz + 0.01f), Vector3.one * 0.1f, new Vector3(90f, 0f, 0f));
            // a winter wreath with a red bow
            ProcMesh.Part("Wreath", m_Leaf, ProcMesh.Torus(0.4f, 0.14f, 20, 8), green, new Vector3(W * 0.5f, H * 0.78f, fz + 0.03f), Vector3.one * 0.36f, new Vector3(90f, 0f, 0f));
            ProcMesh.Part("Bow L", m_Leaf, ProcMesh.Sphere(10, 8), bow, new Vector3(W * 0.5f - 0.035f, H * 0.78f - 0.14f, fz + 0.06f), new Vector3(0.07f, 0.045f, 0.03f));
            ProcMesh.Part("Bow R", m_Leaf, ProcMesh.Sphere(10, 8), bow, new Vector3(W * 0.5f + 0.035f, H * 0.78f - 0.14f, fz + 0.06f), new Vector3(0.07f, 0.045f, 0.03f));
            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        /// <summary>Fades into the room: rises up out of nothing with a shimmer of snow dust.</summary>
        public IEnumerator Appear()
        {
            Fx.SnowPuff(transform.position + Vector3.up * 0.05f, Vector3.up, 24, 1f);
            yield return Ease.Tween(0.6f, k => m_Root.localScale = new Vector3(Mathf.Lerp(0.85f, 1f, k), Ease.OutBack(k), Mathf.Lerp(0.85f, 1f, k)), Ease.OutCubic, false);
            m_Root.localScale = Vector3.one;
        }

        /// <summary>A knock: the door rattles in its frame.</summary>
        public void Rattle() => StartCoroutine(RattleRoutine());

        IEnumerator RattleRoutine()
        {
            var basePos = m_Root.localPosition;
            for (float t = 0f; t < 0.16f; t += Time.deltaTime)
            {
                float a = (1f - t / 0.16f);
                m_Leaf.localRotation = Quaternion.Euler(0f, Random.Range(-1.6f, 1.6f) * a, 0f);
                m_Root.localPosition = basePos + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.004f * a;
                yield return null;
            }
            m_Leaf.localRotation = Quaternion.identity;
            m_Root.localPosition = basePos;
        }

        /// <summary>Flung open toward you, bouncing off its stop.</summary>
        public IEnumerator BangOpen()
        {
            yield return Ease.Tween(0.15f, k => m_Leaf.localRotation = Quaternion.Euler(0f, -108f * k, 0f), Ease.OutCubic, false);
            yield return Ease.Tween(0.3f, k => m_Leaf.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-108f, -96f, k), 0f), Ease.OutBack, false);
        }

        public IEnumerator Vanish()
        {
            Fx.SnowPuff(transform.position + Vector3.up * 1f, Vector3.up, 30, 1.2f);
            var from = m_Root.localScale;
            yield return Ease.Tween(0.45f, k => m_Root.localScale = new Vector3(from.x * (1f - k * 0.2f), from.y * (1f - k), from.z * (1f - k * 0.2f)), Ease.InCubic, false);
            Destroy(gameObject);
        }
    }

    /// <summary>The "cold blooded" logo stamped into the air above the body, then floating up and away.</summary>
    public class TitleLogo : MonoBehaviour
    {
        Material m_Mat;
        Transform m_Quad;
        float m_Width = 1.0f;

        public static TitleLogo Create(Vector3 at, Vector3 viewer)
        {
            var go = new GameObject("Title Drop");
            go.layer = Layers.UI3D;
            var away = (at - viewer).Flat();
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward, Vector3.up));
            var l = go.AddComponent<TitleLogo>();
            l.Build(Vector3.Distance(at, viewer));
            return l;
        }

        void Build(float distance)
        {
            m_Width = Mathf.Clamp(distance * 0.42f, 0.8f, 1.4f);
            var tex = Resources.Load<Texture2D>("Title/title_logo");
            m_Quad = new GameObject("Logo").transform;
            m_Quad.SetParent(transform, false);
            m_Quad.gameObject.layer = Layers.UI3D;
            if (tex)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                m_Mat = Palette.UnlitTextured(tex, Color.white.WithAlpha(0f), true, "Title Drop");
                var q = UIKit.Quad(m_Quad, "Image", ProcMesh.Quad(), m_Mat, 0f);
                q.localScale = new Vector3(m_Width, m_Width * tex.height / tex.width, 1f);
                q.GetComponent<MeshRenderer>().sortingOrder = UIKit.Order.Overlay;
            }
            else
            {
                var t = UIKit.Text(m_Quad, "Text", "COLD BLOODED", UIKit.Face.Display, 2.2f, new Color(0.69f, 0.88f, 0.96f), new Vector2(m_Width, 0.3f), TMPro.TextAlignmentOptions.Center, 0f, false);
                t.outlineWidth = 0.2f; t.outlineColor = new Color32(18, 38, 48, 255);
            }
            m_Quad.localScale = Vector3.zero;
        }

        void SetAlpha(float a) { if (m_Mat) m_Mat.SetColor("_BaseColor", Color.white.WithAlpha(a)); }

        /// <summary>Slams in from big to its size with a heavy thump and a puff of snow.</summary>
        public IEnumerator StampIn()
        {
            yield return Ease.Tween(0.2f, k => { m_Quad.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, k); SetAlpha(k); }, Ease.InCubic, false);
            AudioDirector.Play2D(Sfx.StampSlam, 1f, 0.8f);
            AudioDirector.Play2D(Sfx.Thud, 0.6f, 1.15f);
            Fx.SnowPuff(transform.position - Vector3.up * m_Width * 0.25f, Vector3.up, 34, 1.3f);
            yield return Ease.Tween(0.22f, k => m_Quad.localScale = new Vector3(1f + 0.06f * Mathf.Sin(k * Mathf.PI), 1f - 0.08f * Mathf.Sin(k * Mathf.PI), 1f), Ease.Linear, false);
            m_Quad.localScale = Vector3.one;
        }

        public IEnumerator FloatAway()
        {
            var from = transform.position;
            yield return Ease.Tween(1.8f, k => { transform.position = from + Vector3.up * 0.6f * k; SetAlpha(1f - k); if (!m_Mat) m_Quad.localScale = Vector3.one * (1f - k); }, Ease.InOutCubic, false);
            Destroy(gameObject);
        }
    }
}
