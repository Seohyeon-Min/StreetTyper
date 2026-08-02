/// <summary>씬 이름 상수. 빌드 설정(File > Build Profiles)에 등록된 이름과 정확히 같아야 한다.
/// 씬 전환은 전부 여기를 거치게 해서 문자열 오타가 런타임에야 드러나는 일을 막는다.</summary>
public static class GameScenes
{
    public const string Title = "TitleScene";
    public const string Battle = "SampleScene";
    public const string Intro = "IntroScene";
}
