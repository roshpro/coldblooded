using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace FrostAndFound.EditorTools
{
    /// <summary>
    /// Offline video render: Serena walks up to a snowy cottage door and knocks (~5 s, 1080p30 mp4 with knock sounds).
    /// Builds its own throwaway scene, poses her real rigged model (idle clip + two-bone IK for the knocking arm),
    /// renders every frame supersampled and encodes with Unity's MediaEncoder into &lt;project&gt;/Renders/.
    /// Menu: Frost ▸ Video Renders ▸ Serena Knocks on a Door. Batch: -executeMethod FrostAndFound.EditorTools.SerenaKnockRender.RunBatch
    /// </summary>
    public static class SerenaKnockRender
    {
        const int W = 1920, H = 1080, SS = 2, Fps = 30, SampleRate = 48000;
        const float Seconds = 5f;
        static readonly float[] Knocks = { 1.55f, 1.9f, 2.25f };   // fist-meets-door moments

        static readonly Color CottageRed = new Color(0.71f, 0.29f, 0.24f);
        static readonly Color Trim = new Color(0.96f, 0.94f, 0.9f);
        static readonly Color DoorGreen = new Color(0.17f, 0.37f, 0.31f);
        static readonly Color Stone = new Color(0.62f, 0.64f, 0.7f);
        static readonly Color Pine = new Color(0.16f, 0.36f, 0.3f);
        static readonly Color Background = new Color(0.8f, 0.86f, 0.96f);

        [MenuItem("Frost/Video Renders/Serena Knocks on a Door")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try { Render(); }
            finally { if (!Application.isBatchMode && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static void RunBatch()
        {
            int code = 0;
            try { Render(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        // ================================================================== scene
        class Shot
        {
            public Camera Cam;
            public GameObject Kid;
            public Animator Anim;
            public AnimationClip Idle;
            public Material FaceMat;
            public SkinnedMeshRenderer Smr;
            public Mesh Baked;
            public Transform BakedT;   // offline renders don't re-skin per Camera.Render, so we bake the posed mesh each frame
            public Texture FaceBase, FaceSmirk, FaceO;
            public Transform Door;
            public Vector3 StandPos;
            public Quaternion StandRot;
            public float DoorFrontZ;
            public readonly List<(Transform t, Vector3 p0, float speed, float phase)> Flakes = new List<(Transform, Vector3, float, float)>();
            public readonly List<(Transform t, Vector3 p0, Vector3 v0, float born)> Clumps = new List<(Transform, Vector3, Vector3, float)>();
        }

        static void Render()
        {
            var outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Renders");
            Directory.CreateDirectory(Path.Combine(outDir, "stills"));
            var path = Path.Combine(outDir, "serena_knock.mp4");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Random.InitState(7);
            var shot = BuildScene();

            var big = new RenderTexture(W * SS, H * SS, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var small = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            big.Create(); small.Create();
            shot.Cam.targetTexture = big;
            var frame = new Texture2D(W, H, TextureFormat.RGBA32, false);

            int frames = Mathf.RoundToInt(Seconds * Fps), spf = SampleRate / Fps;
            var audio = KnockAudio(frames * spf);
            var vAttr = new VideoTrackAttributes { frameRate = new MediaRational(Fps), width = W, height = H, includeAlpha = false, bitRateMode = VideoBitrateMode.High };
            var aAttr = new AudioTrackAttributes { sampleRate = new MediaRational(SampleRate), channelCount = 1, language = "en" };
            // compile every shader now (no grey placeholder frames) and let shadows/skinning settle
            ShaderUtil.allowAsyncCompilation = false;
            for (int i = 0; i < 4; i++) { Pose(shot, 0f); shot.Cam.Render(); }
            var stills = new HashSet<int> { 0, 30, 47, 58, 75, 100, 125, frames - 1 };
            using (var enc = new MediaEncoder(path, vAttr, aAttr))
            using (var buf = new NativeArray<float>(spf, Allocator.Persistent))
            {
                for (int i = 0; i < frames; i++)
                {
                    float t = i / (float)Fps;
                    Pose(shot, t);
                    shot.Cam.Render();
                    Graphics.Blit(big, small);
                    var prev = RenderTexture.active;
                    RenderTexture.active = small;
                    frame.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    frame.Apply();
                    RenderTexture.active = prev;
                    enc.AddFrame(frame);
                    NativeArray<float>.Copy(audio, i * spf, buf, 0, spf);
                    enc.AddSamples(buf);
                    if (stills.Contains(i)) File.WriteAllBytes(Path.Combine(outDir, "stills", $"frame_{i:000}.png"), frame.EncodeToPNG());
                    if (i % 30 == 0) Debug.Log($"[Frost Render] frame {i}/{frames}");
                }
            }
            audio.Dispose();
            shot.Cam.targetTexture = null;
            big.Release(); small.Release();
            Debug.Log("[Frost Render] wrote " + path);
        }

        /// <summary>Debug: close-ups of the knock pose, plus a test with the arm forced straight up (checks skinning).</summary>
        public static void DebugPose()
        {
            try
            {
                var outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Renders", "debug");
                Directory.CreateDirectory(outDir);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ShaderUtil.allowAsyncCompilation = false;
                var shot = BuildScene();
                var rt = new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                var tex = new Texture2D(960, 720, TextureFormat.RGBA32, false);
                void Snap(string name, Vector3 camPos, Vector3 look)
                {
                    shot.Cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(look - camPos));
                    shot.Cam.targetTexture = rt;
                    for (int i = 0; i < 2; i++) shot.Cam.Render();
                    RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); tex.Apply(); RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
                }
                var smr = shot.Kid.GetComponentInChildren<SkinnedMeshRenderer>();
                Debug.Log($"[Frost Render] smr root {smr.rootBone?.name} bones {smr.bones.Length} first {string.Join(",", smr.bones.Take(12).Select(b => b ? b.name : "null"))}");
                var up = shot.Anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Debug.Log($"[Frost Render] RightUpperArm in smr.bones: {smr.bones.Contains(up)}  path {AnimationUtility.CalculateTransformPath(up, shot.Kid.transform)}");
                Pose(shot, Knocks[0]);
                var sh = up.position;
                Snap("knock_side", sh + new Vector3(-0.9f, 0.15f, 0.25f), sh + new Vector3(0, 0, -0.05f));
                Snap("knock_top", sh + new Vector3(-0.35f, 0.9f, 0.4f), sh);
                Pose(shot, 0f);
                up.rotation = Quaternion.FromToRotation(Vector3.down, Vector3.up) * up.rotation;
                Bake(shot);
                Snap("arm_forced_up", sh + new Vector3(-0.9f, 0.15f, 0.25f), sh);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static Material Toon(Color c, bool outline = true) => Palette.Toon(c, outline, 0.25f);
        static GameObject Box(string n, Transform p, Color c, Vector3 pos, Vector3 size, Vector3 euler = default, float round = 0.04f, bool outline = true)
            => ProcMesh.Part(n, p, ProcMesh.RoundedBox(round, 3), Toon(c, outline), pos, size, euler);

        static Shot BuildScene()
        {
            var shot = new Shot();
            // light & atmosphere: a bright, cool winter morning
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.SkyTop;
            RenderSettings.ambientEquatorColor = Palette.SkyHorizon;
            RenderSettings.ambientGroundColor = Palette.SkyGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Background;
            RenderSettings.fogStartDistance = 9f;
            RenderSettings.fogEndDistance = 40f;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(38f, 152f, 0f);

            var world = new GameObject("World").transform;
            // snowy ground, drifts, trees
            ProcMesh.Part("Ground", world, ProcMesh.Disc(48), Toon(Palette.Snow, false), new Vector3(0, -0.5f, 0), new Vector3(80, 1, 80));
            for (int i = 0; i < 9; i++)
            {
                float x = -2.2f + i * 0.55f;
                if (Mathf.Abs(x) < 0.75f) continue;   // the path to the door stays clear
                ProcMesh.Part("Drift", world, ProcMesh.Sphere(12, 8, 0.12f, i), Toon(Palette.Snow, false), new Vector3(x, 0f, 0.2f + Random.Range(-0.05f, 0.08f)),
                    new Vector3(Random.Range(0.6f, 0.9f), Random.Range(0.28f, 0.42f), Random.Range(0.5f, 0.7f)));
            }
            foreach (var p in new[] { new Vector3(-4.2f, 0, -2.5f), new Vector3(-3.4f, 0, -4.8f), new Vector3(3.9f, 0, -3.6f), new Vector3(5.2f, 0, -1.2f), new Vector3(-6.5f, 0, -0.8f), new Vector3(2.8f, 0, -7f), new Vector3(-1.5f, 0, -8f) })
                Tree(world, p, Random.Range(2.2f, 3.4f));
            for (int i = 0; i < 6; i++)
                ProcMesh.Part("Hill", world, ProcMesh.Sphere(16, 10, 0.05f, i), Toon(Color.Lerp(Palette.Snow, Palette.SnowShadow, 0.25f), false),
                    new Vector3(-16f + i * 7f, -3f, -22f - (i % 2) * 6f), new Vector3(16f, 8f, 10f));

            // the cottage: front wall at z = 0, gable end facing the camera
            const float Wd = 3.2f, D = 3.0f, Hw = 2.2f;
            var house = new GameObject("Cottage").transform;
            house.SetParent(world, false);
            Box("Walls", house, CottageRed, new Vector3(0, Hw / 2f, -D / 2f), new Vector3(Wd, Hw, D), default, 0.03f);
            float s = Wd / Mathf.Sqrt(2f);
            Box("Gable", house, CottageRed, new Vector3(0, Hw, -D / 2f), new Vector3(s, s, D), new Vector3(0, 0, 45), 0.01f);
            float ridge = Hw + Wd / 2f, eave = 0.35f, L = (Wd / 2f + eave) / Mathf.Cos(45f * Mathf.Deg2Rad);
            foreach (int side in new[] { 1, -1 })
            {
                var down = new Vector3(side * 0.7071f, -0.7071f, 0f);
                var up = new Vector3(side * 0.7071f, 0.7071f, 0f);
                var c = new Vector3(0, ridge, -D / 2f) + down * (L / 2f) + up * 0.08f;
                Box("Roof", house, new Color(0.33f, 0.24f, 0.22f), c, new Vector3(L, 0.12f, D + 0.5f), new Vector3(0, 0, -side * 45f), 0.03f);
                Box("Roof Snow", house, Palette.Snow, c + up * 0.11f + down * 0.05f, new Vector3(L - 0.05f, 0.14f, D + 0.56f), new Vector3(0, 0, -side * 45f), 0.06f, false);
            }
            Box("Chimney", house, new Color(0.55f, 0.36f, 0.3f), new Vector3(0.95f, ridge - 0.2f, -2.1f), new Vector3(0.38f, 1.2f, 0.38f));
            Box("Chimney Snow", house, Palette.Snow, new Vector3(0.95f, ridge + 0.42f, -2.1f), new Vector3(0.44f, 0.1f, 0.44f), default, 0.05f, false);
            // gable trim + round attic window
            ProcMesh.Part("Attic Window", house, ProcMesh.Cylinder(20), Toon(Trim), new Vector3(0, Hw + 0.6f, 0.02f), new Vector3(0.5f, 0.05f, 0.5f), new Vector3(90, 0, 0));
            ProcMesh.Part("Attic Glass", house, ProcMesh.Cylinder(20), Palette.Toon(Palette.LampGlow, false, 0f, 0.7f), new Vector3(0, Hw + 0.6f, 0.05f), new Vector3(0.38f, 0.02f, 0.38f), new Vector3(90, 0, 0));
            // windows either side of the door, warm light inside
            foreach (int side in new[] { -1, 1 })
            {
                var wp = new Vector3(side * 1.05f, 1.3f, 0f);
                Box("Window Frame", house, Trim, wp + new Vector3(0, 0, 0.03f), new Vector3(0.66f, 0.66f, 0.07f));
                ProcMesh.Part("Window Glass", house, ProcMesh.RoundedBox(0.01f, 2), Palette.Toon(Palette.LampGlow, false, 0f, 0.75f), wp + new Vector3(0, 0, 0.06f), new Vector3(0.52f, 0.52f, 0.02f));
                Box("Mullion", house, Trim, wp + new Vector3(0, 0, 0.075f), new Vector3(0.04f, 0.54f, 0.02f), default, 0.01f, false);
                Box("Mullion", house, Trim, wp + new Vector3(0, 0, 0.075f), new Vector3(0.54f, 0.04f, 0.02f), default, 0.01f, false);
                Box("Sill Snow", house, Palette.Snow, wp + new Vector3(0, -0.36f, 0.08f), new Vector3(0.72f, 0.07f, 0.16f), default, 0.03f, false);
            }
            // porch step and door
            const float StepH = 0.16f;
            Box("Step", house, Stone, new Vector3(0, StepH / 2f, 0.3f), new Vector3(1.4f, StepH, 0.6f));
            Box("Step Snow", house, Palette.Snow, new Vector3(-0.52f, StepH + 0.02f, 0.32f), new Vector3(0.34f, 0.06f, 0.5f), default, 0.03f, false);
            Box("Step Snow", house, Palette.Snow, new Vector3(0.55f, StepH + 0.02f, 0.3f), new Vector3(0.28f, 0.05f, 0.5f), default, 0.03f, false);
            Box("Door Frame", house, Trim, new Vector3(0, StepH + 1.0f, 0.03f), new Vector3(1.06f, 2.04f, 0.07f));
            Box("Lintel Snow", house, Palette.Snow, new Vector3(0, StepH + 2.06f, 0.06f), new Vector3(1.14f, 0.08f, 0.16f), default, 0.035f, false);
            var door = new GameObject("Door").transform;   // pivot on the hinge so it can rattle
            door.SetParent(house, false);
            door.localPosition = new Vector3(-0.43f, StepH, 0.07f);
            Box("Door Panel", door, DoorGreen, new Vector3(0.43f, 0.95f, 0f), new Vector3(0.86f, 1.9f, 0.05f), default, 0.02f);
            Box("Door Inset", door, Color.Lerp(DoorGreen, Color.black, 0.15f), new Vector3(0.43f, 0.55f, 0.03f), new Vector3(0.6f, 0.62f, 0.02f), default, 0.01f, false);
            Box("Door Inset", door, Color.Lerp(DoorGreen, Color.black, 0.15f), new Vector3(0.43f, 1.35f, 0.03f), new Vector3(0.6f, 0.62f, 0.02f), default, 0.01f, false);
            ProcMesh.Part("Knob", door, ProcMesh.Sphere(12, 8), Toon(Palette.Gold), new Vector3(0.74f, 0.95f, 0.06f), Vector3.one * 0.07f);
            ProcMesh.Part("Wreath", door, ProcMesh.Torus(0.4f, 0.12f, 20, 8), Toon(new Color(0.22f, 0.55f, 0.3f)), new Vector3(0.43f, 1.45f, 0.06f), Vector3.one * 0.42f, new Vector3(90, 0, 0));
            ProcMesh.Part("Bow", door, ProcMesh.Sphere(10, 8), Toon(Palette.StampRed), new Vector3(0.43f, 1.3f, 0.1f), new Vector3(0.09f, 0.06f, 0.04f));
            shot.Door = door;
            shot.DoorFrontZ = 0.07f + 0.025f;

            // falling snow
            var flakeMat = Palette.Unlit(Color.white);
            var flakeMesh = ProcMesh.Sphere(6, 4);
            for (int i = 0; i < 420; i++)
            {
                var p0 = new Vector3(Random.Range(-3.5f, 4.5f), Random.Range(0f, 4.2f), Random.Range(-1.5f, 3.8f));
                var f = ProcMesh.Part("Flake", world, flakeMesh, flakeMat, p0, Vector3.one * Random.Range(0.018f, 0.032f));
                f.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                shot.Flakes.Add((f.transform, p0, Random.Range(0.35f, 0.65f), Random.Range(0f, 6.28f)));
            }
            // snow clumps that drop off the lintel when she knocks
            foreach (float k in Knocks)
                for (int j = 0; j < 4; j++)
                {
                    var p0 = new Vector3(Random.Range(-0.45f, 0.45f), StepH + 2.1f, 0.12f);
                    var c = ProcMesh.Part("Clump", world, ProcMesh.Sphere(8, 6, 0.2f, j), Toon(Palette.Snow, false), p0, Vector3.one * Random.Range(0.035f, 0.06f));
                    c.SetActive(false);
                    shot.Clumps.Add((c.transform, p0, new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0f, 0.3f), Random.Range(0.15f, 0.4f)), k + Random.Range(0f, 0.08f)));
                }

            // Serena
            var slot = FrostAssets.SlotFor(CastId.Serena);
            if (slot == null || !slot.prefab) throw new Exception("No Serena model in FrostAssets ▸ Character Models");
            var kid = UnityEngine.Object.Instantiate(slot.prefab);
            kid.name = "Serena";
            var anim = kid.GetComponentInChildren<Animator>();
            anim.runtimeAnimatorController = null;
            anim.applyRootMotion = false;
            var smr = kid.GetComponentInChildren<SkinnedMeshRenderer>();
            smr.updateWhenOffscreen = true;
            shot.FaceMat = Palette.SoftTextured(slot.texture, "Serena");
            smr.sharedMaterial = shot.FaceMat;
            shot.Smr = smr;
            shot.Baked = new Mesh { name = "Serena (baked)" };
            var bakedGo = new GameObject("Serena Baked");
            shot.BakedT = bakedGo.transform;   // world space, unit scale: BakeMesh(useScale: true) carries the size
            bakedGo.AddComponent<MeshFilter>().sharedMesh = shot.Baked;
            bakedGo.AddComponent<MeshRenderer>().sharedMaterial = shot.FaceMat;
            smr.forceRenderingOff = true;
            float h = smr.bounds.size.y;
            if (h > 0.05f) kid.transform.localScale *= 1.05f * (slot.scale > 0f ? slot.scale : 1f) / h;
            shot.FaceBase = slot.texture;
            shot.FaceSmirk = Resources.Load<Texture2D>("Faces/Serena/Serena_Face_MouthSmirk");
            shot.FaceO = Resources.Load<Texture2D>("Faces/Serena/Serena_Face_MouthO");
            shot.Idle = AssetDatabase.LoadAllAssetsAtPath("Assets/FrostAndFound/Art/Kids/Animations/HumanF@Idle01.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (!shot.Idle) Debug.LogWarning("[Frost Render] idle clip not found; Serena keeps her bind pose");
            shot.Kid = kid;
            shot.Anim = anim;
            // stand on the step, facing the door, at arm's reach
            shot.StandRot = Quaternion.Euler(0, 180f, 0);
            kid.transform.SetPositionAndRotation(new Vector3(0.18f, StepH, 0.45f), shot.StandRot);
            SampleIdle(shot, 0f);
            var sh = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var el = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var ha = anim.GetBoneTransform(HumanBodyBones.RightHand);
            float arm = Vector3.Distance(sh.position, el.position) + Vector3.Distance(el.position, ha.position);
            float shoulderOffsetZ = sh.position.z - kid.transform.position.z;
            float standZ = shot.DoorFrontZ + 0.045f + arm * 0.72f - shoulderOffsetZ;
            shot.StandPos = new Vector3(0.18f, StepH, 0.25f);
            kid.transform.position = shot.StandPos;
            Debug.Log($"[Frost Render] Serena arm {arm:F2} m (IK reach estimate {standZ:F2})");

            // camera
            var cam = new GameObject("Render Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 120f;
            shot.Cam = cam;
            return shot;
        }

        static void Tree(Transform parent, Vector3 p, float h)
        {
            ProcMesh.Part("Trunk", parent, ProcMesh.Cylinder(8), Toon(Palette.Wood), p + Vector3.up * h * 0.1f, new Vector3(0.18f, h * 0.2f, 0.18f) * (h / 3f));
            for (int i = 0; i < 3; i++)
            {
                float y = h * (0.2f + i * 0.24f), r = h * (0.5f - i * 0.12f);
                ProcMesh.Part("Needles", parent, ProcMesh.Cone(9), Toon(Pine), p + Vector3.up * y, new Vector3(r, h * 0.42f, r));
                ProcMesh.Part("Needles Snow", parent, ProcMesh.Cone(9), Toon(Palette.Snow, false), p + Vector3.up * (y + h * 0.2f), new Vector3(r * 0.55f, h * 0.22f, r * 0.55f));
            }
        }

        // ================================================================== animation
        static float Smooth(float a, float b, float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        static void SampleIdle(Shot s, float t)
        {
            var go = s.Anim.gameObject;
            if (s.Idle) s.Idle.SampleAnimation(go, (t + 0.4f) % s.Idle.length);
            s.Kid.transform.SetPositionAndRotation(s.StandPos == default ? s.Kid.transform.position : s.StandPos, s.StandRot);
            if (go.transform != s.Kid.transform) go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        static void Pose(Shot s, float t)
        {
            // camera: low, at kid height, on her knocking side, slowly pushing in
            float c = Smooth(0f, Seconds, t);
            var camPos = Vector3.Lerp(new Vector3(-1.5f, 1.1f, 1.65f), new Vector3(-1.0f, 0.98f, 1.15f), c);
            var look = Vector3.Lerp(new Vector3(0.02f, 0.86f, 0.08f), new Vector3(0.08f, 0.8f, 0.15f), c);
            s.Cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(look - camPos));

            // body: idle, then turning toward the camera at the end
            float turn = Smooth(3.45f, 4.25f, t);
            var toCam = Vector3.ProjectOnPlane(camPos - s.StandPos, Vector3.up).normalized;
            float yawToCam = Vector3.SignedAngle(Quaternion.Euler(0, 180f, 0) * Vector3.forward, toCam, Vector3.up);
            s.StandRot = Quaternion.Euler(0, 180f + yawToCam * 0.45f * turn, 0);
            SampleIdle(s, t);
            var a = s.Anim;
            var head = a.GetBoneTransform(HumanBodyBones.Head);
            var neck = a.GetBoneTransform(HumanBodyBones.Neck);

            // knocking arm (right): raise 0.85–1.35 s, three knocks, lower 2.6–3.1 s
            float w = Smooth(0.85f, 1.35f, t) * (1f - Smooth(2.6f, 3.1f, t));
            var spine = a.GetBoneTransform(HumanBodyBones.Chest) ?? a.GetBoneTransform(HumanBodyBones.Spine);
            float jab = 0f;
            foreach (var k in Knocks) jab += Mathf.Exp(-Mathf.Abs(t - k) * 16f);
            if (spine) spine.rotation = Quaternion.AngleAxis((8f + 2f * jab) * w, s.Kid.transform.right) * spine.rotation;
            var up = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var lo = a.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var hand = a.GetBoneTransform(HumanBodyBones.RightHand);
            if (w > 0.001f && up && lo && hand)
            {
                float back = 0.06f;   // fist pulled back (and up: a wrist flick) between knocks
                foreach (var k in Knocks) back = Mathf.Min(back, 0.06f * Mathf.Pow(Mathf.Clamp01(Mathf.Abs(t - k) / 0.17f), 0.75f));
                // out to her right side with the elbow winged, so the knock reads in silhouette past her big head
                var knock = up.position + s.Kid.transform.right * 0.08f;
                knock = new Vector3(knock.x, up.position.y + 0.05f + back * 0.7f, s.DoorFrontZ + 0.035f + back);
                var target = Vector3.Lerp(hand.position, knock, w);
                var right = s.Kid.transform.right;
                var pole = up.position + Vector3.down * 0.12f + right * 0.45f + s.Kid.transform.forward * -0.05f;
                TwoBone(up, lo, hand, target, pole);
                if (Mathf.Abs(t - Knocks[0]) < 0.02f) Debug.Log($"[Frost Render] knock: fist {hand.position} target {knock}");
            }

            // head: eyes on the door, then a look back at us
            if (head)
            {
                var toDoor = (new Vector3(0, 1.4f, 0) - head.position).normalized;
                var fwd = s.Kid.transform.forward;
                var lookDoor = Quaternion.FromToRotation(fwd, Vector3.Slerp(fwd, toDoor, 0.5f));
                var toCamH = (camPos - head.position).normalized;
                var lookCam = Quaternion.FromToRotation(fwd, Vector3.Slerp(fwd, toCamH, 0.85f));
                float doorW = Smooth(0.4f, 1.1f, t) * (1f - turn);
                var q = Quaternion.Slerp(Quaternion.identity, lookDoor, doorW);
                q = Quaternion.Slerp(q, lookCam, Smooth(3.6f, 4.35f, t));
                // a tiny nod with every knock
                float nod = 0f;
                foreach (var k in Knocks) nod += Mathf.Exp(-Mathf.Abs(t - k) * 14f);
                q = Quaternion.AngleAxis(3f * nod, s.Kid.transform.right) * q;
                head.rotation = q * head.rotation;
            }

            // face: "O" while knocking, a smirk at the camera
            Texture face = s.FaceBase;
            if (t > 1.45f && t < 2.5f && s.FaceO) face = s.FaceO;
            if (t > 3.75f && s.FaceSmirk) face = s.FaceSmirk;
            s.FaceMat.SetTexture("_BaseMap", face);
            Bake(s);

            // the door rattles on its hinge
            float shake = 0f;
            foreach (var k in Knocks) if (t >= k) shake += Mathf.Exp(-(t - k) * 22f) * Mathf.Sin((t - k) * 75f);
            s.Door.localRotation = Quaternion.Euler(0, -1.1f * shake, 0);

            // weather
            foreach (var (tr, p0, speed, phase) in s.Flakes)
            {
                float y = Mathf.Repeat(p0.y - speed * t, 4.2f);
                tr.position = new Vector3(p0.x + Mathf.Sin(t * 1.3f + phase) * 0.06f, y, p0.z + Mathf.Cos(t * 0.9f + phase) * 0.05f);
            }
            foreach (var (tr, p0, v0, born) in s.Clumps)
            {
                float u = t - born;
                bool on = u >= 0f;
                if (!on) { tr.gameObject.SetActive(false); continue; }
                var p = p0 + v0 * u + 0.5f * Physics.gravity * u * u;
                if (p.y < 0.18f) p.y = 0.18f;   // settles on the step
                tr.gameObject.SetActive(true);
                tr.position = p;
            }
        }

        static void Bake(Shot s)
        {
            s.Smr.BakeMesh(s.Baked, true);
            s.Baked.RecalculateBounds();
            s.BakedT.SetPositionAndRotation(s.Smr.transform.position, s.Smr.transform.rotation);
        }

        /// <summary>Analytic two-bone IK in world space (axis-agnostic, so it works on any rig's bone orientations).</summary>
        static void TwoBone(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
        {
            Vector3 a = upper.position, b = lower.position, c = end.position;
            float l1 = (b - a).magnitude, l2 = (c - b).magnitude;
            var at = target - a;
            float d = Mathf.Clamp(at.magnitude, 0.02f, l1 + l2 - 0.002f);
            var dir = at.normalized;
            float x = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
            float y = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
            var poleDir = Vector3.ProjectOnPlane(pole - a, dir).normalized;
            var elbow = a + dir * x + poleDir * y;
            upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * upper.rotation;
            b = lower.position; c = end.position;
            lower.rotation = Quaternion.FromToRotation(c - b, a + dir * d - b) * lower.rotation;
        }

        // ================================================================== sound
        /// <summary>Three wooden knocks (a low thump + a short bright tap) over a faint winter hush.</summary>
        static NativeArray<float> KnockAudio(int count)
        {
            var buf = new NativeArray<float>(count, Allocator.Persistent);
            var rng = new System.Random(3);
            float hush = 0f;
            for (int i = 0; i < count; i++)
            {
                hush = hush * 0.995f + (float)(rng.NextDouble() * 2 - 1) * 0.005f;   // brown-ish wind
                buf[i] = hush * 0.6f;
            }
            foreach (var k in Knocks)
            {
                int s0 = Mathf.RoundToInt(k * SampleRate);
                float lp = 0f;
                for (int j = 0; j < SampleRate / 8 && s0 + j < count; j++)
                {
                    float t = j / (float)SampleRate;
                    float body = Mathf.Sin(2f * Mathf.PI * 145f * t) * Mathf.Exp(-t * 38f) * 0.75f
                               + Mathf.Sin(2f * Mathf.PI * 310f * t) * Mathf.Exp(-t * 60f) * 0.35f;
                    lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.35f;
                    float tap = lp * Mathf.Exp(-t * 180f) * 0.6f;
                    buf[s0 + j] += (body + tap) * 0.8f;
                }
            }
            return buf;
        }
    }
}
