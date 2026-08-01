using UnityEngine;
using TMPro;

public class FloatingDamageText : MonoBehaviour
{
    [Tooltip("위로 올라가는 속도")]
    public float moveSpeed = 100f;
    [Tooltip("투명해지는 속도")]
    public float fadeSpeed = 2f;

    private TMP_Text _text;
    private Color _color;

    void Start()
    {
        _text = GetComponent<TMP_Text>();
        if (_text != null) _color = _text.color;

        // 생성될 때 2.5배로 커졌다가 쪼그라드는 팝업 연출
        transform.localScale = Vector3.one * 1.5f;

        // 1초 뒤에 자동으로 삭제
        Destroy(gameObject, 1f);
    }

    void Update()
    {
        // 1. 위로 스르륵 이동
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // 2. 크기를 원래 크기로 쫀득하게 복구
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, Time.deltaTime * 10f);

        // 3. 서서히 페이드 아웃 (투명해짐)
        if (_text != null)
        {
            _color.a -= fadeSpeed * Time.deltaTime;
            _text.color = _color;
        }
    }
}