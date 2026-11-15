using System.Collections;
using UnityEngine;

namespace FrostAndFound
{
    /// <summary>Dexter — whistles innocently at the end of the drip trail; drops a marshmallow when snowballed.</summary>
    public class DexterKid : Kid
    {
        AudioSource m_Whistle;
        ParticleSystem m_Notes;
        bool m_Whistling;

        protected override void Build()
        {
            base.Build();
            if (Anim) Anim.RestRightHand = new Vector3(0.1f, 0.5f, 0.2f);   // carrying his cocoa in front of his belly
            var head = Rig.Head;
            m_Notes = Fx.MakeSystem("Whistle Notes", head, FxTextures.MusicNote, false, 20, true);
            m_Notes.transform.localPosition = new Vector3(0.13f, 0.1f, 0.2f);
            var main = m_Notes.main;
            main.loop = true; main.startLifetime = 1.4f; main.startSpeed = 0.12f; main.startSize = 0.05f;
            main.startColor = new ParticleSystem.MinMaxGradient(Palette.DexterBrown, Palette.Cocoa);
            main.gravityModifier = -0.03f;
            var em = m_Notes.emission; em.enabled = true; em.rateOverTime = 1.6f;
            var sh = m_Notes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.02f;
            var col = m_Notes.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
            col.color = g;
            m_Whistle = AudioDirector.Loop(Sfx.Whistle, head, 0.32f);
            SetWhistling(true);
        }

        public void SetWhistling(bool on)
        {
            m_Whistling = on;
            if (m_Notes) { if (on) m_Notes.Play(); else m_Notes.Stop(); }
            if (m_Whistle) m_Whistle.mute = !on;
        }

        protected override void Update()
        {
            base.Update();
            if (m_Whistle) m_Whistle.mute = !m_Whistling || DialogueRunner.IsSpeaking(id);
            UpdateMug();
        }

        protected override Pose PoseFor() => Pose.HoldUpThermos;

        // ---- putting his cocoa down on a real table (only when Space Setup marked one)
        Transform m_Mug;
        HeldByHandle m_MugHold;
        bool m_MugDown;
        Coroutine m_MugMove;
        float m_PickUpAt;
        Vector3? m_RestHand;

        protected internal override void OnLinger(float seconds)
        {
            if (seconds < 3f) return;   // not worth putting it down
            var room = RoomService.Current;
            if (room == null || m_MugDown || Busy || SnowballFight.Active) return;
            if (!m_Mug && Rig.HandR) { m_Mug = Rig.HandR.Find("Mug"); m_MugHold = m_Mug ? m_Mug.GetComponent<HeldByHandle>() : null; }
            if (!m_Mug || !m_MugHold) return;
            var table = room.FindFurniture(FurnitureKind.Table, transform.position);
            if (table == null || table.TopY - room.FloorY < 0.3f || table.TopY - room.FloorY > 1.15f) return;
            var edge = table.EdgePoint(transform.position, 0f, room.FloorY);
            if (FrostUtil.FlatDistance(edge, transform.position) > 0.7f) return;   // not standing at it
            // a spot on the top just in from the edge nearest him
            var inward = (table.Center - edge).Flat();
            var spot = edge + (inward.sqrMagnitude > 1e-4f ? inward.normalized : transform.forward) * 0.1f;
            spot.y = table.TopY + 0.003f;
            if (!table.Contains(spot, -0.02f)) return;
            m_PickUpAt = Time.time + Mathf.Max(2f, seconds - 1.2f);   // and back in his hand before he moves on
            if (m_MugMove != null) StopCoroutine(m_MugMove);
            m_MugMove = StartCoroutine(SetMugDown(spot));
        }

        IEnumerator SetMugDown(Vector3 spot)
        {
            m_MugDown = true;
            Face(spot + Vector3.up * 0.3f, 0.35f);
            yield return new WaitForSeconds(0.35f);
            m_MugHold.enabled = false;
            m_Mug.SetParent(null, true);   // off the hand bone, or it would wiggle on the table with his idle hand
            var from = m_Mug.position;
            var rot = m_Mug.rotation;
            var flatRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(rot * Vector3.forward, Vector3.up).normalized + Vector3.forward * 1e-4f, Vector3.up);
            float sink = MugBottom();
            yield return Ease.Tween(0.45f, t =>
            {
                var p = Vector3.Lerp(from, spot + Vector3.up * sink, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.05f;
                m_Mug.SetPositionAndRotation(p, Quaternion.Slerp(rot, flatRot, t));
            }, Ease.InOutCubic, false);
            AudioDirector.PlayAt(Sfx.Plop, spot, 0.35f, 1.6f);
            if (Anim) { m_RestHand = Anim.RestRightHand; Anim.RestRightHand = null; }   // hands free
            Debug.Log("[Frost] Dexter puts his cocoa down on the table");
            m_MugMove = null;
        }

