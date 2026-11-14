using System.Collections;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Plays the perfect run (doc: 13 actions — 6 photos, 5 shows, 1 scoop-and-throw) on the desktop rig, through
    /// the same input paths a player uses. For smoke-testing every mechanic and for capturing demo footage.
    /// Enable on the GameManager, or call FrostDebug.RunAutopilot() from the console / CLI.
    /// </summary>
    public class Autopilot : MonoBehaviour
    {
        public static Autopilot Instance { get; private set; }
        public string Status = "starting";
        public bool Done { get; private set; }
        public float speed = 1.5f;
        DesktopPlayerRig m_Rig;

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        IEnumerator Start()
        {
            m_Rig = PlayerRig.Active as DesktopPlayerRig;
            if (!m_Rig) { Status = "needs the desktop rig"; yield break; }
            var gm = GameManager.I;
            while (gm.State != GameState.Title) yield return null;
            yield return new WaitForSeconds(1.2f);
            Status = "opening the case";
            gm.StartCaseNow();
            while (gm.State != GameState.Investigating) yield return null;
            var director = CaseDirector.I;

            for (int step = 0; step < CaseDirector.Chain.Length; step++)
            {
                var s = CaseDirector.Chain[step];
                var clue = Clue.Find(s.clue);
                Status = $"step {step + 1}: photograph {s.clue}";
                int tries = 0;
                while (!HasEvidence(s.clue) && tries++ < 4) yield return PhotographClue(clue);
                Status = $"step {step + 1}: show {s.clue} to {s.kid}";
                yield return ShowTo(Kid.Find(s.kid));
                float t = 0;
                while (director.Step <= step && !director.FinaleStarted && t < 30f) { t += Time.deltaTime; yield return null; }
            }

            // finale
            var tony = Kid.Find(CastId.Tony) as TonyKid;
            Status = "finale: Tony bolts";
            while (tony.State == TonyKid.TonyState.Idle) yield return null;
            yield return new WaitForSeconds(1.0f);
            int attempts = 0;
            while (!director.CaseClosed && attempts++ < 12)
            {
                if (!tony.IsDazed)
                {
                    Status = "finale: scoop";
                    m_Rig.ClearScriptCrouch();
                    m_Rig.LookAt(tony.ChestPoint);
                    m_Rig.Scoop();
                    yield return new WaitForSeconds(1.0f);
                    if (SnowballHand.Instance && SnowballHand.Instance.IsHolding)
                    {
                        Status = "finale: throw";
                        // close the distance a little first
                        if (FrostUtil.FlatDistance(m_Rig.Feet, tony.transform.position) > 2.6f)
                            yield return WalkTo(Vector3.Lerp(m_Rig.Feet, tony.transform.position, 0.35f), 1.5f);
                        m_Rig.LookAt(tony.ChestPoint);
                        var lead = tony.AimPoint + tony.Velocity * 0.25f;
                        m_Rig.ThrowAt(lead, 0.7f);
                        yield return new WaitForSeconds(1.4f);
                    }
                }
                if (tony.IsDazed)
                {
                    Status = "finale: snap him while he's dazed";
                    var toPlayer = (m_Rig.Feet - tony.transform.position).Flat().normalized;
                    if (FrostUtil.FlatDistance(m_Rig.Feet, tony.transform.position) > 1.9f)
                        yield return WalkTo(tony.transform.position + toPlayer * 1.4f, 2.0f);
                    m_Rig.SetCrouch(0.35f);
                    m_Rig.LookAt(tony.HeadPoint + Vector3.down * 0.1f);
                    yield return new WaitForSeconds(0.25f);
                    m_Rig.PressShutter();
                    yield return new WaitForSeconds(1.2f);
                    m_Rig.ClearScriptCrouch();
                }
            }
            Status = director.CaseClosed ? "case closed" : "gave up";
            // admire the case file: level the view and hover the primary action
            HPointer.ForceGazeRay = true;
            m_Rig.ClearScriptCrouch();
            while (GameManager.I.State != GameState.Ended) yield return null;
            yield return new WaitForSeconds(2.2f);
            var again = GameObject.Find("Button · Play again");
            if (again) m_Rig.LookAt(again.transform.position + Vector3.up * 0.16f);
            yield return new WaitForSeconds(0.6f);
            if (again) m_Rig.LookAt(again.transform.position);
            Status = "case closed (results)";
            Done = true;
        }

        static bool HasEvidence(ClueId c)
        {
            var d = CaseDirector.I;
            foreach (var p in d.Photos) if (p.Clue == c && !p.Blurred) return true;
            return false;
        }

        IEnumerator PhotographClue(Clue clue)
        {
            if (!clue) yield break;
            var room = RoomService.Current;
            var focus = clue.FocusPoint;
            Vector3 dir;
            if (clue.useFacing) dir = clue.FacingNormal.Flat().normalized;
            else dir = (m_Rig.Feet - focus).Flat().normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            float standOff = focus.y < 0.5f ? 0.75f : 0.95f;
            var vantage = focus + dir * standOff;
            vantage.y = room.FloorY;
            vantage = room.ClampInside(vantage, 0.3f);
            yield return WalkTo(vantage, speed);
            m_Rig.SetCrouch(focus.y < 0.45f ? 1f : (focus.y < 0.75f ? 0.4f : 0f));
            yield return new WaitForSeconds(0.45f);
            m_Rig.LookAt(focus);
            yield return new WaitForSeconds(0.35f);
            m_Rig.LookAt(focus);
            m_Rig.PressShutter();
            yield return WaitForPrint();
            m_Rig.ClearScriptCrouch();
        }

        /// <summary>The print ejects, gets pulled out and develops (~3.5 s) before it can be shown to anyone.</summary>
        static IEnumerator WaitForPrint()
        {
            yield return new WaitForSeconds(0.4f);
            float until = Time.time + 6f;
            while (Time.time < until)
            {
                var cam = PolaroidCamera.Instance; var holder = PhotoHolder.Instance;
                bool ready = cam && !cam.PrintWaiting && holder && holder.Current && holder.Current.IsDeveloped;
                if (ready) break;
                yield return null;
            }
        }

        IEnumerator ShowTo(Kid kid)
        {
            if (!kid) yield break;
            var director = CaseDirector.I;
            var dir = (m_Rig.Feet - kid.transform.position).Flat().normalized;
            var spot = kid.transform.position + dir * 0.85f;
            spot = RoomService.Current.ClampInside(spot, 0.3f);
            yield return WalkTo(spot, speed);
            m_Rig.LookAt(kid.HeadPoint + Vector3.down * 0.15f);
            yield return new WaitForSeconds(0.25f);
            m_Rig.SetShowing(true);
            float t = 0f;
            while (!director.Reacting && t < 4f) { t += Time.deltaTime; m_Rig.LookAt(kid.HeadPoint + Vector3.down * 0.15f); yield return null; }
            while (director.Reacting) yield return null;
            m_Rig.SetShowing(false);
            yield return new WaitForSeconds(0.4f);
        }

        IEnumerator WalkTo(Vector3 target, float walkSpeed)
        {
            var room = RoomService.Current;
            var frosty = Frosty.Instance ? Frosty.Instance.transform.position : (Vector3?)null;
            target = room.ClampInside(target, 0.25f);
            // detour around Frosty if he's in the way
            if (frosty.HasValue)
            {
                var a = m_Rig.Feet.XZ(); var b = target.XZ(); var c = frosty.Value.XZ();
                var ab = b - a;
                float u = Mathf.Clamp01(Vector2.Dot(c - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                var closest = a + ab * u;
                if (Vector2.Distance(closest, c) < 0.75f && u > 0.05f && u < 0.95f)
                {
                    var side = (closest - c).sqrMagnitude > 1e-4f ? (closest - c).normalized : new Vector2(-ab.y, ab.x).normalized;
                    var via = c + side * 0.95f;
                    yield return WalkStraight(new Vector3(via.x, room.FloorY, via.y), walkSpeed);
                }
            }
            yield return WalkStraight(target, walkSpeed);
        }

        IEnumerator WalkStraight(Vector3 target, float walkSpeed)
        {
            var room = RoomService.Current;
            target.y = room.FloorY;
            float guard = 0f;
            while (FrostUtil.FlatDistance(m_Rig.Feet, target) > 0.04f && guard < 12f)
            {
                guard += Time.deltaTime;
                var to = (target - m_Rig.Feet).Flat();
                var step = to.normalized * Mathf.Min(to.magnitude, walkSpeed * Time.deltaTime);
                m_Rig.SetFeet(room.ClampInside(m_Rig.Feet + step, 0.22f));
                // look where we walk, slightly down
                var yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                var cur = m_Rig.Head.eulerAngles;
                m_Rig.SetYawPitch(Mathf.LerpAngle(cur.y, yaw, Time.deltaTime * 6f), Mathf.LerpAngle(cur.x > 180f ? cur.x - 360f : cur.x, 12f, Time.deltaTime * 4f));
                yield return null;
            }
        }
    }

    /// <summary>Console / CLI helpers (call through `unity command script eval`).</summary>
    public static class FrostDebug
    {
        public static string State()
        {
            var gm = GameManager.I;
            var d = CaseDirector.I;
            if (!gm) return "no game";
            var tony = Kid.Find(CastId.Tony) as TonyKid;
            return $"state={gm.State} paused={gm.IsPaused} step={(d != null ? d.Step : -1)} finale={(d != null && d.FinaleStarted)} closed={(d != null && d.CaseClosed)} " +
                   $"photos={(d != null ? d.Photos.Count : 0)} shows={(d != null ? d.Shows : 0)} throws={(d != null ? d.Throws : 0)} tony={(tony ? tony.State.ToString() : "-")} " +
                   $"autopilot={(FrostAndFound.Autopilot.Instance ? FrostAndFound.Autopilot.Instance.Status : "off")} kids={Kid.All.Count} clues={Clue.All.Count} room={(RoomService.Current != null ? RoomService.Current.Source : "none")}";
        }

        public static string RunAutopilot()
        {
            if (!GameManager.I) return "no game";
            if (FrostAndFound.Autopilot.Instance) return "already running";
            GameManager.I.gameObject.AddComponent<FrostAndFound.Autopilot>();
            return "autopilot started";
        }

        public static string StartCase() { GameManager.I?.StartCaseNow(); return State(); }
    }
}
