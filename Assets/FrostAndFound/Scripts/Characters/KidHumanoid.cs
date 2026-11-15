using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>Forwards the Animator's IK pass (Unity only calls OnAnimatorIK on the Animator's own GameObject).</summary>
    public class KidIKRelay : MonoBehaviour
    {
        public KidAnimator Owner;
        void OnAnimatorIK(int layerIndex) { if (Owner) Owner.HumanoidIK(layerIndex); }
    }

    /// <summary>
    /// The rigged (Humanoid) side of KidAnimator, used by the team's Meshy kids. Locomotion comes from the Animator:
    /// idle → walk → run blended by how fast the kid really moves, with the playback pace matched so the feet don't
    /// skate. Everything the story asks for — looking at you, pointing, photo poses, ducking, sitting dazed, recoil —
    /// is layered on with Humanoid IK (hand and foot goals, body offsets), never by spinning bones, so elbows and knees
    /// bend where they should and the clothes ride the skeleton without twisting: hands get position goals only (the
    /// wrists keep the animation's roll) and the avatar shares arm/leg twist 50/50 between joints.
    /// Kids can also scoop a snowball out of the snow and throw it with the baseball-pitch clip.
    /// </summary>
    public partial class KidAnimator
    {
        // seconds into the pitch clip, measured from the right hand's path: 0–1 s is the set (skipped), the arm comes up
        // over the head at 1.1–1.5 s, peaks at 3.3 m/s at 1.6 s (release), follows through and is back at rest by ~2.9 s
        const float ThrowStart = 0.8f, ThrowRelease = 1.58f, ThrowEnd = 2.9f;
        // the pitch sweeps toward the body's left (the pitcher stands side-on), so turn that shoulder to the target
        const float ThrowYaw = 82f;

        bool m_Humanoid;
        HashSet<string> m_Params;
        float m_WalkSpeed = 0.75f, m_RunSpeed = 2f, m_Blend, m_Height = 1.05f, m_ArmLen = 0.4f;
        float m_Scoop, m_ThrowArms;
        Vector3 m_ScoopTarget, m_LookPos;
        float m_LookW;
        bool m_HasLook;
        Snowball m_Carry;
        Transform m_UpperArmL, m_UpperArmR, m_LowerArmR, m_FootL, m_FootR;

        public bool IsHumanoid => m_Humanoid;
        public bool IsThrowing { get; private set; }

        bool HasParam(string n) => m_Params != null && m_Params.Contains(n);

        void SetupHumanoid()
        {
            m_Humanoid = Animator && Animator.avatar && Animator.avatar.isHuman && Animator.runtimeAnimatorController;
            if (!m_Humanoid) return;
            Animator.gameObject.AddComponent<KidIKRelay>().Owner = this;
            m_Params = new HashSet<string>();
            foreach (var p in Animator.parameters) m_Params.Add(p.name);
            m_UpperArmL = Animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            m_UpperArmR = Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            m_LowerArmR = Animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            m_FootL = Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            m_FootR = Animator.GetBoneTransform(HumanBodyBones.RightFoot);
            m_Height = Rig.Height > 0.3f ? Rig.Height : 1.05f;   // what the model was fitted to (bones aren't posed yet at Start)
            if (m_UpperArmR && m_LowerArmR && Rig.HandR)
                m_ArmLen = Vector3.Distance(m_UpperArmR.position, m_LowerArmR.position) + Vector3.Distance(m_LowerArmR.position, Rig.HandR.position);
            // the walk and run cycles' natural ground speed on this body, so pace can match it
            float hs = Animator.humanScale * Animator.transform.lossyScale.y;
            foreach (var clip in Animator.runtimeAnimatorController.animationClips)
            {
                float v = clip.averageSpeed.magnitude * hs;
                if (v < 0.05f) continue;
                if (clip.name.Contains("Walk01")) m_WalkSpeed = v;
                else if (clip.name == "Run") m_RunSpeed = v;
            }
            if (m_RunSpeed < m_WalkSpeed * 1.5f) m_RunSpeed = m_WalkSpeed * 2.4f;
            Debug.Log($"[Frost] {name}: rigged kid, {m_Height:F2} m tall, walks {m_WalkSpeed:F2} m/s, runs {m_RunSpeed:F2} m/s");
        }

        /// <summary>0–1: the head rolls round in circles (dizzy after a snowball, Tony dazed). Set by KidFaces.</summary>
        public float Dizzy { get; set; }

        // ------------------------------------------------------------------ per frame (after the Animator)
        void HumanoidLate(float dt, float t)
        {
            float v = m_SpeedSmoothed, target, pace = 1f;
            if (v < 0.06f) target = 0f;
            else if (v <= m_WalkSpeed) { target = 1f; pace = Mathf.Max(0.55f, v / m_WalkSpeed); }
            else if (v <= m_RunSpeed) target = 1f + (v - m_WalkSpeed) / Mathf.Max(0.01f, m_RunSpeed - m_WalkSpeed);
            else { target = 2f; pace = Mathf.Min(1.6f, v / m_RunSpeed); }
            if (IsThrowing) target = 0f;
            m_Blend = Mathf.MoveTowards(m_Blend, target, dt * 4f);
            Animator.SetFloat("Speed", m_Blend);
            Animator.SetFloat("Pace", Mathf.Lerp(1f, pace, Mathf.Clamp01(m_Blend)));
            if (HasParam("Talk")) Animator.SetBool("Talk", Talking);

            // small touches on top of the solved pose, in world axes (independent of Meshy's bone orientations)
            // dizzy: the upper body sways and the head rolls round in a circle (the tilt axis spins about the vertical)
            var roll = Quaternion.identity;
            if (Dizzy > 0.01f)
            {
                float a = Time.time * 6.5f;
                var axis = Mathf.Cos(a) * transform.right + Mathf.Sin(a) * transform.forward;
                if (Rig.Spine && Rig.Spine != Rig.Head) Rig.Spine.rotation = Quaternion.AngleAxis(7f * Dizzy, axis) * Rig.Spine.rotation;
                roll = Quaternion.AngleAxis(20f * Dizzy, axis);
            }
            if (Rig.Head)
            {
                var q = roll * Quaternion.AngleAxis(Talking ? Mathf.Sin(t * 9f) * 3f : 0f, transform.right);
                if (m_Wobble > 0.01f)
                    q = Quaternion.AngleAxis(Mathf.Sin(t * 5f) * 12f * m_Wobble, transform.right) * Quaternion.AngleAxis(Mathf.Cos(t * 5f) * 14f * m_Wobble, transform.forward) * q;
                Rig.Head.rotation = q * Rig.Head.rotation;
            }
            // a snowball in hand sits in the right fist
            if (m_Carry && Rig.HandR)
            {
                var along = m_LowerArmR ? (Rig.HandR.position - m_LowerArmR.position).normalized : transform.forward;
                m_Carry.transform.position = Rig.HandR.position + along * 0.04f * m_Height;
            }
        }

        // ------------------------------------------------------------------ IK pass
        internal void HumanoidIK(int layer)
        {
            var a = Animator;
            if (!a) return;
            float dt = Time.deltaTime, t = Time.time + m_Seed, H = m_Height;
            Vector3 root = transform.position, fwd = transform.forward, right = transform.right, up = Vector3.up;

            // ---- look at you (head, a touch of chest), fading out when you're behind them or they're mid-throw
            if (LookTarget)
            {
                var want = LookTarget.position;
                m_LookPos = m_HasLook ? Vector3.Lerp(m_LookPos, want, 1f - Mathf.Exp(-6f * dt)) : want;
                m_HasLook = true;
                var toT = (want - root).Flat();
                float facing = toT.sqrMagnitude > 1e-4f ? Vector3.Dot(fwd, toT.normalized) : 1f;
                m_LookW = Mathf.MoveTowards(m_LookW, Mathf.InverseLerp(-0.4f, 0.2f, facing) * (1f - 0.8f * m_ThrowArms), dt * 3f);
            }
            else m_LookW = Mathf.MoveTowards(m_LookW, 0f, dt * 3f);
            if (m_HasLook)
            {
                a.SetLookAtWeight(m_LookW * 0.9f, 0.12f + 0.2f * m_Lean, 0.85f, 1f, 0.55f);
                a.SetLookAtPosition(m_LookPos + Vector3.down * 0.05f);
            }

            // ---- body: lower and tilt (duck, scoop, dive, sit, lean in, recoil) with the feet kept planted
            Vector3 lf = a.GetIKPosition(AvatarIKGoal.LeftFoot), rf = a.GetIKPosition(AvatarIKGoal.RightFoot);
            Quaternion lfr = a.GetIKRotation(AvatarIKGoal.LeftFoot), rfr = a.GetIKRotation(AvatarIKGoal.RightFoot);
            float bodyH = Mathf.Max(0.1f, a.bodyPosition.y - root.y);
            float lower = bodyH * (0.4f * m_Duck + 0.42f * m_Scoop + 0.3f * m_DiveWeight + 0.6f * m_Sit);
            float pitch = 26f * m_Duck + 34f * m_Scoop + 16f * m_Lean - 14f * m_HitKick - 8f * m_Sit;
            if (m_Pose == Pose.Flex) pitch -= 6f * m_PoseWeight;
            float roll = -42f * m_DiveSide * m_DiveWeight;
            a.bodyPosition = a.bodyPosition + Vector3.down * lower + right * (m_DiveSide * m_DiveWeight * 0.2f * H) - fwd * (0.05f * H * m_Sit);
            a.bodyRotation = Quaternion.AngleAxis(pitch, right) * Quaternion.AngleAxis(roll, fwd) * a.bodyRotation;
            float plant = Mathf.Clamp01(Mathf.Max(Mathf.Max(m_Duck, m_Scoop), Mathf.Max(m_DiveWeight, m_Sit)));
            if (plant > 0.001f)
            {
                // sitting in the snow: legs out in front; otherwise the feet stay where the animation put them
                var sitL = root + fwd * 0.34f * H - right * 0.09f * H + up * 0.04f * H;
                var sitR = root + fwd * 0.34f * H + right * 0.09f * H + up * 0.04f * H;
                Goal(a, AvatarIKGoal.LeftFoot, Vector3.Lerp(lf, sitL, m_Sit), lfr, plant);
                Goal(a, AvatarIKGoal.RightFoot, Vector3.Lerp(rf, sitR, m_Sit), rfr, plant);
                // knees bend forward, over the toes
                Hint(a, AvatarIKHint.LeftKnee, Vector3.Lerp(lf, a.bodyPosition, 0.5f) + fwd * 0.3f * H - right * 0.04f * H, plant * 0.7f);
                Hint(a, AvatarIKHint.RightKnee, Vector3.Lerp(rf, a.bodyPosition, 0.5f) + fwd * 0.3f * H + right * 0.04f * H, plant * 0.7f);
            }

            // ---- hands: position goals only (the wrists keep the animation's roll, so sleeves never twist)
            var head = Rig.Head ? Rig.Head.position : root + up * 0.9f * H;
            var chest = Rig.Spine ? Rig.Spine.position : root + up * 0.62f * H;
            var shL = m_UpperArmL ? m_UpperArmL.position : chest - right * 0.12f * H + up * 0.1f * H;
            var shR = m_UpperArmR ? m_UpperArmR.position : chest + right * 0.12f * H + up * 0.1f * H;
            var gl = shL; var gr = shR; float wl = 0f, wr = 0f;
            void Layer(ref Vector3 g, ref float w, Vector3 goal, float weight)
            {
                if (weight <= 0.001f) return;
                g = w <= 0.001f ? goal : Vector3.Lerp(g, goal, weight);
                w = Mathf.Max(w, weight);
            }
            // sitting dazed: hands down in the snow beside you
            Layer(ref gl, ref wl, root - right * 0.2f * H + fwd * 0.02f * H + up * 0.06f * H, m_Sit);
            Layer(ref gr, ref wr, root + right * 0.2f * H + fwd * 0.02f * H + up * 0.06f * H, m_Sit);
            // ducking: arms up over the head
            Layer(ref gl, ref wl, head + up * 0.1f * H - right * 0.07f * H + fwd * 0.07f * H, m_Duck);
            Layer(ref gr, ref wr, head + up * 0.1f * H + right * 0.07f * H + fwd * 0.07f * H, m_Duck);
            // photo poses
            if (m_Pose != Pose.None && m_PoseWeight > 0.001f)
            {
                PoseGoals(m_Pose, t, head, chest, shL, shR, root, fwd, right, H, out var pl, out var pr, out float pwl, out float pwr);
                Layer(ref gl, ref wl, pl, pwl * m_PoseWeight);
                Layer(ref gr, ref wr, pr, pwr * m_PoseWeight);
            }
            // pointing at something
            if (m_PointWeight > 0.001f)
            {
                if (m_PointRight) Layer(ref gr, ref wr, shR + m_PointDirWorld * m_ArmLen * 0.95f, m_PointWeight);
                else Layer(ref gl, ref wl, shL + m_PointDirWorld * m_ArmLen * 0.95f, m_PointWeight);
            }
            // scooping: both hands meet on the snow in front of your feet
            Layer(ref gl, ref wl, m_ScoopTarget - right * 0.05f * H, m_Scoop);
            Layer(ref gr, ref wr, m_ScoopTarget + right * 0.05f * H, m_Scoop);
            // mid-pitch the clip owns the arms
            wl *= 1f - m_ThrowArms; wr *= 1f - m_ThrowArms;

            Goal(a, AvatarIKGoal.LeftHand, gl, Quaternion.identity, wl, false);
            Goal(a, AvatarIKGoal.RightHand, gr, Quaternion.identity, wr, false);
            // elbows out and down, like a kid's
            Hint(a, AvatarIKHint.LeftElbow, shL - right * 0.18f * H - up * 0.2f * H - fwd * 0.05f * H, wl * 0.6f);
            Hint(a, AvatarIKHint.RightElbow, shR + right * 0.18f * H - up * 0.2f * H - fwd * 0.05f * H, wr * 0.6f);
        }

        void PoseGoals(Pose pose, float t, Vector3 head, Vector3 chest, Vector3 shL, Vector3 shR, Vector3 root, Vector3 fwd, Vector3 right, float H,
            out Vector3 l, out Vector3 r, out float wl, out float wr)
        {
            var up = Vector3.up;
            l = shL; r = shR; wl = 1f; wr = 1f;
            switch (pose)
            {
                case Pose.PeaceSigns:
                    l = head - right * 0.2f * H + up * 0.04f * H + fwd * 0.1f * H;
                    r = head + right * 0.2f * H + up * 0.04f * H + fwd * 0.1f * H;
                    break;
                case Pose.Flex:
                    l = shL - right * 0.12f * H + up * 0.2f * H + fwd * 0.03f * H + up * Mathf.Sin(t * 8f) * 0.01f * H;
                    r = shR + right * 0.12f * H + up * 0.2f * H + fwd * 0.03f * H;
                    break;
                case Pose.HairFlip:
                    l = head - right * 0.06f * H + up * 0.09f * H - fwd * 0.02f * H;
                    r = root + right * 0.17f * H + up * 0.5f * H + fwd * 0.02f * H;
                    break;
                case Pose.BlockLens:
                    var toLook = LookTarget ? (LookTarget.position - head).normalized : fwd;
                    r = head + toLook * 0.3f * H + right * 0.03f * H - up * 0.02f * H;
                    wl = 0f;
                    break;
                case Pose.HoldUpThermos:
                    r = shR + up * 0.42f * H + fwd * 0.12f * H + right * 0.03f * H;
                    wl = 0f;
                    break;
                case Pose.Cheer:
                    l = shL + up * (0.46f + Mathf.Sin(t * 10f) * 0.04f) * H - right * 0.1f * H + fwd * 0.04f * H;
                    r = shR + up * (0.46f - Mathf.Sin(t * 10f) * 0.04f) * H + right * 0.1f * H + fwd * 0.04f * H;
                    break;
                case Pose.ArmsCrossed:
                    l = chest + fwd * 0.15f * H + right * 0.08f * H - up * 0.03f * H;
                    r = chest + fwd * 0.17f * H - right * 0.08f * H - up * 0.01f * H;
                    break;
                case Pose.Shrug:
                    l = root + up * 0.52f * H - right * 0.3f * H + fwd * 0.14f * H;
                    r = root + up * 0.52f * H + right * 0.3f * H + fwd * 0.14f * H;
                    break;
                default: wl = wr = 0f; break;
            }
        }

        static void Goal(Animator a, AvatarIKGoal g, Vector3 pos, Quaternion rot, float w, bool useRot = true)
        {
            a.SetIKPositionWeight(g, w);
            a.SetIKRotationWeight(g, useRot ? w : 0f);
            if (w <= 0f) return;
            a.SetIKPosition(g, pos);
            if (useRot) a.SetIKRotation(g, rot);
        }

        static void Hint(Animator a, AvatarIKHint h, Vector3 pos, float w)
        {
            a.SetIKHintPositionWeight(h, w);
            if (w > 0f) a.SetIKHintPosition(h, pos);
        }

        // ------------------------------------------------------------------ snowballs
        /// <summary>Crouch, scoop a snowball out of the snow at your feet, stand and pitch it at target.</summary>
        public IEnumerator ScoopAndThrow(Kid kid, Vector3 target)
        {
            if (!m_Humanoid || IsThrowing) yield break;
            IsThrowing = true;
            var snow = SnowSurface.Instance;
            var from = kid.transform.rotation;
            var face = FrostUtil.FlatLookRotation(kid.transform.position, target);
            yield return Ease.Tween(0.3f, k => kid.transform.rotation = Quaternion.Slerp(from, face, k), Ease.OutCubic, false);

            // crouch and reach into the snow
            m_ScoopTarget = kid.transform.position + kid.transform.forward * 0.3f * m_Height;
            m_ScoopTarget.y = (snow ? snow.SurfaceHeightAt(m_ScoopTarget) : kid.transform.position.y) + 0.015f;
            yield return Ease.Tween(0.42f, k => m_Scoop = k, Ease.InOutCubic, false);
            if (snow && snow.HasSnowAt(m_ScoopTarget)) snow.Stamp(m_ScoopTarget, Random.Range(0f, 360f), StampShape.Scoop, 0.11f, 0.8f);
            Fx.SnowPuff(m_ScoopTarget + Vector3.up * 0.02f, Vector3.up, 10, 0.6f);
            AudioDirector.PlayAt(Sfx.ScoopCrunch, m_ScoopTarget, 0.7f, Random.Range(1.05f, 1.2f));
            m_Carry = Snowball.Create(Rig.HandR ? Rig.HandR.position : m_ScoopTarget, FrostTuning.I.snowballRadius * 0.85f);
            m_Carry.Thrower = kid;
            m_Carry.gameObject.SetLayerRecursive(Layers.Kid);
            yield return new WaitForSeconds(0.22f);   // pat it into shape
            yield return Ease.Tween(0.36f, k => m_Scoop = 1f - k, Ease.InOutCubic, false);
            m_Scoop = 0f;

            // the pitch: turn side-on, wind up, whip the arm over and let go at the top of the swing
            var sideOn = FrostUtil.FlatLookRotation(kid.transform.position, target) * Quaternion.Euler(0f, ThrowYaw, 0f);
            var squareOn = kid.transform.rotation;
            Animator.CrossFadeInFixedTime("Throw", 0.25f, 0, ThrowStart);
            float t0 = Time.time;
            StartCoroutine(Ease.Tween(0.35f, k => kid.transform.rotation = Quaternion.Slerp(squareOn, sideOn, k), Ease.InOutCubic, false));
            yield return Ease.Tween(0.2f, k => m_ThrowArms = k, Ease.Linear, false);
            while (Time.time - t0 < ThrowRelease - ThrowStart) yield return null;
            Release(kid, target);
            while (Time.time - t0 < ThrowEnd - ThrowStart) yield return null;
            Animator.CrossFadeInFixedTime("Locomotion", 0.35f, 0);
            var after = kid.transform.rotation;
            var back = FrostUtil.FlatLookRotation(kid.transform.position, target);
            yield return Ease.Tween(0.35f, k => { m_ThrowArms = 1f - k; kid.transform.rotation = Quaternion.Slerp(after, back, Ease.InOutCubic(k)); }, Ease.Linear, false);
            m_ThrowArms = 0f;
            IsThrowing = false;
        }

        void Release(Kid kid, Vector3 target)
        {
            var ball = m_Carry;
            m_Carry = null;
            if (!ball) return;
            var p = ball.transform.position;
            float dist = Vector3.Distance(p, target);
            float flight = Mathf.Clamp(dist / 6.5f, 0.3f, 0.85f);
            var v = (target - p - 0.5f * Physics.gravity * flight * flight) / flight * 1.02f;   // a touch long for the drag
            ball.Launch(v, Random.insideUnitSphere * 8f);
            Kid.NotifyThrew(kid, ball);
            // never hit yourself on the way out
            var bc = ball.GetComponent<Collider>();
            foreach (var c in kid.GetComponentsInChildren<Collider>()) if (c != bc) Physics.IgnoreCollision(bc, c, true);
        }
    }
}
