# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

**스트리트타이퍼 (Street Typer)** — Unity 프로젝트 (Editor **6000.3.19f1**, URP, 2D 템플릿). 타이핑 액션 텍스트 RPG 프로토타입(기획서상 10일 스코프). 핵심 루프: 제한 시간 동안 화면의 5개 단어 슬롯을 **띄어쓰기·Enter 없이** 연달아 타이핑해 스킬 조합을 만들고, 액션 단어로 조합을 완성해 적을 공격한다.

**타이핑 언어는 한국어와 영어 두 가지고, 언어가 곧 입력 파이프라인이다.** `LanguageSettings`(아래 참조)가 지금 언어를 들고 있고 `CardBase.CardName`이 그에 따라 한글 이름 / 영문 이름을 돌려준다 — **카드 이름이 곧 타이핑 매칭 키**라서 언어를 바꾸면 매칭 대상 문자열 자체가 바뀐다. 이건 표시 문자열만 갈아끼우는 흔한 로컬라이제이션이 아니라 **게임 규칙이 걸린 전환**이며, 그래서 전환은 타이틀 씬의 옵션 창에서만 가능하다(런 도중 전환 금지 — 이유는 `LanguageSettings` 절).

기획 문서(`Street_Typer_GDD.pdf`)와 아키텍처 문서(`StreetTyper아키텍쳐 디자인.pdf`)가 저장소 밖에 있다. 아키텍처 문서는 `WordData`/`WordCategory`/`BattleContext`/`Combatant`/`IActionEffect` 같은 계층을 제시하지만 **이 프로젝트는 의도적으로 그걸 따르지 않고** 이미 있는 `CardBase` 계층을 재사용한다(아래 참조). 문서와 코드가 다르면 코드가 맞다.

## 작업 방식

Unity 프로젝트라 터미널에서 돌릴 build/lint/test 스크립트가 없다.

- **에디터**: 프로젝트 루트를 Unity Hub / Editor `6000.3.19f1`로 연다 (`ProjectSettings/ProjectVersion.txt`와 일치해야 함).
- **컴파일**: Unity가 포커스/저장 시 자동 컴파일. `dotnet build` 없음 — `Assembly-CSharp.csproj`/`StreetTyper.sln`은 Unity 생성물이고 gitignore되어 있으니 절대 직접 수정하지 말 것. 단일 `Assembly-CSharp` 어셈블리, `.asmdef` 분리 없음.
  - ⚠️ **어셈블리가 하나라 스크립트 한 개의 문법 오류가 프로젝트 전체를 멈춘다.** CI가 없어서 **에디터를 열기 전까지 드러나지 않는다** — 실제로 `CameraShake.cs`가 그 상태로 커밋된 적이 있다(아래 "알려진 이슈" 첫 항목, 지금은 해결됨). 스크립트를 고친 뒤에는 에디터 콘솔에서 컴파일이 통과했는지 반드시 확인할 것.
  - **다만 터미널에서 Roslyn으로 미리 걸러낼 수는 있다.** 에디터를 열기 전 문법 오류를 잡는 용도로 실제로 성공한 경로다(빌드 스크립트가 아니라 1회성 검증이다):
    1. Unity가 생성한 `Assembly-CSharp.csproj`에서 `<HintPath>`(약 330개)와 `<DefineConstants>`를 뽑아 응답 파일(`.rsp`)로 만든다.
    2. 소스는 csproj의 `<Compile Include>` 목록 대신 `Assets/**/*.cs`를 직접 글롭할 것 — **새로 만든 `.cs`는 csproj에 아직 없다.** csproj 밖 소스가 `Assets/03_Prefabs/UI/UIStyle/Runtime/`에도 있다.
       - ⚠️ **글롭한 다음 `Assets/Plugins/`와 경로에 `/Editor/`가 든 것을 빼야 한다.** 전자는 `FMODUnity.asmdef`로 **다른 어셈블리**라 아래 `-r:`과 중복되어 CS0121(모호한 호출)이 무더기로 나고, 후자는 `Assembly-CSharp-Editor` 소속이라 CS0579(중복 특성)가 난다. 둘 다 **내 코드와 무관한 가짜 오류**이니 여기서 시간을 쓰지 말 것. 제대로 걸러내면 소스가 **96개**(2026-08-08 기준)로 줄고 경고만 남는다.
    3. `-r:Library/ScriptAssemblies/FMODUnity.dll`을 **손으로 추가**해야 한다. FMOD만 csproj에 HintPath가 없다.
    4. `dotnet "C:/Program Files/dotnet/sdk/<ver>/Roslyn/bincore/csc.dll" @response.rsp` — 옵션은 `-target:library -langversion:9.0 -nostdlib+ -noconfig`.
    5. ⚠️ rsp 안의 경로는 **Windows 형식(`C:/...`)**이어야 한다. Git Bash의 `/c/...`를 넣으면 CS0006이 난다.
    - **컴파일만 검증한다.** 인스펙터 배선 누락·씬 참조·IME 동작은 여전히 에디터 Play와 스탠드얼론 빌드로만 확인된다.
  - ### ⭐ 씬·프리팹 충돌은 **줄 단위로 풀지 말 것** — 문서 단위로 풀어야 한다

    `SampleScene.unity`가 충돌하면 git이 헝크를 십수 개 만들어 놓는데, **양쪽이 서로 다른 새 오브젝트를 파일의 같은 위치에 넣어 뒤섞인 것**이라 헝크 하나하나가 의미 있는 단위가 아니다. 실제로 그렇게 풀면 오브젝트 헤더와 본문이 갈라져 YAML이 깨진다. Unity 씬은 `--- !u!<타입> &<fileID>` 문서의 나열이고 **문서 순서는 의미가 없으므로**, 문서 단위 3-way로 풀면 대부분 기계적으로 정리된다.

    1. 세 버전을 꺼낸다 — `git show :1:<path>`(base) · `:2:`(ours) · `:3:`(theirs). ⚠️ **`git add`를 하면 stage 정보가 사라지므로 그 전에 꺼낼 것.** 이미 add했다면 `git merge-base HEAD MERGE_HEAD`와 `HEAD:`/`MERGE_HEAD:`로 대신 꺼낸다.
    2. 각 버전을 `&fileID → 문서` 사전으로 파싱해 **추가/삭제/수정**을 계산한다. 실제로 이 저장소에서 나온 수치는 "추가 ours 40 / theirs 32(ID 겹침 0), 삭제 ours 8, **양쪽이 같이 수정한 문서 1개**"였다 — 헝크 15개짜리 충돌이 실제로는 문서 1개만 손으로 판단하면 되는 문제였다.
    3. ours를 기준으로 두고, **theirs만 수정한 문서는 그쪽을 받고**, theirs가 추가한 문서를 이어 붙인다. 양쪽이 같이 수정한 문서만 손으로 본다(그때도 대개 `m_Modifications`에 서로 다른 프로퍼티를 더한 것이라 합집합이 정답이다).
    4. 검증: **문서 수 = ours + theirs 추가분** · **중복 `&fileID` 0** · **깨진 참조 0**(guid 없는 `{fileID: N}`이 전부 정의된 문서를 가리키는지). `SceneRoots`(`&9223372036854775807`)에 양쪽의 새 루트가 다 들어갔는지도 볼 것 — 새 오브젝트가 기존 루트의 자식이면 건드릴 필요가 없다.

    ⚠️ **`<<<<<<< HEAD` 쪽이 비어 있다고 "상대가 추가했다"로 읽지 말 것.** **"HEAD가 지웠다"일 수도 있고 git은 둘을 구분해 보여주지 않는다.** 반드시 base와 대조할 것 — 실제로 이 저장소에서 그걸 착각해 `theirs`를 통째로 받았다가, 지워둔 컴포넌트를 되살린 적이 있다. base에 있고 한쪽이 지웠으면 **삭제가 이긴다**(상대가 안 건드렸다면).

  - ### ⭐ 씬 YAML에서 **"지금 실제로 쓰이는 값"**을 찾을 때의 함정

    한 필드의 값이 **① C# 필드 초기화자 → ② 프리팹 → ③ 씬 인스턴스 오버라이드** 순으로 덮인다. **③까지 안 보면 틀린 값을 읽는다** — 실제로 엔딩 대사가 프리팹엔 `플레이스홀더텍스트0`인데 씬 오버라이드에 진짜 문구(`정말 장하구나!`)가 있어서, "플레이스홀더가 화면에 뜬다"고 잘못 결론 낸 적이 있다.

    grep으로 찾을 때 놓치기 쉬운 두 가지:

    - ⚠️ **배열 인덱스가 든 `propertyPath`는 작은따옴표로 감싸인다** — `propertyPath: 'endingEventLines.Array.data[0]'`. `propertyPath: endingEventLines`로 찾으면 **0건**이 나와 "오버라이드 없음"으로 오독한다.
    - ⚠️ **중첩 직렬화 구조체는 들여쓰기가 깊다** — `ScreenPresentation.titleKorean`은 `^  ` 가 아니라 `^    `다. `^  키:` 패턴으로만 훑으면 통째로 빠진다.

    반대로 **프리팹에 키가 아예 없으면** 그건 "빈 값"이 아니라 **"C# 초기화자를 쓴다"**는 뜻이다(`grep -c`가 0을 돌려줘도 대사가 멀쩡히 나오는 이유다).

  - ⚠️ **머지 직후에도 같은 이유로 반드시 에디터를 한 번 열어야 한다.** 브랜치 4개(`main`/`SYLEE`/`SeohyeonMin`/`seungju`)가 같은 씬·프리팹을 건드려서 `SampleScene.unity`가 상시 충돌 대상이고, YAML을 손으로 해소하면 컴파일은 통과해도 **연결이 조용히 빠진 상태**가 나온다. 머지 후 확인 순서는 "에디터 콘솔 컴파일 → `TitleScene`부터 Play → 손패·HP·상태 아이콘·적 인텐트가 다 뜨는지"다.
- **테스트**: `com.unity.test-framework`는 설치되어 있으나 **테스트 어셈블리가 하나도 없다.** 여기서 "테스트"란 Play Mode 수동 확인이며, 보통 `Debug.Log` 출력을 읽는 것이다(`DeckManager.logDebugEvents`, `WordChainManager.logDebugEvents`, `WordUnlockManager.logDebugEvents`, `PendingActionManager.logDebugEvents`).
  - **Play는 `TitleScene`부터 시작해야 실제 흐름과 같다.** `SampleScene`을 직접 Play해도 전투는 돌지만, 일시정지에서 "타이틀"을 치면 `TitleScene`으로 넘어가므로 씬 전환 경로를 확인할 수 없다.
    - 다만 `게임시작`은 이제 `IntroScene`을 거치므로 **전투에 닿기까지 슬라이드가 다 지나가거나 아무 키나 3초 눌러 스킵해야 한다.** 전투만 반복해서 볼 때는 `SampleScene`을 직접 Play하는 쪽이 낫고, 씬 전환·타이틀 복귀·BGM 전환을 볼 때만 `TitleScene`부터 갈 것.
- **실행**: 에디터에서 Play. 헤드리스/CLI 실행 경로 없음.
- **빌드**: `File > Build Profiles`로 Windows 스탠드얼론을 뽑아 `Build/StreetTyper.exe`로 내보내 왔다(`Build/`는 gitignore). CLI 빌드 스크립트는 없다.
  - **IME 관련 동작은 에디터 Play만으로 검증하면 안 된다.** 에디터와 빌드가 다르게 동작한 이력이 있어 스탠드얼론에서 재확인해야 한다.
  - **한글은 OS IME가 아니라 우리가 조합한다**(`DubeolsikHangulComposer`, 아래 입력 파이프라인 절). **플랫폼 분기가 없어 에디터 Play에서 실제 경로가 그대로 돈다** — 예전엔 `#if UNITY_WEBGL && !UNITY_EDITOR`라 웹에서만 도는 코드였다. 그래도 IME 강제(IMM32)는 Windows 전용이므로 **한글 입력을 고쳤으면 WebGL 빌드로도 확인할 것**(브라우저 IME는 강제할 수 없어 폴백 경로를 탄다). 활성 빌드 타깃은 아직 Windows이고 `Build/`에도 `StreetTyper.exe`만 있으니 WebGL 검증에는 타깃 전환이 필요하다.
  - 빌드 런타임 로그: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\StreetTyper\Player.log` (회사/제품명이 `DefaultCompany`/`StreetTyper` 기본값 그대로다). 에디터 로그는 `%LOCALAPPDATA%\Unity\Editor\Editor.log`, 임포트 워커 로그는 저장소의 `Logs/`.
- **디버그 킬스위치가 `BattleManager.Update`에 있다** — `0`은 플레이어 즉사, `9`는 현재 적 즉사(둘 다 방어 무시). `#if UNITY_EDITOR || DEVELOPMENT_BUILD`로 감싸여 있어 릴리스 빌드에는 안 들어간다. 예전의 `1`/`2`/`3` 키(피해 10 / 방어 +10 / 적 턴 즉시)는 없어졌다.
  - 두 키에는 **결과 화면(`isGameOver`)·일시정지(`timeScale == 0`)·이벤트 대화(`eventManager.IsEventActive`) 가드가 전부 붙어 있다.** 예전에 가드가 없어 일시정지 메뉴 뒤에서 스테이지가 넘어간 적이 있으니, 디버그 키를 더 늘린다면 같은 가드를 함께 붙일 것.
- **게임을 진행시키는 입력은 전부 타이핑이고, 지금 언어에 따라 한글 또는 영문으로 친다.**
  - 전투 중 — 손패 단어(`CardInputHandler`)
  - 클리어 보상 — 후보 카드 이름 또는 `넘기기`/`skip` → `RewardInputHandler`. **스테이지 클리어 후 플레이어가 치는 건 여기가 마지막이다** — 보상을 고르면 `rewardAdvanceDelay` 뒤에 다음 스테이지가 저절로 열린다.
  - 결과 화면 — `다시하기`/`retry`(재시작) · `카드`/`cards`(보유 카드 목록) · `타이틀`/`title` → `ResultInputHandler`. **런이 끝났을 때(패배·전체 클리어)만** 세 단어가 손패 자리에 명령 카드로 떠오른다. 일반 스테이지 클리어에는 카드도 칠 단어도 없다(보상 선택과 자동 진행이 대신한다). 옛 `다음`/`next`는 **삭제됐다.**
  - 일시정지 중 — `계속`/`resume` · `카드`/`cards` · `타이틀`/`title` → `PauseManager`
  - 보유 카드 목록(**일시정지와 결과 화면** 양쪽에서 `카드`로 연다) — `닫기`/`close` → `CardCollectionPanel`
  - 카드 삭제(마더 드래곤 보상에서 `지우기`/`erase`로 연다) — 지울 카드 이름 → `CardDeletePanel`. **취소 명령이 없다**(열리면 반드시 한 장을 지운다).
  - **여섯 다 `TypingReceiver`를 상속하고 `InputManager`가 우선순위로 딱 하나에게만 넘긴다** — 각자 이벤트를 받아 스스로 비켜서던 옛 구조가 아니다. 자세한 건 아래 입력 파이프라인 절 참조. **새 명령 단어 시스템은 `TypingReceiver`(또는 `CommandWordReceiver`)를 상속하고 `TypingPriority`에 값을 추가하면 되고, 기존 핸들러의 가드를 손댈 필요가 없다.**
  - 명령 단어는 **`CardLocalization.json`의 카드 행**(`type: "Command"` 6장)이고, 쓰는 쪽은 `resumeCardId` 같은 **id 문자열**로 가리킨다. 안내 문구까지 한 묶음으로 드는 `TypedCommand`도 남아 있으나 **지금 쓰는 곳은 `CardCollectionPanel.closeCommand` 하나뿐이다.** 고르는 건 둘 다 `LanguageSettings.Pick`이다.
- **ESC** — 일시정지 토글. `InputManager.OnCancel` 이벤트로 나가고 `PauseManager`가 받는다. **`_inputEnabled` 가드보다 위에서 읽으므로 타이핑이 잠긴 턴 전환 대기 중에도 걸린다.** 보유 카드 목록이 떠 있으면 "한 단계 뒤로"가 되어 **목록만 닫고 일시정지는 유지된다.**
- **스페이스** — 이벤트 대화 넘기기 **+ 스테이지 시작 배너 넘기기.** `InputManager.OnAdvance`가 **구독자 전원**에게 가고 지금 받는 곳이 둘이다 — `EventManager.HandleAdvance`(대사)와 `StageManager.HandleAdvance`(배너 대기 `stageStartDelay`를 끊는다). **일시정지 가드가 없다.**
  - 둘이 겹치지 않게 `StageManager` 쪽이 **① 배너 대기 중일 때만(`_waitingForStageStart`) ② 이벤트 대사가 열려 있지 않을 때만** 받는다. 플래그 없이 두면 **전투 중에 누른 스페이스가 남아 다음 스테이지 배너를 뜨자마자 지운다.**
  - 배너가 뜨고 `stageStartSkipDelay`(0.25초) 안에는 넘기기를 받지 않는다 — 직전 대사를 스페이스로 넘기고 온 연타가 배너를 한 프레임 만에 지우는 것을 막는다.
- **Ctrl(좌/우) 홀드 — 시간 태우기(빨리감기).** `InputManager.HandleBurnTimeKey`가 `ctrlRepeatDelay`(0.4초) 뒤부터 `ctrlRepeatInterval`(0.1초)마다 **`OnBurnTime`**을 쏘고, `DeckManager.HandleBurnTime`이 틱마다 `timerManager.ReduceTime(ctrlBurnSeconds)`(0.25초)를 한다. **남은 시간을 일부러 깎아 턴을 빨리 끝내거나 퍼펙트(줄어든 초당 +2)를 키우는 조작이다.**
  - ⚠️ **누른 첫 프레임에는 쏘지 않는다** — 그러면 연타가 홀드보다 이득이 되어 비용 없이 시간을 태울 수 있다. 이 가드를 빼지 말 것.
  - ⚠️ 이 틱 안에서 남은 시간이 0이 되면 **그 호출 스택 그대로** `CheckExpired` → `OnTimeExpired` → 턴 전환까지 이어진다(`HandleChainCompleted`의 `AddTime`과 같은 함정).
  - `DeckManager` 쪽은 `HasTypingFocus(cardInputHandler)`를 먼저 보므로 **일시정지·보상·결과 화면에서는 안 먹는다.** `InputFieldDisplay`도 이 이벤트를 구독해 Ctrl 튜토리얼 힌트를 끈다.
- C# `LangVersion` 9.0, .NET Standard 2.1 (Mono) — 그 이상 문법은 컴파일 실패한다.

## 프로젝트 구조

최상위 에셋 폴더는 에셋 브라우저 정렬을 위해 `NN_Name` 접두사를 쓴다. 새 폴더도 이 규칙을 따를 것.

저장소 루트에 **프로젝트와 같은 이름의 빈 `StreetTyper/` 폴더**가 있고 그 안에 `.gitignore` 하나만 커밋되어 있다. 루트 `.gitignore`와 거의 같은 Unity 템플릿의 다른 버전이며, 패턴이 그 하위 폴더에만 걸리는데 폴더가 비어 있어 **아무 효과가 없다.** 실제로 동작하는 건 루트 `.gitignore`이니 여기를 고치지 말 것. 같이 커밋된 `UpgradeLog*.htm` 3개도 Visual Studio 변환 로그로 프로젝트와 무관하다.

**씬은 네 개이고 빌드 설정에 그 순서대로 등록되어 있다** — `LogoSplashScene`(인덱스 0) → `TitleScene`(1) → `IntroScene`(2) → `SampleScene`(3). 인덱스 0이 빌드 시작 씬이므로 **실행하면 로고 스플래시부터 뜬다.** 씬 이름 문자열은 `Assets/02_Scripts/GameScenes.cs`의 상수(`GameScenes.Splash`/`Title`/`Intro`/`Battle`)로만 쓰고 직접 타이핑하지 말 것.

**실제 진행 순서는 빌드 인덱스와 같다** — 스플래시가 끝나거나 스킵되면 `LogoSplashController`가 `TitleScene`을 부르고, 타이틀의 `게임시작`이 `IntroScene`을 부르고(`TitleMenu.HandleStart`), 인트로가 끝나거나 스킵되면 `IntroManager`가 `SampleScene`으로 넘긴다. **런을 시작할 때마다 인트로를 지난다**(건너뛰는 옵션은 스킵 홀드뿐이고, 본 것을 기억하는 저장은 없다). 전투를 검증할 때 `SampleScene`을 직접 Play하는 건 그대로 유효하다.

- `Assets/00_Scenes/LogoSplashScene.unity` — 로고 스플래시(빌드 시작 씬). `LogoSplashController`가 페이드인 → 홀드 → 페이드아웃 뒤 `GameScenes.Title`로 넘긴다. ⚠️ **캔버스·이미지를 전부 `BuildView()`가 코드에서 만든다** — 씬에는 컴포넌트 하나뿐이라 로고·색·시간은 그 인스펙터에서 고친다. 스킵은 Space/Z/ESC/좌클릭. 전 구간 `unscaledTime`이라 `timeScale`이 남아 있어도 진행된다(`Awake`에서 1로 되돌리기도 한다).
- `Assets/00_Scenes/TitleScene.unity` — 타이틀 메뉴. `Canvas`(버튼 `GameStart`/`Option`/`Exit`) · `Main Camera` · `EventSystem`.
  - 루트는 `Canvas`(버튼 `GameStart`/`Option`/`Exit`, 그리고 `UI > Option Panel`) · `Main Camera` · `System` · `EventSystem`이다. `TitleMenu` 컴포넌트는 **`03_Prefabs/Managers/TitleManager.prefab`**(구 `TitleMenu.prefab`을 이름만 바꾼 것) 인스턴스로 들어와 있다 — 씬 파일을 스크립트 GUID로 검색하면 0건이 나오는데, 컴포넌트가 프리팹 쪽에 있어서지 없어서가 아니다. **버튼 3개는 정상 동작한다.**
  - `SoundManager` 프리팹 인스턴스도 이 씬에 있다. `OptionsPanel`이 `SoundManager.Instance`로 볼륨을 읽고 쓰므로 **빼면 옵션 창의 슬라이더가 아무것도 하지 않는다**(경고만 뜬다).
  - **언어 전환 버튼(`Option Panel > Language`)도 여기에만 있다.** `OptionsPanel.languageButton`/`languageButtonText`로 연결되어 있고, 누르면 `LanguageSettings.Toggle()`이 돈다. **전투 씬에는 옵션 창이 없으므로 런 도중에는 언어를 바꿀 수 없고, 그게 의도다**(아래 `LanguageSettings` 참조).
  - `Main Camera`를 지우지 말 것. Overlay 캔버스는 카메라 없이도 그려지지만 카메라가 하나도 없으면 "No cameras rendering" 경고가 뜬다.
  - **버튼 3개와 옵션 창은 마우스 없이도 조작된다** — `MenuKeyboardNavigator`가 타이틀 쪽과 옵션 창 쪽에 하나씩 붙어 있다(아래 `MenuKeyboardNavigator` 절). ⚠️ **`EventSystem`의 `Send Navigation Events`를 꺼둔 상태여야 한다.** 켜면 내장 모듈이 같은 방향키·Enter를 같이 처리해 포커스가 두 칸씩 뛴다.
- `Assets/00_Scenes/IntroScene.unity` — 오프닝 스토리. `IntroSystem.prefab`(`IntroManager`) 인스턴스가 슬라이드를 순서대로 넘긴다. 여기서 나가는 곳은 `SampleScene` 하나뿐이라 **인트로에서 타이틀로 되돌아갈 길이 없다**(ESC도 없다).
- `Assets/00_Scenes/SampleScene.unity` — 전투 씬. 루트: `00_BOOT`, `01_CAMERA`, `02_SYSTEM`, `03_WORLD`, `04_UI`, `05_DEBUG`, `EventSystem`.
  - `02_SYSTEM`: 매니저가 **예외 없이 전부 프리팹 인스턴스**다(아래 `03_Prefabs/Managers/` 참조). 예전엔 `FloatingDamageManager`만 씬에 직접 놓인 오브젝트였지만 지금은 그것도 프리팹으로 뽑혀 있다. `CameraShake`만 매니저가 아니라 `01_CAMERA`의 `Main Camera`에 붙은 컴포넌트다.
  - `03_WORLD`: `player`, `enemySpawnPoint`
    - `player`는 자식 없이 `SpriteRenderer` + `Animator` + `CharacterStats` + `PlayerBattleVisuals`를 직접 들고 있다. 예전의 `PlayerVisual`/`PlayerBlink` 두 오브젝트를 겹쳐 깜빡이던 구조는 **없어졌고**, 이제 애니메이터 하나가 대기·펀치를 모두 재생한다.
  - `04_UI`: 캔버스 **일곱 개**. **정렬 순서(Sort Order)가 화면 겹침을 정하므로 같이 적어둔다** — `Card Canvas`(**0** — `Hand`=손패 · `PendingActionList`=쌓인 공격 · `RewardCardList` · `Black`), `Field Canvas`(**0**), `Start Canvas`(**24 — 가장 위** · 스테이지 등장 배너 `stageStartObject`), `RewardCanvas`(**3**), `End Canvas`(**13** — 결과 창 **둘**: `DefeatPanel`/`GameClearPanel`), `Pause Canvas`(**14** — `Pause Panel` · `Card Collection Panel` · **`PauseHand`**), `Input Canvas`(**15** — `InputFieldDisplay`=입력창 · `WordChainText (TMP)` · `Timer Bar`).
    - ⚠️ **`Pause Canvas`(14) > `End Canvas`(13)이라 보유 카드 목록이 결과 창 위에 제대로 덮인다.** 결과 화면에서 `카드`를 칠 수 있는 게 이 순서 덕분이다 — 뒤집으면 목록이 결과 창에 가려 안 보인다.
    - ⚠️ **명령 카드용 `PauseHand`는 `Pause Canvas`(14) 밑에 있고, 일시정지와 결과 화면이 그걸 같이 쓴다**(`PauseManager.commandCardsLayout` = `ResultInputHandler.resultCardsLayout`). 캔버스가 결과 창(`End Canvas` 13)보다 위라 **명령 카드가 항상 결과 창 위에 그려진다** — 형제 순서로 맞출 필요가 없다. 둘이 겹치지 않는 건 **런이 끝나면 일시정지가 아예 안 걸리기** 때문이다(아래 `PauseManager` 참조).
    - `Black`은 손패 뒤에 까는 하단 그라데이션 이미지(`01_Arts/Map/gradation`)일 뿐 스크립트가 없다.
    - `End Canvas`에는 **결과 창이 둘 있다** — `DefeatPanel`(패배)과 `GameClearPanel`(전체 클리어). `ResultPanel.prefab`을 공통 베이스로 한 **프리팹 변형**이고, `BattleManager`가 결과에 맞는 쪽만 켠다(아래 `ResultPanelView` 참조). 둘 다 평소 비활성이어야 한다.
    - **결과 창이 뜨는 조건은 패배·전체 클리어뿐이고, 일반 스테이지 클리어는 결과를 아예 띄우지 않는다**(`ApplyResult`가 `HideResultUI`로 빠진다) — 결과 창이 풀스크린인데 `End Canvas`(13)가 `RewardCanvas`(3)보다 위라, 띄우면 고를 보상 카드를 통째로 덮기 때문이다.
    - ⚠️ 옛 `Field Canvas > Result Text`와 `End Canvas > StatsText`는 **씬에서 사라졌다.** 결과 창의 제목·이미지는 이제 **프리팹이 통째로 갖고**(변형마다 다르게 디자인한다), 코드는 `ResultStatsView`에 수치만 넘긴다.
    - ⚠️ **일곱 캔버스 전부 RectTransform 스케일이 `(0,0,0)`으로 저장되어 있고 그게 정상이다.** Screen Space Overlay 캔버스는 런타임에 Canvas가 트랜스폼을 덮어쓰므로 직렬화된 값이 의미가 없다 — **특정 캔버스가 안 보이는 원인을 여기서 찾지 말 것**(예전엔 `End Canvas`만의 특징인 것처럼 적혀 있었다). 표시 여부를 정하는 건 각 캔버스를 켜고 끄는 코드다.
    - HP·방어도는 씬 오브젝트가 아니라 **`HPBar.prefab` 인스턴스 2개**(`PlayerHPBar`/`EnemyHPBar`)에 뜨고, `WorldAnchoredUI`로 각 캐릭터를 따라다닌다. 상태이상 아이콘(`StatusIconRow`)도 그 프리팹 안에 있다.
    - 말풍선과 적 의도는 **`SpeechBubbleManager`가 런타임에 찍어내는 프리팹**에 뜬다.
    - 전부 Screen Space Overlay이고 **Canvas Scaler 설정이 같아야 한다** — Scale With Screen Size / 1920×1080 / Match Width Or Height 0.5. 예전에 이 설정이 어긋나 해상도가 바뀌면 HP UI만 틀어진 적이 있다. 새 캔버스를 만들면 이 설정을 복사할 것.
    - 입력창 오브젝트는 `InputFieldDisplay`(자식 `Text Area > Text`)다. 이름과 달리 **`TMP_InputField`가 아니라 TMP 라벨**이며 클릭 대상이 아니다(이유는 아래 `InputFieldDisplay` 참조). `Text Area` 래퍼와 그 `RectMask2D`는 예전 입력 필드의 잔재지만 클리핑 용도로 남겨두었다.
    - ⚠️ **HP 바와 타이머 바는 더 이상 `Slider`가 아니다.** `HealthBarUI`/`TimerView`에서 `Slider` 필드와 로직이 빠졌고, 채우기는 `UIStyle.UIStyle.SetFillAmount(ratio)`가 한다(`uiStyleFill` 인스펙터 필드). 씬의 `Timer Bar`와 `HPBar.prefab`에 남아 있는 `Slider` 컴포넌트는 이제 아무도 읽지 않으니 지워도 안전하다. **`Slider` 기반으로 되돌리지 말 것.**
- `Assets/01_Arts/Fonts/` — TMP 폰트. **한글이 흐를 수 있는 자리에는 반드시 한글 글리프를 가진 폰트를 써야 한다.** 라틴 전용 폰트에 한글을 넣으면 `□`로 나오고 문자당 경고가 하나씩 찍힌다.
  - **한글용은 Paperlogy 계열**(`4Regular`/`7Bold`/`8ExtraBold`). 입력창 `Text`, `WordChainText (TMP)`, `Card.prefab > NameText`, `Actions.prefab`, `SpeechBubble.prefab`, 일시정지 안내 라벨, 타이틀 버튼이 여기에 속한다.
  - ### ⭐ 일본어는 **폰트 폴백**으로 나온다 — 라벨마다 폰트를 바꾸지 말 것

    Paperlogy는 **한글+라틴 11,496자짜리 정적(Static) 아틀라스라 가나·한자가 한 자도 없다.** 그래서 일본어 모드에서는 모든 라벨이 `□`가 됐다. 고친 방법은 라벨을 갈아끼우는 게 아니라 **`TMP Settings`의 전역 폴백**(`m_fallbackFontAssets`)에 **`MochiyPopOne-Regular SDF`**(일본어 전용, **Dynamic** 모드)를 하나 넣은 것이다 — TMP가 글자 단위로 "주 폰트에 없으면 폴백에서" 찾으므로 **라벨을 하나도 안 건드리고 전부 해결된다.**

    - ⚠️ **`MochiyPopOne`의 `Multi Atlas Textures`를 반드시 켜둘 것**(`m_IsMultiAtlasTexturesEnabled: 1`). 아틀라스가 1024×1024 / 샘플링 90pt라 한 장에 80자 남짓밖에 안 들어가는데 **카드 설명에만 고유 한자·가나가 158자**다. 이게 꺼져 있으면 첫 아틀라스가 찬 뒤부터 **다시 `□`가 되는데, 앞부분은 멀쩡해서 폰트 문제로 안 보인다.**
    - 메모리가 아까우면 폰트 에셋 인스펙터에서 샘플링 크기를 60 안팎으로 낮추고 아틀라스를 2048로 키우면 한 장에 다 들어간다(YAML로 손대지 말 것 — 아틀라스 텍스처가 에셋 안에 같이 들어 있다).
    - Dynamic이라 **원본 `MochiyPopOne-Regular.ttf`(4.8MB)가 빌드에 들어가야 한다.** `m_SourceFontFile` 참조를 끊지 말 것.
    - ⚠️ **폴백은 한글을 구제하지 못한다.** Mochiy에 한글이 없어서, 라틴 전용 폰트(`Saira`/`LiberationSans`)를 쓰는 라벨에 한글이 흐르면 여전히 `□`다. 그건 아래 규칙 그대로다.
    - 반대로 `IntroScene`은 이 폰트를 **직접** 쓰고 있다(폴백이 아니라 주 폰트로). 거기 문구는 아직 한 벌뿐이라 한국어가 흐르는데 Mochiy에 한글이 없으니, **인트로를 다국어로 만들 땐 폰트부터 확인할 것.**
  - **라틴 전용 폰트가 여러 벌 들어와 있다** — `SairaExtraCondensed`(Bold/Medium), `Serati-Regular`, `Board of Directors`. 숫자·영문 전용 자리(수치 라벨 등)에 쓰라고 들어온 것이고, **한글이 지나갈 수 있는 라벨에는 절대 붙이지 말 것.** ⚠️ 영어 모드에서는 카드 이름·명령 단어·상태이상 이름이 전부 영문으로 바뀌지만 **그 반대는 성립하지 않는다** — 한국어 모드로 돌아오면 같은 라벨에 한글이 흐른다. 즉 **언어에 따라 폰트를 바꾸는 장치는 없으므로, 두 언어를 다 지나가는 라벨은 Paperlogy로 통일해야 한다.**
  - `Serati-Regular SDF`는 임포트만 되어 있고 **아직 아무 데도 안 쓰인다.**
  - **여전히 `LiberationSans SDF`인 곳**: 스테이지 등장 배너와 **`CurrentStageText.prefab`**(`STAGE n`). 지금은 라틴 문자만 흐르지만, ⚠️ **한글 문구로 바꾸는 순간 `□`가 된다** — 문구를 언어별로 만들 거면 폰트도 Paperlogy로 같이 바꿔야 한다.
    - `HPBar.prefab`은 더 이상 `LiberationSans`가 아니다 — 라벨 5개가 `SairaExtraCondensed-Bold SDF **2**`(`8ee6441a…`), 하나가 Saira Medium이다.
    - ⚠️ **`Card Collection Panel.prefab`의 `Title`/`Hint`는 폰트 에셋이 Paperlogy 4Regular인데 머티리얼(`m_sharedMaterial`)만 `LiberationSans SDF`의 것으로 남아 있다.** 둘이 어긋나면 아틀라스가 맞지 않아 글자가 깨져 나올 수 있는데, 이 창의 제목과 안내는 한국어 모드에서 한글(`보유 카드`, `"닫기"를 입력하면…`)이 흐르는 자리다 — **에디터에서 실제로 어떻게 나오는지 먼저 확인할 것.**
  - ⚠️ **`SairaExtraCondensed-Bold`가 SDF 에셋 세 벌로 중복 임포트되어 있다** — `SDF`(`1f9639e7…`, 씬에서 사용) · `SDF 1`(`6b43f9d8…`, **아무 데도 안 쓰임**) · `SDF 2`(`8ee6441a…`, `HPBar.prefab`에서 사용). 같은 폰트인데 GUID가 달라, 한 벌만 보고 "이 폰트는 여기서만 쓴다"고 결론 내리면 틀린다.
  - 현황을 다시 셀 때는 GUID로 세면 된다 — Paperlogy 4Regular `53b522988c0e6f94d8a0a2d8ed5d613c`, 7Bold `80cc6b00dbd63ac4c9644223150de38e`, 8ExtraBold `03d3b38623907e04b9f51cc6c42a3fb4`, Saira Bold `1f9639e73e173d74e94b542c75c24fa2` / `6b43f9d80c9782d46bb2c7de6bd6eb12` / `8ee6441a73018dc48abb492b496556b6`(위 중복 3벌), Saira Medium `042b9eb82e6b35047ae764188e38c2dc`, `LiberationSans SDF` `8f586378b4e144a9851e7b34d9b748ee`. **프리팹까지 같이 세야 하고**(HP·말풍선·카드 텍스트는 씬이 아니라 프리팹 안에 있다), **`m_fontAsset`과 `m_sharedMaterial`을 따로 봐야 한다** — 위 `Card Collection Panel`처럼 둘이 다를 수 있어서 한쪽만 세면 놓친다.
