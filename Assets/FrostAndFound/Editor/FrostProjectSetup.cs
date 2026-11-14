using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FrostAndFound.EditorTools
{
    /// <summary>
    /// One-click project + scene setup for Frost &amp; Found. Everything here is idempotent.
    /// Menu: Frost &amp; Found ▸ Setup ▸ …
    /// </summary>
    public static class FrostProjectSetup
    {
        public const string Root = "Assets/FrostAndFound";
        public const string ScenePath = Root + "/Scenes/FrostAndFound.unity";

        [MenuItem("Frost & Found/Setup/Run Full Setup", priority = 0)]
        public static string RunAll()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine(EnsureLayers());
            log.AppendLine(EnsurePhysics());
            log.AppendLine(EnsureTmp());
            log.AppendLine(RefreshAssetHub());
            log.AppendLine(EnsureFonts());
            log.AppendLine(EnsureEditorSettings());
            log.AppendLine(EnsureRenderSettings());
            log.AppendLine(BuildScene());
            Debug.Log("[Frost] Setup:\n" + log);
            return log.ToString();
        }

        [MenuItem("Frost & Found/Setup/Layers & Physics")]
        public static string EnsureLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            for (int i = 0; i < Layers.Names.Length; i++)
            {
                if (string.IsNullOrEmpty(Layers.Names[i])) continue;
                var p = layers.GetArrayElementAtIndex(i);
                if (p.stringValue != Layers.Names[i]) p.stringValue = Layers.Names[i];
            }
            tm.ApplyModifiedProperties();
            return "layers ok";
        }

        public static string EnsurePhysics()
        {
            Physics.IgnoreLayerCollision(Layers.Snowball, Layers.PlayerHands, true);
            Physics.IgnoreLayerCollision(Layers.Snowball, Layers.UI3D, true);
            Physics.IgnoreLayerCollision(Layers.Snowball, Layers.PhotoOnly, true);
            Physics.IgnoreLayerCollision(Layers.Kid, Layers.Kid, true);
            Physics.IgnoreLayerCollision(Layers.PlayerHands, Layers.PlayerHands, true);
            Physics.IgnoreLayerCollision(Layers.PlayerHands, Layers.Default, true);
            Physics.IgnoreLayerCollision(Layers.PlayerHands, Layers.Kid, true);
            Physics.IgnoreLayerCollision(Layers.PlayerHands, Layers.Room, true);
            Physics.IgnoreLayerCollision(Layers.PlayerHands, Layers.Snow, true);
            Physics.IgnoreLayerCollision(Layers.UI3D, Layers.Default, true);
            Physics.IgnoreLayerCollision(Layers.UI3D, Layers.Kid, true);
            return "physics matrix ok";
        }

        public static string EnsureTmp()
        {
            if (TMPro.TMP_Settings.instance != null && TMPro.TMP_Settings.defaultFontAsset != null) return "TMP essentials present";
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            return "TMP essentials imported";
        }

        [MenuItem("Frost & Found/Setup/Refresh Asset Hub")]
        public static string RefreshAssetHub()
        {
            Directory.CreateDirectory(Root + "/Resources");
            var path = Root + "/Resources/FrostAssets.asset";
            var hub = AssetDatabase.LoadAssetAtPath<FrostAssets>(path);
            if (!hub)
            {
                hub = ScriptableObject.CreateInstance<FrostAssets>();
                AssetDatabase.CreateAsset(hub, path);
            }
            hub.toon = Shader.Find("FrostAndFound/Toon");
            hub.toonOutlined = Shader.Find("FrostAndFound/ToonOutlined");
            hub.snow = Shader.Find("FrostAndFound/Snow");
            hub.unlit = Shader.Find("FrostAndFound/Unlit");
            hub.unlitTransparent = Shader.Find("FrostAndFound/UnlitTransparent");
            hub.particle = Shader.Find("FrostAndFound/Particle");
            hub.sky = Shader.Find("FrostAndFound/Sky");
            hub.photo = Shader.Find("FrostAndFound/Photo");
            hub.blur = Shader.Find("FrostAndFound/Blur");
            hub.uiGlass = Shader.Find("FrostAndFound/UIGlass");
            EditorUtility.SetDirty(hub);

            var tunePath = Root + "/Resources/FrostTuning.asset";
            if (!AssetDatabase.LoadAssetAtPath<FrostTuning>(tunePath))
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<FrostTuning>(), tunePath);
            AssetDatabase.SaveAssets();
            int missing = 0;
            foreach (var s in new Shader[] { hub.toon, hub.toonOutlined, hub.snow, hub.unlit, hub.unlitTransparent, hub.particle, hub.sky, hub.photo, hub.blur, hub.uiGlass })
                if (!s) missing++;
            return missing == 0 ? "asset hub ok (10 shaders)" : $"asset hub: {missing} shaders missing!";
        }

        [MenuItem("Frost & Found/Setup/Rebuild Fonts")]
        public static string EnsureFonts()
        {
            var hub = AssetDatabase.LoadAssetAtPath<FrostAssets>(Root + "/Resources/FrostAssets.asset");
            if (!hub) return "fonts: run Refresh Asset Hub first";
            string dir = Root + "/Fonts";
            var chars = new System.Text.StringBuilder();
            for (int c = 32; c < 127; c++) chars.Append((char)c);
            chars.Append("¢–—…‘’“”•·é★×→←");
            (string ttf, string asset)[] fonts =
            {
                ("Roboto-Black.ttf", "Frost Display (Roboto Black)"),
                ("Roboto-Bold.ttf", "Frost Heading (Roboto Bold)"),
                ("Inter-SemiBold.ttf", "Frost Body (Inter SemiBold)"),
                ("RobotoMono-Bold.ttf", "Frost Mono (Roboto Mono Bold)"),
                ("Inter-SemiBoldItalic.ttf", "Frost Hand (Inter SemiBold Italic)"),
                ("Inter-Regular.ttf", "Frost Regular (Inter Regular)"),
            };
            var made = new TMPro.TMP_FontAsset[fonts.Length];
            var fallback = TMPro.TMP_Settings.defaultFontAsset;
            for (int i = 0; i < fonts.Length; i++)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(dir + "/" + fonts[i].ttf);
                if (!font) return "fonts: missing " + fonts[i].ttf;
                var path = dir + "/" + fonts[i].asset + ".asset";
                var fa = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
                if (!fa)
                {
                    fa = TMPro.TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                        TMPro.AtlasPopulationMode.Dynamic, true);
                    fa.name = fonts[i].asset;
                    AssetDatabase.CreateAsset(fa, path);
                    for (int k = 0; k < fa.atlasTextures.Length; k++)
                    {
                        fa.atlasTextures[k].name = fonts[i].asset + " Atlas " + k;
                        AssetDatabase.AddObjectToAsset(fa.atlasTextures[k], fa);
                    }
                    fa.material.name = fonts[i].asset + " Material";
                    AssetDatabase.AddObjectToAsset(fa.material, fa);
                }
                fa.TryAddCharacters(chars.ToString(), out _);
                if (fallback && fa.fallbackFontAssetTable != null && !fa.fallbackFontAssetTable.Contains(fallback)) fa.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(fa);
                made[i] = fa;
            }
            hub.displayFont = made[0]; hub.headingFont = made[1]; hub.bodyFont = made[2]; hub.monoFont = made[3]; hub.handFont = made[4]; hub.regularFont = made[5];
            hub.speechFont = made[2]; hub.titleFont = made[0];
            EditorUtility.SetDirty(hub);
            AssetDatabase.SaveAssets();
            return "fonts ok (6 TMP font assets)";
        }

        public static string EnsureEditorSettings()
        {
            // always reload the domain entering Play mode, so static state never leaks between runs
            EditorSettings.enterPlayModeOptionsEnabled = false;
            PlayerSettings.runInBackground = true;
            return "editor settings ok";
        }

        public static string EnsureRenderSettings()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(p);
                if (!a) continue;
                a.shadowDistance = 8f;
                a.shadowCascadeCount = 1;
                a.mainLightShadowmapResolution = 2048;
                a.supportsHDR = false;
                a.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
                EditorUtility.SetDirty(a);
                sb.Append(a.name).Append(' ');
            }
            AssetDatabase.SaveAssets();
            return "URP assets tuned: " + sb;
        }

        // ------------------------------------------------------------------ the scene
        [MenuItem("Frost & Found/Setup/Rebuild Main Scene")]
        public static string BuildScene()
        {
            Directory.CreateDirectory(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // key light: warm, ~45° elevation, across the play axis; soft shadows
            var lightGo = new GameObject("Key Light (warm)");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Palette.Hex("#FFF1DC");
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.65f;
            lightGo.transform.rotation = Quaternion.Euler(47f, -32f, 0f);

            // the ambient answer: a palette gradient that keeps shadow sides alive
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.Hex("#B9CCF0");
            RenderSettings.ambientEquatorColor = Palette.Hex("#E3E6F0");
            RenderSettings.ambientGroundColor = Palette.Hex("#8F8AA6");
            RenderSettings.fog = false;
            RenderSettings.sun = light;

            GameObject xrRoot = BuildXR();

            var game = new GameObject("Frost & Found");
            var gm = game.AddComponent<GameManager>();
            gm.xrRig = xrRoot;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return "scene built: " + ScenePath;
        }

        static GameObject BuildXR()
        {
            var root = new GameObject("XR (Meta XR SDK)");
#if FROST_META
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
            if (!rigPrefab) { Debug.LogError("[Frost] OVRCameraRig prefab not found"); return root; }
            var rigGo = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rigGo.transform.SetParent(root.transform, false);
            PrefabUtility.UnpackPrefabInstance(rigGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var rig = rigGo.GetComponent<OVRCameraRig>();

            var manager = rigGo.GetComponent<OVRManager>() ?? rigGo.AddComponent<OVRManager>();
            manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            manager.isInsightPassthroughEnabled = true;
            var mso = new SerializedObject(manager);
            var p = mso.FindProperty("requestScenePermissionOnStartup"); if (p != null) p.boolValue = true;
            mso.ApplyModifiedPropertiesWithoutUndo();

            var pt = rigGo.AddComponent<OVRPassthroughLayer>();
            var pso = new SerializedObject(pt);
            var ov = pso.FindProperty("overlayType"); if (ov != null) ov.enumValueIndex = (int)OVROverlay.OverlayType.Underlay;
            pso.ApplyModifiedPropertiesWithoutUndo();

            // eye camera: transparent clear for passthrough
            var eye = rigGo.transform.Find("TrackingSpace/CenterEyeAnchor");
            if (eye)
            {
                var cam = eye.GetComponent<Camera>();
                if (cam)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0, 0, 0, 0);
                    cam.nearClipPlane = 0.03f;
                    var data = eye.GetComponent<UniversalAdditionalCameraData>() ?? eye.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    data.renderPostProcessing = false;
                }
            }

            // hand-tracking data (we draw our own mittens, so the hand meshes are removed)
            var handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRHandPrefab.prefab");
            var mpr = rigGo.AddComponent<MetaPlayerRig>();
            mpr.cameraRig = rig;
            foreach (var side in new[] { "Left", "Right" })
            {
                var anchor = rigGo.transform.Find("TrackingSpace/" + side + "HandAnchor");
                if (!anchor || !handPrefab) continue;
                var h = (GameObject)PrefabUtility.InstantiatePrefab(handPrefab);
                PrefabUtility.UnpackPrefabInstance(h, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                h.name = "OVRHand " + side;
                h.transform.SetParent(anchor, false);
                var hand = h.GetComponent<OVRHand>();
                var skel = h.GetComponent<OVRSkeleton>();
                var hso = new SerializedObject(hand);
                hso.FindProperty("HandType").intValue = side == "Left" ? (int)OVRHand.Hand.HandLeft : (int)OVRHand.Hand.HandRight;
                hso.ApplyModifiedPropertiesWithoutUndo();
                var sso = new SerializedObject(skel);
                sso.FindProperty("_skeletonType").intValue = side == "Left" ? (int)OVRSkeleton.SkeletonType.XRHandLeft : (int)OVRSkeleton.SkeletonType.XRHandRight;
                sso.ApplyModifiedPropertiesWithoutUndo();
                var mr = h.GetComponent<OVRMeshRenderer>(); if (mr) Object.DestroyImmediate(mr);
                var sr = h.GetComponent<OVRSkeletonRenderer>(); if (sr) Object.DestroyImmediate(sr);
                var smr = h.GetComponent<SkinnedMeshRenderer>(); if (smr) Object.DestroyImmediate(smr);
                var mesh = h.GetComponent<OVRMesh>(); if (mesh) Object.DestroyImmediate(mesh);
                if (side == "Left") { mpr.leftHand = hand; mpr.leftSkeleton = skel; } else { mpr.rightHand = hand; mpr.rightSkeleton = skel; }
            }
#if FROST_MRUK
            var mrukPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.mrutilitykit/Core/Tools/MRUK.prefab");
            if (mrukPrefab)
            {
                var m = (GameObject)PrefabUtility.InstantiatePrefab(mrukPrefab);
                m.transform.SetParent(root.transform, false);
            }
            else
            {
                var m = new GameObject("MRUK");
                m.transform.SetParent(root.transform, false);
                m.AddComponent<Meta.XR.MRUtilityKit.MRUK>();
            }
#endif
#endif
            // starts disabled; the GameManager enables it only when a headset (or Link / Meta XR Simulator) is active
            root.SetActive(false);
            return root;
        }

        [MenuItem("Frost & Found/Export Voice Script (CSV)")]
        public static string ExportVoiceScript()
        {
            var path = Root + "/VoiceScript.csv";
            File.WriteAllText(path, Lines.ExportCsv());
            AssetDatabase.ImportAsset(path);
            return "wrote " + path;
        }

        [MenuItem("Frost & Found/Export Placeholder Audio (WAV)")]
        public static string ExportPlaceholderAudio()
        {
            var dir = "AudioPreview";
            Directory.CreateDirectory(dir);
            int n = 0;
            WriteWav(Path.Combine(dir, "NoirScore.wav"), Synth.NoirScore(), Synth.SR); n++;
            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx)))
            {
                WriteWav(Path.Combine(dir, "SFX_" + sfx + ".wav"), Synth.Generate(sfx, 1), Synth.SR);
                n++;
            }
            foreach (var id in new[] { "lucy_intro", "dexter_excuse", "tony_caught" })
            {
                var l = Lines.Get(id);
                WriteWav(Path.Combine(dir, "Voice_" + id + "_babble.wav"), Synth.Babble(l.Text, l.Speaker, l.Id.GetHashCode()), Synth.SR);
                n++;
            }
            return "wrote " + n + " files to " + Path.GetFullPath(dir);
        }

        static void WriteWav(string path, float[] data, int sampleRate)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int bytes = data.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(sampleRate); w.Write(sampleRate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                foreach (var f in data) w.Write((short)Mathf.Clamp(f * 32767f, -32768f, 32767f));
            }
        }

        [MenuItem("Frost & Found/Play With Autopilot")]
        public static void PlayAutopilot()
        {
            var gm = Object.FindAnyObjectByType<GameManager>();
            if (gm) { gm.autopilot = true; EditorUtility.SetDirty(gm); }
            EditorApplication.isPlaying = true;
        }
    }
}
