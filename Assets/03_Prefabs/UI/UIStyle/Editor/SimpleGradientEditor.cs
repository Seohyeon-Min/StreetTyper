using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UIStyle;

[CustomEditor(typeof(SimpleGradient))]
public class SimpleGradientEditor : UnityEditor.Editor
{
    private SerializedProperty _baseColor;
    private SerializedProperty _enableGradientColor;
    private SerializedProperty _gradientColor1;
    private SerializedProperty _gradientColor2;
    private SerializedProperty _enableLightGradient;
    private SerializedProperty _lightGradientStrength;
    private SerializedProperty _lightGradientAngle;
    private SerializedProperty _enableHueShift;
    private SerializedProperty _hueShiftWarm;
    private SerializedProperty _hueShiftCool;
    private SerializedProperty _enableEdgeHighlight;
    private SerializedProperty _edgeHighlightStrength;
    private SerializedProperty _edgeHighlightSize;
    private SerializedProperty _enableMaterialType;
    private SerializedProperty _materialType;
    private SerializedProperty _enableNoise;
    private SerializedProperty _noiseStrength;
    private SerializedProperty _cornerRadius;
    
    private bool _showGradientColor = true;
    private bool _showLightGradient = true;
    private bool _showHueShift = true;
    private bool _showEdgeHighlight = true;
    private bool _showMaterialType = true;
    private bool _showNoise = true;
    