- `Assets/01_Arts/Demi/` — 플레이어 스프라이트(`DemiOpenEyes`, `DemiPunch1~4`)와 애님 클립(`PlayerIdle`, `Punch1~4`), 컨트롤러. `PlayerBattleVisuals`가 `Punch1`(첫 타) / `Punch2~4`(랜덤) 트리거를 쏜다. 컨트롤러 파일명 `DemiOpneEyes_0`의 오타는 그대로 두었다.
- `Assets/01_Arts/UI/` — 말풍선 이미지(`SpeechBubbleBody`, `SpeechBubbleTailx2` 일반 꼬리, `ThinkBubbleTailx2` 생각풍선 꼬리)와 **아이콘 8종**(`fire`/`Electric`/`ice`/`Devil` = 상태이상, `Attack`/`shield`/`Up` = 적 인텐트, `Clock`).
  - ⚠️ **꼬리는 이제 코드가 바꾸지 않는다.** 예전 `SpeechBubble.Setup(message, isPlayer, isNormalTail)`은 꼬리의 앵커·피벗·좌우 반전을 코드에서 뒤집었지만, 지금 `Setup(string)`은 텍스트만 넣고 **꼬리는 프리팹에 배치된 그대로 쓴다.** 그래서 일반 말풍선과 생각 말풍선이 `SpeechBubble.prefab` / `ThinkingBubble.prefab` **두 프리팹으로 갈린다.**
- `Assets/01_Arts/Shader/` — 직접 작성한 셰이더. `HitImpact`(피격 이펙트 — `_Progress`/`_FadeStart`로 원형 윤곽선이 나타났다 사라지고 `_GrowthPower`로 커지는 이징 조절), `SpriteOutline`(캐릭터 윤곽선 — 유클리드 거리 팽창 + 프리멀티플라이드 알파 `Blend One OneMinusSrcAlpha`라 반투명 가장자리에서도 안 끊긴다), `UnderwaterGodRaySprite`(배경 광선 — 각도 등분 방식 `_LineCount1`/`_LineCount2`/`_LineWidth`/`_LineJitter`/`_LineSharpness`, 발광 닷지 합성용 `_BackgroundTex`를 머티리얼에 **수동으로** 연결해야 한다), `UnderWaterGodRay`, `SpriteEmissionURP`, `UIStyle`.
  - **`SpriteOutline.mat`은 이제 실제로 붙어 있다** — 씬의 `player`와 일반 적(`Dragon1~6.prefab`)의 `SpriteRenderer` 둘 다. (예전 문서엔 "아직 연결되지 않았다"고 적혀 있었다.)
    - ⚠️ **짝인 `SpriteOutlineUVSync.cs`도 같이 붙여야 한다.** 애니메이션 프레임(`Punch1~4`)처럼 한 텍스처에 스프라이트가 패딩 없이 붙어 있으면 윤곽선 팽창 샘플링이 **옆 프레임까지 읽어 이전/다음 프레임의 윤곽선이 같이 그려진다.** 이 컴포넌트가 `LateUpdate`에서 `SpriteRenderer.sprite.textureRect`를 UV로 바꿔 셰이더의 `_SpriteUVRect`에 `MaterialPropertyBlock`으로 흘려주면 그 범위 밖은 아예 샘플링하지 않는다. 안 붙어 있으면 기본값 `(0,0,1,1)`이라 옛 증상 그대로다. 여유폭은 머티리얼의 `_SpriteRectInset`(px).
    - `sprite`가 바뀔 때만 다시 쓰고, `LateUpdate`인 건 `Animator`의 스프라이트 교체가 그 전에 끝나 있기 때문이다.
- `Assets/01_Arts/Card/` — 카드 아트 5장. 카드 프레임 2종(`Active` 분홍 = 액션 / `Modifier` 남색 = 그 외)과 효과 배지 3종(`IconActive`/`IconMultiply`/`IconUp`). **전부 `Card.prefab`의 `CardView`가 인스펙터로 들고 있고, 코드가 카드 카테고리를 보고 골라 끼운다**(아래 `CardView` 참조).
  - ⚠️ **다섯 장 모두 Sprite(Single)로 임포트되어 있다.** 예전엔 Multiple이었고 `IconActive`는 6장짜리 시트였다. Multiple로 되돌리면 프리팹의 `fileID: 21300000`(텍스처 단일 스프라이트 ID)이 서브스프라이트를 못 찾아 **경고 없이 조용히 비어버린다.**
- `Assets/03_Prefabs/` — `Card.prefab`(런타임 생성되는 손패 카드 — 자식 `Frame`·`NameText`·`Badge`·`Stats`·`Description`, 루트에 `CanvasGroup`+`CardView`+`CardSlotView`. **손패 · 보상 · 일시정지 명령 카드 · 결과 화면 명령 카드 · 보유 카드 목록 · 삭제 목록이 전부 이 하나를 쓴다** — 카드가 화면마다 다르게 보이면 안 된다), `HPBar.prefab`(HP·방어·상태이상 아이콘 UI 한 벌, **플레이어/적이 같은 프리팹을 인스턴스로 공유**), `SpeechBubble.prefab`(말풍선, `ContentSizeFitter`로 문장 길이에 맞춰 늘어난다), `ThinkingBubble.prefab`(생각풍선 꼬리 버전 — `PendingActionView.bubblePrefab`이 이걸 쓴다), `MotherDragon.prefab`(`MotherDragon` 컴포넌트가 붙어 있어야 마더 드래곤으로 인식된다), `Dragon1~6.prefab`(일반 적 6종 — 아래 참조). ⚠️ 결과 창 프리팹 세 개는 **`03_Prefabs/Game Over/`** 밑에 있다.
  - 그 외: `FloatingDamageText.prefab`(피해 숫자 한 개), `Effect/HitEffect.prefab`(피격 이펙트, `EffectBase`+`HitImpact` 셰이더), `Map/Map.prefab`·`Map/HomeDustParticle.prefab`(배경), `Volume Slider.prefab`·`VolumeText.prefab`(옵션 창 슬라이더 한 줄), `Pause Panel.prefab`(일시정지 창 — `TextGateRevealAnimation`이 붙은 `PAUSE` 제목과 명령 카드용 `HandFanLayout`), `ResultPanel.prefab`(결과 창 **공통 베이스** — `ResultStatsView` + `TextGateRevealAnimation` 2개 + `FloatBob`. 씬에는 이게 아니라 변형 둘이 들어간다)·`DefeatPanel.prefab`/`GameClearPanel.prefab`(그 변형 — 패배용/전체 클리어용 겉모습을 각자 갖는다), `Card Collection Panel.prefab`(일시정지 중 여는 보유 카드 목록 — `CardCollectionPanel`), `CurrentStageText.prefab`(화면에 상시 뜨는 `STAGE n` 라벨), `IntroSystem.prefab`(`IntroScene` 전용).
  - ⚠️ **`Actions.prefab`은 이제 죽은 에셋이다.** `PendingActionView`가 "한 줄에 프리팹 하나"를 찍어내던 구조에서 **말풍선 하나에 줄바꿈으로 이어 붙이는** 구조로 바뀌면서 아무도 참조하지 않게 됐다(`_Recovery`의 백업 씬만 아직 가리킨다). 예전 구조로 되돌리려다 이걸 되살리지 말 것.
  - 구 `PlayerSpeechBubble.prefab`은 **삭제됐다.** `EnemySpeechBubble.prefab`은 GUID가 유지된 채 `SpeechBubble.prefab`으로 이름만 바뀌었다(`ececaf37…`). 예전에 남아 있던 `BattleManager.prefab`의 `playerSpeechBubblePrefab` 깨진 참조는 **정리됐다** — 지금 말풍선 프리팹 필드는 **넷**이다 — `SpeechBubbleManager`의 `speechBubblePrefab`(기본) · **`motherDragonSpeechBubblePrefab`**(`SpeechBubbleMother.prefab`) · **`motherDragonPlayerSpeechBubblePrefab`**(`SpeechBubblePlayer.prefab`), 그리고 `PendingActionView.bubblePrefab`. 마더 드래곤 대화용 두 개는 **비어 있으면 `speechBubblePrefab`으로 폴백**하므로 안 붙였을 때 조용히 평범한 말풍선이 나온다.
  - `strongEnemy.prefab`은 **삭제됐다.** `StageManager.prefab`의 `enemyPrefabs` 기본값(3칸)에는 아직 그 깨진 GUID(`f468762e…`)가 2번째 항목으로 남아 있지만, **`SampleScene.unity`의 씬 인스턴스가 `enemyPrefabs.Array.size`를 6으로 덮어써 `Dragon1~6.prefab`을 전부 유효한 참조로 채운다** — 깨진 항목은 씬에서는 이미 가려져 있다(아래 `StageManager` 참조. 옛 `enemy.prefab`은 이제 `Dragon1.prefab`으로 이름만 바뀌었다).
- `Assets/03_Prefabs/Managers/` — 매니저 프리팹 **열여덟 개**(`InputManager`/`Deck Manager`/`StageManager`/`BattleManager`/`WordDictionary`/`WordUnlockManager`/`StatusEffectManager`/`SpeechBubbleManager`/`EventManager`/`SoundManager`/`TimerManager`/`ResultInputHandler`/`RewardInputHandler`/`TitleManager`/`FloatingDamageManager`/`HitEffectManager`/**`HealEffectManager`**/`StatisticsManager`). **`TitleManager`만 `TitleScene`용이고 나머지가 `SampleScene`의 `02_SYSTEM`에 들어간다.** 매니저는 전부 프리팹으로 뽑혀 있고 씬에는 인스턴스만 있다. **씬은 이걸 인스턴스로 들고 있고, 매니저끼리와 씬 오브젝트를 향한 인스펙터 연결은 전부 프리팹 인스턴스 오버라이드로 저장된다**(`SampleScene.unity`의 `m_Modifications` 안 `objectReference`). 자세한 주의점은 컨벤션 절 참조.
  - ⚠️ **매니저 프리팹은 반드시 이 폴더 하나에만 둘 것.** 머지 중에 `FloatingDamageManager`/`HitEffectManager`가 `03_Prefabs/` 루트에 **GUID가 다른 똑같은 복제본**으로 한 벌 더 생긴 적이 있다. 복제본은 내용이 같아도 GUID가 달라 씬이 어느 쪽을 가리키느냐에 따라 **싱글턴이 두 개 생기거나(둘 다 씬에 들어간 경우) 인스펙터 연결이 통째로 빈다.** 같은 이름의 프리팹이 두 경로에 보이면 그건 머지 사고다.
- `Assets/_Recovery/` — Unity 크래시 복구가 남긴 씬 덤프(`0.unity`, `0 (1).unity`)가 커밋되어 있다. **프로젝트와 무관한 잔재**이고 빌드 설정에도 없다. 여기를 실제 씬으로 착각하지 말 것 — 죽은 에셋(`Actions.prefab` 등)의 마지막 참조가 여기 남아 있어서 "아직 쓰이는 것처럼" 보이게 만든다.
- ⭐ `Assets/04_Data/Resources/CardLocalization.json` — **카드 34장이 존재하는 유일한 자리**(단어 28 + 명령 6). 이름·설명·수치 칸을 5개국어로, 그리고 수치·분류·시작 단어 여부까지 한 행에 담는다. `Resources` 폴더에 있는 건 `CardDatabase`가 `Resources.Load`로 읽기 때문이다 — **옮기지 말 것.**
  - ⚠️ **카드 `.asset`은 전부 삭제됐다**(옛 `04_Data/Cards/`와 `Cards/Commands/`). 예전엔 한 장이 에셋(수치) + JSON(문구) 두 곳에 나뉘어 있었고, 그래서 어퍼컷 설명이 에셋엔 "15의 피해" JSON엔 "4의 피해"로 남는 어긋남이 실제로 생겼다. `ScriptableObject`로 되돌리지 말 것.
  - 한 행의 뼈대: `id`(코드·인스펙터가 카드를 가리키는 키) · `type`(`Action`/`Modifier`/`Attribute`/`Command`) · `grantedAtStart`(런 시작 지급) · 언어별 `koName`/`koDesc`/`koLabel`… · 그 종류의 수치 칸. **그 카드에 해당 없는 칸은 아예 적지 않아도 된다**(JsonUtility가 기본값으로 둔다).
  - ⭐ **enum을 숫자가 아니라 이름으로 적는다**(`"effectType": "CritMultiplier"`). 에셋 시절엔 인덱스가 직렬화돼서 `AttributeEffectType`에서 값 하나만 지워도 뒤에 있던 카드가 조용히 다른 효과가 됐다 — **그 위험이 사라졌으니 이제 enum 순서를 바꿔도 안전하다.** 못 읽는 이름이면 `CardDatabase`가 에러를 낸다.
  - ⚠️ **`id`는 이름이 아니라 키다.** 인스펙터의 `resumeCardId` 같은 칸과 이 id가 어긋나면 그 명령이 통째로 사라진다(`CardDatabase.Get`이 경고를 낸다). 카드를 추가할 땐 id를 먼저 정하고, **다른 카드 이름의 접두사가 되지 않게** 할 것(접두사인 쪽은 영영 완성할 수 없다).
  - ⚠️ **명령 카드에 `grantedAtStart`를 켜지 말 것.** 켜면 사전에 들어가 손패에 뜬다. 예전엔 폴더를 나눠 막았지만 지금은 같은 파일에 있으므로, `WordUnlockManager`가 `CardCategory.Command`를 거르는 것이 **유일한 방어선**이다.
  - ⚠️ **수치 칸이 포맷 문자열인 카드가 있다.** 어썸은 `"+{0}"`, 퍼펙트/니킥/촙/박치기는 `"{0}"`이며 `{0}` 자리에 런타임 수치가 들어간다(아래 `SkillResolver` 절). 그 칸을 고정 문구로 바꾸면 **숫자가 사라지고 적어둔 글자만 뜬다** — 조용히 옛 방식으로 되돌아가는 것이라 눈치채기 어렵다.
  - 카드에 `icon`은 없다. 프레임·배지는 카테고리로 정해지고(`CardView`), JSON은 스프라이트 참조를 담을 수 없다 — 카드별 아이콘이 필요해지면 `iconPath` 문자열 + `Resources.Load`로 가야 한다.
- `Assets/04_Data/EnemyTutorial.asset` — 유일한 `EnemyData`.
- `Assets/05_Sounds/FMOD/StreetTyperFMOD/` — **FMOD Studio 프로젝트 원본이 저장소 안에 있다.** `StreetTyperFMOD.fspro`(에디터로 여는 파일) · `Metadata/`(이벤트·버스·뱅크 정의 XML) · `Build/Desktop/`(빌드된 `.bank` 4개) · `Assets/`(원본 오디오). 사운드 구조를 바꾸려면 Unity가 아니라 여기를 FMOD Studio로 열어야 한다 — 자세한 건 아래 `SoundManager` 절과 "알려진 이슈" 참조.
- `Assets/InputSystem_Actions.inputactions` — Input System 기본 템플릿. **미사용.** 게임플레이 입력은 의도적으로 이걸 거치지 않는다(아래).

## 아키텍처

### 전체 흐름

```
키보드 → InputManager → CardInputHandler(5슬롯 매칭) → WordChainManager(조합 검증)
   → [액션 단어로 완성] → DeckManager.HandleChainCompleted
      → SkillResolver(수치 계산) → PendingActionManager(쌓아둠, 아직 적용 안 함)
      → TimerManager.AddTime(시간 증감만 즉시)
   → [타이머 0] → DeckManager.HandleTimeExpired
      → PlayPendingActions
         → PlayerBattleVisuals 돌진 → 쌓인 수만큼 펀치(+ CameraShake / FloatingDamage)
            → CombatManager(적용) → 복귀
      → 손패 리롤 → 적 턴 → StatusEffectManager.OnEnemyTurnEnded(화상 피해·지속 감소)
      → 다음 플레이어 턴(적이 마비면 +5초)
```

`DeckManager`는 단순 파사드가 아니라 **전투 배선의 중심**이다 — 체인 완성과 타이머 만료를 받아 나머지 시스템을 순서대로 호출한다.

⚠️ **체인을 완성해도 그 자리에서 피해가 들어가지 않는다.** 한 턴 동안 완성한 조합은 `PendingActionManager`에 쌓이기만 하고, 턴이 끝날 때 한꺼번에 재생된다. **유일한 예외가 `TimerChange`(잽/훅/퀵/어퍼컷)**로, 이건 남은 시간을 늘리거나 깎는 리스크라 즉시 반영해야 의미가 있다.

### 턴 전환 딜레이 — 애니메이션 자리를 미리 잡아둔 값이다

턴이 바뀔 때마다 코루틴이 사이사이 대기를 넣는다. **이 숫자들은 임의로 고른 게 아니라, 앞으로 들어올 연출의 길이를 어림잡아 미리 자리를 비워둔 것이다.** 줄이거나 없애면 나중에 애니메이션을 넣을 자리가 사라진다. 연출이 실제로 붙을 때 그 길이에 맞춰 조정할 값들이다.

⚠️ **적 공격 모션이 붙으면서 이 표의 전제가 반쯤 바뀌었다.** 적 턴 자체가 코루틴이 되어 돌진→타격→복귀가 실제로 재생되고, 아래 두 대기는 **그 연출의 앞뒤 여백**이지 "빈 시간"이 아니다.

| 필드 | 코드 기본값 | 씬 현재값 | 비우고 있는 자리 |
|---|---|---|---|
| `DeckManager.turnChangeDelay` | 2 | 1 | 타이머 만료 → 적이 공격하기까지 |
| `DeckManager.postAttackDelay` | 4 | 2 | 적 공격 → 플레이어 턴 재개까지. **뒷부분은 3-2-1 카운트다운이 실제로 채운다**(아래) |
| `StageManager.stageStartDelay` | 2 | 2 | 스테이지 등장 → 플레이어 턴 시작까지 |
| `BattleManager.actionBubbleDuration` | 1 | 1 | 공격 말풍선이 떠 있는 시간 |
| `DeckManager.pendingActionInterval` | 0.3 | — | 쌓인 공격이 하나씩 터지는 간격 |
| `StageManager.rewardAdvanceDelay` | 0.6 | — | 보상 선택 → 다음 스테이지 자동 진행까지 |

`pendingActionInterval`은 위의 다른 값들과 성격이 다르다 — **자리만 비워둔 값이 아니라 실제로 연출이 일어나는 구간**이다(쌓인 공격이 하나씩 적용되며 HP가 계단식으로 줄어든다). **`postAttackDelay`도 이제 절반쯤 그렇다** — 뒷부분 `YourTurnBanner.CountdownDuration`(기본 3 × 0.35 = 1.05초)만큼을 3-2-1 카운트다운이 쓰고, 앞의 남은 시간만 빈 여운이다. 카운트다운이 이 대기보다 길어지면 앞이 0이 되고 **그만큼 턴 간격이 실제로 늘어난다.**

⚠️ **이 값은 그대로 쓰이지 않는다.** `PlayPendingActions`가 **공격 연출 전체를 3.9초 안에 끝내도록** 간격을 스스로 압축한다(`maxTotalTime` / 돌진·복귀 0.2초씩이 하드코딩되어 있다). 쌓인 공격이 많으면 간격이 줄고 그 비율만큼 `Animator.speed`가 올라가 애니메이션도 같이 빨라진다. 즉 `pendingActionInterval`은 **공격이 적을 때의 기본값**이고 상한은 코드에 박혀 있다.

대기 중에는 **입력이 잠기고 타이머도 멈춘다**(`DisableInput` + `StopTimer`). 대기가 끝나는 쪽에서 다시 열어주므로, 새 대기 구간을 추가할 땐 반드시 짝을 맞출 것.

스크립트 폴더는 시스템 단위로 나뉘고, 뷰는 각 시스템 아래 `UI/` 하위 폴더에 둔다(`DeckManager/UI/`, `Timer/UI/`, `WordChainManager/UI/`, `Combat/UI/`). 예외는 특정 시스템에 속하지 않는 화면 단위 UI인 `02_Scripts/UI/`(`TitleMenu`/`OptionsPanel`/`PauseManager`/`CardCollectionPanel`/`CardDeletePanel`/`MenuKeyboardNavigator`/`ResultInputHandler`/`ResultStatsView`/`ResultPanelView`/`ScreenPresentation`/`TextGateRevealAnimation`/`StageStartEffect`/`FadeInBackground`/`FlyInAnimation`/`WorldAnchoredUI`/`TMPCornerWarp`/`BossTitleCardView`/`YourTurnBanner`/`UIConfettiBurst`/`SpinnerLayout`/`MouseParallax`/`TextDropBounceEffect`/`TMPCharacterDropBounce`)와 최상위에 흩어져 있는 것들(`GameScenes.cs`·`LanguageSettings.cs`·`BattleManager`·`StageManager`·`EventManager`·`IntroManager`·`SoundManager`·`StatisticsManager`·`SpeechBubble(Manager)`·`HPBarUI`·`CameraShake`·`FloatingDamage*`·`FloatBob`·`SpriteOutlineUVSync`·`BackgroundScroller`·`MotherDragon`·`SceneWhiteFadeIn`·`UnscaledShaderTime`)이다.

- **`TMPCornerWarp`** (`02_Scripts/UI/`) — TMP 텍스트 각 글자의 **위쪽 두 꼭짓점만** `topSkewX`만큼 오른쪽으로 밀어 이탤릭처럼 기울이는 정적 효과(애니메이션 없음). `[ExecuteAlways]`라 Play를 누르지 않아도 씬 뷰에서 바로 보인다. 폰트에 이탤릭 웨이트가 없어도 기울일 수 있게 하는 용도다.
- **연출 컴포넌트들** (최근에 붙었고, 대부분 **붙이기만 하면 스스로 재생된다** — 켜는 쪽 코드를 안 건드리는 게 이 프로젝트의 패턴이다. `StageStartEffect`/`FadeInBackground`와 같은 결).
  - **`YourTurnBanner`** (`02_Scripts/UI/`) — 플레이어 입력이 열릴 때 화면 중앙에 잠깐 뜨는 `YOUR TURN!`**과 그 앞의 3-2-1 카운트다운.** **`DeckManager`가 `yourTurnBannerPrefab`을 들고 찍어낸다**(`GetYourTurnBanner`) — 턴이 열리는 시점을 아는 쪽이 부른다. ⚠️ 문구가 `message` 한 벌뿐이라 **언어를 안 탄다**(`StageManager.stageLabelFormat`과 같은 부류. 숫자는 언어를 안 타므로 카운트다운은 무관하다).
    - **둘이 같은 인스턴스를 쓴다.** 카운트다운용 오브젝트를 따로 두면 Input Canvas 탐색이 한 벌 더 늘고 둘의 위치·폰트가 어긋난다. 애니메이션도 `PlayRoutine(text, size, fadeIn, hold, fadeOut, animateSpacing)` 하나를 공유하며, **숫자는 `animateSpacing: false`**로 자간 연출을 끈다(여러 글자를 벌리는 연출이라 한 글자에는 의미가 없다).
    - **`PlayCountdownRoutine()`은 `StartCoroutine`을 하지 않고 `IEnumerator`를 돌려준다** — 부르는 쪽이 소유해야 중간에 끊을 수 있기 때문이다. `DeckManager.PlayTurnCountdown`이 그걸 직접 `MoveNext`로 돌리며 **숫자와 숫자 사이마다** 판이 끝났는지·대사창이 열렸는지 본다(⚠️ 마더 드래곤이 이 구간에서 대사창을 연다 — 안 보면 대사 위에서 숫자가 세어진다). 끊을 때는 `HideImmediate()`.
    - ⚠️ **숫자 하나가 끝날 때마다 `PlayRoutine`이 오브젝트를 끈다.** 그래서 반복문이 매번 `SetActive(true)`를 다시 한다 — 빼면 **두 번째 숫자부터 안 보인다.**
    - 적용 범위는 **턴 전환뿐**이다. 스테이지 첫 턴(`StageManager.BeginStageAfterDelay`)은 등장 배너가 그 역할을 하므로 `YOUR TURN!`만 뜬다.
  - **`BossTitleCardView`** (`02_Scripts/UI/`) — 보스 인트로 타이틀 카드의 순수 뷰. **`OnEnable`에서 스스로 문구를 채우므로 바깥에서 배선할 참조가 없고**, `StageManager`는 `bossTitleCardObject`를 켜고 끄기만 한다. 제목은 `LanguageSettings.Pick`을 타고 부제는 자식 `TMPCharacterDropBounce`가 떨어뜨린다.
  - **`UIConfettiBurst`** (`02_Scripts/UI/`) — 전체 클리어 축포. 씬에 오브젝트가 없고 **`ResultPanelView`가 `UIConfettiBurst.Play(rect, colors, sparkleSprite)` static으로 런타임에 만든다**(이전 인스턴스를 찾아 `Destroy`하므로 중복되지 않는다).
  - **`SceneWhiteFadeIn`** (`02_Scripts/`) — 흰 화면으로 덮으며 씬을 넘기는 전환. 마찬가지로 **`Play(sceneName, duration)` static이 캔버스를 통째로 코드에서 만든다**(`sortingOrder = short.MaxValue`). 쓰는 곳은 `IntroManager` → `GameScenes.Battle` 하나뿐이다.
  - **`HealEffectManager`** (`02_Scripts/Combat/`) — 가드·회복 시 몸 주변에서 떠오르는 스프라이트 파티클. `CombatManager`(플레이어 가드/힐)와 `EnemyManager`(적 가드)가 부른다. **싱글턴이다**(컨벤션 절 참조).
  - **`SpinnerLayout`** / **`MouseParallax`** (`02_Scripts/UI/`) — 타이틀 메뉴용. 전자는 `EventSystem.currentSelectedGameObject`를 보고 항목을 슬롯머신처럼 배치·페이드하는 **순수 뷰**라 ⚠️ **`items` 순서를 `MenuKeyboardNavigator.items`와 같게 넣어야** 포커스와 어긋나지 않는다. 후자는 `Mouse.current`를 직접 읽어(입력 컨벤션과 같은 이유) `localPosition`을 밀어낸다 — `FloatBob`처럼 **같은 오브젝트의 `localPosition`을 만지는 다른 스크립트와 충돌한다.**
  - **`UnscaledShaderTime`** (`02_Scripts/`) — `Shader.SetGlobalFloat("_GlobalUnscaledTime", …)`을 매 프레임 밀어 넣어 **일시정지 중에도 움직여야 하는 셰이더**(`WaveNoise` 등)를 살려둔다. 전역이라 **씬에 하나만** 두면 된다.
  - ⚠️ **`TextDropBounceEffect`와 `TMPCharacterDropBounce`는 거의 같은 일을 하는 두 벌이다**(TMP 정점을 직접 옮겨 글자를 떨어뜨린다). 실제로 참조되는 건 `TMPCharacterDropBounce`(`BossTitleCardView`가 자식들을 모아 재생) 쪽이고, `TextDropBounceEffect`는 `OnEnable` 자동 재생형이다. **새로 붙일 땐 어느 쪽을 쓰는지 확인할 것** — 둘 다 붙으면 같은 메시를 두 스크립트가 덮어쓴다.
- **`DisplacedUI` / `UIDisplacement`** (`02_Scripts/UI/DisplacedUI.cs`) — **창이 열려 있는 동안 가리는 UI를 인스펙터에 적은 만큼 밀어냈다 되돌리는 공용 장치.** `CardCollectionPanel`(보유 카드 목록)과 `CardDeletePanel`(지우기)이 같이 쓴다.
  - 쓰는 쪽은 `[SerializeField] DisplacedUI[] displacedUI` + `displaceDuration` 두 칸을 두고 넷만 부르면 된다 — `Awake`에서 `CaptureOrigins`, 여닫을 때 `MarkDirty`, `LateUpdate`에서 `Tick`, `OnValidate`에서 `MarkDirty`.
  - **`MonoBehaviour`가 아니라 평범한 클래스다.** 컴포넌트로 뽑으면 창마다 "어느 Displacer를 쓸지" 참조가 하나 더 생기고 씬 인스턴스 오버라이드만 늘어난다 — **인스펙터 배선을 늘리지 않으려는 것**이고 `LanguageSettings`를 static으로 둔 것과 같은 판단이다.
  - ⚠️ **`Tick`은 반드시 `LateUpdate`에서 부를 것.** `HandFanLayout`이 카드(자식)를 배치한 **뒤에** 줄 전체(부모)를 옮겨야 한다 — `Update`에서 부르면 같은 프레임에 카드 배치가 덮어쓴다.
  - ⚠️ **`useUnscaledTime`은 "그 창이 멈춘 화면 위에서 열리는가"로 정한다.** `CardCollectionPanel`은 `true`(일시정지 위에서 열린다), `CardDeletePanel`은 `false`(보상 구간이라 `timeScale`이 1이다). 컨벤션 절의 `deltaTime` 규칙과 같은 기준이고, 비슷해 보인다고 따라 쓰지 말 것.
  - ⚠️ **원래 자리는 `Awake`에서 한 번만 읽는다.** 여닫는 도중에 다시 읽으면 밀려나 있던 좌표가 "원래 자리"로 굳어 UI가 화면 밖에 남는다. 그래서 `Origin`/`Captured`가 `[NonSerialized]`다.
  - ⚠️ **필드 이름 `target`/`offset`/`displacedUI`를 바꾸지 말 것.** 씬에 저장된 값이 그 이름으로 직렬화되어 있어, 바꾸면 **경고 없이 빈 값**이 되어 아무것도 비켜나지 않는다(중첩 클래스에서 최상위로 옮길 때 이름을 그대로 둔 이유이기도 하다 — 덕분에 기존 배선이 살아남았다).
  - 목표에 닿으면 **좌표 쓰기를 멈춘다.** 계속 쓰면 다른 스크립트가 그 UI를 못 옮긴다.
- **`StageStartEffect`** (`02_Scripts/UI/`) — 스테이지 등장 배너의 등장·퇴장 연출. **등장은 `OnEnable`에서 자동 재생**(원래보다 커진 상태 + 투명에서 원래 크기로 축소되며 나타남)이라 `StageManager`는 `SetActive(true)`만 하면 되고, 퇴장은 `PlayExit(onComplete)`를 불러야 한다(축소 + 페이드아웃).
- **`FadeInBackground`** / **`FlyInAnimation`** (`02_Scripts/UI/`) — 각각 배경 페이드인(`duration`)과 날아 들어오는 등장 연출. 결과 창·타이틀에 붙어 있다.
- **`BackgroundScroller`** (`02_Scripts/`) — 배경 한 겹을 왼쪽으로 흘리고 `leftBound`를 넘으면 `loopJumpDistance`만큼 되돌려 무한 스크롤한다. `StartScroll()` / `StopScroll(duration)`. `StageManager.backgroundScrollers`에 씬의 겹들을 전부 연결해 두고, 보상 뒤 다음 스테이지로 넘어갈 때 `transitionDuration` 동안 달리는 연출을 만든다(새 적은 `spawnOffScreenX` 밖에서 `enemySlideInDuration` 동안 미끄러져 들어온다).
- **`FloatBob`** (`02_Scripts/`) — 붙은 오브젝트를 사인파로 위아래로 흔드는 12줄짜리 컴포넌트(`amplitude`/`speed`/`useUnscaledTime`). `Animator`가 없는 스프라이트·UI에 최소한의 생동감을 주는 용도이고, 지금은 씬 하나 + `Pause Panel.prefab` + `ResultPanel.prefab`에 붙어 있다. ⚠️ **`OnEnable`에서 `localPosition`을 원점으로 기억하므로, 다른 스크립트가 같은 오브젝트의 `localPosition`을 쓰면 서로 덮어쓴다.** 멈춘 화면 위(일시정지·결과)에 놓을 때만 `useUnscaledTime`을 켤 것.

### 난이도 — 3단계 (`DifficultySettings.cs`)

**`LanguageSettings`와 똑같은 static 클래스다** — 값 하나와 `PlayerPrefs`(`option.difficulty`, 볼륨·언어와 같은 계열)가 전부라 MonoBehaviour도 씬 오브젝트도 없고 **인스펙터 배선이 늘지 않는다.** `enum GameDifficulty { Easy, Normal, Hard }`, 기본값 **Normal = 지금까지의 밸런스 그대로**.

⭐ **핵심은 `Step`이다** — `Easy = -1 / Normal = 0 / Hard = +1`. **난이도별 수치를 여기 모아두지 않는다.** 각 값은 그것을 쓰는 매니저가 **"보통 기준값 + `Step` × 1단계분"**으로 계산하고, **1단계분만 자기 인스펙터에 든다.** 3벌씩 들면 `StageManager`에만 칸이 9개 생기고 곡선을 손볼 때마다 세 벌을 같이 고쳐야 한다.

| 축 | 어디에 | 1단계분 필드 | 쉬움 / 보통 / 어려움 |
|---|---|---|---|
| 타이머 | `TimerManager.BaseDuration` | `secondsLostPerDifficultyStep`(3) | 한국어 13/10/7 · 그 외 18/15/12 |
| 적 HP | `StageManager.DifficultyHPMultiplier` | `enemyHPPercentPerDifficultyStep`(20) | ×0.8 / ×1.0 / ×1.2 |
| 적 공격력·방어도 | `StageManager.LoadStage`가 넘기는 **증가율** | `enemyGrowthPercentPerDifficultyStep`(5) | 15% / 20% / 25% |
| 럭키 기본 확률 | `SkillResolver.LuckyChance` | `luckyChanceLostPerDifficultyStep`(10) | 30% / 20% / 10% |
| 시작 카드 | `WordUnlockManager.startingCardOverrides` | (id 목록) | 어려움만 `lucky` 제외 |

- **필드 이름이 방향을 말한다**(`...Lost...` / `...Percent...`). 인스펙터에 음수를 넣게 만들면 읽는 쪽이 부호를 두 번 뒤집어 생각해야 한다.
- **표시 이름은 `DifficultyLabels`**(`[Serializable]` 값 묶음, `DifficultySettings.cs`에 같이 있다)**를 쓰는 쪽이 필드로 든다** — `OptionsPanel.difficultyLabels`, `ResultStatsView.difficultyLabels`. ⚠️ **static 클래스에 박지 말 것** — 실제로 그렇게 만들었다가 인스펙터에서 못 고쳐서 되돌렸다(⭐ "화면에 나가는 글자는 예외 없이 인스펙터에서" 규칙).
- ⚠️ **전환은 타이틀에서만.** 런 도중에 바뀌면 이미 스폰된 적과 다음 적의 기준이 달라지고 시작 카드는 이미 지급된 뒤다(언어와 완전히 같은 이유). 옵션 창이 타이틀에만 있어 자동으로 성립하지만 **일시정지 메뉴에 붙이지 말 것.**
- ⚠️ **언어와 달리 순환하지 않는다** — `ChangeDifficulty`가 `Clamp`한다. HARD에서 한 번 더 눌러도 안 바뀌는 게 정상이다.
- ⚠️ **적 스케일링은 보스에 안 걸린다.** `LoadStage`의 `if (!isBossBattle)` 안쪽에만 얹혀 있어 마더 드래곤은 자동으로 빠진다(3턴 스파링이라 체력 공식을 태우면 아웃로가 깨진다).
- ⚠️ **럭키는 `LuckyChance` 한 곳에서 갈린다.** 카드의 `chancePercent`가 JSON에서 온 `readonly` 필드라 카드를 바꿀 수 없는데, 그 함수가 **굴리는 쪽과 표시하는 쪽(카드 수치 칸·보상 화면 라벨)이 모두 지나가는 단일 관문**이라 거기 한 줄이면 화면 숫자와 실제 확률이 같이 움직인다. **출발점만 옮기고 증가폭·상한은 건드리지 않는다.**
  - `SkillResolver`는 씬 컴포넌트인데 `LuckyChance`는 static이라, 인스펙터 값을 `Awake`에서 static 필드로 밀어 넣는다(`timerManager` → `_timer`와 같은 패턴).
