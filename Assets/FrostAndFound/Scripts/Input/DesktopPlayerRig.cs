using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FrostAndFound
{
    /// <summary>
    /// Keyboard + mouse stand-in for the headset so the whole game can be played and tested in the Editor.
    ///   Mouse look (click to capture, Esc to release) · WASD walk · C / Ctrl crouch
    ///   Left click  — Polaroid shutter          · F (hold)  — hold the photo out to a kid
    ///   Q           — reach down & scoop snow    · Right mouse (hold, release) — wind up & throw
    ///   Tab         — swap handedness            · P / Esc   — pause
    /// Every action is also exposed as a method so the Autopilot / tests can drive it.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class DesktopPlayerRig : PlayerRig
    {
        [SerializeField] float m_EyeHeight = 1.55f;
        [SerializeField] float m_CrouchHeight = 0.92f;
        [SerializeField] float m_WalkSpeed = 1.4f;
        [SerializeField] float m_MouseSensitivity = 0.12f;

        Transform m_Head;
        Camera m_Camera;
        float m_Yaw, m_Pitch;
        float m_Crouch;           // 0..1
        float m_ScriptCrouch = -1f;
        Vector3 m_Feet;

        // free-hand animation state
        enum FreeHandMode { Idle, Show, Scooping, Holding, Windup, Throwing }
        FreeHandMode m_FreeMode;
        float m_FreeGrip;
        Vector3 m_FreeTargetLocal;
        Coroutine m_FreeRoutine;
        float m_WindupStart;
        bool m_ScriptShow;
        bool m_MenuDown;
        bool m_ScriptTrigger;

        public override Transform Head => m_Head;
        public override Camera ViewCamera => m_Camera;
        public override bool IsXR => false;
        public override bool MenuDown => m_MenuDown;
        public override bool MenuHeld
        {
            get
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                return kb != null && (kb.pKey.isPressed || kb.escapeKey.isPressed);
            }
        }
        public bool IsScoopingOrThrowing => m_FreeMode == FreeHandMode.Scooping || m_FreeMode == FreeHandMode.Throwing;

        static readonly Vector3 kCamHandLocal = new Vector3(0.2f, -0.21f, 0.46f);
        static readonly Vector3 kFreeHandLocal = new Vector3(-0.19f, -0.2f, 0.3f);
        static readonly Vector3 kFreeShowLocal = new Vector3(-0.05f, -0.09f, 0.62f);
        static readonly Vector3 kWindupLocal = new Vector3(-0.2f, 0.06f, -0.02f);
        static readonly Vector3 kThrowEndLocal = new Vector3(-0.02f, -0.02f, 0.62f);

        public static DesktopPlayerRig Create(Vector3 feet, Quaternion facing)
        {
            var go = new GameObject("Desktop Player (simulated headset)");
            var rig = go.AddComponent<DesktopPlayerRig>();
            rig.Build(feet, facing);
            return rig;
        }

        void Build(Vector3 feet, Quaternion facing)
        {
            m_Feet = feet;
            FloorY = feet.y;
            m_Yaw = facing.eulerAngles.y;
            m_Pitch = 8f;

            m_Head = new GameObject("Head").transform;
            m_Head.SetParent(transform, false);
            m_Camera = m_Head.gameObject.AddComponent<Camera>();
            m_Camera.nearClipPlane = 0.03f;
            m_Camera.farClipPlane = 60f;
            m_Camera.fieldOfView = 70f;
            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = Palette.SkyHorizon;
            m_Camera.cullingMask = ~Layers.Mask(Layers.PhotoOnly);
            m_Camera.tag = "MainCamera";
            m_Head.gameObject.AddComponent<AudioListener>();
            var data = m_Head.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            // no post / AA: the Quest renderer runs post on-tile only, and MSAA already smooths edges
            data.renderPostProcessing = false;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;

            SetupHand(Left, "Left Hand");
            SetupHand(Right, "Right Hand");
            ApplyPose(true);
        }

        void SetupHand(HandRig h, string name)
        {
            h.Anchor = new GameObject(name).transform;
            h.Anchor.SetParent(m_Head, false);
            h.Palm = new GameObject("Palm").transform;
            h.Palm.SetParent(h.Anchor, false);
            h.Palm.localPosition = new Vector3(0, -0.01f, 0.04f);
            h.Tip = new GameObject("Tip").transform;
            h.Tip.SetParent(h.Anchor, false);
            h.Tip.localPosition = new Vector3(0, 0, 0.09f);
            h.IsTracked = true;
            h.IsOpticalHand = false;
        }

        /// <summary>The desktop stand-in for dimming passthrough: the sky goes towards night, and past halfway the
        /// simulated room's ceiling lifts away so you can see up into it (for the northern lights).</summary>
        public override void SetPassthroughDim(float dim, float seconds = 0f)
        {
            if (!m_Camera) return;
            if (m_NightFade != null) StopCoroutine(m_NightFade);
            m_NightFade = StartCoroutine(Night(Mathf.Clamp01(dim / 0.5f), seconds));
        }

        Coroutine m_NightFade;

        System.Collections.IEnumerator Night(float to, float seconds)
        {
            var night = new Color(0.03f, 0.045f, 0.11f);
            var from = m_Camera.backgroundColor;
            var target = Color.Lerp(Palette.SkyHorizon, night, to);
            if (to > 0.5f)
                foreach (var t in FindObjectsByType<Transform>())
                    if (t.name == "Ceiling" && t.GetComponentInParent<SimRoom>()) t.gameObject.SetActive(false);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                m_Camera.backgroundColor = Color.Lerp(from, target, t / seconds);
                yield return null;
            }
            m_Camera.backgroundColor = target;
        }

        public override void Teleport(Vector3 feet, Quaternion facing)
        {
            m_Feet = feet; FloorY = feet.y;
            m_Yaw = facing.eulerAngles.y;
            ApplyPose(true);
        }

        // ------------------------------------------------------------------ scripted API (Autopilot, tests)
        public void LookAt(Vector3 world)
        {
            var d = world - m_Head.position;
            if (d.sqrMagnitude < 1e-6f) return;
            var rot = Quaternion.LookRotation(d.normalized);
            var e = rot.eulerAngles;
            m_Yaw = e.y;
            m_Pitch = e.x > 180f ? e.x - 360f : e.x;
            ApplyPose(true);
        }
        public void SetYawPitch(float yaw, float pitch) { m_Yaw = yaw; m_Pitch = pitch; ApplyPose(true); }
        public void SetFeet(Vector3 feet) { m_Feet = feet; FloorY = feet.y; ApplyPose(true); }
        public Vector3 Feet => m_Feet;
        public void SetCrouch(float amount) { m_ScriptCrouch = amount; }
        public void ClearScriptCrouch() { m_ScriptCrouch = -1f; }
        public void PressShutter() { m_ScriptTrigger = true; }
        public void SetShowing(bool on) { m_ScriptShow = on; }
        public void Scoop() { if (m_FreeMode == FreeHandMode.Idle || m_FreeMode == FreeHandMode.Show) Restart(ScoopRoutine()); }
        public void ThrowAt(Vector3 target, float power01 = 0.75f)
        {
            if (m_FreeMode != FreeHandMode.Holding) return;
            Restart(ScriptedThrow(target, power01));
        }
        public void ThrowForward(float power01) { if (m_FreeMode == FreeHandMode.Holding || m_FreeMode == FreeHandMode.Windup) Restart(ThrowRoutine(null, power01)); }

        void Restart(IEnumerator r)
        {
            if (m_FreeRoutine != null) StopCoroutine(m_FreeRoutine);
            m_FreeRoutine = StartCoroutine(r);
        }

        // ------------------------------------------------------------------ frame
        void Update()
        {
            m_MenuDown = false;
            bool shutter = m_ScriptTrigger; m_ScriptTrigger = false;
            bool showHeld = m_ScriptShow;
            Vector2 move = Vector2.zero;
            bool crouchHeld = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool menuOpen = GameManager.I && GameManager.I.IsPaused;
            if (mouse != null)
            {
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                if (!locked && mouse.leftButton.wasPressedThisFrame && !menuOpen && Application.isFocused && MouseInGameView() && !UIVisible())
                {
                    Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                }
                else if (locked)
                {
                    var d = mouse.delta.ReadValue();
                    m_Yaw += d.x * m_MouseSensitivity;
                    m_Pitch = Mathf.Clamp(m_Pitch - d.y * m_MouseSensitivity, -80f, 85f);
                    if (mouse.leftButton.wasPressedThisFrame) shutter = true;
                    if (mouse.rightButton.wasPressedThisFrame && m_FreeMode == FreeHandMode.Holding) Restart(WindupRoutine());
                    if (mouse.rightButton.wasReleasedThisFrame && m_FreeMode == FreeHandMode.Windup)
                        Restart(ThrowRoutine(null, Mathf.Clamp01((Time.time - m_WindupStart) / 0.6f)));
                }
            }
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                    else m_MenuDown = true;
                }
                if (kb.pKey.wasPressedThisFrame) m_MenuDown = true;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
                crouchHeld = kb.cKey.isPressed || kb.leftCtrlKey.isPressed;
                if (kb.fKey.isPressed) showHeld = true;
                if (kb.qKey.wasPressedThisFrame) Scoop();
                if (kb.tabKey.wasPressedThisFrame) FrostPrefs.LeftHanded = !FrostPrefs.LeftHanded;
                if ((kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) && GameManager.I && GameManager.I.State == GameState.Title && !HPointer.IsOverUI) GameManager.I.StartCaseNow();
            }
