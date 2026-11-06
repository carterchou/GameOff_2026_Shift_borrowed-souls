using BorrowedSouls.Bootstrap;
using BorrowedSouls.Combat;
using BorrowedSouls.Core.Audio;
using BorrowedSouls.Core.Combat;
using BorrowedSouls.Core.Soul;
using BorrowedSouls.Data;
using BorrowedSouls.Run;
using BorrowedSouls.Soul;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Audio
{
    /// <summary>
    /// Maps gameplay events to sound.
    /// Pooled 2D voices with per-category caps so dozens of hits never stack into noise; each voice sets its own
    /// pitch. Hits are routed by who got hit (A1) and built from layers (A2); music (B) is played by
    /// <see cref="MusicPlayer"/>, ducked under heavy hits; everything is low-passed while the soul flies (A4).
    /// Voices belong to the body's class, so a borrowed body keeps its own voice.
    /// Clips come from the AudioBank, falling back to ProceduralSfx / ProceduralMusic. Gameplay code never calls audio.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        private enum Category
        {
            Hit,
            HitLayer,
            Swing,
            Ember,
            Soul,
            Ui,
            World,
            Voice
        }

        private const float OpenCutoffHz = 22000f;
        private static readonly int VoiceLineCount = System.Enum.GetValues(typeof(VoiceLine)).Length;
        private static readonly int[] CategoryCaps = { 6, 6, 4, 4, 2, 3, 6, 2 };

        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerSoulController _soul;
        [Tooltip("Boss and remaining-enemy count for the adaptive music; optional (music stays on the first layers without it).")]
        [SerializeField] private WaveDirector _waves;
        [SerializeField] private AudioBank _bank;
        [SerializeField] private AudioMixSettings _mix;
        [SerializeField, Min(4)] private int _voices = 24;

        private readonly System.Random _rng = new System.Random(20261103);
        private AudioSource[] _sources;
        private Category[] _sourceCategory;
        private AudioLowPassFilter _lowPass;
        private AudioClip[][] _sfx;
        private AudioClip[][] _voiceLines; // [class * VoiceLineCount + line]
        private VariantCycler[] _sfxCyclers;
        private VariantCycler[] _voiceCyclers;
        private AudioClip[] _bossPhaseVoice;
        private readonly VariantCycler _bossPhaseCycler = new VariantCycler();
        private float _nextBossShout;
        private HitCombo _combo;
        private MusicPlayer _music;
        private bool _runStarted, _runEnded, _won;
        private float _duckHoldUntil, _duck01;
        private float _killChimeAt = -1f;
        private float _nextHeartbeat, _nextHurtVoice;

        private void Awake()
        {
            if (_mix == null)
            {
                Debug.LogError("AudioDirector needs an AudioMixSettings asset (Assets/ScriptableObjects/Audio); audio is off.", this);
                enabled = false; // also skips OnEnable, so no event handler runs without settings
                return;
            }
            _sources = new AudioSource[_voices];
            _sourceCategory = new Category[_voices];
            for (int i = 0; i < _voices; i++) _sources[i] = NewSource(false);

            int sfxCount = System.Enum.GetValues(typeof(SfxCue)).Length;
            _sfx = new AudioClip[sfxCount][];
            _sfxCyclers = new VariantCycler[sfxCount];
            for (int i = 0; i < sfxCount; i++)
            {
                AudioClip[] licensed = _bank != null ? _bank.Clips((SfxCue)i) : System.Array.Empty<AudioClip>();
                _sfx[i] = licensed.Length > 0 ? licensed : ProceduralSfx.Variants((SfxCue)i, _mix.ProceduralVariants);
                _sfxCyclers[i] = new VariantCycler();
            }

            int classCount = System.Enum.GetValues(typeof(VesselClass)).Length;
            _voiceLines = new AudioClip[classCount * VoiceLineCount][];
            _voiceCyclers = new VariantCycler[_voiceLines.Length];
            for (int c = 0; c < classCount; c++)
            for (int l = 0; l < VoiceLineCount; l++)
            {
                int i = c * VoiceLineCount + l;
                _voiceLines[i] = _bank != null ? _bank.Voice((VesselClass)c, (VoiceLine)l) : System.Array.Empty<AudioClip>();
                _voiceCyclers[i] = new VariantCycler();
            }
            _bossPhaseVoice = _bank != null ? _bank.BossPhaseVoice : System.Array.Empty<AudioClip>();

            _combo = new HitCombo(_mix.ComboWindow, _mix.ComboMaxStep);
            _music = new MusicPlayer(_mix, _bank, () => MusicSource(), _rng);

            // Filters act on every AudioSource of this GameObject: one cutoff muffles SFX and music together (A4).
            // Added last: an AudioSource added next to a filter tries to play at once and warns when it has no clip.
            _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = OpenCutoffHz;
        }

        private void Start() => _music.Start();

        private void OnEnable()
        {
            var e = _root.Events;
            e.Subscribe<AttackStarted>(OnAttackStarted);
            e.Subscribe<HitConfirmed>(OnHit);
            e.Subscribe<HitEvaded>(OnEvaded);
            e.Subscribe<DodgeStarted>(OnDodge);
            e.Subscribe<VesselDied>(OnDied);
            e.Subscribe<SoulExited>(OnSoulExited);
            e.Subscribe<PossessionSucceeded>(OnPossessed);
            e.Subscribe<SoulExpired>(OnExpired);
            e.Subscribe<EmberCollected>(OnEmber);
            e.Subscribe<UpgradeChosen>(OnUpgrade);
            e.Subscribe<BossPhaseChanged>(OnBossPhase);
            e.Subscribe<AttackReleased>(OnAttackReleased);
            e.Subscribe<BossAttackAnnounced>(OnBossAttackAnnounced);
            e.Subscribe<WaveStarted>(OnWaveStarted);
            e.Subscribe<WaveCleared>(OnWaveCleared);
            e.Subscribe<RunEnded>(OnRunEnded);
        }

        private void OnDisable()
        {
            var e = _root.Events;
            e.Unsubscribe<AttackStarted>(OnAttackStarted);
            e.Unsubscribe<HitConfirmed>(OnHit);
            e.Unsubscribe<HitEvaded>(OnEvaded);
            e.Unsubscribe<DodgeStarted>(OnDodge);
            e.Unsubscribe<VesselDied>(OnDied);
            e.Unsubscribe<SoulExited>(OnSoulExited);
            e.Unsubscribe<PossessionSucceeded>(OnPossessed);
            e.Unsubscribe<SoulExpired>(OnExpired);
            e.Unsubscribe<EmberCollected>(OnEmber);
            e.Unsubscribe<UpgradeChosen>(OnUpgrade);
            e.Unsubscribe<BossPhaseChanged>(OnBossPhase);
            e.Unsubscribe<AttackReleased>(OnAttackReleased);
            e.Unsubscribe<BossAttackAnnounced>(OnBossAttackAnnounced);
            e.Unsubscribe<WaveStarted>(OnWaveStarted);
            e.Unsubscribe<WaveCleared>(OnWaveCleared);
            e.Unsubscribe<RunEnded>(OnRunEnded);
        }

        private void Update()
        {
            // Real time throughout: hit stop and pauses freeze the world, not the mix.
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            UpdateHeartbeat(now);
            if (_killChimeAt >= 0f && now >= _killChimeAt)
            {
                _killChimeAt = -1f;
                Play(SfxCue.KillChime, Category.HitLayer, _mix.PlayerDealtVolume, 1f);
            }
            UpdateLowPass(dt);
            UpdateMusic(now, dt);
        }

        private void UpdateHeartbeat(float now)
        {
            // Last-second heartbeat while the soul flies.
            if (_soul == null || !_soul.IsFree || _soul.Soul.FreeTimeLeft > 1.5f) return;
            if (now < _nextHeartbeat) return;
            _nextHeartbeat = now + (_soul.Soul.FreeTimeLeft < 0.8f ? 0.35f : 0.55f);
            Play(SfxCue.Heartbeat, Category.Soul, 0.7f, 1f);
        }

        private void UpdateLowPass(float dt)
        {
            float target = _soul != null && _soul.IsFree ? _mix.SoulLowPassHz : OpenCutoffHz;
            // Sweep in log-frequency so the muffle sounds even, not stuck high then sudden.
            float k = 1f - Mathf.Exp(-dt / _mix.LowPassSweep);
            float log = Mathf.Lerp(Mathf.Log(_lowPass.cutoffFrequency), Mathf.Log(target), k);
            _lowPass.cutoffFrequency = Mathf.Exp(log);
        }

        private void UpdateMusic(float now, float dt)
        {
            VesselRuntime body = _root.PlayerVessel;
            var context = new MusicContext
            {
                RunStarted = _runStarted,
                Paused = _runEnded || _root.Time.IsPaused,
                BossFight = _waves != null && _waves.IsBossWave,
                Won = _won,
                RemainingEnemies = _waves != null ? _waves.RemainingEnemies : 0,
                PlayerHp01 = body != null && !body.IsDead ? body.Hp / Mathf.Max(1f, body.MaxHp.Value) : 1f
            };
            _duck01 = now < _duckHoldUntil ? 1f : Mathf.MoveTowards(_duck01, 0f, dt / _mix.DuckRelease);
            float duck = Mathf.Lerp(1f, Mathf.Pow(10f, _mix.DuckDb / 20f), _duck01);
            _music.Update(context, duck, dt);
        }

        private void OnAttackStarted(AttackStarted e)
        {
            AttackDefinition a = e.Attack;
            bool player = e.Attacker == _root.PlayerVessel;
            float vol = player ? _mix.PlayerSwingVolume : _mix.EnemySwingVolume;
            if (a.Delivery == AttackDelivery.Projectile) Play(e.Attacker.Class == VesselClass.Mage ? SfxCue.FireCast : SfxCue.BowRelease, Category.Swing, vol, Jitter());
            else if (a.Delivery == AttackDelivery.Cone) Play(SfxCue.FireCast, Category.Swing, vol, 0.8f);
            else Play(a.Tier == HitTier.Weak ? SfxCue.SwingLight : SfxCue.SwingHeavy, Category.Swing, vol, Jitter());

            if (player && a.Tier != HitTier.Weak && Random.value < _mix.AttackShoutChance) PlayVoice(e.Attacker.Class, VoiceLine.Attack);
            if (!player && a.Telegraph != TelegraphShape.None && a.Tier != HitTier.Weak) PlayTelegraph(a.Startup);
        }

        /// <summary>A7: pitch the rising growl so it ends exactly when the attack's startup does.</summary>
        private void PlayTelegraph(float startup)
        {
            AudioClip clip = Pick(SfxCue.Telegraph);
            Vector2 range = _mix.TelegraphPitchRange;
            float pitch = Mathf.Clamp(clip.length / Mathf.Max(0.05f, startup), range.x, range.y);
            PlayClip(clip, Category.World, _mix.TelegraphVolume * _mix.SfxVolume, pitch);
        }

        private void OnHit(HitConfirmed e)
        {
            VesselRuntime player = _root.PlayerVessel;
            HitRoute route = HitRouting.Classify(e.Attacker != null && e.Attacker == player, e.Target == player);
            bool heavy = e.Tier != HitTier.Weak || e.IsKill;
            SfxCue body = BodyCue(e);
            float now = Time.unscaledTime;

            switch (route)
            {
                case HitRoute.PlayerDealt:
                {
                    float vol = _mix.PlayerDealtVolume;
                    float pitch = MusicScale.SemitonesToPitch(_combo.Register(now) * _mix.ComboSemitonesPerStep) * Jitter();
                    Play(SfxCue.HitTransient, Category.HitLayer, vol * _mix.TransientVolume, pitch);
                    Play(body, Category.Hit, vol, pitch);
                    if (heavy) Play(SfxCue.HitSub, Category.HitLayer, vol, Jitter());
                    if (e.IsKill) _killChimeAt = now + _mix.KillChimeDelay;
                    if (heavy || (e.Target != null && e.Target.State.Kind == VesselKind.Boss)) Duck(now);
                    break;
                }
                case HitRoute.PlayerHurt:
                {
                    float vol = _mix.PlayerHurtVolume;
                    Play(SfxCue.PlayerHurt, Category.Hit, vol, Jitter());
                    Play(SfxCue.HitSub, Category.HitLayer, vol, Jitter());
                    if (now >= _nextHurtVoice)
                    {
                        _nextHurtVoice = now + _mix.HurtVoiceCooldown;
                        PlayVoice(player.Class, VoiceLine.Hurt);
                    }
                    Duck(now); // getting hurt is the one sound the player must never miss
                    break;
                }
                default:
                    Play(body, Category.Hit, _mix.BystanderVolume, Jitter());
                    break;
            }
        }

        /// <summary>A2 weapon body: sword = metal, arrow = wood / pierce, fire = burst.</summary>
        private static SfxCue BodyCue(HitConfirmed e)
        {
            if (e.Attack == null) return SfxCue.HitMetal;
            switch (e.Attack.Delivery)
            {
                case AttackDelivery.Projectile:
                    return e.Attacker != null && e.Attacker.Class == VesselClass.Mage ? SfxCue.HitFire : SfxCue.HitWood;
                case AttackDelivery.Cone:
                    return SfxCue.HitFire;
                default:
                    return SfxCue.HitMetal;
            }
        }

        private void OnEvaded(HitEvaded e) => Play(SfxCue.Dodge, Category.Swing, 0.4f, 1.4f);

        private void OnDodge(DodgeStarted e) =>
            Play(SfxCue.Dodge, Category.Swing, e.Vessel == _root.PlayerVessel ? _mix.PlayerSwingVolume : _mix.EnemySwingVolume, Jitter());

        private void OnDied(VesselDied e)
        {
            Play(SfxCue.Death, Category.World, _mix.DeathVolume, Jitter());
            PlayVoice(e.Vessel.Class, VoiceLine.Death); // the body's own voice, whoever was inside
        }

        private void OnSoulExited(SoulExited e) => Play(SfxCue.SoulExit, Category.Soul, _mix.SoulVolume, 1f);

        private void OnPossessed(PossessionSucceeded e)
        {
            _lowPass.cutoffFrequency = OpenCutoffHz; // A4: the world snaps back into focus on entry
            Play(SfxCue.SoulEnter, Category.Soul, _mix.SoulVolume, e.FirstClaim ? 1f : 0.85f);
        }

        private void OnExpired(SoulExpired e) => Play(SfxCue.SoulExpire, Category.Soul, _mix.SoulVolume, 1f);

        private void OnEmber(EmberCollected e)
        {
            // A6: chained pickups climb the D minor pentatonic, in key with the music.
            int step = Mathf.Min(e.Combo, _mix.EmberMaxStep);
            Play(SfxCue.Ember, Category.Ember, _mix.EmberVolume, MusicScale.SemitonesToPitch(MusicScale.PentatonicSemitones(step)));
            if (e.Banked > 0) Play(SfxCue.Upgrade, Category.Ui, _mix.UiVolume, MusicScale.SemitonesToPitch(12f));
        }

        private void OnUpgrade(UpgradeChosen e) => Play(SfxCue.Upgrade, Category.Ui, _mix.UiVolume, 1f);

        private void OnBossPhase(BossPhaseChanged e)
        {
            Play(SfxCue.HitSub, Category.World, 1f, 0.8f);
            Play(SfxCue.KillChime, Category.World, _mix.UiVolume, 0.5f);
            if (_bossPhaseVoice.Length > 0)
                PlayClip(_bossPhaseVoice[_bossPhaseCycler.Next(_bossPhaseVoice.Length, _rng)], Category.Voice, _mix.VoiceVolume, 1f);
            Duck(Time.unscaledTime);
        }

        private void OnAttackReleased(AttackReleased e)
        {
            if (e.Attack.Delivery != AttackDelivery.Ring) return;
            Play(SfxCue.RingSlam, Category.World, _mix.RingSlamVolume, Jitter());
            Play(SfxCue.RingRumble, Category.World, _mix.RingRumbleVolume, Jitter());
            Play(SfxCue.Death, Category.World, _mix.RingSlamVolume, 0.6f); // a body-falling thud, slowed: real weight under the synthesized boom
            Play(SfxCue.HitSub, Category.World, 1f, 0.7f);
            Duck(Time.unscaledTime);
        }

        private void OnBossAttackAnnounced(BossAttackAnnounced e)
        {
            Play(SfxCue.RingCharge, Category.World, _mix.RingChargeVolume, 1f);
            float now = Time.unscaledTime;
            if (_bossPhaseVoice.Length == 0 || now < _nextBossShout) return;
            _nextBossShout = now + _mix.BossShoutCooldown;
            PlayClip(_bossPhaseVoice[_bossPhaseCycler.Next(_bossPhaseVoice.Length, _rng)], Category.Voice, _mix.VoiceVolume, 1f);
        }

        private void OnWaveStarted(WaveStarted e)
        {
            _runStarted = true;
            if (_waves == null || !_waves.IsBossWave) _music.OnWaveStarted(e.WaveNumber); // a boss wave opens straight on the boss's music
        }
        private void OnWaveCleared(WaveCleared e) => Play(SfxCue.WaveCleared, Category.Ui, _mix.StingerVolume, 1f);

        private void OnRunEnded(RunEnded e)
        {
            _runEnded = true;
            _won = e.Victory;
            Play(e.Victory ? SfxCue.RunVictory : SfxCue.RunDefeat, Category.Ui, _mix.StingerVolume, 1f);
        }

        private void Duck(float now) => _duckHoldUntil = Mathf.Max(_duckHoldUntil, now + _mix.DuckHold);

        private float Jitter() => 1f + Random.Range(-_mix.PitchJitter, _mix.PitchJitter);

        private AudioClip Pick(SfxCue cue)
        {
            AudioClip[] clips = _sfx[(int)cue];
            return clips[_sfxCyclers[(int)cue].Next(clips.Length, _rng)];
        }

        private void Play(SfxCue cue, Category category, float volume, float pitch) =>
            PlayClip(Pick(cue), category, volume * _mix.SfxVolume, pitch);

        /// <summary>Voices have no synthesized fallback: an empty slot stays silent.</summary>
        private void PlayVoice(VesselClass vesselClass, VoiceLine line)
        {
            int i = (int)vesselClass * VoiceLineCount + (int)line;
            AudioClip[] clips = _voiceLines[i];
            if (clips.Length == 0) return;
            PlayClip(clips[_voiceCyclers[i].Next(clips.Length, _rng)], Category.Voice, _mix.VoiceVolume, 1f);
        }

        private void PlayClip(AudioClip clip, Category category, float volume, float pitch)
        {
            int cap = CategoryCaps[(int)category];
            int playing = 0;
            int free = -1;
            int oldestSame = -1;
            float oldestTime = -1f;
            for (int i = 0; i < _sources.Length; i++)
            {
                AudioSource s = _sources[i];
                if (!s.isPlaying)
                {
                    if (free < 0) free = i;
                    continue;
                }
                if (_sourceCategory[i] != category) continue;
                playing++;
                if (s.time > oldestTime)
                {
                    oldestTime = s.time;
                    oldestSame = i;
                }
            }

            int slot = playing >= cap ? oldestSame : free;
            if (slot < 0) return;
            AudioSource src = _sources[slot];
            src.Stop();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            _sourceCategory[slot] = category;
            src.Play();
        }

        private AudioSource NewSource(bool loop)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.loop = loop;
            return s;
        }

        private AudioSource MusicSource()
        {
            AudioSource s = NewSource(true);
            s.volume = 0f;
            s.priority = 0; // music must never be stolen by a burst of hits
            return s;
        }
    }
}
