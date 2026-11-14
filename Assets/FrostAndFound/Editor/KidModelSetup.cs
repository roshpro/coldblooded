using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FrostAndFound.EditorTools
{
    /// <summary>
    /// Wires the team's Meshy kid models (roshpro/frosty, Assets/Suspects) into the game:
    ///  • each character FBX is a Humanoid avatar with a T-pose reference (Meshy binds in an A-pose; without the
    ///    T-pose reference retargeted arms sink into the body) and 0.5 twist on arms and legs so sleeves and trouser
    ///    legs share the roll instead of candy-wrapping at the wrists and ankles;
    ///  • Lucy's Meshy animation file (Walking, Running, baseball pitching) imports as Humanoid on her avatar, so the
    ///    pitch can drive every kid's snowball throw;
    ///  • two controllers (boys / girls, Kevin Iglesias idle + walk) with IK on: Locomotion blends idle → walk → run
    ///    on Speed with Pace as the playback multiplier, plus a Throw state;
    ///  • the asset hub gets a model slot per kid.
    /// </summary>
    public static class KidModelSetup
    {
        const string Root = "Assets/FrostAndFound/Art/Kids/";
        static readonly (CastId id, string folder, bool girl)[] Kids =
        {
            (CastId.Dexter, "Dexter", false), (CastId.Sherman, "Sherman", false), (CastId.Serena, "Serena", true),
            (CastId.Lucy, "Lucy", true), (CastId.Tony, "Tony", false),
        };

        [MenuItem("Frost & Found/Setup/Kid Models")]
        public static string Run()
        {
            var log = new StringBuilder();
            var avatars = new Dictionary<CastId, Avatar>();
            foreach (var k in Kids)
            {
                string fbx = Root + "Suspects/" + k.folder + "/Meshy_AI_Character_output.fbx";
                var av = EnsureHumanoid(fbx, log);
                if (av) avatars[k.id] = av;
                SetupTexture(Root + "Suspects/" + k.folder + "/Meshy_AI_texture_0.png");
            }
            if (!avatars.TryGetValue(CastId.Lucy, out var lucyAvatar)) return log.Append("ERROR: Lucy's avatar missing").ToString();

            var clips = ImportLucyClips(lucyAvatar, log);
            if (clips == null) return log.ToString();
            var boys = BuildController(Root + "KidBoy.controller", Clip(Root + "Animations/HumanM@Idle01.fbx"),
                Clip(Root + "Animations/HumanM@Walk01_Forward [RM].fbx"), clips["Run"], clips["Throw"], log);
            var girls = BuildController(Root + "KidGirl.controller", Clip(Root + "Animations/HumanF@Idle01.fbx"),
                Clip(Root + "Animations/HumanF@Walk01_Forward [RM].fbx"), clips["Run"], clips["Throw"], log);

            var hub = Resources.Load<FrostAssets>("FrostAssets");
            if (!hub) return log.Append("ERROR: asset hub missing (run Refresh Asset Hub)").ToString();
            var slots = new List<CharacterModelSlot>();
            foreach (var k in Kids)
            {
                string dir = Root + "Suspects/" + k.folder + "/";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "Meshy_AI_Character_output.fbx");
                if (!model || !avatars.ContainsKey(k.id)) { log.AppendLine("skip " + k.folder); continue; }
                slots.Add(new CharacterModelSlot
                {
                    character = k.id, prefab = model, animator = k.girl ? girls : boys, scale = 1f,
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "Meshy_AI_texture_0.png"),
                });
            }
            hub.characterModels = slots.ToArray();
            EditorUtility.SetDirty(hub);
            AssetDatabase.SaveAssets();
            log.AppendLine($"hub: {slots.Count} kid models");
            return log.ToString();
        }

        static AnimationClip Clip(string fbx) =>
            AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));

        /// <summary>Meshy rigs use Unity's humanoid bone names but have no Spine: Chest/UpperChest sit on the hips.</summary>
        static Avatar EnsureHumanoid(string fbx, StringBuilder log)
        {
            var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (!mi) { log.AppendLine("missing " + fbx); return null; }
            var existing = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
            bool good = mi.animationType == ModelImporterAnimationType.Human && existing && existing.isHuman && existing.isValid
                        && mi.humanDescription.skeleton != null && mi.humanDescription.skeleton.Length > 10;
            if (!good)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                var map = new (string human, string bone)[]
                {
                    ("Hips", "Hips"), ("Spine", "Chest"), ("Chest", "UpperChest"), ("Neck", "Neck"), ("Head", "Head"),
                    ("LeftShoulder", "LeftShoulder"), ("LeftUpperArm", "LeftUpperArm"), ("LeftLowerArm", "LeftLowerArm"), ("LeftHand", "LeftHand"),
                    ("RightShoulder", "RightShoulder"), ("RightUpperArm", "RightUpperArm"), ("RightLowerArm", "RightLowerArm"), ("RightHand", "RightHand"),
                    ("LeftUpperLeg", "LeftUpperLeg"), ("LeftLowerLeg", "LeftLowerLeg"), ("LeftFoot", "LeftFoot"), ("LeftToes", "LeftToes"),
                    ("RightUpperLeg", "RightUpperLeg"), ("RightLowerLeg", "RightLowerLeg"), ("RightFoot", "RightFoot"), ("RightToes", "RightToes"),
                };
                var human = map.Select(m => { var hb = new HumanBone { humanName = m.human, boneName = m.bone }; hb.limit.useDefaultValues = true; return hb; }).ToArray();
                // reference pose: straighten the A-pose arms out sideways into a T-pose (mesh unchanged)
                var temp = Object.Instantiate(model);
                foreach (var side in new[] { "Left", "Right" })
                {
                    var outward = side == "Left" ? Vector3.left : Vector3.right;
                    var upper = FindDeep(temp.transform, side + "UpperArm");
                    var lower = FindDeep(temp.transform, side + "LowerArm");
                    var hand = FindDeep(temp.transform, side + "Hand");
                    if (!upper || !lower || !hand) continue;
                    upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, outward) * upper.rotation;
                    lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, outward) * lower.rotation;
                }
                var all = temp.GetComponentsInChildren<Transform>(true);
                var skeleton = new SkeletonBone[all.Length];
                for (int i = 0; i < all.Length; i++)
                    skeleton[i] = new SkeletonBone { name = i == 0 ? model.name : all[i].name, position = all[i].localPosition, rotation = all[i].localRotation, scale = all[i].localScale };
                Object.DestroyImmediate(temp);
                var hd = mi.humanDescription;
                hd.human = human;
                hd.skeleton = skeleton;
                hd.upperArmTwist = 0.5f; hd.lowerArmTwist = 0.5f; hd.upperLegTwist = 0.5f; hd.lowerLegTwist = 0.5f;
                hd.armStretch = 0.05f; hd.legStretch = 0.05f; hd.feetSpacing = 0f;
                hd.hasTranslationDoF = false;
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.humanDescription = hd;
                mi.importAnimation = false;
                mi.SaveAndReimport();
                existing = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
            }
            bool ok = existing && existing.isHuman && existing.isValid;
            log.AppendLine($"{fbx.Substring(Root.Length)}: humanoid {(ok ? "ok" : "FAILED")}{(good ? " (kept the team's avatar)" : " (rebuilt)")}");
            return ok ? existing : null;
        }

        static void SetupTexture(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!ti) return;
            bool dirty = false;
            if (ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; dirty = true; }
            if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; dirty = true; }
            var android = ti.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.format != TextureImporterFormat.ASTC_6x6 || android.maxTextureSize != 1024)
            {
                android.overridden = true; android.maxTextureSize = 1024; android.format = TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(android);
                dirty = true;
            }
            if (dirty) ti.SaveAndReimport();
        }

        /// <summary>Lucy's Meshy file: Walking / Running / baseball pitching on the shared Meshy skeleton, as Humanoid.</summary>
        static Dictionary<string, AnimationClip> ImportLucyClips(Avatar lucy, StringBuilder log)
        {
            string fbx = Root + "Suspects/Lucy/Meshy_AI_Meshy_Merged_Animations.fbx";
            var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (!mi) { log.AppendLine("ERROR: " + fbx + " missing"); return null; }
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            mi.sourceAvatar = lucy;
            mi.importAnimation = true;
            mi.importBlendShapes = false;
            var takes = mi.defaultClipAnimations;
            var outClips = new List<ModelImporterClipAnimation>();
            foreach (var c in takes)
            {
                string n = c.takeName.ToLowerInvariant();
                string name = n.Contains("pitch") ? "Throw" : n.Contains("running") ? "Run" : n.Contains("walking") ? "MeshyWalk" : null;
                if (name == null) continue;
                c.name = name;
                c.loopTime = name != "Throw";
                // stay on the spot: the game moves the kid; the clips only animate the body
                c.lockRootRotation = true; c.keepOriginalOrientation = true;
                c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = name == "Throw"; c.keepOriginalPositionXZ = true;
                outClips.Add(c);
            }
            mi.clipAnimations = outClips.ToArray();
            mi.SaveAndReimport();
            var dict = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name, c => c);
            foreach (var need in new[] { "Throw", "Run" })
                if (!dict.ContainsKey(need)) { log.AppendLine("ERROR: Lucy's clip " + need + " missing (takes: " + string.Join(", ", takes.Select(t => t.takeName)) + ")"); return null; }
            log.AppendLine("Lucy clips: " + string.Join(", ", dict.Keys) + $" (throw {dict["Throw"].length:F2}s, run {dict["Run"].length:F2}s)");
            return dict;
        }

        static AnimatorController BuildController(string path, AnimationClip idle, AnimationClip walk, AnimationClip run, AnimationClip throwClip, StringBuilder log)
        {
            if (!idle || !walk) { log.AppendLine("ERROR: idle/walk clips missing for " + path); return null; }
            AssetDatabase.DeleteAsset(path);
            var c = AnimatorController.CreateAnimatorControllerAtPath(path);
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter("Pace", AnimatorControllerParameterType.Float);
            c.AddParameter("Throw", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Talk", AnimatorControllerParameterType.Bool);
            c.AddParameter("Dazed", AnimatorControllerParameterType.Bool);
            var ps = c.parameters;
            foreach (var p in ps) if (p.name == "Pace") p.defaultFloat = 1f;
            c.parameters = ps;
            var layers = c.layers;
            layers[0].iKPass = true;
            c.layers = layers;

            var loco = c.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1f);
            if (run) tree.AddChild(run, 2f);
            loco.speedParameterActive = true;
            loco.speedParameter = "Pace";
            loco.iKOnFeet = true;
            var sm = c.layers[0].stateMachine;
            sm.defaultState = loco;
            if (throwClip)
            {
                var thr = sm.AddState("Throw");
                thr.motion = throwClip;
                thr.iKOnFeet = true;
                var any = sm.AddAnyStateTransition(thr);
                any.AddCondition(AnimatorConditionMode.If, 0, "Throw");
                any.hasExitTime = false; any.duration = 0.15f; any.canTransitionToSelf = false;
                var back = thr.AddTransition(loco);
                back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.3f;
            }
            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            log.AppendLine($"{path.Substring(Root.Length)}: idle '{idle.name}', walk '{walk.name}', run '{(run ? run.name : "-")}', throw '{(throwClip ? throwClip.name : "-")}'");
            return c;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform ch in t) { var r = FindDeep(ch, name); if (r) return r; }
            return null;
        }
    }
}
