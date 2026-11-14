# Frost & Found

> A murder mystery where the victim is a snowman, the suspects are knee-high, and your only tools are a Polaroid camera and a snowball.

Mixed-reality game for **Meta Quest 3** (Immerse the Bay). Built with **Unity 6000.6.9f1 · URP · Meta XR All-in-One SDK v209** (Core, Interaction, MR Utility Kit, Audio, Haptics, Platform, Voice) on the OpenXR provider Meta recommends for Unity 6.

Everything in the design doc is playable now with mockup art and synthesized placeholder audio. Final Blender models and ElevenLabs voices drop in without code changes (see [Swapping in final assets](#swapping-in-final-assets)).

---

## Run it

### In the Editor (no headset needed)
1. Open the project in Unity **6000.6.9f1**. Open `Assets/FrostAndFound/Scenes/FrostAndFound.unity`.
2. Press **Play**. With no headset connected, the game runs in a **desktop simulation** inside a furnished stand-in living room.
3. Or use **Frost & Found ▸ Play With Autopilot** to watch the perfect run play itself (13 actions, about 1:35). It's handy for smoke-testing and capturing demo footage.
4. To try a tight venue, set **Simulated Room Size** on the `Frost & Found` GameManager (3 × 3 m works).
5. To hear the placeholder audio outside Unity, use **Frost & Found ▸ Export Placeholder Audio (WAV)**. It writes to `AudioPreview/`.

| Desktop control | Action |
|---|---|
| Click the Game view | Capture the mouse (Esc releases it) |
| Mouse / WASD / C (or Ctrl) | Look / walk / crouch |
| Left click | Polaroid shutter (also presses buttons you aim at) |
| Hold **F** | Hold your photo out to the kid in front of you |
| **Q** | Reach down and scoop a snowball |
| Hold and release right click | Wind up and throw |
| Tab / P or Esc | Swap handedness / pause |

### On a Quest 3
1. Run **Space Setup** on the headset first (Settings ▸ Physical Space ▸ Space Setup). The game reads your floor, walls and furniture from it. Allow the *spatial data* permission when asked.
2. **File ▸ Build Profiles ▸ Android**, then **Build And Run** with the headset plugged in (developer mode on). The project is already configured: OpenXR + Meta XR feature group, IL2CPP, ARM64, Vulkan, min API 32, target API 34, single-pass instanced stereo, foveation, passthrough, hand tracking.
3. Play in a clear space of **at least 3 × 3 m**. The layout scales to your room.

| Quest control | Action |
|---|---|
| Camera hand (right; left in **Lefty mode**): **trigger** or **index pinch** | Take a photo. The photo pops out and lands in your other hand |
| Free hand: hold it **out toward a kid** (within ~1.5 m) | Show your newest photo |
| Free hand: reach to the snow **open**, then **close** (grip / fist) | Pack a snowball (your photo tucks away) |
| Swing and **open** your hand | Throw (real release velocity, light aim assist) |
| Point + trigger/pinch, or **poke** with a fingertip | Press UI buttons |
| Menu button | Pause menu (lefty mode, subtitles, tips, restart) |

Controllers and hand tracking both work, and can be mixed per hand. The doc recommends controllers for the most reliable throws.

---

## What's in the build (mapped to the design doc)

| Design doc | Where it lives |
|---|---|
| §3 The clue chain: photograph → show the right kid → excuse → next clue glints | `Scripts/Story/CaseDirector.cs` (5-step state machine), `Clue.cs` (faint glint) |
| Step 1's two extra cues: drip trail to Dexter (sparkles after the stain photo), Dexter whistling innocently, matching browns | `Frosty/Frosty.cs` (`DripTrail`), `Characters/Suspects.cs` (`DexterKid`) |
| §4 The five suspects, colour-coded to their clue, ~1 m tall with big heads | `Characters/KidBuilder.cs` (mockups), `Kid.cs`, `Suspects.cs`, `TonyKid.cs` |
| §5.1 Polaroid in your camera hand, live viewfinder, flash + ka-CHUNK, photo pops out developed with a 0.3 s white fade and lands in your other hand. Clue counts if in frame and within ~1.5 m. Red hand-drawn circle, no text. No film limit | `Photo/PolaroidCamera.cs` (`ClueDetector`), `Photo/Photo.cs` |
| §5.2 Hold your photo out to a kid; they lean in, squint and react. Always your newest photo. Wrong kid → joke line + a nudge toward the right one | `Game/ShowPhotoDetector.cs`, `CaseDirector.React` |
| §5.3 Scoop and throw: reach down, close your hand, crunch; the photo tucks away; real throw; light aim assist; for-fun reactions (marshmallow, "My HAT!", Lucy's notebook) | `Snow/SnowballHand.cs`, `Snowball.cs`, `Suspects.cs` |
| Handedness toggle | Title, pause menu (`FrostPrefs.LeftHanded`) |
| §6 Finale: "Circumstantial!", goggles down, he bolts, dodges whenever he's in your viewfinder (photos blur), zig-zags the far side, taunts; Lucy's hint + glance at the snow; BONK + birdies + ~4 s daze; GUILTY photo; outro, kids gather, CASE CLOSED, Case File | `Characters/TonyKid.cs`, `CaseDirector`, `GameManager.Outro`, `UI/Screens.cs` (`ResultsScreen`) |
| §7 Frosty at the centre in a chalk outline; kids around the edges; Tony farthest from you; scaled to detected bounds | `Game/LayoutDirector.cs`, `Frosty/Frosty.cs` (`ChalkOutline`) |
| §7 Snow on the detected floor, light snowfall, your own footprints | `Snow/SnowSurface.cs` (displaced snow, drifts at walls, masked off furniture), `Snowfall.cs`, `FootprintTracker` |
| §7 Sound: noir score, dun-dun-DUN, crunchy footsteps, voiced lines with subtitles | `Audio/Synth.cs`, `Audio/AudioDirector.cs`, `Story/DialogueRunner.cs` |
| Safety nets: Lucy's idle nudge after ~15 s; Tony trips over his own sled if not hit in time | `CaseDirector.Nudge`, `TonyKid.Trip` |
| §9 Passthrough: the virtual camera can't see the real room, so the Polaroid sees a snowy backdrop | `PhotoBackdrop` in `Game/GameManager.cs` |
| §9 Room from Space Setup; walls and furniture get invisible colliders (snowballs splat on your real walls) | `Platform/MetaRoomProvider.cs` (MRUK) |

**Extras we filled in:** each kid strikes a pose when photographed (Sherman's peace signs, Tony's "Get my good side"). There are wrong-kid lines for every clue × kid pair, "we've been over this" lines for re-shown evidence, and Sherman drops his books when hit. Lucy also says "Don't contaminate the crime scene!" if you snowball Frosty. Other fills: a Horizon-style title, pause and results UI with tips, kids leave footprints, Tony's sled leaves runner tracks, and snow splats stay on walls.

---

## Swapping in final assets

Everything plugs into **`Assets/FrostAndFound/Resources/FrostAssets.asset`** (select it in the Project window).

### Characters (Blender → FBX)
1. Export each kid facing **+Z**, origin at the feet, about **1 m tall** (Sherman ~0.9, Lucy ~1.06). Apply transforms.
2. Make a prefab from the FBX and add it to **Character Models**: choose the kid, drag the prefab in, and optionally add an Animator Controller.
3. Name joints so the game can find them. All names are optional; missing ones fall back to the procedural rig: `Head`, `Hand_L`, `Hand_R`, `Eye_L`, `Eye_R`, `Mouth`.
4. Animator parameters the game sets (add the ones you need): `Speed` (float), `Talk` (bool), `Dazed` (bool), and triggers `Point`, `Pose`, `Hit`, `Dodge`. Mixamo clips map well: idle / run / dodge / dizzy / pose.
5. Clue props stay attached by name: Sherman's `Carrot` (step 2 clue) and Tony's sled (`Sled Model` slot). Keep a child named `Carrot` on Sherman's model, or tell us where it should go.

**Frosty:** put the model in **Frosty Model** with children named `Base`, `Middle` and `Head`. The cocoa splash, glitter and scrape attach to those, so check they sit on the visible surface.
**Polaroid camera / sled:** use the **Polaroid Model** and **Sled Model** slots. Lens points +Z; the photo ejects from the front-bottom.

### Voices (ElevenLabs or recorded)
1. Every line is in **`Assets/FrostAndFound/VoiceScript.csv`** (id, speaker, line): 74 lines. Regenerate it with **Frost & Found ▸ Export Voice Script (CSV)**.
2. Save each clip as **`Assets/FrostAndFound/Resources/Voice/<id>.wav`** (or .mp3/.ogg), e.g. `dexter_excuse.wav`. It plays instead of the placeholder babble, and subtitles time themselves to its length.
3. Voice direction from the doc: Dexter is a tiny salesman (every line is a pitch); Sherman, 7, is super nerdy; Serena is dramatic and bratty; Lucy, 11, is bossy; Tony thinks he's in an energy-drink commercial.

### Music and SFX
Drop files into `Resources/SFX/` named after the sound: `Footstep`, `ScoopCrunch`, `Shutter`, `Whirr`, `Whoosh`, `Splat`, `Bonk`, `Tweet`, `Sting` (dun-dun-DUN), `Twinkle`, `Marker`, `StampSlam`, `UiClick`, `UiHover`, `Plop`, `Scribble`, `BookThud`, `Whistle`, `Fanfare`, `RoomTone`, `Boing`, `Gasp`. The score goes in `Resources/Music/NoirScore` (or the **Music** slot). Anything missing stays synthesized.

---

## Tuning
**`Resources/FrostTuning.asset`** holds every number worth playtesting: clue range (1.5 m) and frame forgiveness, show range and arm extension, scoop height and grip thresholds, throw multipliers, aim-assist cone / strength / max angle (kept light, per the doc), Tony's speeds, daze time and trip timer, Lucy's idle nudge time, and the music/SFX/voice volumes.

## Project layout
```
Assets/FrostAndFound/
  Scenes/FrostAndFound.unity       the game (Meta XR rig + MRUK, inactive until a headset is active)
  Scripts/Core                     palette (art bible), procedural meshes, FX, tuning
  Scripts/Input                    PlayerRig abstraction, desktop simulation
  Scripts/Platform                 Meta XR SDK rig (OVRCameraRig/OVRInput/OVRHand) + MRUK room
  Scripts/Room Snow Photo Story Characters Frosty Audio UI Game
  Shaders/                         toon (+ outline), snow, sky, photo, particles: all single-pass-instanced ready
  Editor/FrostProjectSetup.cs      Frost & Found ▸ Setup ▸ Run Full Setup (idempotent)
  Resources/                       FrostAssets, FrostTuning, Voice/, SFX/, Music/
  Fonts/                           Roboto, Roboto Mono (Apache 2.0), Inter (OFL)
```
The doc's team split maps to these folders: **(1)** Photo/ · **(2)** Story/ + Characters/ · **(3)** Characters/TonyKid + Snow/ · **(4)** Snow/ Room/ Audio/.

## Verified so far
- Full case playable start → finish in the Editor simulation. The autopilot closes it in about 1:35 (the doc's perfect run: 6 photos, 5 shows, 1 snowball), in both a 4.4 × 4 m and a 3 × 3 m room, with zero console errors. **Play again** reloads cleanly.
- Android/Quest APK builds (IL2CPP, ARM64, Vulkan): `Builds/FrostAndFound.apk`.
- Render cost per frame in the Editor, both cameras and the desktop room included: about 580 draw calls and 280k triangles. On Quest there's no simulated room and the viewfinder renders every third frame.

## Known limitations
- Not yet run on a physical headset: the XR path is complete and configured but untested on device. Test hand-tracked throws early; controllers are the doc's recommended fallback.
- Virtual objects don't hide behind real furniture (no depth occlusion). The layout keeps kids clear of furniture instead. Meta's Depth API could add occlusion later.
- Photos use the snowy-backdrop fix from the doc. Compositing the real room through the Passthrough Camera API is an optional extra.
- If you recompile scripts *during* Play mode in the Editor, Meta MRUK can log "Global context has already been created" on the next play. It's harmless: stop Play before editing code, or restart the Editor.