- ⚠️ **시작 카드 제외는 카드 `id`로 지정한다**(`"lucky"`). `CardName`은 언어마다 달라지므로 이름으로 적으면 **한국어에서만 걸리고 나머지 네 언어에서 조용히 실패한다.** `CardLocalization.json`에 난이도를 넣지 말 것 — JSON은 "카드가 무엇인가"의 단일 출처이고 난이도는 그 위에 얹는 델타다.
  - 적용 후 **액션 카드가 0장이면 `LogError`**를 낸다. 액션 단어로만 체인이 완성되므로 전부 빠지면 런이 잠긴다.
  - 어려움에서도 **럭키는 보상 후보로는 그대로 나온다**(처음부터 안 줄 뿐 못 얻는 건 아니다).

### 언어 — 5개국어 (`LanguageSettings.cs`)

**`LanguageSettings`는 `static` 클래스다**(`GameScenes`와 같은 결). 값 하나와 `PlayerPrefs`, 그리고 아래 JSON 사전이 전부라 MonoBehaviour가 필요 없고, 씬 오브젝트도 `DontDestroyOnLoad`도 없으니 **인스펙터 배선이 늘지 않는다.** 새로 언어를 참조할 때 매니저를 만들어 끼우지 말 것.

**`enum GameLanguage { Korean, English, French, Spanish, Japanese }`** — 다섯이다(예전엔 한/영 둘이었다).

- API: `Current`(캐시된 값 — 타이핑 매칭 경로에서 글자마다 불리므로 `PlayerPrefs`를 매번 읽지 않는다) · `IsEnglish` · `Set` · **`ChangeLanguage(direction)`**(±1로 순환, 옛 `Toggle`은 없다) · `OnChanged` 이벤트 · **`Pick(korean, english, owner, fieldName)`** · **`PickCardText(cardId, textType)`**.
- 저장은 `PlayerPrefs`의 `option.language`(볼륨의 `option.volume.*`와 같은 계열). 기본값은 **한국어 고정**이며 시스템 언어를 따라가지 않는다 — 한글 IME 경로가 기본이 아닌 실행 환경이 생기면 확인할 경우의 수만 늘어난다.
- `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`로 실행마다 한 번 읽고, 같은 자리에서 JSON도 읽는다. static 필드는 Play를 멈춰도 남을 수 있어 초기화 지점을 명시해 둔 것이다.

⚠️ **전환은 타이틀에서만 해야 한다.** `CardName`이 곧 타이핑 매칭 키라서, 런 도중에 바꾸면 사전·손패·체인·쌓인 공격이 전부 바뀐 언어의 이름을 갖게 된다. 이 게임엔 저장이 없어 타이틀로 돌아가면 런이 초기화되므로(`StageManager.Start` → `GrantStartingWords`), **타이틀에서만 바꾸는 한 마이그레이션이 필요 없다.** 일시정지 메뉴에 언어 전환을 붙이지 말 것.

#### ⭐ 카드 텍스트의 단일 출처는 **`Assets/04_Data/Resources/CardLocalization.json`**이다

**카드는 에셋이 아니다.** `CardBase` 계열은 `ScriptableObject`가 아닌 평범한 C# 객체이고, `CardDatabase`가 JSON 한 행으로 만들어 준다. 이름·설명·수치 칸은 그 행의 `id`로 찾아온다.

```
CardName  => LanguageSettings.PickCardText(CardId, "name")
Description => …("desc")   StatsLabel => …("label")   럭키의 "보상됨" => …("labelPending")
```

- `Resources.Load<TextAsset>("CardLocalization")` → `JsonUtility` → `id → CardTranslation` 사전. **34장(카드 28 + 명령 6)이 5개 언어 × 이름/설명/라벨로 전부 채워져 있다.**
- ⚠️ **JSON에 없는 id를 쓰면 `CardName`이 `"MISSING_ID"`가 된다.** 그런 카드가 둘 이상이면 **이름이 전부 같아져 타이핑 매칭이 서로 겹친다.** 인스펙터의 id 칸(`PauseManager.resumeCardId` 등)은 `Awake`에서 `CardDatabase.Get<T>`로 꺼내며, 못 찾으면 **어느 오브젝트의 어느 칸인지**까지 경고에 찍힌다.
- ⚠️ **불어·스페인어·일본어에서 카드 <b>이름</b>은 무조건 영어 대문자다**(`UpperName(enName)`). 이름은 표시 문자열이자 **타이핑 매칭 키**라 라틴 입력으로 통일한 것이고, 설명·수치 칸만 각 언어로 번역된다. `frName`/`esName`/`jaName` 칸이 JSON에 있지만 **읽지 않는다.**
- ⚠️ **`UpperName`과 `InputManager.HandleTextInput`의 `ToUpperInvariant`는 반드시 같이 움직여야 한다.** 매칭 비교가 전부 Ordinal이라 한쪽만 바꾸면 손패·보상·명령 카드가 통째로 안 맞는다. (예전엔 양쪽 다 **소문자**였다 — 옛 문서를 보고 소문자로 되돌리지 말 것.)
- **`labelPending`은 수치 칸이 이분법으로 갈리는 카드용**이고 지금은 **럭키 하나뿐**이다(`보상` → `보상됨`). JSON에 그 칸이 없는 카드는 빈 값이 돌아오고 평소 라벨이 그대로 나온다. ⚠️ 이건 예전에 `ModifierCardData`의 `lootPendingStatsLabel`/`…En`으로 **에셋에 한/영 두 벌만** 있던 자리다 — 카드 라벨 중 유일하게 JSON을 안 타서 불/스/일 모드에서 럭키만 한국어로 튀었고, 그래서 JSON으로 옮겼다. **되돌리지 말 것.**
- 에셋 YAML에 `cardName`/`description*`/`statsLabel*` 같은 옛 키가 남아 있으면 그건 **아무도 안 읽는 고아 키**다(수치가 JSON과 어긋난 채 남아 있어 읽는 사람을 헷갈리게 한다). 34장 전부에서 이미 걷어냈다.

#### ⭐ 대사도 JSON이다 — **`Assets/04_Data/Resources/DialogueLocalization.json`**

마더 드래곤과의 대화 전체(등장 인사 → 턴별 대사 → 처치 후 → 엔딩)가 여기 있고 **5개국어를 다 든다.** 읽는 창구는 **`DialogueDatabase`**(`02_Scripts/DialogueDatabase.cs`)로, `CardDatabase`를 그대로 본뜬 static 지연 로드 클래스다.

- API: `Lines(id)` · `Line(id, index)` · `Count(id)`. 그 언어 칸이 **없으면**(null) 영어 → 한국어로 넘어가고, **빈 배열은 폴백하지 않는다**(그건 "대사 없음"이라는 정상 설정일 수 있다). ⚠️ **없는 id를 물으면 0줄 + 경고**다 — 진행은 막히지 않지만 콘솔이 시끄러워지므로, 쓰지 않을 id는 상수와 JSON 항목을 함께 지울 것.
- id는 코드에 문자열로 적지 말고 **`DialogueIds` 상수**를 쓴다(`GameScenes`와 같은 이유). 지금 **5개**: `DemiGreeting`/`MotherGreeting`/`MotherDragonTurn`/`DragonEvent`/`EndingEvent`.
- ⭐ **`DialogueDatabase`가 로드 시점에 언어별 줄 수가 다르면 `Debug.LogError`를 낸다.** 이건 장식이 아니라 **실제로 겪은 회귀를 막는 장치**다 — 대사는 스페이스로 한 줄씩 넘기는데 언어마다 줄 수가 다르면 그 언어에서만 넘기는 횟수가 어긋나 이벤트가 끝나지 않고, `ShowResult`가 안 불려 **결과 화면도 클리어 보상도 통째로 안 나온다.** 화면이 잠긴 것처럼 보이는데 원인이 번역 파일이라 추적이 매우 어렵다.
- `event.dragon`의 둘째 줄에는 `{0}`(회복량)이 들어간다 — `EventManager`가 `string.Format`을 태우므로 **5개국어 전부에 `{0}`이 살아 있어야 한다.**
- ⚠️ **옛 `event.normal`(일반 적 처치)은 삭제됐다.** 5개 언어가 전부 0줄이라, 말풍선을 하나 만들어 켰다 끄고 같은 프레임에 `EndEvent`로 빠지는 **통과 경로**였을 뿐이다. 실제로 하던 일은 `UpdateUI()` + `ShowResult(Victory)` 둘뿐이라 `BattleManager.FinishEnemyDeathAfterEffect`로 옮겼고, **일반 적 처치는 이제 `EventManager`를 아예 거치지 않는다.** 일반 적에게 대사를 주고 싶어지면 id를 다시 만들되 5개 언어 줄 수를 맞출 것 — **줄이 생기는 순간 스페이스로 넘기는 단계도 같이 생긴다.**

⚠️ **대사를 인스펙터 필드로 되돌리지 말 것.** 언어가 다섯이라 배열이 5벌씩 필요하고, 그러면 값이 프리팹 기본값과 씬 인스턴스 오버라이드로 흩어져 **화면에 실제로 나오는 값이 무엇인지 파일만 봐서는 알 수 없게 된다** — 실제로 엔딩 대사의 진짜 문구가 씬 오버라이드에 숨어 프리팹의 `플레이스홀더텍스트0`이 나오는 것처럼 보였다.

#### 카드 밖 문구 — 아직 `Pick`(한/영 두 벌)이다

⚠️ **`Pick`은 영어 자리가 비면 한국어로 폴백하되 조용히 넘어가지 않고 경고를 남긴다.** 영어 모드는 라틴 문자만 받으므로, 한글 이름이 그대로 손패에 뜨면 **그 카드는 영영 칠 수 없어 턴이 잠긴다.** 화면에 나오기 전에 알아야 하는 종류의 누락이라 일부러 시끄럽게 만들어 뒀다. 다만 `Pick`은 매칭 경로에서 글자마다 불리므로 **경고는 `owner.fieldName`별로 한 번만** 낸다(`SoundManager`가 버스 경고를 경로별로 한 번만 남기는 것과 같은 이유).

⚠️ **`Pick`은 두 벌뿐이라 불어·스페인어·일본어에서는 영어가 나온다** — "한국어 모드면 한국어, 그 외엔 영어"라서 다섯 언어가 실질적으로 둘로 접힌다. 즉 **카드는 5개국어, 그 밖의 문구는 한/영 2개국어**가 지금 상태다. 아래 표의 항목을 5개국어로 만들려면 카드처럼 JSON 쪽으로 옮기는 게 맞다.

⚠️ **`IsEnglish`는 "영어인가"이지 "라틴 입력 모드인가"도 "한국어가 아닌가"도 아니다.** 언어가 다섯이 되면서 셋이 갈렸다.

| 묻고 싶은 것 | 쓸 것 |
|---|---|
| 지금이 영어인가 | `IsEnglish` |
| 한국어면 A, 아니면 B | **`IsKorean`** |
| 라틴 문자를 **그대로** 받는 모드인가 | `Current != GameLanguage.Korean` (`InputManager.HandleTextInput`) — 한국어도 라틴 키를 받지만 그건 두벌식 자모로 조합된다(`UsesSyntheticHangul`) |

`IsEnglish`로 "한국어가 아닌가"를 물으면 **불/스/일이 한국어 쪽으로 떨어진다.** `CommandWordReceiver`의 안내 꼬리말이 그 예였고 지금은 `IsKorean`으로 고쳤다.

한/영 두 벌을 들고 있는 곳:

| 어디 | 필드 |
|---|---|
| 카드로 뜨는 명령 단어 | **JSON의 `type: "Command"` 행 6장**(5개국어). 쓰는 쪽은 id 문자열만 든다 — `PauseManager.resumeCardId`/`cardsCardId`/`titleCardId`, `RewardInputHandler.skipCardId`/`eraseCardId`, **`ResultInputHandler.retryCardId`/`titleCardId`**. `title`/`cards`는 일시정지와 결과 화면이 **같은 id를 공유한다**(같은 행동이라 단어를 새로 만들 이유가 없다) |
| 안내 문구가 붙는 명령 단어 | **`TypedCommand`** 한 덩어리(`korean`/`english` + `hintKorean`/`hintEnglish`). 지금은 **`CardCollectionPanel.closeCommand` 하나뿐**이다 — 결과 화면도 카드 줄로 바뀌면서 `ResultInputHandler`가 이 방식을 떠났다 |
| `CommandWordReceiver` | `hintSuffix`/`hintSuffixEn` — 안내 꼬리말(`을 입력해주세요!`) |
| 보상·목록 화면 제목 | **`ScreenPresentation`**(`titleKorean`/`titleEnglish` + 이미지 + 글자색). `RewardCardView.presentation`, `CardCollectionPanel`/`CardDeletePanel`의 `presentation`. ⚠️ **결과 화면은 이제 안 쓴다** — 아래 `BattleManager` 참조 |
| 결과 통계 라벨 | **`ResultStatsView`**의 라벨 6쌍(`highestStageLabelText`/`…En` 등) — `LanguageSettings.OnChanged`를 직접 구독해 갱신한다. ⚠️ **난이도 줄은 라벨만 번역되고 값(`EASY`/`NORMAL`/`HARD`)은 고정**이다 |
| `BattleManager` | ⚠️ 옛 `motherDragonLines`/`…En`(마더 드래곤 대사)은 **삭제됐다** — 위 대사 JSON(`DialogueIds.MotherDragonTurn`)으로 옮겼다. 옛 `statsFormat`/`statsFormatEn`(통계 문구 한 덩어리)도 **삭제됐다**(`ResultStatsView`가 라벨과 숫자를 따로 그린다) |
| `StageManager` | ⚠️ 옛 `demiGreetingLine`/`motherGreetingLine`(+`…En`)은 **삭제됐다** — 위 대사 JSON(`DemiGreeting`/`MotherGreeting`)으로 옮겼다 |
| `OptionsPanel` | `closeKorean`/`closeEnglish`, 그리고 불/스/일 3칸 — **이미 5개국어다**(`switch (LanguageSettings.Current)`) |
| `TitleMenu` | 버튼 라벨 3개 |
| `StatusEffectManager` | `GetDisplayName`(`화상`/`BURN` 등)과 `DevilDisplayName` — 코드에 직접 박혀 있다 |
| `EventManager` | ⚠️ 대사 배열 6개가 전부 **삭제됐다** — 위 대사 JSON(`DragonEvent`/`EndingEvent`)으로 옮겼고 5개국어가 된다 |
| `CardCollectionPanel` | `presentation`(제목) + `closeCommand`(`닫기`/`close`) |

⚠️ **두 벌을 들지 않아 언어를 안 타는 화면 문구가 아직 있다** — `StageManager`의 `stageLabelFormat`/`bossStageLabel`은 인스펙터로 나왔지만 **한 벌뿐**이고(스테이지 등장 배너는 아예 오브젝트가 문구를 갖는다), `IntroManager.slides`의 대사도 인스펙터에 있지만 **한 벌뿐**이다. 위 표에 없는 문구를 발견하면 새로 만든 게 아니라 이 부류일 가능성이 높다.

⚠️ 다만 **"아직 안 한 것"과 "의도적으로 안 하는 것"을 구분할 것.** 짧은 라틴 대문자 라벨은 다국어로 만들지 **않는 게** 이 게임의 화면 언어다 — `YourTurnBanner.message`(`YOUR TURN!`), `StageManager.stageLabelFormat`(`STAGE {0}`)·`bossStageLabel`(`MOMMY`), **`DifficultyLabels`(`EASY`/`NORMAL`/`HARD`)**, 그리고 불·스·일에서 카드 이름을 영어 대문자로 고정하는 규칙이 같은 부류다. 언어를 바꿔도 그 글자가 안 변하는 건 버그가 아니다.

⚠️ **하지만 "언어를 안 탄다"가 "코드에 박아도 된다"는 뜻은 아니다.** 위 넷 다 인스펙터(또는 JSON)에 있다. 난이도 이름을 `DifficultySettings`에 static으로 박았다가 **인스펙터에서 고칠 수가 없어 되돌린 적이 있다** — 언어를 안 타는 문구도 문구다.

⚠️ **`CommandWordReceiver.JoinHints`의 꼬리말만 `Pick`을 쓰지 않는다.** 꼬리말은 한국어 조사(`을 입력해주세요!`) 때문에 있는 것이라 **영어에서 비워두는 게 정상 설정**인데, `Pick`은 그걸 번역 누락으로 보고 한국어를 되돌리며 경고까지 낸다. 여기서는 빈 값이 곧 "꼬리말 없음"이다. 같은 성격의 필드를 추가할 때도 `Pick`에 넘기지 말 것.

⚠️ **범용 라벨 컴포넌트는 없다.** 한/영 문자열 두 개를 인스펙터에 두고 `OnChanged`를 구독해 알아서 갱신하는 `LocalizedLabel`이 있었지만, 어디에도 붙이지 않은 채로 남아 **삭제됐다.** 지금 언어에 반응하는 라벨은 각자(`TitleMenu.RefreshLabels`, `OptionsPanel`) 직접 구독해 갱신한다 — 그런 라벨이 늘어나면 그때 다시 만드는 게 맞다.

### 입력 파이프라인 (`02_Scripts/InputManager/`)

타이핑 입력은 Unity Input Action 에셋/바인딩을 완전히 우회하고 `Keyboard.current`를 직접 쓴다.

#### 우선순위 기반 단일 디스패치 — `TypingReceiver` / `TypingPriority`

**타이핑을 노리는 화면이 여섯이고(손패 · 보상 · 카드 삭제 · 결과 · 일시정지 · 보유 카드 목록), `InputManager`는 그중 딱 하나에게만 입력을 넘긴다.**

- **`TypingReceiver`** (추상 `MonoBehaviour`) — 하위 클래스가 채우는 건 넷뿐이다: `Priority`(가져가는 순서) · `WantsInput()`(지금 내 차례인가) · `Targets`(지금 노릴 단어들) · `OnCommandMatched(index, wasComposing)`. 선택으로 `HandleTypo()`.
  - `OnEnable`에서 인스펙터로 주입받은 `inputManager`에 **스스로 등록**하고 `OnDisable`에서 해제한다. **스스로 `OnCharacterEntered`/`OnCompositionChanged`를 구독하지 않는다.**
  - **매칭 파이프라인이 전부 여기 하나에 있다** — 커밋+조합 문자열 이어붙이기, 정확 일치 판정, `IsValidProgress` 진행 판정, IME 메아리 필터(`_pendingEcho`), 오타 1회 발화(`_notProgressing`). 하위 클래스는 "무엇을 노리는가"와 "맞혔을 때 무엇을 하는가"만 안다.
- **`TypingPriority`** (enum, 클수록 먼저) — `Battle = 0` · `Result = 10` · `Reward = 15` · **`RewardDelete = 18`** · `Pause = 20` · `CardCollection = 30`. **인스펙터에 노출하지 않는 코드 레벨 불변식이다.** `Reward > Result`인 게 곧 "보상을 정하기 전에는 `다음`이 먹지 않는다"는 게이트이고, `RewardDelete > Reward`가 "지우기 목록이 떠 있는 동안 후보 카드·`넘기기`가 먹지 않는다", `CardCollection > Pause`가 "목록이 떠 있는 동안 `계속`/`타이틀`이 먹지 않는다"는 게이트다 — 전부 별도 상태 플래그 없이 이 순서 하나로 성립한다. `RewardDelete < Pause`인 것도 의도다(지우기 중에도 ESC로 멈출 수 있어야 한다).
  - ⚠️ **타이핑 디스패치만 우선순위로 갈린다. `OnCancel`(ESC)은 구독자 전부에게 간다** — 그래서 `PauseManager.HandleCancel`이 "목록이 열려 있으면 목록만 닫고 리턴"을 손으로 갈라준다. ESC를 받는 화면을 더 만들면 같은 조정이 필요하다.
- **`InputManager.ActiveReceiver`** — 우선순위 내림차순으로 훑다가 처음으로 `isActiveAndEnabled && WantsInput()`인 곳에서 멈춘다. **`DispatchToReceiver`가 이걸 그대로 쓰므로 "누가 입력을 받는가"의 판정이 하나뿐이다.**
  - ⚠️ **캐시하지 않고 부를 때마다 훑는다.** 이 프로젝트엔 스크립트 실행 순서 설정이 없어서, 프레임 앞머리에 캐시해두면 "그 프레임에 일시정지가 걸렸는가"를 읽는 쪽마다 다르게 본다. 수신자는 여섯을 넘지 않고 `WantsInput()`은 전부 필드 검사 수준이라 값이 싸다.
  - ⚠️ **예전 구조로 되돌리지 말 것.** 넷이 전부 이벤트를 받아놓고 각자 `timeScale`·`IsGameOver`를 보며 비켜서던 시절에는, 일시정지 중 `계`의 첫 글자가 손패 쪽에서 오타 처리되어 `ClearInput()`이 불리는 바람에 **명령 단어를 끝까지 칠 수 없었다.** 하나만 고르면 그 종류의 사고가 구조적으로 일어나지 않는다.
  - `CardInputHandler.WantsInput()`에 아직 `timeScale == 0` 검사가 남아 있는데, 이건 **`PauseManager`가 씬에서 빠졌을 때의 보험**이다(그 경우 멈춘 화면에서 손패가 계속 먹는 것보다 아무도 안 먹는 쪽이 안전하다). 결과 화면 가드(`IsGameOver`)는 우선순위가 대신하므로 없앴다.

#### ⭐ 입력을 못 가져간 화면은 **연출도 멈춰야 한다**

우선순위 디스패치는 "**누가 글자를 받는가**"만 가른다. 문제는 **입력창(`CurrentInput`/`Composition`)이 전역이라 아무나 읽을 수 있다는 것**이다 — 그래서 일시정지 중 `계속`을 치면, 글자는 `PauseManager`만 받는데도 손패의 `가드`가 같이 떠오르고 보상 후보 카드가 덩달아 밝아졌다. 매칭은 안 되는데 화면만 반응하는 상태다.

**입력을 폴링해 연출을 그리는 코드는 예외 없이 "지금 내 차례인가"를 먼저 물어야 한다.** 묻는 방법이 둘이고, 부르는 쪽의 성격에 따라 갈린다.

| 누가 | 무엇을 쓰나 | 왜 |
|---|---|---|
| 수신자 자신(`RewardInputHandler`·`CardDeletePanel`) | **`TypingReceiver.HasTypingFocus`** | 자기가 `ActiveReceiver`인지만 알면 된다. `WantsInput()`이 true여도 더 높은 우선순위가 가져갔으면 false다 |
| 카드 한 장(`CardSlotView`) | **`InputManager.IsTypingTarget(word)`** | 이 카드는 **자기 주인을 모른다** — 손패로도, 일시정지 명령 카드로도, 결과 화면 명령 카드로도 쓰이기 때문이다. 그래서 "내 단어가 지금 대상 목록에 있는가"를 묻는다 |

- **`IsTypingTarget`이 `CardSlotView`에 특히 잘 맞는 이유**: 주인을 배선으로 들고 다니면 `HandFanLayout`·`PauseManager`·`ResultInputHandler`가 전부 `CardInputHandler` 참조를 넘겨야 하고 씬 인스턴스 오버라이드만 늘어난다. 단어로 물으면 **일시정지가 열리는 순간 손패 단어가 대상 목록에서 빠지므로 카드가 알아서 가라앉고, 같은 프레임에 명령 카드는 자기 단어가 들어와 평소처럼 떠오른다.** 배선이 하나도 안 늘어난다.
- ⚠️ **차례가 아닐 때는 "얼어붙는" 게 아니라 평상 상태로 되돌려야 한다.** 들림 목표를 0으로 두고(`CardSlotView`), 후보 없음 + 입력 없음으로 되돌린다(`RewardInputHandler` → `SetTypingCandidate(-1, false)`, `CardDeletePanel` → `_highlight = -1` + `_hasInput = false`). 중간 높이에서 멈춰 있으면 고장난 것처럼 보이고, `hasInput`을 남기면 카드가 흐려진 채로 굳는다.
- ⚠️ **`_active`/`_isOpen` 같은 자기 상태 플래그만으로는 부족하다.** 보상은 카드를 고를 때까지 계속 `_active`이고 삭제 창도 계속 `_isOpen`인데, 그 **위로** 일시정지가 열릴 수 있다(`Pause = 20 > RewardDelete = 18 > Reward = 15`). 플래그는 "내 창이 떠 있는가"이고 focus는 "내가 글자를 받는가"라 서로 다른 질문이다.
- `TypingReceiver.ActiveTargets`(internal)가 `Targets`를 `InputManager`에 열어주는 통로다. `Dispatch`와 같은 이유로 internal이며 **바깥에 공개하지 말 것** — 대상 목록을 밖에서 읽기 시작하면 "누가 무엇을 노리는가"가 다시 여러 곳으로 흩어진다.
- **`CommandWordReceiver` : `TypingReceiver`** — 명령 단어(고정 단어 + 화면 안내 문구)를 받는 수신자의 중간 계층. `hintSeparator`/`hintSuffix`/`hintSuffixEn`/`hintLabel`과 `JoinHints(...)`, `BuildHint()`, `RefreshHint()`를 더한다. `LanguageSettings.OnChanged`를 구독해 언어가 바뀌면 안내를 다시 쓴다.
  - **손패에는 안내 문구가 없어서 이 계층이 `TypingReceiver`보다 한 단계 아래에 있다** — 위로 올리면 `CardInputHandler` 인스펙터에 쓰지도 않는 안내 칸이 붙는다.
  - `hintLabel`을 비워두면 부르는 쪽이 `BuildHint()`를 문자열로 가져간다(결과 화면이 그렇게 쓴다 — `BattleManager.ApplyResult`가 제목 뒤에 붙인다).
- **`TypedCommand`** (`[Serializable]`) — 명령 단어 한 개의 한/영 두 벌 + 안내 문구 두 벌. `Word(owner, fieldName)` / `Hint(owner, fieldName)`.
  - **안내를 단어 옆에 두는 게 핵심이다.** 예전 `ResultInputHandler`는 `"...을 입력하세요"`와 `"...를 입력하세요"`를 별도 필드로 들고 있었는데 순전히 받침 때문이었다. 안내가 단어에 딸려 있으면 단어를 바꿔도 안내만 옛 상태로 남지 않는다.
  - ⚠️ **매개변수 없는 생성자를 명시해야 한다.** 기본값용 생성자를 정의하는 순간 컴파일러가 기본 생성자를 자동으로 만들어주지 않아 인스펙터에서 인스턴스가 안 만들어진다(`ScreenPresentation`도 같다).
- **오타 정책은 `TypingReceiver.HandleTypo`의 기본값 = "입력창을 비우지 않는다"다.** 잘못 친 글자를 곧바로 지우면 플레이어가 무엇을 틀렸는지 볼 수 없다. 화면에 남기고 백스페이스로 직접 지우게 하며, 길이 상한은 `maxInputLength`가 맡는다.
  - 그래서 **오타 뒤에는 이어 쳐도 매칭되지 않는다** — 매칭은 버퍼 전체와의 정확 일치라 앞의 잘못된 글자가 남아 있는 한 어떤 단어도 완성되지 않는다. 이건 버그가 아니라 설계다.
  - `HandleTypo`는 "어느 단어로도 이어지지 않게 된 순간" **한 번만** 불린다. 매 글자마다 부르면 로그와 연출이 폭주한다.

#### `InputManager`

- **`InputManager`** — `CurrentInput`(커밋된 문자), `Composition`(IME 조합 중 문자). `EnableInput()`/`DisableInput()`/`ClearInput()`으로 제어하고, 현재 상태는 `IsInputEnabled`로 읽는다(`PauseManager`가 멈추기 전 상태를 기억해 복원하는 데 쓴다).
  - **이벤트는 두 갈래로 성격이 다르다.**
    - `OnCharacterEntered(char)`/`OnCompositionChanged(string)`/`OnBackspace`/`OnInputCleared` — **입력창 상태를 그대로 비추는 뷰용**(`InputFieldDisplay`). 게임 판단을 여기서 하지 말 것.
    - `OnCancel`(ESC) / `OnAdvance`(스페이스) — `PauseManager` / `EventManager`가 받는다. **둘 다 `_inputEnabled` 가드보다 위에서 읽는다** — 턴 전환 대기처럼 타이핑이 잠긴 구간에서도 일시정지는 걸려야 하기 때문이다. 스페이스는 `HandleTextInput`이 어차피 버리는 문자라 타이핑과 충돌하지 않는다.
  - `ClearInput()`은 **수신자에게도 "비었다"를 디스패치한다.** 이게 없으면 오타 상태(`_notProgressing`)가 안 풀려 두 번째 오타부터 아무 반응이 없다. ⚠️ 반대로 **백스페이스는 일부러 디스패치하지 않는다** — 지우는 도중에 평가하면 `"펀치가"`에서 한 글자를 지운 순간 `"펀치"`가 매칭되어 카드가 의도치 않게 소비된다.
  - `LanguageSettings.OnChanged`를 구독해 언어가 바뀌면 `ClearInput()` + 쿨다운 무시 `ApplyImeMode()`를 한다.
  - **지금 언어의 글자만 받는다.** 한국어는 아래 합성 조합 경로로 빠지고, 나머지 언어는 `IsLatinLetter`가 아니면 즉시 리턴한다. **숫자와 공백은 어느 언어에서도 버린다** — 띄어쓰기 없이 이어 치는 게 게임 규칙이라 공백이 버퍼에 들어가면 매칭이 어긋난다.
    - 비한국어 모드에서는 받은 글자를 `char.ToUpperInvariant`로 올린다. **카드의 영문 이름도 `PickCardText`에서 대문자로 나오므로**, 여기서 한 번 올려두면 CapsLock/Shift와 무관하게 매칭되고 비교하는 쪽은 Ordinal 그대로 둘 수 있다. ⚠️ **이 둘은 반드시 같이 움직여야 한다.**
  - `maxInputLength`(기본 12)를 넘으면 **조용히 버린다.** 오타를 자동으로 지우지 않는 대신 길이를 제한하는 설계라, 여기서 버퍼를 비워버리면 그 취지가 무너진다 — 지우는 건 백스페이스의 몫이다.
  - **백스페이스에 키 반복이 있다**(`backspaceRepeatDelay` 0.4초 → `backspaceRepeatInterval` 0.05초). 한글 지우기는 **두 단계**다 — 조합 중인 마지막 음절은 **자모 하나씩**, 그게 다 지워지면 그 앞은 **음절 통째로**(아래 ⭐ "지우기는 두 단계다" 참조). OS 조합 중이면 IME에게 양보한다(`isComposing` 가드 — 지금 구조에서는 항상 꺼져 있다).
  - **글자가 들어오는 경로가 언어에 따라 다르다.** 한국어는 `Update`에서 **물리 키를 폴링**하고(`HandleHangulKeyPresses`), 나머지 언어는 `Keyboard.onTextInput` 문자 이벤트를 쓴다. 이유는 아래 ⭐ 참조.
  - ### ⭐ 한글은 **OS IME를 쓰지 않고 우리가 조합한다** — 플랫폼 공통

    **`DubeolsikHangulComposer`**(`02_Scripts/InputManager/`)가 들어온 **라틴 키를 표준 두벌식으로 조합해 음절을 만든다.** 조합기가 만든 문자열의 **마지막 한 글자를 `Composition`, 앞부분을 `CurrentInput`**으로 잘라 싣는다 — 그래야 매칭(`TypingReceiver`)·들림 판정(`CardSlotView`)이 옛 IME 경로와 똑같은 모양의 입력을 보게 되어 그쪽 코드를 하나도 안 고쳐도 된다.

    - **`InputManager.UsesSyntheticHangul`**(= `LanguageSettings.IsKorean`)이 "지금 이 경로인가"의 창구다.
    - ⭐ **왜 통일했나.** 예전엔 WebGL만 이 방식이고 데스크톱은 OS IME(한글 모드 강제)였는데, **두 방식이 요구하는 한/영 상태가 정반대라**(합성 조합은 영문, OS IME는 한글) 어느 쪽이든 어긋나면 입력이 통째로 죽었다. 웹은 브라우저 IME를 강제할 수단이 없어 더 심했다. 지금은 경로가 하나이고 IME를 **언제나 영문으로** 고정하므로 한/영이 어느 상태든 결과가 같다.
    - ⚠️ **`#if UNITY_WEBGL` 플랫폼 분기로 되돌리지 말 것.** 그러면 에디터 Play가 실제 플레이 경로를 한 줄도 밟지 않게 되어 웹에서만 나는 버그가 다시 생긴다.
    - ### ⭐ 한국어의 글자는 **문자 이벤트가 아니라 물리 키**로 받는다 — 그래야 한/영 상태를 안 탄다

      `Update`의 **`HandleHangulKeyPresses`**가 `Keyboard.current[Key.A..Key.Z]`를 폴링해 조합기에 넣는다. **OS IME가 한글 모드면 라틴 문자가 아예 오지 않기 때문이다** — IME가 키를 가로채 자기 조합에 써버리므로, 예전엔 **플레이어가 한/영으로 키보드를 영문에 맞춰두어야만 입력이 됐다**(영문 강제가 실패하거나 `imeForceCooldown` 안에 있는 동안 친 글자가 통째로 사라졌다). 키는 IME보다 아래(Raw Input)에서 읽히므로 한/영이 어느 쪽이든 결과가 같고, 이 프로젝트는 ESC·백스페이스·Ctrl에서 이미 그 경로에 의존하고 있다.

      - **두벌식은 애초에 자판 *위치*로 정의된 배열**이라 물리 키로 읽는 쪽이 오히려 정확하다. ⚠️ **비한국어 모드는 반대다** — 거기서는 AZERTY 같은 배열에서 글자가 어긋나므로 **문자 이벤트를 그대로 둘 것**(불어·스페인어도 카드 이름은 라틴 대문자다).
      - ⚠️ **IME 영문 강제(`ApplyImeMode`)를 같이 없애지 말 것.** 입력은 물리 키로 받지만, 강제를 그만두면 OS IME가 **자기 조합 오버레이를 화면에 겹쳐 그려** 유령 글자가 보인다. 입력은 폴링이, 화면을 깨끗하게 유지하는 건 강제가 맡는 이중 구조다.
      - ⚠️ **Ctrl·Alt가 눌린 프레임에는 아무 키도 받지 않는다.** 문자 이벤트는 조합키를 알아서 걸러 주지만 물리 키에는 그런 필터가 없어, 빼면 **Ctrl 시간 태우기 중에 누른 글자가 조합에 섞인다.**
      - ⚠️ **대문자는 쌍자음·이중모음이다**(`R`→ㄲ). Shift 상태를 직접 보므로 **CapsLock에 속지 않는다** — 옛 `NormalizeCapsLock`(onTextInput이 준 대문자를 되돌리던 보정)은 물리 키를 읽는 것만으로 필요 없어져 **삭제됐다.**
      - ⚠️ **한국어 모드에서 문자 이벤트로 들어온 라틴 문자는 그냥 버린다.** 방금 물리 키로 받은 그 키의 문자 이벤트라 두 번 들어간다. **시간 창으로 거를 수 없다** — 문자 이벤트가 같은 프레임의 `Update`보다 **먼저** 도착하므로 아예 받지 않는 게 맞다.
    - **`_syntheticBase`** — 조합이 시작될 때 이미 커밋되어 있던 글자. 조합기는 자기가 만든 글자만 알기 때문에, 아래 폴백으로 들어온 글자와 섞일 때 앞부분을 여기 따로 들고 있어야 한다. `maxInputLength`도 `base + 조합 결과`로 잰다. **완성된 앞쪽 음절도 여기로 옮겨진다**(바로 아래).
    - ### ⭐ 지우기는 두 단계다 — 조합기에는 **마지막 한 음절만** 남는다

      ```
      엉엉엉 → 엉엉어 → 엉엉ㅇ → 엉엉 → 엉 → (빈 입력)
      ```

      조합 중인 마지막 음절은 **자모 하나씩**, 그게 다 지워지면 그 앞은 **음절 통째로** 지워진다(OS IME와 같은 동작). 이게 성립하는 건 `AppendHangulKey`가 키를 넣을 때마다 **`DubeolsikHangulComposer.TakeCompletedSyllables()`로 앞쪽 완성 음절을 떼어 `_syntheticBase`(커밋된 글자)로 옮기기** 때문이다 — `HandleBackspace`는 예전 그대로 "조합기에 키가 있으면 한 개 빼고, 없으면 커밋된 글자를 한 글자 지운다"이고, **떼어내는 쪽만 바뀌었다.**

      - ⚠️ **전부 조합기에 쌓아두던 옛 방식으로 되돌리지 말 것** — 그러면 `엉엉엉`이 세 음절 모두 자모 단위로 분해되어 **아홉 번**을 눌러야 지워진다.
      - 떼어내도 안전한 이유: 뒤에 오는 키가 되돌아가 바꿀 수 있는 건 **마지막 음절뿐**이다(겹모음 ㅗ+ㅏ→ㅘ, 겹받침 ㄱ+ㅅ→ㄳ, 받침이 다음 음절 초성으로 넘어가는 규칙이 전부 그 안에서 끝난다). 조합 결과 문자열은 떼어내기 전과 **완전히 같다**(무작위 키 20만 회로 옛 구현과 대조해 확인했다).
    - **폴백**: 강제가 통하지 않아 이미 조합된 한글이 들어오면(웹 브라우저 IME) 영문으로 되돌리면서 **그 글자를 커밋된 글자로 받아둔다** — 아무것도 안 쳐지는 것보다 낫다. 다만 이 경로에서는 한 음절 단어가 커밋 전까지 매칭되지 않을 수 있다.
      - ⚠️ **`syntheticKeyEchoWindow`(0.5초) 안이면 버린다.** 물리 키를 방금 받았다면 그 한글은 같은 키를 IME가 뒤늦게 조합해 보낸 **메아리**라, 안 거르면 한 번 친 글자가 두 벌 들어간다. 반대로 물리 키가 전혀 안 들어오는 환경에서는 이 창이 항상 지나 있어 폴백이 정상 동작한다.
    - ⚠️ **백스페이스에서는 `DispatchToReceiver`를 부르지 않는다**(옛 WebGL 경로는 불렀다). 지우는 도중에 평가하면 `"펀치가"`에서 한 글자를 지운 순간 `"펀치"`가 매칭되어 카드가 소비된다.
  - **IME 상태를 이 클래스가 전부 소유한다.** 플레이어가 입력창을 클릭하거나 한/영을 누르는 준비 동작 없이 Play 직후 바로 타이핑되게 하는 게 목표다.
    - *조합 활성화*(Unity 소유): `EnableInput()`이 `Keyboard.current.SetIMEEnabled(true)` + `Input.imeCompositionMode = On`. ⚠️ **꺼두면 안 된다** — 창에 IME 컨텍스트가 붙지 않아 `ImmGetContext`가 0을 주고, **영문 강제 자체가 먹지 않는다.** `DisableInput()`은 `SetIMEEnabled(false)`로 되돌린다.
    - *변환 모드*(Windows IME 소유, 한글↔영문): Unity API로는 불가능해 `HangulImeMode`(IMM32)로 강제한다.
  - **`ApplyImeMode()`는 언제나 영문으로 맞춘다** — `HangulImeMode.SetAlphanumeric()`. 한국어를 포함해 어느 언어에서도 OS IME로 조합하지 않기 때문이다. ⚠️ **예전처럼 한국어에서 한글 모드를 강제하는 코드로 되돌리지 말 것** — 합성 조합기가 받아야 할 라틴 키를 IME가 먼저 가져가 한글 입력이 통째로 죽는다.
    - ⚠️ **합성 조합 중(`KeyCount > 0`)에는 `ClearComposition()`을 건너뛴다.** 그 `Composition`은 IME가 아니라 우리 조합기가 소유한 글자라, 지우면 조합기에는 남은 채 화면만 어긋난다.
  - **`HangulImeMode`** — `GetActiveWindow` → `ImmGetContext` → `ImmSetOpenStatus` + `ImmSetConversionStatus`. 공개 API는 `SetHangul(bool)` · `Force()`(한글 강제) · `SetAlphanumeric()`(영문 강제) · `CancelComposition()` 넷이지만 **지금 실제로 쓰는 건 `SetAlphanumeric`과 `CancelComposition`뿐**이다. `#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN` 가드, 그 외 플랫폼(WebGL 포함)은 no-op — **웹에서 폴백 경로가 필요한 이유가 이것이다.** 실패 로그는 **첫 번째 실패만** 남긴다.
    - IME를 켠 **뒤에** 불러야 한다. 그래서 `EnableInput()`은 코루틴(`BeginInputNextFrame`)으로 **1프레임 뒤** `CancelComposition()` + `ApplyImeMode()`를 한다.
    - 재적용 지점: `EnableInput()`(턴마다), `OnApplicationFocus(true)`(alt-tab 복귀), 그리고 **자가 복구 두 갈래** — ① `HandleTextInput`에 한글이 들어왔을 때, ② **조합 이벤트가 들어왔을 때**(`HandleCompositionChange`). 한/영 키는 Windows IME가 앱보다 먼저 처리해 차단할 수 없고, 이 자가 복구가 그 대체책이다.
    - ⚠️ **②가 반드시 있어야 한다.** 커밋 시점(`HandleTextInput`)에만 되돌리면 늦다 — 한글은 다음 글자를 칠 때까지 커밋되지 않아 그동안 조합이 남는다. 실제로 났던 버그다.
    - 자가 복구는 **글자만** 신호로 본다. 숫자·공백까지 포함하면 조합 중인 글자가 끊길 수 있다. 과호출 방지로 `imeForceCooldown`(기본 0.2초) 가드가 있다.
    - ⚠️ **`Composition`은 이제 합성 조합기 전용 칸이다** — `HandleCompositionChange`가 OS 조합을 어느 언어에서도 받지 않는다. 그래서 `IsCompositionStale`·`_lastBackspaceComposition`·`isComposing` 가드는 **정상 경로에서 도달하지 않는다.** 강제가 통하지 않는 환경을 위한 안전망으로 남겨둔 것이니 살아 있는 경로로 읽지 말 것.
    - 과거의 `ChangeHangul()`(RightAlt에서 `imeCompositionMode = On`)은 **삭제됐다.** 그건 한/영 토글이 아니라 조합 활성화를 늦게 켜주는 코드였고, "Alt를 눌러야 타이핑이 시작되는" 증상의 원인이었다. 되살리지 말 것.
  - `GetLeadConsonant(char)` / `IsValidProgress(committed, composing, target)` 정적 유틸을 제공한다. 후자는 "커밋된 문자열 + 조합 중인 글자가 target 단어를 향해 여전히 유효한가"를 판정하며, **매칭 로직(`TypingReceiver`)·손패 들림(`CardSlotView`)·보상 카드 들림(`RewardInputHandler`)이 같은 판정을 공유**하도록 하는 단일 기준점이다. 조합 중 글자는 표준 유니코드 한글 분해 공식으로 **초성만** 비교한다(모음 단계까지 검증하지 않는 의도적 절충).
  - **Enter는 쓰지 않는다.** 옛 `OnSubmit` 이벤트는 삭제됐다.
