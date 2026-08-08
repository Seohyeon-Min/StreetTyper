using System;
using UnityEngine;

/// <summary>난이도 단계. 순서에 의미가 있으므로 값을 재배열하지 말 것 -
/// <see cref="DifficultySettings.Step"/>이 이 순서를 그대로 -1/0/+1로 읽는다.</summary>
public enum GameDifficulty
{
    Easy,
    Normal,
    Hard
}

/// <summary>
/// 화면에 띄울 난이도 이름 세 개. <b>인스펙터에서 바꿀 수 있어야 하므로</b> 이 프로젝트의
/// 기본 사양대로 <c>[Serializable]</c> 값 묶음으로 두고, 쓰는 쪽이 필드로 하나씩 들고 있는다
/// (<c>ScreenPresentation</c>·<c>TypedCommand</c>·<c>DisplacedUI</c>와 같은 방식).
///
/// ⚠️ <b>이 글자를 static 클래스에 박지 말 것.</b> 그러면 문구 하나 고치는 데 스크립트를 열고
/// 컴파일을 기다려야 하고, 기획·아트가 손댈 수가 없다 - 실제로 그렇게 만들었다가 되돌렸다.
///
/// 지금은 다섯 언어 모두 같은 글자를 쓴다(짧은 라틴 대문자 라벨은 이 게임의 화면 언어다 -
/// <c>STAGE {0}</c>·<c>MOMMY</c>·<c>YOUR TURN!</c>과 같은 부류). 언어별로 나누고 싶어지면
/// 여기에 칸을 늘리는 게 아니라 쓰는 쪽에서 이 묶음을 언어 수만큼 두는 게 맞다.
/// </summary>
[Serializable]
public class DifficultyLabels
{
    public string easy = "EASY";
    public string normal = "NORMAL";
    public string hard = "HARD";

    /// <summary>그 난이도의 표시 이름. 비어 있으면 enum 이름을 대문자로 돌려준다 -
    /// 인스펙터 칸을 비웠을 때 라벨이 통째로 사라지는 것보다 낫다.</summary>
    public string For(GameDifficulty difficulty)
    {
        string label;
        switch (difficulty)
        {
            case GameDifficulty.Easy: label = easy; break;
            case GameDifficulty.Hard: label = hard; break;
            default: label = normal; break;
        }

        return string.IsNullOrEmpty(label) ? difficulty.ToString().ToUpperInvariant() : label;
    }

    /// <summary>지금 난이도의 표시 이름.</summary>
    public string Current => For(DifficultySettings.Current);
}

/// <summary>
/// 게임 난이도. 값 하나와 PlayerPrefs가 전부라 <see cref="LanguageSettings"/>와 똑같이
/// static 클래스로 둔다 - 씬 오브젝트도 DontDestroyOnLoad도 없으니 인스펙터 배선이 늘지 않는다.
///
/// ⭐ <b>난이도별 수치를 여기 모아두지 않는다.</b> 각 값은 그것을 쓰는 매니저가
/// "보통 기준값 + <see cref="Step"/> × 1단계분"으로 계산하고, 1단계분만 자기 인스펙터에 든다
/// (TimerManager의 초, StageManager의 %, SkillResolver의 확률). 난이도별로 값을 3벌씩 들면
/// StageManager에만 칸이 9개 생기고, 곡선을 손볼 때마다 세 벌을 같이 고쳐야 한다.
///
/// ⚠️ <b>전환은 타이틀에서만 해야 한다.</b> 런 도중에 바뀌면 이미 스폰된 적과 다음 적의 기준이
/// 달라지고, 시작 카드는 이미 지급된 뒤다. 옵션 창이 타이틀에만 있어 지금은 자동으로 성립하지만,
/// 일시정지 메뉴에 난이도를 붙이지 말 것(언어와 완전히 같은 이유다 - LanguageSettings 참조).
/// </summary>
public static class DifficultySettings
{
    // 볼륨·언어와 같은 계열의 키를 쓴다(option.volume.master, option.language).
    private const string PrefsKey = "option.difficulty";

    private static GameDifficulty _current = GameDifficulty.Normal;

    /// <summary>지금 난이도. 기본값은 <see cref="GameDifficulty.Normal"/>이고, 그 값이 곧
    /// <b>지금까지의 밸런스 그대로</b>다(모든 스텝이 0이 된다).</summary>
    public static GameDifficulty Current => _current;

    /// <summary>
    /// 보통을 0으로 둔 난이도 단계. <b>어려울수록 +1</b>이다(쉬움 -1 / 보통 0 / 어려움 +1).
    ///
    /// 쓰는 쪽은 "보통 기준값 + Step × 1단계분"으로 계산하되, <b>필드 이름이 방향을 말하게</b> 한다
    /// (<c>secondsLostPerDifficultyStep</c>처럼). 인스펙터에 음수를 넣게 만들면 읽는 사람이
    /// 부호를 두 번 뒤집어 생각해야 한다.
    /// </summary>
    public static int Step => (int)_current - (int)GameDifficulty.Normal;

    /// <summary>난이도가 바뀐 순간 화면에 붙은 라벨들이 스스로 갱신하도록 알린다.</summary>
    public static event Action OnChanged;

    // static 필드는 Play를 멈춰도 남을 수 있어서 실행마다 한 번은 확실히 읽어 오게 한다
    // (LanguageSettings와 같은 이유).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        _current = (GameDifficulty)PlayerPrefs.GetInt(PrefsKey, (int)GameDifficulty.Normal);
    }

    /// <summary>난이도를 바꾸고 디스크에 저장한다. 값이 그대로면 아무 일도 하지 않는다.</summary>
    public static void Set(GameDifficulty value)
    {
        if (_current == value)
            return;

        _current = value;
        PlayerPrefs.SetInt(PrefsKey, (int)value);
        PlayerPrefs.Save();

        OnChanged?.Invoke();
    }

    /// <summary>
    /// 방향키에 맞춰 한 칸씩 옮긴다.
    ///
    /// ⚠️ <b>언어와 달리 순환하지 않는다.</b> 난이도는 순서에 의미가 있어서, 어려움에서 한 칸 더
    /// 갔을 때 쉬움으로 넘어가는 건 사고다 - 양 끝에서 멈춘다.
    /// </summary>
    public static void ChangeDifficulty(int direction)
    {
        var next = Mathf.Clamp((int)_current + direction, (int)GameDifficulty.Easy, (int)GameDifficulty.Hard);
        Set((GameDifficulty)next);
    }

    // ⚠️ 표시 이름은 여기 두지 않는다 - static 클래스에 박으면 인스펙터에서 못 고친다.
    // 화면에 띄울 글자는 DifficultyLabels(위)를 쓰는 쪽이 필드로 들고 있는다.
}
