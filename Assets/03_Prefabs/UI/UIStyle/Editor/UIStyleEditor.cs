using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UIStyle;

namespace UIStyle.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(global::UIStyle.UIStyle))]
    public class UIStyleEditor : UnityEditor.Editor
    {
        private SerializedProperty _fillAmount;
        private SerializedProperty _cornerRadius;
        private SerializedProperty _usePerCornerRadius;
        private SerializedProperty _cornerRadiusTopLeft;
        private SerializedProperty _cornerRadiusTopRight;
        private SerializedProperty _cornerRadiusBottomLeft;
        private SerializedProperty _cornerRadiusBottomRight;
        private SerializedProperty _useCapsuleShape;
        private SerializedProperty _pillShapeHorizontal;
        private SerializedProperty _pillShapeVertical;
        private SerializedProperty _useDiamondShape;
        private SerializedProperty _diamondCurvature;
        private SerializedProperty _skew;
        private SerializedProperty _enableEdgeBlur;
        private SerializedProperty _edgeBlurAmount;
        
        private SerializedProperty _enableDropShadow;
        private SerializedProperty _dropShadowOffset;
        private SerializedProperty _dropShadowColor;
        private SerializedProperty _dropShadowBlur;
        private SerializedProperty _dropShadowSize;
        
        private SerializedProperty _enableInnerShadow;
        private SerializedProperty _innerShadowOffset;
        private SerializedProperty _innerShadowColor;
        private SerializedProperty _innerShadowBlur;
        
        private SerializedProperty _enableGradient;
        private SerializedProperty _gradientBaseColor;
        private SerializedProperty _enableColorGradient;
        private SerializedProperty _gradientColorStart;
        private SerializedProperty _gradientColorEnd;
        private SerializedProperty _gradientRadial;
        private SerializedProperty _gradientDirection;
        private SerializedProperty _gradientBlend;
        private SerializedProperty _lightGradientStrength;
        private SerializedProperty _lightDirection;
        private SerializedProperty _hueShiftWarm;
        private SerializedProperty _hueShiftCool;
        private SerializedProperty _edgeHighlightStrength;
        private SerializedProperty _edgeHighlightSize;
        private SerializedProperty _materialType;
        private SerializedProperty _enableNoise;
        private SerializedProperty _noiseStrength;
        
        private SerializedProperty _enableBottomEdgeLine;
        private SerializedProperty _edgeLineThickness;
        private SerializedProperty _edgeLineIntensity;
        private SerializedProperty _edgeLineColor;
        private SerializedProperty _edgeLineSharpness;
        
        private SerializedProperty _preset;
        
        // Image properties
        private SerializedObject _imageSerializedObject;
        private SerializedProperty _imageSprite;
        private SerializedProperty _imageColor;
        private SerializedProperty _imageMaterial;
        private SerializedProperty _imageRaycastTarget;
        
        private bool _showRoundedCorners = true;
        private bool _showEdgeBlur = true;
        private bool _showDropShadow = true;
        private bool _showInnerShadow = true;
        private bool _showGradient = true;
        private bool _showPreset = true;
        private bool _showImageProperties = true;
        
        private void OnEnable()
        {
            _fillAmount = serializedObject.FindProperty("fillAmount");
            _cornerRadius = serializedObject.FindProperty("cornerRadius");
            _usePerCornerRadius = serializedObject.FindProperty("usePerCornerRadius");
            _cornerRadiusTopLeft = serializedObject.FindProperty("cornerRadiusTopLeft");
            _cornerRadiusTopRight = serializedObject.FindProperty("cornerRadiusTopRight");
            _cornerRadiusBottomLeft = serializedObject.FindProperty("cornerRadiusBottomLeft");
            _cornerRadiusBottomRight = serializedObject.FindProperty("cornerRadiusBottomRight");
            _useCapsuleShape = serializedObject.FindProperty("useCapsuleShape");
            _pillShapeHorizontal = serializedObject.FindProperty("pillShapeHorizontal");
            _pillShapeVertical = serializedObject.FindProperty("pillShapeVertical");
            _useDiamondShape = serializedObject.FindProperty("useDiamondShape");
            _diamondCurvature = serializedObject.FindProperty("diamondCurvature");
            _skew = serializedObject.FindProperty("skew");
            _enableEdgeBlur = serializedObject.FindProperty("enableEdgeBlur");
            _edgeBlurAmount = serializedObject.FindProperty("edgeBlurAmount");
            
            _enableDropShadow = serializedObject.FindProperty("enableDropShadow");
            _dropShadowOffset = serializedObject.FindProperty("dropShadowOffset");
            _dropShadowColor = serializedObject.FindProperty("dropShadowColor");
            _dropShadowBlur = serializedObject.FindProperty("dropShadowBlur");
            _dropShadowSize = serializedObject.FindProperty("dropShadowSize");
            
            _enableInnerShadow = serializedObject.FindProperty("enableInnerShadow");
            _innerShadowOffset = serializedObject.FindProperty("innerShadowOffset");
            _innerShadowColor = serializedObject.FindProperty("innerShadowColor");
            _innerShadowBlur = serializedObject.FindProperty("innerShadowBlur");
            
            _enableGradient = serializedObject.FindProperty("enableGradient");
            _gradientBaseColor = serializedObject.FindProperty("gradientBaseColor");
            _enableColorGradient = serializedObject.FindProperty("enableColorGradient");
            _gradientColorStart = serializedObject.FindProperty("gradientColorStart");
            _gradientColorEnd = serializedObject.FindProperty("gradientColorEnd");
            _gradientRadial = serializedObject.FindProperty("gradientRadial");
            _gradientDirection = serializedObject.FindProperty("gradientDirection");
            _gradientBlend = serializedObject.FindProperty("gradientBlend");
            _lightGradientStrength = serializedObject.FindProperty("lightGradientStrength");
            _lightDirection = serializedObject.FindProperty("lightDirection");
            _hueShiftWarm = serializedObject.FindProperty("hueShiftWarm");
            _hueShiftCool = serializedObject.FindProperty("hueShiftCool");
            _edgeHighlightStrength = serializedObject.FindProperty("edgeHighlightStrength");
            _edgeHighlightSize = serializedObject.FindProperty("edgeHighlightSize");
            _materialType = serializedObject.FindProperty("materialType");
            _enableNoise = serializedObject.FindProperty("enableNoise");
            _noiseStrength = serializedObject.FindProperty("noiseStrength");
            
            _enableBottomEdgeLine = serializedObject.FindProperty("enableBottomEdgeLine");
            _edgeLineThickness = serializedObject.FindProperty("edgeLineThickness");
            _edgeLineIntensity = serializedObject.FindProperty("edgeLineIntensity");
            _edgeLineColor = serializedObject.FindProperty("edgeLineColor");
            _edgeLineSharpness = serializedObject.FindProperty("edgeLineSharpness");
            
            _preset = serializedObject.FindProperty("preset");
            
            // Image 컴포넌트 속성 가져오기
            global::UIStyle.UIStyle uiStyle = (global::UIStyle.UIStyle)target;
            Image image = uiStyle.GetComponent<Image>();
            if (image != null)
            {
                _imageSerializedObject = new SerializedObject(image);
                _imageSprite = _imageSerializedObject.FindProperty("m_Sprite");
                _imageColor = _imageSerializedObject.FindProperty("m_Color");
                _imageMaterial = _imageSerializedObject.FindProperty("m_Material");
                _imageRaycastTarget = _imageSerializedObject.FindProperty("m_RaycastTarget");
            }
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            global::UIStyle.UIStyle uiStyle = (global::UIStyle.UIStyle)target;

            if (!uiStyle)
            {
                EditorGUILayout.HelpBox("UIStyle component is missing!", MessageType.Error);
                return;
            }
            
            // Header
            EditorGUILayout.Space(10);
            DrawHeader("UI Style", Color.cyan);
            EditorGUILayout.Space(5);
            
            // Base Image properties
            Image image = uiStyle.GetComponent<Image>();
            if (image == null)
            {
                EditorGUILayout.HelpBox("Image component is required! It will be added automatically.", MessageType.Warning);
                if (GUILayout.Button("Add Image Component"))
                {
                    image = uiStyle.gameObject.AddComponent<Image>();
                    OnEnable(); // 재초기화
                }
                EditorGUILayout.Space(10);
            }
            else
            {
                _showImageProperties = EditorGUILayout.Foldout(_showImageProperties, "📷 Base Image Properties", true);
                if (_showImageProperties)
                {
                    EditorGUI.indentLevel++;
                    
                    if (_imageSerializedObject != null)
                    {
                        _imageSerializedObject.Update();
                        
                        EditorGUILayout.PropertyField(_imageSprite, new GUIContent("Source Image"));
                        EditorGUILayout.PropertyField(_imageColor, new GUIContent("Color"));
                        EditorGUILayout.PropertyField(_imageMaterial, new GUIContent("Material"));
                        EditorGUILayout.PropertyField(_imageRaycastTarget, new GUIContent("Raycast Target"));
                        
                        _imageSerializedObject.ApplyModifiedProperties();
                    }
                    
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(5);
                }
            }
            
            // Gauge Fill
            EditorGUILayout.LabelField("⛽ Gauge Fill", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_fillAmount, new GUIContent("Fill Amount", "왼쪽 고정, 오른쪽 경계만 줄였다 늘렸다(모서리 유지). 1 = 꽉 참"));
            EditorGUILayout.Space(5);

            // Rounded Corners
            _showRoundedCorners = EditorGUILayout.Foldout(_showRoundedCorners, "🔲 Shape", true);
            if (_showRoundedCorners)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_usePerCornerRadius, new GUIContent("Use Per-Corner Radius", "모서리별로 다른 둥글기 값을 사용"));
                if (_usePerCornerRadius.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_cornerRadiusTopLeft, new GUIContent("Top Left", "좌상단 모서리 둥글기"));
                    EditorGUILayout.PropertyField(_cornerRadiusTopRight, new GUIContent("Top Right", "우상단 모서리 둥글기"));
                    EditorGUILayout.PropertyField(_cornerRadiusBottomLeft, new GUIContent("Bottom Left", "좌하단 모서리 둥글기"));
                    EditorGUILayout.PropertyField(_cornerRadiusBottomRight, new GUIContent("Bottom Right", "우하단 모서리 둥글기"));
                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.PropertyField(_cornerRadius, new GUIContent("Corner Radius", "모서리 둥글기 (0-50)"));
                }
                EditorGUILayout.Space(3);
                EditorGUILayout.PropertyField(_useCapsuleShape, new GUIContent("Use Capsule/Pill Shape", "가로/세로 비율에 따라 자동으로 알약 형태 적용"));
                if (_useCapsuleShape.boolValue)
                {
                    EditorGUILayout.HelpBox("Capsule Shape: 가로형은 좌우 끝이 둥글고, 세로형은 위아래 끝이 둥글게 됩니다.", MessageType.Info);
                }
                EditorGUILayout.Space(3);
                EditorGUILayout.PropertyField(_pillShapeHorizontal, new GUIContent("Pill Shape Horizontal", "가로 알약 모양 (좌우 끝이 둥글게)"));
                if (_pillShapeHorizontal.boolValue)
                {
                    EditorGUILayout.HelpBox("가로 알약 모양: 좌우 끝이 완전히 둥글게 됩니다.", MessageType.Info);
                }
                EditorGUILayout.Space(3);
                EditorGUILayout.PropertyField(_pillShapeVertical, new GUIContent("Pill Shape Vertical", "세로 알약 모양 (위아래 끝이 둥글게)"));
                if (_pillShapeVertical.boolValue)
                {
                    EditorGUILayout.HelpBox("세로 알약 모양: 위아래 끝이 완전히 둥글게 됩니다.", MessageType.Info);
                }
                EditorGUILayout.Space(3);
                EditorGUILayout.PropertyField(_useDiamondShape, new GUIContent("Use Diamond Shape", "마름모(다이아몬드) 모양 사용"));
                if (_useDiamondShape.boolValue)
                {
                    EditorGUILayout.HelpBox("마름모 모양: 상/하/좌/우 중앙이 꼭짓점인 다이아몬드 형태가 됩니다.", MessageType.Info);
                    EditorGUILayout.PropertyField(_diamondCurvature, new GUIContent("Diamond Curvature", "1 = 직선 변, 1보다 크면 변이 바깥으로 둥글게, 1보다 작으면 안쪽으로 오목하게"));
                }
                EditorGUILayout.Space(3);
                EditorGUILayout.PropertyField(_skew, new GUIContent("Skew", "아래쪽 고정, 위쪽이 좌우로 기울어짐 (사다리꼴). 0 = 안 기울어짐"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Edge Blur (Airbrush)
            _showEdgeBlur = EditorGUILayout.Foldout(_showEdgeBlur, "💨 Edge Blur (Airbrush)", true);
            if (_showEdgeBlur)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_enableEdgeBlur, new GUIContent("Enable"));
                if (_enableEdgeBlur.boolValue)
                {
                    EditorGUILayout.PropertyField(_edgeBlurAmount, new GUIContent("Blur Amount", "경계 흐림 정도. 클수록 가장자리가 더 넓게 퍼짐"));
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Drop Shadow
            _showDropShadow = EditorGUILayout.Foldout(_showDropShadow, "🌑 Drop Shadow", true);
            if (_showDropShadow)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_enableDropShadow, new GUIContent("Enable"));
                
                if (_enableDropShadow.boolValue)
                {
                    EditorGUILayout.PropertyField(_dropShadowOffset, new GUIContent("Offset", "섀도우 위치 (픽셀)"));
                    EditorGUILayout.PropertyField(_dropShadowColor, new GUIContent("Color"));
                    EditorGUILayout.PropertyField(_dropShadowBlur, new GUIContent("Blur", "경계 흐림 정도"));
                    EditorGUILayout.PropertyField(_dropShadowSize, new GUIContent("Size", "셰도우 크기 (-50 = 축소, 50 = 확대)"));
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(5);
            
            // Inner Shadow
            _showInnerShadow = EditorGUILayout.Foldout(_showInnerShadow, "🌘 Inner Shadow", true);
            if (_showInnerShadow)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_enableInnerShadow, new GUIContent("Enable"));
                
                if (_enableInnerShadow.boolValue)
                {
                    EditorGUILayout.PropertyField(_innerShadowOffset, new GUIContent("Offset", "섀도우 위치 (픽셀)"));
                    EditorGUILayout.PropertyField(_innerShadowColor, new GUIContent("Color"));
                    EditorGUILayout.PropertyField(_innerShadowBlur, new GUIContent("Blur", "경계 흐림 정도"));
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(5);
            
            // Gradient
            _showGradient = EditorGUILayout.Foldout(_showGradient, "🌈 Detailed Gradient", true);
            if (_showGradient)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_enableGradient, new GUIContent("Enable"));
                
                if (_enableGradient.boolValue)
                {
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Base", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_gradientBaseColor, new GUIContent("Base Color", "기본 색상"));
                    
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Color Gradient (색상 그래디언트)", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_enableColorGradient, new GUIContent("Enable Color Gradient", "색상 그래디언트 활성화"));
                    
                    if (_enableColorGradient.boolValue)
                    {
                        EditorGUILayout.PropertyField(_gradientColorStart, new GUIContent("Start Color", "그래디언트 시작 색상"));
                        EditorGUILayout.PropertyField(_gradientColorEnd, new GUIContent("End Color", "그래디언트 끝 색상"));
                        EditorGUILayout.PropertyField(_gradientRadial, new GUIContent("Radial (Center)", "켜면 Direction 대신 중심->가장자리 방향의 방사형 그래디언트 적용"));
                        if (!_gradientRadial.boolValue)
                        {
                            EditorGUILayout.PropertyField(_gradientDirection, new GUIContent("Direction", "그래디언트 방향 (각도)"));
                        }
                        EditorGUILayout.PropertyField(_gradientBlend, new GUIContent("Blend", "그래디언트 블렌드 강도 (0 = 없음, 100 = 최대)"));
                    }
                    
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Light Gradient (명암)", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_lightGradientStrength, new GUIContent("Strength", "명암 강도 (위→밝음, 아래→어두움)"));
                    EditorGUILayout.PropertyField(_lightDirection, new GUIContent("Direction", "광원 방향 (270 = 위에서 아래)"));
                    
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Hue Shift (색온도)", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_hueShiftWarm, new GUIContent("Warm (하이라이트)", "양수 = 따뜻하게"));
                    EditorGUILayout.PropertyField(_hueShiftCool, new GUIContent("Cool (섀도우)", "음수 = 차갑게"));
                    
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Edge Highlight (엣지)", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_edgeHighlightStrength, new GUIContent("Strength", "엣지 하이라이트 강도"));
                    EditorGUILayout.PropertyField(_edgeHighlightSize, new GUIContent("Size", "엣지 크기"));
                    
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Material (재질)", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_materialType, new GUIContent("Type", "Plastic/Metal/Glass/Paper"));
                    
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Noise (노이즈)", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_enableNoise, new GUIContent("Enable"));
                    if (_enableNoise.boolValue)
                    {
                        EditorGUILayout.PropertyField(_noiseStrength, new GUIContent("Strength", "1~3% 권장"));
                    }
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(5);
            
            // Bottom Edge Line
            bool showBottomEdgeLine = EditorGUILayout.Foldout(true, "📏 Bottom Edge Line", true);
            if (showBottomEdgeLine)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_enableBottomEdgeLine, new GUIContent("Enable", "아래 엣지 라인 활성화"));
                
                if (_enableBottomEdgeLine.boolValue)
                {
                    EditorGUILayout.PropertyField(_edgeLineThickness, new GUIContent("Edge Line Thickness", "엣지 라인 두께 (0 = 없음, 20 = 매우 두껍게)"));
                    EditorGUILayout.PropertyField(_edgeLineIntensity, new GUIContent("Edge Line Intensity", "엣지 라인 강도 (0 = 없음, 10 = 매우 강함)"));
                    EditorGUILayout.PropertyField(_edgeLineColor, new GUIContent("Edge Line Color", "엣지 라인 색상"));
                    EditorGUILayout.PropertyField(_edgeLineSharpness, new GUIContent("Edge Line Sharpness", "경계 뚜렷함 (0 = 매우 부드럽게, 10 = 매우 뚜렷하게)"));
                    EditorGUILayout.HelpBox("아래 엣지 라인: UI 요소의 아래쪽 엣지에 두꺼운 선을 추가합니다. 두께, 강도, 색상, 경계 뚜렷함을 조절할 수 있습니다.", MessageType.Info);
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(10);
            
            // Preset
            _showPreset = EditorGUILayout.Foldout(_showPreset, "💾 Preset", true);
            if (_showPreset)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_preset, new GUIContent("Preset", "프리셋 적용"));
                
                if (_preset.objectReferenceValue != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Apply Preset", GUILayout.Height(25)))
                    {
                        uiStyle.ApplyPreset((UIStylePreset)_preset.objectReferenceValue);
                        EditorUtility.SetDirty(uiStyle);
                    }
                    if (GUILayout.Button("Save as Preset", GUILayout.Height(25)))
                    {
                        SaveAsPreset(uiStyle);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    if (GUILayout.Button("Save Current as Preset", GUILayout.Height(25)))
                    {
                        SaveAsPreset(uiStyle);
                    }
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(10);
            
            // Apply button
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Apply Style", GUILayout.Height(30)))
            {
                uiStyle.ApplyStyle();
                EditorUtility.SetDirty(uiStyle);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void SaveAsPreset(global::UIStyle.UIStyle uiStyle)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save UIStyle Preset",
                "UIStylePreset",
                "asset",
                "Choose where to save the preset");
            
            if (string.IsNullOrEmpty(path))
                return;
            
            UIStylePreset preset = ScriptableObject.CreateInstance<UIStylePreset>();
            
            preset.cornerRadius = uiStyle.cornerRadius;
            preset.useCapsuleShape = uiStyle.useCapsuleShape;
            preset.pillShapeHorizontal = uiStyle.pillShapeHorizontal;
            preset.pillShapeVertical = uiStyle.pillShapeVertical;
            preset.useDiamondShape = uiStyle.useDiamondShape;
            preset.diamondCurvature = uiStyle.diamondCurvature;
            preset.enableDropShadow = uiStyle.enableDropShadow;
            preset.dropShadowOffset = uiStyle.dropShadowOffset;
            preset.dropShadowColor = uiStyle.dropShadowColor;
            preset.dropShadowBlur = uiStyle.dropShadowBlur;
            preset.dropShadowSize = uiStyle.dropShadowSize;
            preset.enableInnerShadow = uiStyle.enableInnerShadow;
            preset.innerShadowOffset = uiStyle.innerShadowOffset;
            preset.innerShadowColor = uiStyle.innerShadowColor;
            preset.innerShadowBlur = uiStyle.innerShadowBlur;
            preset.enableGradient = uiStyle.enableGradient;
            preset.gradientBaseColor = uiStyle.gradientBaseColor;
            preset.lightGradientStrength = uiStyle.lightGradientStrength;
            preset.lightDirection = uiStyle.lightDirection;
            preset.hueShiftWarm = uiStyle.hueShiftWarm;
            preset.hueShiftCool = uiStyle.hueShiftCool;
            preset.edgeHighlightStrength = uiStyle.edgeHighlightStrength;
            preset.edgeHighlightSize = uiStyle.edgeHighlightSize;
            preset.materialType = uiStyle.materialType;
            preset.enableNoise = uiStyle.enableNoise;
            preset.noiseStrength = uiStyle.noiseStrength;
            
            preset.enableBottomEdgeLine = uiStyle.enableBottomEdgeLine;
            preset.edgeLineThickness = uiStyle.edgeLineThickness;
            preset.edgeLineIntensity = uiStyle.edgeLineIntensity;
            preset.edgeLineColor = uiStyle.edgeLineColor;
            preset.edgeLineSharpness = uiStyle.edgeLineSharpness;
            
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            
            _preset.objectReferenceValue = preset;
            EditorUtility.SetDirty(uiStyle);
            
            EditorUtility.DisplayDialog("Preset Saved", $"Preset saved to:\n{path}", "OK");
        }
        
        private void DrawHeader(string title, Color color)
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            
            var style = new GUIStyle(EditorStyles.helpBox);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;
            style.padding = new RectOffset(10, 10, 10, 10);
            
            EditorGUILayout.BeginVertical(style);
            EditorGUILayout.LabelField(title, style);
            EditorGUILayout.EndVertical();
            
            GUI.backgroundColor = originalColor;
        }
    }
}
