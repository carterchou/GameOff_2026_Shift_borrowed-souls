using System.Collections.Generic;
using BorrowedSouls.Core.Combat;
using BorrowedSouls.Data;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// Ground preview of a vessel's heavy attacks. A faint
    /// frame shows the whole area that will be hit, sized from the attack data, and a brighter copy fills it from the feet outward
    /// during the start-up, reaching the edge as the attack lands. A shape flashes as it fires. The player's own aim is gold, so
    /// it can't be mistaken for an enemy's warning. Cone attacks keep a faint fan under the flame. Meshes are generated once per
    /// shape and reused. A ring attack has its own look, in its own colour: ripples draw in from its reach while it charges and power
    /// gathers at the feet. The wave itself is <see cref="Shockwave"/>; only the mark at the edge of its reach stays here.
    /// </summary>
    public sealed class TelegraphView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Dictionary<int, Mesh> FanCache = new Dictionary<int, Mesh>();
        private static readonly Dictionary<int, Mesh> LineCache = new Dictionary<int, Mesh>();

        /// <summary>Fan edge width as a fraction of its radius. Overlaps the fill, so the edge reads brighter.</summary>
        private const float FanOutline = 0.035f;
        private const float DefaultHeadShare = 0.12f;
        private const int WholeCircle = 360;
        private const int RingSegments = 72;
        private const int RingRows = 4;
        private const int RingBands = 2;

        [SerializeField] private VesselRuntime _vessel;
        [SerializeField] private MeshFilter _filter;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private MeshFilter _fillFilter;
        [SerializeField] private MeshRenderer _fillRenderer;
        [Tooltip("An enemy's warning.")]
        [SerializeField, ColorUsage(true, true)] private Color _color = new Color(1f, 0.25f, 0.1f, 0.5f);
        [Tooltip("The player's own aim.")]
        [SerializeField, ColorUsage(true, true)] private Color _playerColor = new Color(1.1f, 0.85f, 0.4f, 0.75f);
        [Tooltip("Strength of the frame compared with a full fill.")]
        [SerializeField, Range(0f, 1f)] private float _frameShare = 0.4f;
        [Tooltip("Fan strength under a burning cone attack, where 1 is the strength at the moment of impact.")]
        [SerializeField, Range(0f, 1f)] private float _burningConeStrength = 0.3f;
        [Tooltip("Seconds the shape takes to fade once the attack fires.")]
        [SerializeField, Min(0f)] private float _releaseFadeSeconds = 0.14f;
        [SerializeField, Min(0.1f)] private float _lineMinWidth = 0.5f;
        [SerializeField, Min(1f)] private float _lineMaxLength = 14f;
        [Tooltip("Length of the arrowhead, in metres.")]
        [SerializeField, Min(0.1f)] private float _lineHeadLength = 0.75f;

        [Header("Ring attack")]
        [SerializeField, ColorUsage(true, true)] private Color _ringColor = new Color(1.15f, 0.5f, 1.8f, 0.95f);
        [Tooltip("Seconds each ripple takes to draw in from the edge while the attack charges.")]
        [SerializeField, Min(0.05f)] private float _ringRipple = 0.42f;
        [Tooltip("Radius of the glow that gathers at the feet during the start-up, in metres.")]
        [SerializeField, Min(0f)] private float _ringCoreRadius = 2.6f;
        [Tooltip("Share of the wave's travel over which the mark at its reach fades.")]
        [SerializeField, Range(0.01f, 1f)] private float _ringEndFade = 0.12f;

        private MaterialPropertyBlock _block;
        private MaterialPropertyBlock _fillBlock;
        private AttackDefinition _startingUp;
        private float _releaseLeft;
        private Mesh _ringMesh;
        private Vector3[] _ringVerts;
        private Color32[] _ringColours;
        private Vector3[] _ringDirections;
        private float _ringClock;

        /// <summary>How far the fill has reached, 0 to 1, for tests and tools.</summary>
        public float Fill { get; private set; }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _fillBlock = new MaterialPropertyBlock();
            _renderer.enabled = false;
            _fillRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            AttackDefinition attack = null;
            float strength = 0f;
            float fill = -1f; // none
            VesselCombat combat = _vessel.Combat;

            if (!_vessel.IsDead)
            {
                AttackDefinition current = combat.CurrentAttack;
                bool hasShape = current != null && current.Telegraph != TelegraphShape.None;
                if (hasShape && combat.Phase == AttackPhase.Startup)
                {
                    attack = current;
                    strength = 1f;
                    fill = combat.PhaseProgress;
                }
                else if (hasShape && combat.Phase == AttackPhase.Active && (current.Delivery == AttackDelivery.Ring || current.Delivery == AttackDelivery.Cone))
                {
                    attack = current;
                    strength = current.Delivery == AttackDelivery.Ring ? 1f : _burningConeStrength;
                }
                else if (_vessel.LastIntent.Telegraphing && _vessel.LastIntent.TelegraphAttack != null && _vessel.LastIntent.TelegraphAttack.Telegraph != TelegraphShape.None)
                {
                    attack = _vessel.LastIntent.TelegraphAttack;
                    strength = 0.6f;
                    fill = 0f;
                }
            }

            if (attack == null)
            {
                bool fired = _startingUp != null && combat.CurrentAttack == _startingUp && combat.Phase == AttackPhase.Active;
                if (fired) _releaseLeft = _releaseFadeSeconds;
                _startingUp = null;
                if (_releaseLeft > 0f && !_vessel.IsDead)
                {
                    _releaseLeft -= _vessel.Root.Time.WorldDelta;
                    float fade = Mathf.Clamp01(_releaseLeft / Mathf.Max(_releaseFadeSeconds, 0.001f));
                    Paint(fade, fade);
                    FollowVessel();
                    return;
                }
                Fill = 0f;
                _ringClock = 0f;
                if (_renderer.enabled) _renderer.enabled = false;
                if (_fillRenderer.enabled) _fillRenderer.enabled = false;
                return;
            }

            _startingUp = combat.CurrentAttack == attack && combat.Phase == AttackPhase.Startup ? attack : null;
            _releaseLeft = 0f;
            Fill = Mathf.Max(fill, 0f);
            if (attack.Telegraph == TelegraphShape.Ring)
            {
                FollowVessel();
                ShowRing(attack, combat, fill);
                return;
            }
            Shape(attack, fill);
            FollowVessel();
            Paint(strength, fill >= 0f ? Mathf.Lerp(0.55f, 1f, fill) : 0f);
        }

        private void FollowVessel()
        {
            Transform t = _filter.transform;
            t.position = _vessel.transform.position + Vector3.up * 0.04f;
            t.rotation = Quaternion.LookRotation(_vessel.transform.forward, Vector3.up);
        }

        /// <summary>Sizes come from the attack data, which hits the same whatever the body's size, so the body's scale is cancelled.</summary>
        private void Shape(AttackDefinition attack, float fill)
        {
            Transform t = _filter.transform;
            float bodyScale = Mathf.Max(t.parent.lossyScale.x, 0.01f);
            float grown = Mathf.Max(fill, 0.001f);
            switch (attack.Telegraph)
            {
                case TelegraphShape.Fan:
                    _filter.sharedMesh = FanMesh(attack.ArcDegrees);
                    _fillFilter.sharedMesh = _filter.sharedMesh;
                    t.localScale = new Vector3(attack.Range, bodyScale, attack.Range) / bodyScale;
                    _fillFilter.transform.localScale = new Vector3(grown, 1f, grown);
                    break;
                case TelegraphShape.Line:
                    float length = attack.Delivery == AttackDelivery.Projectile ? Mathf.Min(attack.ProjectileMaxDistance, _lineMaxLength) : attack.Range + attack.LungeDistance;
                    _filter.sharedMesh = LineMesh(_lineHeadLength / Mathf.Max(length, _lineHeadLength));
                    _fillFilter.sharedMesh = _filter.sharedMesh;
                    t.localScale = new Vector3(Mathf.Max(_lineMinWidth, attack.ProjectileRadius * 3f), bodyScale, length) / bodyScale;
                    _fillFilter.transform.localScale = new Vector3(1f, 1f, grown);
                    break;
            }
            bool showFill = fill > 0f;
            if (_fillRenderer.enabled != showFill) _fillRenderer.enabled = showFill;
        }

        /// <summary>Drawn in metres, so the wave on the ground is the wave <see cref="VesselCombat"/> hits with.</summary>
        private void ShowRing(AttackDefinition attack, VesselCombat combat, float fill)
        {
            if (_ringMesh == null) BuildRingMesh();
            float reach = attack.Range;
            float strength = 1f;
            bool waveOut = combat.Phase == AttackPhase.Active && combat.CurrentAttack == attack && attack.Active > 0f;

            // The far edge of its reach stays marked throughout.
            WriteBand(0, reach - 0.3f, 0f, reach - 0.08f, 1f, reach - 0.08f, 1f, reach, 1f);
            if (waveOut)
            {
                float travelled = Mathf.Clamp01((combat.Elapsed - attack.Startup) / attack.Active);
                WriteBand(1, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
                strength = Mathf.Clamp01((1f - travelled) / _ringEndFade);
            }
            else
            {
                _ringClock += _vessel.Root.Time.WorldDelta;
                float closing = Mathf.Repeat(_ringClock / _ringRipple, 1f);
                float radius = reach * (1f - closing);
                float ripple = Mathf.Lerp(0.5f, 1f, closing);
                WriteBand(1, radius - 0.12f, 0f, radius, ripple, radius, ripple, radius + 1.1f, 0f);
            }
            _ringMesh.vertices = _ringVerts;
            _ringMesh.colors32 = _ringColours;

            Transform t = _filter.transform;
            _filter.sharedMesh = _ringMesh;
            float bodyScale = Mathf.Max(t.parent.lossyScale.x, 0.01f);
            t.localScale = Vector3.one / bodyScale;

            Color colour = _ringColor;
            colour.a *= strength;
            _block.SetColor(BaseColorId, colour);
            _renderer.SetPropertyBlock(_block);
            if (!_renderer.enabled) _renderer.enabled = true;

            bool gathering = !waveOut && fill > 0f;
            if (_fillRenderer.enabled != gathering) _fillRenderer.enabled = gathering;
            if (!gathering) return;
            _fillFilter.sharedMesh = FanMesh(WholeCircle);
            float core = Mathf.Max(fill, 0.001f) * _ringCoreRadius;
            _fillFilter.transform.localScale = new Vector3(core, 1f, core);
            Color gathered = _ringColor;
            gathered.a *= Mathf.Lerp(0.35f, 0.9f, fill);
            _fillBlock.SetColor(BaseColorId, gathered);
            _fillRenderer.SetPropertyBlock(_fillBlock);
        }

        private void BuildRingMesh()
        {
            int vertexCount = RingBands * RingRows * RingSegments;
            _ringVerts = new Vector3[vertexCount];
            _ringColours = new Color32[vertexCount];
            _ringDirections = new Vector3[RingSegments];
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                _ringDirections[i] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            }
            var tris = new int[RingBands * (RingRows - 1) * RingSegments * 6];
            int k = 0;
            for (int band = 0; band < RingBands; band++)
            {
                int first = band * RingRows * RingSegments;
                for (int row = 0; row < RingRows - 1; row++)
                {
                    for (int i = 0; i < RingSegments; i++)
                    {
                        int next = (i + 1) % RingSegments;
                        int inner = first + i * RingRows + row;
                        int nextInner = first + next * RingRows + row;
                        tris[k++] = inner;
                        tris[k++] = inner + 1;
                        tris[k++] = nextInner;
                        tris[k++] = inner + 1;
                        tris[k++] = nextInner + 1;
                        tris[k++] = nextInner;
                    }
                }
            }
            _ringMesh = new Mesh { name = "TelegraphRingWave" };
            _ringMesh.MarkDynamic();
            _ringMesh.vertices = _ringVerts;
            _ringMesh.colors32 = _ringColours;
            _ringMesh.triangles = tris;
            _ringMesh.RecalculateNormals();
        }

        /// <summary>One band of the ring: four circles, inner to outer, each with its own strength.</summary>
        private void WriteBand(int band, float r0, float a0, float r1, float a1, float r2, float a2, float r3, float a3)
        {
            int first = band * RingRows * RingSegments;
            Color32 c0 = Faded(a0), c1 = Faded(a1), c2 = Faded(a2), c3 = Faded(a3);
            r0 = Mathf.Max(r0, 0f);
            r1 = Mathf.Max(r1, 0f);
            r2 = Mathf.Max(r2, 0f);
            r3 = Mathf.Max(r3, 0f);
            for (int i = 0; i < RingSegments; i++)
            {
                Vector3 d = _ringDirections[i];
                int v = first + i * RingRows;
                _ringVerts[v] = d * r0;
                _ringVerts[v + 1] = d * r1;
                _ringVerts[v + 2] = d * r2;
                _ringVerts[v + 3] = d * r3;
                _ringColours[v] = c0;
                _ringColours[v + 1] = c1;
                _ringColours[v + 2] = c2;
                _ringColours[v + 3] = c3;
            }
        }

        private void OnDestroy()
        {
            if (_ringMesh != null) Destroy(_ringMesh);
        }

        private void Paint(float frameStrength, float fillStrength)
        {
            Color colour = _vessel == _vessel.Root.PlayerVessel ? _playerColor : _color;
            Color frame = colour;
            frame.a = colour.a * _frameShare * frameStrength;
            _block.SetColor(BaseColorId, frame);
            _renderer.SetPropertyBlock(_block);
            if (!_renderer.enabled) _renderer.enabled = true;

            Color filled = colour;
            filled.a = colour.a * fillStrength;
            _fillBlock.SetColor(BaseColorId, filled);
            _fillRenderer.SetPropertyBlock(_fillBlock);
        }

        /// <summary>
        /// Filled fan of radius 1 that brightens towards its rim, with a brighter outline along the arc and both straight edges.
        /// A whole circle has no straight edges. Faces up.
        /// </summary>
        public static Mesh FanMesh(float arcDegrees)
        {
            int key = Mathf.RoundToInt(arcDegrees);
            if (FanCache.TryGetValue(key, out Mesh m)) return m;
            const int segments = 48;
            var verts = new List<Vector3>(segments * 2 + 10) { Vector3.zero };
            var colours = new List<Color32>(segments * 2 + 10) { Faded(0.25f) };
            var tris = new List<int>(segments * 9 + 12);
            float half = arcDegrees * 0.5f * Mathf.Deg2Rad;

            for (int i = 0; i <= segments; i++)
            {
                float a = -half + (2f * half) * i / segments;
                verts.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)));
                colours.Add(Faded(0.7f));
            }
            for (int i = 0; i < segments; i++)
            {
                tris.Add(0);
                tris.Add(i + 1);
                tris.Add(i + 2);
            }

            // Arc outline: outer points are the fan's own rim, inner points sit just inside it.
            int rimStart = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                verts.Add(verts[i + 1]);
                colours.Add(Faded(1f));
            }
            for (int i = 0; i <= segments; i++)
            {
                verts.Add(verts[i + 1] * (1f - FanOutline));
                colours.Add(Faded(1f));
            }
            int inner = rimStart + segments + 1;
            for (int i = 0; i < segments; i++)
            {
                tris.Add(inner + i);
                tris.Add(rimStart + i);
                tris.Add(rimStart + i + 1);
                tris.Add(inner + i);
                tris.Add(rimStart + i + 1);
                tris.Add(inner + i + 1);
            }

            if (key < WholeCircle)
            {
                // Straight outlines, each offset towards the middle of the fan.
                Vector3 towardsRight = new Vector3(Mathf.Cos(half), 0f, Mathf.Sin(half)) * FanOutline;
                Vector3 towardsLeft = new Vector3(-Mathf.Cos(half), 0f, Mathf.Sin(half)) * FanOutline;
                AddQuad(verts, colours, tris, 1f, Vector3.zero, verts[1], verts[1] + towardsRight, towardsRight);
                AddQuad(verts, colours, tris, 1f, Vector3.zero, towardsLeft, verts[segments + 1] + towardsLeft, verts[segments + 1]);
            }

            m = new Mesh { name = "TelegraphFan" + key };
            m.SetVertices(verts);
            m.SetColors(colours);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            FanCache[key] = m;
            return m;
        }

        private static Color32 Faded(float alpha) => new Color32(255, 255, 255, (byte)(alpha * 255f));

        /// <summary>Adds a quad whose corners a, b, c, d run clockwise seen from above.</summary>
        private static void AddQuad(List<Vector3> verts, List<Color32> colours, List<int> tris, float alpha, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int first = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            verts.Add(d);
            for (int i = 0; i < 4; i++) colours.Add(Faded(alpha));
            tris.Add(first);
            tris.Add(first + 1);
            tris.Add(first + 2);
            tris.Add(first);
            tris.Add(first + 2);
            tris.Add(first + 3);
        }

        public static Mesh LineMesh() => LineMesh(DefaultHeadShare);

        /// <summary>
        /// An arrow from the feet (z 0) to its tip (z 1), one unit wide at the shaft and twice that at the head, brightening towards
        /// the tip, with a bright outline. headShare is the part of its length that is the arrowhead. Faces up.
        /// </summary>
        public static Mesh LineMesh(float headShare)
        {
            int key = Mathf.RoundToInt(Mathf.Clamp01(headShare) * 100f);
            if (LineCache.TryGetValue(key, out Mesh m)) return m;
            const float edge = 0.1f;
            float neck = 1f - Mathf.Max(key, 1) / 100f;
            float headLength = 1f - neck;
            var verts = new List<Vector3>(32);
            var colours = new List<Color32>(32);
            var tris = new List<int>(48);

            // Shaft, faint at the feet, in full colour at the neck.
            int first = verts.Count;
            verts.Add(new Vector3(-0.5f, 0f, 0f));
            verts.Add(new Vector3(-0.5f, 0f, neck));
            verts.Add(new Vector3(0.5f, 0f, neck));
            verts.Add(new Vector3(0.5f, 0f, 0f));
            colours.Add(Faded(0.3f));
            colours.Add(Faded(0.75f));
            colours.Add(Faded(0.75f));
            colours.Add(Faded(0.3f));
            tris.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });

            // Arrowhead.
            Vector3 left = new Vector3(-1f, 0f, neck), right = new Vector3(1f, 0f, neck), tip = new Vector3(0f, 0f, 1f);
            first = verts.Count;
            verts.Add(left);
            verts.Add(tip);
            verts.Add(right);
            for (int i = 0; i < 3; i++) colours.Add(Faded(0.85f));
            tris.AddRange(new[] { first, first + 1, first + 2 });

            // Outlines.
            AddQuad(verts, colours, tris, 1f, new Vector3(-0.5f, 0f, 0f), new Vector3(-0.5f, 0f, neck), new Vector3(-0.5f + edge, 0f, neck), new Vector3(-0.5f + edge, 0f, 0f));
            AddQuad(verts, colours, tris, 1f, new Vector3(0.5f - edge, 0f, 0f), new Vector3(0.5f - edge, 0f, neck), new Vector3(0.5f, 0f, neck), new Vector3(0.5f, 0f, 0f));
            Vector3 innerLeft = new Vector3(-1f + edge * 3f, 0f, neck + headLength * 0.06f);
            Vector3 innerRight = new Vector3(1f - edge * 3f, 0f, neck + headLength * 0.06f);
            Vector3 innerTip = new Vector3(0f, 0f, 1f - headLength * 0.28f);
            AddQuad(verts, colours, tris, 1f, left, tip, innerTip, innerLeft);
            AddQuad(verts, colours, tris, 1f, innerTip, tip, right, innerRight);

            m = new Mesh { name = "TelegraphLine" + key };
            m.SetVertices(verts);
            m.SetColors(colours);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            LineCache[key] = m;
            return m;
        }

    }
}
