using BorrowedSouls.Core.Combat;
using BorrowedSouls.Data;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// The sheet of light a heavy swing leaves behind. A line trail can't show a blade's width, so this records
    /// where the blade's base and tip have been and draws a ribbon between them, curved to hide the gaps between frames. Attacks
    /// opt in with a colour in their data. It ages on world time, so it holds still through hit stop along with the blade.
    /// </summary>
    [DefaultExecutionOrder(100)] // after VesselAnimationDriver has finished correcting the pose
    public sealed class BladeTrailView : MonoBehaviour
    {
        private const string BladeObject = "Weapon_Blade";
        private const int MaxSamples = 32;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private struct Sample
        {
            public Vector3 Base;
            public Vector3 Tip;
            public float Age;
        }

        [SerializeField] private VesselRuntime _vessel;
        [SerializeField] private VesselModel _model;
        [SerializeField] private MeshFilter _filter;
        [SerializeField] private MeshRenderer _renderer;
        [Tooltip("Seconds a point on the ribbon takes to fade out.")]
        [SerializeField, Min(0.02f)] private float _fadeSeconds = 0.2f;
        [Tooltip("Share of the recovery the blade keeps carrying through after the hit, which the clips are built around.")]
        [SerializeField, Range(0f, 1f)] private float _followThroughShare = 0.2f;
        [Tooltip("Curve points added between two recorded frames.")]
        [SerializeField, Range(1, 8)] private int _subdivisions = 5;
        [Tooltip("How much longer than the real blade the ribbon may reach, so its edge can follow the rim of the attack's area.")]
        [SerializeField, Min(1f)] private float _maxReachOfBlade = 2.2f;
        [Tooltip("Most blade positions recorded per second, so a very high frame rate can't fill the ribbon and cut its tail off.")]
        [SerializeField, Min(30f)] private float _samplesPerSecond = 90f;

        private readonly Sample[] _samples = new Sample[MaxSamples];
        private Vector3[] _vertices;
        private Color32[] _colors;
        private Vector2[] _uvs;
        private int[] _triangles;
        private Mesh _mesh;
        private MaterialPropertyBlock _block;
        private AttackDefinition _attack;
        private int _count;
        private float _followThroughLeft;
        private float _sinceSample;
        private Transform _blade;
        private Vector3 _bladeBase;
        private Vector3 _bladeTip;
        private bool _bladeSearched;

        /// <summary>Vertices drawn this frame; 0 when there is nothing on screen.</summary>
        public int VertexCount { get; private set; }

        private void Awake()
        {
            int points = (MaxSamples - 1) * _subdivisions + 1;
            _vertices = new Vector3[points * 2];
            _colors = new Color32[points * 2];
            _uvs = new Vector2[points * 2];
            _triangles = new int[(points - 1) * 6];
            _mesh = new Mesh { name = "BladeTrail" };
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;
            _block = new MaterialPropertyBlock();
            _renderer.enabled = false;
        }

        private void OnDestroy()
        {
            Destroy(_mesh);
        }

        private void LateUpdate()
        {
            float dt = _vessel.Root.Time.WorldDelta;
            if (dt <= 0f) return; // hit stop: blade and ribbon hold together

            AgeSamples(dt);
            _sinceSample += dt;
            if (ShouldEmit(dt) && FindBlade() && _sinceSample >= 1f / _samplesPerSecond)
            {
                _sinceSample = 0f;
                AddSample();
            }
            Draw();
        }

        private bool ShouldEmit(float dt)
        {
            VesselCombat combat = _vessel.Combat;
            AttackDefinition attack = combat.CurrentAttack;
            if (attack == null || !attack.HasBladeTrail)
            {
                _followThroughLeft = 0f;
                return false;
            }
            if (attack != _attack)
            {
                _attack = attack;
                _block.SetColor(BaseColorId, attack.BladeTrailColor);
                _renderer.SetPropertyBlock(_block);
            }
            if (combat.Phase == AttackPhase.Active)
            {
                _followThroughLeft = attack.Recovery * _followThroughShare;
                return true;
            }
            if (combat.Phase != AttackPhase.Recovery || _followThroughLeft <= 0f) return false;
            _followThroughLeft -= dt;
            return true;
        }

        /// <summary>The blade is on a model picked at runtime. Its ends come from the mesh, so any sword size works.</summary>
        private bool FindBlade()
        {
            if (_bladeSearched) return _blade != null;
            if (_model == null || _model.Root == null) return false;
            _bladeSearched = true;
            foreach (MeshFilter part in _model.Root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (part.name != BladeObject) continue;
                Bounds bounds = part.sharedMesh.bounds;
                Vector3 size = bounds.size;
                int longest = size.x > size.y ? (size.x > size.z ? 0 : 2) : (size.y > size.z ? 1 : 2);
                Vector3 half = Vector3.zero;
                half[longest] = bounds.extents[longest];
                Vector3 a = bounds.center + half, b = bounds.center - half; // the mesh's origin is the grip
                bool aIsTip = a.sqrMagnitude > b.sqrMagnitude;
                _blade = part.transform;
                _bladeTip = aIsTip ? a : b;
                _bladeBase = aIsTip ? b : a;
                return true;
            }
            return false;
        }

        private void AgeSamples(float dt)
        {
            int kept = 0;
            for (int i = 0; i < _count; i++)
            {
                Sample sample = _samples[i];
                sample.Age += dt;
                if (sample.Age < _fadeSeconds) _samples[kept++] = sample;
            }
            _count = kept;
        }

        private void AddSample()
        {
            if (_count == MaxSamples)
            {
                System.Array.Copy(_samples, 1, _samples, 0, MaxSamples - 1);
                _count--;
            }
            Vector3 bladeBase = _blade.TransformPoint(_bladeBase);
            Vector3 tip = _blade.TransformPoint(_bladeTip);
            _samples[_count++] = new Sample { Base = bladeBase, Tip = ReachOfAttack(bladeBase, tip), Age = 0f };
        }

        /// <summary>
        /// A hit lands anywhere inside the attack's range, which is longer than the sword. The ribbon carries on past the blade to that
        /// range, so what the swing shows is what the preview promised and what actually hits.
        /// </summary>
        private Vector3 ReachOfAttack(Vector3 bladeBase, Vector3 tip)
        {
            Vector3 along = tip - bladeBase;
            Vector3 flatAlong = new Vector3(along.x, 0f, along.z);
            Vector3 fromBody = tip - _vessel.transform.position;
            fromBody.y = 0f;
            float missing = _attack.Range - fromBody.magnitude;
            if (missing <= 0f || flatAlong.sqrMagnitude < 1e-4f) return tip;
            float stretch = 1f + missing / flatAlong.magnitude;
            return bladeBase + along * Mathf.Min(stretch, _maxReachOfBlade);
        }

        private void Draw()
        {
            if (_count < 2)
            {
                VertexCount = 0;
                _renderer.enabled = false;
                return;
            }

            int points = 0;
            for (int i = 0; i < _count - 1; i++)
            {
                for (int step = 0; step < _subdivisions; step++) AddPoint(points++, i, step / (float)_subdivisions);
            }
            AddPoint(points++, _count - 2, 1f);

            int triangles = 0;
            for (int i = 0; i < points - 1; i++)
            {
                int a = i * 2;
                _triangles[triangles++] = a;
                _triangles[triangles++] = a + 1;
                _triangles[triangles++] = a + 3;
                _triangles[triangles++] = a;
                _triangles[triangles++] = a + 3;
                _triangles[triangles++] = a + 2;
            }

            VertexCount = points * 2;
            _mesh.Clear();
            _mesh.SetVertices(_vertices, 0, VertexCount);
            _mesh.SetColors(_colors, 0, VertexCount);
            _mesh.SetUVs(0, _uvs, 0, VertexCount);
            _mesh.SetTriangles(_triangles, 0, triangles, 0, false);
            _mesh.RecalculateBounds();
            _renderer.enabled = true;
        }

        private void AddPoint(int point, int segment, float t)
        {
            Sample before = _samples[Mathf.Max(segment - 1, 0)];
            Sample from = _samples[segment];
            Sample to = _samples[segment + 1];
            Sample after = _samples[Mathf.Min(segment + 2, _count - 1)];
            float age = Mathf.Lerp(from.Age, to.Age, t) / _fadeSeconds;
            byte alpha = (byte)(255f * Mathf.Pow(Mathf.Clamp01(1f - age), 1.5f));
            int a = point * 2;
            _vertices[a] = transform.InverseTransformPoint(CatmullRom(before.Base, from.Base, to.Base, after.Base, t));
            _vertices[a + 1] = transform.InverseTransformPoint(CatmullRom(before.Tip, from.Tip, to.Tip, after.Tip, t));
            _colors[a] = _colors[a + 1] = new Color32(255, 255, 255, alpha);
            _uvs[a] = new Vector2(age, 0f);
            _uvs[a + 1] = new Vector2(age, 1f);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * (t2 * t));
        }
    }
}
