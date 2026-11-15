using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// Plays everything: pooled spatial SFX, voiced lines (recorded clip if present, else babble), the noir score
    /// (ducked under dialogue) and the room tone. Volumes go through a static gain layer (music / sfx / voice).
    /// Real audio overrides: Resources/SFX/&lt;Sfx name&gt;, Resources/Voice/&lt;line id&gt;, Resources/Music/NoirScore.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        static AudioDirector s_I;
        public static bool Ready { get; private set; }

        public static float MusicVolume = 0.32f, SfxVolume = 0.9f, VoiceVolume = 1f;

        readonly Dictionary<Sfx, List<AudioClip>> m_Sfx = new Dictionary<Sfx, List<AudioClip>>();
        readonly Dictionary<string, AudioClip> m_Voice = new Dictionary<string, AudioClip>();
        readonly Dictionary<CastId, (AudioSource src, AudioClip clip)> m_Talking = new Dictionary<CastId, (AudioSource, AudioClip)>();
        static readonly Dictionary<string, bool> s_Recorded = new Dictionary<string, bool>();
        readonly ConcurrentQueue<(string key, float[] data, int kind)> m_Pending = new ConcurrentQueue<(string, float[], int)>();
        readonly List<AudioSource> m_Pool = new List<AudioSource>();
        AudioSource m_Music, m_Room;
        float m_Duck = 1f, m_DuckTarget = 1f;
        int m_Speaking;

        public static void Ensure()
        {
            if (s_I) return;
            var go = new GameObject("[Frost Audio]");
            DontDestroyOnLoad(go);
            s_I = go.AddComponent<AudioDirector>();
        }

        void Awake()
        {
            var t = FrostTuning.I;
            MusicVolume = t.musicVolume; SfxVolume = t.sfxVolume; VoiceVolume = t.voiceVolume;
            m_Music = gameObject.AddComponent<AudioSource>();
            m_Music.loop = true; m_Music.spatialBlend = 0f; m_Music.playOnAwake = false; m_Music.priority = 10;
            m_Room = gameObject.AddComponent<AudioSource>();
            m_Room.loop = true; m_Room.spatialBlend = 0f; m_Room.playOnAwake = false; m_Room.volume = 0.12f;
            for (int i = 0; i < 20; i++) m_Pool.Add(NewSource(i));
            StartCoroutine(Prepare());
        }

        AudioSource NewSource(int i)
        {
            var go = new GameObject("sfx " + i);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 0.6f;
            s.maxDistance = 14f;
            s.dopplerLevel = 0f;
            return s;
        }

        IEnumerator Prepare()
        {
            // real clips first
            foreach (Sfx s in System.Enum.GetValues(typeof(Sfx)))
            {
                var c = Resources.Load<AudioClip>("SFX/" + s);
                if (c) m_Sfx[s] = new List<AudioClip> { c };
            }
            // (Unity's null checks must run on the main thread — resolve everything here, before the worker tasks)
            AudioClip music = FrostAssets.Instance ? FrostAssets.Instance.music : null;
            if (!music) music = Resources.Load<AudioClip>("Music/NoirScore");
            bool synthMusic = !music;

            // synthesise everything else on worker threads
            var todo = new List<Sfx>();
            foreach (Sfx s in System.Enum.GetValues(typeof(Sfx))) if (!m_Sfx.ContainsKey(s)) todo.Add(s);
            Task.Run(() =>
            {
                try {
                foreach (var s in todo)
                {
                    int variants = (s == Sfx.Footstep || s == Sfx.ScoopCrunch || s == Sfx.Splat) ? 4 : 1;
                    for (int v = 0; v < variants; v++) m_Pending.Enqueue((s.ToString(), Synth.Generate(s, v + 1), 0));
                }
                if (synthMusic) m_Pending.Enqueue(("NoirScore", Synth.NoirScore(), 2));
                } catch (System.Exception e) { m_Pending.Enqueue(("!error", null, -1)); UnityEngine.Debug.LogException(e); }
            });
            var lines = new List<Line>(Lines.All);
            Task.Run(() =>
            {
                foreach (var l in lines)
                    m_Pending.Enqueue((l.Id, Synth.Babble(l.Text, l.Speaker, l.Id.GetHashCode()), 1));
            });

            if (!synthMusic) StartMusic(music);
            m_Room.clip = null;
            float waited = 0f;
            while (waited < 30f)
            {
                int n = 0;
                while (n < 6 && m_Pending.TryDequeue(out var item))
                {
                    n++;
                    if (item.kind == 0)
                    {
                        var s = (Sfx)System.Enum.Parse(typeof(Sfx), item.key);
                        if (!m_Sfx.TryGetValue(s, out var list)) m_Sfx[s] = list = new List<AudioClip>();
                        list.Add(Synth.ToClip(item.key, item.data));
                        if (s == Sfx.RoomTone && !m_Room.isPlaying) { m_Room.clip = list[0]; m_Room.Play(); }
                    }
                    else if (item.kind == 1)
                    {
                        if (!m_Voice.ContainsKey(item.key)) m_Voice[item.key] = Synth.ToClip("babble_" + item.key, item.data);
                    }
                    else if (item.kind == 2) StartMusic(Synth.ToClip("NoirScore", item.data, true));
                }
                if (m_Sfx.Count >= System.Enum.GetValues(typeof(Sfx)).Length) Ready = true;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Ready = true;
        }

        void StartMusic(AudioClip clip)
        {
            m_Music.clip = clip;
            m_Music.volume = 0f;
            m_Music.Play();
        }

        void Update()
        {
            m_DuckTarget = m_Speaking > 0 ? 0.45f : 1f;
            m_Duck = Ease.Damp(m_Duck, m_DuckTarget, 4f, Time.unscaledDeltaTime);
            if (m_Music.clip) m_Music.volume = Ease.Damp(m_Music.volume, MusicVolume * m_Duck * MusicBoost, 2f, Time.unscaledDeltaTime);
            m_Room.volume = 0.1f * SfxVolume;
        }

        public static float MusicBoost = 1f;

        AudioSource Grab()
        {
            foreach (var s in m_Pool) if (!s.isPlaying) return s;
            var n = NewSource(m_Pool.Count);
            m_Pool.Add(n);
            return n;
        }

        AudioClip Clip(Sfx s)
        {
            if (m_Sfx.TryGetValue(s, out var list) && list.Count > 0) return list[Random.Range(0, list.Count)];
            return null;
        }

        // ------------------------------------------------------------------ public API
        public static AudioSource PlayAt(Sfx s, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            if (!s_I) return null;
            var clip = s_I.Clip(s);
            return clip ? s_I.PlayClip(clip, pos, volume * SfxVolume, pitch, true) : null;
        }

        public static AudioSource Play2D(Sfx s, float volume = 1f, float pitch = 1f)
        {
            if (!s_I) return null;
            var clip = s_I.Clip(s);
            return clip ? s_I.PlayClip(clip, Vector3.zero, volume * SfxVolume, pitch, false) : null;
        }

        AudioSource PlayClip(AudioClip clip, Vector3 pos, float volume, float pitch, bool spatial, bool voice = false)
        {
            var src = Grab();
            src.transform.position = pos;
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.loop = false;
            if (voice)
            {
                // voices carry across the room: full volume within 3 m and a gentle fall-off after, a fifth of the line
                // always heard straight, and Unity's own panning for direction (the headset's spatialiser attenuated
                // them so hard you had to stand right next to a kid to hear them)
                src.spatialBlend = spatial ? 0.8f : 0f;
                src.spatialize = false;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 3f;
                src.maxDistance = 25f;
            }
            else
            {
                src.spatialBlend = spatial ? 1f : 0f;
                src.spatialize = spatial && PlayerRig.Active && PlayerRig.Active.IsXR;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 0.6f;
                src.maxDistance = 14f;
            }
            src.Play();
            return src;
        }

        /// <summary>A looping spatial emitter (Dexter's whistle, thermos steam…). Caller owns the returned source.</summary>
        public static AudioSource Loop(Sfx s, Transform parent, float volume)
        {
            var go = new GameObject("loop " + s);
            go.transform.SetParent(parent, false);
            var src = go.AddComponent<AudioSource>();
            src.loop = true; src.spatialBlend = 1f; src.minDistance = 0.5f; src.maxDistance = 8f; src.dopplerLevel = 0f;
            src.volume = volume * SfxVolume;
            src.playOnAwake = false;
            if (s_I) s_I.StartCoroutine(s_I.StartLoopWhenReady(src, s));
            return src;
        }

        IEnumerator StartLoopWhenReady(AudioSource src, Sfx s)
        {
            AudioClip c = null;
            while (src && (c = Clip(s)) == null) yield return null;
            if (!src) yield break;
            src.clip = c;
            src.spatialize = PlayerRig.Active && PlayerRig.Active.IsXR;
            src.Play();
        }

        /// <summary>A booming 2D announcement (the "SNOWBALL FIGHT!" voice) from Resources/Voice/&lt;clipId&gt;, or a deep
        /// babble placeholder until that recording lands. Ducks the music under it. Returns its length.</summary>
        public static float PlayAnnouncer(string clipId, string text)
        {
            if (!s_I) return 1.5f;
            var clip = Resources.Load<AudioClip>("Voice/" + clipId);
            float pitch = 1f;
            if (!clip)
            {
                if (!s_I.m_Voice.TryGetValue(clipId, out clip) || !clip)
                    s_I.m_Voice[clipId] = clip = Synth.ToClip("babble_" + clipId, Synth.Babble(text, CastId.Tony, clipId.GetHashCode()));
                pitch = 0.7f;   // deeper: an announcer, not a kid
            }
            var src = s_I.PlayClip(clip, Vector3.zero, 1f, pitch, false);
            float len = clip.length / pitch;
            s_I.StartCoroutine(s_I.TrackSpeaking(len, src, null));
            return len;
        }

        /// <summary>Is there a recording for this line in Resources/Voice (rather than placeholder babble)?</summary>
        public static bool HasVoice(Line line)
        {
            if (line == null) return false;
            if (!s_Recorded.TryGetValue(line.Clip, out bool has))
                s_Recorded[line.Clip] = has = Resources.Load<AudioClip>("Voice/" + line.Clip) != null;
            return has;
        }

        /// <summary>Plays a voiced line at the speaker. Returns its duration (for subtitles).</summary>
        public static float PlayVoice(Line line, Transform speaker)
        {
            if (!s_I || line == null) return 2f;
            var clip = Resources.Load<AudioClip>("Voice/" + line.Clip);
            if (!clip) s_I.m_Voice.TryGetValue(line.Id, out clip);
            if (!clip)
            {
                // babble not ready yet: synthesise right now (rare: first seconds after boot)
                clip = Synth.ToClip("babble_" + line.Id, Synth.Babble(line.Text, line.Speaker, line.Id.GetHashCode()));
                s_I.m_Voice[line.Id] = clip;
            }
            var src = s_I.PlayClip(clip, speaker ? speaker.position : Vector3.zero, VoiceVolume, 1f, speaker, true);
            if (speaker && src) src.transform.position = speaker.position;
            if (src) s_I.m_Talking[line.Speaker] = (src, clip);
            s_I.StartCoroutine(s_I.TrackSpeaking(clip.length, src, speaker));
            return clip.length;
        }

        IEnumerator TrackSpeaking(float seconds, AudioSource src, Transform follow)
        {
            m_Speaking++;
            float t = 0f;
            while (t < seconds)
            {
                if (follow && src) src.transform.position = follow.position;
                t += Time.deltaTime;
                yield return null;
            }
            m_Speaking--;
        }

        /// <summary>Current loudness of whatever is playing near a speaker (drives mouth flaps).</summary>
        public static bool IsSpeaking => s_I && s_I.m_Speaking > 0;

        /// <summary>For tests: what's loaded and playing.</summary>
        public static string DebugSummary()
        {
            if (!s_I) return "no audio director";
            int clips = 0; float sfxSecs = 0f;
            foreach (var kv in s_I.m_Sfx) foreach (var c in kv.Value) if (c) { clips++; sfxSecs += c.length; }
            int playing = 0; foreach (var src in s_I.m_Pool) if (src.isPlaying) playing++;
            return $"ready={Ready} sfxTypes={s_I.m_Sfx.Count}/{System.Enum.GetValues(typeof(Sfx)).Length} sfxClips={clips} ({sfxSecs:0.0}s) voices={s_I.m_Voice.Count} " +
                   $"music={(s_I.m_Music.clip ? s_I.m_Music.clip.length.ToString("0.0") + "s playing=" + s_I.m_Music.isPlaying + " vol=" + s_I.m_Music.volume.ToString("0.00") : "none")} " +
                   $"room={(s_I.m_Room.clip ? "on" : "off")} activeSources={playing}";
        }

        /// <summary>Cuts off one kid mid-line (recorded or babble), e.g. when they start a new line.</summary>
        public static void StopVoice(CastId who)
        {
            if (!s_I || !s_I.m_Talking.TryGetValue(who, out var t)) return;
            if (t.src && t.src.isPlaying && t.src.clip == t.clip) t.src.Stop();   // the pooled source may have moved on
            s_I.m_Talking.Remove(who);
        }

        public static void StopAllVoices()
        {
            if (!s_I) return;
            foreach (var s in s_I.m_Pool) if (s.isPlaying && s.clip && s.clip.name.StartsWith("babble_")) s.Stop();
            foreach (var kv in s_I.m_Talking) if (kv.Value.src && kv.Value.src.isPlaying && kv.Value.src.clip == kv.Value.clip) kv.Value.src.Stop();
            s_I.m_Talking.Clear();
        }
    }
}