        // how far the mug's pivot sits above its base, so it stands on the table rather than in it
        float MugBottom()
        {
            float lowest = float.MaxValue;
            foreach (var r in m_Mug.GetComponentsInChildren<Renderer>()) lowest = Mathf.Min(lowest, r.bounds.min.y);
            return lowest < float.MaxValue ? m_Mug.position.y - lowest : 0.04f;
        }

        IEnumerator PickMugUp()
        {
            if (Anim && m_RestHand.HasValue) Anim.RestRightHand = m_RestHand;
            var from = m_Mug.position;
            var hand = Rig.HandR ? Rig.HandR.position : from;
            yield return Ease.Tween(0.3f, t => m_Mug.position = Vector3.Lerp(from, Rig.HandR ? Rig.HandR.position : hand, t), Ease.InOutCubic, false);
            if (Rig.HandR) m_Mug.SetParent(Rig.HandR, true);
            m_MugHold.enabled = true;   // back in his fist by the handle
            m_MugDown = false;
            m_MugMove = null;
        }

        public override void OnPhotographed(PhotoData photo)
        {
            if (m_MugDown && m_MugMove == null) m_MugMove = StartCoroutine(PickMugUp());   // his pose holds the cocoa up
            base.OnPhotographed(photo);
        }

        void OnDestroy() { if (m_Mug && m_MugDown) Destroy(m_Mug.gameObject); }

        void UpdateMug()
        {
            if (!m_MugDown || m_MugMove != null) return;
            // picks it up again before he wanders off, or the moment anything happens
            if (Time.time > m_PickUpAt || IsMoving || Busy || SnowballFight.Active || (GameManager.I && GameManager.I.State != GameState.Investigating))
                m_MugMove = StartCoroutine(PickMugUp());
        }

        protected override void HitGag()
        {
            // a marshmallow pops out of the thermos and plops into the snow
            var from = Rig.HandR ? Rig.HandR.position + Vector3.up * 0.15f : HeadPoint;
            var m = ProcMesh.Part("Marshmallow", null, ProcMesh.Cylinder(12), Palette.Toon(Palette.Marshmallow, true, 0.3f), from, new Vector3(0.035f, 0.035f, 0.035f));
            m.layer = Layers.Default;
            var c = m.AddComponent<CapsuleCollider>(); c.height = 1f; c.radius = 0.5f;
            var rb = m.AddComponent<Rigidbody>(); rb.mass = 0.02f;
            rb.linearVelocity = (transform.forward * 0.6f + Vector3.up * 1.6f + Random.insideUnitSphere * 0.3f);
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            StartCoroutine(Plop(m));
        }

        IEnumerator Plop(GameObject m)
        {
            yield return new WaitForSeconds(0.55f);
            if (m) AudioDirector.PlayAt(Sfx.Plop, m.transform.position, 0.6f, 1f);
            yield return new WaitForSeconds(14f);
            if (m) Destroy(m);
        }
    }

    /// <summary>Sherman — peace signs for photos; drops his stack of books when snowballed.</summary>
    public class ShermanKid : Kid
    {
        protected override void Build()
        {
            base.Build();
            if (Anim) Anim.LowArms = true;   // no overhead stretches or waves
        }

        protected override Pose PoseFor() => Pose.PeaceSigns;

        protected override void HitGag()
        {
            var books = Rig.HandL ? Rig.HandL.Find("Books") : null;
            if (books) StartCoroutine(DropBooks(books));
        }

        IEnumerator DropBooks(Transform books)
        {
            Busy = true;
            var parts = new System.Collections.Generic.List<(Transform t, Vector3 p, Quaternion r, Transform parent)>();
            foreach (Transform b in books) parts.Add((b, b.localPosition, b.localRotation, b.parent));
            foreach (var (t, _, _, _) in parts)
            {
                t.SetParent(null, true);
                var rb = t.gameObject.AddComponent<Rigidbody>(); rb.mass = 0.3f;
                rb.linearVelocity = Random.insideUnitSphere * 0.5f + Vector3.up * 0.8f;
                rb.angularVelocity = Random.insideUnitSphere * 6f;
                t.gameObject.AddComponent<BoxCollider>();
            }
            yield return new WaitForSeconds(0.45f);
            AudioDirector.PlayAt(Sfx.BookThud, transform.position, 0.8f, 1f);
            yield return new WaitForSeconds(2.6f);
            // tidies them back up, muttering
            foreach (var (t, p, r, parent) in parts)
            {
                if (!t) continue;
                Destroy(t.GetComponent<Rigidbody>());
                Destroy(t.GetComponent<BoxCollider>());
                var from = t.position; var fromR = t.rotation;
                t.SetParent(parent, true);
                var lp = t.localPosition; var lr = t.localRotation;
                yield return Ease.Tween(0.25f, k => { t.localPosition = Vector3.Lerp(lp, p, k); t.localRotation = Quaternion.Slerp(lr, r, k); }, Ease.OutCubic, false);
            }
            Busy = false;
        }
    }

