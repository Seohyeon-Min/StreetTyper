using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ESC로 게임을 멈추고 메뉴를 띄운다. 메뉴 선택은 버튼 클릭이 아니라 타이핑으로 한다 -
// 멈춘 동안 입력창에 "계속"/"타이틀"을 쳐서 고른다.
//
// 매칭 파이프라인은 TypingReceiver가, 안내 문구 조립은 CommandWordReceiver가 맡는다.
// 여기 남은 것은 "언제 내 차례인가"(멈춰 있을 때)와 "맞혔을 때 무엇을 하는가"뿐이다.
//
// Time.timeScale = 0 하나로 턴 전환 대기(DeckManager/StageManager)·말풍선(BattleManager)·
// 타이머 감소(TimerManager)·카드 애니메이션(CardSlotView/HandFanLayout)이 전부 멈춘다 -
// 시간에 의존하는 코드가 모두 deltaTime/WaitForSeconds 기반이라 개별 정지 처리는 하지 않는다.
public class PauseManager : CommandWordReceiver
{
    [Header("UI")]
    [Tooltip("일시정지 창 루트. 평소엔 비활성이어야 한다.")]
    [SerializeField] private GameObject pausePanel;

    [Header("명령 단어")]
    [SerializeField]
    private TypedCommand resumeCommand = new TypedCommand(
        "계속", "resume",
        "계속 진행을 원한다면 \"{0}\"!",
        "Type \"{0}\" to keep playing!");

    [SerializeField]
    private TypedCommand titleCommand = new TypedCommand(
        "타이틀", "title",
        "타이틀로 돌아가길 원한다면 \"{0}\"!",
        "Type \"{0}\" to return to the title!");

    private bool _isPaused;
    private bool _inputWasEnabled;

    // 명령 단어를 담아둘 버퍼. 글자마다 Targets가 불리므로 매번 새로 만들지 않는다.
    private readonly string[] _targets = new string[2];

    public bool IsPaused => _isPaused;

    /// 일시정지는 무엇보다 우선한다 - 멈춘 화면에서 명령 단어의 첫 글자가 손패 쪽으로 새면
    /// 그 자리에서 오타 처리되어 명령 단어를 끝까지 칠 수 없다.
    public override TypingPriority Priority => TypingPriority.Pause;

    public override bool WantsInput() => _isPaused;

    protected override IReadOnlyList<string> Targets
    {
        get
        {
            _targets[0] = resumeCommand.Word(this, nameof(resumeCommand));
            _targets[1] = titleCommand.Word(this, nameof(titleCommand));
            return _targets;
        }
    }

    public override string BuildHint()
    {
        return JoinHints(
            resumeCommand.Hint(this, nameof(resumeCommand)),
            titleCommand.Hint(this, nameof(titleCommand)));
    }

    protected override void OnCommandMatched(int index, bool wasComposing)
    {
        if (index == 0)
            Resume();
        else
            ReturnToTitle();
    }

    private void Awake()
    {
        // 이전 플레이에서 멈춘 채로 씬을 다시 불러왔을 수 있다.
        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);
        else
            Debug.LogWarning("PauseManager: pausePanel이 연결되지 않았습니다.", this);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (inputManager != null)
            inputManager.OnCancel += HandleCancel;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (inputManager != null)
            inputManager.OnCancel -= HandleCancel;
    }

    // ESC는 InputManager가 준다. 그쪽에서 입력 잠금(_inputEnabled)보다 위에서 읽으므로
    // 턴 전환 대기처럼 타이핑이 잠긴 구간에서도 일시정지가 걸린다.
    private void HandleCancel()
    {
        if (_isPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        if (_isPaused)
            return;

        _isPaused = true;

        // 멈추기 전 입력 상태를 기억해 뒀다가 재개할 때 그대로 되돌린다.
        _inputWasEnabled = inputManager != null && inputManager.IsInputEnabled;

        if (inputManager != null)
        {
            // 메뉴를 타이핑으로 고르므로 입력을 끄지 않는다 - 오히려 꺼져 있었다면 켠다.
            // (턴 전환 대기나 결과 화면에서 멈췄다면 원래 잠겨 있다.)
            // EnableInput은 IME 모드까지 다시 맞춰주므로 명령 단어를 바로 칠 수 있다.
            inputManager.EnableInput();

            // ⚠️ ClearInput을 따로 불러야 한다. EnableInput은 이미 켜져 있으면 곧바로 리턴해서
            // 내부의 ClearInput까지 건너뛰는데, 일시정지는 보통 입력이 켜진 플레이어 턴 중에
            // 걸린다 - 그때 치다 만 글자가 남아 명령 단어에 섞인다.
            inputManager.ClearInput();
        }

        RefreshHint();

        if (pausePanel != null)
            pausePanel.SetActive(true);

        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (!_isPaused)
            return;

        _isPaused = false;
        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (inputManager != null)
        {
            // 명령 단어가 입력창에 남아 전투 입력으로 흘러가지 않게 비운다.
            inputManager.ClearInput();

            // 멈추기 전에 잠겨 있었다면(턴 전환 대기 등) 도로 잠근다.
            if (!_inputWasEnabled)
                inputManager.DisableInput();
        }
    }

    public void ReturnToTitle()
    {
        if (inputManager != null)
            inputManager.ClearInput();

        // 씬을 넘어가도 timeScale은 유지된다 - 여기서 되돌리지 않으면 타이틀이 멈춘 채로 뜬다.
        Time.timeScale = 1f;

        if (SoundManager.Instance != null)
            SoundManager.Instance.StopBGM();

        SceneManager.LoadScene(GameScenes.Title);
    }
}
