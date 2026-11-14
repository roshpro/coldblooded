using System.Collections;
using UnityEngine;

namespace FrostAndFound
{
    public enum Pose { None, PeaceSigns, Flex, HairFlip, BlockLens, HoldUpThermos, Cheer, ArmsCrossed, Shrug }

    /// <summary>
    /// The joints the game animates. The mockup builder creates them; a Blender model can provide the same
    /// names (Hips, Spine, Neck, Head, Arm_L, Arm_R, Leg_L, Leg_R, Hand_L, Hand_R) or simply be parented under
    /// Spine/Head and keep the procedural motion.
    /// </summary>
    public class KidRig
    {
        public Transform Visual, Hips, Spine, Neck, Head, ArmL, ArmR, HandL, HandR, LegL, LegR;
        public Transform EyeL, EyeR, Mouth, BubbleAnchor;
        public float HipHeight = 0.32f;
        public float Scale = 1f;
    }

    /// <summary>
    /// Procedural character motion for the mockups — gait bob and limb swing from velocity, lean, breathing idle,
    /// head-look, blinks, mouth flaps while talking, and one-shot actions (lean in & squint, point, poses, duck,
    /// dive, sit dazed, trip). If the character has an Animator (final model), the same calls also set its
    /// parameters so the art team can drive real clips.
    /// </summary>
    public class KidAnimator : MonoBehaviour
    {
        [System.NonSerialized] public KidRig Rig;
        public Animator Animator;          // optional, for final rigged models

        // live inputs
        public Vector3 Velocity;
        public Transform LookTarget;
        public bool Talking;

        // action overrides (0..1 weights)
        float m_Lean, m_Squint, m_Duck, m_Sit, m_DiveSide, m_DiveWeight, m_Wobble;
        Vector3 m_PointDirWorld; float m_PointWeight; bool m_PointRight = true;
        Pose m_Pose; float m_PoseWeight;
        float m_Phase, m_Blink, m_NextBlink, m_Mouth, m_HitKick;
        float m_SpeedSmoothed;
        Quaternion m_HeadLook = Quaternion.identity;
        Vector3 m_EyeScale = Vector3.one;
        float m_Seed;

        void Start()
        {
            m_Seed = Random.value * 10f;
            m_NextBlink = Time.time + Random.Range(1f, 4f);
            if (Rig.EyeL) m_EyeScale = Rig.EyeL.localScale;
        }

        // ------------------------------------------------------------------ one-shots
        public Coroutine LeanInAndSquint(float seconds) => StartCoroutine(LeanRoutine(seconds));
        IEnumerator LeanRoutine(float seconds)
        {
            yield return Ease.Tween(0.35f, t => { m_Lean = t; m_Squint = t; }, Ease.OutBack, false);
            yield return new WaitForSeconds(Mathf.Max(0f, seconds - 0.6f));
            yield return Ease.Tween(0.25f, t => { m_Lean = 1f - t; m_Squint = 1f - t; }, Ease.OutCubic, false);
        }

        public void SetSquint(float s) => m_Squint = s;

        public Coroutine Point(Vector3 worldTarget, float seconds, bool rightArm = true)
        {
            m_PointRight = rightArm;
            return StartCoroutine(PointRoutine(worldTarget, seconds));
        }
        IEnumerator PointRoutine(Vector3 target, float seconds)
        {
            var from = (Rig.ArmR ? Rig.ArmR.position : transform.position);
            m_PointDirWorld = (target - from).normalized;
            Trigger("Point");
            yield return Ease.Tween(0.25f, t => m_PointWeight = t, Ease.OutBack, false);
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                m_PointDirWorld = (target - (Rig.ArmR ? Rig.ArmR.position : transform.position)).normalized;
                yield return null;
            }
            yield return Ease.Tween(0.3f, t => m_PointWeight = 1f - t, Ease.OutCubic, false);
        }

