using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// ESC로 게임을 멈추고 메뉴를 띄운다. 메뉴 선택은 버튼 클릭이 아니라 타이핑으로 한다 -
// 멈춘 동안 입력창에 "계속"/"타이틀"을 쳐서 고른다.
//
// 전투의 단어 조합(WordChainManager/CardInputHandler)과는 완전히 별개의 경로다. 명령 단어는
// 카드도 사전 단어도 아니므로 InputManager의 이벤트를 여기서 직접 받아 처리한다.
//
// Time.timeScale = 0 하나로 턴 전환 대기(DeckManager/StageManager)·말풍선(BattleManager)·
// 타이머 감소(TimerManager)·카드 애니메이션(CardSlotView/HandFanLayout)이 전부 멈춘다 -
// 시간에 의존하는 코드가 모두 deltaTime/WaitForSeconds 기반이라 개별 정지 처리는 하지 않는다.
public class PauseManager : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("일시정지 창 루트. 평소엔 비활성이어야 한다.")]
    [SerializeField] private GameObject pausePanel;

    [Header("References")]
    [SerializeField] private InputManager inputManager;

    [Header("명령 단어")]
    [Tooltip("치면 일시정지가 풀리는 단어")]
    [SerializeField] private string resumeWord = "계속";

    [Tooltip("치면 타이틀 씬으로 돌아가는 단어")]
    [SerializeField] private string titleWord = "타이틀";

    private bool _isPaused;
    private bool _inputWasEnabled;

    public bool IsPaused => _isPaused;

    private void Awake()
    {
        // 이전 플레이에서 멈춘 채로 씬을 다시 불러왔을 수 있다.
        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);
        else
            Debug.LogWarning("PauseManager: pausePanel이 연결되지 않았습니다.", this);

        if (inputManager == null)
            Debug.LogWarning("PauseManager: inputManager가 연결되지 않아 명령 단어를 입력받을 수 없습니다.", this);
    }

    private void OnEnable()
    {
        if (inputManager == null)
            return;

        // 조합 중에도 평가해야 하므로 두 이벤트를 모두 구독한다(이유는 Evaluate 주석 참조).
        inputManager.OnCharacterEntered += HandleCharacterEntered;
        inputManager.OnCompositionChanged += HandleCompositionChanged;
    }

    private void OnDisable()
    {
        if (inputManager == null)
            return;

        inputManager.OnCharacterEntered -= HandleCharacterEntered;
        inputManager.OnCompositionChanged -= HandleCompositionChanged;
    }

    // Update는 timeScale 0에서도 계속 돌기 때문에 멈춘 상태에서도 ESC를 받을 수 있다.
    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (_isPaused)
            Resume();
        else
            Pause();
    }

    // 글자가 커밋되는 순간 Composition은 아직 방금 커밋된 옛 값을 들고 있을 수 있다.
    // 그걸 이어붙이면 "계계"처럼 중복되므로, 커밋 경로에서는 조합 문자열을 비워서 평가한다.
    private void HandleCharacterEntered(char _)
    {
        Evaluate(string.Empty);
    }

    private void HandleCompositionChanged(string composing)
    {
        Evaluate(composing);
    }

    // 조합 중에도 평가하는 이유는 CardInputHandler와 같다 - "계속"의 마지막 음절 "속"은
    // 뒤에 이어질 글자가 없어 IME가 영영 커밋하지 않는다. 커밋만 기다리면 명령 단어가
    // 절대 완성되지 않는다.
    private void Evaluate(string composing)
    {
        if (!_isPaused || inputManager == null)
            return;

        var committed = inputManager.CurrentInput;
        var typed = committed + composing;

        // ClearInput()이 OnCompositionChanged를 발생시켜 이 메서드가 재진입한다.
        // 그때는 입력이 비어 있으므로 여기서 빠져나가 무한 재귀가 되지 않는다.
        if (typed.Length == 0)
            return;

        if (typed == resumeWord)
        {
            inputManager.ClearInput();
            Resume();
            return;
        }

        if (typed == titleWord)
        {
            inputManager.ClearInput();
            ReturnToTitle();
            return;
        }

        // 두 단어 중 어느 쪽으로도 진행 중이 아니면 오타다. 입력창을 비워 처음부터 다시 치게 한다.
        // 매칭 판정은 전투 쪽과 같은 기준(InputManager.IsValidProgress)을 쓴다.
        if (InputManager.IsValidProgress(committed, composing, resumeWord) ||
            InputManager.IsValidProgress(committed, composing, titleWord))
            return;

        inputManager.ClearInput();
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
            // EnableInput은 한글 모드까지 다시 강제하므로 명령 단어를 바로 칠 수 있다.
            inputManager.EnableInput();

            // 치다 만 글자가 명령 단어와 섞이지 않게 비운다.
            inputManager.ClearInput();
        }

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
        SceneManager.LoadScene(GameScenes.Title);
    }
}
