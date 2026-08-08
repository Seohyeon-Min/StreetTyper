using UnityEngine;

public class StatisticsManager : MonoBehaviour
{
    public static StatisticsManager Instance;

    [Header("Statistics")]
    public float totalPlayTime;
    public int totalTypedCharacters;
    public int validWordsUsed;
    public int totalDamageDealt;
    public int totalDamageTaken;
    public int highestStageReached;

    /// <summary>이 런을 시작할 때 고른 난이도. 쉬움 10/10과 어려움 10/10을 결과 화면에서
    /// 구분하기 위한 값이다.
    ///
    /// 다른 수치처럼 <c>Add*</c>로 쌓이지 않고 <b>시작 시점에 한 번 정해진다</b> - 난이도는
    /// 타이틀에서만 바뀌고(DifficultySettings 참조) 전투 씬은 런당 한 번 로드되므로,
    /// Awake에서 읽어두면 새 호출부를 만들 필요가 없다.</summary>
    public GameDifficulty runDifficulty { get; private set; }

    private bool isTrackingTime = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        runDifficulty = DifficultySettings.Current;
    }

    private void Update()
    {
        if (isTrackingTime)
        {
            totalPlayTime += Time.deltaTime;
        }
    }

    public void StartTracking() => isTrackingTime = true;
    public void StopTracking() => isTrackingTime = false;

    /// <summary>단어 하나를 성공적으로 완성했다. 글자 수는 여기서 세지 않는다 -
    /// <see cref="AddTypedCharacter"/>가 입력 시점에 이미 세고 있어서, 여기서 또 더하면
    /// 성공한 단어만 두 번 계산된다.</summary>
    public void AddValidWord(string word)
    {
        validWordsUsed++;
    }

    /// <summary>글자 하나가 실제로 입력창에 들어갔다(<see cref="InputManager.HandleTextInput"/>).
    ///
    /// 오타든 나중에 지운 글자든 전부 센다 - "1분 동안 타이핑한 글자 수"는 누적 타이핑 량이라
    /// 백스페이스로 되돌리지 않는다. 예전에는 성공한 단어의 글자 수만 세서 실제로 친 양보다
    /// 적게 나왔다.
    ///
    /// ⚠️ 한글은 <b>커밋된 음절</b> 하나당 1이다(조합 중인 자모는 세지 않는다 - 조합은 음절마다
    /// 여러 번 갱신되어 3배쯤 부풀려진다). 그래서 한글은 음절 수, 영어는 글자 수가 되어
    /// 두 언어의 수치를 직접 비교할 수는 없다.</summary>
    public void AddTypedCharacter() => totalTypedCharacters++;

    public void AddDamageDealt(int damage) => totalDamageDealt += damage;
    public void AddDamageTaken(int damage) => totalDamageTaken += damage;

    public void UpdateHighestStage(int stage)
    {
        if (stage > highestStageReached) highestStageReached = stage;
    }

    // 평균 타자 속도 (CPM: Characters Per Minute) 반환
    public float GetCPM()
    {
        if (totalPlayTime <= 0) return 0;
        float minutes = totalPlayTime / 60f;
        return totalTypedCharacters / minutes;
    }
}