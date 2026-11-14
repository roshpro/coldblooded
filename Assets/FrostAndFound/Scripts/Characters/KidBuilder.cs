using TMPro;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Builds each suspect as a chunky, big-headed (~1 m) mockup out of primitives, faithful to the look in the
    /// design doc and colour-coded to their clue. Replace with a Blender model via FrostAssets ▸ Character Models.
    /// </summary>
    public static class KidBuilder
    {
        // shared proportions (metres, before per-kid scale)
        const float HipY = 0.32f, TorsoH = 0.36f, HeadR = 0.2f;

        public static KidRig Build(Kid kid, GameObject customModel)
        {
            var id = kid.id;
            var rig = new KidRig { Scale = Cast.Scale(id), HipHeight = HipY };
            var visual = new GameObject("Visual").transform;
            visual.SetParent(kid.transform, false);
            visual.gameObject.layer = Layers.Kid;
            visual.localScale = Vector3.one * rig.Scale;
            rig.Visual = visual;

            GameObject custom = null;
            if (customModel)
            {
                custom = Object.Instantiate(customModel, visual, false);
                custom.name = "Model";
                custom.SetLayerRecursive(Layers.Kid);
                var anim = custom.GetComponentInChildren<Animator>();
                if (anim && anim.avatar && anim.avatar.isHuman)
                {
                    BindHumanoid(rig, custom.transform, anim, FrostAssets.SlotFor(id));
                    return rig;
                }
            }

            // joints
            rig.Hips = ProcMesh.Pivot("Hips", visual, new Vector3(0, HipY, 0));
            rig.Spine = ProcMesh.Pivot("Spine", rig.Hips, new Vector3(0, 0.02f, 0));
            rig.Neck = ProcMesh.Pivot("Neck", rig.Spine, new Vector3(0, TorsoH, 0));
            rig.Head = ProcMesh.Pivot("Head", rig.Neck, new Vector3(0, 0.02f, 0));
            rig.ArmL = ProcMesh.Pivot("Arm_L", rig.Spine, new Vector3(-0.17f, TorsoH - 0.07f, 0));
            rig.ArmR = ProcMesh.Pivot("Arm_R", rig.Spine, new Vector3(0.17f, TorsoH - 0.07f, 0));
            rig.HandL = ProcMesh.Pivot("Hand_L", rig.ArmL, new Vector3(0, -0.27f, 0.01f));
            rig.HandR = ProcMesh.Pivot("Hand_R", rig.ArmR, new Vector3(0, -0.27f, 0.01f));
            rig.LegL = ProcMesh.Pivot("Leg_L", rig.Hips, new Vector3(-0.08f, 0, 0));
            rig.LegR = ProcMesh.Pivot("Leg_R", rig.Hips, new Vector3(0.08f, 0, 0));
            rig.BubbleAnchor = ProcMesh.Pivot("BubbleAnchor", rig.Head, new Vector3(0, HeadR * 2f + 0.28f, 0));

            if (custom)
            {
                BindCustom(rig, custom.transform);
                return rig;
            }

            switch (id)
            {
                case CastId.Dexter: Dexter(rig); break;
                case CastId.Sherman: Sherman(rig); break;
                case CastId.Serena: Serena(rig); break;
                case CastId.Lucy: Lucy(rig); break;
                case CastId.Tony: Tony(rig); break;
            }
            MeshMerger.Merge(visual);
            return rig;
        }

        /// <summary>A rigged Humanoid kid (the team's Meshy models): the Animator drives the body; the rig's joints
        /// are the real bones, so everything that attaches to a head or hand follows the animation.</summary>
        static void BindHumanoid(KidRig rig, Transform model, Animator anim, CharacterModelSlot slot)
        {
            if (slot != null && slot.animator) anim.runtimeAnimatorController = slot.animator;
            anim.applyRootMotion = false;   // the game walks the kid; the clips animate the body in place
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr)
            {
                // our kids stand ~1.05 m tall (Visual carries each character's scale on top)
                float h = smr.bounds.size.y / Mathf.Max(1e-4f, rig.Visual.lossyScale.y);
                float want = 1.05f * (slot != null && slot.scale > 0f ? slot.scale : 1f);
                if (h > 0.05f) model.localScale *= want / h;
                rig.Height = want * rig.Visual.lossyScale.y;
                smr.sharedMaterial = Palette.SoftTextured(slot != null ? slot.texture : null, "Kid_" + model.root.name);
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.receiveShadows = true;
                smr.updateWhenOffscreen = true;
            }
            Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);
            rig.Hips = B(HumanBodyBones.Hips);
            rig.Spine = B(HumanBodyBones.Chest) ?? B(HumanBodyBones.Spine) ?? rig.Hips;
            rig.Neck = B(HumanBodyBones.Neck) ?? rig.Spine;
            rig.Head = B(HumanBodyBones.Head) ?? rig.Neck;
            rig.ArmL = B(HumanBodyBones.LeftUpperArm);
            rig.ArmR = B(HumanBodyBones.RightUpperArm);
            rig.HandL = B(HumanBodyBones.LeftHand);
            rig.HandR = B(HumanBodyBones.RightHand);
            rig.LegL = B(HumanBodyBones.LeftUpperLeg);
            rig.LegR = B(HumanBodyBones.RightUpperLeg);
            rig.HipHeight = rig.Hips ? rig.Visual.InverseTransformPoint(rig.Hips.position).y : HipY;
            var bubble = new GameObject("BubbleAnchor").transform;
            bubble.SetParent(rig.Head, false);
            bubble.position = rig.Head.position + Vector3.up * 0.3f * rig.Visual.lossyScale.y;
            rig.BubbleAnchor = bubble;
        }

        static void BindCustom(KidRig rig, Transform model)
        {
            // adopt named joints from the model if present; otherwise keep the procedural pivots
            Transform F(string n) { foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
            rig.Head = F("Head") ?? rig.Head;
            rig.HandL = F("Hand_L") ?? rig.HandL;
            rig.HandR = F("Hand_R") ?? rig.HandR;
            rig.EyeL = F("Eye_L"); rig.EyeR = F("Eye_R"); rig.Mouth = F("Mouth");
        }

        // ================================================================== shared body parts
        static Material M(Color c, bool outline = true, float rim = 0.25f) => Palette.Toon(c, outline, rim);

        static GameObject P(string n, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default)
            => ProcMesh.Part(n, parent, mesh, mat, pos, scale, euler);

        static Mesh Ball => ProcMesh.Sphere(16, 12);
        static Mesh LowBall => ProcMesh.Sphere(10, 8);

        /// <summary>Big round head with a face. Returns the head-shell transform for hats/hair.</summary>
        static Transform HeadWithFace(KidRig rig, Color skin, bool bigEyes = false, bool bunnyTeeth = false, bool freckles = false)
        {
            var h = rig.Head;
            P("Head", h, Ball, M(skin, true, 0.3f), new Vector3(0, HeadR, 0), new Vector3(HeadR * 2.05f, HeadR * 1.95f, HeadR * 1.9f));
            float ey = HeadR + 0.015f, ez = HeadR * 0.9f;
            float es = bigEyes ? 0.062f : 0.05f;
            var eyeMat = M(Palette.Ink, false, 0.6f);
            rig.EyeL = P("Eye_L", h, LowBall, eyeMat, new Vector3(-0.068f, ey, ez), new Vector3(es, es * 1.25f, es * 0.6f)).transform;
            rig.EyeR = P("Eye_R", h, LowBall, eyeMat, new Vector3(0.068f, ey, ez), new Vector3(es, es * 1.25f, es * 0.6f)).transform;
            var shine = Palette.Unlit(Palette.Paper);
            P("Shine", rig.EyeL, LowBall, shine, new Vector3(0.18f, 0.22f, 0.45f), Vector3.one * 0.32f);
            P("Shine", rig.EyeR, LowBall, shine, new Vector3(0.18f, 0.22f, 0.45f), Vector3.one * 0.32f);
            P("Nose", h, LowBall, M(Color.Lerp(skin, Palette.Cheek, 0.35f), false), new Vector3(0, HeadR - 0.035f, HeadR * 0.97f), new Vector3(0.045f, 0.035f, 0.035f));
            var cheek = M(Color.Lerp(skin, Palette.Cheek, 0.75f), false, 0.1f);
            P("Cheek_L", h, LowBall, cheek, new Vector3(-0.11f, HeadR - 0.05f, HeadR * 0.82f), new Vector3(0.05f, 0.03f, 0.02f), new Vector3(0, -30, 0));
            P("Cheek_R", h, LowBall, cheek, new Vector3(0.11f, HeadR - 0.05f, HeadR * 0.82f), new Vector3(0.05f, 0.03f, 0.02f), new Vector3(0, 30, 0));
            rig.Mouth = P("Mouth", h, ProcMesh.RoundedBox(0.45f, 2), M(Palette.Coal, false, 0f), new Vector3(0, HeadR - 0.085f, HeadR * 0.9f), new Vector3(0.05f, 0.02f, 0.02f)).transform;
            if (bunnyTeeth)
            {
                var tooth = M(Palette.Paper, true, 0.2f);
                P("Tooth_L", rig.Head, ProcMesh.RoundedBox(0.3f, 2), tooth, new Vector3(-0.011f, HeadR - 0.1f, HeadR * 0.93f), new Vector3(0.02f, 0.028f, 0.01f));
                P("Tooth_R", rig.Head, ProcMesh.RoundedBox(0.3f, 2), tooth, new Vector3(0.011f, HeadR - 0.1f, HeadR * 0.93f), new Vector3(0.02f, 0.028f, 0.01f));
            }
            if (freckles)
            {
                var fr = M(Color.Lerp(skin, Palette.Cocoa, 0.4f), false, 0f);
                for (int i = 0; i < 3; i++)
                {
                    P("Freckle", h, LowBall, fr, new Vector3(-0.09f + i * 0.012f, HeadR - 0.03f - i * 0.006f, HeadR * 0.9f), Vector3.one * 0.009f);
                    P("Freckle", h, LowBall, fr, new Vector3(0.09f - i * 0.012f, HeadR - 0.03f - i * 0.006f, HeadR * 0.9f), Vector3.one * 0.009f);
                }
            }
            return h;
        }

        static void Torso(KidRig rig, Color coat, float puff = 1f, bool flare = false, Color? flareColor = null)
        {
            var s = rig.Spine;
            P("Torso", s, Ball, M(coat, true, 0.3f), new Vector3(0, TorsoH * 0.5f, 0), new Vector3(0.36f * puff, TorsoH * 1.05f, 0.3f * puff));
            P("Collar", s, ProcMesh.Torus(0.4f, 0.12f, 18, 8), M(Color.Lerp(coat, Palette.Paper, 0.15f), true), new Vector3(0, TorsoH - 0.01f, 0), new Vector3(0.24f, 0.3f, 0.21f));
            if (flare)
            {
                // a puffy coat that flares like a dress
                P("Flare", s, ProcMesh.Cylinder(18, 0.55f), M(flareColor ?? coat, true, 0.3f), new Vector3(0, 0.02f, 0), new Vector3(0.48f, 0.2f, 0.42f));
            }
        }

        static void Arms(KidRig rig, Color sleeve, Color mitten, float thickness = 1f, Material mittenMat = null)
        {
            foreach (var arm in new[] { rig.ArmL, rig.ArmR })
            {
                P("Sleeve", arm, ProcMesh.Capsule(0.25f, 1f), M(sleeve, true), new Vector3(0, -0.12f, 0), new Vector3(0.11f * thickness, 0.26f, 0.11f * thickness));
                var hand = arm == rig.ArmL ? rig.HandL : rig.HandR;
                P("Mitten", hand, Ball, mittenMat ?? M(mitten, true), Vector3.zero, new Vector3(0.085f, 0.095f, 0.08f) * Mathf.Max(1f, thickness * 0.9f));
                P("Thumb", hand, LowBall, mittenMat ?? M(mitten, true), new Vector3(arm == rig.ArmL ? 0.03f : -0.03f, 0.01f, 0.03f), new Vector3(0.035f, 0.045f, 0.035f));
            }
        }

        static void Legs(KidRig rig, Color pants, Color boot, float thickness = 1f)
        {
            foreach (var leg in new[] { rig.LegL, rig.LegR })
            {
                P("Leg", leg, ProcMesh.Capsule(0.25f, 1f), M(pants, true), new Vector3(0, -0.15f, 0), new Vector3(0.12f * thickness, 0.3f, 0.12f * thickness));
                P("Boot", leg, ProcMesh.RoundedBox(0.35f, 3), M(boot, true, 0.3f), new Vector3(0, -HipY + 0.045f, 0.03f), new Vector3(0.12f * thickness, 0.09f, 0.17f));
            }
        }

        static Transform Hat(KidRig rig) => ProcMesh.Pivot("Hat", rig.Head, new Vector3(0, HeadR * 1.75f, 0));

        static void Beanie(KidRig rig, Color c, Color pom, float pomSize = 0.09f)
        {
            var h = rig.Head;
            P("Beanie", h, ProcMesh.Sphere(16, 10), M(c, true), new Vector3(0, HeadR * 1.3f, -0.01f), new Vector3(HeadR * 2.12f, HeadR * 1.35f, HeadR * 2.05f));
            P("Cuff", h, ProcMesh.Torus(0.47f, 0.09f, 20, 8), M(Color.Lerp(c, Palette.Ink, 0.15f), true), new Vector3(0, HeadR * 1.18f, -0.005f), new Vector3(HeadR * 2.1f, 0.16f, HeadR * 2.05f), new Vector3(-6, 0, 0));
            P("Pom", h, ProcMesh.Sphere(12, 9, 0.12f, 3), M(pom, true), new Vector3(0, HeadR * 2.02f + pomSize * 0.3f, -0.02f), Vector3.one * pomSize);
        }

        // ================================================================== the suspects

        /// <summary>Dexter Park, 9 — apron reading DEXTER'S COCOA 25¢, steaming thermos, cocoa-brown mittens. A tiny salesman.</summary>
        static void Dexter(KidRig rig)
        {
            var skin = Palette.SkinB;
            Torso(rig, Palette.Gold * 0.85f + Palette.DexterBrown * 0.15f);
            Arms(rig, Palette.Gold * 0.85f + Palette.DexterBrown * 0.15f, Palette.Cocoa);
            Legs(rig, Palette.InkSoft, Palette.Boot);
            HeadWithFace(rig, skin, freckles: true);
            Beanie(rig, Palette.DexterBrown, Palette.Apron, 0.08f);
            // hair tufts peeking out
            var hair = M(Palette.Hair, true);
            P("Tuft", rig.Head, LowBall, hair, new Vector3(-0.17f, HeadR * 1.05f, 0.05f), new Vector3(0.06f, 0.05f, 0.08f));
            P("Tuft", rig.Head, LowBall, hair, new Vector3(0.17f, HeadR * 1.05f, 0.05f), new Vector3(0.06f, 0.05f, 0.08f));
            // the apron, with the sales pitch on it
            var apronMat = M(Palette.Apron, true, 0.15f);
            var apron = P("Apron", rig.Spine, ProcMesh.RoundedBox(0.25f, 2), apronMat, new Vector3(0, TorsoH * 0.42f, 0.135f), new Vector3(0.28f, 0.36f, 0.05f), new Vector3(-6, 0, 0));
            P("Apron Strap", rig.Spine, ProcMesh.Torus(0.45f, 0.06f, 16, 6), apronMat, new Vector3(0, TorsoH * 0.82f, 0.0f), new Vector3(0.3f, 0.3f, 0.25f), new Vector3(-12, 0, 0));
            var label = UIStyle.WorldText("Apron Text", rig.Spine, "DEXTER'S\n<size=165%>COCOA</size>\n<size=130%>25¢</size>", UIStyle.Role.Apron, 0.3f, new Vector2(0.27f, 0.32f), Palette.Cocoa);
            label.transform.localPosition = new Vector3(0, TorsoH * 0.44f, 0.164f);
            label.transform.localRotation = Quaternion.Euler(-6f, 180f, 0);
            label.lineSpacing = -28f;
            label.characterSpacing = 0f;
            // leaking thermos (right hand) — the drips come from here
            var thermos = ProcMesh.Pivot("Thermos", rig.HandR, new Vector3(0, -0.02f, 0.06f), new Vector3(0, 0, 0));
            P("Bottle", thermos, ProcMesh.Cylinder(14), M(Palette.SledRed * 0.4f + Palette.DexterBrown * 0.6f, true, 0.4f), new Vector3(0, 0.02f, 0), new Vector3(0.07f, 0.18f, 0.07f));
            P("Band", thermos, ProcMesh.Cylinder(14), M(Palette.Apron, true), new Vector3(0, 0.03f, 0), new Vector3(0.074f, 0.03f, 0.074f));
            P("Cup Lid", thermos, ProcMesh.Cylinder(14, 1.15f), M(Palette.CameraBody, true, 0.4f), new Vector3(0, 0.13f, 0), new Vector3(0.068f, 0.05f, 0.068f));
            var steam = ProcMesh.Pivot("Steam", thermos, new Vector3(0, 0.17f, 0));
            ThermosSteam.Attach(steam);
        }

        /// <summary>Sherman Barnes, 7 — huge white snowsuit with orange stains, glasses, bunny-ear hat, bunny teeth, stack of books.</summary>
        static void Sherman(KidRig rig)
        {
            var skin = Palette.SkinE;
            Torso(rig, Palette.ShermanSuit, 1.18f);
            Arms(rig, Palette.ShermanSuit, Palette.ShermanBlue, 1.25f);
            Legs(rig, Palette.ShermanSuit, Palette.ShermanBlue, 1.25f);
            HeadWithFace(rig, skin, bigEyes: true, bunnyTeeth: true);
            // hat with bunny ears (Sherman's colour: blue)
            var blue = M(Palette.ShermanBlue, true);
            P("Hat", rig.Head, ProcMesh.Sphere(16, 10), blue, new Vector3(0, HeadR * 1.3f, -0.01f), new Vector3(HeadR * 2.14f, HeadR * 1.4f, HeadR * 2.08f));
            foreach (var side in new[] { -1f, 1f })
            {
                var ear = ProcMesh.Pivot("Ear", rig.Head, new Vector3(0.075f * side, HeadR * 1.85f, -0.01f), new Vector3(-6, 0, 12 * side));
                P("Ear", ear, ProcMesh.Capsule(0.25f, 1f), blue, new Vector3(0, 0.13f, 0), new Vector3(0.075f, 0.28f, 0.04f));
                P("Ear Inner", ear, ProcMesh.Capsule(0.25f, 1f), M(Palette.Cheek, false), new Vector3(0, 0.13f, 0.014f), new Vector3(0.042f, 0.22f, 0.02f));
            }
            // glasses
            var frame = M(Palette.Ink, false, 0.4f);
            foreach (var side in new[] { -1f, 1f })
            {
                P("Lens Ring", rig.Head, ProcMesh.Torus(0.42f, 0.07f, 20, 6), frame, new Vector3(0.068f * side, HeadR + 0.015f, HeadR * 0.98f), new Vector3(0.1f, 0.1f, 0.1f), new Vector3(90, 0, 0));
                P("Lens", rig.Head, ProcMesh.Disc(18), Palette.Toon(Palette.SkyHorizon, false, 0.9f, 0.05f), new Vector3(0.068f * side, HeadR + 0.015f, HeadR * 0.985f), new Vector3(0.08f, 0.002f, 0.08f), new Vector3(90, 0, 0));
                P("Arm", rig.Head, ProcMesh.RoundedBox(0.3f, 1), frame, new Vector3(0.115f * side, HeadR + 0.02f, HeadR * 0.6f), new Vector3(0.008f, 0.008f, 0.16f), new Vector3(0, 8 * side, 0));
            }
            P("Bridge", rig.Head, ProcMesh.RoundedBox(0.3f, 1), frame, new Vector3(0, HeadR + 0.025f, HeadR * 1.0f), new Vector3(0.04f, 0.008f, 0.008f));
            // orange carrot stains on the snowsuit (they glint with the carrot in step 2)
            var stain = M(Palette.Carrot, false, 0.1f);
            P("Stain", rig.Spine, ProcMesh.Sphere(10, 8), stain, new Vector3(0.05f, TorsoH * 0.42f, 0.17f), new Vector3(0.07f, 0.05f, 0.015f), new Vector3(0, 0, 20));
            P("Stain", rig.Spine, ProcMesh.Sphere(10, 8), stain, new Vector3(-0.06f, TorsoH * 0.3f, 0.165f), new Vector3(0.045f, 0.035f, 0.012f));
            P("Stain", rig.Spine, ProcMesh.Sphere(10, 8), stain, new Vector3(0.0f, TorsoH * 0.6f, 0.15f), new Vector3(0.03f, 0.025f, 0.01f));
            // stack of books (left arm, hugged)
            var books = ProcMesh.Pivot("Books", rig.HandL, new Vector3(0.05f, 0.02f, 0.06f), new Vector3(0, 20, 0));
            Color[] cols = { Palette.Books1, Palette.Books2, Palette.Books3 };
            for (int i = 0; i < 3; i++)
            {
                P("Book", books, ProcMesh.RoundedBox(0.08f, 1), M(cols[i], true), new Vector3(0.005f * i, i * 0.042f, 0), new Vector3(0.12f, 0.04f, 0.16f), new Vector3(0, i * 7f - 6f, 0));
                P("Pages", books, ProcMesh.RoundedBox(0.05f, 1), M(Palette.Paper, false), new Vector3(0.005f * i + 0.008f, i * 0.042f, 0), new Vector3(0.112f, 0.03f, 0.152f), new Vector3(0, i * 7f - 6f, 0));
            }
            // the carrot — Frosty's nose, with bite marks
            var carrot = ProcMesh.Pivot("Carrot", rig.HandR, new Vector3(0, 0.01f, 0.05f), new Vector3(70, 0, 0));
            P("Carrot", carrot, ProcMesh.Cylinder(10, 0.25f), M(Palette.Carrot, true, 0.35f), new Vector3(0, 0.06f, 0), new Vector3(0.05f, 0.15f, 0.05f));
            P("Bite", carrot, ProcMesh.Disc(10), M(Palette.Books3, false), new Vector3(0, -0.015f, 0), new Vector3(0.052f, 0.004f, 0.052f));
            for (int i = 0; i < 3; i++)
                P("Bite Mark", carrot, LowBall, M(Palette.Apron, false), new Vector3(Mathf.Cos(i * 2.1f) * 0.022f, -0.012f, Mathf.Sin(i * 2.1f) * 0.022f), Vector3.one * 0.014f);
            P("Leaf", carrot, ProcMesh.Cone(6), M(Palette.CarrotLeaf, true), new Vector3(0.008f, -0.04f, 0), new Vector3(0.02f, 0.06f, 0.02f), new Vector3(180, 0, 10));
            P("Leaf", carrot, ProcMesh.Cone(6), M(Palette.CarrotLeaf, true), new Vector3(-0.008f, -0.04f, 0), new Vector3(0.02f, 0.06f, 0.02f), new Vector3(180, 0, -14));
        }

        /// <summary>Serena Pruitt, 10 — purple coat, an enormous pom-pom hat, mittens caked in purple glitter, pink handbag.</summary>
        static void Serena(KidRig rig)
        {
            var skin = Palette.SkinA;
            var glitterMitten = Palette.Toon(Palette.Glitter, true, 0.4f);
            var gm = new Material(glitterMitten) { name = "GlitterMitten" };
            gm.SetFloat("_Sparkle", 1f); gm.SetFloat("_SparkleScale", 180f); gm.SetColor("_SparkleColor", Palette.GlintWhite);
            Torso(rig, Palette.SerenaPurple, 1.05f);
            Arms(rig, Palette.SerenaPurple, Palette.Glitter, 1f, gm);
            Legs(rig, Palette.SerenaPurpleDark, Palette.PinkBag);
            HeadWithFace(rig, skin);
            // long hair behind and a side-swept fringe
            var hair = M(Palette.HairBrown, true);
            P("Hair Back", rig.Head, ProcMesh.Capsule(0.25f, 1f), hair, new Vector3(0, HeadR * 0.65f, -0.12f), new Vector3(0.36f, 0.4f, 0.18f));
            P("Hair L", rig.Head, ProcMesh.Capsule(0.25f, 1f), hair, new Vector3(-0.17f, HeadR * 0.6f, -0.02f), new Vector3(0.1f, 0.36f, 0.14f), new Vector3(0, 0, 8));
            P("Hair R", rig.Head, ProcMesh.Capsule(0.25f, 1f), hair, new Vector3(0.17f, HeadR * 0.6f, -0.02f), new Vector3(0.1f, 0.36f, 0.14f), new Vector3(0, 0, -8));
            // the enormous pom-pom hat
            Beanie(rig, Palette.SerenaPurple, Palette.PinkBag, 0.24f);
            // lashes
            P("Lash", rig.EyeL, ProcMesh.RoundedBox(0.3f, 1), M(Palette.Ink, false), new Vector3(-0.35f, 0.55f, 0.2f), new Vector3(0.6f, 0.18f, 0.3f), new Vector3(0, 0, 25));
            P("Lash", rig.EyeR, ProcMesh.RoundedBox(0.3f, 1), M(Palette.Ink, false), new Vector3(0.35f, 0.55f, 0.2f), new Vector3(0.6f, 0.18f, 0.3f), new Vector3(0, 0, -25));
            // pink handbag on the left arm
            var bag = ProcMesh.Pivot("Handbag", rig.HandL, new Vector3(0.0f, -0.09f, 0.0f));
            P("Bag", bag, ProcMesh.RoundedBox(0.3f, 3), M(Palette.PinkBag, true, 0.4f), Vector3.zero, new Vector3(0.15f, 0.11f, 0.06f));
            P("Strap", bag, ProcMesh.Torus(0.4f, 0.05f, 16, 6), M(Palette.PinkBag * 0.8f + Palette.Paper * 0.2f, true), new Vector3(0, 0.07f, 0), new Vector3(0.12f, 0.12f, 0.12f), new Vector3(90, 0, 0));
            P("Clasp", bag, LowBall, M(Palette.Gold, false, 0.6f), new Vector3(0, 0.03f, 0.032f), Vector3.one * 0.018f);
        }

        /// <summary>Lucy Whitmore, 11 — black bob, oversized deerstalker, bright blue puffy coat that flares like a dress, white tights, snow boots, magnifying glass, suspect notebook.</summary>
        static void Lucy(KidRig rig)
        {
            var skin = Palette.SkinD;
            Torso(rig, Palette.LucyBlue, 1.02f, true, Palette.LucyBlue);
            Arms(rig, Palette.LucyBlue, Palette.LucyBlueDark);
            Legs(rig, Palette.Tights, Palette.Boot, 0.85f);
            HeadWithFace(rig, skin);
            // black bob
            var hair = M(Palette.Hair, true, 0.35f);
            P("Bob", rig.Head, ProcMesh.Sphere(16, 10), hair, new Vector3(0, HeadR * 1.0f, -0.06f), new Vector3(HeadR * 2.25f, HeadR * 1.95f, HeadR * 2.0f));
            P("Fringe", rig.Head, ProcMesh.RoundedBox(0.4f, 2), hair, new Vector3(0, HeadR * 1.55f, HeadR * 0.78f), new Vector3(0.34f, 0.08f, 0.08f), new Vector3(15, 0, 0));
            // oversized deerstalker perched on top
            var tweed = M(Palette.Deerstalker, true);
            var hat = ProcMesh.Pivot("Deerstalker", rig.Head, new Vector3(0, HeadR * 1.78f, 0), new Vector3(-8, 0, 0));
            P("Crown", hat, ProcMesh.Sphere(14, 9), tweed, new Vector3(0, 0.03f, 0), new Vector3(0.38f, 0.2f, 0.36f));
            P("Brim F", hat, ProcMesh.Sphere(12, 6), tweed, new Vector3(0, -0.01f, 0.17f), new Vector3(0.26f, 0.035f, 0.14f), new Vector3(-12, 0, 0));
            P("Brim B", hat, ProcMesh.Sphere(12, 6), tweed, new Vector3(0, -0.01f, -0.17f), new Vector3(0.26f, 0.035f, 0.14f), new Vector3(12, 0, 0));
            P("Bow", hat, ProcMesh.Sphere(8, 6), M(Palette.WallTrim, true), new Vector3(0, 0.13f, 0), new Vector3(0.05f, 0.03f, 0.05f));
            for (int i = 0; i < 4; i++)
                P("Check", hat, ProcMesh.RoundedBox(0.2f, 1), M(Palette.WallTrim, false, 0f), new Vector3(-0.12f + i * 0.08f, 0.07f, 0.12f - Mathf.Abs(i - 1.5f) * 0.02f), new Vector3(0.012f, 0.1f, 0.012f), new Vector3(-30, 0, 0));
            // magnifying glass (right) she jabs like a pointer
            var glass = ProcMesh.Pivot("Magnifier", rig.HandR, new Vector3(0, -0.02f, 0.05f), new Vector3(80, 0, 0));
            P("Handle", glass, ProcMesh.Cylinder(10), M(Palette.Wood, true), new Vector3(0, 0.04f, 0), new Vector3(0.022f, 0.1f, 0.022f));
            P("Rim", glass, ProcMesh.Torus(0.42f, 0.08f, 22, 8), M(Palette.Gold, true, 0.6f), new Vector3(0, 0.15f, 0), new Vector3(0.14f, 0.14f, 0.14f), new Vector3(90, 0, 0));
            P("Glass", glass, ProcMesh.Disc(20), Palette.Toon(Palette.SkyHorizon, false, 1f, 0.12f), new Vector3(0, 0.15f, 0), new Vector3(0.11f, 0.002f, 0.11f), new Vector3(90, 0, 0));
            // suspect notebook (left)
            var nb = ProcMesh.Pivot("Notebook", rig.HandL, new Vector3(0.02f, 0.0f, 0.05f), new Vector3(-20, 0, 0));
            P("Cover", nb, ProcMesh.RoundedBox(0.1f, 1), M(Palette.StampRed, true), Vector3.zero, new Vector3(0.09f, 0.12f, 0.02f));
            P("Pages", nb, ProcMesh.RoundedBox(0.05f, 1), M(Palette.Paper, false), new Vector3(0, 0, 0.004f), new Vector3(0.085f, 0.115f, 0.018f));
        }

        /// <summary>Tony "Turbo" Ramirez, 10 — red puffer jacket, ski goggles, a cap, and the dented red sled he drags everywhere.</summary>
        static void Tony(KidRig rig)
        {
            var skin = Palette.SkinC;
            var red = Palette.TonyRed;
            Torso(rig, red, 1.12f);
            // puffer ridges
            var ridge = M(Palette.TonyRedDark, true);
            for (int i = 0; i < 3; i++)
                P("Puff Ridge", rig.Spine, ProcMesh.Torus(0.47f, 0.05f, 20, 6), ridge, new Vector3(0, 0.08f + i * 0.09f, 0), new Vector3(0.39f - Mathf.Abs(i - 1) * 0.03f, 0.25f, 0.33f - Mathf.Abs(i - 1) * 0.03f));
            P("Zip", rig.Spine, ProcMesh.RoundedBox(0.3f, 1), M(Palette.Coal, false), new Vector3(0, TorsoH * 0.48f, 0.175f), new Vector3(0.015f, 0.3f, 0.01f));
            Arms(rig, red, Palette.Coal, 1.1f);
            Legs(rig, Palette.Coal, Palette.Paper);
            HeadWithFace(rig, skin);
            // cap (worn forward, slightly tilted), the clue Lucy names: "the guy with the cap"
            var capMat = M(Palette.Coal, true, 0.3f);
            var cap = ProcMesh.Pivot("Cap", rig.Head, new Vector3(0, HeadR * 1.55f, 0), new Vector3(-6, 12, 0));
            P("Crown", cap, ProcMesh.Sphere(16, 8), capMat, new Vector3(0, 0.0f, -0.01f), new Vector3(0.42f, 0.24f, 0.42f));
            P("Brim", cap, ProcMesh.RoundedBox(0.4f, 2), capMat, new Vector3(0, -0.035f, 0.22f), new Vector3(0.27f, 0.02f, 0.16f), new Vector3(-8, 0, 0));
            P("Logo", cap, ProcMesh.Disc(14), M(red, false), new Vector3(0, 0.03f, 0.19f), new Vector3(0.07f, 0.002f, 0.07f), new Vector3(70, 0, 0));
            // ski goggles pushed up on the cap; they snap down over the eyes when he bolts
            var goggles = ProcMesh.Pivot("Goggles", rig.Head, new Vector3(0, HeadR * 1.35f, 0.015f), new Vector3(-35, 0, 0));
            P("Strap", goggles, ProcMesh.Torus(0.47f, 0.05f, 22, 6), M(Palette.Goggles, true), Vector3.zero, new Vector3(0.43f, 0.2f, 0.43f));
            P("Lens", goggles, ProcMesh.RoundedBox(0.45f, 3), Palette.Toon(Palette.Goggles, true, 0.9f, 0.05f), new Vector3(0, 0, 0.19f), new Vector3(0.24f, 0.08f, 0.05f));
            P("Lens Shine", goggles, ProcMesh.RoundedBox(0.45f, 2), Palette.Toon(Palette.Lens, false, 1f, 0.2f), new Vector3(0, 0, 0.215f), new Vector3(0.2f, 0.055f, 0.01f));
        }
    }

    /// <summary>The steaming thermos — a little looping steam wisp (it has a job: it marks Dexter as the cocoa kid).</summary>
    public class ThermosSteam : MonoBehaviour
    {
        public static void Attach(Transform at)
        {
            var ps = Fx.MakeSystem("Steam", at, FxTextures.SoftDot, false, 40, true);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.4f;
            main.startSpeed = 0.12f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = Palette.SkyHorizon.WithAlpha(0.55f);
            main.gravityModifier = -0.02f;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 7f;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 8f; shape.radius = 0.01f;
            shape.rotation = new Vector3(-90, 0, 0);
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.8f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Palette.SkyHorizon, 0), new GradientColorKey(Palette.Snow, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.5f, 0.2f), new GradientAlphaKey(0, 1) });
            col.color = g;
            ps.Play();
        }
    }
}
