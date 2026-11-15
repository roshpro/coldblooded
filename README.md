# Cold Blooded

> A murder mystery in your own living room. The victim is a snowman, the suspects are five kids, and your only tools are a Polaroid camera and a snowball.

Mixed-reality game for **Meta Quest 3 / 3S**, made for **Immerse the Bay**. Built with **Unity 6000.6.9f1 · URP · Meta XR All-in-One SDK v209** (Core, Interaction, MR Utility Kit, Audio, Haptics, Platform) on the OpenXR provider, plus **Unity OpenXR Meta 2.7** for the Depth API.

Your real room fills with snow, snowy redwoods and a toppled snowman. You photograph clues, show the photos to the right kid, follow the chain of excuses to the culprit, and catch him on film. All five kids are fully voiced (ElevenLabs).

---

## The game in one minute

1. **The Knock.** On the title screen your room is untouched: just poke the floating **Play** button. A door appears (on your *real* door if Space Setup marked one) and someone knocks. Lucy wants you out building a snowman. Snow starts falling and piles up across your floor, and the trees rise out of it. Then the music stops: *THUD*. Frosty is lying in the snow in the middle of the room. Lucy bursts in, sirens wash the room red and blue, and your real room drains to **film-noir grey** until the case is closed. Hold the menu button to skip the intro.
2. **Investigate.** Each clue on (or near) Frosty leads to a kid: a cocoa splash → Dexter, carrot bits → Sherman, purple glitter → Serena, a red scrape → Lucy, a dented sled → Tony. **Photograph** the clue, then **show** the photo to that kid. They lean in, squint, make an excuse, and point you to the next clue, which starts to glint. Show the wrong kid and you get a joke and a nudge.
3. **The finale.** Tony refuses to be photographed. He bolts, zig-zags, dodges your viewfinder and ducks behind your real sofa or table. Bonk him with a snowball, and while he's dazed, take the **GUILTY** photo.
4. **CASE CLOSED.** The colour floods back into your room, the northern lights blaze overhead, and your photos are taped into a scrapbook detective journal with your stats.

Throw snowballs at the kids for fun, but hit the same kid three times and it's a **SNOWBALL FIGHT**: an alarm, an announcer, and everyone pelting you (and each other).

---

## Run it

### On a Quest 3 / 3S
1. Run **Space Setup** first (Settings ▸ Physical Space ▸ Space Setup) and mark your walls, **doors** and **furniture**. The game uses all of it. Allow the *spatial data* permission when asked.
2. **File ▸ Build Profiles ▸ Android**, then **Build And Run** with the headset plugged in (developer mode on). The project is already configured: OpenXR + Meta XR features, IL2CPP, ARM64, Vulkan, single-pass instanced stereo, foveation, passthrough, hand tracking, and the AR Session and AR Occlusion features for the Depth API.
3. Play in a clear space of **at least 3 × 3 m**. The layout is generated fresh every round and scales to your room.

| On the headset | Hand tracking | Controllers |
|---|---|---|
| Start | Poke the **Play** button with a fingertip | Poke it with the controller tip |
| Raise the Polaroid (camera hand) | Bring thumb and index together (about 6–9 cm) | Hold **grip** |
| Take a photo | Pinch | **Index trigger** |
| Get the print | It hangs from the slot at the bottom of the camera; the next shot, or putting the camera away, drops it in the snow | Same |
| Show a photo | **Face** the kid and **push** the photo toward them (slowly is fine) | Same |
| Drop the photo in your hand | Open your hand flat | **A / X** |
| Pick a photo up | Reach down to it (it glows softly) and grab | Reach and squeeze **grip** |
| Make a snowball | Free hand open near the snow, then close it | **Grip** near the snow |
| Throw | Swing and open your hand | Swing and release **grip** |
| Pause, or skip the intro | Menu gesture | Menu button (hold it to skip the intro) |

Lefty mode in the pause menu swaps which hand holds the camera.

### In the Editor (no headset needed)
1. Open `Assets/FrostAndFound/Scenes/FrostAndFound.unity` in Unity **6000.6.9f1** and press **Play**. With no headset connected, the game runs in a **desktop simulation** inside a furnished stand-in living room. It has a door, a sofa, a coffee table, a shelf and a lamp, so the door and furniture features work there too.
2. **Frost & Found ▸ Export Placeholder Audio (WAV)** writes the synthesized sounds and music to `AudioPreview/`.

| Desktop | Action |
|---|---|
| Click the Game view | Capture the mouse (Esc releases it) |
| Mouse / WASD or arrows / C or Ctrl | Look / walk / crouch |
| Space or Enter | Press Play on the title |
| Left click | Polaroid shutter (also presses buttons you aim at) |
| Hold **F** | Push your photo toward the kid in front of you |
| **G** | Drop your photo / pick up the nearest one |
| **Q** | Scoop a snowball |
| Hold and release right click | Wind up and throw |
| Tab / P | Swap handedness / pause |

---

## Mixed reality: how it uses your room