    /// <summary>Serena — hair flip for photos; her enormous pom-pom hat flies off when snowballed ("My HAT!").</summary>
    public class SerenaKid : Kid
    {
        protected override void Build()
        {
            base.Build();
            if (Anim) Anim.LowArms = true;   // no overhead stretches or waves
        }

        protected override Pose PoseFor() => Pose.HairFlip;

        protected override void HitGag()
        {
            AudioDirector.PlayAt(Sfx.Gasp, HeadPoint, 0.6f, 1.2f);
            StartCoroutine(HatHop());
        }

        IEnumerator HatHop()
        {
            var parts = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in Rig.Head) if (t.name == "Beanie" || t.name == "Cuff" || t.name == "Pom") parts.Add(t);
            var start = new System.Collections.Generic.List<(Vector3 p, Quaternion r)>();
            foreach (var t in parts) start.Add((t.localPosition, t.localRotation));
            float spin = Random.Range(-1f, 1f) > 0 ? 1f : -1f;
            yield return Ease.Tween(0.35f, k =>
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    parts[i].localPosition = start[i].p + Vector3.up * 0.35f * k + Vector3.back * 0.05f * k;
                    parts[i].localRotation = Quaternion.Euler(-25f * k, 0, 40f * spin * k) * start[i].r;
                }
            }, Ease.OutCubic, false);
            yield return Ease.Tween(0.45f, k =>
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    parts[i].localPosition = Vector3.Lerp(start[i].p + Vector3.up * 0.35f + Vector3.back * 0.05f, start[i].p, k);
                    parts[i].localRotation = Quaternion.Slerp(Quaternion.Euler(-25f, 0, 40f * spin) * start[i].r, Quaternion.Euler(0, 0, 8f * spin) * start[i].r, k);
                }
            }, Ease.OutBounce, false);
        }
    }

    /// <summary>Lucy — the street's detective and your guide. Blocks the lens for photos; writes your name in her suspect notebook when snowballed.</summary>
    public class LucyKid : Kid
    {
        Transform m_GlanceTarget;
        protected override Pose PoseFor() => Pose.BlockLens;

        protected override void Build()
        {
            base.Build();
            if (Anim) Anim.CalmArms = true;   // a gentle sway: her coat sleeves twist with the arm gestures
        }

        protected override void HitGag()
        {
            StartCoroutine(Scribble());
        }

        IEnumerator Scribble()
        {
            Busy = true;
            Anim.StrikePose(Pose.Shrug, 1.0f);   // a shrug, not folded arms
            AudioDirector.PlayAt(Sfx.Scribble, Rig.HandL ? Rig.HandL.position : HeadPoint, 0.7f, 1f);
            yield return new WaitForSeconds(1.5f);
            Busy = false;
        }

        /// <summary>Squints at whoever's next (a vague nudge, not an answer).</summary>
        public IEnumerator SquintAt(Transform target, float seconds)
        {
            Anim.LookTarget = target;
            Anim.SetSquint(0.8f);
            yield return new WaitForSeconds(seconds);
            Anim.SetSquint(0f);
            Anim.LookTarget = PlayerRig.Active ? PlayerRig.Active.Head : null;
        }

        /// <summary>Glances pointedly at the snow (finale hint; no glowing markers).</summary>
        public IEnumerator GlanceAtSnow(float seconds)
        {
            if (!m_GlanceTarget) m_GlanceTarget = new GameObject("Lucy Glance").transform;
            var room = RoomService.Current;
            m_GlanceTarget.position = transform.position + transform.forward * 0.55f + Vector3.up * ((room != null ? room.FloorY : 0f) + 0.02f - transform.position.y);
            Anim.LookTarget = m_GlanceTarget;
            yield return new WaitForSeconds(seconds);
            Anim.LookTarget = PlayerRig.Active ? PlayerRig.Active.Head : null;
        }

        public void JabAt(Vector3 target) => Anim.Point(target, 1.1f, true);
    }
}
