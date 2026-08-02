using System.Collections.Generic;
using UnityEngine;

// 결과 화면에서 명령 단어를 타이핑해 다음 스테이지로 넘어가거나 재도전한다.
// 타이핑 게임이므로 결과 화면만 숫자 키를 쓰지 않게 하려는 것이고, 구조는 PauseManager와 같다.
//
// 이긴 판에서는 "다음"만, 진 판에서는 "다시"만 유효하다 - 둘 다 받으면 진 판에서도 다음
// 스테이지로 넘어갈 수 있게 된다.
public class ResultInputHandler : CommandWordReceiver
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private StageManager stageManager;

    [Tooltip("승리인지 패배인지 판별하는 데 쓴다.")]
    [SerializeField] private CharacterStats player;

    [Header("명령 단어")]
    [SerializeField]
    private TypedCommand nextCommand = new TypedCommand(
        "다음", "next",
        "\n\n\"{0}\"을 입력하세요",
        "\n\nType \"{0}\"");

    [SerializeField]
    private TypedCommand retryCommand = new TypedCommand(
        "다시", "retry",
        "\n\n\"{0}\"를 입력하세요",
        "\n\nType \"{0}\"");

    [SerializeField] private bool logDebugEvents;

    // 지금 유효한 단어 하나만 담는다. 글자마다 Targets가 불리므로 매번 새로 만들지 않는다.
    private readonly string[] _target = new string[1];

    /// 결과 화면은 손패보다 먼저 가져간다 - 안 그러면 "다음"의 첫 글자가 손패에 없는 단어라
    /// 오타로 처리되어 명령 단어를 끝까지 칠 수 없다.
    public override TypingPriority Priority => TypingPriority.Result;

    public override bool WantsInput() => battleManager != null && battleManager.IsGameOver;

    /// <summary>이번 판을 이겼는지. 승패에 따라 받는 단어와 안내가 갈린다.</summary>
    private bool IsVictory => player != null && player.currentHP > 0;

    private TypedCommand ActiveCommand => IsVictory ? nextCommand : retryCommand;
    private string ActiveFieldName => IsVictory ? nameof(nextCommand) : nameof(retryCommand);

    protected override IReadOnlyList<string> Targets
    {
        get
        {
            _target[0] = ActiveCommand.Word(this, ActiveFieldName);
            return _target;
        }
    }

    public override string BuildHint()
    {
        return JoinHints(ActiveCommand.Hint(this, ActiveFieldName));
    }

    protected override void OnCommandMatched(int index, bool wasComposing)
    {
        var isVictory = IsVictory;

        if (logDebugEvents)
            Debug.Log($"ResultInput: 명령 단어 입력 - {(isVictory ? "다음 스테이지" : "재시작")}", this);

        if (stageManager == null)
        {
            Debug.LogWarning("ResultInputHandler: stageManager가 연결되지 않아 스테이지를 넘길 수 없습니다.", this);
            return;
        }

        if (isVictory)
            stageManager.NextStage();
        else
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