- **`InputFieldDisplay`** — 순수 뷰. `CurrentInput + Composition`을 `TextMeshProUGUI`에 미러링한다(라벨은 `GetComponent`가 아니라 인스펙터의 `text` 필드로 연결하므로 라벨과 다른 오브젝트에 붙어도 된다). **`TMP_InputField`가 아니라 그냥 라벨인 게 의도다** — 입력 필드는 선택될 때 `imeCompositionMode`를 `On`, 해제될 때 `Auto`로 되돌려 `InputManager`가 소유해야 할 IME 상태를 뺏어가고, 클릭 가능한 UI가 되어 "여길 눌러야 하나?" 하는 오해를 만든다. 예전엔 `TMP_InputField` + `readOnly = true` 우회를 썼는데, 클릭해서 활성화한 뒤 포커스가 빠지면 입력이 죽는 문제가 있어 라벨로 교체했다. **입력 필드로 되돌리지 말 것.**
  - `SetIMECursorPosition`으로 OS IME 오버레이(조합 중인 밑줄 글자) 위치를 텍스트 끝에 맞춘다. `WorldToScreenPoint`는 좌하단 원점, `SetIMECursorPosition`은 좌상단 원점이라 Y를 뒤집는 코드가 필요하다.

### 덱 / 카드 (`02_Scripts/DeckManager/`)

> 폴더명은 `DeckManager`(공백 없음)다. 예전엔 `Deck Manager`(공백 포함)였으나 이름이 바뀌었다 — 씬의 GameObject 이름은 여전히 `Deck Manager`(공백 포함)이니 혼동하지 말 것.

- **`Cards/CardBase.cs`** — 추상 `ScriptableObject`: `CardName`(타이핑할 단어 = 표시 텍스트 = 매칭 키), `Icon`, `Description`, `StatsLabel`, 추상 `Category`. `enum CardCategory { Modifier, Time, Type, Action, Command }`.
  - **`Command`는 조합에 들어가지 않는 명령 단어**(넘기기/지우기/계속/카드/타이틀)다. `CommandCardData`가 그 분류를 갖고, JSON에 `type: "Command"` 행이 6개 있다. 예전엔 명령 단어가 `CardBase`가 아니라 그냥 문자열이라 `CardView.SetText`로 이름만 그렸는데, **그래서 카드 분류 밖에 있었고 화면마다 다르게 그려질 참이었다.**
  - ⚠️ **`Command` 카드는 사전·해금 목록·조합에 들어가면 안 된다.** `CardBase`를 상속하니 인스펙터에서 실수로 꽂힐 수 있어 **세 곳이 각각 막는다** — `WordDictionary.AddInternal`(거부+경고) · `WordUnlockManager.OnValidate`(경고) · `WordChainManager.SubmitWord`(거부). 특히 `WordChainManager.IsCategoryFull`은 모르는 분류를 "안 참"으로 보므로 막지 않으면 **체인에 무제한으로 쌓인다.**
  - **`Description`도 `StatsLabel`처럼 `virtual`이다.** 확률처럼 인스펙터 값에서 나와야 하는 수치가 있는 카드는 하위 클래스가 재정의해 포맷 자리를 채운다(아래 "카드 설명의 확률 표시"). `CardBase.Fill(text, args)`이 공용 헬퍼이고, **포맷 자리(`{`)가 없으면 원문을 그대로 돌려준다.**
  - `Description`은 카드 하단 설명(`위력 +5, 시간 -1초`), `StatsLabel`은 카드 위쪽 큰 글씨 요약(`+5`/`화상`/`2회`)이다. 둘 다 `CardView`가 읽는다.
  - ⚠️ **표시 칸이 좁다.** `Description`은 200×50 / 20pt라 한 줄 약 10자·2줄, `StatsLabel`은 100×50 / **42pt**라 한글 실질 2자가 한계다(`+1초`는 `+`·`1`이 좁아 들어간다). 둘 다 `overflowMode: Overflow`라 넘치면 잘리는 게 아니라 **칸 밖으로 삐져나온다.**
  - `Icon`은 남아 있지만 **읽는 코드가 없다.** 프레임과 배지는 카테고리로 정해지므로 카드별 스프라이트를 쓰려면 `CardView`에 오버라이드를 새로 넣어야 한다.
  - **`Category`는 직렬화되지 않는 계산 프로퍼티다.** 그래서 enum 값을 바꿔도 `.asset` 마이그레이션이 필요 없다.
  - `AttributeCardData.Category`는 `effectType`에서 계산된다: `RepeatAction`→`Time`, `StatusChance*`/`Bleed`→`Type`, 나머지(`LifeDrain`/`DamageReduction`/`CritMultiplier`)→`Modifier`. 즉 GDD의 "속성 및 특수효과" 한 덩어리가 세 분류로 쪼개진다.
  - **`StatsLabel`은 `virtual`이다.** 값이 런 도중 변하는 카드가 재정의해 지금 수치를 끼워 넣는다 — `ModifierCardData`(어썸)와 `ActionCardData`(턴 스케일링) 둘이 그렇게 한다. 둘 다 JSON의 수치 칸(`koLabel`/`enLabel`/…)을 **포맷 문자열**로 쓰고, `{0}`이 없으면 적힌 그대로 돌려주는 폴백이 있다(포맷을 안 넣은 카드에서 `string.Format`이 예외를 내거나 글자가 통째로 사라지지 않게).
  - **턴 스케일링** — `TurnScalingSource`(`None`/`SecondsSpentThisTurn`/`ActionsThisTurn`/`ModifiersThisTurn`)는 **`CardBase.cs`에 있다. 액션 카드와 수식어 카드가 같이 쓰기 때문**이지 액션 전용이 아니다(예전 이름 `ActionScalingSource`).
    - **액션 쪽**(`ActionCardData`): `scalingSource` + `scalingPerUnit`. 위력은 `strengthBonus + ScalingBonus(...)` = **`CurrentStrengthBonus`** 하나로 모인다. **니킥**(위력 +10, 액션당 -1) · **촙**(액션당 +2) · **박치기**(수식어당 +2)가 여기다. 힘(power)이 기본으로 깔리고(펀치 = 힘+3과 같은 규칙) **파워 보정은 안 받는다**(액션 카드 자신의 값이라서).
    - **수식어 쪽**(`ModifierCardData`): `effectType = TurnScalingStatBonus` + `scalingSource`, 1단위당 값은 기존 `value`를 쓴다. **퍼펙트**(이번 턴에 **흘러간 초**당 +2, JSON `value: 2`)가 여기다. 수치 상승 단어이므로 어썸·슈퍼처럼 **파워 보정(+1/장)을 받는다.**
    - 두 경로 다 계산과 표시가 **`SkillResolver.ScalingBonus(source, perUnit)`** 하나를 거친다 — 카드에 뜬 숫자와 실제 피해가 어긋나지 않게 하려는 것이다.
    - ⚠️ **퍼펙트가 세는 초는 "이번 턴에 흘러간 시간" 전부다** — `SecondsSpentThisTurn`이 `TimerManager`에서 `Duration - RemainingTime`을 **라이브로 읽는다**(누적 상태가 없어 `ResetTurn`에서 비울 것도 없다). 자연 감소·훅/어퍼컷의 `TimerChange`·**Ctrl 시간 태우기**가 전부 여기 들어간다. 잽/퀵으로 시간을 늘리면 그만큼 줄어든다.
      - 옛 문서는 "`TimerChange`(단어 효과)만 센다"고 적혀 있었지만 **코드는 그런 적이 없다.** 카드 설명(`이번 턴에 흘러간 시간(초) 1당`)도 코드 쪽과 같다. Ctrl 홀드로 퍼펙트를 키우는 조작이 성립하는 것도 자연 감소분을 세기 때문이다.
    - ⚠️ **`TurnScalingSource`와 `ModifierEffectType`의 순서를 바꾸지 말 것** — `AttributeEffectType`과 같은 이유로 직렬화되는 건 인덱스다. 새 값은 **맨 뒤에만** 붙인다(`TurnScalingStatBonus`가 그렇게 들어갔다).
    - 표시는 부호를 붙여(`+0;-0;0`) 넣으므로 **포맷에 `+`를 적지 말 것** — 니킥과 퍼펙트는 음수까지 내려가 `"+-2"`가 된다. 어썸만 오르기만 해서 `"+{0}"`을 쓴다.
    - ⚠️ **퍼펙트는 이제 수식어라 `ModifiersThisTurn`에 스스로 잡힌다** — 퍼펙트를 쓴 조합 다음에 박치기를 치면 그만큼 위력이 오른다.
  - **enum 순서는 이제 안전하다.** JSON이 값을 **이름으로** 적으므로 순서를 바꾸거나 중간 값을 지워도 카드가 어긋나지 않는다. ⚠️ 예전엔 인덱스가 직렬화돼서 `Bleed`(페인풀, 미사용)를 지우면 스마트가 조용히 `RepeatAction`으로 바뀌었다 — **그 주의는 더 이상 유효하지 않다.** 다만 이름을 바꾸면 JSON도 같이 고쳐야 한다.
- **`WordDictionary`** — 플레이어가 *지금* 쓸 수 있는 단어. 직렬화 필드 없는 순수 런타임 상태. `Words`(읽기 전용 목록 — `CardCollectionPanel`이 이걸 그대로 격자로 펼친다)/`Contains`/`TryGetWord`/`GetRandomWord`/`AddWord`(한 장, 보상 확정용)/`AddWords`(배치)/`Clear`, `OnWordsChanged` 이벤트.
  - **슬롯 뽑기와 타이핑 검증이 둘 다 여기 하나만 바라본다.** 예전엔 같은 24장이 `CardSlotManager`와 `WordChainManager` 양쪽 인스펙터에 중복돼 있어 "슬롯엔 뜨는데 입력은 안 되는" 버그가 실제로 났었다. 이 단일 출처 구조를 깨지 말 것.
  - `GetRandomWord()`와 `GetRandomWord(CardBase exclude)` 두 오버로드가 있다. 후자는 거절 샘플링(다를 때까지 다시 뽑기) 대신 **인덱스를 건너뛰어 남은 n-1개에 균등하게** 뽑는다. 보유 단어가 하나뿐이거나 `exclude`가 사전에 없으면 평소대로 뽑는다 — 이 폴백이 없으면 단어가 하나일 때 슬롯이 null이 되어 카드가 사라진다.
  - `AddWords`(배치)는 이벤트를 마지막에 **한 번만** 쏜다. `OnWordsChanged`는 현재 구독자가 없지만(손패를 뽑는 시점은 `CardSlotManager`가 따로 정한다), 사전 UI 같은 게 붙을 때를 대비해 배치 단위로 유지한다.
- **`WordUnlockManager`** — 지급 로직. **단어 목록을 들고 있지 않다** — `CardDatabase.All`을 훑고 명령 카드만 걸러낸다. ⚠️ 옛 `allWords`(인스펙터 28행 + `WordEntry { card, grantedAtStart }`)는 **삭제됐다**: 프리팹 기본값과 씬 인스턴스 오버라이드에 시작 단어가 따로 저장돼 파일만 봐서는 실제 값을 알 수 없었다(실제로 프리팹은 잽·럭키, 씬은 가드·훅·럭키·펀치·슈퍼였다). 지금 시작 단어의 유일한 출처는 **JSON의 `grantedAtStart`**다.
  - `GrantStartingWords()` — 런 시작. 사전을 비우고 `grantedAtStart` 전부 지급한 뒤 **난이도 보정(`ApplyDifficultyOverride`)을 얹는다.** 럭키 라운드 누적도 여기서 비운다(지난 런이 이월되지 않게).
    - 보정은 `startingCardOverrides`(난이도별 `removeIds`/`addIds`)이고 지금 설정은 **어려움에서 `lucky` 제외** 하나뿐이다. ⚠️ **카드 `id`로 적는다** — `CardName`은 언어마다 달라 이름으로 적으면 한국어에서만 걸린다. 액션 카드가 0장이 되면 `LogError`가 난다(런이 잠긴다).
  - ⚠️ **뽑기와 확정이 두 메서드로 갈려 있다.** `RollRewardCandidates()`가 미보유 중 랜덤 `wordsPerReward`개(기본 3)를 **뽑기만 하고 사전에는 넣지 않으며**, 플레이어가 고른 한 장을 `ConfirmReward(card)`가 넣는다. 옛 `GrantStageClearReward()`는 둘을 한 메서드에서 같이 해서 "뽑았지만 아직 확정 안 함"이라는 상태가 없었고, 그래서 선택제가 성립하지 않았다. **다시 합치지 말 것.**
  - 후보 목록(`_offered`)은 시작 단어용 재사용 리스트(`_granted`)와 **따로 둔다** — 선택제에서는 플레이어가 고를 때까지 여러 프레임에 걸쳐 들고 있어야 한다.
  - **럭키는 이제 확률로 걸린다** — 기본 `chancePercent`에서 시작해 **쓸 때마다** `chanceGainPerUse`씩 올라 `maxChancePercent`에서 멈춘다. 셋 다 `CardLocalization.json`의 럭키 행에 있다.
    - ⚠️ **난이도가 출발점을 옮긴다** — `LuckyChance` 안에서 `luckyChanceLostPerDifficultyStep`만큼 더하고 뺀다(쉬움 +10 / 어려움 -10). **증가폭과 상한은 건드리지 않는다** — "쓸수록 오른다"는 규칙 자체는 난이도와 무관하다.
    - 누적은 `SkillResolver.LuckyUses`(static, **스테이지 단위** — `ResetStage`가 되돌리므로 스테이지가 바뀌면 기본 확률로 돌아간다. ⚠️ 런 단위인 어썸과 범위가 다르다)이고, 확률 계산과 카드 표시가 **`SkillResolver.LuckyChance(base, gain, max)` 하나를 같이 쓴다** — 카드에 뜬 숫자와 실제 확률이 어긋나면 그게 곧 버그로 보인다(`ScalingBonus`와 같은 규칙).
    - ⚠️ **성공했는지가 아니라 "썼는지"로 센다.** 실패해도 다음 확률은 올라가야 한다 — 안 그러면 운이 나쁠수록 계속 나빠진다. 증가 시점은 `Resolve` 맨 끝이라 **첫 사용은 기본 확률로 굴린다**(어썸이 "이전에 성공한 횟수"만 세는 것과 같다).
    - ⚠️ 굴리기는 카드를 다 훑은 **뒤에 한 번만** 한다. 손패 중복으로 럭키가 두 장 들어와도 체인당 보상 라운드는 하나다.
  - **판정에 성공하면 그 조합이 적용되는 순간 곧바로** `AddLuckyBonus()`가 `luckyBonusRounds`(기본 1)만큼 보상 라운드를 쌓는다(장수를 늘리는 게 아니다 — 선택제에서 후보만 늘리면 고를 수 있는 건 여전히 하나라 오히려 보상이 줄어 보인다). 쌓인 라운드는 스테이지를 클리어할 때 `StageManager`가 `TryConsumeBonusRound()`로 하나씩 꺼내 쓴다. 부르는 쪽은 `DeckManager.PlayPendingActions`다.
    - ⚠️ **"그 공격이 처치했는지"는 보지 않는다.** 럭키는 *쓰면 확률이 오르고, 성공하면 그 스테이지를 클리어할 때 보상이 한 번 더 열리는* 카드다. 예전엔 여기서 처치 여부를 같이 봐서 **판정에 성공해도 그 콤보가 마지막 일격이 아니면 아무 일도 일어나지 않았다**(옛 필드 이름 `LootBonusOnKill`이 규칙을 잘못 말하고 있었고, 지금은 `GrantsLootBonus`다). "이후 적을 처치했을 때 보상이 추가된다"는 규칙은 **클리어해야 보상 창이 열린다**는 사실만으로 이미 성립한다.
    - 수치 칸이 `보상` → `보상됨`으로 바뀌는 건 **이 쌓인 상태를 보여주려고** 있는 것이다. 처치 시점에 쌓으면 그 표시가 보일 틈이 거의 없다.
  - `OnValidate`가 `allWords`의 **`CardName` 중복을 경고한다.** 이름이 곧 매칭 키라 중복되면 하나는 영영 입력할 수 없다.
- **`RewardInputHandler` : `CommandWordReceiver`** (`DeckManager/`) — 클리어 보상 줄에서 **카드 하나를 타이핑으로 고르는** 수신자. `TypingPriority.Reward`.

  ```
  [지우기]  [후보1] [후보2] [후보3]  [넘기기]   ← 마더 드래곤 스테이지
            [후보1] [후보2] [후보3]  [넘기기]   ← 일반 스테이지
  ```

  - **후보든 명령이든 전부 `CardBase`라 매칭 키가 `CardName` 하나로 통일된다.** 넘기기·지우기는 `CommandCardData` 에셋(`skipCard`/`eraseCard`)이고, 안내 문구가 아니라 **카드로 화면에 놓인다.**
  - ⚠️ **`_targets`의 순서 = 화면에 놓이는 카드 순서**다(`_row` 하나를 매칭과 표시가 같이 본다). 그래야 `SetTypingCandidate`에 넘기는 인덱스가 그대로 들어맞는다. 경계는 `EraseIndex`(없으면 -1)·`FirstCandidateIndex`·`SkipIndex` 계산 프로퍼티로 둔다 — **예전의 "마지막 칸이 스킵"식 계산은 왼쪽 카드가 붙는 순간 깨진다.**
  - **지우기 카드는 조건 넷이 다 맞을 때만 놓인다** — 마더 드래곤 스테이지 + `eraseCard`·`cardDeletePanel`·`wordDictionary` 배선 + `Words.Count > minWordsToKeep`(기본 4). 하나라도 없으면 줄에서 아예 빠진다(쳐도 아무 일이 안 일어나는 카드를 띄우지 않는다).
  - `BeginSelection(candidates, allowErase)`로 한 라운드를 열고 **`rewardCardView.Show(_row)`까지 여기서 부른다** — 단어를 가진 쪽이 화면 순서까지 정해야 매칭 인덱스와 카드 위치가 어긋나지 않는다(`StageManager`는 더 이상 `Show`를 부르지 않는다. 폴백 경로만 예외).
  - 고르거나 넘기거나 **지우면** `OnSelectionFinished`를 쏜다(`StageManager`가 받아 럭키 라운드가 남았는지 본다). `IsSelecting`은 `BattleManager`가 결과 화면 안내를 띄울지 판단하는 데 쓴다.
  - ⚠️ **지우기를 골랐을 때는 `EndSelection`을 부르지 않는다.** `cardDeletePanel.Open()`만 하고 `_active`를 내린 뒤, 삭제가 끝나 `OnDeleteFinished`가 오면 그때 끝낸다 — 여기서 끝내면 아직 지우지도 않았는데 다음 스테이지로 넘어간다.
  - `Update`에서 `IsValidProgress`로 지금 치고 있는 카드를 골라 `rewardCardView.SetTypingCandidate(...)`에 넘긴다 — **판단은 여기서, 표시는 뷰에서**.
- **`UI/CardDeletePanel` : `CommandWordReceiver`** (`02_Scripts/UI/`) — `지우기`로 열리는 **사전 카드 삭제 창**. 지금 사전에 든 카드를 격자로 펼치고, 이름을 친 한 장을 `WordDictionary.RemoveWord`로 지운 뒤 `OnDeleteFinished`를 쏜다. `TypingPriority.RewardDelete`.
  - **취소가 없다** — 지우기를 고른 순간 반드시 한 장을 지운다. 그래서 명령 단어가 하나도 없고 타이핑 대상은 보유 카드 이름뿐이다. 사전이 비어 있으면 창을 열지 않고 곧바로 `OnDeleteFinished`로 빠진다(취소가 없는 창이라 열리면 갇힌다).
  - **가리는 UI를 비켜나게 하는 `displacedUI`/`displaceDuration`이 여기에도 있다** — 이 창은 보상 카드 줄 **위에** 겹쳐 뜨므로 그대로 두면 뒤에 비친다. 로직은 `CardCollectionPanel`과 같은 공용 `UIDisplacement`를 쓴다(위 참조).
  - `CardCollectionPanel`과 격자 배치가 겹치지만 **일시정지 경로를 건드리지 않으려고 따로 뒀다.** 비켜나기는 두 번째 중복이 되면서 실제로 공용(`UIDisplacement`)으로 뽑았고, **격자 배치도 또 갈라지면 같이 뽑는 게 맞다.**
  - ⚠️ **`Time.deltaTime`을 쓴다**(비켜나기에도 `useUnscaledTime: false`를 넘긴다) — 이 창은 `timeScale`이 1인 보상 구간에서만 뜬다. `CardCollectionPanel`이 `unscaledDeltaTime`인 건 그쪽이 일시정지 **위에서** 열리기 때문이고 성격이 다르다.
  - ⚠️ `PauseManager`·`CardCollectionPanel`과 같은 이유로 **항상 켜져 있는 오브젝트에 붙일 것.** 여닫는 건 인스펙터로 받은 `panel`이다.
  - **`UI/RewardCardView`** (`DeckManager/UI/`) — 후보 카드를 화면 가운데에 펼치는 **순수 뷰**. 카드 중심 간격은 **프리팹 폭 + `spacing`**이라, `Card.prefab`(100px)에 `spacing 100`이면 사이가 실제로 100 벌어진다. `Awake`에서 프리팹 폭을 읽어두므로 카드 크기를 바꿔도 여백은 유지된다.
    - **게임 판단을 하지 않는다.** "어느 카드를 치고 있는가"는 `RewardInputHandler`가 `SetTypingCandidate(index, hasInput)`로 알려주고, 여기는 그걸 들림(`typingLiftHeight`)과 흐림(`dimAlpha`)으로 비추기만 한다. `hasInput`을 따로 받는 이유는 **"아직 안 쳤다"(전부 선명)와 "쳤는데 아무것도 안 맞는다"(전부 흐림)를 갈라야** 하기 때문이다.
    - 제목과 이미지는 `ScreenPresentation presentation` 필드에서 나온다(결과 화면과 같은 구조).
    - **`PlayExit(keepIndex, onComplete)`가 선택이 끝났을 때의 퇴장 연출이다** — `keepIndex` 카드(고른 카드)만 그 자리에 `exitKeepLinger`만큼 남고, 나머지는 패배 시 손패가 무너지는 것과 같은 방식으로 시차(`exitFallStagger`)를 두고 회전하며 떨어진다(`exitFall*` 인스펙터 값들). `reveals`(제목·배경 게이트)는 **`PlayReverse()`로 열릴 때와 반대로 닫힌다.** 다 끝나면 `onComplete`가 다음 스테이지 진행을 잇는다.
      - `keepIndex`는 `RewardInputHandler.LastPickedRowIndex`에서 오고, **넘기기·지우기처럼 남길 카드가 없으면 -1**이라 전부 떨어진다. 표시된 카드가 아예 없던 라운드면 연출 없이 곧바로 `onComplete`.
      - ⚠️ **`Clear()`는 `StopAllCoroutines()`부터 한다.** 퇴장 연출 도중 플레이어가 앞질러 넘어가면(`StageManager.LoadStage`) 낙하 코루틴이 살아남아 이미 `Destroy`된 카드를 건드리거나, **`onComplete`가 한 번 더 불려 스테이지를 건너뛴다.**
    - **카드 내용은 `CardView.SetCard` 하나로 그린다.** 예전엔 `GetComponentInChildren<TMP_Text>()`/`<Image>()`로 계층 **첫 번째** 컴포넌트를 집어 이름과 아이콘을 직접 넣었는데, 자식 순서에 의존하는 구조라 `Badge`를 추가하는 순간 깨질 참이었다. 그 방식으로 되돌리지 말 것.
    - `CardSlotView`는 꺼서 손패 이벤트에 반응하지 않게 한다. 그리기는 `CardView`가 따로 하므로 꺼도 카드 내용은 정상적으로 나온다.
    - `localRotation`을 초기화하는 줄이 있는데 **지금은 사실상 방어용이다.** `Card.prefab` 루트 회전은 항등이고, 기울기는 `NameText` 자식에 7도가 따로 박혀 있다(아트 의도라 그대로 둔다). 손패에서 카드가 기우는 건 `HandFanLayout`이 매 프레임 루트를 돌리기 때문이다.
  - **시작 단어는 현재 5장이다: 가드 · 훅 · 럭키 · 펀치 · 슈퍼**(어려움에서는 럭키가 빠져 4장). 바꾸려면 `CardLocalization.json`에서 그 카드 행의 `"grantedAtStart": true`를 켜고 끄면 된다 — 씬도 프리팹도 건드리지 않는다. **난이도별로 다르게 하고 싶으면** JSON이 아니라 `WordUnlockManager.startingCardOverrides`를 쓴다(위 참조).
  - 액션 단어(`Category == Action`)가 시작 목록에 최소 하나는 있어야 한다. 액션 단어로만 체인이 완성되므로, 전부 빼면 **어떤 조합도 완성할 수 없어 공격이 영원히 불가능해진다.**
- **`CardSlotManager`** — 5슬롯(`CurrentCards`/`SlotCount`/`OnSlotChanged`/`ConsumeSlot`/`RefillAll`). 사전에서 균등 랜덤으로 뽑으며 **슬롯 간 중복은 의도된 동작**(중복 방지 버전을 만들었다가 요청으로 되돌린 이력이 있으니 확인 없이 "고치지" 말 것).
  - **단, 한 슬롯이 직전에 들고 있던 카드는 제외한다.** `FillSlot(index, excludeCurrent)`의 플래그로 갈리며, `ConsumeSlot`은 `true`(타이핑으로 방금 쓴 카드가 같은 자리에 곧바로 다시 오지 않게), `RefillAll`은 `false`(완전 랜덤)를 넘긴다. 제외 자체는 `WordDictionary.GetRandomWord(exclude)`가 담당한다. **슬롯 간 중복과 혼동하지 말 것** — 막는 건 *한 칸의 연속 재등장*뿐이다.
  - `ConsumeSlot`(한 칸 보충)과 `RefillAll`(손패 통째로 교체)은 쓰임이 다르다. 사전이 비어 있으면 `RefillAll`은 들고 있던 카드를 null로 지워버리므로 아예 손대지 않고 경고만 남긴다.
  - **`EmptyAllSlots()`는 다시 뽑지 않고 5칸을 비우기만 한다.** 부르는 곳은 **`StageManager.LoadStage`의 즉시 정리 구간 하나뿐**이다 — 손패가 새로 뽑히는 건 `stageStartDelay`(2초) 뒤 `RefillAll()`이라, 비우지 않으면 **새 적이 등장하는 그 2초 동안 이전 스테이지의 카드가 그대로 남아 있다.** `NextStage()`·`RestartStage()`가 둘 다 `LoadStage`를 거치므로 한 곳으로 충분하다.
    - `OnSlotChanged(i, null)` → `CardSlotView.PlaySwap(null)` → `CardView.SetCard(null)` 경로를 타서 **빈 카드 5장**이 된다(프레임은 남고 이름·설명·수치가 빈다). `_liftTargetWord`가 `null`이 되므로 **빈 카드는 타이핑에 반응하지 않는다.**
    - ⚠️ **패배 연출(`CardSlotView.PlayCollapse`)과 섞지 말 것.** 두 순간은 성격이 다르므로 일부러 독립적으로 뒀다 — 한 메서드로 묶으면 패배 연출을 바꿀 때 스테이지 전환까지 딸려 바뀐다. 반대로 `PlayCollapse`가 도는 중에 `OnSlotChanged`가 나가면 그 코루틴이 끊기므로, **패배 시점에는 슬롯 데이터를 건드리지 않는다**(`DeckManager` 쪽 주석 참조).
  - **`RefillAll`을 부르는 곳은 두 군데다**: `StageManager.BeginStageAfterDelay`(스테이지 시작)와 `DeckManager.RunTurnTransition`(**턴이 바뀔 때마다**). 후자는 쌓인 공격 재생이 끝난 직후, 적 턴 대기 전에 돈다.
  - **손패가 채워지는 경로는 `ConsumeSlot`/`RefillAll` 둘뿐이다.** `Awake`는 배열만 잡고 채우지 않으며, 사전이 채워질 때 자동으로 뽑지도 않는다. 예전엔 `WordDictionary.OnWordsChanged`를 구독해 빈 슬롯을 채웠는데, 그러면 게임 시작 시 `GrantStartingWords()` 시점에 손패가 먼저 나왔다가 `stageStartDelay` 뒤 `RefillAll()`이 **다시 뽑아** 눈에 보이는 리롤이 생긴다. 그 구독을 되살리지 말 것.
