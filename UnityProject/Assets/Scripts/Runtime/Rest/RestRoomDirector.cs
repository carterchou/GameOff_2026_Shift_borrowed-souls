using System.Collections;
using System.Collections.Generic;
using BorrowedSouls.Core.Meta;
using BorrowedSouls.Data;
using BorrowedSouls.Debugging;
using BorrowedSouls.Meta;
using BorrowedSouls.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedSouls.Rest
{
    /// <summary>
    /// The rest room is the main menu. The menu (or a finished run's results) is on the left from the first frame, with three empty bodies
    /// slumped on the bench, while the soul drifts in from the right and lands in the chosen one, which sits up. Nothing waits for that, so a
    /// player restarting for the fifth time can press Start at once. Choosing another body sends the soul across to it. Starting a run
    /// stands the body up and walks it out to the right, then the loading page leads to the arena.
    ///
    /// </summary>
    public sealed class RestRoomDirector : MonoBehaviour
    {
        private static readonly int Slumped = Animator.StringToHash("Slumped");
        private static readonly int SitUp = Animator.StringToHash("SitUp");
        private static readonly int SeatedIdle = Animator.StringToHash("SeatedIdle");
        private static readonly int StandUp = Animator.StringToHash("StandUp");
        private static readonly int Walk = Animator.StringToHash("Walk");

        /// <summary>The bodies from the left of the screen to the right, the order their buttons sit in.</summary>
        private static readonly StartingBody[] BenchOrder = { StartingBody.Archer, StartingBody.Swordsman, StartingBody.Mage };

        public enum Stage
        {
            Results,
            Menu,
            Talents,
            Controls,
            Credits,
            Leaving
        }

        [Header("Stage")]
        [Tooltip("One per StartingBody, in that order.")]
        [SerializeField] private Animator[] _bodies = new Animator[0];
        [SerializeField] private Transform _soul;
        [SerializeField] private Transform _soulEntry;
        [Tooltip("Where the soul enters a body, from the body's feet.")]
        [SerializeField] private Vector3 _soulLanding = new Vector3(0f, 1.2f, 0.25f);
        [SerializeField] private ParticleSystem _landingFlash;
        [Tooltip("Off screen. Walkers head for its side of the room along its line.")]
        [SerializeField] private Transform _exit;
        [SerializeField] private ScreenFade _fade;
        [SerializeField] private LoadingScreen _loading;
        [SerializeField] private TalentCatalog _talents;

        [Header("Menu")]
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private Button _start;
        [SerializeField] private Button _talentsButton;
        [SerializeField] private Button _controls;
        [SerializeField] private Button _credits;
        [SerializeField] private Button _quit;
        [SerializeField] private Text _crystals;
        [SerializeField] private TalentPanel _talentPanel;
        [SerializeField] private GameObject _controlsPanel;
        [SerializeField] private Button _controlsBack;
        [SerializeField] private GameObject _creditsPanel;
        [SerializeField] private Text _creditsText;
        [SerializeField] private TextAsset _creditsSource;
        [SerializeField] private Button _creditsBack;

        [Header("Choice of body")]
        [Tooltip("One per StartingBody, in that order.")]
        [SerializeField] private Button[] _bodyButtons = new Button[0];
        [SerializeField] private GameObject[] _bodyMarks = new GameObject[0];
        [SerializeField] private Text _bodyNote;
        [SerializeField] private string[] _bodyNotes = new string[0];

        [Header("Results")]
        [SerializeField] private GameObject _resultsPanel;
        [SerializeField] private Text _resultsTitle;
        [SerializeField] private Text _resultsBody;
        [SerializeField] private Button _resultsContinue;

        [Header("Timing (seconds)")]
        [SerializeField, Min(0.1f)] private float _soulFlight = 2.2f;
        [SerializeField, Min(0.1f)] private float _soulHop = 0.5f;
        [SerializeField, Min(0.1f)] private float _sitUp = 1.3f;
        [SerializeField, Min(0.1f)] private float _standUp = 0.95f;
        [SerializeField, Min(0f)] private float _stepClear = 0.5f;
        [SerializeField, Min(0.1f)] private float _minWalk = 0.7f;
        [SerializeField, Min(0.1f)] private float _walkSpeed = 1.55f;
        [SerializeField, Min(0.05f)] private float _fadeOut = 0.7f;
        [SerializeField, Min(0.01f)] private float _blendSeconds = 0.18f;
        [Tooltip("Standing straight up from a slump, when Start is pressed before the soul has landed.")]
        [SerializeField, Min(0.01f)] private float _wakeBlendSeconds = 0.35f;

        [Header("The walk out")]
        [Tooltip("The fade starts once the walker is this close to the exit, by when it is leaving the frame.")]
        [SerializeField, Min(0f)] private float _fadeDistance = 3.8f;
        [Tooltip("How far ahead along the room the walker steers: larger joins its line more gently.")]
        [SerializeField, Min(0.1f)] private float _steerAhead = 1.2f;
        [SerializeField, Min(1f)] private float _turnDegreesPerSecond = 180f;

        private StartingBody _chosen;
        private Coroutine _soulTrip;
        private bool _hasStarted;

        public Stage Current { get; private set; }
        public StartingBody Chosen => _chosen;

        /// <summary>True once the soul has landed and the chosen body has finished sitting up.</summary>
        public bool SoulHome { get; private set; }

        private void Awake()
        {
            _start.onClick.AddListener(() => StartCoroutine(WalkOut()));
            _talentsButton.onClick.AddListener(OpenTalents);
            _controls.onClick.AddListener(OpenControls);
            _controlsBack.onClick.AddListener(ShowMenu);
            _credits.onClick.AddListener(OpenCredits);
            _creditsBack.onClick.AddListener(ShowMenu);
            _quit.onClick.AddListener(Quit);
            _quit.gameObject.SetActive(CanQuit);
            _resultsContinue.onClick.AddListener(ShowMenu);
            _talentPanel.Closed += ShowMenu;
            for (int i = 0; i < _bodyButtons.Length; i++)
            {
                var body = (StartingBody)i;
                _bodyButtons[i].onClick.AddListener(() => Choose(body));
            }
            _creditsText.text = _creditsSource != null ? _creditsSource.text : string.Empty;
            HidePanels();
        }

        private void Start()
        {
            if (AutoPilot.RequestedAtLaunch)
            {
                StartCoroutine(_loading.Load(GameScenes.Arena)); // a bot run goes straight in, by the road a player takes
                return;
            }
            _chosen = ProfileStore.Current(_talents).Body;
            RunTransfer.Summary? summary = RunTransfer.Take();
            foreach (Animator body in _bodies) body.Play(Slumped);
            if (summary.HasValue) ShowResults(summary.Value);
            else ShowMenu();
            _soulTrip = StartCoroutine(SoulInto(_chosen, _soulEntry.position, _soulFlight, 0.35f));
        }

        private void Update()
        {
            if ((Current == Stage.Controls || Current == Stage.Credits) && MenuInput.Back) ShowMenu();
        }

        private Animator BodyOf(StartingBody body) => _bodies[(int)body];

        private Vector3 LandingIn(StartingBody body) => BodyOf(body).transform.position + _soulLanding;

        private void PlaceSoul(Vector3 position)
        {
            if (_soul.gameObject.activeSelf) return; // already in the air: it just changes course
            _soul.position = position;
            _soul.gameObject.SetActive(true);
            _soul.GetComponentInChildren<TrailRenderer>().Clear(); // or it streaks from wherever it last was
        }

        /// <summary>The soul flies to a body and lands; the body sits up.</summary>
        private IEnumerator SoulInto(StartingBody body, Vector3 from, float seconds, float arc)
        {
            SoulHome = false;
            PlaceSoul(from);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                FlySoul(from, LandingIn(body), t / seconds, arc);
                yield return null;
            }
            _soul.gameObject.SetActive(false);
            Flash(LandingIn(body));
            Animator animator = BodyOf(body);
            animator.CrossFadeInFixedTime(SitUp, 0.05f);
            for (float t = 0f; t < _sitUp; t += Time.unscaledDeltaTime) yield return null;
            animator.CrossFadeInFixedTime(SeatedIdle, _blendSeconds);
            SoulHome = true;
            _soulTrip = null;
        }

        private void FlySoul(Vector3 from, Vector3 to, float u, float arc)
        {
            float eased = u * u * (3f - 2f * u);
            Vector3 path = Vector3.Lerp(from, to, eased);
            path.y += Mathf.Sin(u * Mathf.PI) * arc;
            _soul.position = path;
        }

        private void Flash(Vector3 position)
        {
            if (_landingFlash == null) return;
            _landingFlash.transform.position = position;
            _landingFlash.Play(true);
        }

        /// <summary>The soul leaves one body, which slumps, and crosses to the next, which sits up.</summary>
        private void Choose(StartingBody body)
        {
            if (Current != Stage.Menu || body == _chosen) return;
            StartingBody previous = _chosen;
            _chosen = body;
            ProfileStore.Current(_talents).ChooseBody(body);
            ProfileStore.Save(_talents);
            ShowChoice();
            if (_soulTrip != null) StopCoroutine(_soulTrip);
            BodyOf(previous).CrossFadeInFixedTime(Slumped, 0.3f);
            Vector3 from = _soul.gameObject.activeSelf ? _soul.position : LandingIn(previous); // a second press catches it in the air
            _soulTrip = StartCoroutine(SoulInto(body, from, _soulHop, 0.2f));
        }

        private void ShowResults(in RunTransfer.Summary s)
        {
            HidePanels();
            Current = Stage.Results;
            _resultsTitle.text = s.Victory ? "魂祭場解放" : "靈魂消散";
            int minutes = (int)(s.Seconds / 60f);
            string pay = _talents.Released ? $"\n\n獲得魂晶  {s.Crystals}" : string.Empty;
            _resultsBody.text = $"通過波次  {s.WavesCleared}\n時間  {minutes}:{(int)s.Seconds % 60:00}\n擊殺  {s.Kills}{pay}";
            _resultsPanel.SetActive(true);
            Select(_resultsContinue);
        }

        private void ShowMenu()
        {
            HidePanels();
            Current = Stage.Menu;
            _talentsButton.gameObject.SetActive(_talents.Released);
            _crystals.gameObject.SetActive(_talents.Released);
            if (_talents.Released) _crystals.text = $"魂晶  {ProfileStore.Current(_talents).Crystals}";
            ShowChoice();
            _menuPanel.SetActive(true);
            Select(_start);
        }

        private void ShowChoice()
        {
            for (int i = 0; i < _bodyMarks.Length; i++) _bodyMarks[i].SetActive(i == (int)_chosen);
            _bodyNote.text = _bodyNotes[(int)_chosen];
            LinkMenu();
        }

        /// <summary>
        /// Up and down run through the buttons that are showing and pass through the row of bodies, entering it at the chosen one;
        /// left and right walk along that row. Set here, since which buttons show depends on what has been released.
        /// </summary>
        /// <summary>A page in a browser can't close itself, so there it has no way out to offer.</summary>
        private static bool CanQuit => Application.platform != RuntimePlatform.WebGLPlayer;

        private void LinkMenu()
        {
            var column = new List<Selectable> { _start };
            if (_talents.Released) column.Add(_talentsButton);
            column.Add(_controls);
            column.Add(_credits);
            if (CanQuit) column.Add(_quit);
            Selectable chosen = _bodyButtons[(int)_chosen];
            for (int i = 0; i < column.Count; i++)
            {
                column[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? column[i - 1] : chosen,
                    selectOnDown = i < column.Count - 1 ? column[i + 1] : chosen
                };
            }
            int count = BenchOrder.Length;
            for (int seat = 0; seat < count; seat++)
            {
                _bodyButtons[(int)BenchOrder[seat]].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = _bodyButtons[(int)BenchOrder[(seat + count - 1) % count]],
                    selectOnRight = _bodyButtons[(int)BenchOrder[(seat + 1) % count]],
                    selectOnUp = column[column.Count - 1],
                    selectOnDown = _start
                };
            }
        }

        private void OpenTalents()
        {
            HidePanels();
            Current = Stage.Talents;
            _talentPanel.Open();
        }

        private void OpenControls()
        {
            HidePanels();
            Current = Stage.Controls;
            _controlsPanel.SetActive(true);
            Select(_controlsBack);
        }

        private void OpenCredits()
        {
            HidePanels();
            Current = Stage.Credits;
            _creditsPanel.SetActive(true);
            Select(_creditsBack);
        }

        private void HidePanels()
        {
            _menuPanel.SetActive(false);
            _resultsPanel.SetActive(false);
            _controlsPanel.SetActive(false);
            _creditsPanel.SetActive(false);
            _talentPanel.Hide();
        }

        private static void Select(Selectable selectable)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        /// <summary>Up off the bench, a step clear of it, a turn towards the way out, and out of the frame as the screen fades; then the loading page.</summary>
        private IEnumerator WalkOut()
        {
            if (_hasStarted) yield break;
            _hasStarted = true;
            HidePanels();
            Current = Stage.Leaving;
            if (_soulTrip != null) StopCoroutine(_soulTrip);
            if (_soul.gameObject.activeSelf)
            {
                _soul.gameObject.SetActive(false); // Start beat the soul home: it drops straight into the body
                Flash(LandingIn(_chosen));
            }

            Animator walker = BodyOf(_chosen);
            Transform body = walker.transform;
            walker.CrossFadeInFixedTime(StandUp, SoulHome ? _blendSeconds : _wakeBlendSeconds);
            Vector3 seat = body.position;
            Vector3 clear = seat + body.forward * _stepClear;
            for (float t = 0f; t < _standUp; t += Time.unscaledDeltaTime)
            {
                body.position = Vector3.Lerp(seat, clear, Mathf.SmoothStep(0f, 1f, t / _standUp));
                yield return null;
            }

            walker.CrossFadeInFixedTime(Walk, _blendSeconds);
            float walked = 0f;
            float stopAt = float.PositiveInfinity;
            while (walked < stopAt)
            {
                Vector3 toExit = _exit.position - body.position;
                var heading = new Vector3(Mathf.Sign(toExit.x) * _steerAhead, 0f, toExit.z);
                body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(heading), _turnDegreesPerSecond * Time.unscaledDeltaTime);
                body.position += body.forward * (_walkSpeed * Time.unscaledDeltaTime);
                if (float.IsPositiveInfinity(stopAt) && walked >= _minWalk && Mathf.Abs(toExit.x) <= _fadeDistance)
                {
                    StartCoroutine(_fade.FadeTo(1f, _fadeOut));
                    stopAt = walked + _fadeOut;
                }
                walked += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return _loading.Load(GameScenes.Arena);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
