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
        }
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

    public void AddValidWord(string word)
    {
        validWordsUsed++;
        // 공백을 제외한 순수 알파벳/글자 수만 누적합니다.
        totalTypedCharacters += word.Replace(" ", "").Length;
    }

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