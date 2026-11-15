using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Push the kids too far and it's war. Keep hitting the same kid and they complain (2nd hit), then warn you
    /// ("don't you DARE throw another one", 3rd), then snap (4th): an alarm, a booming "SNOWBALL FIGHT!" (drop the
    /// recording in as Resources/Voice/snowball_fight.mp3), a big red stamp, and every kid scoops up snow and pelts
    /// you for a while before Lucy calls a truce. Hits a long way apart don't add up, it never kicks off during the
    /// finale (that's Tony's), and the case pauses while it's on.
    /// </summary>
    public class SnowballFight : MonoBehaviour
    {
        public static SnowballFight I { get; private set; }
        public static bool Active => I && I.m_Active;

        const float ForgetAfter = 45f;     // hits on a kid further apart than this start counting again
        const float FightSeconds = 18f;
        const float Breather = 20f;        // no new fight straight after one

        readonly Dictionary<CastId, int> m_Count = new Dictionary<CastId, int>();
        readonly Dictionary<CastId, float> m_Last = new Dictionary<CastId, float>();
        bool m_Active;
        float m_LastFightEnd = -999f;
        int m_Taken, m_Landed;

        void OnEnable() { I = this; Snowball.HitPlayer += OnHitPlayer; }
        void OnDisable() { if (I == this) I = null; Snowball.HitPlayer -= OnHitPlayer; }

        /// <summary>Your snowball hit a kid. Returns how many times running you've got them (1 = the usual yelp,
        /// 2 = complaining, 3 = "don't you dare", 4 = war), or 0 when it doesn't count (a fight's on, it's the
        /// finale or the case is closed).</summary>
        public int RegisterHit(Kid kid)
        {
            if (!kid) return 0;
            if (m_Active) { m_Landed++; return 0; }
            var gm = GameManager.I;
            var cd = CaseDirector.I;
            // never mid-reaction (a hit while a kid is explaining themselves doesn't count) and never in the finale
            if (!gm || gm.State != GameState.Investigating || (cd != null && (cd.FinaleStarted || cd.CaseClosed || cd.Reacting))) return 0;
            if (Time.time - m_LastFightEnd < Breather) return 1;
            m_Count.TryGetValue(kid.id, out int n);
            if (m_Last.TryGetValue(kid.id, out float last) && Time.time - last > ForgetAfter) n = 0;
            n++;
            m_Count[kid.id] = n;
            m_Last[kid.id] = Time.time;
            if (n >= 4) StartCoroutine(Fight());
            return n;
        }

        IEnumerator Fight()
        {
            m_Active = true;
            m_Taken = m_Landed = 0;
            // let "That's IT!" land, then the alarm, the stamp and the announcer
            yield return new WaitForSeconds(0.8f);
            AudioDirector.Play2D(Sfx.Alarm, 1f);
            StartCoroutine(FightStamp.Slam());
            yield return new WaitForSeconds(0.6f);
            AudioDirector.PlayAnnouncer("snowball_fight", "SNOWBALL FIGHT!");
            AudioDirector.MusicBoost = 1.35f;
            HToast.Show($"Snowball fight! Throw back. Truce in {FightSeconds:0} s.", UIKit.Icon.Snowball, 4f);
            foreach (var k in Kid.All) if (k) StartCoroutine(Pelt(k));

            float end = Time.time + FightSeconds;
            while (Time.time < end && StillOn()) yield return null;
            m_Active = false;
            AudioDirector.MusicBoost = 1f;
            m_Count.Clear();
            m_Last.Clear();
            m_LastFightEnd = Time.time;
            // everyone stops where they are (a throw already in the air still lands)
            foreach (var k in Kid.All) if (k && k.IsMoving && !(k is TonyKid t && t.State != TonyKid.TonyState.Idle)) k.StopMoving();

            // the last throws land, then Lucy calls it off (unless the case moved on meanwhile)
            float wait = 0f;
            while (wait < 4f && AnyThrowing()) { wait += Time.deltaTime; yield return null; }
            if (!StillOn()) yield break;
            var lucy = Kid.Find(CastId.Lucy);
            var line = Lines.Get("lucy_truce");
            if (lucy && line != null) DialogueRunner.Say(line, lucy, true);
            HToast.Show($"Truce! You took {m_Taken} hit{(m_Taken == 1 ? "" : "s")} and landed {m_Landed}.", UIKit.Icon.Snowball, 4.5f);
        }

        /// <summary>Still a normal moment of the case (not paused out, not the finale, not closed)?</summary>
        static bool StillOn()
        {
            var gm = GameManager.I;
            var cd = CaseDirector.I;
            return gm && gm.State == GameState.Investigating && (cd == null || (!cd.FinaleStarted && !cd.CaseClosed));
        }

        float m_LastNotNow = -99f;

        /// <summary>You held a photo out mid-fight: nobody's looking at photos now (said once in a while, not every reach).</summary>
        public void NotNow(Kid kid)
        {
            if (Time.time - m_LastNotNow < 6f) return;
            m_LastNotNow = Time.time;
            HToast.Show("Not mid-fight! Show photos after the truce.", UIKit.Icon.Snowball, 3f);
            var shout = kid ? Lines.Get(Cast.Key(kid.id) + "_fight") : null;
            if (shout != null && !DialogueRunner.IsSpeaking(kid.id)) DialogueRunner.Say(shout, kid, false);
        }

        static bool AnyThrowing()
        {
            foreach (var k in Kid.All) if (k && k.Anim && k.Anim.IsThrowing) return true;
            return false;
        }

        /// <summary>One kid's war: get within range, scoop, throw at you (not all perfectly), sidestep, repeat.</summary>
        IEnumerator Pelt(Kid k)
        {
            yield return new WaitForSeconds(Random.Range(0.3f, 1.8f));   // not all at once
            while (m_Active && k && StillOn())
            {
                var rig = PlayerRig.Active;
                var room = RoomService.Current;
                if (!rig || room == null) yield break;
                if (k.Busy || k.IsStunned || (k.Anim && k.Anim.IsThrowing) || k.IsMoving) { yield return null; continue; }
                var you = rig.Head.position;
                // now and then someone gets a kid instead (one who isn't already seeing stars)
                if (Random.value < KidTargetChance && TryKidTarget(k, out var victim))
                {
                    float kd = FrostUtil.FlatDistance(k.transform.position, victim.transform.position);
                    var kidAim = victim.AimPoint + Random.insideUnitSphere * Mathf.Lerp(0.15f, 0.4f, Mathf.Clamp01(kd / 5f));
                    if (k.ThrowSnowballAt(kidAim))
                    {
                        if (Random.value < 0.35f && !DialogueRunner.IsSpeaking(k.id))
                        {
                            var shout = Lines.Get(Cast.Key(k.id) + "_fight");
                            if (shout != null) DialogueRunner.Say(shout, k, false);
                        }
                        yield return null;
                        while (m_Active && k && k.Anim && k.Anim.IsThrowing) yield return null;
                        yield return new WaitForSeconds(Random.Range(0.8f, 2f));
                        continue;
                    }
                }
                float d = FrostUtil.FlatDistance(k.transform.position, you);
                // close in (or back off) to a good throwing distance, if there's room to; otherwise throw from here
                if ((d > 5.5f || d < 1.1f) && TrySpot(room, you, k, Random.Range(2.6f, 4f), out var spot, d < 1.1f ? 120f : 35f))
                {
                    k.MoveTo(spot, 1.8f);
                    float t = 0f;
                    while (m_Active && k && k.IsMoving && t < 3.5f) { t += Time.deltaTime; yield return null; }
                    continue;
                }
                // aim at your face or chest, with enough scatter that about half of them miss
                var aim = you + Vector3.down * Random.Range(0.05f, 0.4f) + Random.insideUnitSphere * Mathf.Lerp(0.38f, 0.8f, Mathf.Clamp01(d / 5.5f));
                if (k.ThrowSnowballAt(aim))
                {
                    if (Random.value < 0.3f && !DialogueRunner.IsSpeaking(k.id))
                    {
                        var shout = Lines.Get(Cast.Key(k.id) + "_fight");
                        if (shout != null) DialogueRunner.Say(shout, k, false);
                    }
                    yield return null;
                    while (m_Active && k && k.Anim && k.Anim.IsThrowing) yield return null;
                }
                yield return new WaitForSeconds(Random.Range(0.8f, 2.2f));
                // now and then a quick sidestep, so they're not sitting ducks
                if (m_Active && k && Random.value < 0.4f && TrySpot(room, you, k, Mathf.Clamp(d, 2.6f, 4.5f), out var side, 50f))
                    k.MoveTo(side, 1.6f);
            }
        }

        const float KidTargetChance = 0.25f;   // about one throw in four goes at another kid

        /// <summary>Another kid within throwing range of <paramref name="k"/> who isn't stunned (nearer ones more likely).</summary>
        static bool TryKidTarget(Kid k, out Kid victim)
        {
            victim = null;
            float best = float.MaxValue;
            foreach (var o in Kid.All)
            {
                if (!o || o == k || o.IsStunned || (o is TonyKid t && t.State != TonyKid.TonyState.Idle)) continue;
                float d = FrostUtil.FlatDistance(k.transform.position, o.transform.position);
                if (d < 1.2f || d > 6f) continue;
                float score = d * Random.Range(0.6f, 1.4f);
                if (score < best) { best = score; victim = o; }
            }
            return victim;
        }

        /// <summary>A free spot about `range` from you, near the line to the kid (turned up to ±spread degrees),
        /// clear of the other kids and of where they're heading, so they don't bunch up.</summary>
        static bool TrySpot(RoomInfo room, Vector3 you, Kid k, float range, out Vector3 spot, float spread = 35f)
        {
            var dir = (k.transform.position - you).Flat();
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            for (int i = 0; i < 14; i++)
            {
                var p = you + Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * dir * range;
                p.y = room.FloorY;
                if (!room.IsFree(p, 0.25f, 0.3f)) continue;
                bool crowded = false;
                foreach (var o in Kid.All)
                    if (o && o != k && (FrostUtil.FlatDistance(p, o.transform.position) < 0.8f || FrostUtil.FlatDistance(p, o.Destination) < 0.8f)) crowded = true;
                if (crowded) continue;
                spot = p;
                return true;
            }
            spot = default;
            return false;
        }

        void OnHitPlayer(Snowball ball)
        {
            if (!ball || !ball.Thrower) return;
            if (m_Active) m_Taken++;
            FaceSplat.Show();
            AudioDirector.Play2D(Sfx.Splat, 0.9f, Random.Range(0.9f, 1.05f));
        }
    }

    /// <summary>Snow splattered across your view for a moment when a kid's snowball gets you.</summary>
    public static class FaceSplat
    {
        static float s_Last = -1f;

        public static void Show()
        {
            var rig = PlayerRig.Active;
            var cam = rig ? rig.ViewCamera : null;
            if (!cam || Time.time - s_Last < 0.35f) return;   // never a pile-up of splats
            s_Last = Time.time;
            // smallish and off to one side, so you can still see the kid who got you
            float side = Random.value < 0.5f ? -1f : 1f;
            var go = ProcMesh.Part("Face Splat", cam.transform, ProcMesh.Quad(), Palette.UnlitTextured(FxTextures.Clump, Color.white, true, "FaceSplat"),
                new Vector3(side * Random.Range(0.05f, 0.14f), Random.Range(-0.09f, 0.05f), 0.45f), Vector3.one * Random.Range(0.17f, 0.23f), new Vector3(0f, 0f, Random.Range(0f, 360f)));
            go.layer = Layers.UI3D;
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            rig.StartCoroutine(Fade(go, mr.material));
        }

        static IEnumerator Fade(GameObject go, Material m)
        {
            yield return new WaitForSeconds(0.45f);
            float t = 0f;
            var start = go ? go.transform.localPosition : Vector3.zero;
            while (go && t < 1f)
            {
                t += Time.deltaTime / 1.1f;
                m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f - t));
                go.transform.localPosition = start + Vector3.down * 0.03f * t;   // it slides off
                yield return null;
            }
            if (go) Object.Destroy(go);
            Object.Destroy(m);
        }
    }

    /// <summary>A big red "SNOWBALL FIGHT!" stamp slammed in the air in front of you, pulsing with the alarm.</summary>
    public static class FightStamp
    {
        public static IEnumerator Slam()
        {
            var rig = PlayerRig.Active;
            if (!rig) yield break;
            var root = new GameObject("SNOWBALL FIGHT").transform;
            root.gameObject.layer = Layers.UI3D;
            var fwd = rig.FlatForward;
            root.position = rig.Head.position + fwd * 1.6f + Vector3.up * 0.14f;
            root.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            UIKit.Stamp(root, "SNOWBALL FIGHT!", 1.3f, Palette.StampRed, 6f, 0.95f);
            root.gameObject.SetLayerRecursive(Layers.UI3D);
            yield return Ease.Tween(0.18f, t => root.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, t), Ease.InCubic, false);
            AudioDirector.Play2D(Sfx.StampSlam, 1f);
            rig.Left.Haptic(0.9f, 0.15f);
            rig.Right.Haptic(0.9f, 0.15f);
            float hold = 0f;
            while (hold < 2.2f)
            {
                hold += Time.deltaTime;
                root.localScale = Vector3.one * (1f + 0.05f * Mathf.Abs(Mathf.Sin(hold * Mathf.PI * 3.2f)));   // throbbing with the siren
                yield return null;
            }
            var from = root.position;
            yield return Ease.Tween(0.4f, t => { root.position = from + Vector3.up * 0.3f * t; root.localScale = Vector3.one * Mathf.Lerp(1f, 0f, Ease.InCubic(t)); }, Ease.Linear, false);
            Object.Destroy(root.gameObject);
        }
    }
}
