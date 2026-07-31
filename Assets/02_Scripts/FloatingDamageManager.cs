using UnityEngine;
using TMPro;

public class FloatingDamageManager : MonoBehaviour
{
    public static FloatingDamageManager Instance;

    public GameObject damagePrefab; // 방금 만든 데미지 텍스트 프리팹
    public Transform damageCanvas;  // 데미지 텍스트가 담길 캔버스

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void ShowDamage(int damage, Vector3 worldPosition)
    {
        if (damagePrefab == null || damageCanvas == null) return;

        // 프리팹 소환
        GameObject go = Instantiate(damagePrefab, damageCanvas);

        // 데미지 숫자 입력
        TMP_Text txt = go.GetComponent<TMP_Text>();
        if (txt != null) txt.text = damage.ToString();

        // 적 위치 기반으로 화면상 좌표 계산 (적 머리 위쪽으로 +1.5f)
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition + new Vector3(0, 1.5f, 0));

        // 숫자들이 완전히 겹치지 않게 살짝씩 랜덤 위치로 흩뿌림
        screenPos.x += Random.Range(-40f, 40f);
        screenPos.y += Random.Range(-20f, 20f);

        go.GetComponent<RectTransform>().position = screenPos;
    }
}