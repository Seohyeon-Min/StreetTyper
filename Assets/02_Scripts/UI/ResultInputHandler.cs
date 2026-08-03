using System.Collections.Generic;
using UnityEngine;

// 결과 화면에서 "다시"를 타이핑해 재도전한다.
// 타이핑 게임이므로 결과 화면만 숫자 키를 쓰지 않게 하려는 것이고, 구조는 PauseManager와 같다.
//
// ⚠️ 이긴 판에는 명령 단어가 없다. 예전엔 "다음"이 있었지만 승리는 보상 선택이 끝나면
// StageManager가 자동으로 넘기므로 쓰이지 않아 삭제했다. 그래도 WantsInput은 결과 화면
// 전체에서 true를 유지한다 - 그래야 우선순위상 이 수신자가 입력을 붙들어, 승리 화면에서
// 친 글자가 손패(CardInputHandler)로 새어 조합이 쌓이는 일이 없다.
public class ResultInputHandler : CommandWordReceiver
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private StageManager stageManager;

    [Tooltip("승리인지 패배인지 판별하는 데 쓴다.")]
    [SerializeField] private CharacterStats player;

    [Header("명령 단어")]
    [SerializeField]
    private TypedCommand retryCommand = new TypedCommand(
        "다시", "retry",
        "\n\n\"{0}\"를 입력하세요",
        "\n\nType \"{0}\"");

    [SerializeField] private bool logDebugEvents;

    // 지금 유효한 단어 하나만 담는다. 글자마다 Targets가 불리므로 매번 새로 만들지 않는다.
    private readonly string[] _target = new string[1];

    /// 결과 화면은 손패보다 먼저 가져간다 - 안 그러면 "다시"의 첫 글자가 손패에 없는 단어라
    /// 오타로 처리되어 명령 단어를 끝까지 칠 수 없다.
    public override TypingPriority Priority => TypingPriority.Result;

    public override bool WantsInput() => battleManager != null && battleManager.IsGameOver;

    /// <summary>이번 판을 이겼는지. 이긴 판에는 칠 단어가 없다.</summary>
    private bool IsVictory => player != null && player.currentHP > 0;

    protected override IReadOnlyList<string> Targets
    {
        get
        {
            // 이긴 판에서는 빈 문자열을 둔다. 베이스가 빈 항목을 매칭과 진행 판정 양쪽에서
            // 건너뛰므로, 입력은 계속 이쪽이 붙들면서 아무 단어도 완성되지 않는다.
            _target[0] = IsVictory
                ? string.Empty
                : retryCommand.Word(this, nameof(retryCommand));

            return _target;
        }
    }

    public override string BuildHint()
    {
        return IsVictory
            ? string.Empty
            : JoinHints(retryCommand.Hint(this, nameof(retryCommand)));
    }

    protected override void OnCommandMatched(int index, bool wasComposing)
    {
        // Targets가 이긴 판에서 비어 있으므로 여기까지 오면 진 판이다.
        if (logDebugEvents)
            Debug.Log("ResultInput: 명령 단어 입력 - 재시작", this);

        if (stageManager == null)
        {
            Debug.LogWarning("ResultInputHandler: stageManager가 연결되지 않아 재시작할 수 없습니다.", this);
            return;
        }

        stageManager.RestartStage();
    }

    private void Awake()
    {
        if (battleManager == null)
            Debug.LogWarning("ResultInputHandler: battleManager가 연결되지 않아 결과 화면인지 알 수 없습니다.", this);

        if (stageManager == null)
            Debug.LogWarning("ResultInputHandler: stageManager가 연결되지 않아 스테이지를 넘길 수 없습니다.", this);

        if (player == null)
            Debug.LogWarning("ResultInputHandler: player가 연결되지 않아 승패를 판별할 수 없습니다.", this);
    }
}
