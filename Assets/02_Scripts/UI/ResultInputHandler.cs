using UnityEngine;

// 결과 화면에서 명령 단어를 타이핑해 다음 스테이지로 넘어가거나 재도전한다.
// 타이핑 게임이므로 결과 화면만 숫자 키를 쓰지 않게 하려는 것이고,
// 구조는 PauseManager("계속"/"타이틀")와 같다.
public class ResultInputHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputManager inputManager;
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private StageManager stageManager;

    [Tooltip("승리인지 패배인지 판별하는 데 쓴다.")]
    [SerializeField] private CharacterStats player;

    [Header("명령 단어")]
    [Tooltip("승리 화면에서 치면 다음 스테이지로 넘어가는 단어")]
    [SerializeField] private string nextWord = "다음";

    [Tooltip("패배 화면에서 치면 같은 스테이지를 다시 시작하는 단어")]
    [SerializeField] private string retryWord = "다시";

    [Header("안내 문구")]
    [Tooltip("승리 화면에 덧붙일 안내. {0} 자리에 위 명령 단어가 들어간다. " +
             "받침 유무로 조사가 달라져 승리/패배 형식을 따로 둔다.")]
    [SerializeField] private string nextHintFormat = "\n\n\"{0}\"을 입력하세요";

    [Tooltip("패배 화면에 덧붙일 안내.")]
    [SerializeField] private string retryHintFormat = "\n\n\"{0}\"를 입력하세요";

    [SerializeField] private bool logDebugEvents;

    /// <summary>결과 화면에 붙일 안내 문구. 명령 단어를 인스펙터에서 바꾸면 안내도 함께 따라가도록
    /// 문구를 여기서 만든다 - 호출부에 하드코딩하면 단어를 바꿨을 때 조용히 어긋난다.</summary>
    public string GetHintText(bool isVictory)
    {
        var word = isVictory ? nextWord : retryWord;
        var format = isVictory ? nextHintFormat : retryHintFormat;

        return string.IsNullOrEmpty(format) ? string.Empty : string.Format(format, word);
    }

    private void Awake()
    {
        if (inputManager == null)
            Debug.LogWarning("ResultInputHandler: inputManager가 연결되지 않아 명령 단어를 받을 수 없습니다.", this);

        if (battleManager == null)
            Debug.LogWarning("ResultInputHandler: battleManager가 연결되지 않아 결과 화면인지 알 수 없습니다.", this);

        if (stageManager == null)
            Debug.LogWarning("ResultInputHandler: stageManager가 연결되지 않아 스테이지를 넘길 수 없습니다.", this);
    }

    private void OnEnable()
    {
        if (inputManager == null)
            return;

        // 조합 중에도 평가해야 한다 - "다음"의 마지막 음절 "음"은 뒤에 이어질 글자가 없어
        // IME가 영원히 커밋하지 않는다. 커밋만 기다리면 명령 단어가 완성되지 않는다.
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

    // 글자가 커밋되는 순간 Composition은 아직 방금 커밋된 옛 값을 들고 있을 수 있다.
    // 그걸 이어붙이면 "다다"처럼 중복되므로, 커밋 경로에서는 조합 문자열을 비워서 평가한다.
    private void HandleCharacterEntered(char _)
    {
        Evaluate(string.Empty);
    }

    private void HandleCompositionChanged(string composing)
    {
        Evaluate(composing);
    }

    private void Evaluate(string composing)
    {
        // 결과 화면일 때만 동작한다. 전투 중에는 CardInputHandler가 타이핑을 가져간다.
        if (battleManager == null || !battleManager.IsGameOver || inputManager == null)
            return;

        var committed = inputManager.CurrentInput;
        var typed = committed + composing;

        // ClearInput()이 OnCompositionChanged를 발생시켜 이 메서드가 재진입한다.
        // 그때는 입력이 비어 있으므로 여기서 빠져나가 무한 재귀가 되지 않는다.
        if (typed.Length == 0)
            return;

        // 이긴 판에서는 "다음", 진 판에서는 "다시"만 받는다.
        var isVictory = player != null && player.currentHP > 0;
        var target = isVictory ? nextWord : retryWord;

        if (typed == target)
        {
            inputManager.ClearInput();

            if (logDebugEvents)
                Debug.Log($"ResultInput: [{target}] 입력 - {(isVictory ? "다음 스테이지" : "재시작")}", this);

            if (stageManager == null)
                return;

            if (isVictory)
                stageManager.NextStage();
            else
                stageManager.RestartStage();

            return;
        }

        // 명령 단어를 향해 진행 중이 아니면 오타다. 입력창을 비워 처음부터 다시 치게 한다.
        // 판정은 전투 쪽과 같은 기준(InputManager.IsValidProgress)을 쓴다.
        if (InputManager.IsValidProgress(committed, composing, target))
            return;

        inputManager.ClearInput();
    }
}
