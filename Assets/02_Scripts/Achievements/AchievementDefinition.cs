using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도전과제 하나가 언제 달성되는지의 <b>종류</b>. 조건의 종류만 코드에 있고, 어떤 도전과제가
/// 존재하는지·문턱값이 얼마인지·Steam id가 무엇인지는 전부
/// <c>AchievementDefinitions.json</c>에 있다.
///
/// ⭐ <b>도전과제 하나를 추가하는 데 코드를 고치지 않는 것이 이 구조의 목적이다.</b> 새 도전과제가
/// 아래 종류 중 하나로 표현되면 JSON에 행 하나만 더 적으면 끝난다. 카드가
/// <c>CardLocalization.json</c>으로, 대사가 <c>DialogueLocalization.json</c>으로 간 것과 같은 이유다 -
/// 단일 어셈블리라 스크립트 한 줄을 고치는 것이 프로젝트 전체의 컴파일을 걸고 넘어진다.
///
/// ⚠️ <b>값을 지우거나 이름을 바꾸면 JSON도 같이 고쳐야 한다.</b> 순서를 바꾸는 것은 안전하다 -
/// JSON이 숫자가 아니라 <b>이름</b>으로 적기 때문이다(카드 JSON의 effectType과 같은 규칙).
/// </summary>
public enum AchievementCondition
{
    /// <summary>게임을 실행했다. 씬과 무관하게 한 번.</summary>
    GameLaunched,

    /// <summary>아무 적이나 쓰러뜨렸다.</summary>
    EnemyDefeated,

    /// <summary>보스(마더 드래곤) 스테이지에 진입했다.</summary>
    BossEncountered,

    /// <summary>보스(마더 드래곤)를 쓰러뜨렸다.</summary>
    BossDefeated,

    /// <summary>적을 쓰러뜨린 그 순간 플레이어 HP가 <see cref="AchievementDefinition.threshold"/> 이하였다.</summary>
    PlayerHPAtKillAtMost,

    /// <summary>한 턴에 액션(= 완성한 조합)을 threshold회 이상 실행했다.</summary>
    ActionsInTurnAtLeast,

    /// <summary>한 조합에 시너지 카드를 threshold장 이상 쌓아 공격했다.</summary>
    SynergiesInChainAtLeast,

    /// <summary><see cref="AchievementDefinition.language"/> 설정으로 전체 클리어했다.</summary>
    ClearInLanguage,

    /// <summary>피해를 한 번도 받지 않고 전체 클리어했다.</summary>
    ClearWithNoDamage,

    /// <summary>덱의 액션 카드가 threshold장 이하인 채로 전체 클리어했다.</summary>
    ClearWithActionsAtMost,

    /// <summary>덱의 시너지 카드가 threshold장 이하인 채로 전체 클리어했다.</summary>
    ClearWithSynergiesAtMost,

    /// <summary>덱이 threshold장 이상인 채로 전체 클리어했다.</summary>
    ClearWithDeckSizeAtLeast,

    /// <summary>덱 증감이 <see cref="AchievementDefinition.addedRule"/>·
    /// <see cref="AchievementDefinition.removedRule"/>을 만족한 채로 전체 클리어했다.</summary>
    ClearWithDeckChange
}

/// <summary>
/// 이번 런에 카드를 몇 장 더했는가 / 뺐는가에 대한 조건.
///
/// ⚠️ <b>숫자 센티널(-1 = 무제한)을 쓰지 않는 이유가 있다.</b> JsonUtility는 JSON에 없는 int 칸을
/// 0으로 채우는데, 0이 "제한 없음"인지 "정확히 0장"인지 구분되지 않는다 - 칸을 빠뜨리면 조용히
/// 다른 규칙이 되어 도전과제가 엉뚱한 때 떠버린다. 이름으로 적으면 빠뜨렸을 때
/// <see cref="AchievementDatabase"/>가 에러를 낸다.
/// </summary>
public enum DeckChangeRule
{
    /// <summary>몇 장이든 상관없다.</summary>
    Any,

    /// <summary>한 장도 없어야 한다.</summary>
    Zero,

    /// <summary>최소 한 장은 있어야 한다.</summary>
    AtLeastOne
}

/// <summary>
/// <c>AchievementDefinitions.json</c>의 도전과제 한 행. <b>도전과제 하나가 존재하는 유일한 자리</b>이고,
/// <see cref="AchievementDatabase"/>가 읽어 <see cref="AchievementManager"/>가 평가한다.
///
/// ⚠️ JsonUtility가 읽으므로 <b>필드는 public이어야 하고 이름이 JSON 키와 정확히 같아야</b> 한다.
/// JSON에 없는 칸은 기본값(0/null)으로 남으니, 그 조건이 쓰지 않는 칸은 아예 적지 않아도 된다.
/// </summary>
[Serializable]
public class AchievementDefinition
{
    [Tooltip("Steam 파트너 사이트의 API Name과 글자 그대로 같아야 한다. 다르면 SetAchievement가 " +
             "실패하는데 Steam은 그것을 조용히 무시하므로 화면에 아무 표시도 나지 않는다.")]
    public string id;

    [Tooltip("AchievementCondition의 값 이름. 숫자가 아니라 이름으로 적는다.")]
    public string condition;

    [Tooltip("문턱값을 쓰는 조건(다이하드/컴보/시너지/덱 크기 등)에서만 쓴다.")]
    public int threshold;

    [Tooltip("ClearInLanguage에서만 쓴다. GameLanguage의 값 이름(Korean/English/French/Spanish/Japanese).")]
    public string language;

    [Tooltip("ClearWithDeckChange에서만 쓴다. DeckChangeRule의 값 이름.")]
    public string addedRule;

    [Tooltip("ClearWithDeckChange에서만 쓴다. DeckChangeRule의 값 이름.")]
    public string removedRule;

    // ── 표시 문구 ─────────────────────────────────────────────────────────────
    // ⚠️ 게임은 이 글자를 그리지 않는다 - 도전과제 이름과 설명은 Steam이 언어별로 들고 있다.
    // 그런데도 여기 두는 이유는 "이 게임에 어떤 도전과제가 있는가"의 단일 출처를 이 파일 하나로
    // 만들기 위해서다. 파트너 사이트에 올릴 원본을 여기서 그대로 복사하면 되고, 나중에 게임 안에
    // 도전과제 목록 UI를 붙이더라도 문구가 이미 자리에 있다.
    public string koName, koDesc;
    public string enName, enDesc;
}

/// <summary>JsonUtility가 최상위 배열을 못 읽어서 두는 껍데기. JSON의 <c>{"achievements": [...]}</c>와 짝이다.</summary>
[Serializable]
public class AchievementDefinitionList
{
    public List<AchievementDefinition> achievements;
}