    private void OnEnable()
    {
        _baseColor = serializedObject.FindProperty("baseColor");
        _enableGradientColor = serializedObject.FindProperty("enableGradientColor");
        _gradientColor1 = serializedObject.FindProperty("gradientColor1");
        _gradientColor2 = serializedObject.FindProperty("gradientColor2");
        _enableLightGradient = serializedObject.FindProperty("enableLightGradient");
        _lightGradientStrength = serializedObject.FindProperty("lightGradientStrength");
        _lightGradientAngle = serializedObject.FindProperty("lightGradientAngle");
        _enableHueShift = serializedObject.FindProperty("enableHueShift");
        _hueShiftWarm = serializedObject.FindProperty("hueShiftWarm");
        _hueShiftCool = serializedObject.FindProperty("hueShiftCool");
        _enableEdgeHighlight = serializedObject.FindProperty("enableEdgeHighlight");
        _edgeHighlightStrength = serializedObject.FindProperty("edgeHighlightStrength");
        _edgeHighlightSize = serializedObject.FindProperty("edgeHighlightSize");
        _enableMaterialType = serializedObject.FindProperty("enableMaterialType");
        _materialType = serializedObject.FindProperty("materialType");
        _enableNoise = serializedObject.FindProperty("enableNoise");
        _noiseStrength = serializedObject.FindProperty("noiseStrength");
        _cornerRadius = serializedObject.FindProperty("cornerRadius");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        SimpleGradient simpleGradient = (SimpleGradient)target;
        
        // 헤더
        EditorGUILayout.Space(5);
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 14;
        EditorGUILayout.LabelField("🎨 Simple Gradient", headerStyle);
        EditorGUILayout.Space(5);
        
        // Image 컴포넌트 확인
        Image image = simpleGradient.GetComponent<Image>();
        if (image == null)
        {
            EditorGUILayout.HelpBox("Image 컴포넌트가 필요합니다.", MessageType.Warning);
            if (GUILayout.Button("Image 컴포넌트 추가"))
            {
                simpleGradient.gameObject.AddComponent<Image>();
            }
            EditorGUILayout.Space(5);
        }
        else
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Image 설정", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.ObjectField("Sprite", image.sprite, typeof(Sprite), false);
            EditorGUILayout.ColorField("Color", image.color);
            EditorGUILayout.EnumPopup("Image Type", image.type);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        
        // Base Color
        EditorGUILayout.PropertyField(_baseColor, new GUIContent("Base Color", "기본 색상"));
        EditorGUILayout.Space(5);
        
        // Gradient Colors
        _showGradientColor = EditorGUILayout.Foldout(_showGradientColor, "🎨 Gradient Colors (그래디언트 색상)", true);
        if (_showGradientColor)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_enableGradientColor, new GUIContent("Enable", "그래디언트 색상 활성화"));
            if (_enableGradientColor.boolValue)
            {
                EditorGUILayout.PropertyField(_gradientColor1, new GUIContent("Color 1", "그래디언트 색상 1 (밝은 쪽)"));
                EditorGUILayout.PropertyField(_gradientColor2, new GUIContent("Color 2", "그래디언트 색상 2 (어두운 쪽)"));
                EditorGUILayout.HelpBox("Light Gradient Angle 방향으로 두 색상이 블렌딩됩니다.", MessageType.Info);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space(3);
        
        // Light Gradient
        _showLightGradient = EditorGUILayout.Foldout(_showLightGradient, "☀️ Light Gradient (명암 그래디언트)", true);
        if (_showLightGradient)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_enableLightGradient, new GUIContent("Enable", "명암 그래디언트 활성화"));
            if (_enableLightGradient.boolValue)
            {
                EditorGUILayout.PropertyField(_lightGradientStrength, new GUIContent("Strength", "그래디언트 강도 (0-0.1)"));
                EditorGUILayout.PropertyField(_lightGradientAngle, new GUIContent("Angle", "광원 방향 (0-360도)"));
                EditorGUILayout.HelpBox("위→밝음, 아래→어두움 형태의 명암 그래디언트를 적용합니다.", MessageType.Info);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space(3);
        
        // Hue Shift
        _showHueShift = EditorGUILayout.Foldout(_showHueShift, "🌈 Hue Shift (색온도 변화)", true);
        if (_showHueShift)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_enableHueShift, new GUIContent("Enable", "색온도 변화 활성화"));
            if (_enableHueShift.boolValue)
            {
                EditorGUILayout.PropertyField(_hueShiftWarm, new GUIContent("Warm Shift", "따뜻한 색상 변화 (-10 ~ 10)"));
                EditorGUILayout.PropertyField(_hueShiftCool, new GUIContent("Cool Shift", "차가운 색상 변화 (-10 ~ 10)"));
                EditorGUILayout.HelpBox("하이라이트는 따뜻하게, 섀도우는 차갑게 색온도를 변화시킵니다.", MessageType.Info);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space(3);
        
        // Edge Highlight
        _showEdgeHighlight = EditorGUILayout.Foldout(_showEdgeHighlight, "✨ Edge Highlight (엣지 하이라이트)", true);
        if (_showEdgeHighlight)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_enableEdgeHighlight, new GUIContent("Enable", "엣지 하이라이트 활성화"));
            if (_enableEdgeHighlight.boolValue)
            {
                EditorGUILayout.PropertyField(_edgeHighlightStrength, new GUIContent("Strength", "엣지 하이라이트 강도 (0-0.2)"));
                EditorGUILayout.PropertyField(_edgeHighlightSize, new GUIContent("Size", "엣지 크기 (0-0.1)"));
                EditorGUILayout.HelpBox("이미지 가장자리에 미세한 밝기 변화를 추가합니다.", MessageType.Info);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space(3);
        
        // Material Type
        _showMaterialType = EditorGUILayout.Foldout(_showMaterialType, "💎 Material Type (재질 그래디언트)", true);
        if (_showMaterialType)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_enableMaterialType, new GUIContent("Enable", "재질 타입 활성화"));
            if (_enableMaterialType.boolValue)
            {
                EditorGUILayout.PropertyField(_materialType, new GUIContent("Material", "재질 타입"));
                string materialInfo = "";
                switch ((SimpleGradient.MaterialType)_materialType.enumValueIndex)
                {
                    case SimpleGradient.MaterialType.Plastic:
                        materialInfo = "Plastic: 부드러운 전이";
                        break;
                    case SimpleGradient.MaterialType.Metal:
                        materialInfo = "Metal: 대비 강한 명암";
                        break;
                    case SimpleGradient.MaterialType.Glass:
                        materialInfo = "Glass: 중심 밝음 + 엣지 어두움";
                        break;
                    case SimpleGradient.MaterialType.Paper:
                        materialInfo = "Paper: 거의 변화 없음";
                        break;
                }
                EditorGUILayout.HelpBox(materialInfo, MessageType.Info);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space(3);
        
        // Micro Noise
        _showNoise = EditorGUILayout.Foldout(_showNoise, "🔊 Micro Noise (미세 노이즈)", true);
        if (_showNoise)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_enableNoise, new GUIContent("Enable", "미세 노이즈 활성화"));
            if (_enableNoise.boolValue)
            {
                EditorGUILayout.PropertyField(_noiseStrength, new GUIContent("Strength", "노이즈 강도 (0-0.03)"));
                EditorGUILayout.HelpBox("디지털 느낌을 제거하고 프린트된 느낌을 주는 미세 노이즈입니다.", MessageType.Info);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space(3);
        
        // Rounded Corners
        EditorGUILayout.LabelField("🔲 Rounded Corners (라운드 모서리)", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(_cornerRadius, new GUIContent("Corner Radius", "모서리 둥글기 (0 = 직각, 50 = 매우 둥글게)"));
        EditorGUILayout.HelpBox("UI 요소의 모서리를 둥글게 처리합니다. 화면을 꽉 채우고 모서리만 둥글게 됩니다.", MessageType.Info);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
        
        // Apply 버튼
        if (GUILayout.Button("Apply Gradient", GUILayout.Height(30)))
        {
            simpleGradient.ApplyGradient();
            EditorUtility.SetDirty(simpleGradient);
        }
        
        serializedObject.ApplyModifiedProperties();
    }
}