| Feature | Where it lives |
|---|---|
| Room from Space Setup: floor, walls, ceiling, **doors and furniture** (tables, couches, beds, storage, screens). Snowballs splat on your real walls and furniture | `Platform/MetaRoomProvider.cs`, `Room/RoomInfo.cs` |
| **Real-world occlusion** (Meta Depth API): a real person or piece of furniture in front of a kid, the snow or a snowball hides it. Your held camera and photos and the aurora are exempt, and so are the UI and the Polaroid's lens | `Shaders/FrostOcclusion.hlsl`, `Core/DepthOcclusion.cs` |
| **The room tells the story** with passthrough colour grading: noir for the whole case, a flash when you take a photo, icy blue in a snowball fight (red pulses with the alarm), colour back at CASE CLOSED | `Game/PassthroughMood.cs` (runtime LUTs), `RoomTint` in `Game/OpeningSequence.cs` |
| The opening's door stands on your real door when there is one | `Game/OpeningSequence.cs`, `GameManager.BeginCase` |
| Kids use your furniture: Tony hides behind your sofa or table in the chase, Dexter puts his cocoa down on a table, and everyone wanders over to look at things | `Characters/TonyKid.cs`, `Characters/Suspects.cs`, `Characters/KidBrain.cs` |
| Northern lights fade in with the snow and stay up all game; a night veil darkens your real ceiling beneath them | `Game/NorthernLights.cs` |
| The Polaroid can't see passthrough, so photos show a snowy backdrop only the lens can see | `PhotoBackdrop` in `Game/GameManager.cs` |

---

## How it's built

| Area | Where it lives |
|---|---|
| The clue chain (photograph → show the right kid → excuse → next clue glints), wrong-kid lines, Lucy's hints | `Scripts/Story/CaseDirector.cs`, `Lines.cs`, `DialogueRunner.cs` |
| The opening sequence ("The Knock") | `Scripts/Game/OpeningSequence.cs` |
| Polaroid: viewfinder, flash, the print slot, clue detection, developing photos | `Scripts/Photo/PolaroidCamera.cs`, `Photo.cs` |
| Showing photos (face the kid, push it forward; the kid your clue points to is favoured; one kid at a time) | `Scripts/Game/ShowPhotoDetector.cs` |
| Snowballs, scooping, aim assist, wall splats | `Scripts/Snow/` |
| The kids: rigged Meshy models with IK, faces, poses, stun; personalities and an AI that picks what to do next; A* walking round furniture with personal space | `Scripts/Characters/` (`Kid.cs`, `KidBrain.cs`, `KidHumanoid.cs`, `Suspects.cs`, `TonyKid.cs`) |
| A new layout every round: Frosty behind you, the kids, sled and toys placed fresh; trees always along the walls | `Scripts/Game/LayoutDirector.cs`, `Scenery.cs` |
| Snowball fights (three hits on one kid starts one) | `Scripts/Game/SnowballFight.cs` |
| Case-closed journal | `ResultsScreen` in `Scripts/UI/Screens.cs` |
| Audio: synthesized SFX and music, the Satie soundscape when installed, and 3D voices that follow each kid's head | `Scripts/Audio/` |
| UI (Horizon-style menus, gesture hint cards, world-space text); your hands draw over the UI | `Scripts/UI/` |

### Voices
All lines live in `Scripts/Story/Lines.cs`. Clips are `Assets/FrostAndFound/Resources/Voice/<id>.mp3` and play instead of the placeholder babble; subtitles time themselves to the clip. `voice_lines_todo.csv` lists anything still unrecorded: only the announcer's "SNOWBALL FIGHT!" at the moment.

### Sound effects and music
Drop a file into `Resources/SFX/` named after the sound (e.g. `Knock.wav`, `Thud.wav`, `Jingle.wav`) to replace the synthesized one; the noir score goes in `Resources/Music/NoirScore`. Anything missing stays synthesized.

### Tuning
`Resources/FrostTuning.asset` holds the numbers worth playtesting: clue range, show range and arm extension, scoop height and grip thresholds, throw multipliers and aim assist, Tony's speeds and daze time, passthrough dim, and volumes.

---

## Project layout
```
Assets/FrostAndFound/
  Scenes/FrostAndFound.unity    the game (Meta XR rig + MRUK)
  Scripts/Core                  palette, procedural meshes, FX, tuning, depth occlusion
  Scripts/Input                 PlayerRig abstraction, desktop simulation
  Scripts/Platform              Meta XR rig (hands, controllers, passthrough) + MRUK room
  Scripts/Room Snow Photo Story Characters Frosty Audio UI Game
  Shaders/                      toon, snow, sky, photo, particles: single-pass instanced, Depth API occlusion
  Editor/                       Frost & Found ▸ Setup, voice script export, audio export
  Resources/                    FrostAssets, FrostTuning, Voice/, SFX/, Music/, Title/
```
Code, folders and the package id (`com.immersethebay.frostandfound`) still use the original working title, *Frost & Found*.

## Known limitations
- Depth occlusion has soft edges, like the Quest's depth sensor itself; held items and anything within about 45 cm of your eyes are never occluded.
- Without doors or furniture in Space Setup, the opening uses a free-standing door and the furniture features stay off.
- If you recompile scripts *during* Play mode in the Editor, MRUK can log "Global context has already been created" on the next play. It's harmless: stop Play before editing code.

## License
The code is released under the [MIT License](LICENSE). Some assets in the repo keep their own terms: the fonts (see `Assets/FrostAndFound/Fonts/LICENSES.md`), the ElevenLabs voice clips and the Meshy character models (each under that service's terms). The Meta XR SDK, MRUK and Unity packages aren't stored here and stay under their own licenses.
