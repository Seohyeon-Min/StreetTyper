using TMPro;
using UnityEngine;

// 언어에 따라 글자를 바꾸는 TMP 라벨. 타이틀 버튼처럼 씬에 글자가 박혀 있는 UI에 붙인다.
//
// LanguageSettings.OnChanged를 구독하는 게 핵심이다 - 옵션 창에서 언어를 누른 그 순간
// 화면이 바뀌어야 하는데, 씬을 다시 불러오지 않으면 스스로 갱신할 방법이 없다.
[RequireComponent(typeof(TMP_Text))]
public class LocalizedLabel : MonoBehaviour
{
    [Tooltip("한국어일 때 표시할 글자")]
    [SerializeField, TextArea] private string korean;

    [Tooltip("영어일 때 표시할 글자")]
    [SerializeField, TextArea] private string english;

    private TMP_Text _text;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();

        // 인스펙터를 비워둔 채 붙이면 씬에 있던 글자가 지워져 원인을 찾기 어렵다.
        if (string.IsNullOrEmpty(korean) && string.IsNullOrEmpty(english))
            Debug.LogWarning("LocalizedLabel: korean/english가 둘 다 비어 있어 라벨이 지워집니다.", this);
    }

    private void OnEnable()
    {
        LanguageSettings.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LanguageSettings.OnChanged -= Refresh;
    }

    private void Refresh()
    {
        if (_text != null)
            _text.text = LanguageSettings.Pick(korean, english, this, "english");
    }
}
