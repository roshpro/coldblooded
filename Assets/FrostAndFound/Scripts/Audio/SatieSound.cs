using System.Collections.Generic;
using UnityEngine;
#if FROST_SATIE
using Satie.Core;
#endif

namespace FrostAndFound
{
    /// <summary>
    /// The snowy-day soundscape from Satie (Satie/scene.satie, compiled into Resources/SatieCore by the satie-unity
    /// installer): wind in the redwoods, falling snow, creaking boughs, and the footsteps, scoops, throws, hits and
    /// camera sounds. When the Satie package is installed (FROST_SATIE) and its player is running, the game fires
    /// these events instead of playing its own synthesized versions; otherwise every call returns false and the
    /// game's own sounds play as before.
    /// </summary>
    public static class SatieSound
    {
        /// <summary>Is Satie playing the scene?</summary>
        public static bool On
        {
            get
            {
#if FROST_SATIE
                return SatieCoreBehaviour.Instance != null;
#else
                return false;
#endif
            }
        }

        /// <summary>Fire a scene event at a point (true = Satie played it, so skip the game's own sound).</summary>
        public static bool Fire(string eventName, Vector3 at, string surface = null)
        {
#if FROST_SATIE
            if (!On) return false;
            SatieCore.Fire(eventName, at, surface);
            return true;
#else
            return false;
#endif
        }

        public const string Snow = "snow";

        internal static void Bind(string host, Transform target)
        {
#if FROST_SATIE
            if (target) SatieCore.Bind(host, target);
#endif
        }

        internal static void State(string name)
        {
#if FROST_SATIE
            SatieCore.State(name);
#endif
        }
    }

    /// <summary>Keeps Satie told what's going on: which mode the game is in (menu, investigating, snowball fight,
    /// chase, case closed) and where the kids, Tony's sled, Frosty and the camera are.</summary>
    public class SatieStateDriver : MonoBehaviour
    {
        string m_State;
        readonly HashSet<Object> m_Bound = new HashSet<Object>();
        float m_NextBind;

        void Update()
        {
            if (!SatieSound.On) return;
            AudioDirector.RoomToneMuted = true;   // the wind and falling snow replace the plain room tone

            var s = CurrentState();
            if (s != m_State) { m_State = s; SatieSound.State(s); }

            if (Time.unscaledTime < m_NextBind) return;
            m_NextBind = Time.unscaledTime + 1f;
            foreach (var k in Kid.All)
            {
                if (!k || m_Bound.Contains(k)) continue;
                m_Bound.Add(k);
                SatieSound.Bind(k.id.ToString().ToLowerInvariant(), k.transform);
                if (k is TonyKid tony && tony.Sled) SatieSound.Bind("tony_sled", tony.Sled.transform);
            }
            if (Frosty.Instance && m_Bound.Add(Frosty.Instance)) SatieSound.Bind("frosty", Frosty.Instance.transform);
            var cam = PolaroidCamera.Instance;
            if (cam && m_Bound.Add(cam)) SatieSound.Bind("polaroid_camera", cam.transform);
            var rig = PlayerRig.Active;
            if (rig && m_Bound.Add(rig)) SatieSound.Bind("player", rig.Head);
        }

        static string CurrentState()
        {
            var gm = GameManager.I;
            var cd = CaseDirector.I;
            if (!gm || gm.IsPaused || gm.State == GameState.Boot || gm.State == GameState.Title || gm.State == GameState.Intro) return "menu";
            if (gm.State == GameState.Outro || gm.State == GameState.Ended || (cd != null && cd.CaseClosed)) return "case_closed";
            if (SnowballFight.Active) return "snowball_fight";
            if (cd != null && cd.FinaleStarted) return "chase";
            return "investigating";
        }
    }
}
