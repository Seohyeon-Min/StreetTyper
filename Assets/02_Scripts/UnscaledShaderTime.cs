using UnityEngine;

// WaveNoise.shader처럼 "일시정지 중에도 계속 움직여야 하는" 셰이더용 전역 시간을 매 프레임
// 갱신한다. Shader.SetGlobalFloat이라 값 하나가 그 셰이더를 쓰는 모든 머티리얼에 동시에
// 적용되므로, 씬에 이 컴포넌트를 하나만 두면 된다(LanguageSettings를 static으로 둔 것과
// 같은 이유 - 머티리얼마다 따로 값을 밀어 넣는 스크립트를 붙일 필요가 없다).
public class UnscaledShaderTime : MonoBehaviour
{
    private static readonly int GlobalUnscaledTimeId = Shader.PropertyToID("_GlobalUnscaledTime");

    private void Update()
    {
        Shader.SetGlobalFloat(GlobalUnscaledTimeId, Time.unscaledTime);
    }
}
