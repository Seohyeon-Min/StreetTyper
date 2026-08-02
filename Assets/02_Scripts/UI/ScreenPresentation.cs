using System;
using UnityEngine;

/// <summary>전투가 끝나는 세 가지 방식. 어떤 문구와 이미지를 띄울지 고르는 데 쓴다.</summary>
public enum ResultKind
{
    /// <summary>스테이지 클리어. 다음 스테이지로 이어진다.</summary>
    Victory,

    /// <summary>플레이어 사망. 같은 스테이지를 다시 시작한다.</summary>
    Defeat,

    /// <summary>모든 스테이지 클리어.</summary>
    GameClear,
}

/// <summary>
/// 화면 하나가 어떤 상태일 때의 겉모습 — 제목(한/영)과 이미지, 글자색을 인스펙터에서
/// 바꿀 수 있게 묶어둔 것이다.
///
/// <b>화면에 나가는 글자와 이미지는 예외 없이 인스펙터에서 바꿀 수 있어야 한다</b>는 게
/// 이 프로젝트의 기본 사양이고, 이 클래스가 그 단위다. 예전에는 "VICTORY!" / "DEFEAT..." /
/// "ALL STAGES CLEARED!"가 BattleManager 코드에 리터럴로 박혀 있어서 영어 모드에서도 그대로
/// 나왔고, 문구 하나 바꾸려면 코드를 고쳐야 했다.
///
/// 결과 화면(<see cref="BattleManager"/>)과 보상 화면(<see cref="RewardCardView"/>)이 함께 쓴다 -
/// 결과 전용이 아니라서 이름이 ScreenPresentation이다.
/// </summary>
[Serializable]
public class ScreenPresentation
{
    [Tooltip("화면에 뜰 한국어 제목")]
    [SerializeField] private string titleKorean;

    [Tooltip("영어 제목. 비워두면 한국어로 대체되고 경고가 남는다.")]
    [SerializeField] private string titleEnglish;

    [Tooltip("함께 보여줄 이미지. 비워두면 이미지 오브젝트를 끈다.")]
    [SerializeField] private Sprite image;

    [Tooltip("제목 글자색")]
    [SerializeField] private Color titleColor = Color.white;

    /// <summary>Unity 역직렬화용. 아래 생성자를 정의하면 컴파일러가 기본 생성자를 자동으로
    /// 만들어주지 않으므로 여기 명시해야 인스펙터에서 인스턴스가 만들어진다.</summary>
    public ScreenPresentation() { }

    public ScreenPresentation(string titleKorean, string titleEnglish)
    {
        this.titleKorean = titleKorean;
        this.titleEnglish = titleEnglish;
    }

    public string Title(UnityEngine.Object owner, string fieldName)
    {
        return LanguageSettings.Pick(titleKorean, titleEnglish, owner, fieldName);
    }

    public Sprite Image => image;
    public Color TitleColor => titleColor;
}