- **`CardInputHandler` : `TypingReceiver`** — 손패 매칭. `TypingPriority.Battle`(최하위 폴백). **파이프라인은 전부 베이스에 있고**(위 입력 파이프라인 절), 여기 남은 건 `Targets`(손패 5장의 `CardName`, 빈 슬롯은 빈 문자열) · `OnCommandMatched`(효과음 → 통계 → `MainBufferManager` → `WordChainManager.SubmitWord` → `ConsumeSlot`) · `HandleTypo`(`MainBufferManager.ClearBuffer` + `OnTypo`)뿐이다.
  - `Targets`는 **글자마다 불리므로 캐시한 배열을 채워서 돌려준다.** 매번 새 목록을 만들지 말 것(같은 규칙이 `PauseManager`/`ResultInputHandler`/`RewardInputHandler`에도 적용된다).
  - ⚠️ **오타가 나도 체인(`WordChainManager`)은 지우지 않는다.** GDD의 오타 페널티는 적용하지 않기로 한 결정이다. 지우는 건 `MainBufferManager`뿐.
  - (베이스에 있는 것들 — 되돌리지 말 것) 조합 중에도 평가하는 이유: 퀵/잽/훅 같은 **한 음절 단어는 뒤에 이어질 음절이 없어 IME가 영원히 커밋하지 않는다.** 커밋 경로에서 조합 문자열을 빈 문자열로 넘기는 이유: 커밋 순간 `Composition`이 아직 옛 값을 들고 있어 "펀펀"처럼 중복된다. `_pendingEcho`: 조합 중 매칭으로 슬롯을 소비하면 OS IME는 그 글자를 아직 붙잡고 있어 다음 입력 때 뒤늦게 커밋되어 돌아온다 — 그 메아리를 한 번만 걸러낸다. **IME 설정을 건드려 해결하려 하지 말 것.**
- **`MainBufferManager`** — 매칭된 카드의 단순 목록. 화면 표시/디버그용이며 `WordChainManager`와 별개로 유지된다.
- **`HandFanLayout`** (`04_UI/Card Canvas/Hand`) — `Card.prefab`을 `SlotCount`만큼 생성하고 `CardSlotView.Bind(manager, i, inputManager)` 호출 후, `LateUpdate`에서 부채꼴 배치. `[ExecuteAlways]`라 생성은 `Application.isPlaying`으로 가드된다.
  - **위치를 쓰는 건 여기 하나뿐이다.** `CardSlotView`는 `VerticalOffset`(떠 있어야 할 높이)만 계산해 들고 있고, `HandFanLayout`이 부채꼴 목표에 더한다. `CardSlotView`가 자기 `anchoredPosition`을 직접 만지면 같은 프레임에 두 스크립트가 경쟁한다.
  - `CollectChildren()`이 `_children`과 `_cards`를 같은 루프에서 나란히 재수집한다 — `centerOnTop`이 형제 순서를 바꾸므로 스폰 순서로 고정해두면 어긋난다.
- **`UI/CardView`** — **카드 한 장의 겉모습만** 담당하는 순수 뷰(이름·설명·`StatsLabel`·카테고리 프레임·효과 배지). 공개 API는 `SetCard(CardBase)` · `SetAlpha(float)` **둘뿐**이고 **매니저 참조가 하나도 없다.** ⚠️ 옛 `SetText(string)`은 **삭제됐다** — 명령 단어도 이제 `CommandCardData` 에셋이라 손패·보상·일시정지가 전부 `SetCard` 하나로 그려진다. 그 메서드로 되돌리지 말 것.
  - 이 분리가 핵심이다. 손패(`CardSlotView`)와 보상 화면(`RewardCardView`)이 같은 프리팹을 쓰는데 보상 카드에는 슬롯도 타이핑도 없어서, 예전엔 `CardSlotView`를 통째로 끄고 이름·아이콘을 **따로 다시 그려야 했다.** 그리기 규칙이 두 곳에 중복되지 않게 유지할 것.
  - **프레임**: `Action`→`actionFrame`(분홍), **`Command`→`commandFrame`**(회색, 비어 있으면 `defaultFrame`으로 폴백), 나머지 전부 `defaultFrame`(남색).
  - **배지**: `AttributeEffectType.RepeatAction`(더블/트리플)→`multiplyBadge`, `Category == Action`→`actionBadge`, **`Category == Command`→없음(null)**, 그 외 전부→`upBadge`. `Category == Time`을 보지 않고 `RepeatAction`을 직접 보는 건, 배지가 "무슨 효과인가"의 표현이지 조합 규칙상의 분류가 아니기 때문이다(지금은 둘이 1:1이다).
    - ⚠️ 예전의 **"배지가 없는 카드는 없다"는 규칙은 깨졌다** — 명령 카드는 "무슨 효과인가"가 없어서 배지를 달지 않는다. `BadgeFor`가 null을 돌려주면 `SetCard`가 `Image`를 끄므로(흰 사각형 방지) 추가 처리는 필요 없다.
  - ⚠️ **스프라이트가 없으면 `Image`를 끈다. `sprite = null`로 지우지 말 것** — 스프라이트 없는 `Image`는 사라지는 게 아니라 **흰 사각형**으로 그려진다. 빈 슬롯(`card == null`)에서 배지가 꺼지는 것도 같은 처리다.
  - ⚠️ 프레임 대입에는 null 가드가 있다. 인스펙터에 프레임 스프라이트를 안 넣었을 때 null로 덮어쓰면 **Play 시작과 동시에 카드 프레임이 사라진다**(예전에 실제로 났던 버그).
  - **`SetCard(null)`은 `Clear()`와 같고, "빈 카드"는 `Card.prefab`의 기본 카드 이미지만 남는 상태다** — 이름·설명·수치를 비우고 배지를 끄고, **프레임을 `Awake`에서 캐시해 둔 프리팹 원본으로 되돌린다.**
    - ⚠️ **프레임 복원이 핵심이다.** 예전엔 "프레임은 빈 슬롯에서도 남겨둔다"며 그냥 뒀는데, 그러면 직전 카드의 카테고리 프레임(분홍 액션/회색 명령)이 남아 **이전 카드가 아직 손에 있는 것처럼 보인다.** 스테이지 전환에서 손패를 비울 때 실제로 그렇게 보였다.
    - 프레임 자체를 끄지는 않는다 — 카드 자리가 통째로 사라지면 부채꼴 배치가 흔들린다.
    - 프리팹 원본은 **`Awake`에서 한 번만** 읽는다. 나중에 읽으면 이미 갈아끼운 카테고리 프레임이 "원본"으로 굳는다.
  - 배선 검증 경고는 **`Awake` 1회**에서만 낸다. `SetCard`에서 내면 매 턴 5슬롯 × 스왑마다 불려 콘솔이 폭주한다.
  - **그린 카드를 `_card`에 들고 있다가 `SkillResolver.OnCardValuesChanged`에 반응해 수치 칸만 다시 쓴다.** 어썸·촙·박치기처럼 값이 도중에 변하는 카드가 **손패에 남은 채로** 다른 조합이 완성되면 그 자리에서 같이 갱신되어야 하기 때문이다(슬롯 간 중복이 허용돼 어썸이 두 장 뜰 수도 있다). static 이벤트라 "매니저를 모른다"는 성질은 그대로다(`LanguageSettings.OnChanged`와 같은 결).
    - **럭키도 이 이벤트로 갱신된다** — 럭키 보너스가 쌓이면 그 자리에서 수치 칸이 `보상` → `보상됨`으로 바뀐다(아래 `WordUnlockManager` 참조).
- **`UI/CardSlotView`** — 한 슬롯의 **동작**(슬롯 구독 + 타이핑 들림/교체 애니메이션). 그리기는 같은 오브젝트의 `CardView`에 위임한다. 런타임 생성이라 `OnEnable`이 `Bind`보다 먼저 돌므로 구독이 null 관용적이고 멱등하다(`Subscribe`/`Unsubscribe`/`_subscribed`).
  - **`cardSlotManager`/`inputManager`는 여기 남아야 한다.** 전자는 `OnSlotChanged` 구독과 `Refresh()`의 초기 읽기에, 후자는 `Update`의 타이핑 들림 판정 폴링에 쓰인다 — **둘 다 손패 전용**이라 `CardView`에는 없다.
  - ⚠️ **들림 판정의 첫 조건은 `inputManager.IsTypingTarget(_liftTargetWord)`다** — 입력을 다른 화면이 가져갔으면 이 카드는 반응하지 않는다(위 "입력을 못 가져간 화면은 연출도 멈춰야 한다" 참조). 이 줄을 빼면 일시정지 중 `계속`의 첫 글자에 손패의 `가드`가 같이 떠오른다.
  - ⚠️ **`Refresh()`의 갈래가 둘이고 순서가 중요하다.**
    - **`cardSlotManager == null`이면 곧바로 리턴한다** — 슬롯에 묶이지 않은 카드(일시정지·결과 화면의 명령 카드)는 `BindStatic`이 이미 내용을 채워놨는데, 여기서 비우면 그걸 지운다. 명령 카드는 `Instantiate → BindStatic → Start` 순서라 `Start()`의 `Refresh`가 항상 나중에 오고, **실제로 카드가 통째로 비어 보이는 회귀가 났던 자리다.**
    - **슬롯에 묶여 있는데 못 읽는 경우엔** 조용히 리턴하지 말고 `SetCard(null)`로 비운다. 그냥 두면 `Card.prefab`에 저장된 예시 문구(`파워` / `테스트테스트…` / `+1`)가 그대로 화면에 나온다. **이 프로젝트엔 스크립트 실행 순서 설정이 없어서** `HandFanLayout`이 카드를 스폰·`Bind`할 때 `CardSlotManager.Awake`가 아직 안 돌았을 수 있고, 그러면 `_currentCards`가 `null`이라 이 경로로 온다.
  - ⚠️ **페이드는 루트 `CanvasGroup` 하나로 한다**(`CardView.SetAlpha`). 예전엔 이름과 아이콘의 알파만 따로 만져서, 나중에 붙은 `Description`이 교체 애니메이션 중 혼자 안 사라졌다. 표시 요소가 늘어도 코드를 고칠 필요가 없는 쪽이 의도다 — **이 프리팹에 알파를 만지는 컴포넌트를 더 붙이지 말 것**(`PendingActionView`의 알파 소유권 충돌 사례 참조).
  - **손패를 치우는 연출이 셋이고 성격이 다르다.** 되살아나는 방식이 갈리는 게 핵심이다.

    | 메서드 | 언제 | 끝난 뒤 | 어떻게 되살아나나 |
    |---|---|---|---|
    | `PlayCollapse(delay)` | 패배 | 회전한 채 떨어져 남음. **`_isSwapping`을 켜둔 채 둔다** | 다음 `PlaySwap`(`RefillAll`) |
    | `PlaySink()` | 전체 클리어 | 내려간 자리에 남음. **`_isSwapping`을 켜둔 채 둔다** | 다음 `PlaySwap`(`RefillAll`) |
    | `PlayExit(offset, onComplete)` | 일시정지 | **원래 자리로 튀어오른다**(`_isSwapping`을 내린다) | 부르는 쪽이 `onComplete`에서 끄고 나중에 `PlayEnter`로 켠다 |

    - ⚠️ **`PlaySink`을 `PlayExit`으로 대신하지 말 것.** `PlayExit`은 끝나면서 `_isSwapping`을 내려 카드가 원래 부채꼴 자리로 도로 튀어오르므로, 부르는 쪽이 오브젝트를 꺼야 한다. 그런데 여기서 끄면 **`OnSlotChanged` 구독이 끊겨 재시작 후 `RefillAll`이 그 카드를 못 되살린다** — 결과 화면에서 `다시하기`로 돌아온 판에 손패가 영영 안 보인다. 일시정지가 `PlayExit`을 쓸 수 있는 건 `HideCommandCards`가 `PlayEnter`로 **직접** 되돌리기 때문이다.
    - 가라앉기는 무너짐과 이징이 반대다 — 무너짐은 EaseIn(가속해 떨어진다), 가라앉기는 EaseOut(부드럽게 멎는다). 회전도 시차도 없다: "졌다"가 아니라 "다 끝냈다"라 요란할 이유가 없다.
  - **`IsLeaving`은 이 카드가 자리에서 물러나는 중인지** 알려준다 — **무너짐(stagger 대기 포함)과 가라앉기 둘 다** 해당한다. 기다리는 쪽은 어느 연출인지 알 필요 없이 "치워졌는가"만 보면 된다. ⚠️ **`_isSwapping`으로는 알 수 없다** — 그건 위 표대로 끝나도 켜둔 채 남고 평범한 슬롯 교체에도 켜진다. `HandFanLayout.IsLeaving`이 손패 전체를 묶어서 답하고, `ResultInputHandler`가 그걸 기다렸다가 명령 카드를 띄운다.
  - **`PlayExit(offset, onComplete)` / `PlayEnter(fromOffset, setContent, duration?)`는 슬롯 교체와 무관한 범용 API다.** 밖에서 직접 불러 카드 자리를 통째로 다른 용도로 바꿀 수 있다(`PauseManager`가 손패 5장을 가라앉히고 명령 카드 3장을 띄우는 데, `ResultInputHandler`가 패배 화면에 카드 2장을 띄우는 데 쓴다). `offset` 양수면 위로, 음수면 아래로. `duration`을 비우면 인스펙터의 `enterDuration`을 쓰고, 넘기면 그 화면만 느리게/빠르게 뜬다(결과 화면이 0.6초로 늘려 쓴다).
  - **`BindStatic(CardBase, input)`** — `CardSlotManager` 슬롯 없이 명령 카드 하나를 보여준다(옛 시그니처는 `string`이었다). 슬롯 구독을 끊으므로 `CurrentCards`가 바뀌어도 영향받지 않는다. ⚠️ **`_currentCard`는 일부러 `null`로 둔다** — 그건 "이 슬롯이 대표하는 손패 카드"라서, 빌려 쓰는 명령 카드를 넣으면 슬롯 로직이 진짜 손패 카드로 오인한다. 들림 판정이 실제로 비교하는 건 `_liftTargetWord` 쪽이다. ⚠️ **`slotIndex`는 건드리지 않고 남겨둔다** — 나중에 원래 슬롯으로 되돌릴 때 `SlotIndex`로 읽어 쓴다(`centerOnTop`이 형제 순서를 바꾸므로 리스트 순서로 복원하면 어긋난다).
  - ⚠️ **들림·교체 애니메이션은 `unscaledDeltaTime`을 쓴다.** 이 카드가 일시정지 중 명령 카드로도 재사용되는데 그때는 `timeScale == 0`이라 보통의 `deltaTime`이면 애니메이션이 아예 안 움직인다. 평소엔 값이 같으므로 손패 동작은 그대로다(컨벤션 절의 의도된 예외 목록 참조).
- **`DeckManager`** — 파사드 + **전투 배선**. `HandleChainCompleted`, `HandleTimeExpired`(코루틴으로 딜레이를 두고 턴 전환), `HandleBattleEnded`를 소유한다. `turnChangeDelay`/`postAttackDelay`가 인스펙터에 노출된다.
  - **`TurnPhase` — 지금이 턴의 어느 구간인지 알려주는 단일 출처.** `PlayerInput`(입력) → `ResolvingPlayerActions`(내 공격 재생) → `TurnChangeRest` → `EnemyTurn` → `PostAttackRest` → 다시 `PlayerInput`으로 순환하고, `CurrentPhase` 프로퍼티와 `OnTurnPhaseChanged` 이벤트로 노출된다(`SetPhase`가 값이 실제로 바뀔 때만 쏜다).
    - 쓰는 쪽은 `BattleManager`다 — **내 턴이 아닌 동안 적 인텐트 말풍선을 숨기고**(공격 애니메이션 중에 남아 있으면 어색하다), `PlayerInput`으로 돌아오면 `UpdateUI()`가 적이 살아있는지부터 다시 판단해 알아서 켠다.
    - "지금 무슨 구간인가"를 각자 `timeScale`이나 코루틴 진행 상태로 추측하지 말고 **여기를 볼 것.** 새 연출을 붙일 때도 구간을 추가로 쪼개는 쪽이 맞다.
  - **카메라 흔들림 세기가 인스펙터로 나와 있다** — `hitShakeDuration`(0.1) / `hitShakeMagnitude`(0.2). 예전의 하드코딩 `Shake(0.1f, 0.8f)`는 없어졌다.
    - ⚠️ **흔들림은 시퀀스당 첫 펀치 한 번만 준다.** `CameraShake.Shake`가 매번 `StopAllCoroutines`로 이전 흔들림을 끊고 다시 시작하기 때문에, 펀치마다 부르면 **쉴 새 없이 떨리기만 하고 감쇠가 안 보인다.** 펀치마다 흔드는 쪽으로 되돌리지 말 것.
  - `HandleChainCompleted`의 순서가 중요하다: 적용(`CombatManager`) → UI/말풍선 → 체인 비우기 → **마지막에** `timerManager.AddTime`. 마지막인 이유는 이 호출이 타이머를 0으로 만들면 그 자리에서 `OnTimeExpired` → 턴 전환 코루틴이 시작되기 때문이다. 앞으로 옮기면 턴 전환 도중에 나머지 처리가 끼어든다.
  - `battleManager.OnBattleEnded`를 구독해 승패가 갈리는 즉시 `StopTimer`한다. 이게 없으면 적이 죽은 뒤에도 타이머가 0까지 흐르는 동안 턴 전환이 걸린다. **입력은 반대로 `EnableInput()` + `ClearInput()`으로 다시 연다** — 결과 화면에서 `다시하기`를, 보상 화면에서 카드 이름을 쳐야 하기 때문이다(어느 쪽이 받을지는 `TypingPriority`가 가른다). 치다 만 글자가 명령 단어에 섞이지 않도록 `ClearInput`을 따로 부르는 것도 필수다.
  - **패배(`player.currentHP <= 0`)일 때만 추가로 하는 일이 셋 있다** — `SoundManager.StopBGM()` · 손패를 `PlayCollapse(i * collapseStagger)`로 **시차를 두고 무너뜨리기** · `timerManager.ResetToFull()`.
  - **전체 클리어(`battleManager.IsFinalResult`)에는 손패를 `PlaySink()`으로 그냥 아래로 가라앉힌다.** 진 게 아니니 무너뜨리지 않지만, 결과 화면 명령 카드가 같은 아래쪽 자리로 떠오르므로 치워두지 않으면 겹친다. ⚠️ **`IsGameOver`가 아니라 `IsFinalResult`인 게 중요하다** — 일반 스테이지 클리어(보상 선택 중)까지 걸리면 다음 스테이지로 이어지는 판에서 손패가 사라진다. 그 경우 BGM도 손패도 그대로 둔다.
    - ⚠️ **여기서 슬롯 데이터를 비우지 않는다.** `OnSlotChanged(null)`가 나가면 `CardSlotView.PlaySwap`이 다시 불려 **방금 시작한 무너짐 코루틴을 그 자리에서 끊는다.** 데이터는 재시작 시 `LoadStage`가 알아서 비우고 `RefillAll`이 다시 채운다.
  - **`IsEnemyDefeated()`가 "적이 죽었나"의 단일 판정이다** — `currentEnemy == null || !activeInHierarchy || currentHP <= 0` 셋을 함께 본다. `IsGameOver`만으로는 부족한데, **이벤트 스테이지(마더 드래곤)는 대사가 끝날 때까지 `IsGameOver`가 false로 남고** `CheckGameState`가 죽은 적을 `Destroy`가 아니라 `SetActive(false)`로만 끄기 때문이다. `RunTurnTransition`(가짜 적 턴 방지)과 `PlayPendingActions`(죽은 적에게 남은 콤보가 들어가는 것 방지) 양쪽에서 쓴다.
  - **`DeckManager.TurnPhase`** — `RunTurnTransition`이 어느 구간을 지나는 중인지 다른 시스템이 알 수 있게 노출한 enum이다: `PlayerInput`(입력 열려있고 타이머 도는 중) → `ResolvingPlayerActions`(`PlayPendingActions` — 내가 쌓은 펀치 재생 중) → `TurnChangeRest`(`turnChangeDelay` 대기) → `EnemyTurn`(`ExecuteEnemyTurn` + `OnEnemyTurnEnded` 화상 틱) → `PostAttackRest`(`postAttackDelay` 대기) → 다시 `PlayerInput`. `CurrentPhase` 프로퍼티로 즉시 읽거나 `OnTurnPhaseChanged` 이벤트로 구독할 수 있다. **적 인텐트 말풍선을 내 공격 애니메이션 재생 중(`ResolvingPlayerActions`)엔 숨기고 싶다는 요청으로 추가됐다** — 이런 "지금 연출 중이라 이 UI는 꺼야 한다"류 판단이 필요하면 여기부터 확인할 것.

### 조합 (`02_Scripts/WordChainManager/`)

- **`WordChainManager`** — 현재 조합(체인) 상태. `SubmitWord(string)` → `WordSubmitResult`. 규칙: `Modifier` 무제한 · `Time` 최대 1 · `Type` 최대 1 · `Action` 정확히 1개이며 마지막(넣는 순간 완성). 같은 단어 중복 불가.
  - **오타가 나도 체인은 지우지 않는다.** GDD의 "오타 페널티: 조합 전부 초기화"는 의도적으로 적용하지 않기로 한 결정이다. 체인은 완성되어 `OnChainCompleted`로 넘어간 뒤 `DeckManager`가 `ClearChain()`을 부를 때만 비워진다.
  - 완성된 체인에 유효한 새 단어가 들어오면 그 순간을 다음 체인 시작으로 보고 자동으로 비운다.
- **`WordInstance`** — `CardBase`를 감싸는 얇은 래퍼. `UpgradeLevel`/`UseCount`/`PermanentValueBonus`는 아직 아무도 채우지 않는다.
- **`UI/WordChainView`** — 체인 단어를 공백으로 이어 표시하는 순수 뷰.

### 전투 (`02_Scripts/SkillResolver/`, `Combat/`, `Character/`, `Enemy/`, `BattleManager.cs`, `StageManager.cs`)

**이제 전부 씬에 배치되어 실제로 동작한다.** (예전엔 미배선 스케치였다.)

- **`SkillResolver.Resolve(chain, casterPower)` → `ResolvedAction`** — 체인 단어값 + 시전자의 힘만으로 계산하며 **대상의 방어도나 상태는 모른다.** 타격 횟수(더블/트리플/뎀프시롤)와 치명타(인텔리) 확률을 여기서 즉시 굴려 최종 정수로 접는다. 파워는 타이핑 순서와 무관하게 적용되도록 다른 계산 전에 개수부터 센다.
  - ⚠️ **음수 방지 클램프가 타격 횟수를 곱하기 직전에 있다.** 니킥처럼 깎는 스케일링이 붙으면 위력이 음수까지 내려갈 수 있는데, `CharacterStats.TakeDamage`는 음수를 걸러내지 않아 **피해가 회복으로 둔갑한다.** 곱한 뒤로 옮기면 트리플이 음수를 세 배로 만든다.
  - ⚠️ **`using System;`이 들어 있어 `Random`을 그냥 쓰면 컴파일이 깨진다**(`System.Random`과 모호). `UnityEngine.Random.Range`로 명시할 것 — `WordUnlockManager`도 같은 이유로 명시한다. 단일 어셈블리라 이 한 줄이 프로젝트 전체를 멈춘다.

