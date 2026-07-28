using UnityEditor;
using UnityEngine;

// GDD 4장 단어 사전 중 아직 .asset이 없는 16개 카드를 한 번에 생성하는 1회성 도구.
// 손으로 .asset YAML을 쓰지 않고 AssetDatabase.CreateAsset으로 실제 Unity 에셋 파이프라인을
// 거치게 해서 GUID가 어긋날 위험을 없앤다. 다 만들고 나면 이 스크립트는 지워도 된다.
public static class CardDataSeeder
{
    private const string FolderPath = "Assets/04_Data/Cards";

    [MenuItem("Tools/Deck Manager/Seed Missing Word Cards")]
    private static void SeedMissingCards()
    {
        CreateModifier("Ultra", "울트라", ModifierEffectType.StatBonus, 2);
        CreateModifier("Hyper", "하이퍼", ModifierEffectType.StatBonus, 3);
        CreateModifier("Megaton", "메가톤", ModifierEffectType.StatBonus, 4);
        CreateModifier("Lucky", "럭키", ModifierEffectType.LootBonusOnKill, 0);

        CreateAttribute("Drain", "드레인", AttributeEffectType.LifeDrain, StatusEffectType.None, 0, 10);
        CreateAttribute("Devil", "데빌", AttributeEffectType.DamageReduction, StatusEffectType.None, 0, 25);

        CreateAttribute("Double", "더블", AttributeEffectType.RepeatAction, StatusEffectType.None, 0, 2);
        CreateAttribute("Triple", "트리플", AttributeEffectType.RepeatAction, StatusEffectType.None, 0, 3);

        CreateAttribute("Electric", "일렉트릭", AttributeEffectType.StatusChanceSingle, StatusEffectType.Paralysis, 20, 0);
        CreateAttribute("Ice", "아이스", AttributeEffectType.StatusChanceSingle, StatusEffectType.Freeze, 20, 0);
        CreateAttribute("Colorful", "컬러풀", AttributeEffectType.StatusChanceAll, StatusEffectType.None, 10, 0);

        CreateAction("Jab", "잽", ActionKind.Attack, strengthBonus: 0, timerDelta: 1);
        CreateAction("Hook", "훅", ActionKind.Attack, strengthBonus: 5, timerDelta: -1);
        CreateAction("Feint", "페인트", ActionKind.Attack, strengthBonus: 0, timerDelta: 0, ignoresDefense: true);
        CreateAction("DempseyRoll", "뎀프시롤", ActionKind.Attack, strengthBonus: 0, timerDelta: 0,
            isComboAttack: true, comboChancePercent: 50, comboMinHits: 2, comboMaxHits: 5);
        CreateAction("Guard", "가드", ActionKind.Defense, strengthBonus: 0, timerDelta: 0);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CardDataSeeder: done.");
    }

    private static void CreateModifier(string fileName, string cardName, ModifierEffectType effectType, float value)
    {
        var card = ScriptableObject.CreateInstance<ModifierCardData>();
        var so = new SerializedObject(card);
        so.FindProperty("cardName").stringValue = cardName;
        so.FindProperty("effectType").enumValueIndex = (int)effectType;
        so.FindProperty("value").floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(card, fileName);
    }

    private static void CreateAttribute(string fileName, string cardName, AttributeEffectType effectType,
        StatusEffectType statusEffect, float chancePercent, float value)
    {
        var card = ScriptableObject.CreateInstance<AttributeCardData>();
        var so = new SerializedObject(card);
        so.FindProperty("cardName").stringValue = cardName;
        so.FindProperty("effectType").enumValueIndex = (int)effectType;
        so.FindProperty("statusEffect").enumValueIndex = (int)statusEffect;
        so.FindProperty("chancePercent").floatValue = chancePercent;
        so.FindProperty("value").floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(card, fileName);
    }

    private static void CreateAction(string fileName, string cardName, ActionKind actionKind,
        int strengthBonus, int timerDelta, bool ignoresDefense = false, bool breaksEnemyDefense = false,
        bool isComboAttack = false, float comboChancePercent = 0, int comboMinHits = 0, int comboMaxHits = 0)
    {
        var card = ScriptableObject.CreateInstance<ActionCardData>();
        var so = new SerializedObject(card);
        so.FindProperty("cardName").stringValue = cardName;
        so.FindProperty("actionKind").enumValueIndex = (int)actionKind;
        so.FindProperty("strengthBonus").intValue = strengthBonus;
        so.FindProperty("timerDelta").intValue = timerDelta;
        so.FindProperty("ignoresDefense").boolValue = ignoresDefense;
        so.FindProperty("breaksEnemyDefense").boolValue = breaksEnemyDefense;
        so.FindProperty("isComboAttack").boolValue = isComboAttack;
        so.FindProperty("comboChancePercent").floatValue = comboChancePercent;
        so.FindProperty("comboMinHits").intValue = comboMinHits;
        so.FindProperty("comboMaxHits").intValue = comboMaxHits;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(card, fileName);
    }

    private static void Save(ScriptableObject card, string fileName)
    {
        var path = $"{FolderPath}/{fileName}.asset";
        if (AssetDatabase.LoadAssetAtPath<CardBase>(path) != null)
        {
            Debug.LogWarning($"CardDataSeeder: {path} already exists, skipping.");
            Object.DestroyImmediate(card);
            return;
        }

        AssetDatabase.CreateAsset(card, path);
    }
}
