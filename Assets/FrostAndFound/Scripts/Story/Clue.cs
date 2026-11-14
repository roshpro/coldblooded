using System.Collections.Generic;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>
    /// A photographable clue. While it is the current step's clue it gives off a faint glint — subtle enough
    /// that you still have to look for it.
    /// </summary>
    public class Clue : MonoBehaviour
    {
        public static readonly List<Clue> All = new List<Clue>();

        public ClueId id;
        [Tooltip("Point the photo check uses (defaults to this transform).")]
        public Transform focus;
        [Tooltip("Rough size of the clue (m) — used for the circle size and range forgiveness.")]
        public float radius = 0.08f;
        [Tooltip("Surface clues (stains, scrapes) only count from the front.")]
        public bool useFacing;
        public Vector3 localNormal = Vector3.forward;
        [Tooltip("Colliders under this root don't block the photo of this clue.")]
        public Transform owner;
        [Tooltip("Extra points that glint together with the clue (e.g. stains on Sherman's suit).")]
        public List<Transform> extraGlints = new List<Transform>();
        public Color glintTint = new Color(1f, 0.98f, 0.9f, 1f);
        public float glintSize = 0.045f;

        public bool IsActive { get; private set; }
        public bool IsSolved { get; private set; }
        public bool CountsAsEvidence => IsActive || IsSolved;
        public Vector3 FocusPoint => focus ? focus.position : transform.position;
        public Vector3 FacingNormal => (focus ? focus : transform).TransformDirection(localNormal).normalized;

        float m_Next;
        float m_NextTwinkleSound;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public static Clue Find(ClueId id)
        {
            foreach (var c in All) if (c && c.id == id) return c;
            return null;
        }

        public void SetSparkling(bool on)
        {
            IsActive = on;
            m_Next = Time.time + 0.2f;
            m_NextTwinkleSound = Time.time + Random.Range(1.5f, 3f);
        }

        public void MarkSolved() { IsSolved = true; IsActive = false; }

        void Update()
        {
            if (!IsActive) return;
            if (Time.time >= m_Next)
            {
                m_Next = Time.time + Random.Range(0.45f, 1.2f);
                var basePt = FocusPoint;
                if (extraGlints.Count > 0 && Random.value < 0.45f) basePt = extraGlints[Random.Range(0, extraGlints.Count)].position;
                var p = basePt + Random.insideUnitSphere * radius * 0.7f;
                if (useFacing) p += FacingNormal * 0.01f;
                Fx.GlintAt(p, glintSize * Random.Range(0.75f, 1.25f), glintTint);
            }
            if (Time.time >= m_NextTwinkleSound)
            {
                m_NextTwinkleSound = Time.time + Random.Range(3.5f, 6f);
                AudioDirector.PlayAt(Sfx.Twinkle, FocusPoint, 0.18f, Random.Range(0.95f, 1.1f));
            }
        }
    }
}
