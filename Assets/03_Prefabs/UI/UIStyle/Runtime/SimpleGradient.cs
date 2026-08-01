using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UIStyle
{
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("UI/Simple Gradient")]
    public class SimpleGradient : MonoBehaviour
    {
        [Header("Base Color")]
        [Tooltip("기본 색상")]
        public Color baseColor = Color.white;
        
        [Header("Gradient Colors")]
        [Tooltip("그래디언트 색상 활성화")]
        public bool enableGradientColor = false;
        [Tooltip("그래디언트 색상 1 (밝은 쪽)")]
        public Color gradientColor1 = Color.white;
        [Tooltip("그래디언트 색상 2 (어두운 쪽)")]
        public Color gradientColor2 = new Color(0.8f, 0.8f, 0.8f, 1f);
        
        [Header("Light Gradient")]
        [Tooltip("명암 그래디언트 활성화")]
        public bool enableLightGradient = true;
        [Range(0, 0.1f)]
        [Tooltip("그래디언트 강도")]
        public float lightGradientStrength = 0.05f;
        [Range(0, 360)]
        [Tooltip("광원 방향 (각도)")]
        public float lightGradientAngle = 270f;
        
        [Header("Hue Shift")]
        [Tooltip("색온도 변화 활성화")]
        public bool enableHueShift = true;
        [Range(-10, 10)]
        [Tooltip("따뜻한 색상 변화")]
        public float hueShiftWarm = 2f;
        [Range(-10, 10)]
        [Tooltip("차가운 색상 변화")]
        public float hueShiftCool = -2f;
        
        [Header("Edge Highlight")]
        [Tooltip("엣지 하이라이트 활성화")]
        public bool enableEdgeHighlight = true;
        [Range(0, 0.2f)]
        [Tooltip("엣지 하이라이트 강도")]
        public float edgeHighlightStrength = 0.05f;
        [Range(0, 0.1f)]
        [Tooltip("엣지 크기")]
        public float edgeHighlightSize = 0.02f;
        
        [Header("Material Type")]
        [Tooltip("재질 타입 활성화")]
        public bool enableMaterialType = false;
        [Tooltip("재질 타입 (Plastic, Metal, Glass, Paper)")]
        public MaterialType materialType = MaterialType.Plastic;
        
        [Header("Micro Noise")]
        [Tooltip("미세 노이즈 활성화")]
        public bool enableNoise = false;
        [Range(0, 0.03f)]
        [Tooltip("노이즈 강도")]
        public float noiseStrength = 0.01f;
        
        [Header("Rounded Corners")]
        [Range(0, 50)]
        [Tooltip("모서리 둥글기 (0 = 직각, 50 = 매우 둥글게)")]
        public float cornerRadius = 0f;
        
        public enum MaterialType
        {
            Plastic = 0,
            Metal = 1,
            Glass = 2,
            Paper = 3
        }
        
        private Material _material;
        private Image _image;
        
        private void Awake()
        {
            _image = GetComponent<Image>();
            if (_image == null)
            {
                _image = gameObject.AddComponent<Image>();
            }
            ApplyGradient();
        }
        
        private void OnValidate()
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }
            if (_image != null)
            {
                ApplyGradient();
            }
        }
        
        public void ApplyGradient()
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
                if (_image == null) return;
            }
            
            // Image Type을 Simple로 설정 (Material 사용 시 필요)
            if (_image.type != Image.Type.Simple)
            {
                _image.type = Image.Type.Simple;
            }
            
            // 셰이더 찾기
            Shader shader = Shader.Find("UI/SimpleGradient");
            if (shader == null)
            {
                Debug.LogError("SimpleGradient shader not found!");
                return;
            }
            
            // Material 인스턴스 생성
            if (_material == null)
            {
                _material = new Material(shader);
                _material.hideFlags = HideFlags.HideAndDontSave;
            }
            
            // 셰이더 파라미터 설정
            _material.SetColor("_Color", baseColor);
            _material.SetFloat("_EnableGradientColor", enableGradientColor ? 1f : 0f);
            _material.SetColor("_GradientColor1", gradientColor1);
            _material.SetColor("_GradientColor2", gradientColor2);
            _material.SetFloat("_EnableLightGradient", enableLightGradient ? 1f : 0f);
            _material.SetFloat("_LightGradientStrength", lightGradientStrength);
            _material.SetFloat("_LightGradientAngle", lightGradientAngle);
            _material.SetFloat("_EnableHueShift", enableHueShift ? 1f : 0f);
            _material.SetFloat("_HueShiftWarm", hueShiftWarm);
            _material.SetFloat("_HueShiftCool", hueShiftCool);
            _material.SetFloat("_EnableEdgeHighlight", enableEdgeHighlight ? 1f : 0f);
            _material.SetFloat("_EdgeHighlightStrength", edgeHighlightStrength);
            _material.SetFloat("_EdgeHighlightSize", edgeHighlightSize);
            _material.SetFloat("_EnableMaterialType", enableMaterialType ? 1f : 0f);
            _material.SetFloat("_MaterialType", (float)materialType);
            _material.SetFloat("_EnableNoise", enableNoise ? 1f : 0f);
            _material.SetFloat("_NoiseStrength", noiseStrength);
            
            // Rounded Corners
            _material.SetFloat("_CornerRadius", cornerRadius / 100f);
            
            // Image에 Material 할당 (파라미터 설정 후)
            _image.material = _material;
            
            // Material 업데이트 강제
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                EditorUtility.SetDirty(_material);
                EditorUtility.SetDirty(_image);
            }
#endif
        }
        
        private void OnDestroy()
        {
            if (_material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_material);
                }
                else
                {
                    DestroyImmediate(_material);
                }
            }
        }
    }
}
