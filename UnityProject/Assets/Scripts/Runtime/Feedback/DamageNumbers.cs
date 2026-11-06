using System.Collections.Generic;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Combat;
using BorrowedSouls.Core.Combat;
using UnityEngine;
using UnityEngine.Pool;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// Pooled floating damage numbers: scale
    /// curve 1 → 1.2 → 0.8 animated on the transform (not fontSize, which rebuilt the mesh), random offset rolled
    /// per spawn (not once per instance), billboarded to the camera, real-time so hit stop does not freeze them.
    /// Strong/special hits are larger and gold; damage to the player is red. Can be turned off in options.
    /// </summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        private struct Live
        {
            public TextMesh Text;
            public float Born;
            public Vector3 Start;
            public float Size;
            public Color Color;
        }

        [SerializeField] private GameRoot _root;
        [SerializeField] private Camera _camera;
        [SerializeField] private TextMesh _prefab;
        [SerializeField] private Color _healColor = new Color(0.45f, 1f, 0.55f);
        [SerializeField, Min(1)] private int _prewarm = 24;
        [SerializeField, Min(0.1f)] private float _lifetime = 0.9f;
        [SerializeField, Min(0f)] private float _rise = 1.1f;
        [SerializeField] private AnimationCurve _scale = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.35f, 1.2f), new Keyframe(1f, 0.8f));

        private readonly List<Live> _live = new List<Live>(32);
        private ObjectPool<TextMesh> _pool;

        private void Awake()
        {
            _pool = new ObjectPool<TextMesh>(
                () => Instantiate(_prefab, transform),
                t => t.gameObject.SetActive(true),
                t => t.gameObject.SetActive(false),
                t => Destroy(t.gameObject),
                collectionCheck: true, defaultCapacity: _prewarm, maxSize: 64);
            StartCoroutine(PoolWarmup.Fill(this, _pool, _prewarm));
        }

        private void OnEnable()
        {
            _root.Events.Subscribe<HitConfirmed>(OnHit);
            _root.Events.Subscribe<BodyHealed>(OnHealed);
        }

        private void OnDisable()
        {
            _root.Events.Unsubscribe<HitConfirmed>(OnHit);
            _root.Events.Unsubscribe<BodyHealed>(OnHealed);
        }

        private void OnHealed(BodyHealed e)
        {
            if (!_root.Options.DamageNumbers) return;
            TextMesh t = _pool.Get();
            t.text = "+" + Mathf.RoundToInt(e.Amount);
            Vector3 start = e.Vessel.transform.position + Vector3.up * (2.1f + e.Vessel.transform.localScale.y * 0.4f);
            _live.Add(new Live { Text = t, Born = Time.unscaledTime, Start = start, Size = 1.35f, Color = _healColor });
        }

        private void OnHit(HitConfirmed e)
        {
            if (e.Damage < 0.5f || !_root.Options.DamageNumbers) return;
            bool toPlayer = e.Target == _root.PlayerVessel;
            bool big = e.Tier != HitTier.Weak || e.IsKill;
            Color color = toPlayer ? new Color(1f, 0.35f, 0.3f) : (big ? new Color(1f, 0.85f, 0.35f) : Color.white);

            TextMesh t = _pool.Get();
            t.text = Mathf.RoundToInt(e.Damage).ToString();
            Vector2 jitter = Random.insideUnitCircle * 0.35f;
            Vector3 start = e.Target.transform.position + new Vector3(jitter.x, 2.1f + e.Target.transform.localScale.y * 0.3f, jitter.y);
            _live.Add(new Live { Text = t, Born = Time.unscaledTime, Start = start, Size = big ? 1.35f : 1f, Color = color });
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            Quaternion facing = _camera != null ? _camera.transform.rotation : Quaternion.identity;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Live l = _live[i];
                float k = (now - l.Born) / _lifetime;
                if (k >= 1f)
                {
                    _pool.Release(l.Text);
                    _live.RemoveAt(i);
                    continue;
                }
                Transform tr = l.Text.transform;
                tr.SetPositionAndRotation(l.Start + Vector3.up * (_rise * k), facing);
                tr.localScale = Vector3.one * (0.12f * l.Size * _scale.Evaluate(k));
                Color c = l.Color;
                c.a = 1f - k * k;
                l.Text.color = c;
            }
        }
    }
}