#endif
            // walking
            if (move.sqrMagnitude > 0f)
            {
                var fwd = Quaternion.Euler(0, m_Yaw, 0) * Vector3.forward;
                var right = Quaternion.Euler(0, m_Yaw, 0) * Vector3.right;
                var delta = (fwd * move.y + right * move.x).normalized * (m_WalkSpeed * Time.deltaTime);
                var next = m_Feet + delta;
                if (RoomService.Current != null) next = RoomService.Current.ClampInside(next, 0.25f);
                m_Feet = next;
            }

            float crouchTarget = m_ScriptCrouch >= 0f ? m_ScriptCrouch : (crouchHeld ? 1f : 0f);
            if (m_FreeMode == FreeHandMode.Scooping) crouchTarget = Mathf.Max(crouchTarget, 0.75f);
            m_Crouch = Ease.Damp(m_Crouch, crouchTarget, 10f, Time.deltaTime);

            // free hand idle / show
            if (m_FreeMode == FreeHandMode.Idle || m_FreeMode == FreeHandMode.Show)
            {
                m_FreeMode = showHeld ? FreeHandMode.Show : FreeHandMode.Idle;
                m_FreeTargetLocal = showHeld ? kFreeShowLocal : kFreeHandLocal;
                m_FreeGrip = 0.15f;
            }

            // camera hand trigger
            CameraHand.UpdateTrigger(shutter ? 1f : 0f);
            FreeHand.UpdateTrigger(0f);
            FreeHand.Grip = m_FreeGrip;
            CameraHand.Grip = 0.9f; // holding the camera

            ApplyPose(false);
        }

        void LateUpdate() => SampleTrackers();

        void ApplyPose(bool snap)
        {
            float h = Mathf.Lerp(m_EyeHeight, m_CrouchHeight, m_Crouch);
            transform.position = Vector3.zero;
            m_Head.position = m_Feet + Vector3.up * h;
            m_Head.rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);

            // hands live in head space with a little lag so motion reads as physical
            var cam = CameraHand;
            var free = FreeHand;
            float dt = snap ? 1f : Time.deltaTime;
            var camTarget = MirrorFor(cam, kCamHandLocal);
            cam.Anchor.localPosition = snap ? camTarget : Ease.Damp(cam.Anchor.localPosition, camTarget, 18f, dt);
            // aim the Polaroid so its lens converges on what the player looks at, 1.4 m ahead
            var aimPoint = m_Head.position + m_Head.forward * 1.4f;
            cam.Anchor.rotation = Quaternion.LookRotation(aimPoint - cam.Anchor.position, m_Head.up);

            if (m_FreeMode != FreeHandMode.Scooping)
            {
                var target = MirrorFor(free, m_FreeTargetLocal);
                free.Anchor.localPosition = snap ? target : Ease.Damp(free.Anchor.localPosition, target,
                    m_FreeMode == FreeHandMode.Throwing ? 40f : 14f, dt);
                free.Anchor.localRotation = Quaternion.Euler(m_FreeMode == FreeHandMode.Show ? -10f : 10f, free.Side == HandSide.Left ? 12f : -12f, 0);
            }
        }

        static Vector3 MirrorFor(HandRig h, Vector3 local)
        {
            // positions above are authored for a right-handed camera hand / left free hand.
            bool authoredForRight = local.x > 0f;
            bool wantRight = h.Side == HandSide.Right;
            if (authoredForRight != wantRight && Mathf.Abs(local.x) > 0.001f) local.x = -local.x;
            return local;
        }

        static bool UIVisible()
        {
            foreach (var c in HControl.All) if (c && c.isActiveAndEnabled) return true;
            return false;
        }

        static bool MouseInGameView()
        {
#if UNITY_EDITOR
            var p = Mouse.current.position.ReadValue();
            return p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height;
#else
            return true;
#endif
        }

        // ------------------------------------------------------------------ free-hand routines
        IEnumerator ScoopRoutine()
        {
            m_FreeMode = FreeHandMode.Scooping;
            var hand = FreeHand;
            var start = hand.Anchor.position;
            var feetFwd = m_Feet + Quaternion.Euler(0, m_Yaw, 0) * new Vector3(hand.Side == HandSide.Left ? -0.18f : 0.18f, 0, 0.5f);
            float snowY = SnowSurface.Instance ? SnowSurface.Instance.SurfaceHeight : FloorY;
            var down = new Vector3(feetFwd.x, snowY + 0.03f, feetFwd.z);
            m_FreeGrip = 0.05f;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)
            {
                hand.Anchor.position = Vector3.Lerp(start, down, Ease.InOutCubic(t));
                hand.Anchor.rotation = Quaternion.LookRotation(Vector3.down + m_Head.forward * 0.4f, m_Head.forward);
                yield return null;
            }
            // close the hand in the snow
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.18f)
            {
                hand.Anchor.position = down;
                m_FreeGrip = Mathf.Lerp(0.05f, 1f, t);
                hand.Grip = m_FreeGrip;
                yield return null;
            }
            m_FreeGrip = 1f;
            yield return null;
            var back = m_Head.TransformPoint(MirrorFor(hand, kFreeHandLocal));
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.3f)
            {
                back = m_Head.TransformPoint(MirrorFor(hand, kFreeHandLocal));
                hand.Anchor.position = Vector3.Lerp(down, back, Ease.InOutCubic(t));
                yield return null;
            }
            m_FreeTargetLocal = kFreeHandLocal;
            hand.Anchor.localPosition = MirrorFor(hand, kFreeHandLocal);
            bool holding = SnowballHand.Instance && SnowballHand.Instance.IsHolding;
            m_FreeMode = holding ? FreeHandMode.Holding : FreeHandMode.Idle;
            if (!holding) m_FreeGrip = 0.15f;
            m_FreeRoutine = null;
        }

        IEnumerator WindupRoutine()
        {
            m_FreeMode = FreeHandMode.Windup;
            m_WindupStart = Time.time;
            m_FreeTargetLocal = kWindupLocal;
            m_FreeGrip = 1f;
            while (m_FreeMode == FreeHandMode.Windup) yield return null;
        }

        IEnumerator ScriptedThrow(Vector3 target, float power01)
        {
            m_FreeMode = FreeHandMode.Windup;
            m_FreeTargetLocal = kWindupLocal;
            m_FreeGrip = 1f;
            yield return new WaitForSeconds(0.35f);
            yield return ThrowRoutine(target, power01);
        }

        IEnumerator ThrowRoutine(Vector3? target, float power01)
        {
            m_FreeMode = FreeHandMode.Throwing;
            var hand = FreeHand;
            m_FreeTargetLocal = kThrowEndLocal;
            m_FreeGrip = 1f;
            Vector3 velocity;
            if (target.HasValue)
            {
                velocity = Ballistics.SolveLaunch(hand.Palm.position, target.Value, Mathf.Lerp(6.5f, 11f, power01), true);
            }
            else
            {
                float speed = Mathf.Lerp(5f, 13f, power01);
                velocity = (m_Head.forward + Vector3.up * 0.12f).normalized * speed;
            }
            hand.ReleaseVelocityOverride = velocity;
            yield return new WaitForSeconds(0.09f);
            m_FreeGrip = 0f;     // opening the hand releases the snowball
            hand.Grip = 0f;
            yield return new WaitForSeconds(0.05f);
            hand.ReleaseVelocityOverride = null;
            yield return new WaitForSeconds(0.15f);
            m_FreeTargetLocal = kFreeHandLocal;
            m_FreeGrip = 0.15f;
            m_FreeMode = FreeHandMode.Idle;
            m_FreeRoutine = null;
        }

        /// <summary>Called by SnowballHand when a ball forms so the desktop hand goes into hold mode.</summary>
        public void NotifyHolding()
        {
            if (m_FreeMode == FreeHandMode.Idle || m_FreeMode == FreeHandMode.Show) { m_FreeMode = FreeHandMode.Holding; m_FreeGrip = 1f; m_FreeTargetLocal = kFreeHandLocal; }
        }
    }

    public static class Ballistics
    {
        /// <summary>Launch velocity that lands on target at the given speed (low arc), or a straight lob when out of range.</summary>
        public static Vector3 SolveLaunch(Vector3 from, Vector3 to, float speed, bool lowArc)
        {
            var g = -Physics.gravity.y;
            var d = to - from;
            var flat = d.Flat();
            float x = flat.magnitude;
            float y = d.y;
            float v2 = speed * speed;
            float disc = v2 * v2 - g * (g * x * x + 2f * y * v2);
            if (x < 1e-3f) return d.normalized * speed;
            if (disc < 0f)
            {
                // out of range: 45° lob
                return (flat.normalized + Vector3.up).normalized * speed;
            }
            float root = Mathf.Sqrt(disc);
            float tan = (v2 + (lowArc ? -root : root)) / (g * x);
            float angle = Mathf.Atan(tan);
            return flat.normalized * (Mathf.Cos(angle) * speed) + Vector3.up * (Mathf.Sin(angle) * speed);
        }
    }
}
