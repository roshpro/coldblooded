using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Keeps the little control hints rare: each at most twice a session, the second time only after a long
    /// gap, and never again once the player has shown they know how (done it twice).
    /// </summary>
    public static class Hints
    {
        class State { public int shown, done; public float last = -999f; }
        static readonly Dictionary<string, State> s_State = new Dictionary<string, State>();

        static State Get(string key)
        {
            if (!s_State.TryGetValue(key, out var s)) s_State[key] = s = new State();
            return s;
        }

        /// <summary>May this hint show now? Counts it as shown if so.</summary>
        public static bool Allow(string key, int maxTimes = 2, float gap = 90f)
        {
            var s = Get(key);
            if (s.done >= 2 || s.shown >= maxTimes || Time.time - s.last < gap) return false;
            s.shown++;
            s.last = Time.time;
            return true;
        }

        /// <summary>The player did the thing this hint teaches.</summary>
        public static void Done(string key) => Get(key).done++;
    }

    /// <summary>
    /// A few short control tips (toggle "Tips" in the menu). They teach controls, never answers — the doc's
    /// safety nets for the mystery itself are Lucy's nudges. The looping gesture animations live in GestureDirector.
    /// </summary>
    public class TipsDirector : MonoBehaviour
    {
        bool m_PhotoTip, m_ShowTip, m_ThrowTip, m_CrouchTip;
        float m_EvidenceAt = -1f;

        void OnEnable()
        {
            PolaroidCamera.PhotoTaken += OnPhoto;
            SnowballHand.Formed += OnFormed;
        }

        void OnDisable()
        {
            PolaroidCamera.PhotoTaken -= OnPhoto;
            SnowballHand.Formed -= OnFormed;
        }

        void Update()
        {
            var gm = GameManager.I;
            var d = CaseDirector.I;
            if (!gm || d == null || gm.State != GameState.Investigating || gm.IsPaused) return;
            if (!m_PhotoTip && d.Photos.Count == 0 && !DialogueRunner.AnyoneSpeaking)
            {
                m_PhotoTip = true;
                var rig = PlayerRig.Active;
                string side = rig ? (rig.CameraHand.Side == HandSide.Left ? "left" : "right") : "left";
                string tip = !(rig && rig.IsXR) ? "Take a photo: left click (WASD to walk, C to crouch)"
                    : rig.CameraHand.IsOpticalHand ? null   // hand tracking: the pinch gesture animation teaches it (GestureDirector)
                    : $"Hold the {side} grip to raise the camera, pull the {side} trigger to shoot";
                if (tip != null) HToast.Show(tip, UIKit.Icon.Camera, 5f);
            }
            if (!m_ShowTip && m_EvidenceAt > 0f && Time.time - m_EvidenceAt > 7f && d.Shows == 0 && !DialogueRunner.AnyoneSpeaking)
            {
                m_ShowTip = true;
                var rig = PlayerRig.Active;
                HToast.Show(rig && rig.IsXR ? "Show it: hold your photo out toward a kid" : "Show it: walk up to a kid and hold F", UIKit.Icon.Hand, 5f);
            }
            if (!m_CrouchTip && d.CurrentClue == ClueId.RedScrape && Time.time - d.StepStartedAt > 20f && !d.FinaleStarted)
            {
                m_CrouchTip = true;
                HToast.Show("Some clues are low. Crouch down for a closer look.", UIKit.Icon.Magnifier, 4.5f);
            }
        }

        void OnPhoto(PhotoData p)
        {
            if (p.IsEvidence && m_EvidenceAt < 0f) m_EvidenceAt = Time.time;
        }

        void OnFormed()
        {
            if (m_ThrowTip) return;
            m_ThrowTip = true;
            var rig = PlayerRig.Active;
            HToast.Show(rig && rig.IsXR ? "Throw: swing and open your hand" : "Throw: hold and release right click", UIKit.Icon.Snowball, 3.5f);
        }
    }
}