- **런타임 수치를 카드에 띄우는 static 후크** (`SkillResolver`) — `AwesomeBonus`(런 단위) · `LuckyUses`(**스테이지 단위**) · `ActionsThisTurn` · `ModifiersThisTurn` · `SecondsSpentThisTurn`(턴 단위) + `OnCardValuesChanged` 이벤트, 그리고 `ScalingBonus(source, perUnit)`.
  - ⭐ **누적의 범위가 셋으로 갈리고 리셋 메서드도 그만큼 있다 — 런 ⊃ 스테이지 ⊃ 턴.** `ResetRun()`(어썸) → `ResetStage()`(럭키 확률) → `ResetTurn()`(퍼펙트·니킥·촙·박치기)이 바깥에서 안쪽을 차례로 부른다. **새 누적을 추가할 때 어느 단계에 속하는지부터 정하고 그 메서드에 넣을 것.**
  - ⚠️ **static인 데 이유가 있다.** 카드는 `ScriptableObject`라 씬의 `SkillResolver`를 참조할 수 없는데, 자기 `StatsLabel`에 지금 수치를 띄워야 한다. `CardBase.CardName`이 static `LanguageSettings`를 읽어 언어를 고르는 것과 **똑같은 구조**이고, 같은 이유로(카드마다 인스펙터 배선을 늘리지 않으려고) 이렇게 두었다. 매니저를 만들어 끼우지 말 것.
  - static이라 Play를 멈춰도 남을 수 있어 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`로 초기화 지점을 명시해 둔다(`LanguageSettings`와 같은 이유). 이벤트도 여기서 `null`로 비워 지난 판의 죽은 구독자를 끊는다.
  - **`ScalingBonus`를 계산과 표시가 같이 쓴다** — 카드에 뜬 숫자와 실제로 들어가는 피해가 어긋나면 그게 곧 버그로 보이기 때문이다. 새 스케일링을 넣을 때도 이 한 함수를 거치게 할 것.
  - **누적 시점** — `Resolve` 맨 끝에서 `ActionsThisTurn++` / `ModifiersThisTurn +=` / 시간 증감을 더한다. ⚠️ **지금 완성하는 조합 자신은 세지 않는다**(어썸이 "이전에 성공한 횟수"만 세는 것과 같은 규칙). 계산 앞으로 옮기면 카드 설명의 "이번 턴에 한" 의미가 바뀐다.
  - **비우는 곳이 셋이다** — `DeckManager.RunTurnTransition`이 `ResetTurn()`(턴 끝, `IsGameOver` 검사보다 **위**여야 게임오버로 빠질 때도 비워진다) · `StageManager.LoadStage`가 `ResetStage()`(럭키 확률 + 턴 누적) · `StageManager.Start`가 `ResetRun()`(어썸까지 전부).
- **`ResolvedAction`** — `Damage`/`Defense`/`Heal`/`IgnoresDefense`/`BreaksEnemyDefense`/`StatusEffect`/`DamageReduction`/`TimerChange`/`GrantsLootBonus`. **소비할 시스템이 없어도 계산해서 싣는다**는 원칙이다 — 아직 안 읽히는 값이 있을 뿐 계산이 빠진 게 아니다.
- **`CombatManager.ExecutePlayerAction`** — 상대가 있어야 알 수 있는 것만 처리(방어도 파괴, 피해 적용, 방어/회복) 후 `StatusEffectManager`에 상태이상 부여를 넘긴다. **여기서 읽지 않는 값이 둘 있다** — `TimerChange`는 `DeckManager`가 체인 완성 시점에 쓰고(여기에 추가하면 이중 적용), `GrantsLootBonus`는 보상 라운드를 쌓는 게 `WordUnlockManager`의 일이라 `DeckManager.PlayPendingActions`가 쓴다.
- **`StatusEffectManager`** (`Combat/`) — 상태이상의 부여·지속·해제를 전부 소유한다. **화상/얼음/데빌은 3턴, 마비는 1회성**(GDD가 "다음 턴"으로 명시). 재부여는 지속 턴만 갱신하고 효과를 중첩하지 않는다.
  - `OnEnemyTurnEnded()`는 **코루틴**이다 — 화상 피해를 적 공격과 겹치지 않게 잠깐 띄우고 말풍선까지 보여주므로 `DeckManager`가 `yield return`으로 기다린다.
  - ⚠️ **화상 피해는 `TakeDamage`가 아니라 `currentHP`를 직접 깎는다.** `CharacterStats.Die()`가 `Destroy`를 부르는데 `BattleManager.CheckGameState`의 승리 판정은 `currentEnemy != null`을 요구해서, 화상으로 적을 파괴하면 **승리 처리가 통째로 건너뛰어진다.** 직접 깎고 `UpdateUI()`를 부르면 정상적인 `SetActive(false)` 경로를 탄다.
  - **얼음**은 `enemy.power`를 직접 깎고 **실제로 깎은 양을 기억했다가** 그만큼만 복원한다(최소 0 클램프 때문). **데빌**은 `CharacterStats.damageTakenMultiplier`를 0.75로 낮춘다 — 받는 쪽에서 처리해야 `EnemyManager`(다른 작업자 파일)를 고치지 않는다.
  - 뷰가 읽는 공개 상태는 `EnemyEffects`(`IReadOnlyDictionary<StatusEffectType, int>`) · `DevilTurnsLeft` · `OnEffectsChanged` 이벤트, 그리고 표시 이름 `GetDisplayName(effect)` / `DevilDisplayName`(한/영 분기).
  - `IsCurrentEnemy(Transform)`으로 "이 대상이 지금 적인가"를 알려준다. `StatusIconRow`가 이걸로 자기가 적용인지 플레이어용인지 판별한다.
  - **`UI/StatusIconRow`** — 화상/마비/냉동/데빌을 **아이콘 + 남은 턴 숫자**로 보여주는 순수 뷰. **`HPBar.prefab` 안에 들어 있어** 캐릭터를 따라다닌다(`[RequireComponent(typeof(WorldAnchoredUI))]`). 같은 오브젝트의 `WorldAnchoredUI.Target`으로 적/플레이어를 **자동 판별**한다 — 예전엔 인스펙터 토글이었는데 그 오버라이드가 Revert되자 플레이어 바가 적 상태이상을 그리는 버그가 났다.
    - **방어(쉴드)는 여기서 다루지 않는다.** 그건 상태이상이 아니라 `CharacterStats.defense` 값이고 `HealthBarUI`의 `defIcon`/`defText`가 담당한다.
    - **가로 배치는 좌표 계산이 아니라 레이아웃이 한다** — 부모에 `Horizontal Layout Group`을 붙이고 아이콘을 **반드시 화상 → 마비 → 냉동 → 데빌 순서로** 자식에 두면, 꺼진 아이콘은 레이아웃에서 자동으로 빠지고 켜진 것만 그 순서대로 왼쪽부터 붙는다. 코드에서 위치를 잡으려 하지 말 것.
    - **숫자 텍스트는 인스펙터에 연결하지 않는다** — 각 아이콘 밑에 `TMP_Text` 자식을 만들어두면 `Awake`가 `GetComponentInChildren(true)`로 찾는다(`true`여야 꺼진 상태에서도 찾힌다). 아이콘을 `SetActive`로 켜고 끄면 자식 텍스트도 같이 켜지고 꺼진다.
    - ⚠️ **구 `UI/StatusEffectView`(`화상 3` 텍스트 한 줄)는 이걸로 완전히 대체됐고 파일도 삭제됐다.** 텍스트 한 줄 방식으로 되돌리지 말 것.
- **`PendingActionManager`** (`Combat/`) — 한 턴 동안 완성된 조합의 `{ SkillName, ResolvedAction }` 쌍을 쌓아두는 **순수 보관소**. `Enqueue`/`TryDequeue`/`Clear` + `OnActionQueued`/`OnActionDequeued`/`OnCleared`. 먼저 완성한 조합이 먼저 나가는 **FIFO**다(화면엔 "쌓이는" 것처럼 보이지만 재생 순서는 쌓인 순서 그대로).
  - **적용도 재생도 하지 않는다.** 꺼내서 `CombatManager`에 넘기고 사이에 간격을 두는 건 `DeckManager.PlayPendingActions`다 — 전투 배선을 `DeckManager` 한 곳에 유지하려는 의도적 분리다.
  - ⚠️ **`HandleChainCompleted`에서 `Enqueue`는 반드시 `timerManager.AddTime`보다 먼저 와야 한다.** `AddTime`이 남은 시간을 0으로 만들면 그 호출 안에서 곧바로 턴 전환 코루틴이 시작되기 때문이다. 순서가 뒤바뀌면 **훅으로 타이머를 깎아 턴을 끝낸 그 조합만 재생 목록에서 빠진다.**
  - 재생 중 적이 죽으면 남은 것을 버리고 즉시 중단한다(`Die()`가 `Destroy`를 부르므로 이후 공격은 대상이 없다). `HandleBattleEnded`와 `StageManager.LoadStage`에서도 비워, 지난 판 공격이 다음 스테이지로 넘어가지 않게 한다.
  - **`UI/PendingActionView`** — 쌓인 문장을 플레이어 옆에 보여주는 순수 뷰. **말풍선 하나에 줄바꿈으로 이어 붙인다** — 예전처럼 한 줄에 프리팹 하나씩 찍어내지 않는다.
    - `Awake`에서 `bubblePrefab`(`ThinkingBubble.prefab`)을 **자식으로 한 번만** `Instantiate`하고, 이후엔 `_lines` 목록을 `string.Join("\n", …)`으로 합쳐 `SpeechBubble.Setup`에 넘긴다. 배경·꼬리·`ContentSizeFitter`는 전부 그 프리팹이 이미 하던 일이라 여기서 다시 만들지 않는다(`RewardCardView.cardPrefab`, `SpeechBubbleManager.speechBubblePrefab`과 같은 패턴).
    - 이 오브젝트 자신은 **위치만 잡는 빈 앵커**다. `followTarget`(보통 `player`)을 넣으면 `LateUpdate`에서 `WorldToScreenPoint` + `screenOffset`(참조 해상도 픽셀 × `scaleFactor`)으로 캐릭터를 따라가고, 비우면 캔버스 앵커에 그대로 머문다.
    - 줄이 늘어날수록 `fontSizeStepPerLine`(기본 2)만큼 폰트를 줄이고 `minFontSize`(12)에서 멈춘다. 기준값은 `Awake`에서 읽은 **프리팹의 폰트 크기**다.
    - 숨김은 **자식 말풍선의 `SetActive`**로 한다(목록이 비었을 때 + 턴 종료 `OnTimeExpired`). 자기 자신을 끄는 게 아니라 자식만 끄므로 `LateUpdate`와 이벤트 구독이 계속 살아 있다 — 예전에 `CanvasGroup.alpha`로 숨기면서 `WorldAnchoredUI`와 알파 소유권이 충돌하던 문제가 이 구조로 사라졌다.
    - 한글 문장이 들어가므로 **말풍선 라벨 폰트는 Paperlogy여야 한다.**
- **`CharacterStats`** — `TakeDamage(damage, ignoreDefense = false)`, `Heal`, `AddDefense`, `IncreasePower`, private `Die()` → `Destroy(gameObject)`(그래서 호출자들이 매 프레임 null 체크한다).
  - **피해를 깎는 상시 "방어력" 스탯은 없다.** 스탯은 `maxHP`/`currentHP`/`power`/`defense` 넷뿐이고 `power`는 공격에만 쓰인다. 방어에 관여하는 값은 넷이며 성격이 다르다 — **`defense`**(유일하게 피해를 막는 값. 쌓였다 닳는 소모품) · `EnemyData.defensePower`(적이 Defend할 때 쌓는 **양**) · `damageTakenMultiplier`(배율, 데빌만 0.75) · `ResolvedAction.DamageReduction`(데빌이 실어 보내 위 배율로 변환된다).
  - **계산 순서**: `× damageTakenMultiplier` → `defense`가 있는 만큼 흡수(그만큼 `defense`도 닳음) → **넘친 만큼은 반드시 HP로.** `IgnoresDefense`(킥)는 흡수를 건너뛰고, `BreaksEnemyDefense`(어퍼컷)는 때리기 전에 `defense = 0`으로 만든다.
  - 흡수는 `Mathf.Min(defense, damage)` 한 줄로 계산한다 — 예전엔 `defense >= damage`로 두 갈래로 나눴는데, 한쪽 부호를 잘못 쓰면 넘친 피해가 통째로 사라질 수 있어 합쳤다. 음수 피해 가드도 여기 있다(`SkillResolver`가 이미 막지만 적 공격·디버그 킬스위치 등 경로가 여럿이다).
  - **로그가 입력값까지 찍는다** — `피해 10 / 방어 15 -> 5 / HP 150 -> 150 (실피해 0)`. "피해가 0으로 들어왔다"와 "방어가 다 먹었다"를 구분하려면 이 로그를 볼 것.
  - `damageTakenMultiplier`(기본 1)를 `TakeDamage` 맨 앞에서 곱한다. 데빌이 이걸 0.75로 낮춘다 — **공격하는 쪽이 아니라 받는 쪽에서** 처리하는 이유는 적 공격이 `EnemyManager`(다른 작업자 파일)에서 나가기 때문이다.
- **`PlayerBattleVisuals`** (`Character/`) — 플레이어 공격 연출. `MoveToEnemyCoroutine`(적 앞 `dashOffset`까지 돌진) → `PlayAttackAnimation(isFirstAttack, speedMultiplier)` → `MoveToOriginCoroutine`(복귀). 첫 타는 `Punch1`, 이후는 `Punch2~4` 중 랜덤 트리거다.
  - `speedMultiplier`가 `Animator.speed`에 그대로 들어가므로, 쌓인 공격이 많아 간격이 압축되면 애니메이션도 같이 빨라진다. 턴이 끝나면 `ResetAnimationSpeed()`로 1.0으로 되돌린다.
  - `Start()`에서 원래 위치를 기억하므로 **시작 위치를 런타임에 옮기면 복귀 지점이 어긋난다.**
- **`EnemyBase : CharacterStats`** — `EnemyData`(SO)를 런타임 스탯으로 옮기는 다리. `enemyManager.currentEnemy`의 타입이자 `StageManager`가 스폰 직후 `GetComponent`로 집어오는 타입이다.
  - **스탯이 정해지는 곳이 둘이고 `isScaled` 플래그로 갈린다.** `StageManager`가 스폰 직후 부르는 **`ApplyScaling(stageIndex)`**(스테이지당 +20%로 `maxHP`/`power`를 정하고 `currentHP`·`defense`를 초기화한 뒤 `isScaled = true`)와, 그게 안 걸렸을 때만 도는 **`Start()`**(에셋값 그대로)다. `Start()`는 `ApplyScaling`보다 **나중에** 돌기 때문에 이 순서가 성립한다.
  - ⚠️ **`enemyData`가 비어 있으면 두 분기가 <b>모두</b> 건너뛰어져 프리팹에 박힌 인스펙터 값이 그대로 남는다.** (예전 문서엔 "`base.Start()`로 폴백한다"고 적혀 있었지만 **`base.Start()`를 부르지 않는다** — `CharacterStats.Start()`의 `currentHP = maxHP`조차 안 돈다.) 마더 드래곤이 9999를 유지하는 게 이 경로다. 적이 엉뚱한 체력으로 나오면 프리팹의 `enemyData` 연결부터 확인할 것 — 조용히 넘어간다.
  - ⚠️ **`Start()`가 `animator = GetComponent<Animator>()`로 인스펙터 연결을 덮어쓴다.** 같은 오브젝트에 `Animator`가 없거나 자식에 있으면 인스펙터에 넣어둔 참조가 날아간다.
  - **적도 이제 돌진한다** — `MoveToPlayerCoroutine(player)` / `MoveToOriginCoroutine()`이 `PlayerBattleVisuals`와 짝을 이루는 대칭 구현이다(`dashOffset` 1.5 / `moveDuration` 0.2 / 이동 커브 2개). 복귀 지점은 `Start()`에서 읽은 스폰 위치라 **런타임에 적을 옮기면 어긋난다**(플레이어 쪽과 같은 함정).
  - **돌진 중 정렬 순서를 상대보다 +1로 올렸다가 복귀할 때 되돌린다.** 양쪽 다 그렇게 하므로 "앞으로 나간 쪽이 항상 위"가 된다. `spriteRenderer`를 비워두면 같은 오브젝트에서 찾는다.
- **`EnemyManager`** — 가중치로 다음 의도를 굴리고(`ActionType { Attack, Defend, Buff }`) **`ExecuteEnemyTurnCoroutine(player)`**에서 실행. **액션 enum이 두 개 있다**: 카드의 `ActionKind { Attack, Defense }`와 이것.
  - ⚠️ **적 턴은 이제 코루틴이다.** `DeckManager.RunTurnTransition` → `BattleManager.ExecuteEnemyTurnCoroutine()` → `EnemyManager.ExecuteEnemyTurnCoroutine(player)`가 전부 `yield return`으로 이어져, 돌진→타격→복귀가 끝날 때까지 턴 전환이 기다린다. 옛 `void ExecuteEnemyTurn(...)` 시그니처로 되돌리면 연출이 잘린다.
  - 순서는 플레이어 쪽과 같다 — **공격 자세를 먼저 잡고 그 자세인 채로 돌진**(`PlayAttackAnimation()` → `MoveToPlayerCoroutine`) → `attackHitDelay`(0.15초, 주먹이 뻗는 시점) → `TakeDamage` → 복귀. **공격일 때만** 이동하고 방어·버프는 제자리에서 한다.
  - **적 인텐트는 아이콘 + 색 입힌 숫자로 보여준다.** 인스펙터에 `attackIcon`/`defendIcon`/`buffIcon`(`01_Arts/UI/Attack`·`shield`·`Up`)과 `attackColor`/`defendColor`/`buffColor`가 있고, `GetIntentIcon()`/`GetIntentColor()`로 꺼낸다.
  - ⚠️ **`GetIntentString()`은 이제 숫자만 돌려준다.** 예전의 `"Intent: Attack (10)"` 같은 라벨 문구가 아니다 — 무슨 행동인지는 아이콘이 말하므로 텍스트에서 뺐다. 이 문자열을 파싱하거나 라벨로 그대로 쓰는 코드를 만들지 말 것.
- **`BattleManager`** — 승패 판정과 턴 진행의 UI 측 창구. HP/방어도 표시는 `HealthBarUI`로, 말풍선은 `SpeechBubbleManager`로 **위임한다**(직접 슬라이더를 만지지 않는다). `OnPlayerActionResolved(bubbleText)`는 **턴을 끝내지 않는다**(쌓인 공격을 재생하는 동안 여러 번 호출됨). 적 턴은 **`ExecuteEnemyTurnCoroutine()`**(코루틴)으로 분리되어 있고 `DeckManager`가 `yield return`으로 기다린다. 말풍선엔 스킬 이름이 아니라 적용된 수치가 뜬다.
  - **`IsEventActive`가 "지금 대사 중인가"의 창구다** — `EventManager.isEventActive`를 그대로 중계한다. `DeckManager`가 대사 중에 타이머를 다시 돌리지 않으려고 보고, `DeckManager`에 `EventManager` 참조를 새로 꽂지 않으려고 여기를 거친다. ⚠️ **`IsGameOver`로는 구분되지 않는다** — 이벤트 스테이지는 대사가 끝날 때까지 false다.
  - **결과 화면은 `ShowResult(ResultKind)`다** — `enum ResultKind { Victory, Defeat, GameClear }`. 예전의 `ShowResult(bool showStats)`가 아니다.
    - ⭐ **`IsFinalResult`(= `isGameOver && lastResultKind != Victory`)가 "런이 끝났는가"의 단일 판정이다.** `IsGameOver`만 보면 **보상을 고르는 중인 일반 클리어까지 걸린다** — 그때는 게임이 이어지므로 일시정지도 걸려야 하고 결과 명령 카드가 떠서도 안 된다. `PauseManager`(멈출 수 있나)와 `ResultInputHandler`(카드를 띄우나)가 **둘 다 이걸** 본다. 같은 판단이 또 필요하면 여기를 쓸 것 — `player.currentHP`로 승패를 되짚는 방식으로 돌아가지 말 것(전체 클리어를 승리로 오인한다).
    - ⭐ **결과 종류마다 프리팹이 따로다.** `BattleManager`는 `ResultPanelView` 두 벌(`defeatResult`/`gameClearResult`)을 들고 결과에 맞는 쪽만 켠다 — 코드가 제목을 갈아끼우지 않고 **어느 패널을 켤지만** 정한다.
      - 옛 `ScreenPresentation` 세 벌(`victory`/`defeat`/`gameClearPresentation`)과 `resultImage`는 **삭제됐다** — 그 제목이 들어갈 `ResultStatsView.titleText`가 프리팹에서 연결조차 되어 있지 않아 **화면에 나간 적이 없었다.** 채워도 안 보이는 죽은 값이라 문구가 아니라 프리팹 단위로 가르는 쪽으로 바꿨다. 코드에 문구를 되돌리지 말 것.
      - ⚠️ **`HideResultUI()`는 둘 다 끈다.** 하나만 끄면 결과 종류가 바뀌는 경로(마지막 스테이지의 `Victory`→`GameClear`, 패배 후 `다시하기`)에서 반대쪽이 켜진 채 남는다. `ApplyResult`도 새 패널을 켜기 전에 이걸 먼저 부른다.
    - ⚠️ **일반 스테이지 승리(`ResultKind.Victory`)는 결과를 아예 띄우지 않는다** — `ApplyResult`가 곧바로 `HideResultUI()`로 빠진다(이유는 위 `04_UI` 절). `ShowStatisticsUI`가 도는 건 **패배·전체 클리어뿐**이다.
    - **통계는 `ResultStatsView`가 그린다** — 각 패널 안의 라벨 6개 + 값 6개를 각각의 `TMP_Text`로 받는다(최고 스테이지·CPM·단어 수·가한 피해·받은 피해·**난이도**). ⚠️ **결과 창 프리팹이 둘이라 두 벌 다 배선해야 한다** — 한쪽만 하면 반대쪽 결과에서 그 줄만 조용히 빈다. 라벨 문구는 한/영 두 벌이 이 뷰의 인스펙터에 있고 언어가 바뀔 때만, 값은 결과가 뜰 때마다 갱신된다. **라벨과 숫자를 따로 디자인·배치해도 스크립트를 안 고쳐도 된다는 게 요점**이고, 그래서 옛 `statsText`(한 줄) + `statsFormat`(문자열 한 덩어리) 구조는 삭제됐다. ⚠️ `panel`이 비면 **폴백 없이 경고만 남기고 리턴한다**(엉뚱한 화면이 뜬 채 넘어가는 것보다 낫다). 반대로 `stats`는 **선택**이라 비어 있으면 수치만 건너뛰고 패널은 정상적으로 켜진다 — 통계를 안 보여주는 결과 화면도 만들 수 있게 한 것이다.
    - **`ResultPanelView.reveals`(`TextGateRevealAnimation[]`)를 패널을 켤 때 재생한다.** 이 컴포넌트는 아무도 `Play()`를 부르지 않으면 마스크가 **닫힌 채(폭 0)** 남으므로, 패널에 게이트 연출을 새로 붙였다면 이 배열에 넣어야 보인다(마스크마다 컴포넌트가 하나씩 따로 필요하다).
      - ⚠️ **꺼짐→켜짐으로 바뀔 때만 재생한다.** `ApplyResult`는 같은 결과로 여러 번 불릴 수 있어서(`RefreshResult`, `CheckGameState`→`ShowResult`), 무조건 재생하면 이미 다 열린 마스크가 처음부터 되감긴다.
    - 무엇을 입력해야 하는지는 `resultInputHandler.BuildHint()`를 제목 뒤에 붙이는 경로가 남아 있으나, **지금 그 값은 빈 문자열이다**(명령이 카드로 뜬다). 안내 문구를 되살리려면 `ResultInputHandler.BuildHint()`부터 고칠 것 — **명령 단어를 소유한 쪽이 안내도 만든다**는 규칙은 그대로다.
    - ⚠️ **`ShowResult` 안에서 `OnBattleEnded`가 `StageManager`의 보상 라운드를 열고 돌아온다.** 그래서 첫 그리기는 "보상을 고르는 중" 상태이고(`rewardInputHandler.IsSelecting`이면 `다음` 안내를 숨긴다), 선택이 끝나면 `StageManager`가 **`RefreshResult()`**를 불러 같은 화면을 다시 그린다. 그제서야 안내가 뜬다.
- **`HealthBarUI`** (`02_Scripts/HPBarUI.cs`) — HP 슬라이더·텍스트·방어 아이콘을 한 묶음으로 갱신하는 뷰. `UpdateUI(currentHP, maxHP, defense)` / `Hide()`. 방어도가 있으면 Fill이 회색, 없으면 `normalColor`로 돌아간다.
  - ⚠️ **파일명(`HPBarUI.cs`)과 클래스명(`HealthBarUI`)이 다르다.** Unity는 MonoBehaviour의 둘이 일치해야 컴포넌트로 붙일 수 있어서, **지금 이 스크립트는 `Add Component`로 추가할 수 없다.** 아래 "알려진 이슈" 참조.
- **`SpeechBubbleManager`** — 말풍선 프리팹을 런타임에 찍어내고 `duration` 뒤 `Destroy`한다. **싱글턴**(`public static Instance`)이고 `BattleManager`·`StatusEffectManager`가 `Instance`를 직접 참조한다 — 인스펙터 배선 컨벤션의 예외다(컨벤션 절 참조).
  - **위치는 캐릭터의 월드 좌표를 스크린으로 변환해 잡는다.** `playerScreenOffset`/`enemyScreenOffset`(참조 해상도 1920×1080 기준 픽셀)을 더해 좌우 대칭으로 밀어내며, 캔버스 `scaleFactor`를 곱해 해상도가 바뀌어도 UI 크기와 비율이 유지된다. `playerScreenRatio`/`enemyScreenRatio`는 이제 **카메라를 못 찾을 때의 폴백**으로만 남아 있다.
  - ⚠️ 오프셋 단위에 주의할 것. `bubbleWorldOffset`은 **월드 단위**라 카메라가 orthographic size 5인 이 프로젝트에서는 **1080p 기준 1 ≈ 108픽셀**이다. 미세 조정은 픽셀 단위인 `*ScreenOffset`으로 하는 게 맞다.
  - **`SpeechBubble`** — 프리팹 쪽 순수 뷰. API가 셋이다: `Setup(message)`(텍스트만) · `SetupIcon(icon)`(아이콘만) · **`SetupIntent(icon, message, textColor)`**(적 인텐트 — 아이콘 + 색 입힌 숫자). 셋 다 쓰지 않는 쪽을 `SetActive(false)`로 꺼서 `Horizontal Layout Group`이 알아서 좁혀지게 한다.
    - `Setup`은 텍스트 색을 **캐시해 둔 프리팹 기본색으로 되돌린다** — 같은 오브젝트를 `SetupIntent`로 물들인 뒤 재사용해도 색이 남지 않게 하기 위한 것이다.
    - ⚠️ **꼬리는 코드가 건드리지 않는다.** 예전 `Setup(message, isPlayer, isNormalTail)`은 앵커·피벗·좌우 반전을 코드에서 뒤집었지만 지금은 프리팹 배치 그대로 쓴다. 생각풍선은 별도 프리팹(`ThinkingBubble.prefab`)이다.
    - 가로 배치(아이콘 → 텍스트 순서)도 코드가 아니라 **프리팹의 `Horizontal Layout Group` + 자식 순서**가 담당한다.
  - ⚠️ **`BattleManager`가 인텐트 말풍선을 두 곳에서 띄우는데 한쪽만 아이콘화되어 있다.** `UpdateUI()`의 상시 인텐트 표시는 `SetupIntent(...)`를 쓰지만(마더 드래곤만 대사라 `Setup`), **적 행동 직후의 임시 결과 말풍선**(`ExecuteEnemyTurn` 안 `SpeechBubbleManager.ShowBubble(...)`)은 아직 텍스트 그대로다. `ShowBubble`이 문자열만 받기 때문이고, 아이콘화하려면 오버로드를 새로 만들어야 한다.
- **`WorldAnchoredUI`** (`02_Scripts/UI/`) — 월드 오브젝트를 따라다니는 Overlay UI. HP 바가 이걸로 캐릭터 위에 붙는다. `LateUpdate`에서 `WorldToScreenPoint`로 위치를 잡고 `Camera.main`을 캐시한다.
  - 위치 조정은 **`screenOffset`(참조 해상도 픽셀)** 으로 한다. `worldOffset`도 있지만 위와 같은 이유로 1이 100픽셀을 넘는다.
  - ⚠️ 숨길 때 `SetActive(false)`가 아니라 **`CanvasGroup.alpha`** 를 쓴다 — 오브젝트를 끄면 `LateUpdate`가 멈춰 대상이 다시 나타나도 스스로 되살아나지 못한다. 그래서 `[RequireComponent(typeof(CanvasGroup))]`이 걸려 있다.
  - 적은 `Destroy`(`CharacterStats.Die`)와 `SetActive(false)`(`BattleManager.CheckGameState`) 두 경로로 사라지므로 **둘 다 검사**한다.
- **`SoundManager`** — FMOD 재생 창구이자 **볼륨 설정의 소유자**. `PlayBGM`/`StopBGM`/`PlaySFX`(2D·3D 두 오버로드)/`SetBGMParameter`, 그리고 인스펙터 이벤트를 감싼 편의 메서드들 — `PlayTitleBGM`/`PlayBattleBGM`/`PlayBossBGM`(`titleBGM`/`battleBGM`/`bossBGM` — 어느 것을 언제 트는지는 `TitleMenu.Start`와 `StageManager.LoadStage`가 정한다), `PlayRandomPunch`(`punchSounds[]`), `PlayRandomCardUse`(`cardUseSounds[]`), `PlayWordComplete`(`wordCompleteSound` — `CardInputHandler`가 매칭 성공 때 부른다). **싱글턴 + `DontDestroyOnLoad`** 라 타이틀에서 바꾼 볼륨이 전투 씬까지 따라간다. `EventReference.IsNull` 가드가 있어 이벤트 미지정 자체는 안전하다.
  - **볼륨 3종은 전부 버스에 건다** — `getBus(경로).setVolume()` 하나로 통일되어 있다. FMOD Studio 프로젝트의 믹서 구조가 이렇다:

    ```
    bus:/            (Master Bus)
      ├─ bus:/BGM    ← BGM, Main
      └─ bus:/SFX    ← Punch1~3, Card1~3, Input
    ```

    마스터가 하류라 BGM/SFX에 **곱해서** 걸린다(마스터 0이면 전부 무음).
  - ⚠️ **인스턴스(`EventInstance.setVolume`)에 거는 방식으로 되돌리지 말 것.** 실제로 그렇게 만들었다가 갈아엎었다. 인스턴스에 걸면 **생성 시점에 볼륨이 박혀서 재생 중에는 바꿀 수 없고**(슬라이더를 움직여도 이미 흐르는 BGM은 그대로), `SoundManager`가 만들지 않은 소리에는 아예 걸리지 않는다. 버스는 믹서 하류라 누가 언제 재생했든 실시간으로 적용된다. 당시 증상은 "마스터만 먹고 BGM/SFX 슬라이더는 안 먹는다"였는데, 마스터만 유일하게 버스였기 때문이다.
  - **버스를 새로 추가하려면 Unity만으로는 안 된다.** FMOD Studio의 `Mixer > Routing`에서 `New Group`으로 그룹을 만들고 **Routing 브라우저 안에서** 이벤트를 그 그룹으로 드래그한 뒤 `File > Build`로 뱅크를 다시 빌드해야 한다. 빌드를 빠뜨리면 `Master.strings.bank`에 경로가 없어 `getBus`가 못 찾는다.
    - VCA로도 같은 걸 할 수 있지만 **이벤트를 VCA에 직접 끌어다 놓는 건 동작하지 않는다**(VCA는 버스를 조절하는 물건이다). 실제로 시도했다 실패해서 그룹 버스로 갔다.
  - `PlaySFX`는 `RuntimeManager.PlayOneShot`을 그대로 쓴다. 볼륨은 버스가 잡으므로 핸들을 들고 있을 이유가 없다.
  - ⚠️ **슬라이더 값과 실제 게인이 일부러 다르다.** `setVolume`은 선형 진폭인데 청감은 로그에 가까워, `ToGain`이 값을 **제곱**(`VolumeCurve = 2f`)해서 넘긴다. UI는 원래 값을 %로 보여준다.
  - ⚠️ **버스를 잡을 때 `RuntimeManager.IsInitialized`로 먼저 막으면 안 된다.** 그건 FMOD 초기화를 유발하지 않아서(내부의 `instance` 필드만 본다), 아직 아무 소리도 재생하지 않은 타이틀 씬에서는 항상 false가 되고 볼륨이 조용히 안 먹는다. `RuntimeManager.StudioSystem`에 접근하는 것 자체가 초기화를 유발하므로 그쪽을 `try`로 감싸고 `RESULT`를 직접 본다. 실패 경고는 **경로별로 첫 번째만** 남긴다(슬라이더를 움직일 때마다 불린다).
  - 볼륨은 `PlayerPrefs`(`option.volume.*`)에 저장된다. `Awake`에서 읽어두고 FMOD 호출은 `Start`로 미루며(뱅크 로드 후라야 안전), 디스크 쓰기는 드래그 중이 아니라 **옵션 창을 닫을 때** `SaveVolumes()` 한 번이다.
- **타격감 연출 — `CameraShake` / `FloatingDamageManager` / `HitEffectManager`** — 셋 다 `public static Instance` 싱글턴이고 **호출부에 null 가드가 있어 씬에 없어도 조용히 넘어간다**(소리·흔들림·숫자·이펙트가 안 나오면 씬에 오브젝트가 있는지부터 볼 것).
  - `DeckManager.PlayPendingActions`의 타격 루프에서 **셋 다 `Damage > 0`인 조합에만** 붙는다(방어처럼 피해가 0인 조합은 펀치 자체를 재생하지 않는다). **단 카메라 흔들림만 시퀀스당 한 번**이고 나머지 둘은 타격마다다.
  - 플레이어가 맞을 때는 `EnemyManager.ExecuteEnemyTurn`이 `HitEffectManager`를 부른다(흔들림·피해 숫자는 그쪽에 없다).
  - `CameraShake`는 `Main Camera`에 붙어 `OnEnable`에서 원위치를 기억하고 `localPosition`을 흔든다 — **런타임에 카메라를 옮기면 복귀 지점이 어긋난다**(`PlayerBattleVisuals`와 같은 함정). 세기는 인스펙터 `shakeMultiplier`가 전체 배율이고, 호출 인자는 `DeckManager.hitShakeDuration`/`hitShakeMagnitude`에서 온다.
  - `FloatingDamageManager`는 `FloatingDamageText.prefab`을 `damageCanvas` 아래에 찍는다. 위치는 적의 월드 좌표를 `WorldToScreenPoint`로 바꿔 잡고 숫자가 겹치지 않게 살짝 랜덤으로 흩뿌린다. ⚠️ **`Camera.main`을 캐시 없이 매번 부르고 null 검사도 하지 않는다.**
  - **`HitEffectManager`** (`Combat/`) — 피격 시 대상 `SpriteRenderer`의 `bounds` 안 랜덤 위치에 `hitEffectPrefab`(`03_Prefabs/Effect/HitEffect.prefab`)을 하나 찍는다. **부르는 쪽은 `CharacterStats.TakeDamage`가 아니라 `DeckManager`의 펀치 루프(적 쪽)와 `EnemyManager.ExecuteEnemyTurn`(플레이어 쪽) 두 군데다** — `TakeDamage`에서 부르면 화상 같은 지속 피해에도 이펙트가 튄다.
    - 싱글턴인 이유가 여기 명시돼 있다: 부르는 쪽이 적 프리팹마다 하나씩 붙는 `CharacterStats` 계열이라 인스펙터 연결로 하면 **프리팹 개수만큼 같은 참조를 반복해서 심어야 한다.**
  - **`EffectBase`** (`Combat/`) — 이펙트 프리팹 공통 베이스(`team17_gamejam`에서 그대로 가져옴). 자식 렌더러의 머티리얼에서 `_Progress`(0~1)를 `lifetime` 동안 채우고, `_Duration`이 있으면 **레이어마다 다른 속도**로, `_Seed`가 있으면 스폰 때 랜덤값을 한 번 넣어 매번 다른 모양이 되게 한다. 다 되면 스스로 `Destroy`.
    - `lifetime = -1`은 **"외부에서 파괴할 때까지 무한 재생"**을 뜻하는 약속된 값이다(`OnValidate`가 음수를 -1로 고정한다). 실수로 음수를 넣어 무한 재생이 되지 않게 하는 장치이니 의미를 바꾸지 말 것.
    - 머티리얼 프로퍼티는 `MaterialPropertyBlock`으로 넣는다 — `sharedMaterial`을 직접 쓰면 같은 셰이더를 쓰는 이펙트가 전부 같이 움직인다.
  - ⚠️ 셋 다 나머지 프로젝트의 인스펙터 배선 컨벤션과 어긋나는 `public` 필드 + 싱글턴 스타일이다(컨벤션 절 참조). **새 코드를 이 패턴으로 확장하지 말 것.**
- **`StatisticsManager`** (`02_Scripts/`) — 런 통계 수집기. **싱글턴**이고 `Awake`에서 중복 인스턴스를 스스로 `Destroy`한다. `totalPlayTime`(`StartTracking`/`StopTracking` 사이 `Time.deltaTime` 누적) · `totalTypedCharacters` · `validWordsUsed` · `totalDamageDealt` · `totalDamageTaken` · `highestStageReached`, 그리고 `GetCPM()`(분당 타수).
  - 수집 지점이 시스템 곳곳에 흩어져 있다: `CardInputHandler`(매칭 성공 → `AddValidWord`) · `CombatManager`(`AddDamageDealt`) · `EnemyManager`(`AddDamageTaken`) · `StageManager`(`StartTracking`/`UpdateHighestStage`) · `BattleManager`(`StopTracking` + 결과 표시). 전부 `Instance != null` 가드가 있다.
  - 결과는 `BattleManager.ShowStatisticsUI(...)`가 `resultPanel`(`End Canvas > ResultPanel`) 안의 **`ResultStatsView.SetStats(...)`**에 넘긴다. 라벨 문구는 코드 리터럴이 아니라 그 뷰의 인스펙터에 한/영 두 벌로 있고, 스테이지 분모는 `stageManager.TotalStages`(계산 프로퍼티)에서 읽는다.
  - ⚠️ **저장되지 않는다.** 씬을 넘어가면 사라지고(`DontDestroyOnLoad` 없음) `PlayerPrefs`에도 안 들어간다 — 한 판짜리 통계다.
- **`EventManager`** — **마더 드래곤 전용** 대화 이벤트. `StartEvent(isMotherDragon, healAmount, isEnding)` → 대사를 순서대로 보여준다. 대사는 JSON(`DialogueIds.DragonEvent`/`EndingEvent`)이고 `string.Format`으로 `healAmount`가 들어간다.
  - ⭐ **여기 도달하는 경로는 마더 드래곤 아웃로와 엔딩 둘뿐이고, 둘 다 시간으로 저절로 넘어간다**(`PlayMotherDragonOutroRoutine` / `PlayEndingRoutine`). 즉 **`isMotherDragon`은 사실상 항상 true**다. 일반 적 처치는 `BattleManager.FinishEnemyDeathAfterEffect`가 여기를 거치지 않고 직접 처리한다(위 `event.normal` 항목 참조).
  - ⚠️ **그래서 스페이스로 넘기는 경로(`HandleAdvance` → `ShowNextDialogue`)는 지금 아무도 타지 않는다.** 구독과 코드는 남아 있지만 `wasMotherDragon`이 항상 true라 조건을 통과하지 못한다 — **살아 있는 경로로 읽지 말 것.** 대사 id를 직접 받는 형태로 `StartEvent`를 일반화할 때 다시 쓰일 자리라 남겨 두었다.
  - 종료 시 회복을 적용한 뒤 `battleManager.ShowResult(...)`를 직접 호출해 승리 화면을 띄운다. 회복은 `CharacterStats.Heal`이 아니라 `currentHP`를 직접 더하고 `maxHP`로 클램프한다.
  - 마더 드래곤 여부는 **`enemyManager.currentEnemy is MotherDragon`**(클래스 판별)으로 본다. `MotherDragon`은 `EnemyBase`를 상속만 하는 빈 클래스(`02_Scripts/MotherDragon.cs`)이고, 옛 `EnemyBase.isMotherDragon` 플래그는 **삭제됐다** — 프리팹마다 체크박스를 켜는 대신 프리팹이 그 컴포넌트를 달면 되므로 켜는 걸 잊을 수가 없다.
  - ⚠️ `HandleAdvance`에 일시정지 가드가 없어 **`timeScale = 0`에서도 스페이스가 먹힌다**(`InputManager`가 `OnAdvance`를 `_inputEnabled` 가드보다 위에서 쏘므로 더욱 그렇다). 지금은 위 이유로 실질적인 영향이 없지만, 이 경로를 되살리면 같이 봐야 한다.
  - ⭐ **`OnBattleEnded`는 `isGameOver`가 false→true로 바뀔 때 <b>또는 `lastResultKind`가 바뀔 때</b> 발생한다.** `CheckGameState`가 `UpdateUI`마다 `ShowResult`를 다시 부르므로 걸러야 하는 건 맞지만, 그 반복은 **언제나 같은 kind**다. kind가 바뀌는 건 새로운 사건이다.
    - ⚠️ **"처음 끝났을 때만"으로 되돌리지 말 것.** 마지막 스테이지는 `Victory`로 결과가 이미 떠 있는 상태에서 보상을 고른 뒤 `LoadStage`가 `GameClear`를 띄우는데, `wasOver`만 보면 그 전환에서 이벤트가 통째로 묻힌다. 그러면 `DeckManager`가 타이머를 멈추지도 손패를 치우지도 않고 `ResultInputHandler`의 명령 카드도 안 떠서 **화면이 잠긴다**(실제로 났던 버그다).
  - 방어도 UI는 **아이콘 오브젝트가 텍스트를 자식으로 품는 구조**다(`PlayerDefIcon > PlayerDef`). 방어도가 0이면 아이콘째 꺼서 둘 다 사라진다. 아이콘 Image엔 아직 스프라이트가 없어 흰 사각형으로 보이는 게 현재 정상이다. 이 켜고 끄는 판단은 이제 `HealthBarUI.UpdateUI`가 한다.
- **`StageManager`** — `Start()`에서 시작 단어 지급 + `skillResolver.ResetRun()`(어썸 카운터 초기화, 안에서 `ResetStage`까지 이어진다) 후 `LoadStage(0)`. `RestartStage()`(사망 재시작)는 사전을 건드리지 않아 얻은 단어가 유지된다.
  - **일반 스테이지는 `enemyPrefabs`(현재 씬에서 `Dragon1~6.prefab` 6종) 중 하나를 랜덤으로 스폰한다.** `enemyPrefabs.Count == 1`이면 그대로 `[0]`을 쓰지만, 지금처럼 여러 장이면 **직전 스테이지와 같은 프리팹이 다시 뽑히지 않을 때까지**(`lastNormalEnemyPrefab`, 최대 20회 재시도) 다시 굴린다. `currentBattleIndex`가 4 또는 9면 보스전으로 보고 `motherDragonPrefab`을 스폰한다 — 이건 그대로다.
  - 적 스탯은 프리팹이 아니라 **`EnemyBase.ApplyScaling(체력, 공격력, 방어도)`**로 준다(스폰 직후 호출). 세 값을 `ComputeEnemyMaxHP` / `ComputeGrowthMultiplier`가 계산하고, **난이도 스텝도 그 자리에서 얹힌다** — 체력은 `DifficultyHPMultiplier`로 곱하고(곡선 모양은 그대로, 높이만) 공격력·방어도는 **증가율에 더한다**(그래서 첫 스테이지는 세 난이도가 같고 뒤로 갈수록 벌어진다). ⚠️ 이 세 줄이 전부 `if (!isBossBattle)` 안이라 **마더 드래곤은 자동으로 제외된다** — 3턴 스파링이라 체력 공식을 태우면 아웃로가 깨진다.
  - **스테이지 표시가 둘로 갈린다.**
    - `currentStageText`(상시 표시) — 문구가 인스펙터의 **`stageLabelFormat`**(`"STAGE {0}"`)과 **`bossStageLabel`**(씬에서 `"MAMA"`로 덮여 있다)에서 나온다. 코드에 박혀 있던 걸 걷어낸 자리다.
    - **`stageStartObject`**(등장 배너) — 이제 코드가 문구를 쓰지 않는다. `LoadStage`가 `SetActive(true)`로 켜기만 하고, **무엇이 적혀 있는지는 그 오브젝트(프리팹/애니메이션)가 통째로 갖는다.** 옛 `stageStartText`(TMP 라벨에 문구를 대입하던 것)는 삭제됐다.
    - 끄는 건 `BeginStageAfterDelay`가 `stageStartDelay` 뒤에 한다 — **`stageStartEffect`(`StageStartEffect`)가 있으면 축소·페이드아웃 퇴장 연출을 맡기고**(백그라운드로 흘러가며 턴 시작을 늦추지 않는다), 없으면 그냥 끈다. 등장 연출은 `StageStartEffect.OnEnable`이 알아서 재생하므로 켜기만 하면 된다.
    - ⚠️ 전체 클리어로 빠질 때도 이 배너를 꺼야 한다(`LoadStage`의 클리어 분기). 거기서는 퇴장 연출 없이 즉시 끈다 — 결과 창이 곧바로 덮으므로 축소되는 걸 볼 이유가 없다.
  - **BGM도 여기서 고른다** — `LoadStage`가 `isBossBattle`이면 `PlayBossBGM()`, 아니면 `PlayBattleBGM()`을 부른다. ⚠️ 코드 주석은 "이미 재생 중이면 알아서 무시됨"이라고 하지만 **실제로는 매 스테이지 처음부터 다시 재생된다**(아래 "알려진 이슈"의 `PlayBGM` 항목).
  - **스테이지 구성이 인스펙터 두 칸으로 정해진다** — `totalBattles`(현재 **13**, 보스 포함한 총 전투 수)와 `bossBattleIndices`(현재 `{4, 9}`). 게임 클리어 판정은 `currentBattleIndex >= totalBattles` 하나뿐이다.
    - ⭐ **마지막 전투(`totalBattles - 1`, 지금은 인덱스 12)는 `bossBattleIndices`에 없어도 자동으로 엔딩 대결이 된다.** `EnemyBase.isEndingBoss`가 서고, `LoadStage`가 **전투를 아예 건너뛰고** 엔딩 이벤트(`DialogueIds.EndingEvent`)를 재생한 뒤 게임 클리어로 넘어간다.
    - **결과 화면의 "최고 도달 스테이지" 분모는 이제 계산 프로퍼티 `TotalStages`다**(보스를 뺀 일반 스테이지 수). ⚠️ 예전엔 인스펙터에 손으로 적는 값이라 분자와 조용히 어긋났고 실제로 `11 / 12`가 뜬 적이 있다 — **다시 필드로 되돌리지 말 것.**
  - ⚠️ **`HandleBattleEnded`는 `battleManager.IsFinalResult`면 보상을 열지 않는다.** 전체 클리어는 플레이어가 살아 있어서 `player.currentHP <= 0` 검사에 안 걸리는데, 그대로 두면 **결과 화면이 뜬 뒤에 보상 창이 한 번 더 올라온다**(더 갈 스테이지가 없어 고른 카드를 쓸 곳도 없다). 마지막 스테이지의 보상은 그 직전 `Victory`에서 이미 받는다 — `OnBattleEnded`가 kind가 바뀔 때도 나가게 되면서 생긴 경로다.
  - **보상은 `NextStage()`가 아니라 `battleManager.OnBattleEnded`를 구독해 연다.** 적 HP가 0이 되는 순간(= 결과 화면이 뜨는 순간) `BeginRewardRound()`가 돌아 후보를 뽑고 `RewardCardView`로 펼치며 `RewardInputHandler.BeginSelection`을 연다. 패배(`player.currentHP <= 0`)에는 보상이 없다.
    - **한 라운드가 끝나면**(`OnSelectionFinished`) `TryConsumeBonusRound()`로 럭키 라운드가 남았는지 보고, 남았으면 `BeginRewardRound()`로 한 번 더 연다. 다 끝나면 `FinishReward()`가 **퇴장 연출**(`rewardCardView.PlayExit(keepIndex, …)`)을 걸고, 그게 끝난 뒤 `BeginAdvanceAfterReward()`가 **`rewardAdvanceDelay`(기본 0.6초) 뒤 `NextStage()`를 부르는 코루틴을 건다.** 연출 뒤에도 대기를 한 번 더 두는 건 "방금 얻은 카드를 볼 짧은 여유"라는 원래 의도를 유지하기 위해서다.
    - ⚠️ **`IsAdvancingAutomatically`는 `advanceRoutine != null`이 아니라 별도 플래그 `_advancingAfterReward`다.** 코루틴만 보면 **퇴장 연출이 도는 동안**(코루틴이 아직 안 걸린 구간) false가 되어 그 사이에 `다음` 안내가 잘못 깜빡인다. 플래그는 `FinishReward`에서 서고 `LoadStage`에서 내려간다.
    - ⚠️ **`FinishReward`에서 `NextStage()`를 곧바로 부르면 안 된다.** 이 경로는 `BattleManager.ShowResult` 안의 `OnBattleEnded`에서 시작되는데, 그 호출은 우리가 돌아간 **뒤에** 이어서 결과 화면을 그린다 — 스테이지를 먼저 갈아끼우면 **새 스테이지 위에 VICTORY 화면이 덮인다.** 코루틴으로 최소 한 프레임 미뤄 그 호출을 빠져나온 뒤에 넘긴다.
    - ⚠️ **`LoadStage`가 이 코루틴을 반드시 끊어야 한다.** 대기 중에 플레이어가 `다음`을 쳐서 먼저 넘어오면, 남은 코루틴이 뒤늦게 `NextStage()`를 한 번 더 불러 **스테이지를 하나 건너뛴다.**
    - **자동 진행 여부는 `IsAdvancingAutomatically`로 노출된다.** `BattleManager.ApplyResult`가 이걸 보고 `다음` 안내를 숨긴다 — 보상 선택 중(`IsSelecting`)과 같은 이유다. ⚠️ 반대로 **둘 다 아니면 안내를 반드시 띄워야 한다** — 배선이 빠져 자동 진행이 안 걸렸는데 안내까지 숨기면 칠 것도 없고 넘어가지도 않는 화면에 갇힌다.
    - **미보유 단어가 다 떨어지면 보상 창을 아예 열지 않고 곧바로 `FinishReward()`로 간다.** 그래야 자동 진행이 걸린다.
    - `rewardInputHandler`가 비어 있으면 **후보를 전부 지급하는 옛 동작으로 떨어지고 경고를 남긴다**(조용히 보상이 증발하는 것보다 낫다).
    - ⚠️ **`HandleBattleEnded`에서 `wordUnlockManager == null`로 조기 리턴하지 말 것.** 그 경우에도 `BeginRewardRound`가 `FinishReward`로 빠져 자동 진행을 걸어준다 — 앞에서 끊으면 승리 화면에 멈춘다.
  - 적을 스폰한 직후 `enemyHealthBarAnchor.Bind(...)`로 적 HP 바의 추종 대상을 넘긴다(뷰가 스스로 적을 찾지 않는다).
  - `LoadStage`는 적을 스폰한 **직후 곧바로 플레이어 턴을 열지 않는다.** 타이머를 멈추고 입력을 잠근 뒤(+ 이전 스테이지에서 쌓다 만 체인을 비운 뒤) `stageStartDelay`만큼 기다렸다가, `BeginStageAfterDelay`에서 **손패를 전부 새로 뽑고**(`CardSlotManager.RefillAll()`) 입력·타이머를 연다.
    - **이 대기는 스페이스로 넘길 수 있다**(`WaitOrSkip` — 위 입력 목록의 스페이스 항목 참조). ⚠️ **넘기는 것은 대기뿐**이고 그 뒤 절차(배너 퇴장 → 손패 리롤 → 입력·타이머 오픈)는 그대로 지나간다. `WaitForSeconds`와 같은 `Time.deltaTime` 기준이라 일시정지에서 함께 멈춘다.
    - ⚠️ `LoadStage`가 `startRoutine`을 끊을 때 `_waitingForStageStart`/`_skipStageStartRequested`도 같이 내린다 — 코루틴이 끊기면 `WaitOrSkip`의 정리 코드가 돌지 않아 플래그가 남는다.
    - 보스 인트로(`PlayBossIntroThenBegin`)의 대기들은 **아직 못 넘긴다.** 같은 `WaitOrSkip`으로 바꾸면 되지만 사이의 `WaitUntil`(게이트 닫힘·퇴장 연출 콜백)은 애니메이션이 끝나야 다음이 성립하는 자리라 그대로 둬야 한다.
  - 체인과 입력창은 대기 후가 아니라 **`LoadStage` 시점에 즉시** 비운다 — 새 적이 등장하는데 이전 조합 텍스트가 2초 더 남아 있으면 어색하기 때문.
  - 스테이지가 연달아 바뀌어도 겹치지 않도록 진행 중인 코루틴을 `StopCoroutine`으로 정리한다.

### 타이머 (`02_Scripts/Timer/`)

- **`TimerManager`** — 카운트다운. **기준 시간은 `BaseDuration` 계산 프로퍼티 하나가 정한다** — **언어가 기준을 잡고**(한국어 `baseDuration` 10초 / 그 외 `nonKoreanBaseDuration` 15초 — 라틴 표기가 길다) **난이도가 그 위에서 더하고 뺀다**(`secondsLostPerDifficultyStep` 3초). ⚠️ 순서를 뒤집어 난이도를 먼저 적용하고 언어로 갈아치우면 **난이도 항이 통째로 사라진다.** `RestartTurn`·`ResetToFull`이 둘 다 이걸 읽으므로 대기 중 게이지와 실제 카운트다운이 어긋나지 않는다. 이벤트가 **두 개**인 게 핵심이다:
  - `OnTimeChanged(remaining)` — 매 프레임(자연 감소 포함). 슬라이더 위치 갱신용.
  - `OnTimeAdjusted(delta)` — `AddTime`/`ReduceTime`로 **효과에 의해** 증감했을 때만. 색 반짝임용.
  - 이 둘을 합치면 정상 카운트다운도 매 프레임 "감소"로 잡혀 반짝임이 끝날 틈 없이 재시작되어 **항상 빨간색으로 고정**된다. 실제로 겪었던 버그다.
  - **만료 판정(`CheckExpired`)은 `Update`와 `AddTime` 양쪽에서 부른다.** 시간이 0이 되는 경로가 두 개이기 때문이다 — 자연 감소뿐 아니라 훅/어퍼컷(`TimerDelta: -1`)이 남은 1초를 깎아 0으로 만들 수도 있다. 예전엔 `Update`에만 있어서, 단어 효과로 0이 되면 `OnTimeExpired`가 영영 안 나가고 **타이머가 0에 멈춘 채 입력만 계속 열려 있는** 버그가 났다.
  - **`ResetToFull()`은 게이지를 최대치로 되돌리되 카운트다운을 시작하지 않는다.** 턴 전환·스테이지 시작 대기 동안 게이지가 0에 붙어 있지 않고 가득 찬 채 멈춰 있게 하기 위한 것이고, 실제 시작은 대기가 끝날 때 `RestartTurn()`이 한다. `_running = false`를 포함하므로 `StopTimer()`를 대체한다.
    - ⚠️ **`ResetToFull`은 `OnTimeAdjusted`를 쏘지 않는다.** 그 이벤트는 "단어 효과로 시간이 변했다"는 신호 전용이라, 여기서 쏘면 대기에 들어갈 때마다 `TimerView`가 초록색으로 반짝인다.
    - `Start()`도 `RestartTurn()`이 아니라 이걸 부른다. 프로젝트에 스크립트 실행 순서 설정이 없어서(`DefaultExecutionOrder` 없음) `StageManager.Start()`와의 순서가 정해져 있지 않은데, `TimerManager`가 나중에 돌면 카운트다운이 시작되어 **첫 스테이지 대기 동안 게이지가 줄어든다.** `ResetToFull`이면 어느 순서로 돌든 최종 상태가 "가득 참 + 정지"라 결과가 같다.
- **`UI/TimerView`** — 게이지 채우기(`UIStyle.SetFillAmount`) + 증가 초록 / 감소 빨강 반짝임. 남은 초를 보여주는 `remainingText`도 있다. **`Slider`가 아니다**(위 `04_UI` 절 참조).

### 씬 전환과 일시정지 (`02_Scripts/UI/`, `GameScenes.cs`)

- **`GameScenes`** — 씬 이름 상수만 담은 정적 클래스. 씬 전환은 전부 여기를 거친다.
- **`TitleMenu`** — 타이틀 버튼 3개 배선. `Awake`에서 **`Time.timeScale = 1f`로 되돌리는 게 핵심**이다(일시정지 상태로 타이틀에 돌아오면 멈춘 채로 뜬다). 종료는 `#if UNITY_EDITOR` 분기가 있어야 에디터에서도 반응한다. `옵션`은 이제 잠겨 있지 않고 `optionsPanel`을 켠다(`Awake`에서 먼저 꺼둔다). **닫기는 `OptionsPanel`이 자기 닫기 버튼으로 직접 처리한다** — 여는 쪽과 닫는 쪽이 다른 스크립트다.
  - **`게임시작`은 전투 씬이 아니라 `GameScenes.Intro`를 부른다**(BGM을 끄고 넘어간다). `Start()`에서 `PlayTitleBGM()`을 부르므로 타이틀로 돌아올 때마다 타이틀 BGM이 다시 걸린다.
