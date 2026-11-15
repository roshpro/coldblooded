using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// The chain logic (doc §3, §9): a tiny state machine with a step index 1..5. Each step names the clue that
    /// sparkles and the kid who reacts to its photo. A reaction is just (kid, photo's clue) → animation + voice
    /// line, and the right match advances the step. Also runs the intro, Lucy's safety-net nudges, the finale
    /// and the outro.
    /// </summary>
    public class CaseDirector : MonoBehaviour
    {
        public static CaseDirector I { get; private set; }

        public struct ChainStep
        {
            public ClueId clue; public CastId kid; public string excuse, point; public CastId? pointsAt; public ClueId next;
        }

        public static readonly ChainStep[] Chain =
        {
            // (the kids still point at the next suspect; Lucy's guide line names the next clue, so there's no "that's him" line)
            new ChainStep { clue = ClueId.CocoaStain,    kid = CastId.Dexter,  excuse = "dexter_excuse",  point = null,            pointsAt = CastId.Sherman, next = ClueId.CarrotBits },
            new ChainStep { clue = ClueId.CarrotBits,    kid = CastId.Sherman, excuse = "sherman_excuse", point = null,            pointsAt = CastId.Serena,  next = ClueId.PurpleGlitter },
            new ChainStep { clue = ClueId.PurpleGlitter, kid = CastId.Serena,  excuse = "serena_excuse",  point = null,            pointsAt = null,           next = ClueId.RedScrape },
            new ChainStep { clue = ClueId.RedScrape,     kid = CastId.Lucy,    excuse = "lucy_excuse",    point = null,            pointsAt = CastId.Tony,    next = ClueId.DentedSled },
            new ChainStep { clue = ClueId.DentedSled,    kid = CastId.Tony,    excuse = null,             point = null,            pointsAt = null,           next = ClueId.None },
        };

        public int Step { get; private set; }               // 0-based index into Chain
        public bool FinaleStarted { get; private set; }
        public bool CaseClosed { get; private set; }
        public bool Reacting { get; private set; }
        public readonly HashSet<ClueId> Solved = new HashSet<ClueId>();
        public readonly List<PhotoData> Photos = new List<PhotoData>();
        public int Shows, WrongShows, Throws, Hits;
        public float StartTime { get; private set; }
        public float StepStartedAt { get; private set; }
        public float EndTime { get; private set; }

        float m_LastProgress, m_NextNudge;
        int m_NudgeCount;
        bool m_FirstTooFarHinted, m_FinaleHintGiven;
        PhotoData m_GuiltyPhoto;

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        void OnEnable()
        {
            PolaroidCamera.PhotoTaken += OnPhotoTaken;
            PolaroidCamera.Evaluate += EvaluatePhoto;
            Snowball.Thrown += OnThrown;
        }

        void OnDisable()
        {
            PolaroidCamera.PhotoTaken -= OnPhotoTaken;
            PolaroidCamera.Evaluate -= EvaluatePhoto;
            Snowball.Thrown -= OnThrown;
        }

        public ClueId CurrentClue => Step < Chain.Length ? Chain[Step].clue : ClueId.None;
        public CastId CurrentKid => Chain[Mathf.Min(Step, Chain.Length - 1)].kid;

        public void NotifyActivity() { /* snowball fun doesn't count as progress, but resets nothing */ }

        void Progress()
        {
            m_LastProgress = Time.time;
            m_NextNudge = Time.time + FrostTuning.I.idleNudgeSeconds;
        }

        // ------------------------------------------------------------------ intro (0:00–0:10)
        public IEnumerator RunIntro()
        {
            StartTime = Time.time;
            var lucy = Kid.Find(CastId.Lucy) as LucyKid;
            var frosty = Frosty.Instance;
            var snowfall = FindAnyObjectByType<Snowfall>();
            snowfall?.Flurry(260);
            yield return new WaitForSeconds(0.6f);
            if (frosty) yield return frosty.Reveal();
            AudioDirector.Play2D(Sfx.Sting, 0.85f);
            yield return new WaitForSeconds(0.4f);
            if (lucy)
            {
                var player = PlayerRig.Active;
                lucy.Face(player.Head.position);
                yield return lucy.SayAndWait("lucy_intro");
            }
            // the cocoa splash sparkles; the drip trail leads to Dexter
            SetSparkling(ClueId.CocoaStain);
            StepStartedAt = Time.time;
            Progress();
        }

        // ------------------------------------------------------------------ photos
        void EvaluatePhoto(PhotoData photo, Camera lens)
        {
            var tony = Kid.Find(CastId.Tony) as TonyKid;
            if (!FinaleStarted || !tony || CaseClosed) return;
            var vp = lens.WorldToViewportPoint(tony.ChestPoint);
            bool inFrame = vp.z > 0.1f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.1f && vp.y < 1.1f;
            if (!inFrame) return;
            float dist = Vector3.Distance(lens.transform.position, tony.ChestPoint);
            if (tony.IsDazed && dist <= FrostTuning.I.tonyPhotoRange && !Occluded(lens.transform.position, tony))
            {
                photo.Guilty = true;
                photo.Clue = ClueId.GuiltyTony;
                photo.ClueUV = new Vector2(vp.x, vp.y);
            }
            else if (!tony.IsDazed)
            {
                // he ducks, dives or hides — the photo comes out as a useless blur
                photo.Blurred = true;
                if (photo.Clue == ClueId.DentedSled) photo.Clue = ClueId.None;
            }
            else
            {
                // dazed, but you're too far away or something's in the way: he stays down a moment longer and Lucy says so
                photo.TooFar = true;
                tony.GiveMoreTime(1.5f);
            }
        }

        static bool Occluded(Vector3 from, Kid target)
        {
            var to = target.HeadPoint;
            var hits = Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to) - 0.15f, Layers.Mask(Layers.Room), QueryTriggerInteraction.Ignore);
            return hits.Length > 0;
        }

        void OnPhotoTaken(PhotoData photo)
        {
            Photos.Add(photo);
            // poses: whoever is in frame strikes their pose
            foreach (var id in photo.KidsInFrame)
            {
                var kid = Kid.Find(id);
                if (kid && !(FinaleStarted && id == CastId.Tony)) kid.OnPhotographed(photo);
            }
            if (photo.Guilty) { m_GuiltyPhoto = photo; StartCoroutine(CloseTheCase(photo)); return; }
            if (photo.Subject == SubjectKind.Sled) SledQuip();
            if (FinaleStarted && photo.TooFar && Kid.Find(CastId.Tony) is TonyKid t && t.IsDazed) StartCoroutine(CloserHint());

            if (photo.Clue != ClueId.None && photo.Clue == CurrentClue)
            {
                Progress();
                // step 1's second cue: the drip trail sparkles once you've taken the stain photo
                if (photo.Clue == ClueId.CocoaStain && Frosty.Instance && Frosty.Instance.Drips) Frosty.Instance.Drips.SetSparkling(true);
            }
            else if (photo.TooFar && !m_FirstTooFarHinted && !FinaleStarted)
            {
                m_FirstTooFarHinted = true;
                StartCoroutine(LucyLine("lucy_nudge_close", 1.2f));
            }
            if (photo.Blurred && FinaleStarted && !m_FinaleHintGiven) StartCoroutine(FinaleHint());
        }

        float m_LastCloserHint = -99f;

        IEnumerator CloserHint()
        {
            if (Time.time - m_LastCloserHint < 4f) yield break;
            m_LastCloserHint = Time.time;
            yield return new WaitForSeconds(0.25f);
            var lucy = Kid.Find(CastId.Lucy);
            var line = Lines.Get("lucy_finale_closer");
            if (lucy && line != null) DialogueRunner.Say(line, lucy, false);
        }

        IEnumerator LucyLine(string id, float delay)
        {
            yield return new WaitForSeconds(delay);
            var lucy = Kid.Find(CastId.Lucy);
            if (lucy && !DialogueRunner.AnyoneSpeaking && !Reacting) DialogueRunner.Say(Lines.Get(id), lucy, false);
        }

        void OnThrown(Snowball ball) { if (!ball.Thrower && GameManager.I && GameManager.I.State == GameState.Investigating) Throws++; }   // only yours, in the case (not the title)
        public void OnTonyHit() { Hits++; }

        // ------------------------------------------------------------------ show a photo (doc §5.2)
        public bool CanShow => !Reacting && !CaseClosed && !SnowballFight.Active;

        public void ShowPhoto(Kid kid, PhotoData photo)
        {
            if (!CanShow || kid == null || photo == null) return;
            StartCoroutine(React(kid, photo));
        }

        IEnumerator React(Kid kid, PhotoData photo)
        {
            Reacting = true;
            Shows++;
            kid.Busy = true;
            var player = PlayerRig.Active;
            kid.StopMoving();
            if (player) kid.Face(player.Head.position, 0.3f);
            // they lean in and squint at the photo
            kid.Anim.LeanInAndSquint(1.0f);
            yield return new WaitForSeconds(0.75f);

            var clue = photo.Blurred ? ClueId.None : photo.Clue;
            bool isCurrent = clue != ClueId.None && clue == CurrentClue;
            var step = Chain[Mathf.Min(Step, Chain.Length - 1)];

            if (isCurrent && kid.id == step.kid)
            {
                Progress();
                yield return RightKid(kid, step);
            }
            else if (clue == ClueId.None)
            {
                yield return ReactToPicture(kid, photo);
            }
            else if (Solved.Contains(clue) && OwnerOf(clue) == kid.id)
            {
                yield return kid.SayAndWait(Cast.Key(kid.id) + "_again");
            }
            else
            {
                // wrong kid: a joke line and a nudge toward the right one
                WrongShows++;
                var owner = OwnerOf(clue);
                // their joke line about this clue if it's been recorded, else their recorded "not mine" line
                var specific = Lines.Get(Cast.Key(kid.id) + "_wrong_" + ClueKey(clue));
                var generic = Lines.Get(Cast.Key(kid.id) + "_wrong_generic");
                var line = AudioDirector.HasVoice(specific) ? specific
                         : AudioDirector.HasVoice(generic) ? generic
                         : specific ?? generic ?? Lines.Get(Cast.Key(kid.id) + "_snow");
                var target = Kid.Find(owner);
                if (target && target != kid) kid.Anim.Point(target.HeadPoint, 2.2f);
                yield return DialogueRunner.SayAndWait(line, kid);
            }
            kid.Busy = false;
            Reacting = false;
        }

        // ------------------------------------------------------------------ a photo that isn't evidence
        readonly Dictionary<CastId, int> m_SelfShows = new Dictionary<CastId, int>();

        /// <summary>They react to what's actually in the picture: themselves (a delighted double-take and their
        /// photo pose), another kid (a jab, pointing them out), Frosty, the sled, the toys, the trees, plain snow,
        /// or nothing much at all.</summary>
        IEnumerator ReactToPicture(Kid kid, PhotoData photo)
        {
            string me = Cast.Key(kid.id);
            Line line = null;
            if (photo.Blurred) line = Lines.Get(kid.id == CastId.Tony ? "tony_blur" : me + "_blur");
            else if (photo.KidsFeatured.Contains(kid.id))
            {
                m_SelfShows.TryGetValue(kid.id, out int n);
                m_SelfShows[kid.id] = n + 1;
                line = Lines.Get(me + "_self_" + (n % 2 + 1));
                if (kid.Faces) kid.Faces.Show(FaceExpression.Wide, 0.5f);
                if (kid.Anim && !kid.Anim.CalmArms) kid.Anim.StrikePose(kid.SignaturePose, 1.4f);
            }
            else
            {
                switch (photo.Subject)
                {
                    case SubjectKind.Kid:
                        line = Lines.Get(me + "_about_" + Cast.Key(photo.SubjectKid));
                        var other = Kid.Find(photo.SubjectKid);
                        if (line != null && other && other != kid && kid.Anim && !kid.Anim.CalmArms) kid.Anim.Point(other.HeadPoint, 2f);
                        break;
                    case SubjectKind.Frosty: line = Lines.Get(me + "_frosty"); break;
                    case SubjectKind.Sled: line = Lines.Get(me + "_sled"); break;
                    case SubjectKind.Toys: line = Lines.Get(me + "_toys"); break;
                    case SubjectKind.Tree: line = Lines.Get(me + "_tree"); break;
                    case SubjectKind.Nature: line = Lines.Get(me + "_nature"); break;
                    case SubjectKind.Snow: line = Lines.Get(me + "_snow"); break;
                }
            }
            line ??= Lines.Get(me + "_nothing") ?? Lines.Get(me + "_snow");
            if (line != null) yield return DialogueRunner.SayAndWait(line, kid);
        }

        static string ClueKey(ClueId c) => c switch
        {
            ClueId.CocoaStain => "stain",
            ClueId.CarrotBits => "carrot",
            ClueId.PurpleGlitter => "glitter",
            ClueId.RedScrape => "scrape",
            ClueId.DentedSled => "sled",
            _ => "snow",
        };

        static CastId OwnerOf(ClueId c)
        {
            foreach (var s in Chain) if (s.clue == c) return s.kid;
            return CastId.Lucy;
        }

        IEnumerator RightKid(Kid kid, ChainStep step)
        {
            Solved.Add(step.clue);
            Clue.Find(step.clue)?.MarkSolved();
            if (step.clue == ClueId.CocoaStain)
            {
                if (Frosty.Instance && Frosty.Instance.Drips) Frosty.Instance.Drips.SetSparkling(false);
                (kid as DexterKid)?.SetWhistling(false);
            }

            if (step.kid == CastId.Tony)
            {
                // the finale: "Circumstantial! No picture of ME, no case!" — goggles down, and he bolts
                var tony = kid as TonyKid;
                Step = Chain.Length;
                FinaleStarted = true;
                kid.Busy = false;
                Reacting = false;
                if (tony) yield return tony.Confronted();
                StartCoroutine(FinaleWatch());
                yield break;
            }

            yield return kid.SayAndWait(step.excuse);
            // the next clue starts sparkling (with a dun-dun-DUN)
            if (step.pointsAt.HasValue)
            {
                var next = Kid.Find(step.pointsAt.Value);
                if (next)
                {
                    if (kid is LucyKid lucy) lucy.JabAt(next.HeadPoint);
                    else kid.Anim.Point(next.HeadPoint, 2.4f);
                }
                if (!string.IsNullOrEmpty(step.point)) yield return kid.SayAndWait(step.point);
            }
            else if (step.next == ClueId.RedScrape && Frosty.Instance)
            {
                kid.Anim.Point(Frosty.Instance.Scrape.FocusPoint, 2f);
                yield return new WaitForSeconds(0.6f);
            }
            Step++;
            StepStartedAt = Time.time;
            SetSparkling(step.next);
            AudioDirector.Play2D(Sfx.Sting, 0.8f);
            Progress();
            StartCoroutine(GuideLine(Step));
        }

        /// <summary>Lucy points out the clue to photograph next ("Um, hello? The carrot?"), once the talking's done.
        /// Step 0's line (the cocoa stain) opens the case from the gesture tutorial.</summary>
        IEnumerator GuideLine(int step)
        {
            var line = Lines.Get("lucy_pic" + (step + 1));
            var lucy = Kid.Find(CastId.Lucy) as LucyKid;
            if (line == null || !lucy) yield break;
            // she's the guide throughout: wait for a quiet moment (up to ~15 s) instead of giving up on the line
            float waited = 0f;
            while ((DialogueRunner.AnyoneSpeaking || Reacting || lucy.Busy) && waited < 15f) { waited += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.4f);
            while (DialogueRunner.IsSpeaking(CastId.Lucy) || lucy.Busy) yield return null;
            if (Step != step || FinaleStarted || CaseClosed) yield break;
            var player = PlayerRig.Active;
            if (player) lucy.Face(player.Head.position, 0.3f);
            var at = GuideTarget(step);
            if (at.HasValue) lucy.JabAt(at.Value);
            DialogueRunner.Say(line, lucy, false);
        }

        public static Vector3? GuideTarget(int step)
        {
            var id = step < Chain.Length ? Chain[step].clue : ClueId.None;
            if (id == ClueId.DentedSled) { var tony = Kid.Find(CastId.Tony); if (tony) return tony.HeadPoint; }   // "see that guy in red?"
            var clue = Clue.Find(id);
            return clue ? clue.FocusPoint : (Vector3?)null;
        }

        float m_LastSledQuip = -99f;

        /// <summary>Photograph the sled and its (supposedly anonymous) owner can't help himself: "Get my good side."</summary>
        void SledQuip()
        {
            var tony = Kid.Find(CastId.Tony);
            var player = PlayerRig.Active;
            if (!tony || !player || tony.Busy || FinaleStarted || Time.time - m_LastSledQuip < 40f || DialogueRunner.IsSpeaking(CastId.Tony)) return;
            if (FrostUtil.FlatDistance(tony.transform.position, player.Head.position) > 6f) return;
            m_LastSledQuip = Time.time;
            var line = Lines.Get("tony_pose");
            if (line != null) DialogueRunner.Say(line, tony, false);
        }

        void SetSparkling(ClueId id)
        {
            foreach (var c in Clue.All) if (c && !c.IsSolved) c.SetSparkling(c.id == id);
        }

        // ------------------------------------------------------------------ finale (doc §6)
        IEnumerator FinaleWatch()
        {
            float start = Time.time;
            AudioDirector.MusicBoost = 1.25f;
            // Lucy's hint comes after the first useless blur, or a few seconds in
            while (!CaseClosed && !m_FinaleHintGiven && Time.time - start < 7f) yield return null;
            if (!CaseClosed && !m_FinaleHintGiven) yield return FinaleHint();
        }

        IEnumerator FinaleHint()
        {
            if (m_FinaleHintGiven) yield break;
            m_FinaleHintGiven = true;
            yield return new WaitForSeconds(0.8f);
            var lucy = Kid.Find(CastId.Lucy) as LucyKid;
            if (!lucy || CaseClosed) yield break;
            lucy.StartCoroutine(lucy.GlanceAtSnow(2.5f));
            yield return lucy.SayAndWait("lucy_finale_hint");
        }

        IEnumerator CloseTheCase(PhotoData photo)
        {
            if (CaseClosed) yield break;
            CaseClosed = true;
            EndTime = Time.time;
            var tony = Kid.Find(CastId.Tony) as TonyKid;
            tony?.Caught();
            AudioDirector.MusicBoost = 0.6f;
            yield return new WaitForSeconds(1.1f);
            GameManager.I?.EnterOutro(photo);
        }

        // ------------------------------------------------------------------ safety nets
        void Update()
        {
            if (GameManager.I == null || GameManager.I.State != GameState.Investigating || FinaleStarted || CaseClosed) return;
            if (Reacting || DialogueRunner.AnyoneSpeaking || SnowballFight.Active) { m_NextNudge = Mathf.Max(m_NextNudge, Time.time + 4f); return; }
            if (Time.time < m_NextNudge) return;
            StartCoroutine(Nudge());
            m_NextNudge = Time.time + FrostTuning.I.idleNudgeSeconds + 6f;
        }

        IEnumerator Nudge()
        {
            var lucy = Kid.Find(CastId.Lucy) as LucyKid;
            if (!lucy) yield break;
            m_NudgeCount++;
            var current = CurrentClue;
            bool havePhoto = false;
            foreach (var p in Photos) if (p.Clue == current && !p.Blurred) havePhoto = true;
            if (havePhoto)
            {
                // she squints at whoever's next
                var next = Kid.Find(CurrentKid);
                if (next && next != lucy) lucy.StartCoroutine(lucy.SquintAt(next.Rig.Head, 3f));
                yield return lucy.SayAndWait(m_NudgeCount % 3 == 0 ? "lucy_nudge_show" : "lucy_nudge_guilty");
            }
            else
            {
                var clue = Clue.Find(current);
                if (clue) lucy.StartCoroutine(lucy.SquintAt(clue.transform, 3f));
                string id = (current == ClueId.RedScrape && m_NudgeCount >= 2) ? "lucy_nudge_crouch"
                          : (m_NudgeCount % 3 == 2 ? "lucy_nudge_worst" : "lucy_nudge_sparkle");
                yield return lucy.SayAndWait(id);
            }
        }

        // ------------------------------------------------------------------ debug / autopilot hooks
        public void DebugSkipTo(int step)
        {
            for (int i = 0; i < step && i < Chain.Length; i++) { Solved.Add(Chain[i].clue); Clue.Find(Chain[i].clue)?.MarkSolved(); }
            Step = Mathf.Clamp(step, 0, Chain.Length - 1);
            SetSparkling(CurrentClue);
            var dexter = Kid.Find(CastId.Dexter) as DexterKid;
            if (step > 0) dexter?.SetWhistling(false);
            Progress();
        }
    }
}
