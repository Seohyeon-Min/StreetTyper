using UnityEngine;

namespace UIStyle
{
    [CreateAssetMenu(fileName = "UIStylePreset", menuName = "UI/UIStyle Preset", order = 1)]
    public sealed class UIStylePreset : ScriptableObject
    {
        [Header("Rounded Corners")]
        [Range(0, 50)]
        public float cornerRadius = 10f;
        public bool useCapsuleShape = false;
        public bool pillShapeHorizontal = false;
        public bool pillShapeVertical = false;
        public bool useDiamondShape = false;
        [Range(0.2f, 4f)]
        public float diamondCurvature = 1f;

        [Header("Drop Shadow")]
        public bool enableDropShadow = false;
        public Vector2 dropShadowOffset = new Vector2(2, -2);
        public Color dropShadowColor = new Color(0, 0, 0, 0.5f);
        [Range(0, 10)]
        public float dropShadowBlur = 2f;
        [Range(-50, 50)]
        public float dropShadowSize = 0f;
        
        [Header("Inner Shadow")]
        public bool enableInnerShadow = false;
        public Vector2 innerShadowOffset = new Vector2(0, 0);
        public Color innerShadowColor = new Color(0, 0, 0, 0.3f);
        [Range(0, 10)]
        public float innerShadowBlur = 2f;
        
        [Header("Gradient")]
        public bool enableGradient = false;
        public Color gradientBaseColor = Color.white;
        
        [Header("Light Gradient")]
        [Range(0, 10)]
        public float lightGradientStrength = 5f;
        [Range(0, 360)]
        public float lightDirection = 270f;
        
        [Header("Hue Shift")]
        [Range(-10, 10)]
        public float hueShiftWarm = 2f;
        [Range(-10, 10)]
        public float hueShiftCool = -2f;
        
        [Header("Edge Highlight")]
        [Range(0, 20)]
        public float edgeHighlightStrength = 5f;
        [Range(0, 10)]
        public float edgeHighlightSize = 2f;
        
        [Header("Material")]
        public UIStyle.MaterialType materialType = UIStyle.MaterialType.Plastic;
        
        [Header("Noise")]
        public bool enableNoise = false;
        [Range(0, 3)]
        public float noiseStrength = 1f;
        
        [Header("Bottom Edge Line")]
        public bool enableBottomEdgeLine = false;
        [Range(0, 20)]
        public float edgeLineThickness = 2f;
        [Range(0, 10)]
        public float edgeLineIntensity = 2f;
        public Color edgeLineColor = new Color(0, 0, 0, 1f);
        [Range(0, 10)]
        public float edgeLineSharpness = 5f;
        
        public void ApplyTo(UIStyle uiStyle)
        {
            if (!uiStyle) return;
            
            uiStyle.cornerRadius = cornerRadius;
            uiStyle.useCapsuleShape = useCapsuleShape;
            uiStyle.pillShapeHorizontal = pillShapeHorizontal;
            uiStyle.pillShapeVertical = pillShapeVertical;
            uiStyle.useDiamondShape = useDiamondShape;
            uiStyle.diamondCurvature = diamondCurvature;

            uiStyle.enableDropShadow = enableDropShadow;
            uiStyle.dropShadowOffset = dropShadowOffset;
            uiStyle.dropShadowColor = dropShadowColor;
            uiStyle.dropShadowBlur = dropShadowBlur;
            uiStyle.dropShadowSize = dropShadowSize;
            
            uiStyle.enableInnerShadow = enableInnerShadow;
            uiStyle.innerShadowOffset = innerShadowOffset;
            uiStyle.innerShadowColor = innerShadowColor;
            uiStyle.innerShadowBlur = innerShadowBlur;
            
            uiStyle.enableGradient = enableGradient;
            uiStyle.gradientBaseColor = gradientBaseColor;
            uiStyle.lightGradientStrength = lightGradientStrength;
            uiStyle.lightDirection = lightDirection;
            uiStyle.hueShiftWarm = hueShiftWarm;
            uiStyle.hueShiftCool = hueShiftCool;
            uiStyle.edgeHighlightStrength = edgeHighlightStrength;
            uiStyle.edgeHighlightSize = edgeHighlightSize;
            uiStyle.materialType = materialType;
            uiStyle.enableNoise = enableNoise;
            uiStyle.noiseStrength = noiseStrength;
            
            uiStyle.enableBottomEdgeLine = enableBottomEdgeLine;
            uiStyle.edgeLineThickness = edgeLineThickness;
            uiStyle.edgeLineIntensity = edgeLineIntensity;
            uiStyle.edgeLineColor = edgeLineColor;
            uiStyle.edgeLineSharpness = edgeLineSharpness;
        }
    }
}
