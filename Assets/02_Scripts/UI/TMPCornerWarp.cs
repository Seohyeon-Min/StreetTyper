using TMPro;
using UnityEngine;

// 텍스트 각 글자의 위쪽 두 꼭짓점(좌상/우상)만 오른쪽으로 밀어서 기울인다(이탤릭 느낌).
// 애니메이션 없이 인스펙터 값 그대로 고정된다. TMP는 여러 글자가 하나의 메시로 합쳐져 있어서
// 셰이더로는 "이 정점이 어느 글자의 어느 모서리냐"를 구분하기 어려우므로, CPU 쪽에서
// TMP_Text.textInfo.meshInfo[].vertices를 직접 건드린다.
// 에디터에서 Play를 안 눌러도 씬 뷰에서 바로 결과를 보려고 ExecuteAlways를 붙인다 -
// 없으면 Awake/LateUpdate가 Play 모드에서만 돌아서 에디터에서는 값이 전혀 적용되지 않는다.
[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class TMPCornerWarp : MonoBehaviour
{
    [Tooltip("위쪽 꼭짓점(좌상/우상)을 오른쪽으로 미는 양(포인트 단위). 음수면 왼쪽으로 민다.")]
    [SerializeField] private float topSkewX = 4f;

    private TMP_Text _text;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        ApplyWarp();
    }

    // 텍스트 내용이 바뀌면 TMP가 메시를 다시 만들어서 왜곡이 지워지므로, 텍스트가 바뀔 때마다
    // 다시 걸어줘야 한다. 애니메이션이 아니라 텍스트 갱신에 반응하는 용도라 매 프레임 다시
    // 계산해도 가볍다(고정값 더하기뿐).
    private void LateUpdate()
    {
        ApplyWarp();
    }

    private void ApplyWarp()
    {
        // ExecuteAlways라 에디터에서 컴포넌트가 막 추가됐을 때 Awake가 아직 안 돌았을 수 있다.
        if (_text == null)
            _text = GetComponent<TMP_Text>();

        if (_text == null)
            return;

        _text.ForceMeshUpdate();

        var textInfo = _text.textInfo;
        var skew = new Vector3(topSkewX, 0f, 0f);

        for (var i = 0; i < textInfo.characterCount; i++)
        {
            var charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible)
                continue;

            var vertices = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;
            var vertexIndex = charInfo.vertexIndex;

            // 정점 순서(TMP 고정 순서): +0 좌하단, +1 좌상단, +2 우상단, +3 우하단.
            // 위쪽 두 개(+1, +2)만 민다 - 아래쪽은 그대로 둬서 밑변이 고정된 기울임이 된다.
            vertices[vertexIndex + 1] += skew;
            vertices[vertexIndex + 2] += skew;
        }

        for (var m = 0; m < textInfo.meshInfo.Length; m++)
        {
            var meshInfo = textInfo.meshInfo[m];
            meshInfo.mesh.vertices = meshInfo.vertices;
            _text.UpdateGeometry(meshInfo.mesh, m);
        }
    }
}
