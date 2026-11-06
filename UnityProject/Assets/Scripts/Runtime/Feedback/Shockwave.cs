using BorrowedSouls.Bootstrap;
using BorrowedSouls.Combat;
using BorrowedSouls.Data;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// What a ring attack looks like once it goes out: a glowing wave as wide as the band that hurts, a wall of light
    /// standing on it, a ring of rock bursting out of the ground as it passes, a swell of stone under it, and sparks, stone chips and dust
    /// that ride along with it, so that everything stays a ring instead of being left behind to fill the circle. All of it is for show: the floor's
    /// collider and everyone's footing stay where they are, and the damage comes from <see cref="VesselCombat"/> alone. The swell is its
    /// own unbroken mesh rather than the floor's tiles: they are thin separate faces, so lifting them opened holes showing the dark below.
    /// </summary>
    public sealed class Shockwave : MonoBehaviour
    {
        private const int Segments = 96;
        private const int GroundRows = 7;
        private const int WallRows = 4;
        private const int Rows = GroundRows + WallRows;
        private const int SwellRows = 11;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private GameRoot _root;
        [SerializeField] private MeshFilter _wave;
        [SerializeField] private MeshRenderer _waveRenderer;
        [SerializeField] private MeshFilter _swell;
        [SerializeField] private MeshRenderer _swellRenderer;
        [SerializeField] private ParticleSystem _sparks;
        [SerializeField] private ParticleSystem _chips;
        [SerializeField] private ParticleSystem _rocks;
        [SerializeField] private ParticleSystem _dust;

        [Header("The wave")]
        [SerializeField, ColorUsage(true, true)] private Color _color = new Color(1.15f, 0.5f, 1.8f, 0.95f);
        [Tooltip("How far the glow trails behind the part that hurts, in metres.")]
        [SerializeField, Min(0f)] private float _trail = 1.2f;
        [Tooltip("Strength of that glow. The part that hurts is brighter.")]
        [SerializeField, Range(0f, 1f)] private float _trailStrength = 0.1f;
        [Tooltip("Share of its travel over which the wave fades at the far edge.")]
        [SerializeField, Range(0.01f, 1f)] private float _endFade = 0.12f;
        [SerializeField, Min(0f)] private float _wallHeight = 1.3f;
        [Tooltip("Share of the wall's height left by the far edge.")]
        [SerializeField, Range(0f, 1f)] private float _farWall = 0.6f;
        [Tooltip("Metres the glow sits above the swell, so the stone doesn't cut through it.")]
        [SerializeField, Min(0f)] private float _groundClearance = 0.08f;

        [Header("The swell of stone")]
        [Tooltip("Height of its crest near the boss, in metres.")]
        [SerializeField, Min(0f)] private float _swellHeight = 0.5f;
        [Tooltip("Share of that height left by the far edge.")]
        [SerializeField, Range(0f, 1f)] private float _farSwell = 0.55f;
        [Tooltip("Half its width, in metres, out to where it meets the floor. Near the band that hurts, so what rises is what hits, and it covers little else.")]
        [SerializeField, Min(0.1f)] private float _swellWidth = 1.5f;
        [Tooltip("Seconds it takes to sink back once the wave has gone.")]
        [SerializeField, Min(0.01f)] private float _settleSeconds = 0.3f;
        [Tooltip("Metres the swell's edge sits above the floor, so the two don't flicker through each other.")]
        [SerializeField, Min(0f)] private float _floorClearance = 0.02f;

        [Header("The ring of rock")]
        [Tooltip("The wave bursts a whole ring of rock out of the ground each time it has gone this many metres.")]
        [SerializeField, Min(0.2f)] private float _rockRingEvery = 1.6f;
        [Tooltip("Metres between rocks around a ring.")]
        [SerializeField, Min(0.1f)] private float _rockSpacing = 1.15f;
        [Tooltip("Height of a rock near the boss, in metres; the far edge has the share below.")]
        [SerializeField] private Vector2 _rockHeight = new Vector2(1.3f, 2.5f);
        [SerializeField, Range(0f, 1f)] private float _farRock = 0.65f;
        [Tooltip("How far a rock leans outward, away from the boss.")]
        [SerializeField, Range(0f, 1f)] private float _rockLean = 0.3f;

        [Header("Riding along the wave (per metre of wave, per second)")]
        [SerializeField, Min(0f)] private float _sparksRate = 5f;
        [SerializeField, Min(0f)] private float _chipsRate = 2.5f;
        [SerializeField, Min(0f)] private float _dustRate = 1.4f;

        private readonly float[] _rowOffset = new float[GroundRows];
        private readonly float[] _rowAlpha = new float[GroundRows];
        private readonly byte[] _wallAlpha = { 170, 80, 20, 0 };
        private readonly float[] _wallAt = { 0f, 0.25f, 0.6f, 1f };

        private Mesh _waveMesh;
        private Vector3[] _waveVerts;
        private Color32[] _waveColours;
        private Mesh _swellMesh;
        private Vector3[] _swellVerts;
        private Vector3[] _swellNormals;
        private Vector3[] _directions;
        private MaterialPropertyBlock _block;

        private VesselRuntime _attacker;
        private AttackDefinition _attack;
        private Vector3 _centre;
        private float _clock;
        private float _sparksOwed;
        private float _chipsOwed;
        private float _dustOwed;
        private float _nextRockRing;

        public bool Running { get; private set; }

        /// <summary>Where the wave is, in metres from where it started. For tests and tools.</summary>
        public float Radius { get; private set; }

        /// <summary>Height of the swell's crest right now, in metres. For tests and tools.</summary>
        public float SwellHeight { get; private set; }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _directions = new Vector3[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                _directions[i] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            }
            BuildWaveMesh();
            BuildSwellMesh();
            _waveRenderer.enabled = false;
            _swellRenderer.enabled = false;
        }

        private void OnEnable() => _root.Events.Subscribe<AttackReleased>(OnAttackReleased);
        private void OnDisable() => _root.Events.Unsubscribe<AttackReleased>(OnAttackReleased);

        private void OnDestroy()
        {
            if (_waveMesh != null) Destroy(_waveMesh);
            if (_swellMesh != null) Destroy(_swellMesh);
        }

        private void OnAttackReleased(AttackReleased e)
        {
            if (e.Attack.Delivery != AttackDelivery.Ring) return;
            _attacker = e.Attacker;
            _attack = e.Attack;
            _centre = e.Attacker.transform.position;
            _clock = 0f;
            _sparksOwed = 0f;
            _chipsOwed = 0f;
            _dustOwed = 0f;
            _nextRockRing = _rockRingEvery;
            Running = true;
            _waveRenderer.enabled = true;
            _swellRenderer.enabled = true;
            if (!_sparks.isPlaying) _sparks.Play();
            if (!_chips.isPlaying) _chips.Play();
            if (!_rocks.isPlaying) _rocks.Play();
            if (!_dust.isPlaying) _dust.Play();
        }

        private void LateUpdate()
        {
            if (!Running) return;
            float dt = _root.Time.WorldDelta;
            _clock += dt;
            if (_attacker != null && _attacker.Combat.CurrentAttack == _attack) _centre = _attacker.transform.position;

            float active = Mathf.Max(_attack.Active, 0.01f);
            float travelled = Mathf.Clamp01(_clock / active);
            float settled = Mathf.Clamp01((_clock - active) / _settleSeconds);
            if (settled >= 1f)
            {
                Finish();
                return;
            }

            Radius = _attack.Range * travelled;
            SwellHeight = _swellHeight * Mathf.Lerp(1f, _farSwell, travelled) * (1f - settled);
            var at = new Vector3(_centre.x, 0f, _centre.z);
            _wave.transform.position = at;
            _swell.transform.position = at;
            DrawWave(travelled);
            DrawSwell();
            if (travelled < 1f) Fling(dt, active, travelled);
        }

        /// <summary>The swell's height at a distance from its crest: a smooth hump that only ever rises, reaching the floor at its edges.</summary>
        private float Height(float fromCrest)
        {
            float x = Mathf.Abs(fromCrest) / _swellWidth;
            if (x >= 1f) return 0f;
            float hump = Mathf.Cos(x * Mathf.PI * 0.5f);
            return SwellHeight * hump * hump;
        }

        private void DrawWave(float travelled)
        {
            float band = _attack.RingBand;
            _rowOffset[0] = -band - _trail;
            _rowOffset[1] = -band - _trail * 0.5f;
            _rowOffset[2] = -band;
            _rowOffset[3] = -band + 0.02f;
            _rowOffset[4] = -band * 0.33f;
            _rowOffset[5] = band * 0.33f;
            _rowOffset[6] = band;
            _rowAlpha[0] = 0f;
            _rowAlpha[1] = _trailStrength * 0.5f;
            _rowAlpha[2] = _trailStrength;
            _rowAlpha[3] = 0.24f; // see-through enough that the stone and rock under it still show
            _rowAlpha[4] = 0.2f;
            _rowAlpha[5] = 0.3f;
            _rowAlpha[6] = 1f; // a bright leading edge

            for (int row = 0; row < GroundRows; row++)
            {
                float r = Mathf.Max(Radius + _rowOffset[row], 0f);
                float y = Height(_rowOffset[row]) + _floorClearance + _groundClearance;
                WriteWaveRow(row, r, y, new Color32(255, 255, 255, (byte)(_rowAlpha[row] * 255f)));
            }

            float wallRadius = Radius + band;
            float wallFoot = Height(band) + _floorClearance + _groundClearance;
            float wallHeight = _wallHeight * Mathf.Lerp(1f, _farWall, travelled);
            for (int row = 0; row < WallRows; row++)
            {
                WriteWaveRow(GroundRows + row, wallRadius, wallFoot + wallHeight * _wallAt[row], new Color32(255, 255, 255, _wallAlpha[row]));
            }

            _waveMesh.vertices = _waveVerts;
            _waveMesh.colors32 = _waveColours;

            Color colour = _color;
            colour.a *= Mathf.Clamp01((1f - travelled) / _endFade);
            _block.SetColor(BaseColorId, colour);
            _waveRenderer.SetPropertyBlock(_block);
        }

        private void WriteWaveRow(int row, float radius, float y, Color32 colour)
        {
            int first = row * Segments;
            for (int i = 0; i < Segments; i++)
            {
                Vector3 d = _directions[i];
                _waveVerts[first + i] = new Vector3(d.x * radius, y, d.z * radius);
                _waveColours[first + i] = colour;
            }
        }

        private void DrawSwell()
        {
            float step = 2f * _swellWidth / (SwellRows - 1);
            for (int row = 0; row < SwellRows; row++)
            {
                float fromCrest = -_swellWidth + row * step;
                float r = Mathf.Max(Radius + fromCrest, 0f);
                float y = Height(fromCrest) + _floorClearance;
                float slope = (Height(fromCrest + 0.05f) - Height(fromCrest - 0.05f)) / 0.1f;
                int first = row * Segments;
                for (int i = 0; i < Segments; i++)
                {
                    Vector3 d = _directions[i];
                    _swellVerts[first + i] = new Vector3(d.x * r, y, d.z * r);
                    _swellNormals[first + i] = new Vector3(-d.x * slope, 1f, -d.z * slope).normalized;
                }
            }
            _swellMesh.vertices = _swellVerts;
            _swellMesh.normals = _swellNormals;
        }

        private void Fling(float dt, float active, float travelled)
        {
            while (Radius >= _nextRockRing)
            {
                BurstRockRing(_nextRockRing, travelled);
                _nextRockRing += _rockRingEvery;
            }
            float metres = Mathf.PI * 2f * Mathf.Max(Radius, 0.5f);
            float speed = _attack.Range / active;
            _sparksOwed += _sparksRate * metres * dt;
            _chipsOwed += _chipsRate * metres * dt;
            _dustOwed += _dustRate * metres * dt;
            // Moving at about the wave's own speed is what keeps them in a ring around it.
            Emit(_sparks, ref _sparksOwed, speed * 0.85f, speed * 1.05f, 2.5f, 6f);
            Emit(_chips, ref _chipsOwed, speed * 0.8f, speed * 1.05f, 2f, 5f);
            Emit(_dust, ref _dustOwed, speed * 0.75f, speed * 0.95f, 0.3f, 1.4f);
        }

        private void BurstRockRing(float radius, float travelled)
        {
            int count = Mathf.Max(6, Mathf.CeilToInt(Mathf.PI * 2f * radius / _rockSpacing));
            float scale = Mathf.Lerp(1f, _farRock, travelled);
            var at = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.value * 0.6f) * Mathf.PI * 2f / count;
                var way = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                at.position = new Vector3(_centre.x, 0f, _centre.z) + way * Mathf.Max(0f, radius + Random.Range(-0.3f, 0.3f));
                float height = Random.Range(_rockHeight.x, _rockHeight.y) * scale;
                float width = height * Random.Range(0.75f, 1.05f); // chunky, or a crowd of them reads as spikes
                at.startSize3D = new Vector3(width, height, width);
                Quaternion lean = Quaternion.FromToRotation(Vector3.up, (Vector3.up + way * _rockLean).normalized);
                at.rotation3D = (lean * Quaternion.Euler(0f, Random.value * 360f, 0f)).eulerAngles;
                _rocks.Emit(at, 1);
            }
        }

        private void Emit(ParticleSystem system, ref float owed, float outMin, float outMax, float upMin, float upMax)
        {
            float band = _attack.RingBand;
            var at = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            while (owed >= 1f)
            {
                owed -= 1f;
                float a = Random.value * Mathf.PI * 2f;
                var way = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                at.position = new Vector3(_centre.x, 0.15f, _centre.z) + way * Mathf.Max(0f, Radius + Random.Range(-band, band));
                at.velocity = way * Random.Range(outMin, outMax) + Vector3.up * Random.Range(upMin, upMax);
                system.Emit(at, 1);
            }
        }

        private void Finish()
        {
            Running = false;
            Radius = 0f;
            SwellHeight = 0f;
            _waveRenderer.enabled = false;
            _swellRenderer.enabled = false;
        }

        private void BuildWaveMesh()
        {
            _waveVerts = new Vector3[Rows * Segments];
            _waveColours = new Color32[Rows * Segments];
            var tris = new int[((GroundRows - 1) + (WallRows - 1)) * Segments * 6];
            int k = 0;
            for (int row = 0; row < Rows - 1; row++)
            {
                if (row == GroundRows - 1) continue; // the wall stands on the wave, it isn't joined to it
                k = WriteStrip(tris, k, row);
            }
            _waveMesh = new Mesh { name = "ShockwaveMesh" };
            _waveMesh.MarkDynamic();
            _waveMesh.vertices = _waveVerts;
            _waveMesh.colors32 = _waveColours;
            _waveMesh.triangles = tris;
            _wave.sharedMesh = _waveMesh;
        }

        private void BuildSwellMesh()
        {
            _swellVerts = new Vector3[SwellRows * Segments];
            _swellNormals = new Vector3[SwellRows * Segments];
            var tris = new int[(SwellRows - 1) * Segments * 6];
            int k = 0;
            for (int row = 0; row < SwellRows - 1; row++) k = WriteStrip(tris, k, row);
            _swellMesh = new Mesh { name = "ShockwaveSwell" };
            _swellMesh.MarkDynamic();
            _swellMesh.vertices = _swellVerts;
            _swellMesh.normals = _swellNormals;
            _swellMesh.triangles = tris;
            _swell.sharedMesh = _swellMesh;
        }

        /// <summary>The band between a row and the next, facing up. Returns where the next band's indices go.</summary>
        private static int WriteStrip(int[] tris, int k, int row)
        {
            for (int i = 0; i < Segments; i++)
            {
                int next = (i + 1) % Segments;
                int a = row * Segments + i, b = (row + 1) * Segments + i;
                int c = row * Segments + next, d = (row + 1) * Segments + next;
                tris[k++] = a;
                tris[k++] = b;
                tris[k++] = c;
                tris[k++] = b;
                tris[k++] = d;
                tris[k++] = c;
            }
            return k;
        }
    }
}