        public Coroutine StrikePose(Pose pose, float seconds) => StartCoroutine(PoseRoutine(pose, seconds));
        IEnumerator PoseRoutine(Pose pose, float seconds)
        {
            m_Pose = pose;
            Trigger("Pose");
            yield return Ease.Tween(0.22f, t => m_PoseWeight = t, Ease.OutBack, false);
            yield return new WaitForSeconds(seconds);
            yield return Ease.Tween(0.3f, t => m_PoseWeight = 1f - t, Ease.OutCubic, false);
            m_Pose = Pose.None;
        }

        public void HitReact() { m_HitKick = 1f; Trigger("Hit"); }

        public Coroutine Duck(float seconds) => StartCoroutine(DuckRoutine(seconds));
        IEnumerator DuckRoutine(float seconds)
        {
            Trigger("Dodge");
            yield return Ease.Tween(0.12f, t => m_Duck = t, Ease.OutCubic, false);
            yield return new WaitForSeconds(seconds);
            yield return Ease.Tween(0.2f, t => m_Duck = 1f - t, Ease.OutCubic, false);
        }

        public Coroutine Dive(float side, float seconds) => StartCoroutine(DiveRoutine(side, seconds));
        IEnumerator DiveRoutine(float side, float seconds)
        {
            Trigger("Dodge");
            m_DiveSide = side;
            yield return Ease.Tween(0.15f, t => m_DiveWeight = t, Ease.OutCubic, false);
            yield return new WaitForSeconds(seconds);
            yield return Ease.Tween(0.25f, t => m_DiveWeight = 1f - t, Ease.OutBack, false);
        }

        public void SetSitting(bool dazed)
        {
            StopCoroutine(nameof(SitRoutine));
            StartCoroutine(SitRoutine(dazed));
            if (Animator) Animator.SetBool("Dazed", dazed);
        }
        IEnumerator SitRoutine(bool down)
        {
            float from = m_Sit, to = down ? 1f : 0f;
            yield return Ease.Tween(down ? 0.25f : 0.4f, t => { m_Sit = Mathf.Lerp(from, to, t); m_Wobble = m_Sit; }, down ? Ease.OutBounce : Ease.OutBack, false);
        }

        void Trigger(string name)
        {
            if (Animator && Animator.runtimeAnimatorController) Animator.SetTrigger(name);
        }

