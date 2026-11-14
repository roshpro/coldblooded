using UnityEngine;

namespace FrostAndFound
{
    public enum CastId { Dexter, Sherman, Serena, Lucy, Tony }

    public enum ClueId
    {
        None,
        CocoaStain,     // step 1 → Dexter
        CarrotBits,     // step 2 → Sherman
        PurpleGlitter,  // step 3 → Serena
        RedScrape,      // step 4 → Lucy
        DentedSled,     // step 5 → Tony
        GuiltyTony      // finale photo of dazed Tony
    }

    public enum HandSide { Left, Right }

    /// <summary>
    /// Every number worth tuning in playtests, in one asset (Resources/FrostTuning).
    /// The doc says: tune detection forgiveness and aim assist in playtesting rather than cranking them up.
    /// </summary>
    [CreateAssetMenu(menuName = "Frost & Found/Tuning", fileName = "FrostTuning")]
    public class FrostTuning : ScriptableObject
    {
        [Header("Photo")]
        [Tooltip("A clue counts if it is in frame and within this distance of the lens (doc: about 1.5 m).")]
        public float clueRange = 1.5f;
        [Tooltip("Extra viewport margin that still counts as 'in frame' (forgives slightly off-centre shots).")]
        [Range(0f, 0.25f)] public float frameForgiveness = 0.06f;
        [Tooltip("Range for the finale photo of dazed Tony (he is big in frame, and dazed only ~4 s).")]
        public float tonyPhotoRange = 3.0f;
        public float photoFov = 52f;
        public int photoResolution = 512;
        public float developFadeSeconds = 0.3f;

        [Header("Show a photo")]
        [Tooltip("Kid must be within this distance of your hand (doc: about 1.5 m).")]
        public float showRange = 1.5f;
        [Tooltip("Hand must be at least this far from your head (arm extended toward the kid) to count as held out.")]
        public float armExtendedDistance = 0.46f;
        [Tooltip("Max angle between your outstretched hand and the kid.")]
        public float showConeDegrees = 38f;
        public float showDwellSeconds = 0.3f;

        [Header("Snow & snowballs")]
        [Tooltip("Hand must be this close to the snow surface to scoop.")]
        public float scoopHeight = 0.16f;
        [Range(0f, 1f)] public float gripClosed = 0.72f;
        [Range(0f, 1f)] public float gripOpen = 0.35f;
        public float snowballRadius = 0.04f;
        [Tooltip("Hand-tracked throws under-read speed; this compensates a little.")]
        public float throwMultiplierHands = 1.2f;
        public float throwMultiplierControllers = 1.05f;
        public float minThrowSpeed = 0.9f;
        public float maxThrowSpeed = 18f;
        [Header("Aim assist (light nudge, not a homing missile)")]
        public float aimAssistConeDegrees = 11f;
        [Range(0f, 1f)] public float aimAssistStrength = 0.35f;
        public float aimAssistMaxDegrees = 4.5f;

        [Header("Finale")]
        public float tonyDazeSeconds = 4.2f;
        public float tonyRunSpeed = 1.15f;
        public float tonyPanicSpeed = 1.7f;
        [Tooltip("If Tony still hasn't been hit this long after he bolts, he trips over his own sled (doc: by ~1:40).")]
        public float tonyTripAfterSeconds = 25f;
        public float tonyDodgeCooldown = 0.35f;

        [Header("Safety nets")]
        [Tooltip("Seconds of no progress before Lucy gives a vague nudge (doc: about 15 s).")]
        public float idleNudgeSeconds = 15f;

        [Header("Layout")]
        public float kidWallInset = 0.55f;
        public float minKidSpacing = 0.9f;
        public Vector2 fallbackRoomSize = new Vector2(4f, 4f);

        [Header("Audio")]
        [Range(0f, 1f)] public float musicVolume = 0.32f;
        [Range(0f, 1f)] public float sfxVolume = 0.9f;
        [Range(0f, 1f)] public float voiceVolume = 1f;

        static FrostTuning s_Instance;
        public static FrostTuning I
        {
            get
            {
                if (!s_Instance)
                {
                    s_Instance = Resources.Load<FrostTuning>("FrostTuning");
                    if (!s_Instance) s_Instance = CreateInstance<FrostTuning>();
                }
                return s_Instance;
            }
        }
    }

    /// <summary>Player-facing options that persist between sessions.</summary>
    public static class FrostPrefs
    {
        public static event System.Action Changed;

        public static bool LeftHanded
        {
            get => PlayerPrefs.GetInt("frost.lefty", 0) == 1;
            set { PlayerPrefs.SetInt("frost.lefty", value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static bool Subtitles
        {
            get => PlayerPrefs.GetInt("frost.subs", 1) == 1;
            set { PlayerPrefs.SetInt("frost.subs", value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static bool Tips
        {
            get => PlayerPrefs.GetInt("frost.tips", 1) == 1;
            set { PlayerPrefs.SetInt("frost.tips", value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        /// <summary>The hand holding the Polaroid (right unless lefty mode).</summary>
        public static HandSide CameraHand => LeftHanded ? HandSide.Left : HandSide.Right;
        public static HandSide FreeHand => LeftHanded ? HandSide.Right : HandSide.Left;
    }
}
