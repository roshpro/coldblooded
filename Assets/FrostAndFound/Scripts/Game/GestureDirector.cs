using System.Collections;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// When the in-game gesture tutorials play (the title screen's snowball gesture lives on the title itself):
    /// - Camera pinch: right after Lucy tells you to take a picture at the start of the case, and again whenever
    ///   the current clue still needs a photo and you haven't taken one in a long while. Hidden on your next photo.
    /// - Scoop &amp; throw: once Tony runs, if you haven't thrown a snowball in a long while. Hidden once you scoop.
    /// - Show a photo: holding a developed photo without showing it to anyone (soon for your first, then only if you
    ///   seem stuck). Hidden the moment you show one, or when you stop holding a photo.
    /// The take-photo hint always plays with the show-photo hint beside it. All respect the "Tips" toggle.
    /// </summary>
    public class GestureDirector : MonoBehaviour
    {
        float m_InvestigatingSince = -1f, m_LastPhoto = -1f, m_LastThrow = -1f, m_FinaleSince = -1f;
        bool m_Prompted;
        PolaroidPhoto m_HeldPhoto, m_ShownPhoto;
        float m_HeldSince = -1f;
        bool m_EverShown;

        void OnEnable()
        {
            PolaroidCamera.PhotoTaken += OnPhoto;
            Snowball.Thrown += OnThrown;
            SnowballHand.Formed += OnFormed;
            ShowPhotoDetector.Shown += OnShown;
        }

        void OnDisable()
        {
            PolaroidCamera.PhotoTaken -= OnPhoto;
            Snowball.Thrown -= OnThrown;
            SnowballHand.Formed -= OnFormed;
            ShowPhotoDetector.Shown -= OnShown;
        }

        void OnShown(Kid kid)
        {
            m_EverShown = true;
            m_ShownPhoto = PhotoHolder.Instance ? PhotoHolder.Instance.Current : null;   // no more hints for this one
            GestureHint.Hide(GestureKind.ShowPhoto);
        }

        static void HideAll()
        {
            GestureHint.Hide(GestureKind.Camera);
            GestureHint.Hide(GestureKind.ScoopThrow);
            GestureHint.Hide(GestureKind.ShowPhoto);
        }

        void OnPhoto(PhotoData p)
        {
            m_LastPhoto = Time.time;
            GestureHint.Hide(GestureKind.Camera);
        }

        void OnThrown(Snowball ball)
        {
            if (ball.Thrower) return;   // a kid's throw
            m_LastThrow = Time.time;
            GestureHint.Hide(GestureKind.ScoopThrow);
        }

        void OnFormed() => GestureHint.Hide(GestureKind.ScoopThrow);

        void Update()
        {
            var gm = GameManager.I;
            var d = CaseDirector.I;
            if (!gm || d == null || gm.IsPaused) return;
            if (gm.State != GameState.Investigating || d.CaseClosed || SnowballFight.Active) { HideAll(); return; }
            if (m_InvestigatingSince < 0f) m_InvestigatingSince = Time.time;
            if (!FrostPrefs.Tips) { HideAll(); return; }
            var tune = FrostTuning.I;

            if (d.FinaleStarted)
            {
                GestureHint.Hide(GestureKind.Camera);
                GestureHint.Hide(GestureKind.ShowPhoto);
                if (m_FinaleSince < 0f) m_FinaleSince = Time.time;
                var tony = Kid.Find(CastId.Tony) as TonyKid;
                bool holding = SnowballHand.Instance && SnowballHand.Instance.IsHolding;
                float idle = Time.time - Mathf.Max(m_FinaleSince, m_LastThrow);
                if (!holding && !(tony && tony.IsDazed) && idle > tune.throwGestureIdleSeconds && !GestureHint.IsShowing(GestureKind.ScoopThrow))
                    GestureHint.Show(GestureKind.ScoopThrow);
                return;
            }

            // the opening prompt: Lucy tells you to take a picture, then the pinch plays
            if (!m_Prompted && Time.time - m_InvestigatingSince > tune.cameraPromptDelay && !DialogueRunner.AnyoneSpeaking && !d.Reacting)
            {
                m_Prompted = true;
                if (d.Photos.Count == 0) StartCoroutine(PromptPhoto());
            }

            // holding a developed photo but not showing it to anyone: teach holding it out to a kid
            var holder = PhotoHolder.Instance;
            var held = holder && holder.Visible && holder.Current && holder.Current.IsDeveloped ? holder.Current : null;
            if (held != m_HeldPhoto) { m_HeldPhoto = held; m_HeldSince = Time.time; }
            if (!held) GestureHint.Hide(GestureKind.ShowPhoto);
            else if (held != m_ShownPhoto && d.CanShow && !GestureHint.IsShowing(GestureKind.ShowPhoto) && !GestureHint.OtherShowing(GestureKind.ShowPhoto)
                     && Time.time - m_HeldSince > (m_EverShown ? tune.showGestureIdleSeconds : tune.showGestureFirstSeconds))
                GestureHint.Show(GestureKind.ShowPhoto);

            // supposed to take a picture but hasn't for a long while: the current clue has no usable photo yet
            if (m_Prompted && !d.Reacting && !NeedsNoPhoto(d) && !GestureHint.IsShowing(GestureKind.Camera))
            {
                float idle = Time.time - Mathf.Max(m_InvestigatingSince, m_LastPhoto, d.StepStartedAt);
                if (idle > tune.cameraGestureIdleSeconds) GestureHint.Show(GestureKind.Camera);
            }
        }

        static bool NeedsNoPhoto(CaseDirector d)
        {
            var clue = d.CurrentClue;
            if (clue == ClueId.None) return true;
            foreach (var p in d.Photos) if (p.Clue == clue && !p.Blurred) return true;
            return false;
        }

        /// <summary>The opening's last beat: Lucy points out the cocoa stain now, and the pinch hint follows.</summary>
        public void PromptNow()
        {
            if (m_Prompted) return;
            m_Prompted = true;
            StartCoroutine(PromptPhoto());
        }

        IEnumerator PromptPhoto()
        {
            var lucy = Kid.Find(CastId.Lucy);
            var line = Lines.Get("lucy_pic1") ?? Lines.Get("lucy_snap");
            if (lucy && line != null)
            {
                // she points the cocoa stain out; the pinch hint comes up once she's got to "take a picture"
                var at = CaseDirector.GuideTarget(0);
                if (at.HasValue && lucy is LucyKid lk) lk.JabAt(at.Value);
                DialogueRunner.Say(line, lucy, false);
                yield return null;
                float t = 0f;
                while (t < 3.2f && DialogueRunner.IsSpeaking(CastId.Lucy)) { t += Time.deltaTime; yield return null; }
            }
            var d = CaseDirector.I;
            if (d != null && d.Photos.Count == 0 && !d.FinaleStarted && FrostPrefs.Tips) GestureHint.Show(GestureKind.Camera);
        }
    }
}
