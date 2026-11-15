using UnityEngine;

namespace FrostAndFound
{
    public enum FaceExpression { Neutral, Talk, Wide, O, Smirk, Frown, Smile, Dizzy }

    /// <summary>
    /// Swaps a rigged kid's whole colour texture for a painted face variant (Resources/Faces/&lt;Name&gt;/&lt;Name&gt;_Face_*.png):
    /// mouth flaps while they talk, a held expression after each line, a quick "O" when photographed, and after a
    /// snowball hit (or while Tony is dazed) spiral eyes, a head rolling round in circles and stars orbiting it.
    /// Kids without face textures are untouched.
    /// </summary>
    public class KidFaces : MonoBehaviour
    {
        static readonly string[] Keys = { null, "MouthTalk", "MouthWide", "MouthO", "MouthSmirk", "MouthFrown", "MouthSmile", "Dizzy" };
        // talking: which mouth shape comes up next (weights), so it reads as speech rather than a metronome
        static readonly (FaceExpression face, float weight)[] Flaps =
            { (FaceExpression.Talk, 0.42f), (FaceExpression.Wide, 0.26f), (FaceExpression.Neutral, 0.2f), (FaceExpression.O, 0.12f) };

        Kid m_Kid;
        Material m_Mat;
        Texture m_Base;
        readonly Texture[] m_Tex = new Texture[Keys.Length];
        FaceExpression m_Hold, m_Flap, m_Rest = FaceExpression.Neutral;
        float m_HoldUntil, m_DizzyUntil, m_RestUntil, m_NextFlap;
        bool m_WasSpeaking;
        Texture m_Shown;
        float m_DizzyAmt, m_StarAmt;
        StunStarRing m_Stars;

        public static KidFaces Attach(Kid kid, Renderer renderer)
        {
            if (!kid || !renderer || !renderer.sharedMaterial) return null;
            string name = kid.id.ToString();
            var f = kid.gameObject.AddComponent<KidFaces>();
            int found = 0;
            for (int i = 1; i < Keys.Length; i++)
                if ((f.m_Tex[i] = Resources.Load<Texture2D>($"Faces/{name}/{name}_Face_{Keys[i]}"))) found++;
            if (found == 0) { Destroy(f); return null; }
            f.m_Kid = kid;
            f.m_Mat = renderer.sharedMaterial;   // each kid has its own material instance
            f.m_Base = f.m_Mat.GetTexture("_BaseMap");
            f.m_Shown = f.m_Base;
            DialogueRunner.LineStarted += f.OnLine;
            Debug.Log($"[Frost] {name}: {found} face textures");
            return f;
        }

        void OnDestroy()
        {
            DialogueRunner.LineStarted -= OnLine;
            if (m_Stars) Destroy(m_Stars.gameObject);
        }

        /// <summary>Hold an expression for a while (overrides talking).</summary>
        public void Show(FaceExpression face, float seconds)
        {
            m_Hold = face;
            m_HoldUntil = Time.time + seconds;
        }

        public void Dizzy(float seconds) => m_DizzyUntil = Mathf.Max(m_DizzyUntil, Time.time + seconds);

        void OnLine(Kid speaker, Line line)
        {
            if (speaker != m_Kid || line == null) return;
            m_Rest = RestFor(line.Id);
        }

        /// <summary>The face a kid settles into after a line (Lucy is smug unless something's gone wrong).</summary>
        static FaceExpression RestFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return FaceExpression.Smirk;
            if (id.Contains("case_closed") || id.Contains("_self_")) return FaceExpression.Smile;   // they love a photo of themselves
            if (id.Contains("_hit") || id.Contains("_snow") || id.Contains("_again") || id.Contains("_tamper") || id.Contains("_pose")
                || id.Contains("_frosty") || id.Contains("_nothing") || id.Contains("_blur"))
                return FaceExpression.Frown;
            return FaceExpression.Smirk;
        }

        float RestSeconds => m_Rest == FaceExpression.Smile ? 4f : 1.8f;

        void Update()
        {
            if (!m_Mat) return;
            float now = Time.time;
            bool speaking = DialogueRunner.IsSpeaking(m_Kid.id);
            if (m_WasSpeaking && !speaking) m_RestUntil = now + RestSeconds;
            m_WasSpeaking = speaking;

            // dizzy: spiral eyes, the head rolling round, and stars circling (Tony's daze has its own birdies + stars)
            bool hitDizzy = now < m_DizzyUntil, dazed = m_Kid is TonyKid tony && tony.IsDazed;
            m_DizzyAmt = Mathf.MoveTowards(m_DizzyAmt, hitDizzy || dazed ? 1f : 0f, Time.deltaTime / 0.3f);
            if (m_Kid.Anim) m_Kid.Anim.Dizzy = m_DizzyAmt;
            m_StarAmt = Mathf.MoveTowards(m_StarAmt, hitDizzy && !dazed ? 1f : 0f, Time.deltaTime / 0.25f);
            if (m_StarAmt > 0f && !m_Stars) m_Stars = StunStarRing.Create(m_Kid);
            if (m_Stars) m_Stars.Amount = m_StarAmt;

            FaceExpression face;
            if (hitDizzy || dazed) face = FaceExpression.Dizzy;
            else if (now < m_HoldUntil) face = m_Hold;
            else if (speaking)
            {
                if (now >= m_NextFlap)
                {
                    m_Flap = NextFlap(m_Flap);
                    m_NextFlap = now + Random.Range(0.07f, 0.14f);
                }
                face = m_Flap;
            }
            else if (now < m_RestUntil) face = m_Rest;
            else face = FaceExpression.Neutral;

            var tex = m_Tex[(int)face] ? m_Tex[(int)face] : m_Base;
            if (tex != m_Shown)
            {
                m_Mat.SetTexture("_BaseMap", tex);
                m_Shown = tex;
            }
        }

        static FaceExpression NextFlap(FaceExpression last)
        {
            for (int tries = 0; tries < 4; tries++)
            {
                float r = Random.value, acc = 0f;
                foreach (var (f, w) in Flaps)
                {
                    acc += w;
                    if (r <= acc) { if (f != last) return f; break; }
                }
            }
            return last == FaceExpression.Talk ? FaceExpression.Wide : FaceExpression.Talk;
        }
    }
}
