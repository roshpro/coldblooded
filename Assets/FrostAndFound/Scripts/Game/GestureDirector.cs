using System.Collections;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// When the in-game gesture tutorials play (the title screen's snowball gesture lives on the title itself):
    /// - Camera pinch: right after Lucy tells you to take a picture at the start of the case, and again whenever
    ///   the current clue still needs a photo and you haven't taken one in a long while. Hidden on your next photo.
    /// - Scoop &amp; throw: once Tony runs, if you haven't thrown a snowball in a long while. Hidden once you scoop.
    /// Both respect the "Tips" toggle.
    /// </summary>
    public class GestureDirector : MonoBehaviour
    {
        float m_InvestigatingSince = -1f, m_LastPhoto = -1f, m_LastThrow = -1f, m_FinaleSince = -1f;
        bool m_Prompted;

        void OnEnable()
        {
            PolaroidCamera.PhotoTaken += OnPhoto;
            Snowball.Thrown += OnThrown;
            SnowballHand.Formed += OnFormed;
        }

        void OnDisable()
        {
            PolaroidCamera.PhotoTaken -= OnPhoto;
            Snowball.Thrown -= OnThrown;
            SnowballHand.Formed -= OnFormed;
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
            if (gm.State != GameState.Investigating || d.CaseClosed)
            {
                GestureHint.Hide(GestureKind.Camera);
                GestureHint.Hide(GestureKind.ScoopThrow);
                return;
            }
            if (m_InvestigatingSince < 0f) m_InvestigatingSince = Time.time;
            if (!FrostPrefs.Tips) { GestureHint.Hide(GestureKind.Camera); GestureHint.Hide(GestureKind.ScoopThrow); return; }
            var tune = FrostTuning.I;

            if (d.FinaleStarted)
            {
                GestureHint.Hide(GestureKind.Camera);
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

        IEnumerator PromptPhoto()
        {
            var lucy = Kid.Find(CastId.Lucy);
            if (lucy) yield return lucy.SayAndWait("lucy_snap", false);
            var d = CaseDirector.I;
            if (d != null && d.Photos.Count == 0 && !d.FinaleStarted && FrostPrefs.Tips) GestureHint.Show(GestureKind.Camera);
        }
    }
}