- **`IntroManager`** (`02_Scripts/`) — `IntroScene`의 슬라이드쇼. `StorySlide { image, text, duration }` 배열을 인스펙터에 넣으면 순서대로 보여주고, 다 끝나면 **`GameScenes.Battle`로 넘어간다**(메서드 이름과 주석에 "타이틀"이라 적혀 있지만 실제 대상은 전투 씬이다).
  - **스킵은 아무 키나 `skipHoldTime`(기본 3초) 동안 누르고 있는 것**이다. 코드 주석은 스페이스바라고 하지만 실제로는 `Keyboard.current.anyKey`이고, 게이지(`skipGaugeFill`)의 `fillAmount`가 진행도를 보여준다. 손을 떼면 0으로 되돌아간다.
  - 이 프로젝트의 다른 UI 컨벤션을 따르지 않는다 — `public` 필드에 `[SerializeField] private`가 아니고, 문자열이 인스펙터에 있긴 하나 **한/영 두 벌이 아니라 한 벌뿐이라 `LanguageSettings`를 타지 않는다.** 손댈 일이 생기면 그때 맞추는 게 맞고, 새 코드의 본보기로 삼지 말 것.
- **`MenuKeyboardNavigator`** (`02_Scripts/UI/`) — 메뉴 한 벌을 키보드로 훑는 범용 컴포넌트. **타이틀 버튼 3개와 옵션 창이 각각 하나씩 붙여 쓴다.** 위/아래로 포커스를 옮기고(`items` 배열 순서 = 화면 위→아래 순서, 배치를 추측하지 않는다), 좌우로 `Slider` 값을 굴리고, 확인 키로 `Button`을 누른다. 포커스 표시는 `EventSystem.SetSelectedGameObject`에 맡기므로 **어떻게 보일지는 각 `Selectable`의 Transition이 정하고 코드가 관여하지 않는다.**
  - ⚠️ **`EventSystem`의 `Send Navigation Events`를 꺼야 한다.** 켜두면 내장 모듈이 같은 방향키·Enter를 함께 처리해 포커스가 두 칸씩 뛰거나 버튼이 두 번 눌린다. 마우스 클릭은 그 체크박스와 무관하다.
  - `InputManager`와 같은 이유로 **`Keyboard`를 직접 읽는다**(Input Action 에셋을 의도적으로 우회한다). 키 목록(`upKeys`/`confirmKeys`/`cancelKeys` 등)은 전부 인스펙터에 있다.
  - `blockedWhileActive`에 옵션 창을 넣으면 창이 떠 있는 동안 뒤쪽 메뉴가 움직이지 않는다. `cancelTarget`에 닫기 **버튼**을 넣어야 ESC/X가 버튼과 완전히 같은 경로를 탄다.
  - **켜진 뒤 `inputBlockDelay`(기본 0.5초) 동안 키를 읽지 않는다**(포인터 배치는 그 동안에도 계속 갱신한다 — 한 프레임이라도 엉뚱한 자리에 떠 있으면 눈에 띈다). 옵션 창을 여는 Space가 같은 프레임에 옵션 쪽 내비게이터까지 닿는 것을 막고, **씬이 바뀐 직후의 무의식적인 Enter**도 같이 걸러낸다 — 전투에서 `타이틀`을 치고 나온 플레이어가 습관적으로 Enter를 눌러 곧바로 게임이 시작되는 일이 있었다. 옛 "켜진 첫 프레임만 무시"는 한 프레임이라 그걸 못 막았다.
    - `wasPressedThisFrame` 기반이라 **키를 누른 채로 씬을 넘어와도 차단이 풀리는 순간 발동하지 않는다.** 풀린 뒤 새로 누른 것만 먹는다.
    - ⚠️ 새로 추가된 필드라 **씬·프리팹에 직렬화된 값이 없어 C# 초기화자(0.5)가 그대로 적용된다** — 인스펙터를 손대지 않아도 동작한다. 값이 거슬리면 `Option.prefab`/`TitleScene` 쪽에서 낮추면 된다.
  - `AdjustSlider`는 **일부러 `SetValueWithoutNotify`가 아니라 `value`로 넣는다** — `onValueChanged`가 돌아야 `OptionsPanel`이 볼륨을 적용하고 `%` 라벨을 갱신한다(`OnEnable`의 되비추기와 반대 방향이니 혼동하지 말 것).
- **`OptionsPanel`** (`02_Scripts/UI/`) — 타이틀 옵션 창. **순수 뷰**이고 값의 소유자는 `SoundManager`(볼륨)·`LanguageSettings`(언어)·`DifficultySettings`(난이도)다. 마스터/BGM/SFX 슬라이더 3개(전부 0~1, `Whole Numbers` 끄기)와 선택적 `%` 라벨, **언어 버튼**, **난이도 버튼**, 닫기 버튼.
  - **언어·난이도 버튼은 클릭과 좌우 방향키 둘 다 받는다** — `Update`가 `EventSystem.currentSelectedGameObject`를 보고 지금 포커스된 쪽만 굴린다(`ReadHorizontal` 하나를 공유). ⚠️ 예전엔 `languageButton.gameObject`를 **null 검사 없이** 읽어서 그 배선이 비면 창이 열려 있는 동안 매 프레임 NullReference가 났다 — 지금은 둘 다 막혀 있으니 버튼을 더 추가할 때도 같은 모양을 지킬 것.
  - ⚠️ **난이도 라벨은 언어를 타지 않는다**(`EASY`/`NORMAL`/`HARD`). 꾸밈만 `difficultyLabelFormat`(`< {0} >`)으로 씌운다.
  - ⚠️ 새 버튼은 **`MenuKeyboardNavigator.items`에도 넣어야** 키보드로 닿는다(배열 순서 = 화면 위→아래).
  - ⚠️ **`OnEnable`에서 저장값을 슬라이더에 되비출 때 `SetValueWithoutNotify`를 쓴다.** 평범한 `value =` 대입은 `onValueChanged`를 되쏘아 **방금 읽어온 값을 그대로 덮어쓴다.**
  - 구독/해제가 `OnEnable`/`OnDisable`이라 창을 여닫을 때마다 도는데, `OnDisable`에서 `SaveVolumes()`를 부른다 — **드래그 중엔 적용만, 닫을 때 한 번만 디스크에 쓴다.**
  - **언어 버튼은 `LanguageSettings.Toggle()`만 부른다.** 라벨은 "지금 언어"가 아니라 **"누르면 바뀔 언어"**를 보여준다(영어일 때 `한국어`, 한국어일 때 `English`). `languageButtonText`를 비워두면 버튼의 첫 `TMP_Text`를 알아서 찾는다. 닫기 버튼 라벨은 `closeKorean`/`closeEnglish`를 `Pick`으로 고른다.
- **`ResultInputHandler` : `CommandWordReceiver`** — **런이 끝난 결과 화면 전용**이다. `다시하기`/`retry`(→ `StageManager.RestartStage()`) · `카드`/`cards`(→ `CardCollectionPanel.Open()`) · `타이틀`/`title`(→ `ReturnToTitle()`) 세 장을 **손패 자리에 명령 카드로 띄우고** 타이핑으로 받는다. `TypingPriority.Result`, `WantsInput() => battleManager.IsGameOver`.
  - **`PauseManager`의 명령 카드 연출과 같은 구조다** — `resultCardsLayout`(**실제로 `PauseManager`와 같은 `PauseHand`를 가리킨다**, `Card Slot Manager`는 비워둘 것) + `commandCardPrefab`(보통 `Card.prefab`)에 `PlayEnter` + `BindStatic(CommandCardData, input)`. 자리가 비어 있는 건 `DeckManager`가 패배 시 손패를 `PlayCollapse`로 이미 무너뜨렸기 때문이다.
  - **`카드`는 카드를 치우지 않고 목록만 연다** — 닫으면 이 화면으로 돌아와야 하기 때문이다. 목록이 떠 있는 동안은 우선순위(`CardCollection = 30`)가 그쪽으로 넘어가 여기로 입력이 오지 않으므로 **상태를 따로 들 필요가 없다**(`PauseManager`와 같다). `cardCollectionPanel`/`cardsCard`를 비워두면 그 명령이 통째로 사라진다.
  - `battleManager.OnBattleEnded`를 구독해 카드를 띄운다. **⚠️ 손패가 다 치워질 때까지 먼저 기다린다**(`handFanLayout.IsLeaving`) — 명령 카드가 같은 아래쪽 자리로 떠오르므로, 안 기다리면 사라지는 카드와 올라오는 카드가 한 화면에서 엇갈린다. **패배는 무너짐(`PlayCollapse`), 전체 클리어는 가라앉기(`PlaySink`)**인데 기다리는 쪽은 둘을 구분하지 않는다. **고정 지연으로 어림잡지 말 것**: 무너짐 길이는 `collapseDuration + (장수-1) × collapseStagger`라 카드 수와 두 인스펙터 값에 따라 변하고, 하나만 바뀌어도 적어둔 숫자가 조용히 어긋난다. 그 뒤 `cardsAppearDelay`(0.8초)를 더 기다리고, `cardsAppearDuration`(0.6초)을 `PlayEnter`에 넘겨 손패보다 **천천히** 떠오르게 한다.
  - **뜨는 조건은 `battleManager.IsFinalResult`다**(패배·전체 클리어). 일반 스테이지 클리어에서는 `Targets` 세 칸이 전부 빈 문자열이지만(베이스가 빈 항목을 건너뛴다), **`WantsInput()`은 결과 화면 내내 true를 유지한다** — 그래야 우선순위상 이 수신자가 입력을 붙들어 **그 구간에 친 글자가 손패로 새어 조합이 쌓이지 않는다.**
    - ⚠️ 예전엔 `player.currentHP > 0`으로 판별해서 **전체 클리어에도 카드가 안 떴다.** `player` 필드는 그래서 삭제됐다.
    - ⚠️ `BattleManager.ShowResult`가 `lastResultKind`를 세운 **뒤에** `OnBattleEnded`를 쏘므로 핸들러 안에서 읽어도 값이 최신이다. 그 순서를 뒤집으면 카드가 아예 안 뜬다.
  - `BuildHint()`는 **빈 문자열**이다(단어가 카드로 뜨므로 안내가 중복). `PauseManager`와 같다.
  - 옛 구조로 되돌리지 말 것 — 예전엔 `TypedCommand retryCommand`/`다음`(`next`) 두 명령을 들고 안내 문구를 `BattleManager`가 제목 뒤에 붙였다. **`다음`은 삭제됐다**(승리는 자동 진행이라 쓰이지 않았다).
  - `ReturnToTitle()`은 `PauseManager`와 마찬가지로 `ClearInput()` + `Time.timeScale = 1f` + `StopBGM()`을 하고 씬을 부른다.
- **`PauseManager` : `CommandWordReceiver`** — ESC 토글(`InputManager.OnCancel`). ⚠️ **런이 끝나면(`battleManager.IsFinalResult` = 패배·전체 클리어) 멈출 수 없다** — 결과 화면이 이미 같은 `PauseHand`에 명령 카드를 띄우고 있어서 겹치면 두 화면의 카드가 서로를 밀어낸다(애초에 멈출 게임도 안 남았다). **`IsGameOver`가 아니라 `IsFinalResult`를 보는 게 핵심**으로, 보상을 고르는 중인 일반 클리어는 게임이 이어지므로 그대로 멈출 수 있어야 한다. `HandleCancel`의 검사 순서도 정해져 있다 — **① 목록 열려 있으면 닫기 → ② 이미 멈춰 있으면 풀기 → ③ 런이 끝났으면 리턴 → ④ 멈추기.** ②가 ③보다 먼저여야 "멈춘 채로 갇히는" 경우가 없다. **버튼이 아니라 명령 단어 타이핑으로 조작한다** — 멈춘 동안 `계속`/`카드`/`타이틀`(영어 모드면 `resume`/`cards`/`title`)을 친다(인스펙터의 `resumeCard`/`cardsCard`/`titleCard`, 각각 **`CommandCardData` 에셋**). `TypingPriority.Pause`, `WantsInput() => _isPaused`.
  - **에셋 참조라 프리팹에 그대로 저장된다** — 이 프로젝트에서 드문 경우다(대부분의 매니저 참조는 씬 오브젝트라 인스턴스 오버라이드로만 존재한다).
  - **`BuildHint()`는 빈 문자열을 돌려준다** — 단어가 카드로 화면에 그대로 뜨므로 안내 문구가 중복이다. `hintLabel`은 씬에서 떼어내도 된다.
  - **`_targets` 배열 순서가 곧 `OnCommandMatched`의 index이자 화면에 놓이는 명령 카드 순서다**(`ResumeIndex`/`CardsIndex`/`TitleIndex` 상수로 묶어두었다). 명령을 더할 땐 세 곳(배열 크기 · `Targets` · `ShowCommandCards`)을 같이 봐야 한다.
  - **`cardCollectionPanel`을 비워두면 `카드` 명령이 통째로 사라진다** — `Targets`의 그 칸이 빈 문자열이 되고(베이스가 빈 항목을 매칭·진행 판정 양쪽에서 건너뛴다) 안내에도 안 뜨고 명령 카드도 안 만들어진다. **쳐도 아무 일이 안 일어나는 단어를 남기지 않으려는 의도적 처리**다.
  - **`Time.timeScale = 0` 하나로 게임이 통째로 멈춘다.** 시간에 의존하는 코드가 전부 `deltaTime`/`WaitForSeconds` 기반이기 때문이다 — 턴 전환 대기, 쌓인 공격 재생, 스테이지 시작 대기, 말풍선, 타이머 감소. **개별 정지 로직을 만들 필요가 없다.** (예외는 일시정지 화면 자체의 연출뿐 — 컨벤션 절 참조.)
  - ⚠️ **일시정지 중에는 입력을 끄지 않고 오히려 켠다.** 명령 단어를 쳐야 하기 때문이다. 대신 멈추기 전 상태를 `InputManager.IsInputEnabled`로 기억해 두고, `Resume()`에서 **원래 잠겨 있었다면 도로 잠근다**(턴 전환 대기나 결과 화면에서 멈춘 경우). 이 복원을 빼면 열리면 안 될 타이밍에 타이핑이 열린다.
    - ⚠️ **`EnableInput()`과 별개로 `ClearInput()`을 따로 불러야 한다.** `EnableInput`은 이미 켜져 있으면 곧바로 리턴하면서 내부의 `ClearInput`까지 건너뛰는데, 일시정지는 보통 입력이 켜진 플레이어 턴 중에 걸린다 — 그때 치다 만 글자가 명령 단어에 섞인다.
  - **일시정지 중 손패 자리가 명령 카드로 바뀐다.** 기존 5장은 `PlayExit(-height)`로 가라앉히며 끄고, `Pause Canvas` 쪽의 별도 `commandCardsLayout`(`HandFanLayout`, **`Card Slot Manager`를 비워둘 것** — 자동 스폰 없이 이 스크립트가 명령 카드만 채운다)에 `계속`/`카드`/`타이틀` 카드를 `PlayEnter` + `BindStatic(CardBase, input)`으로 띄운다. 연결이 빠진 명령은 **카드 자체를 만들지 않는다**(빈 카드가 자리만 차지하면 부채꼴 배치가 어긋난다).
    - ⚠️ 끄기 전에 5장을 **스냅샷으로 저장해야 한다.** `HandFanLayout.Cards`는 매 프레임 활성 자식만 다시 모으는 리스트라 끄는 순간 목록에서 빠져 참조를 잃는다. 복원은 리스트 순서가 아니라 **카드 자신의 `SlotIndex`**로 한다(`centerOnTop`이 형제 순서를 바꾼다).
    - ⚠️ **`Resume()`은 `HideCommandCards()`를 `pausePanel.SetActive(false)`보다 먼저 부른다.** 패널을 먼저 끄면 그 아래 카드도 `activeInHierarchy == false`가 되어 코루틴이 "game object is inactive"로 실패한다. 같은 이유로 명령 카드는 퇴장 애니메이션 없이 바로 `Destroy`한다(걸어봤자 부모가 꺼지며 잘려 `Destroy`가 끝내 안 불리고 비활성 상태로 쌓이기만 한다).
  - `titleReveal`(**`TextGateRevealAnimation`**)이 `PAUSE` 제목을 가운데서부터 열어 보여준다 — `RectMask2D`의 폭을 키우고 양 끝 막대(`|`)가 따라가다 바깥으로 밀려나며 사라지는 2단계 연출. 셰이더가 아니다. **`PlayReverse()`는 같은 경로를 `t=1→0`으로 훑어 막대가 다시 모이며 덮는다**(`RewardCardView`의 퇴장 연출이 쓴다). ⚠️ 이 컴포넌트는 **아무도 `Play()`를 부르지 않으면 마스크가 닫힌 채(폭 0) 남는다** — 다른 화면에 붙였다면 그 화면을 켜는 쪽에서 반드시 불러줄 것(`BattleManager`의 `ResultPanelView.reveals`가 그 예다).
  - `PauseManager`는 **항상 켜져 있는 오브젝트**에 붙여야 한다. 일시정지 창 루트에 붙이면 그게 평소 비활성이라 ESC를 못 받는다.
  - `timeScale`은 씬을 넘어가도 유지되므로 `ReturnToTitle`이 1로 되돌리고 `StopBGM()`까지 한 뒤 씬을 불러온다.
- **`CardCollectionPanel` : `CommandWordReceiver`** (`02_Scripts/UI/`) — 일시정지 중 `카드`를 쳐서 여는 **보유 카드 목록**. `WordDictionary.Words`(= 이번 런에서 해금해 실제로 손패에 뜰 수 있는 카드)를 격자로 전부 펼치고, `닫기`/`close`를 치면 닫는다. `TypingPriority.CardCollection`(가장 먼저), `WantsInput() => _isOpen`.
  - **카드를 그리는 건 `CardView.SetCard` 하나뿐이다** — 손패·보상 화면과 같은 `Card.prefab`을 쓰고 `CardSlotView`는 꺼서 슬롯·타이핑 로직을 떼어낸다(`RewardCardView`와 같은 패턴). **세 화면에서 카드가 다르게 보이면 안 된다.**
  - **`PauseManager`와 같은 이유로 항상 켜져 있는 오브젝트(보통 `Pause Canvas`)에 붙여야 한다.** 여닫는 대상은 이 컴포넌트가 아니라 인스펙터로 받은 `panel`이다.
  - **`displacedUI`가 목록을 가리는 UI를 잠시 비켜나게 한다**(보통 명령 카드 줄과 입력창). 실제 로직은 공용 `UIDisplacement`가 갖고 있고(위 참조) 여기는 `useUnscaledTime: true`로 넘긴다 — 일시정지 위에서 열리기 때문이다. **결과 화면에서 열 때는 `timeScale`이 1이라 두 값이 같아 그대로 동작한다.**
  - `Close()`는 **`_isOpen = false`를 `ClearInput()`보다 먼저** 세운다. `ClearInput`은 "비었다"를 수신자에게도 디스패치하는데, 아직 열린 상태면 이쪽이 그 신호를 가로채 일시정지 메뉴의 오타 상태가 안 풀린다.
  - 열 때마다 `Rebuild()`로 새로 그린다(보상으로 카드가 늘어난다). 해금 단어가 늘어 화면을 넘치면 `columns`/`cellSize`/`cardScale`을 같이 줄일 것.

### 에디터 도구 — 이제 없다

한때 `02_Scripts/DeckManager/Editor/`에 카드 24장을 손으로 드래그하다 빠뜨리는 사고를 막는 1회성 도구 둘이 있었다(실제로 어퍼컷 11중복 + 3장 누락이 났던 적 있다). **24장이 다 만들어지고 `WordUnlockManager`에 다 들어간 뒤 둘 다 삭제됐고, `Editor/` 폴더 자체가 없다.**

- `CardDataSeeder`(`Tools > Deck Manager > Seed Missing Word Cards`) — 카드 `.asset`을 `AssetDatabase`로 생성했다. **지금은 카드가 에셋이 아니라 JSON 행이라 이 도구 자체가 의미가 없다** — 카드를 추가하려면 JSON에 행을 하나 더 적으면 된다.
- `WordUnlockPopulator`(`Tools > Deck Manager > Populate Word Unlock Manager`) — 씬의 `WordUnlockManager` 목록을 채웠다.

**둘 다 되살릴 이유가 없어졌다.** 카드도 시작 단어 목록도 JSON 한 파일에 있어서 대량 추가가 텍스트 편집이다. **시작 단어의 유일한 출처는 JSON의 `grantedAtStart`다.**

### 아직 없는 것

단어 강화/합성, 단어별 사용 횟수 제한, 저장/불러오기, `StageData` SO, **결과 창 버튼**(지금은 `다시하기`/`타이틀` 명령 카드 타이핑뿐), **힘(Power) HUD 표시**.

**단어 선택은 이제 있다.** 클리어 시 후보 3장 + `넘기기` 카드가 한 줄로 펼쳐지고 플레이어가 이름을 타이핑해 하나를 고른다(`RewardInputHandler` + `WordUnlockManager.RollRewardCandidates`/`ConfirmReward`). **마더 드래곤 스테이지에서는 왼쪽 끝에 `지우기` 카드가 붙어** 사전에서 한 장을 지울 수 있다(`CardDeletePanel`). 럭키는 판정에 성공한 순간 라운드를 쌓아두고(그때부터 수치 칸이 `보상됨`으로 바뀐다) 클리어할 때 창을 한 번 더 연다.

⚠️ **`minWordsToKeep`(기본 4)은 장수만 본다.** 사전에 5장이 있어도 그중 액션 카드가 하나뿐이면 그걸 지워 **런이 잠길 수 있다**(액션 단어로만 체인이 완성된다). 완전히 막으려면 "액션 카드가 1장이면 그 카드만 목록에서 제외"를 더해야 하는데 아직 없다. 지운 카드는 사전에 없으므로 **다시 보상 후보로 나올 수 있다**(의도).

**통계는 생겼지만 점수는 아니다.** `StatisticsManager`가 플레이 시간·타수·CPM·누적 피해·최고 스테이지를 모아 결과 창에 보여주지만, GDD 6장이 말하는 **점수 계산·랭킹은 없다.**

**GDD와 수치가 다른 곳**: 적 AI 확률이 GDD는 `70/30`인데 `EnemyTutorial.asset`은 `60/30/10`이고, GDD에 없는 `buffChance`(힘 증가)가 세 번째 행동으로 들어가 있다.

**단어 사전에서 빠진 것**: **페인풀**(출혈) 카드 에셋이 없다. ⚠️ `AttributeEffectType.Bleed` enum 값은 **삭제 금지** — 인덱스가 밀려 `Smart.asset`(`effectType: 5` = `CritMultiplier`)이 조용히 다른 효과가 된다.

**컬러풀은 이제 셋을 각각 굴린다.** `ResolvedAction.StatusEffects`가 리스트라 화상·마비·얼음을 `chancePercent`로 **독립적으로** 굴려 걸린 것을 전부 부여한다(셋 다 걸릴 수도, 하나도 안 걸릴 수도 있다). 옛 "화상 하나만" 단순화는 없어졌다. 적용·표시 계층은 원래부터 다중을 지원했다 — `StatusEffectManager._enemyEffects`가 `Dictionary`이고 `StatusIconRow`가 아이콘을 각각 켠다.

**옵션은 볼륨 3종 + 언어 + 난이도뿐이다.** 해상도·키 설정 같은 건 없고, 옵션 창은 **타이틀 씬에만** 있다(일시정지 중에는 열 수 없다 — 언어와 난이도는 그래야 하는 이유가 따로 있다. 위 `LanguageSettings`·`DifficultySettings` 참조).

**저장되는 건 볼륨·언어·난이도뿐이다.** `PlayerPrefs`의 `option.volume.*` 3개와 `option.language`·`option.difficulty`가 전부이고, **런 저장이 없어서 타이틀로 돌아가면 진행이 초기화된다.** 해금한 단어와 스테이지 진행이 전부 사라지고 시작 단어 3장부터 다시 시작한다. 의도된 현재 상태다(`StageManager.RestartStage`만 사전을 유지한다). `StatisticsManager`의 통계도 씬을 넘어가면 사라진다.

**연출은 양쪽 다 붙었다.** `PlayerBattleVisuals`가 돌진 → 펀치(`Punch1~4`) → 복귀를 재생하고, 그 사이 쌓인 공격이 하나씩 적용되며 HP가 계단식으로 줄어든다. 여기에 **피해 숫자(`FloatingDamageManager`)·피격 이펙트(`HitEffectManager`)·카메라 흔들림(`CameraShake`, 시퀀스당 한 번)** 이 붙는다. **적 공격도 이제 같은 모양의 돌진→타격→복귀를 한다**(`EnemyBase.MoveToPlayerCoroutine`/`MoveToOriginCoroutine`) — 예전엔 이게 없어 `turnChangeDelay`/`postAttackDelay`가 빈 자리로 남아 있었다. 캐릭터 윤곽선(`SpriteOutline.mat` + `SpriteOutlineUVSync`)도 플레이어와 적 양쪽에 붙어 있다. **스테이지 전환 쪽은 등장 배너(`stageStartObject` + `StageStartEffect`)와 배경 스크롤(`BackgroundScroller`)이 `stageStartDelay`를 채운다.** **보상 화면에는 퇴장 연출**(고른 카드만 남고 나머지는 떨어짐)이, **패배 화면에는 명령 카드 등장 연출**이 있다. 그 밖에 **턴 시작 배너**(`YourTurnBanner`)·**보스 타이틀 카드**(`BossTitleCardView`)·**전체 클리어 축포**(`UIConfettiBurst`)·**흰 화면 씬 전환**(`SceneWhiteFadeIn`)·**가드/힐 파티클**(`HealEffectManager`)이 붙었다 — 자세한 건 위 "연출 컴포넌트들" 참조.

**보유 카드를 확인할 수 있다.** 일시정지 중 `카드`를 치면 `CardCollectionPanel`이 사전에 든 카드를 전부 펼친다. **런 밖에서 보는 도감(전체 28장 중 해금 현황)은 없다** — 이건 지금 손에 든 것만 보여준다.

## 컨벤션

