using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrostAndFound
{
    public enum GameState { Boot, Title, Intro, Investigating, Outro, Ended }

    /// <summary>
    /// The shell: boots the right player (Quest via Meta XR SDK, or the desktop simulation), finds the room,
    /// lays snow, shows the title, runs the case, and closes with the outro and the Case File.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        [Tooltip("The Meta XR camera rig in the scene (used on device / Link / Meta XR Simulator).")]
        public GameObject xrRig;
        [Tooltip("Force the desktop simulation even if an XR device is active.")]
        public bool forceDesktop;
        [Tooltip("Play the perfect run automatically (desktop only) — for testing and demo capture.")]
        public bool autopilot;
        [Tooltip("Size of the stand-in room used by the desktop simulation (try 3 × 3 to test a tight venue).")]
        public Vector2 simulatedRoomSize = new Vector2(4.4f, 4.0f);

        public GameState State { get; private set; } = GameState.Boot;
        public bool IsPaused { get; private set; }
        public bool AllowsGameplay => State == GameState.Investigating && !IsPaused;
        public PlayerRig Player { get; private set; }
        public LayoutDirector.Result Layout { get; private set; }

        TitleScreen m_Title;
        MenuScreen m_Pause;
        PolaroidCamera m_Camera;
        CaseDirector m_Director;
        GameObject m_Scenery;
        Snowfall m_Snowfall;
        OpeningSequence m_Intro;
        float m_MenuHold;

        void Awake()
        {
            I = this;
            Fx.Reset();
            RoomTint.Reset();
            AudioDirector.Ensure();
            DialogueRunner.Ensure();
            HPointer.Ensure();
            Application.targetFrameRate = -1;
            RoomService.Clear();
        }

        void OnDestroy() { if (I == this) I = null; Time.timeScale = 1f; }

        IEnumerator Start()
        {
            bool xr = !forceDesktop && XRActive();
            if (!xr && !forceDesktop && Application.platform == RuntimePlatform.Android)
            {
                // On the headset the OpenXR session is often still starting when the scene loads, so isDeviceActive
                // reads false for the first frames. Give it a moment, but the APK only ever runs on a Quest: never
                // build the desktop stand-in room there (it would cover the real room with virtual walls).
                for (float t = 0f; t < 2f && !(xr = XRActive()); t += Time.unscaledDeltaTime) yield return null;
                if (!xr) Debug.LogWarning("[Frost] XR not reported active after 2 s; using the headset rig anyway.");
                xr = true;
            }
#if FROST_META
            if (xr && xrRig)
            {
                xrRig.SetActive(true);
                Player = xrRig.GetComponentInChildren<MetaPlayerRig>(true);
                if (!Player) Player = xrRig.AddComponent<MetaPlayerRig>();
                var provider = gameObject.AddComponent<MetaRoomProvider>();
                provider.Begin(Player);
            }
            else
#endif
            {
                if (xrRig) xrRig.SetActive(false);
                var size = new Vector2(Mathf.Max(2.5f, simulatedRoomSize.x), Mathf.Max(2.5f, simulatedRoomSize.y));
                var room = SimRoom.Build(size, out _);
                Player = DesktopPlayerRig.Create(new Vector3(0.2f, 0f, -size.y * 0.5f + 0.45f), Quaternion.Euler(0, -4f, 0));
                RoomService.Set(room);
            }
            Debug.Log("[Frost] Player: " + (Player && Player.IsXR ? "Meta XR (Quest)" : "desktop simulation"));
            while (RoomService.Current == null) yield return null;
            var r = RoomService.Current;
            Player.FloorY = r.FloorY;
            UIOverlayCamera.Attach(Player.ViewCamera);   // messages and menus always draw on top
#if FROST_META && (FROST_META_OPENXR || FROST_OCULUS_XR)
            if (Player.IsXR) EnableDepthOcclusion();   // needs a Depth API provider (Meta OpenXR or Oculus XR plug-in)
#endif

            // the world: snow on the floor, light snowfall, your footprints
            SnowSurface.Create(r);
            m_Snowfall = Snowfall.Create(r);
            m_Scenery = Scenery.Create(r, Player, Random.Range(1, int.MaxValue));   // a fresh arrangement every game: snowy redwoods, shrubs, rocks and drifts — more of them in bigger rooms
            gameObject.AddComponent<FootprintTracker>();
            PhotoBackdrop.Create(r);

            // your kit: Polaroid (camera hand), photo slot + snow (free hand), mittens
            m_Camera = PolaroidCamera.Create();
            m_Camera.Enabled = false;
            gameObject.AddComponent<PhotoHolder>();
            gameObject.AddComponent<SnowballHand>().Enabled = false;
            gameObject.AddComponent<ShowPhotoDetector>();
            Mittens.Create();
            gameObject.AddComponent<PlayerPunch>();
            m_Director = gameObject.AddComponent<CaseDirector>();

            gameObject.AddComponent<TipsDirector>();
            gameObject.AddComponent<SatieStateDriver>();   // the snowy-day soundscape, when the Satie package is installed
            gameObject.AddComponent<GestureDirector>();
            gameObject.AddComponent<SnowballFight>();   // hit the same kid four times and it's war
            yield return null;
            EnterTitle();
            if (autopilot && !Player.IsXR) gameObject.AddComponent<Autopilot>();
        }

#if FROST_META && (FROST_META_OPENXR || FROST_OCULUS_XR)
        /// <summary>Meta's Depth API: real things in front of the snow (your feet, the sofa) hide it, so it reads as
        /// lying in your room rather than painted over it. Hands are removed from the depth map because the
        /// mittens stand in for them.</summary>
        void EnableDepthOcclusion()
        {
            if (!Meta.XR.EnvironmentDepth.EnvironmentDepthManager.IsSupported)
            {
                Debug.Log("[Frost] Depth API not supported here; snow drawn without occlusion.");
                return;
            }
            var depth = gameObject.AddComponent<Meta.XR.EnvironmentDepth.EnvironmentDepthManager>();
            depth.OcclusionShadersMode = Meta.XR.EnvironmentDepth.OcclusionShadersMode.SoftOcclusion;
            depth.RemoveHands = true;
            Debug.Log("[Frost] Depth occlusion on (soft, hands removed).");
        }
#endif

        static bool XRActive()
        {
            var settings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            bool loader = settings && settings.Manager && settings.Manager.activeLoader != null;
            return loader && UnityEngine.XR.XRSettings.isDeviceActive;
        }

        // ------------------------------------------------------------------ title
        void EnterTitle()
        {
            State = GameState.Title;
            // the title is just your room: no snow, no scenery, no colour change, quiet
            RoomTint.Normal();
            if (SnowSurface.Instance) SnowSurface.Instance.Coverage = 0f;
            if (m_Snowfall) m_Snowfall.SetFalling(false);
            if (m_Scenery) m_Scenery.SetActive(false);
            var prints = GetComponent<FootprintTracker>();
            if (prints) prints.enabled = false;
            AudioDirector.MusicGate = 0f;
            AudioDirector.Quiet = true;
            m_Title = TitleScreen.Create(StartCaseNow);
        }

        void Update()
        {
            if (!Player) return;
            if (State == GameState.Intro)
            {
                // hold the menu button briefly to skip the intro (with hand tracking the menu gesture is a single press)
                bool hands = Player.CameraHand.IsOpticalHand;
                m_MenuHold = Player.MenuHeld ? m_MenuHold + Time.unscaledDeltaTime : 0f;
                if (m_Intro && (m_MenuHold > 0.5f || (hands && Player.MenuDown))) m_Intro.Skip();
            }
            else if (Player.MenuDown && State != GameState.Boot && State != GameState.Title) TogglePause();
            if (m_Camera) m_Camera.Enabled = AllowsGameplay && !HPointer.ConsumedThisFrame && !HPointer.IsOverUI;
            if (SnowballHand.Instance) SnowballHand.Instance.Enabled = State == GameState.Investigating && !IsPaused;
        }

        public void StartCaseNow() { if (State == GameState.Title) StartCoroutine(BeginCase()); }

        IEnumerator BeginCase()
        {
            if (State != GameState.Title) yield break;
            State = GameState.Intro;
            if (m_Title) yield return m_Title.Dismiss();
            // "The Knock": a door ~2 m in front of you, Frosty's body ~2 m behind you, the kids around him (hidden)
            var room = RoomService.Current;
            var feet = Player.FeetPosition;
            var fwd = Player.FlatForward;
            float ahead = 2f;
            var door = feet + fwd * ahead;
            if (room.EdgeDistance(door.XZ()) < 0.6f)
                for (ahead = 2f; ahead > 1.2f && room.EdgeDistance((feet + fwd * ahead).XZ()) < 0.6f; ahead -= 0.1f) { }
            door = room.ClampInside(feet + fwd * ahead, 0.5f);
            door.y = room.FloorY;
            Layout = LayoutDirector.Build(room, Player, feet - fwd * 2f);
            m_Intro = OpeningSequence.Begin(this, Player, Layout, m_Scenery, m_Snowfall, door, () => State = GameState.Investigating);
            while (!m_Intro.Done) yield return null;
        }

        static IEnumerator ColourBack(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) { RoomTint.Noir(1f - t / seconds); yield return null; }
            RoomTint.Normal();
        }

        // ------------------------------------------------------------------ pause
        public void TogglePause()
        {
            if (IsPaused)
            {
                IsPaused = false;
                Time.timeScale = 1f;
                if (m_Pause) Destroy(m_Pause.gameObject);
                AudioListener.pause = false;
            }
            else
            {
                IsPaused = true;
                Time.timeScale = 0f;
                m_Pause = MenuScreen.Open(TogglePause, Restart, PauseContext());
            }
        }

        string PauseContext()
        {
            var d = m_Director;
            if (State == GameState.Title || d == null) return "Case #0001 · not opened yet";
            if (d.CaseClosed) return "Case #0001 · closed";
            if (d.FinaleStarted) return "Case #0001 · finale — catch Tony";
            return $"Case #0001 · clue {Mathf.Min(d.Step + 1, 5)} of 5";
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            RoomTint.Normal();   // back to the Play button with the room back to normal
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ------------------------------------------------------------------ outro (doc §6.7, ~5 s)
        public void EnterOutro(PhotoData guilty) => StartCoroutine(Outro(guilty));

        IEnumerator Outro(PhotoData guilty)
        {
            if (State == GameState.Outro || State == GameState.Ended) yield break;
            State = GameState.Outro;
            var tony = Kid.Find(CastId.Tony) as TonyKid;
            if (tony) yield return tony.SayAndWait("tony_caught");
            // the kids gather around the culprit, on the far side from you, so the crowd frames the scene
            var feet = Player.FeetPosition;
            var center = tony ? tony.transform.position : (Frosty.Instance ? Frosty.Instance.transform.position : RoomService.Current.Center);
            var away = (center - feet).Flat();
            away = away.sqrMagnitude > 1e-3f ? away.normalized : Player.FlatForward;
            float[] angles = { -78f, -34f, 34f, 78f };
            int i = 0;
            foreach (var kid in Kid.All)
            {
                if (!kid || kid.id == CastId.Tony || i >= angles.Length) continue;
                var spot = center + Quaternion.Euler(0, angles[i], 0) * away * 0.95f;
                spot = RoomService.Current.ClampInside(spot, 0.32f);
                if (FrostUtil.FlatDistance(spot, feet) < 1.25f) spot = RoomService.Current.ClampInside(spot + away * (1.25f - FrostUtil.FlatDistance(spot, feet)), 0.32f);
                kid.CanMove = true;
                kid.Busy = false;
                var k = kid;
                kid.MoveTo(spot, 1.0f, () => k.Face(Player.Head.position));
                i++;
            }
            yield return new WaitForSeconds(1.0f);
            var lucy = Kid.Find(CastId.Lucy) as LucyKid;
            if (lucy) lucy.Anim.StrikePose(Pose.Cheer, 1.2f);
            StartCoroutine(ColourBack(1.4f));   // the room's colour comes back at CASE CLOSED
            yield return CaseClosedStamp.Slam();
            if (lucy) DialogueRunner.Say(Lines.Get("lucy_case_closed"), lucy, true);
            foreach (var kid in Kid.All) if (kid && kid.id != CastId.Tony) kid.Anim.StrikePose(Pose.Cheer, 1.5f);

            // your photos fan out as a Case File collage
            var d = m_Director;
            var evidence = new List<PolaroidPhoto>();
            PolaroidPhoto guiltyPhoto = null;
            var holder = PhotoHolder.Instance;
            if (holder)
            {
                // the newest good shot of each clue, in chain order, then the GUILTY photo
                foreach (var step in CaseDirector.Chain)
                {
                    PolaroidPhoto found = null;
                    for (int k = holder.Album.Count - 1; k >= 0 && !found; k--)
                    {
                        var p = holder.Album[k];
                        if (p && p.Data.Clue == step.clue && p.Data.IsEvidence) found = p;
                    }
                    evidence.Add(found);
                }
                foreach (var p in holder.Album) if (p && p.Data.Guilty) { guiltyPhoto = p; break; }
                holder.enabled = false;
            }
            float secs = d.EndTime - d.StartTime;
            string time = $"{(int)(secs / 60)}:{(int)(secs % 60):00}";
            ResultsScreen.Show(guiltyPhoto, evidence, time, d.Photos.Count, d.Shows, d.WrongShows, d.Throws, Restart);
            State = GameState.Ended;
        }
    }

    /// <summary>
    /// The virtual Polaroid can't see the real room through passthrough, so photos would come out black behind
    /// the subject. The fix from the doc: a snowy backdrop only the Polaroid lens can see.
    /// </summary>
    public static class PhotoBackdrop
    {
        public static void Create(RoomInfo room)
        {
            var go = new GameObject("Photo Backdrop (Polaroid only)");
            go.layer = Layers.PhotoOnly;
            go.transform.position = room.Center + Vector3.up * 1.2f;
            go.transform.localScale = Vector3.one * 40f;
            go.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Sphere(24, 16);
            var mat = new Material(FrostAssets.Shader(FrostAssets.ShaderId.Sky)) { name = "PhotoSky" };
            mat.SetColor("_TopColor", Palette.SkyTop);
            mat.SetColor("_HorizonColor", Palette.SkyHorizon);
            mat.SetColor("_BottomColor", Palette.SkyGround);
            mat.SetFloat("_Flakes", 0.6f);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }
}
