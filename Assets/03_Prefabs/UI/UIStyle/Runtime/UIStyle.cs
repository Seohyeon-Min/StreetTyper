using UnityEngine;
using UnityEngine.UI;

namespace UIStyle
{
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("UI/UIStyle")]
    public sealed class UIStyle : MonoBehaviour
    {
        [Header("Shader")]
        [SerializeField, Tooltip("빌드에서 셰이더가 제거되지 않도록 직접 참조합니다.")]
        private Shader uiStyleShader;

        [Header("Gauge Fill")]
        [Range(0f, 1f)]
        [Tooltip("게이지처럼 왼쪽을 고정하고 오른쪽 경계만 줄였다 늘렸다 한다(모서리 둥글기 유지). 1 = 꽉 참")]
        public float fillAmount = 1f;

        [Header("Rounded Corners")]
        [Range(0, 50)]
        [Tooltip("모서리 둥글기 (0 = 직각, 50 = 매우 둥글게)")]
        public float cornerRadius = 10f;

        [Tooltip("모서리별로 다른 둥글기 값을 사용 (켜면 위 Corner Radius 대신 아래 4개 값을 각각 적용)")]
        public bool usePerCornerRadius = false;

        [Range(0, 50)]
        [Tooltip("좌상단 모서리 둥글기")]
        public float cornerRadiusTopLeft = 10f;

        [Range(0, 50)]
        [Tooltip("우상단 모서리 둥글기")]
        public float cornerRadiusTopRight = 10f;

        [Range(0, 50)]
        [Tooltip("좌하단 모서리 둥글기")]
        public float cornerRadiusBottomLeft = 10f;

        [Range(0, 50)]
        [Tooltip("우하단 모서리 둥글기")]
        public float cornerRadiusBottomRight = 10f;

        [Tooltip("Capsule/Pill Shape 사용 (가로/세로 비율에 따라 자동 적용)")]
        public bool useCapsuleShape = false;
        
        [Tooltip("가로 알약 모양 (좌우 끝이 둥글게)")]
        public bool pillShapeHorizontal = false;
        
        [Tooltip("세로 알약 모양 (위아래 끝이 둥글게)")]
        public bool pillShapeVertical = false;

        [Tooltip("마름모(다이아몬드) 모양 사용")]
        public bool useDiamondShape = false;

        [Range(0.2f, 4f)]
        [Tooltip("마름모 변의 곡률. 1 = 직선 변, 1보다 크면 변이 바깥으로 둥글게 휘고, 1보다 작으면 안쪽으로 휘어 오목해짐")]
        public float diamondCurvature = 1f;

        [Range(-1f, 1f)]
        [Tooltip("전체 기울기. 아래쪽은 고정된 채 위쪽만 좌우로 밀려서 사다리꼴처럼 기울어짐. 0 = 안 기울어짐")]
        public float skew = 0f;

        [Header("Edge Blur (Airbrush)")]
        [Tooltip("도형 가장자리를 에어브러쉬처럼 부드럽게 흐림")]
        public bool enableEdgeBlur = false;

        [Range(0f, 0.5f)]
        [Tooltip("경계 흐림 정도. 클수록 가장자리가 더 넓게 퍼짐")]
        public float edgeBlurAmount = 0.05f;

        [Header("Drop Shadow")]
        [Tooltip("드롭 섀도우 활성화")]
        public bool enableDropShadow = false;
        
        [Tooltip("섀도우 오프셋 (X, Y 픽셀 단위)")]
        public Vector2 dropShadowOffset = new Vector2(2, -2);
        
        [Tooltip("섀도우 색상")]
        public Color dropShadowColor = new Color(0, 0, 0, 0.5f);
        
        [Range(0, 10)]
        [Tooltip("섀도우 경계 흐림 정도")]
        public float dropShadowBlur = 2f;
        
        [Range(-50, 50)]
        [Tooltip("셰도우 크기 (-50 = 축소, 50 = 확대)")]
        public float dropShadowSize = 0f;
        
        [Header("Inner Shadow")]
        [Tooltip("인사이드 섀도우 활성화")]
        public bool enableInnerShadow = false;
        
        [Tooltip("인사이드 섀도우 오프셋 (X, Y 픽셀 단위)")]
        public Vector2 innerShadowOffset = new Vector2(0, 0);
        
        [Tooltip("인사이드 섀도우 색상")]
        public Color innerShadowColor = new Color(0, 0, 0, 0.3f);
        
        [Range(0, 10)]
        [Tooltip("인사이드 섀도우 경계 흐림 정도")]
        public float innerShadowBlur = 2f;
        
        [Header("Gradient")]
        [Tooltip("세부 그래디언트 활성화")]
        public bool enableGradient = false;
        
        [Tooltip("기본 색상")]
        public Color gradientBaseColor = Color.white;
        
        [Space(5)]
        [Header("Color Gradient")]
        [Tooltip("색상 그래디언트 활성화")]
        public bool enableColorGradient = false;
        
        [Tooltip("그래디언트 시작 색상")]
        public Color gradientColorStart = Color.white;
        
        [Tooltip("그래디언트 끝 색상")]
        public Color gradientColorEnd = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Tooltip("중앙 기준 방사형 그래디언트 사용 (켜면 Direction 대신 중심->가장자리 방향으로 그래디언트 적용)")]
        public bool gradientRadial = false;

        [Range(0, 360)]
        [Tooltip("그래디언트 방향 (각도)")]
        public float gradientDirection = 270f;
        
        [Range(0, 100)]
        [Tooltip("그래디언트 블렌드 강도 (0 = 없음, 100 = 최대)")]
        public float gradientBlend = 50f;
        
        [Header("Light Gradient")]
        [Range(0, 10)]
        [Tooltip("명암 그래디언트 강도 (위→밝음, 아래→어두움)")]
        public float lightGradientStrength = 5f;
        
        [Range(0, 360)]
        [Tooltip("광원 방향 (도 단위, 270 = 위에서 아래)")]
        public float lightDirection = 270f;
        
        [Header("Hue Shift")]
        [Range(-10, 10)]
        [Tooltip("하이라이트 색온도 (양수 = 따뜻하게)")]
        public float hueShiftWarm = 2f;
        
        [Range(-10, 10)]
        [Tooltip("섀도우 색온도 (음수 = 차갑게)")]
        public float hueShiftCool = -2f;
        
        [Header("Edge Highlight")]
        [Range(0, 20)]
        [Tooltip("엣지 하이라이트 강도")]
        public float edgeHighlightStrength = 5f;
        
        [Range(0, 10)]
        [Tooltip("엣지 크기")]
        public float edgeHighlightSize = 2f;
        
        [Header("Material")]
        [Tooltip("재질 타입")]
        public MaterialType materialType = MaterialType.Plastic;
        
        [Header("Noise")]
        [Tooltip("미세 노이즈 활성화")]
        public bool enableNoise = false;
        
        [Range(0, 3)]
        [Tooltip("노이즈 강도 (1~3% 권장)")]
        public float noiseStrength = 1f;
        
        [Header("Bottom Edge Line")]
        [Tooltip("아래 엣지 라인 활성화")]
        public bool enableBottomEdgeLine = false;
        
        [Range(0, 20)]
        [Tooltip("엣지 라인 두께 (0 = 없음, 20 = 매우 두껍게)")]
        public float edgeLineThickness = 2f;
        
        [Range(0, 10)]
        [Tooltip("엣지 라인 강도 (0 = 없음, 10 = 매우 강함)")]
        public float edgeLineIntensity = 2f;
        
        [Tooltip("엣지 라인 색상")]
        public Color edgeLineColor = new Color(0, 0, 0, 1f);
        
        [Range(0, 10)]
        [Tooltip("경계 뚜렷함 (0 = 매우 부드럽게, 10 = 매우 뚜렷하게)")]
        public float edgeLineSharpness = 5f;
        
        public enum MaterialType
        {
            Plastic = 0,
            Metal = 1,
            Glass = 2,
            Paper = 3
        }
        
        [Header("Preset")]
        [Tooltip("프리셋 적용 (에디터에서만 작동)")]
        public UIStylePreset preset;
        
        private Image _image;
        private Material _material;
        private static Material _defaultMaterial;
        
        private void Awake()
        {
            _image = GetComponent<Image>();
            if (!_image)
                _image = gameObject.AddComponent<Image>();
            
            ApplyStyle();
        }
        
        private void OnValidate()
        {
            if (!this || !gameObject) return; // 객체가 파괴되었는지 확인
            
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // 에디터에서 즉시 적용하고 씬 뷰 리프레시
                // delayCall 대신 즉시 실행하여 씬 뷰와 동기화
                if (_image == null)
                {
                    _image = GetComponent<Image>();
                }
                
                if (_image != null)
                {
                    ApplyStyle();
                    // 씬 뷰 업데이트를 위해 SetDirty 호출
                    UnityEditor.EditorUtility.SetDirty(this);
                    UnityEditor.EditorUtility.SetDirty(_image);
                    
                    // 씬 뷰 강제 리프레시
                    if (UnityEditor.SceneView.lastActiveSceneView != null)
                    {
                        UnityEditor.SceneView.lastActiveSceneView.Repaint();
                    }
                    UnityEditor.SceneView.RepaintAll();
                }
            }
            else
            #endif
            {
                if (_image)
                    ApplyStyle();
            }
        }
        
        // 게이지처럼 런타임에 RectTransform 크기(anchorMax 등)가 계속 바뀌는 경우, _AspectRatio를 포함한
        // 셰이더 파라미터가 낡은 채로 남아있으면 도형 비율이 깨지거나(예: 아이스 게이지) 라운드 코너가
        // 좁아진 사각형 크기를 삼켜버려 아예 안 채워진 것처럼 보인다(예: 온도 게이지). 크기가 바뀔 때마다 재적용한다.
        private void OnRectTransformDimensionsChange()
        {
            if (!this || !gameObject || !isActiveAndEnabled) return;
            ApplyStyle();
        }

        private void Reset()
        {
            // 컴포넌트가 처음 추가될 때 초기화
            _image = GetComponent<Image>();
            if (!_image)
                _image = gameObject.AddComponent<Image>();
            
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ApplyStyle();
                UnityEditor.EditorUtility.SetDirty(this);
            }
            #endif
        }
        
        public void ApplyStyle()
        {
            // 객체가 파괴되었는지 확인
            if (!this || !gameObject) return;
            
            if (!_image)
            {
                _image = GetComponent<Image>();
                if (!_image)
                {
                    if (!gameObject) return; // gameObject도 확인
                    _image = gameObject.AddComponent<Image>();
                }
            }
            
            if (!_image) return;
            
            // Material 생성
            if (!_material)
            {
                Shader shader = uiStyleShader != null
                    ? uiStyleShader
                    : Shader.Find("UI/UIStyle");
                if (!shader)
                {
                    Debug.LogError("UIStyle shader not found! Make sure the shader is in the project.");
                    return;
                }
                
                _material = new Material(shader);
                _material.hideFlags = HideFlags.HideAndDontSave;
            }
            
            // Material을 Image에 적용
            if (_image && _material)
            {
                _image.material = _material;
                
                #if UNITY_EDITOR
                // 에디터에서 Material 변경을 즉시 반영
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(_image);
                    // Material도 SetDirty로 표시
                    UnityEditor.EditorUtility.SetDirty(_material);
                }
                #endif
            }
            
            // 셰이더 파라미터 설정 (Material이 존재하는지 확인)
            if (!_material) return;
            
            // 실제 UI 요소의 가로세로 비율 계산
            RectTransform rectTransform = _image.rectTransform;
            float aspectRatio = rectTransform.rect.width / rectTransform.rect.height;
            if (float.IsNaN(aspectRatio) || float.IsInfinity(aspectRatio) || aspectRatio <= 0f)
            {
                aspectRatio = 1.0f; // 기본값: 정사각형
            }
            _material.SetFloat("_AspectRatio", aspectRatio);
            
            _material.SetFloat("_FillAmount", fillAmount);
            _material.SetFloat("_CornerRadius", cornerRadius / 100f);
            _material.SetFloat("_UsePerCornerRadius", usePerCornerRadius ? 1f : 0f);
            _material.SetFloat("_CornerRadiusTopLeft", cornerRadiusTopLeft / 100f);
            _material.SetFloat("_CornerRadiusTopRight", cornerRadiusTopRight / 100f);
            _material.SetFloat("_CornerRadiusBottomLeft", cornerRadiusBottomLeft / 100f);
            _material.SetFloat("_CornerRadiusBottomRight", cornerRadiusBottomRight / 100f);
            _material.SetFloat("_UseCapsuleShape", useCapsuleShape ? 1f : 0f);
            _material.SetFloat("_PillShapeHorizontal", pillShapeHorizontal ? 1f : 0f);
            _material.SetFloat("_PillShapeVertical", pillShapeVertical ? 1f : 0f);
            _material.SetFloat("_UseDiamondShape", useDiamondShape ? 1f : 0f);
            _material.SetFloat("_DiamondCurvature", diamondCurvature);
            _material.SetFloat("_Skew", skew);
            _material.SetFloat("_EnableEdgeBlur", enableEdgeBlur ? 1f : 0f);
            _material.SetFloat("_EdgeBlurAmount", edgeBlurAmount);
            
            // Drop shadow
            _material.SetFloat("_EnableDropShadow", enableDropShadow ? 1f : 0f);
            _material.SetVector("_DropShadowOffset", new Vector4(dropShadowOffset.x, dropShadowOffset.y, 0, 0));
            _material.SetColor("_DropShadowColor", dropShadowColor);
            _material.SetFloat("_DropShadowBlur", dropShadowBlur / 100f);
            _material.SetFloat("_DropShadowSize", dropShadowSize / 100f);
            
            // Inner shadow
            _material.SetFloat("_EnableInnerShadow", enableInnerShadow ? 1f : 0f);
            _material.SetVector("_InnerShadowOffset", new Vector4(innerShadowOffset.x, innerShadowOffset.y, 0, 0));
            _material.SetColor("_InnerShadowColor", innerShadowColor);
            _material.SetFloat("_InnerShadowBlur", innerShadowBlur / 100f);
            
            // Gradient
            _material.SetFloat("_EnableGradient", enableGradient ? 1f : 0f);
            _material.SetColor("_GradientBaseColor", gradientBaseColor);
            _material.SetFloat("_EnableColorGradient", enableColorGradient ? 1f : 0f);
            _material.SetColor("_GradientColorStart", gradientColorStart);
            _material.SetColor("_GradientColorEnd", gradientColorEnd);
            _material.SetFloat("_GradientRadial", gradientRadial ? 1f : 0f);
            _material.SetFloat("_GradientDirection", gradientDirection);
            _material.SetFloat("_GradientBlend", gradientBlend / 100f);
            _material.SetFloat("_LightGradientStrength", lightGradientStrength / 100f);
            _material.SetFloat("_LightDirection", lightDirection);
            _material.SetFloat("_HueShiftWarm", hueShiftWarm);
            _material.SetFloat("_HueShiftCool", hueShiftCool);
            _material.SetFloat("_EdgeHighlightStrength", edgeHighlightStrength / 100f);
            _material.SetFloat("_EdgeHighlightSize", edgeHighlightSize / 100f);
            _material.SetFloat("_MaterialType", (float)materialType);
            _material.SetFloat("_EnableNoise", enableNoise ? 1f : 0f);
            _material.SetFloat("_NoiseStrength", noiseStrength / 100f);
            
            // Bottom Edge Line
            _material.SetFloat("_EnableBottomEdgeLine", enableBottomEdgeLine ? 1f : 0f);
            _material.SetFloat("_EdgeLineThickness", edgeLineThickness / 100f);
            _material.SetFloat("_EdgeLineIntensity", edgeLineIntensity / 10f);
            _material.SetColor("_EdgeLineColor", edgeLineColor);
            _material.SetFloat("_EdgeLineSharpness", edgeLineSharpness / 10f);
        }
        
        /// <summary>게이지처럼 매 프레임 값이 바뀌는 경우를 위한 가벼운 갱신. ApplyStyle() 전체를 다시 돌리지 않고
        /// _FillAmount 하나만 갱신한다. RectTransform 크기는 그대로 두고(고정) 셰이더 안에서만 오른쪽 경계가 움직인다.</summary>
        public void SetFillAmount(float value)
        {
            fillAmount = Mathf.Clamp01(value);

            // 머티리얼이 아직 안 만들어졌으면(Awake 순서 등으로 아직 초기화 전) 조용히 무시하지 말고
            // 전체를 한 번 초기화해서 fillAmount를 포함한 모든 값이 확실히 반영되게 한다.
            if (_material == null)
            {
                ApplyStyle();
                return;
            }

            _material.SetFloat("_FillAmount", fillAmount);
        }

        public void ApplyPreset(UIStylePreset presetToApply)
        {
            if (!presetToApply) return;
            
            presetToApply.ApplyTo(this);
            ApplyStyle();
        }
        
        private void OnDestroy()
        {
            if (_material)
            {
                if (Application.isPlaying)
                    Destroy(_material);
                else
                    DestroyImmediate(_material);
                _material = null;
            }
            
            // Image의 Material도 제거
            if (_image)
            {
                _image.material = null;
            }
        }
    }
}