- C# 네임스페이스 없음 — 전부 전역 네임스페이스.
- 컴포넌트 간 의존은 인스펙터에서 손으로 연결하는 `[SerializeField]` 참조가 원칙이다. 런타임에만 알 수 있는 의존은 `Bind(...)` 메서드를 명시적으로 둔다(`CardSlotView` 참조) — 조회하지 말 것.
  - **예외가 일곱 있다: `SpeechBubbleManager` · `SoundManager` · `CameraShake` · `FloatingDamageManager` · `HitEffectManager` · **`HealEffectManager`** · `StatisticsManager`가 싱글턴**(`public static Instance`)이다. 다른 브랜치에서 머지되어 들어온 코드이며 나머지 프로젝트의 배선 방식과 어긋난다. **새 코드를 이 패턴으로 확장하지 말 것** — 넷이던 게 일곱이 됐으니 특히 주의.
    - `HealEffectManager`는 `HitEffectManager`와 같은 이유로 싱글턴이다(부르는 쪽이 `CombatManager`·`EnemyManager`라 프리팹마다 참조를 심어야 한다). ⚠️ **`Awake`가 `Instance = this`만 해서 중복을 스스로 정리하지 않는다** — `HitEffectManager`와 같은 조용한 실패 경로다.
    - 그나마 정당화되는 건 `HitEffectManager` 정도다(부르는 쪽이 적 프리팹마다 붙는 `CharacterStats` 계열이라 인스펙터 연결이면 프리팹 수만큼 같은 참조를 반복해야 한다). **그 조건에 해당하지 않으면 `[SerializeField]`로 갈 것.**
    - 지금은 **호출부가 전부 `!= null` 가드를 갖고 있다**(예전에 `BattleManager`가 `SoundManager.Instance`를 가드 없이 불러 씬에 없으면 `Start()`에서 예외가 나던 문제는 고쳐졌다). 씬에 매니저가 빠져 있으면 예외 대신 **조용히 아무 일도 안 일어난다** — 소리·흔들림·피해 숫자·피격 이펙트·통계가 안 나오면 씬에 오브젝트가 있는지부터 볼 것.
    - ⚠️ **`Awake`의 중복 처리 방식이 제각각이다.** `StatisticsManager`/`SoundManager`는 중복 인스턴스를 스스로 `Destroy`하지만 `HitEffectManager`는 `if (Instance == null) Instance = this;`만 해서 **두 개가 있으면 둘 다 살아남고 늦게 깬 쪽이 무시된다.** 머지 사고로 같은 매니저가 두 벌 들어오면 후자는 증상이 조용하니(위 `03_Prefabs/Managers/` 경고 참조) 씬의 오브젝트 수부터 셀 것.
    - `SoundManager`는 `DontDestroyOnLoad`까지 붙어 씬을 넘어 유지된다. 그래서 **두 씬 모두에 인스턴스가 있어야 한다** — 타이틀에서 시작하면 타이틀 쪽 인스턴스가 살아남고 전투 씬 쪽은 `Awake`에서 스스로 `Destroy`된다.
- 씬 오브젝트 참조는 프리팹 에셋에 저장되지 않는다. 프리팹이 씬 컴포넌트를 필요로 하면 스포너가 `Bind()`로 넘겨준다(`HandFanLayout` → `CardSlotView`의 `InputManager`).
- ⚠️ **매니저 프리팹의 인스펙터 연결은 프리팹이 아니라 씬 인스턴스에 있다.** `03_Prefabs/Managers/`의 매니저들은 서로와 씬 오브젝트(`player`/`enemySpawnPoint`/각종 UI)를 참조하는데, 그 참조는 프리팹 에셋에 담길 수 없으므로 전부 인스턴스 오버라이드로만 존재한다. 따라서:
  - 인스턴스에서 **Apply / Apply All을 누르지 말 것** — 씬 참조가 프리팹 쪽에서 null이 되고, 그 프리팹을 다시 인스턴스화하면 "아무 일도 안 일어남" 상태가 된다. 프리팹은 Override 상태로 두는 게 정상이다.
  - 프리팹을 다시 씬에 끌어다 놓으면 참조가 하나도 안 붙어 온다. 연결을 손으로 전부 다시 채워야 한다.
  - 매니저에 `[SerializeField]`를 추가했다면 프리팹이 아니라 **씬 인스턴스에서** 채우고 `SampleScene.unity`를 커밋할 것.
- 로직 vs 뷰 분리: "매니저"가 상태와 판단을 소유하고, "뷰"(`InputFieldDisplay`/`CardSlotView`/`WordChainView`/`TimerView`/`StatusIconRow`/`PendingActionView`)는 그걸 UI에 비추기만 하며 게임 판단을 하지 않는다.
- ### ⭐ 화면에 나가는 글자와 이미지는 **예외 없이 인스펙터에서 바꿀 수 있어야 한다**

  이 프로젝트의 **기본 사양**이다. 문구 하나 고치려고 스크립트를 열고 컴파일을 기다리는 일이 없어야 하고, 기획·아트가 코드를 몰라도 화면을 손볼 수 있어야 한다. **새 UI를 만들 때 가장 먼저 지킬 규칙이다.**

  | 무엇을 | 어떻게 |
  |---|---|
  | 제목 + 이미지 + 글자색 한 덩어리 | **`ScreenPresentation`** (`titleKorean`/`titleEnglish`/`image`/`titleColor`) — 보상·보유 카드·삭제 창이 쓴다. ⚠️ **결과 화면은 예외로 프리팹이 겉모습을 통째로 갖는다**(`ResultPanel.prefab`) |
  | 타이핑하는 명령 단어 | **`CommandCardData` 에셋** (이름·설명·수치 한/영 6칸). 화면엔 카드로 뜬다 |
  | 안내 문구가 붙는 명령 단어 | **`TypedCommand`** (단어 + 안내를 한 묶음으로 — 단어를 바꿔도 안내가 옛 상태로 안 남는다). 지금은 `CardCollectionPanel.closeCommand` 하나뿐 |
  | 결과 화면 한 벌(패널+통계+게이트) | **`ResultPanelView`** — 결과 종류마다 하나씩. 겉모습은 프리팹 변형(`DefeatPanel`/`GameClearPanel`)이 갖고 코드는 켜기만 한다 |
  | 결과 통계의 라벨·숫자 | **`ResultStatsView`** — 라벨과 값을 각각 다른 `TMP_Text`로 받으므로 배치·폰트를 따로 디자인해도 코드를 안 고친다 |
  | 카드 프레임·배지 스프라이트 | `Card.prefab`의 `CardView` 인스펙터(`actionFrame`/`defaultFrame`/`commandFrame`/배지 3종) — **한 곳만 고치면 손패·보상·일시정지·목록에 동시에 적용된다** |
  | 카드 이름·설명·수치 칸 | ⭐ **`04_Data/Resources/CardLocalization.json`** (5개 언어 × 34장). 에셋이 아니라 여기다 — 에셋에 남은 텍스트 키는 죽은 값이다. 수치가 들어가는 칸은 JSON 쪽 문구를 **포맷 문자열**로 쓰고 런타임 값을 끼운다(아래) |
  | 대사(마더 드래곤과의 대화 전체) | ⭐ **`04_Data/Resources/DialogueLocalization.json`** (5개 언어 × 6묶음). 읽는 창구는 `DialogueDatabase`, id는 `DialogueIds` 상수 |
  | 난이도 이름 | **`DifficultyLabels`**(`[Serializable]` 값 묶음) — 쓰는 쪽이 필드로 든다. 언어를 안 타지만 **그래도 인스펙터에 있어야 한다** |
  | 그 외 라벨 | `[SerializeField]` 한/영 두 벌 + `LanguageSettings.Pick(...)` |

  **하면 안 되는 것**: 화면에 나갈 문자열을 코드에 박기, 포맷 조각(`"됨"`·`"ED"` 같은 접미사)만 코드에 두기, 스프라이트를 코드에서 `Resources.Load`로 집기.

  ⚠️ **카드와 대사의 JSON은 이 규칙의 위반이 아니라 의도된 예외다.** 이 규칙의 목적은 "문구 하나 고치려고 코드를 열고 컴파일을 기다리지 않기"인데 JSON은 그걸 그대로 만족한다 — 텍스트 파일이라 컴파일이 필요 없고 기획·번역 담당이 코드를 몰라도 고친다. **언어가 다섯이라 인스펙터 방식이면 칸이 5벌씩 필요**하고, 그러면 값이 프리팹 기본값과 씬 오버라이드로 흩어져 **실제로 화면에 나오는 값을 파일만 봐서는 알 수 없게 된다**(엔딩 대사와 카드 시작 단어에서 실제로 겪었다). 번역가에게 파일 하나로 넘길 수 있다는 이점도 크다.

  ⚠️ **아직 이 규칙을 반만 지키는 곳**(고칠 때 같이 정리할 것): `StageManager.stageLabelFormat`/`bossStageLabel`과 `IntroManager.slides`는 인스펙터에 있지만 **한 벌뿐이라 언어를 안 탄다.**

- **표시 문자열에 한글을 직접 박지 말 것.** 두 언어를 다 지나가는 문자열이면 `[SerializeField]` 두 개(`xxx`/`xxxEn`)를 두고 `LanguageSettings.Pick(...)`으로 고르거나, 코드에 박아야 하면 `LanguageSettings.IsEnglish` 삼항으로 갈라 쓴다(`StatusEffectManager.GetDisplayName` 참조). **`Pick`에 `owner`/`fieldName`을 넘겨야 누락 경고가 어느 필드인지 알려준다.**
- **수치가 들어가는 문구는 포맷 문자열로 쓴다.** 글자만 고쳐두면 인스펙터에서 값을 바꿨을 때 문구가 옛 숫자로 남고, **카드에 적힌 숫자와 실제 동작이 어긋나는 건 곧 버그로 보인다.**
  - `CardBase.Fill(text, args)`이 공용 헬퍼다 — 포맷 자리(`{`)가 없으면 **원문을 그대로** 돌려주고, 자리 번호가 인자 수를 넘어도 원문으로 떨어진다(카드가 통째로 비어 보이는 것보다 낫다).
  - `AttributeCardData.Description` → `{0}`=`chancePercent`, `{1}`=`value` · `ActionCardData.Description` → `{0}`=`comboMinHits`, `{1}`=`comboMaxHits`, `{2}`=`comboChancePercent` · **`ModifierCardData.Description` → `{0}`=`value`, `{1}`=`CurrentTurnBonus`(퍼펙트)** · `StatsLabel` 쪽은 어썸 `"+{0}"`, 퍼펙트/니킥/촙/박치기 `"{0}"`.
  - ⚠️ **`ModifierCardData`에는 오래도록 `Description` 재정의가 없었다.** 그래서 그 계열(슈퍼/울트라/하이퍼/메가톤/파워/퀵/럭키/어썸/퍼펙트)만 설명에 `{0}`을 적으면 값이 채워지는 게 아니라 **중괄호가 그대로 화면에 나왔고**, 수치를 글자로 박아둘 수밖에 없었다. 지금은 채워진다 — **카드 계열을 새로 만들면 `Description` 재정의를 빠뜨리지 말 것.**
  - **상태가 이분법이면 포맷 대신 완성된 문구를 두 벌 둔다** — 럭키의 JSON `koLabel`(`보상`)과 **`koLabelPending`**(`보상됨`)이 그 예다. `{0}`에 `"됨"`을 끼우는 방식이면 그 조각이 코드에 박혀 JSON에서 못 바꾼다. ⚠️ 이 두 칸은 예전에 `ModifierCardData`에 **에셋 필드로 한/영 두 벌만** 있었고 그래서 불/스/일에서 럭키만 튀었다 — 에셋으로 되돌리지 말 것.
- 이벤트는 평범한 C# `event Action`/`event Action<T>`, `OnEnable`에서 구독하고 `OnDisable`에서 해제.
- 새 스크립트는 `[SerializeField] private` + `[Header]`/`[Tooltip]`, 식 본문 읽기 전용 프로퍼티, 한글 주석. 편집 중인 파일의 스타일에 맞출 것. (`BattleManager`/`StageManager`/`EnemyManager`는 이 컨벤션보다 먼저 작성된 코드라 스타일 참고 대상이 아니다.)
- 인스펙터 참조가 비어 있으면 조용히 `return`하지 말고 필드명을 담은 `Debug.LogWarning(..., this)`를 남길 것 — 에디터에서 조용한 실패는 진단이 매우 어렵다. **실제로 이번 프로젝트에서 연결 누락으로 인한 "아무 일도 안 일어남" 버그가 여러 번 났다.**
- 이름이 의도적인 경우가 있다(씬의 `Deck Manager` GameObject는 공백 포함). 과거 진짜 오타(`InputManger`, `DeckManger.cs`)는 이미 수정됐으니 추가로 이름을 바꾸지 말 것.
- **"일시정지 중인가"는 `PauseManager` 참조 대신 `Mathf.Approximately(Time.timeScale, 0f)`로 판단한다**(`CardInputHandler.WantsInput`, `BattleManager`의 디버그 킬스위치). `PauseManager`는 씬 오브젝트고 `CardInputHandler`는 프리팹 안이라, 참조로 엮으면 씬 인스턴스 오버라이드가 늘어나기만 한다. 새로 같은 판단이 필요하면 이 방식을 따를 것.
- **타이핑을 받는 새 화면은 `TypingReceiver`(안내 문구가 있으면 `CommandWordReceiver`)를 상속할 것.** `InputManager.OnCharacterEntered`/`OnCompositionChanged`를 직접 구독해 자기 차례를 스스로 판별하지 말 것 — 그 구조에서 실제로 입력이 서로 새는 버그가 났고, 그래서 우선순위 단일 디스패치로 바꿨다. 그 두 이벤트는 이제 **뷰 전용**이다.
- **`InputManager.CurrentInput`/`Composition`을 폴링해 연출을 그린다면 "지금 내 차례인가"를 먼저 볼 것** — 수신자는 `HasTypingFocus`, 카드처럼 주인을 모르는 뷰는 `InputManager.IsTypingTarget(word)`다. 우선순위는 글자만 가르고 입력창은 전역이라, 이 검사가 없으면 **입력을 못 가져간 화면의 카드가 같이 움직인다**(위 입력 파이프라인 절의 ⭐ 항목).
- **시간에 의존하는 새 코드는 `Time.deltaTime`/`WaitForSeconds`를 쓸 것.** 일시정지가 `timeScale = 0` 하나로 성립하는 게 그 덕분이다. `unscaledDeltaTime`/`WaitForSecondsRealtime`을 쓰면 일시정지 중에도 계속 돌아 그 전제가 깨진다.
  - **의도된 예외는 다섯이고 전부 "멈춘 화면 위에서 돌아야 하는 것"이다** — `InputManager.ApplyImeMode`의 IME 쿨다운, `CardSlotView`의 들림·교체·무너짐·가라앉기 애니메이션(일시정지 명령 카드로 재사용된다), `TextGateRevealAnimation`(`PAUSE` 제목이 `timeScale`이 0이 되는 순간에도 끝까지 재생돼야 한다), `CardCollectionPanel`의 비켜나기 애니메이션(목록이 일시정지 위에서 열린다 — 같은 `UIDisplacement`를 쓰는 `CardDeletePanel`은 `useUnscaledTime: false`다), `MenuKeyboardNavigator`의 키 반복·포인터 이동(일시정지 메뉴에도 붙일 수 있어야 한다). 새로 예외를 만들려면 같은 이유가 있어야 한다.
    - 반대 사례가 **`CardDeletePanel`**이다 — 겉보기엔 `CardCollectionPanel`과 같은 격자 창이지만 `timeScale`이 1인 보상 구간에서만 뜨므로 **`Time.deltaTime`을 쓴다.** 비슷해 보인다고 따라 쓰지 말고 "그 화면이 멈춘 위에서 도는가"로 판단할 것.

## 알려진 이슈

- **컴파일이 한 파일에 걸려 통째로 멈춘 적이 있다 — 해결됨.** `CameraShake.cs`에 `= 1.5 f;`(숫자와 `f` 접미사 사이 공백)가 커밋된 적이 있고(`cfc7957`), 단일 어셈블리라 **그 파일 하나 때문에 모든 스크립트가 컴파일되지 않았다.** CI도 터미널 컴파일 경로도 없어 에디터를 열기 전까지 드러나지 않는 종류의 사고다 — 스크립트를 고친 뒤에는 에디터 콘솔에서 컴파일 통과를 눈으로 확인할 것.

- **머지가 남긴 잔재들.** 브랜치 4개가 같은 씬·프리팹을 건드리다 보니 아래가 쌓였다. 전부 지금 당장 깨지진 않지만, "왜 이게 두 개지?" 싶을 때 여기를 먼저 볼 것.
  - `Assets/_Recovery/`의 크래시 복구 씬 두 개(`0.unity`, `0 (1).unity`)가 커밋되어 있다. 죽은 에셋의 마지막 참조가 여기 남아 "아직 쓰인다"고 착각하게 만든다.
  - `Assets/03_Prefabs/Actions.prefab`은 **아무 씬·프리팹도 참조하지 않는다**(대체품은 `ThinkingBubble.prefab`). 같이 죽어 있던 `StatusEffectView.cs`는 삭제했다.
  - `StageManager.prefab`의 `enemyPrefabs` 기본값(3칸) 중 2번째 항목이 삭제된 `strongEnemy`의 **깨진 GUID**다(씬 인스턴스가 배열 크기를 6으로 덮어써 `Dragon1~6.prefab`으로 대체되어 있어 가려져 있다).
  - ~~`EventManager`의 영문 대사 배열이 비어 있다~~ — **해결됨.** 대사가 전부 `DialogueLocalization.json`으로 옮겨져 5개국어가 된다. 프리팹에 있던 `플레이스홀더텍스트0/1`도 같이 사라졌다(그건 **씬 오버라이드에 가려 화면에 나온 적이 없던** 값이다).
  - 저장소 루트에 `MERGE_NOTES.md`와 `SYLEE_머지시_변경사항.md`가 커밋되어 있다. 특정 머지 시점의 메모라 **지금 코드 상태와는 어긋날 수 있다** — 판단 근거로 삼지 말 것.
  - ⚠️ **`AGENTS.md`는 이 파일(`CLAUDE.md`)의 복사본이다.** 3번째 줄(도구 이름)만 다르고 나머지는 **한 줄도 빠짐없이 같아야 한다** — 실제로 한쪽만 갱신해 `Dragon1~6`·`enemyPrefabs` 대목이 갈라진 적이 있고, 그러면 다음 사람이 옛 쪽을 읽는다. 확인은 `diff <(tail -n +4 CLAUDE.md) <(tail -n +4 AGENTS.md)`가 비는지로 하고, **한쪽을 고쳤으면 그 자리에서 복사해 맞출 것**(`sed`로 3번째 줄만 바꿔 덮어쓰면 된다).
  - ⚠️ 저장소 루트에 **`BackgroundScroller.dll`**(300KB)이 커밋되어 있다(`c596a74 Merge complete`). `Assets/` 밖이라 Unity가 읽지도 않는 빌드 잔재이며, 같은 이름의 실제 스크립트는 `Assets/02_Scripts/BackgroundScroller.cs`다. 이 DLL을 코드 출처로 착각하지 말 것.
  - `Assets/01_Arts/Material/UIStyle.mat`과 `UnderwaterGodray.mat`(+ 짝인 `UnderWaterGodRay.shader`)은 참조가 없다. 현역인 `UnderwaterGodRaySprite` 쪽과 이름이 헷갈리니 주의.
- **인덱스 9 보스전 도달 불가 — 해결됨.** `LoadStage`의 `currentBattleIndex >= totalStages` 검사가 빠지고 `>= totalBattles`(현재 13) 하나만 남아, 두 번째 보스전(인덱스 9)이 실제로 실행된다. **옛 `totalStages` 필드도 이제 없다** — 결과 화면의 "최고 도달 스테이지" 분모는 `totalBattles`와 `bossBattleIndices`에서 파생되는 계산 프로퍼티 `TotalStages`라 전투 구성을 바꿔도 저절로 따라온다(위 `StageManager` 참조). **다시 필드로 되돌리지 말 것** — 손으로 적던 시절에 분자와 조용히 어긋나 `11 / 12`가 뜬 적이 있다.
- **적이 죽는 경로가 두 개로 갈려 있던 문제 — 해결됨.** `CharacterStats.Die()`는 `Destroy(gameObject)`지만 `BattleManager.CheckGameState`는 `SetActive(false)`로 비활성화만 하고, 이벤트 스테이지에서는 대사가 끝날 때까지 `IsGameOver`가 false로 남는다. **`DeckManager.IsEnemyDefeated()`가 `null` · `!activeInHierarchy` · `currentHP <= 0` 셋을 함께 보는 단일 판정**이 되어 `RunTurnTransition`과 `PlayPendingActions` 양쪽에 걸려 있다. ⚠️ 적 상태를 새로 판단해야 하면 `currentEnemy == null`만 보지 말고 이 메서드를 쓸 것 — **두 소멸 경로 자체는 그대로 남아 있다.**
- ⚠️ **`HPBarUI.cs`의 클래스명이 `HealthBarUI`다.** Unity는 MonoBehaviour의 파일명과 클래스명이 같아야 하므로 **`Add Component`로는 새로 붙일 수 없다.** 다만 `HPBar.prefab`에 이미 직렬화되어 있어 프리팹을 인스턴스화하면 정상 동작한다 — 새로 붙일 일이 생기면 파일명을 `HealthBarUI.cs`로 바꾸는 쪽이 호출부를 안 건드려 간단하다.
- **`BattleManager.prefab`의 `playerHealthBar`가 `{fileID: 0}`이다.** 씬 오브젝트 참조라 프리팹에 저장될 수 없어서 정상이며, 실제 연결은 **씬 인스턴스 오버라이드**에 있다. 프리팹에서 `Apply`를 누르면 이 null이 확정되어 배선이 날아간다.
- **`MotherDragon.prefab`의 체력이 의도대로 나오지 않는다.** 프리팹에 `maxHP: 9999`가 박혀 있지만 `enemyData`가 `EnemyTutorial.asset`(maxHP 150)으로 연결돼 있어 `EnemyBase.Start()`가 덮어쓴다. 마더 드래곤은 3턴을 버텨야 스파링 연출이 성립하므로 **`enemyData` 연결을 비우는 것이 맞다** — 그러면 `base.Start()` 폴백으로 9999가 유지되고, `EnemyManager`의 세 메서드가 `enemyData == null`에서 조용히 리턴해 공격·방어·버프도 하지 않는다(스파링 상대로 적절하다). 전용 `EnemyData`를 새로 만들 필요는 없다.
- **FMOD 뱅크는 이제 있다.** `Assets/05_Sounds/FMOD/StreetTyperFMOD/`에 FMOD Studio 프로젝트(`.fspro`)와 빌드된 뱅크 4개(`Master`/`Master.strings`/`BGM`/`SFX`)가 들어와 있고, `FMODStudioSettings.asset`의 `sourceBankPath`가 `Assets/05_Sounds/FMOD/StreetTyperFMOD/Build`를 가리킨다. `BattleManager.prefab`의 `attackSound`/`battleBGM`도 실제 이벤트 GUID로 채워져 있다.
  - 이벤트는 **10개**다 — `BGM`·`Main`·`MommyDragon`(→ `bus:/BGM`), `Punch1~3`·`Card1~3`·`Input`(→ `bus:/SFX`). 그룹 버스 2개(`BGM`/`SFX`) 외에 VCA는 없다. 어느 이벤트가 어디 붙는지는 `SoundManager`의 인스펙터 필드(`titleBGM`=`Main` / `battleBGM`=`BGM` / `bossBGM`=`MommyDragon` / `punchSounds[]`/`cardUseSounds[]`/`wordCompleteSound`)에서 정해진다.
  - ⚠️ **뱅크(`.bank`)는 빌드 산출물인데 저장소에 커밋된다.** FMOD Studio에서 믹서를 바꿨으면 `File > Build`까지 하고 갱신된 `.bank` 4개를 함께 커밋해야 한다. 라우팅만 바꾸고 빌드를 빠뜨리면 Unity 쪽에서는 아무것도 달라지지 않는다 — 실제로 겪었다. 뱅크 파일의 수정 시각이 `Metadata/` 변경보다 오래됐으면 빌드를 안 한 것이다.
  - `Assets/StreamingAssets`는 **git에 추적되지 않는 게 정상이다** — `ImportType: 0`(StreamingAssets)이라 FMOD가 임포트/빌드 시점에 뱅크를 복사해 넣는다(그래서 작업 트리에는 `.bank` 4개가 실제로 들어 있다). 손으로 채우지도, 커밋하지도 말 것.
  - ⚠️ **두 씬 모두 FMOD `StudioListener`가 없다**(0건). 3D 사운드(`PlaySFX(event, position)`)를 쓰려면 `Main Camera`에 붙여야 한다. 지금 실제로 쓰이는 건 2D 오버로드뿐이라 드러나지 않는다.
  - `Assets/Plugins/FMOD/platforms/mac/**/Info.plist`가 체크아웃만 해도 수정된 것으로 잡히는 일이 있다(플랫폼 간 차이).
- **타이틀로 돌아왔을 때 전투 BGM이 남던 문제 — 해결됨.** `PauseManager.ReturnToTitle`이 `StopBGM()`을 부르고, `TitleMenu.Start`가 `PlayTitleBGM()`으로 타이틀 BGM을 새로 건다. `DeckManager.HandleBattleEnded`도 패배 시 `StopBGM()`한다.
- ⚠️ **보스 BGM이 실제 플레이에서는 재생되지 않을 가능성이 높다.** `bossBGM`(`event:/MommyDragon`)이 **`SampleScene`의 `SoundManager` 인스턴스 오버라이드에만** 채워져 있고 프리팹과 `TitleScene` 인스턴스에는 비어 있다. 그런데 `SoundManager`는 `DontDestroyOnLoad` + "먼저 깬 쪽이 이긴다"(`Awake`에서 나중 인스턴스가 스스로 `Destroy`)라, **타이틀부터 시작하면 살아남는 건 `bossBGM`이 빈 타이틀 쪽 인스턴스**다 → `PlayBossBGM()`이 `EventReference.IsNull` 가드에 걸려 조용히 아무 일도 안 한다. `SampleScene`을 직접 Play할 때만 들린다. **고치려면 `bossBGM`을 `SoundManager.prefab` 쪽에 채울 것**(씬 오브젝트 참조가 아니라 에셋 참조라 프리팹에 저장된다 — 매니저 프리팹에 Apply 금지 규칙의 예외에 해당한다).
  - ⚠️ **이건 `SoundManager` 전체에 걸리는 함정이다.** `titleBGM`/`battleBGM`/`punchSounds[]` 등은 프리팹에 값이 있어서 지금 동작하는 것뿐이다 — **`SoundManager`에 새 `EventReference` 필드를 추가하면 반드시 프리팹에 채울 것.** 씬 인스턴스에서만 채우면 타이틀부터 시작하는 실제 경로에서 증발한다.
- ⚠️ **`SoundManager.PlayBGM`의 "같은 BGM이면 그대로 둔다" 가드가 죽어 있다.** 중복 판정(`currentBGM.Guid == bgmEvent.Guid && bgmInstance.isValid()`)보다 **먼저** `StopBGM()`을 부르는데, 그 안에서 `currentBGM`이 초기화되고 인스턴스가 release되므로 조건이 절대 참이 되지 않는다. 그래서 `StageManager.LoadStage`가 스테이지마다 `PlayBattleBGM()`을 불러 **같은 곡이 매번 처음부터 다시 재생된다**(주석의 "이미 재생 중이면 알아서 무시됨"은 사실이 아니다). 고치려면 앞쪽 `StopBGM()` 한 줄을 지우면 된다 — 뒤쪽에 같은 호출이 이미 있다.
- **옵션 창의 SFX 슬라이더는 타이틀에서 미리듣기가 안 된다.** 타이틀 씬에서 SFX를 재생하는 코드가 없어서 움직여도 들리는 변화가 없다(값은 정상 반영된다). 미리듣기를 붙이려면 슬라이더를 놓을 때 `event:/Kick`을 한 번 재생하면 된다.
- **방어도에 상한이 없고, 플레이어와 적의 초기화 규칙이 일부러 다르다.**
  - **플레이어**: 턴마다(`DeckManager.RunTurnTransition`의 `player.defense = 0`) + 스테이지마다(`StageManager.LoadStage`·`RestartStage`) 비운다. 턴마다 비우지 않으면 **가드를 반복하는 것만으로 영구 무적**이 된다.
  - **적**: 어디서도 비우지 않는다. 줄어드는 건 플레이어가 때릴 때(`TakeDamage`가 흡수한 만큼)와 어퍼컷(`BreaksEnemyDefense`)뿐이고, 새 적은 `EnemyBase.ApplyScaling`이 0에서 시작시킨다. **이건 버그가 아니라 확정된 밸런스다 — 턴마다 초기화하지 말 것**(그러면 `EnemyData.defendChance` 30%가 사실상 무의미해진다).
  - ⚠️ **그래서 적이 Defend를 연달아 고르면 방어가 쌓여 한동안 HP가 전혀 안 줄어든다**(`defensePower`가 5라 두 번이면 10). "공격이 방어보다 큰데 체력이 안 닳는다"로 보이지만 정상 동작이다 — 실제로 이걸 버그로 오해한 적이 있다. 확인은 `CharacterStats.TakeDamage`의 로그로 한다(아래).
  - 한 턴 안에서 `AddDefense`를 누적하는 데는 여전히 상한이 없다. GDD에 규칙이 없어 그대로 두었다.
- **공격 말풍선이 꺼져 있다.** `DeckManager.PlayPendingActions`에서 `battleManager.OnPlayerActionResolved(BuildBubbleText(...))` 호출이 **빠졌다**(그 자리에 주석으로 경위만 남아 있다). 타격 수치는 이제 말풍선이 아니라 `FloatingDamageManager`가 띄운다. `BattleManager.OnPlayerActionResolved`와 `DeckManager.BuildBubbleText`는 살아 있지만 **아무도 부르지 않는 죽은 코드**다.
  - ⚠️ **그 자리에는 대신 `battleManager.UpdateUI()`가 있다. 같이 지우지 말 것.** `OnPlayerActionResolved`는 말풍선과 **UI 갱신 두 가지**를 했는데, 말풍선을 없애려고 호출을 통째로 주석 처리했다가 갱신까지 사라진 적이 있다. 그때 증상은 "**쌓인 공격이 한 번에 적용된다**"였다 — 수치는 한 대씩 정상적으로 깎이는데 HP 바만 그대로 있다가 턴 끝에 한 번에 뚝 떨어진 것이다.
  - ⚠️ **이 `UpdateUI()`는 반드시 럭키(`GrantsLootBonus`) 처리보다 뒤에 있어야 한다.** `UpdateUI` → `CheckGameState` → `ShowResult` → `OnBattleEnded`가 한 호출 안에서 이어지고 그 안에서 `StageManager`가 클리어 보상을 지급하므로, 앞으로 옮기면 럭키 보너스가 다음 스테이지 보상에 얹혀 **로그만 찍히고 카드는 3장 그대로**가 된다.

- **한/영 IME — 해결됨. 이제 OS IME로 한글을 조합하지 않는다.**
  - 마지막까지 남아 있던 증상은 **"한/영 상태가 어긋나면 입력이 통째로 죽는다"**였고, 원인은 한글 경로가 플랫폼별로 둘인데 **요구하는 한/영 상태가 정반대**였던 것이다(데스크톱 OS IME = 한글 상태 / WebGL 합성 조합 = 영문 상태). 지금은 **합성 조합 하나로 통일하고 IME를 언제나 영문으로 고정**해 한/영이 어느 쪽이든 결과가 같다(위 입력 파이프라인의 ⭐ 절).
  - **웹에서 백스페이스를 두 번 눌러야 한 글자가 지워지던 것도 같은 뿌리였다.** 합성 조합의 `Composition`은 글자가 있는 동안 항상 채워져 있는데 `Update`의 `isComposing` 가드가 그걸 OS 조합으로 오인해, `IsCompositionStale()`이 매번 첫 누름을 IME에게 양보했다. 가드에 `!UsesSyntheticHangul`을 넣어 해결.
  - 그 전에 해결된 갈래들(**되돌리면 재발한다**): "Play 직후엔 입력창을 클릭하거나 Alt를 눌러야 조합이 시작된다" → `imeCompositionMode = On` 고정 + `TMP_InputField`를 라벨로 교체. "조합이 시작되면 백스페이스까지 막혀 지울 수도 칠 수도 없다" → `HandleCompositionChange`가 **조합이 시작되는 그 순간** 영문으로 되돌린다(커밋 시점에만 되돌리면 늦다).
  - 이미 시도했다 되돌린 것: P/Invoke `ImmSimulateHotKey`(한/영 핫키 시뮬레이션) — 효과 없었으니 다시 시도하지 말 것.
  - **앞으로 IME 쪽을 건드리면 반드시 빌드에서 재검증할 것.** 이 문제는 **에디터 전용이 아니라 스탠드얼론에서도 재현된 이력**이 있고, WebGL은 IMM32가 no-op이라 아예 다른 경로(폴백)를 탄다. `HangulImeMode`가 실패하면 `Player.log`에 경고 한 줄이 남는다(첫 실패만).
  - ⭐ **"키보드를 영문에 맞춰둬야 입력이 된다"도 해결됨.** 남아 있던 원인은 글자를 문자 이벤트로 받은 것이었다 — IME가 한글 모드면 그 문자가 아예 오지 않으므로, 영문 강제가 실패하거나 `imeForceCooldown`(0.2초) 안에 있는 동안 친 글자가 통째로 사라졌다. 지금은 한국어의 글자를 **물리 키로 폴링**해(`HandleHangulKeyPresses`) IME 상태와 무관하게 받는다(위 입력 파이프라인의 ⭐ 절). ⚠️ **한글 입력을 막는 방식으로 "고치지" 말 것** — IME가 한글이어도 그대로 쳐지는 게 목표다.
  - ⚠️ **아직 남은 구멍**: 브라우저 IME가 한글 상태면 강제할 수단이 없어 **OS 조합 오버레이가 화면에 겹쳐 보일 수 있다**(입력 자체는 물리 키로 들어오므로 매칭은 된다). 커밋되어 돌아오는 한글은 `syntheticKeyEchoWindow`로 걸러 두 벌 입력을 막는다. 안내 문구("한/영 키를 눌러주세요")를 띄우는 안전망은 아직 넣지 않았다 — 넣는다면 `InputFieldDisplay`의 기존 힌트 체계를 재사용하는 게 맞다.
  - ⚠️ **물리 키 폴링은 에디터·스탠드얼론에서만 확인했다는 전제로 읽지 말 것** — WebGL은 브라우저 keydown을 거치므로 **웹 빌드에서 한글이 실제로 쳐지는지 반드시 다시 볼 것.** 만약 웹에서 키가 안 들어오면 폴백(문자 이벤트로 들어온 한글)이 그대로 살아 있어 조합 없이 커밋된 글자만 받는 상태가 된다.
- **한 음절 단어 미매칭** — 조합 중에도 `CurrentInput + Composition`으로 평가하게 바꿔 **해결됨**(지금은 `InputManager.HandleCompositionChange` → `TypingReceiver.Dispatch` 경로다). 커밋만 기다리는 구조로 되돌리면 퀵/잽/훅뿐 아니라 `계속`의 `속`, `다음`의 `음`까지 완성 불가가 된다.
- **입력을 못 가져간 화면의 카드가 같이 움직이던 문제 — 해결됨.** 우선순위 디스패치는 글자를 하나에게만 넘기지만 **입력창 자체는 전역이라 아무나 읽을 수 있었다.** 그래서 일시정지 중 `계속`을 치면 손패의 `가드`가 같이 떠오르고, 보상 화면 위에 일시정지가 열리면 후보 카드가 덩달아 밝아졌다(매칭은 안 되는데 화면만 반응). `InputManager.ActiveReceiver`를 단일 판정으로 세우고, 카드는 `IsTypingTarget(word)`·수신자는 `HasTypingFocus`를 먼저 보게 해서 막았다. **입력을 폴링하는 연출 코드를 새로 쓸 때 이 검사를 빠뜨리면 같은 증상이 그대로 돌아온다.**
- **일시정지 명령 단어를 끝까지 칠 수 없던 문제 — 해결됨.** 원인은 손패 핸들러가 같은 입력을 같이 받아 첫 글자를 오타로 처리하며 `ClearInput()`을 부른 것이었다. 우선순위 단일 디스패치(`TypingPriority`)로 구조적으로 막았다 — 개별 가드를 덧붙이는 방식으로 되돌리지 말 것.
- **손패 들림 판정의 절충** — 조합 중 글자는 초성만 비교한다. 모음을 잘못 짚어도 초성이 같으면 카드가 계속 떠 있다. 정밀 검증은 유니코드 분해가 훨씬 깊어져 의도적으로 하지 않았다.
- **`Colorful`(컬러풀)의 단순화 — 해결됨.** `ResolvedAction.StatusEffects`가 리스트라 화상·마비·얼음을 각각 독립적으로 굴려 걸린 것을 전부 부여한다(위 "아직 없는 것" 절 참조). 옛 "우선순위가 가장 높은 화상 하나만" 단순화는 없어졌으니 **단일 `StatusEffect` 필드로 되돌리지 말 것.**
