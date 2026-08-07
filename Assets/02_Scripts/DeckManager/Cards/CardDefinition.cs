using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <c>CardLocalization.json</c>의 카드 한 행. <b>카드 한 장이 존재하는 유일한 자리</b>이고,
/// 여기서 <see cref="CardDatabase"/>가 실제 카드 객체를 만든다.
///
/// 예전에는 카드 한 장이 <c>.asset</c>(수치) + JSON(문구) 두 곳에 나뉘어 있었다. 문구가
/// 먼저 JSON으로 옮겨가면서 에셋에는 <c>cardNameEn</c>(JSON id)만 남았고, 그마저도 조회 키일
/// 뿐이라 결국 통째로 이쪽으로 합쳤다. 덕분에 시작 단어가 프리팹 기본값과 씬 오버라이드에
/// 따로 저장되어 어긋나던 문제도 같이 사라졌다(<see cref="grantedAtStart"/>).
///
/// ⚠️ <b>enum을 숫자가 아니라 이름으로 적는다.</b> 에셋 시절에는 직렬화되는 게 인덱스라서
/// <c>AttributeEffectType</c>에서 값 하나만 지워도 뒤에 있던 카드가 조용히 다른 효과로
/// 바뀌었다(그래서 "Bleed를 삭제하지 말 것"이라는 주의가 필요했다). 이름으로 적으면 enum
/// 순서가 바뀌어도 안전하고, 못 읽으면 <see cref="CardDatabase"/>가 경고를 남긴다.
///
/// ⚠️ JsonUtility가 읽으므로 <b>필드는 public이어야 하고 이름이 JSON 키와 정확히 같아야</b>
/// 한다. JSON에 없는 칸은 기본값(0/false/null)으로 남으니, 그 카드 종류에 해당하지 않는
/// 칸은 아예 적지 않아도 된다.
/// </summary>
[Serializable]
public class CardDefinition
{
    [Tooltip("카드 고유 id. 코드와 인스펙터가 카드를 가리키는 유일한 키다.")]
    public string id;

    [Tooltip("Action / Modifier / Attribute / Command 넷 중 하나. 어느 클래스로 만들지 정한다.")]
    public string type;

    [Tooltip("true면 런 시작 시 사전에 바로 들어간다(WordUnlockManager.GrantStartingWords).")]
    public bool grantedAtStart;

    // ── 표시 문자열 ───────────────────────────────────────────────────────────
    // 이름은 표시 문자열이자 타이핑 매칭 키다. 한국어/영어 외 언어에서는 이름만 영어를 쓰고
    // (LanguageSettings.PickCardText 참조) 설명·수치 칸만 각 언어로 나간다.
    public string koName, koDesc, koLabel;
    public string enName, enDesc, enLabel;
    public string frName, frDesc, frLabel;
    public string esName, esDesc, esLabel;
    public string jaName, jaDesc, jaLabel;

    /// <summary>수치 칸이 이분법으로 갈리는 카드가 쓰는 "다른 쪽" 문구. 지금은 럭키 하나뿐이다
    /// (보상 -> 보상됨). 이 칸이 없는 카드는 비어 있어 평소 라벨이 그대로 나간다.</summary>
    public string koLabelPending, enLabelPending, frLabelPending, esLabelPending, jaLabelPending;

    // ── 액션 카드 ─────────────────────────────────────────────────────────────
    public string actionKind;
    public int strengthBonus;
    public int timerDelta;
    public bool ignoresDefense;
    public bool breaksEnemyDefense;
    public bool isComboAttack;
    public float comboChancePercent;
    public int comboMinHits;
    public int comboMaxHits;
    public int scalingPerUnit;

    // ── 수식어 / 속성 카드 ────────────────────────────────────────────────────
    // effectType은 type에 따라 ModifierEffectType 또는 AttributeEffectType으로 읽는다.
    // 한 행은 언제나 둘 중 하나이므로 칸을 나눌 이유가 없다.
    public string effectType;
    public string statusEffect;
    public float value;
    public float chancePercent;
    public float chanceGainPerUse;
    public float maxChancePercent;

    /// <summary>액션(니킥/촙/박치기)과 수식어(퍼펙트)가 같이 쓰는 칸이라 양쪽 구획 밖에 둔다.</summary>
    public string scalingSource;
}

/// <summary>JsonUtility가 최상위 배열을 못 읽어서 두는 껍데기. JSON의 <c>{"cards": [...]}</c>와 짝이다.</summary>
[Serializable]
public class CardDefinitionList
{
    public List<CardDefinition> cards;
}
