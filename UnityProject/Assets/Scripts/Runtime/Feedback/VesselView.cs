using BorrowedSouls.Core.Combat;
using BorrowedSouls.Core.Soul;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Feedback
{
    public enum SoulHighlight
    {
        None,
        Legal,
        Targeted,
        Immune,
        /// <summary>Soul vision: not a legal target, shown in greyscale so legal bodies keep their colour.</summary>
        Dimmed
    }

    /// <summary>
    /// Grey-box presentation for a vessel: hit flash via MaterialPropertyBlock (no material instancing — fixes
    /// a hit flash), scale punch, telegraph / i-frame tints, fallen-corpse pose with expiry blink,
    /// suppressed-enemy-soul tint and soul-vision highlight with a non-colour cue (ring).
    /// Runs on real time so it stays readable during hit stop and slow motion.
    /// </summary>
    public sealed class VesselView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int AuraStrengthId = Shader.PropertyToID("_AuraStrength");

        [SerializeField] private VesselRuntime _vessel;
        [SerializeField] private Renderer[] _bodyRenderers = new Renderer[0];
        [SerializeField] private Transform _visualRoot;
        [Tooltip("Ground ring shown when this body is a legal soul target (shape cue, not only colour).")]
        [SerializeField] private Transform _soulRing;
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.55f, 0.15f);
        [SerializeField] private Color _invulnColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] private Color _deadColor = new Color(0.18f, 0.18f, 0.2f);
        [SerializeField] private Color _brokenColor = new Color(0.07f, 0.07f, 0.08f);
        [SerializeField] private Color _suppressedColor = new Color(0.62f, 0.32f, 0.85f);
        [SerializeField] private Color _soulLegalColor = new Color(0.45f, 0.95f, 1f);
        [Tooltip("Glow strength of a legal soul target; pulses between 75% and 100% of this.")]
        [SerializeField, Min(0f)] private float _soulLegalGlow = 0.6f;
        [Tooltip("Glow strength of the body the soul would possess right now.")]
        [SerializeField, Min(0f)] private float _soulTargetGlow = 1.1f;
        [Tooltip("Whole-body glow (S_SoulAura), added to Aura Renderers while this body is a legal soul target. Drawn on a mesh's last submesh.")]
        [SerializeField] private Material _auraMaterial;
        [Tooltip("Smooth-shaded main body meshes. Hard-edged meshes split the aura shell into floating faces.")]
        [SerializeField] private Renderer[] _auraRenderers = new Renderer[0];
        [SerializeField, Min(0f)] private float _soulLegalAura = 0.35f;
        [SerializeField, Min(0f)] private float _soulTargetAura = 0.55f;
        [Tooltip("Pose of a body with no soul in it, dead or left behind.")]
        [SerializeField] private Vector3 _fallenPose = new Vector3(80f, 0f, 0f);
        [Tooltip("Optional art. When it spawns, it replaces the grey-box children of the visual root.")]
        [SerializeField] private VesselModel _model;
        [Tooltip("A textured model doesn't show a white base colour, so hit flash goes through emission at this strength.")]
        [SerializeField, Min(0f)] private float _modelFlashGlow = 0.8f;
        [Tooltip("A detailed model catches far more rim light than a capsule, so soul glow is scaled down on one.")]
        [SerializeField, Range(0f, 1f)] private float _modelGlowScale = 0.55f;

        private MaterialPropertyBlock _block;
        private MaterialPropertyBlock _auraBlock;
        private Material[][] _plainMaterials;
        private Material[][] _auraMaterials;
        private Color _baseColor;
        private float _flashLeft;
        private float _flashDuration;
        private float _punchLeft;
        private float _punchAmount;
        private SoulHighlight _highlight;
        private bool _colorReady;
        private Renderer[] _visualRenderers;
        private int[] _visualLayers;
        private int _soulColorLayer;
        private bool _onSoulColorLayer;
        private bool _hasModel;
        private const float PunchDuration = 0.12f;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_visualRoot == null) _visualRoot = transform;
            if (_soulRing != null)
            {
                _soulRing.gameObject.SetActive(false);
                SoulColorLayer.Apply(_soulRing.gameObject); // only ever visible during soul vision
            }
            SwapInModel();
            _visualRenderers = (_hasModel ? _model.Root : _visualRoot).GetComponentsInChildren<Renderer>(true);
            _visualLayers = new int[_visualRenderers.Length];
            for (int i = 0; i < _visualRenderers.Length; i++) _visualLayers[i] = _visualRenderers[i].gameObject.layer;
            _soulColorLayer = SoulColorLayer.Id;
            CacheAuraMaterials();
        }

        private void SwapInModel()
        {
            if (_model == null) return;
            int greyBoxChildren = _visualRoot.childCount;
            if (!_model.Spawn(_visualRoot)) return;

            for (int i = 0; i < greyBoxChildren; i++) _visualRoot.GetChild(i).gameObject.SetActive(false);
            _hasModel = true;
            _bodyRenderers = new[] { _model.Body };
            _auraRenderers = new[] { _model.Body };
        }

        private void CacheAuraMaterials()
        {
            _auraBlock = new MaterialPropertyBlock();
            _plainMaterials = new Material[_auraRenderers.Length][];
            _auraMaterials = new Material[_auraRenderers.Length][];
            for (int i = 0; i < _auraRenderers.Length; i++)
            {
                Material[] plain = _auraRenderers[i].sharedMaterials;
                var withAura = new Material[plain.Length + 1];
                plain.CopyTo(withAura, 0);
                withAura[plain.Length] = _auraMaterial;
                _plainMaterials[i] = plain;
                _auraMaterials[i] = withAura;
            }
        }

        public void Flash(float duration)
        {
            _flashDuration = Mathf.Max(0.001f, duration);
            _flashLeft = duration;
        }

        public void Punch(float amount)
        {
            _punchAmount = amount;
            _punchLeft = PunchDuration;
        }

        public void SetSoulHighlight(SoulHighlight mode)
        {
            _highlight = mode;
            // Legal bodies keep their colour in soul vision: the whole frame is greyed by the vision
            // Volume, and the SoulColor layer is drawn again after post-processing. Visual children carry no colliders.
            bool colour = _soulColorLayer >= 0 && (mode == SoulHighlight.Legal || mode == SoulHighlight.Targeted);
            if (colour == _onSoulColorLayer || _visualRenderers == null) return;
            _onSoulColorLayer = colour;
            for (int i = 0; i < _visualRenderers.Length; i++)
                _visualRenderers[i].gameObject.layer = colour ? _soulColorLayer : _visualLayers[i];
            if (_auraMaterial == null) return;
            for (int i = 0; i < _auraRenderers.Length; i++)
                _auraRenderers[i].sharedMaterials = colour ? _auraMaterials[i] : _plainMaterials[i];
        }

        private void SetAuraStrength(float strength)
        {
            if (_auraMaterial == null) return;
            _auraBlock.SetFloat(AuraStrengthId, strength);
            for (int i = 0; i < _auraRenderers.Length; i++)
                _auraRenderers[i].SetPropertyBlock(_auraBlock, _plainMaterials[i].Length);
        }

        private void LateUpdate()
        {
            float realDt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            _flashLeft -= realDt;
            _punchLeft -= realDt;

            VesselState state = _vessel.State;
            if (!_colorReady)
            {
                // Read after VesselRuntime.Awake (component Awake order on one GameObject is not guaranteed).
                _baseColor = _hasModel ? Color.white : _vessel.Definition.BodyColor; // a model's colour is in its texture
                // Elite template: gilded tint.
                if (state.Kind == VesselKind.Elite) _baseColor = Color.Lerp(_baseColor, new Color(0.95f, 0.78f, 0.3f), 0.45f);
                _colorReady = true;
            }
            Color c = _baseColor;
            Color glow = Color.black;
            if (state.IsBroken) c = _brokenColor;
            else if (_vessel.IsDead)
            {
                c = _deadColor;
                // Fallen corpse: blink during the last 2 s of its claim window.
                if (state.IsFallenCorpse && state.CorpseTimeLeft < 2f && Mathf.Repeat(now * 6f, 1f) < 0.5f) c = Color.Lerp(_deadColor, _baseColor, 0.5f);
            }
            else if (state.EnemySoulSuppressed) c = Color.Lerp(_baseColor, _suppressedColor, 0.45f + 0.15f * Mathf.Sin(now * 5f));
            else if (_vessel.IsInvulnerable) c = Color.Lerp(_baseColor, _invulnColor, 0.7f);
            else if (_vessel.LastIntent.Telegraphing) c = Color.Lerp(_baseColor, _telegraphColor, 0.35f + 0.25f * Mathf.Sin(now * 14f));
            else if (_vessel.Combat.Phase == AttackPhase.Startup) c = Color.Lerp(_baseColor, _telegraphColor, 0.6f);

            if (_highlight == SoulHighlight.Dimmed || _highlight == SoulHighlight.Immune)
            {
                float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                c = new Color(lum, lum, lum) * 0.75f;
                if (_highlight == SoulHighlight.Immune) c = Color.Lerp(c, new Color(0.45f, 0.1f, 0.1f), 0.35f); // boss: marked as never possessable
            }
            else if (_highlight == SoulHighlight.Legal || _highlight == SoulHighlight.Targeted)
            {
                // A corpse that can be taken shows its living colour, not the dark corpse tint.
                if (_vessel.IsDead) c = _baseColor;
                c = Color.Lerp(c, _soulLegalColor, (_highlight == SoulHighlight.Targeted ? 0.6f : 0.3f) * (_hasModel ? _modelGlowScale : 1f));
                bool targeted = _highlight == SoulHighlight.Targeted;
                float pulse = targeted ? 1f : 0.875f + 0.125f * Mathf.Sin(now * 5f);
                // The claim window's last 2 s blink through the glow, since the corpse tint no longer shows.
                if (state.IsFallenCorpse && state.CorpseTimeLeft < 2f && Mathf.Repeat(now * 6f, 1f) < 0.5f) pulse *= 0.25f;
                float glowScale = _hasModel ? pulse * _modelGlowScale : pulse;
                glow = _soulLegalColor * ((targeted ? _soulTargetGlow : _soulLegalGlow) * glowScale);
                SetAuraStrength((targeted ? _soulTargetAura : _soulLegalAura) * glowScale);
            }
            if (_flashLeft > 0f)
            {
                float flash = Mathf.Clamp01(_flashLeft / _flashDuration);
                c = Color.Lerp(c, _flashColor, flash);
                if (_hasModel) glow += _flashColor * (flash * _modelFlashGlow);
            }

            _block.SetColor(BaseColorId, c);
            _block.SetColor(EmissionColorId, glow);
            for (int i = 0; i < _bodyRenderers.Length; i++) _bodyRenderers[i].SetPropertyBlock(_block);

            if (_soulRing != null)
            {
                bool showRing = _highlight == SoulHighlight.Legal || _highlight == SoulHighlight.Targeted;
                if (_soulRing.gameObject.activeSelf != showRing) _soulRing.gameObject.SetActive(showRing);
                if (showRing)
                {
                    float s = _highlight == SoulHighlight.Targeted ? 1.25f + 0.1f * Mathf.Sin(now * 10f) : 1f;
                    _soulRing.localScale = new Vector3(1.5f * s, 0.01f, 1.5f * s);
                }
            }

            if (state.IsBroken)
            {
                // Shattered (ShardBurst): the body is gone; restored dummies / pooled respawns become visible again.
                _visualRoot.localScale = Vector3.zero;
                if (_soulRing != null && _soulRing.gameObject.activeSelf) _soulRing.gameObject.SetActive(false);
                return;
            }

            float scale = 1f;
            if (_punchLeft > 0f)
            {
                float t = 1f - _punchLeft / PunchDuration;
                scale += _punchAmount * Mathf.Sin(t * Mathf.PI) * (1f - t);
            }
            _visualRoot.localScale = new Vector3(scale, scale * (2f - scale), scale);

            // A model lies down through its Death clip; only the grey-box capsule needs tipping over.
            Quaternion pose = _vessel.IsDown && !_hasModel ? Quaternion.Euler(_fallenPose) : Quaternion.identity;
            _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, pose, 1f - Mathf.Exp(-12f * realDt));
        }
    }
}