        // ------------------------------------------------------------------ per frame
        void LateUpdate()
        {
            if (Rig == null || !Rig.Hips) return;
            float dt = Time.deltaTime;
            float t = Time.time + m_Seed;
            var flatVel = Velocity.Flat();
            float speed = flatVel.magnitude;
            m_SpeedSmoothed = Ease.Damp(m_SpeedSmoothed, speed, 8f, dt);
            float speedN = Mathf.Clamp01(m_SpeedSmoothed / 1.6f);
            float stepHz = Mathf.Lerp(1.6f, 3.2f, speedN);
            if (speedN > 0.02f) m_Phase += dt * stepHz * Mathf.PI * 2f;
            if (Animator) Animator.SetFloat("Speed", m_SpeedSmoothed);
            m_HitKick = Mathf.Max(0f, m_HitKick - dt * 4f);

            float s = Rig.Scale;
            // ---------- hips: gait bob, breathing, duck, sit
            float bob = Mathf.Abs(Mathf.Sin(m_Phase)) * 0.035f * speedN;
            float breathe = Mathf.Sin(t * 2f * Mathf.PI / 2.2f) * 0.006f;
            float hipY = Rig.HipHeight + bob + breathe;
            hipY = Mathf.Lerp(hipY, Rig.HipHeight * 0.5f, m_Duck);
            hipY = Mathf.Lerp(hipY, 0.06f, m_Sit);
            Rig.Hips.localPosition = new Vector3(Mathf.Sin(m_Phase * 0.5f) * 0.01f * speedN + m_DiveSide * m_DiveWeight * 0.25f, hipY, 0f);
            float roll = Mathf.Sin(m_Phase) * 3f * speedN + m_DiveSide * m_DiveWeight * -60f;
            Rig.Hips.localRotation = Quaternion.Euler(0, 0, roll);

            // ---------- spine: lean into motion, lean in to look, hit recoil, squash on duck
            var localVel = transform.InverseTransformDirection(flatVel);
            float leanFwd = Mathf.Clamp(localVel.z * 7f, -8f, 12f) + m_Lean * 22f - m_HitKick * 18f + m_Duck * 25f + m_Sit * -8f;
            float leanSide = Mathf.Clamp(-localVel.x * 5f, -6f, 6f) + Mathf.Sin(t * 0.7f) * 1.2f * (1f - speedN);
            if (m_Pose == Pose.Flex) leanFwd -= 6f * m_PoseWeight;
            Rig.Spine.localRotation = Quaternion.Euler(leanFwd, 0, leanSide);
            float squash = 1f - m_HitKick * 0.08f;
            Rig.Spine.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));

            // ---------- head look (clamped, damped) + dazed wobble
            Quaternion want = Quaternion.identity;
            if (LookTarget && Rig.Neck)
            {
                var dir = LookTarget.position - Rig.Head.position;
                var local = Quaternion.Inverse(Rig.Neck.parent.rotation) * dir;
                if (local.sqrMagnitude > 1e-4f)
                {
                    var look = Quaternion.LookRotation(local.normalized, Vector3.up).eulerAngles;
                    float yaw = Mathf.DeltaAngle(0, look.y), pitch = Mathf.DeltaAngle(0, look.x);
                    want = Quaternion.Euler(Mathf.Clamp(pitch, -30f, 35f), Mathf.Clamp(yaw, -65f, 65f), 0);
                }
            }
            if (m_Wobble > 0.01f) want *= Quaternion.Euler(Mathf.Sin(t * 5f) * 12f * m_Wobble, 0, Mathf.Cos(t * 5f) * 14f * m_Wobble);
            m_HeadLook = Ease.Damp(m_HeadLook, want, 7f, dt);
            float talkNod = Talking ? Mathf.Sin(t * 9f) * 2.5f : 0f;
            if (Rig.Neck) Rig.Neck.localRotation = m_HeadLook * Quaternion.Euler(talkNod + m_Lean * 8f, 0, 0);

            // ---------- arms
            float swing = Mathf.Sin(m_Phase) * 32f * speedN;
            Quaternion armL = Quaternion.Euler(swing, 0, -8f - Mathf.Sin(t * 1.3f) * 2f);
            Quaternion armR = Quaternion.Euler(-swing, 0, 8f + Mathf.Sin(t * 1.1f) * 2f);
            if (m_Duck > 0.01f) { armL = Quaternion.Slerp(armL, Quaternion.Euler(-150f, 0, -30f), m_Duck); armR = Quaternion.Slerp(armR, Quaternion.Euler(-150f, 0, 30f), m_Duck); }
            if (m_Sit > 0.01f) { armL = Quaternion.Slerp(armL, Quaternion.Euler(-20f, 0, -55f), m_Sit); armR = Quaternion.Slerp(armR, Quaternion.Euler(-20f, 0, 55f), m_Sit); }
            ApplyPose(ref armL, ref armR, t);
            if (m_PointWeight > 0.001f)
            {
                var arm = m_PointRight ? Rig.ArmR : Rig.ArmL;
                if (arm && arm.parent)
                {
                    var localDir = arm.parent.InverseTransformDirection(m_PointDirWorld);
                    var pointRot = Quaternion.FromToRotation(Vector3.down, localDir);
                    if (m_PointRight) armR = Quaternion.Slerp(armR, pointRot, m_PointWeight);
                    else armL = Quaternion.Slerp(armL, pointRot, m_PointWeight);
                }
            }
            if (Rig.ArmL) Rig.ArmL.localRotation = armL;
            if (Rig.ArmR) Rig.ArmR.localRotation = armR;

            // ---------- legs: alternate swing, tuck on duck, forward when sitting
            float legSwing = Mathf.Sin(m_Phase) * 30f * speedN;
            var legL = Quaternion.Euler(legSwing, 0, 0);
            var legR = Quaternion.Euler(-legSwing, 0, 0);
            if (m_Sit > 0.01f) { legL = Quaternion.Slerp(legL, Quaternion.Euler(-82f, -12f, 0), m_Sit); legR = Quaternion.Slerp(legR, Quaternion.Euler(-82f, 12f, 0), m_Sit); }
            if (m_Duck > 0.01f) { legL = Quaternion.Slerp(legL, Quaternion.Euler(-60f, 0, 0), m_Duck * 0.7f); legR = Quaternion.Slerp(legR, Quaternion.Euler(-60f, 0, 0), m_Duck * 0.7f); }
            if (Rig.LegL) Rig.LegL.localRotation = legL;
            if (Rig.LegR) Rig.LegR.localRotation = legR;

            // ---------- face: blink, squint, mouth
            if (Time.time > m_NextBlink) { m_Blink = 1f; m_NextBlink = Time.time + Random.Range(2f, 5f); }
            m_Blink = Mathf.Max(0f, m_Blink - dt * 9f);
            float eyeOpen = Mathf.Lerp(1f, 0.25f, m_Squint) * (1f - Ease.Bump(m_Blink) * 0.9f);
            if (m_Wobble > 0.5f) eyeOpen *= 0.5f;
            if (Rig.EyeL) Rig.EyeL.localScale = new Vector3(m_EyeScale.x, m_EyeScale.y * eyeOpen, m_EyeScale.z);
            if (Rig.EyeR) Rig.EyeR.localScale = new Vector3(m_EyeScale.x, m_EyeScale.y * eyeOpen, m_EyeScale.z);
            float mouthTarget = Talking ? 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 13f) * Mathf.Sin(t * 5.3f + 1f)) : 0.15f;
            m_Mouth = Ease.Damp(m_Mouth, mouthTarget, 20f, dt);
            if (Rig.Mouth) Rig.Mouth.localScale = new Vector3(Rig.Mouth.localScale.x, Mathf.Lerp(0.012f, 0.05f, m_Mouth) * s / Mathf.Max(s, 0.001f), Rig.Mouth.localScale.z);
            if (Animator) Animator.SetBool("Talk", Talking);
        }

        void ApplyPose(ref Quaternion armL, ref Quaternion armR, float t)
        {
            if (m_Pose == Pose.None || m_PoseWeight <= 0.001f) return;
            Quaternion l = armL, r = armR;
            switch (m_Pose)
            {
                case Pose.PeaceSigns: l = Quaternion.Euler(-160f, 0, -25f); r = Quaternion.Euler(-160f, 0, 25f); break;
                case Pose.Flex: l = Quaternion.Euler(-90f, 0, -80f) * Quaternion.Euler(0, Mathf.Sin(t * 8f) * 5f, 0); r = Quaternion.Euler(-90f, 0, 80f); break;
                case Pose.HairFlip: l = Quaternion.Euler(-150f, 0, -60f); r = Quaternion.Euler(10f, 0, 30f); break;
                case Pose.BlockLens: r = Quaternion.Euler(-95f, 0, 10f); break;
                case Pose.HoldUpThermos: r = Quaternion.Euler(-130f, 0, 15f); break;
                case Pose.Cheer: l = Quaternion.Euler(-170f, 0, -20f + Mathf.Sin(t * 10f) * 15f); r = Quaternion.Euler(-170f, 0, 20f - Mathf.Sin(t * 10f) * 15f); break;
                case Pose.ArmsCrossed: l = Quaternion.Euler(-70f, 0, 55f); r = Quaternion.Euler(-60f, 0, -55f); break;
                case Pose.Shrug: l = Quaternion.Euler(-30f, 0, -65f); r = Quaternion.Euler(-30f, 0, 65f); break;
            }
            armL = Quaternion.Slerp(armL, l, m_PoseWeight);
            armR = Quaternion.Slerp(armR, r, m_PoseWeight);
        }
    }
}
