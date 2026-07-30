# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

**스트리트타이퍼 (Street Typer)** — Unity 프로젝트 (Editor **6000.3.19f1**, URP, 2D 템플릿). 타이핑 액션 텍스트 RPG 프로토타입(기획서상 10일 스코프). 핵심 루프: 제한 시간 동안 화면의 5개 단어 슬롯을 **띄어쓰기·Enter 없이** 연달아 타이핑해 스킬 조합을 만들고, 액션 단어로 조합을 완성해 적을 공격한다.

**게임은 한국어로 타이핑한다.** 카드 이름·매칭·입력 파이프라인 전부 한글 기준이며, 이건 로컬라이제이션 문제가 아니라 설계의 핵심이다.

기획 문서(`Street_Typer_GDD.pdf`)와 아키텍처 문서(`StreetTyper아키텍쳐 디자인.pdf`)가 저장소 밖에 있다. 아키텍처 문서는 `WordData`/`WordCategory`/`BattleContext`/`Combatant`/`IActionEffect` 같은 계층을 제시하지만 **이 프로젝트는 의도적으로 그걸 따르지 않고** 이미 있는 `CardBase` 계층을 재사용한다(아래 참조). 문서와 코드가 다르면 코드가 맞다.

## 작업 방식

Unity 프로젝트라 터미널에서 돌릴 build/lint/test 스크립트가 없다.

- **에디터**: 프로젝트 루트를 Unity Hub / Editor `6000.3.19f1`로 연다 (`ProjectSettings/ProjectVersion.txt`와 일치해야 함).
- **컴파일**: Unity가 포커스/저장 시 자동 컴파일. `dotnet build` 없음 — `Assembly-CSharp.csproj`/`StreetTyper.sln`은 Unity 생성물이고 gitignore되어 있으니 절대 직접 수정하지 말 것. 단일 `Assembly-CSharp` 어셈블리, `.asmdef` 분리 없음.
- **테스트**: `com.unity.test-framework`는 설치되어 있으나 **테스트 어셈블리가 하나도 없다.** 여기서 "테스트"란 Play Mode 수동 확인이며, 보통 `Debug.Log` 출력을 읽는 것이다(`DeckManager.logDebugEvents`, `WordChainManager.logDebugEvents`, `WordUnlockManager.logDebugEvents`, `PendingActionManager.logDebugEvents`).
  - **Play는 `TitleScene`부터 시작해야 실제 흐름과 같다.** `SampleScene`을 직접 Play해도 전투는 돌지만, 일시정지에서 "타이틀"을 치면 `TitleScene`으로 넘어가므로 씬 전환 경로를 확인할 수 없다.
- **실행**: 에디터에서 Play. 헤드리스/CLI 실행 경로 없음.
- **빌드**: `File > Build Profiles`로 Windows 스탠드얼론을 뽑아 `Build/StreetTyper.exe`로 내보내 왔다(`Build/`는 gitignore). CLI 빌드 스크립트는 없다.
  - **IME 관련 동작은 에디터 Play만으로 검증하면 안 된다.** 에디터와 빌드가 다르게 동작한 이력이 있어 스탠드얼론에서 재확인해야 한다.
  - 빌드 런타임 로그: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\StreetTyper\Player.log` (회사/제품명이 `DefaultCompany`/`StreetTyper` 기본값 그대로다). 에디터 로그는 `%LOCALAPPDATA%\Unity\Editor\Editor.log`, 임포트 워커 로그는 저장소의 `Logs/`.
- **디버그 키**(`BattleManager.Update`, `Keyboard.current` 직접 폴링):
  - `1` — 결과 화면에서만 동작. 승리면 `StageManager.NextStage()`(= 클리어 보상 지급), 패배면 `RestartStage()`. **스테이지를 넘기는 유일한 입력이다.**
  - `3` — 적 턴 즉시 실행(`ExecuteEnemyTurn`). 타이머를 기다리지 않고 적 행동을 보려는 디버그용.
  - **일시정지 중에는 둘 다 막힌다** — `BattleManager.Update`가 `Time.timeScale`이 0이면 즉시 리턴한다. 없으면 일시정지 메뉴 뒤에서 `1`로 스테이지가 넘어간다.
- **ESC** — 일시정지 토글(`PauseManager`). 디버그 키가 아니라 정식 기능이다.
- C# `LangVersion` 9.0, .NET Standard 2.1 (Mono) — 그 이상 문법은 컴파일 실패한다.

## 프로젝트 구조

최상위 에셋 폴더는 에셋 브라우저 정렬을 위해 `NN_Name` 접두사를 쓴다. 새 폴더도 이 규칙을 따를 것.

**씬은 두 개이고 빌드 설정에 그 순서대로 등록되어 있다** — `TitleScene`(인덱스 0) → `SampleScene`(인덱스 1). 인덱스 0이 빌드 시작 씬이므로 이 순서가 곧 "타이틀부터 시작"이다. 씬 이름 문자열은 `Assets/02_Scripts/GameScenes.cs`의 상수(`GameScenes.Title`/`GameScenes.Battle`)로만 쓰고 직접 타이핑하지 말 것.

- `Assets/00_Scenes/TitleScene.unity` — 타이틀 메뉴. `Canvas`(버튼 `GameStart`/`Option`/`Exit`) · `TitleMenu`(프리팹 인스턴스) · `Main Camera` · `EventSystem`.
  - `Main Camera`를 지우지 말 것. Overlay 캔버스는 카메라 없이도 그려지지만 카메라가 하나도 없으면 "No cameras rendering" 경고가 뜬다.
  - `TitleMenu`는 `03_Prefabs/Managers/TitleMenu.prefab`의 인스턴스이고, **버튼 3개 연결은 프리팹이 아니라 씬 인스턴스 오버라이드로만 존재한다**(프리팹 쪽 세 슬롯은 비어 있다). 매니저 프리팹들과 같은 주의사항이 적용된다 — `Apply` 금지.
- `Assets/00_Scenes/SampleScene.unity` — 전투 씬. 루트: `00_BOOT`, `01_CAMERA`, `02_SYSTEM`, `03_WORLD`, `04_UI`, `05_DEBUG`, `EventSystem`.
  - `02_SYSTEM`: `InputManager`, `Deck Manager`, `StageManager`, `BattleManager`, `WordDictionary`, `WordUnlockManager` — **여섯 개 전부 프리팹 인스턴스**다(아래 `03_Prefabs/Managers/` 참조).
  - `03_WORLD`: `player`(자식 `PlayerVisual`/`PlayerBlink`가 눈 깜빡임 담당), `enemySpawnPoint`
  - `04_UI`: `Card Canvas`(`InputFieldDisplay`=입력창 · `Hand`=손패 · `WordChainText (TMP)` · `Timer Bar`), `Field Canvas`(`playerHPBar`/`enemyHPBar`+HP 텍스트 · `PlayerDefIcon`/`EnemyDefIcon` · `Result Text`). 적 의도는 씬 오브젝트가 아니라 **런타임에 생성되는 `EnemySpeechBubble`**에 뜬다(`BattleManager`가 `GetComponentInChildren`으로 텍스트를 잡는다).
    - 둘 다 Screen Space Overlay이고 **Canvas Scaler 설정이 같아야 한다** — Scale With Screen Size / 1920×1080 / Match Width Or Height 0.5. 예전엔 `Field Canvas`만 Constant Pixel Size라 해상도가 바뀌면 HP UI만 어긋났다. 새 캔버스를 만들면 이 설정을 복사할 것.
    - 입력창 오브젝트는 `Card Canvas > InputFieldDisplay`(자식 `Text Area > Text`)다. 이름과 달리 **`TMP_InputField`가 아니라 TMP 라벨**이며 클릭 대상이 아니다(이유는 아래 `InputFieldDisplay` 참조). `Text Area` 래퍼와 그 `RectMask2D`는 예전 입력 필드의 잔재지만 클리핑 용도로 남겨두었다.
    - HP 슬라이더 내부(`Background`/`Fill Area`/`Fill`)의 RectTransform 값은 **`Slider` 컴포넌트가 구동한다** — 인스펙터에서 잠겨 보이는 게 정상이고 손으로 맞추려 하지 말 것.
- `Assets/BattleScene.unity` — 최상위에 있는 **미사용 씬**(빌드 설정 미등록, 병합 잔재). 여기에 작업하지 말 것.
- `Assets/01_Arts/Fonts/` — Paperlogy 계열 TMP 폰트. **한글 글리프를 포함한 폰트를 써야 한다.** 기본 `LiberationSans SDF`는 라틴 전용이라 한글이 `□`로 나오고 문자당 경고 하나씩 찍힌다.
  - **한글이 흐르는 곳은 전부 Paperlogy로 맞춰져 있다**: 입력창 `Text`, `WordChainText (TMP)`, `Card.prefab > NameText`, `Actions.prefab`의 라벨(쌓인 공격 문장), 일시정지 안내 라벨, 타이틀 씬 버튼 3개(`게임시작`/`옵션`/`게임종료`).
  - 현황을 다시 셀 때는 GUID로 세면 된다 — Paperlogy `53b522988c0e6f94d8a0a2d8ed5d613c`, `LiberationSans SDF` `8f586378b4e144a9851e7b34d9b748ee`.
  - **아직 `LiberationSans SDF`인 곳이 7군데 있다**: `Result Text`, `playerHP`, `enemyHP`, `PlayerDef`, `EnemyDef `, `EnemySpeechBubble > EnemyIntent`, `PlayerSpeechBubble > PlayerActionText`. 지금은 이들이 숫자나 영어("VICTORY!", "Intent: Attack (5)")만 표시해 문제가 없지만, **여기에 한글을 넣는 순간 전부 `□`가 된다.** 결과/의도 문구를 한글화할 계획이면 폰트부터 교체할 것.
- `Assets/01_Arts/Demi/` — 플레이어 캐릭터 스프라이트(`DemiOpenEyes`/`DemiClosedEyes`, Git LFS)와 애니메이터 컨트롤러·애님 클립. **눈 뜬/감은 스프라이트를 각각 별도 오브젝트로 겹쳐두고 각자 애니메이터로 깜빡임을 만드는 구조**다(씬의 `player > PlayerVisual` / `PlayerBlink`). 컨트롤러 파일명 `DemiOpneEyes_0`의 오타는 그대로 두었다.
- `Assets/03_Prefabs/` — `Card.prefab`(런타임 생성되는 손패 카드), `Actions.prefab`(쌓인 공격 문장 한 줄, `PendingActionView`가 찍어낸다), `PlayerSpeechBubble`/`EnemySpeechBubble`, `enemy`/`strongEnemy`(스테이지별 적).
- `Assets/03_Prefabs/Managers/` — `02_SYSTEM`의 매니저 여섯 개(`InputManager`/`Deck Manager`/`StageManager`/`BattleManager`/`WordDictionary`/`WordUnlockManager`)를 씬에서 뽑아낸 프리팹. **씬은 이걸 인스턴스로 들고 있고, 매니저끼리와 씬 오브젝트를 향한 인스펙터 연결은 전부 프리팹 인스턴스 오버라이드로 저장된다**(`SampleScene.unity`의 `m_Modifications` 안 `objectReference`). 자세한 주의점은 컨벤션 절 참조.
- `Assets/04_Data/Cards/` — **24개 `CardBase` 에셋**(GDD 4장 단어 사전 전체, 페인풀만 제외). `Assets > Create > Deck Manager > Cards > ...` 메뉴로 만들 것. `.asset` YAML을 손으로 작성하면 스크립트 GUID가 조용히 어긋날 수 있다.
- `Assets/04_Data/EnemyTutorial.asset` — 유일한 `EnemyData`.
- `Assets/InputSystem_Actions.inputactions` — Input System 기본 템플릿. **미사용.** 게임플레이 입력은 의도적으로 이걸 거치지 않는다(아래).

## 아키텍처

### 전체 흐름

```
키보드 → InputManager → CardInputHandler(5슬롯 매칭) → WordChainManager(조합 검증)
   → [액션 단어로 완성] → DeckManager.HandleChainCompleted
      → SkillResolver(수치 계산) → PendingActionManager(쌓아둠, 아직 적용 안 함)
      → TimerManager.AddTime(시간 증감만 즉시)
   → [타이머 0] → DeckManager.HandleTimeExpired
      → PlayPendingActions(쌓인 순서대로 0.3초 간격)
         → CombatManager(실제 적용) → BattleManager(UI/말풍선)
      → 손패 리롤 → 적 턴 → 다음 플레이어 턴
```

`DeckManager`는 단순 파사드가 아니라 **전투 배선의 중심**이다 — 체인 완성과 타이머 만료를 받아 나머지 시스템을 순서대로 호출한다.

⚠️ **체인을 완성해도 그 자리에서 피해가 들어가지 않는다.** 한 턴 동안 완성한 조합은 `PendingActionManager`에 쌓이기만 하고, 턴이 끝날 때 한꺼번에 재생된다. **유일한 예외가 `TimerChange`(잽/훅/퀵/어퍼컷)**로, 이건 남은 시간을 늘리거나 깎는 리스크라 즉시 반영해야 의미가 있다.

### 턴 전환 딜레이 — 애니메이션 자리를 미리 잡아둔 값이다

턴이 바뀔 때마다 코루틴이 사이사이 대기를 넣는다. **이 숫자들은 임의로 고른 게 아니라, 앞으로 들어올 적 공격 모션·스테이지 전환 연출의 길이를 어림잡아 미리 자리를 비워둔 것이다.** 지금은 그 시간 동안 화면에 아무 일도 안 일어나므로 "불필요한 대기"처럼 보이지만, 줄이거나 없애면 나중에 애니메이션을 넣을 자리가 사라진다. 연출이 실제로 붙을 때 그 길이에 맞춰 조정할 값들이다.

| 필드 | 코드 기본값 | 씬 현재값 | 비우고 있는 자리 |
|---|---|---|---|
| `DeckManager.turnChangeDelay` | 2 | 1 | 타이머 만료 → 적이 공격하기까지 |
| `DeckManager.postAttackDelay` | 4 | 2 | 적 공격 → 플레이어 턴 재개까지 |
| `StageManager.stageStartDelay` | 2 | 2 | 스테이지 등장 → 플레이어 턴 시작까지 |
| `BattleManager.actionBubbleDuration` | 1 | 1 | 공격 말풍선이 떠 있는 시간 |
| `DeckManager.pendingActionInterval` | 0.3 | — | 쌓인 공격이 하나씩 터지는 간격 |

`pendingActionInterval`은 위의 다른 값들과 성격이 다르다 — **자리만 비워둔 값이 아니라 실제로 연출이 일어나는 구간**이다(쌓인 공격이 하나씩 적용되며 HP가 계단식으로 줄어든다).

대기 중에는 **입력이 잠기고 타이머도 멈춘다**(`DisableInput` + `StopTimer`). 대기가 끝나는 쪽에서 다시 열어주므로, 새 대기 구간을 추가할 땐 반드시 짝을 맞출 것.

스크립트 폴더는 시스템 단위로 나뉘고, 뷰는 각 시스템 아래 `UI/` 하위 폴더에 둔다(`DeckManager/UI/`, `Timer/UI/`, `WordChainManager/UI/`, `Combat/UI/`). 예외는 특정 시스템에 속하지 않는 화면 단위 UI인 `02_Scripts/UI/`(`TitleMenu`/`PauseManager`)와 최상위 `GameScenes.cs`다.

### 입력 파이프라인 (`02_Scripts/InputManager/`)

타이핑 입력은 Unity Input Action 에셋/바인딩을 완전히 우회하고 `Keyboard.current`를 직접 쓴다.

- **`InputManager`** — `CurrentInput`(커밋된 문자), `Composition`(IME 조합 중 문자), 이벤트 `OnCharacterEntered(char)`/`OnCompositionChanged(string)`/`OnBackspace`/`OnSubmit`/`OnInputCleared`. `EnableInput()`/`DisableInput()`/`ClearInput()`으로 제어하고, 현재 상태는 `IsInputEnabled`로 읽는다(`PauseManager`가 멈추기 전 상태를 기억해 복원하는 데 쓴다).
  - **입력 이벤트 구독자가 둘이다** — `CardInputHandler`(전투)와 `PauseManager`(일시정지 명령 단어). 둘 다 `OnCharacterEntered`+`OnCompositionChanged`를 받으며, `timeScale`로 서로를 배제한다(위 일시정지 절 참조).
  - **한글만 받는다.** `HandleTextInput`이 `IsHangul` 아니면 즉시 리턴 — ASCII/영문은 `CurrentInput`에 도달조차 못 한다. 타이핑 경로 어디에서도 영어가 동작한다고 가정하지 말 것.
  - 문자는 `Keyboard.onTextInput`에서 온다(키 폴링 아님) — 그래야 조합된 한글 음절이 나온다.
  - **IME 상태를 이 클래스가 전부 소유한다.** 플레이어가 입력창을 클릭하거나 한/영을 누르는 준비 동작 없이 Play 직후 바로 타이핑되게 하는 게 목표이며, 성질이 다른 두 가지를 각각 처리한다.
    - *조합 활성화*(Unity 소유): `EnableInput()`이 `Keyboard.current.SetIMEEnabled(true)` + `Input.imeCompositionMode = On`. **`imeCompositionMode`를 `On`으로 고정하는 게 핵심이다** — 기본값 `Auto`는 "텍스트 필드가 선택된 동안"에만 IME를 켜므로, 예전엔 입력창을 클릭해 TMP가 대신 `On`으로 바꿔주기 전까지 한글이 조합되지 않았다. `DisableInput()`은 `SetIMEEnabled(false)`로 되돌린다(입력이 잠긴 동안 OS 조합 오버레이가 뜨는 걸 막는다).
    - *변환 모드*(Windows IME 소유, 한글↔영문): Unity API로는 불가능해 `HangulImeMode.Force()`(IMM32)로 강제한다.
  - **`HangulImeMode`** — `GetActiveWindow` → `ImmGetContext` → `ImmSetOpenStatus` + `ImmSetConversionStatus(IME_CMODE_NATIVE, IME_SMODE_NONE)`. `IME_CMODE_FULLSHAPE`는 **넣지 말 것**(전각이 된다). `#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN` 가드, 그 외 플랫폼은 no-op. 실패 로그는 **첫 번째 실패만** 남긴다(재적용 지점이 많아 그대로 두면 폭주한다).
    - IME를 켠 **뒤에** 불러야 한다. 꺼진 상태에서는 창에 IME 컨텍스트가 붙어 있지 않아 `ImmGetContext`가 0을 준다 — 그래서 `EnableInput()`은 코루틴으로 **1프레임 뒤** 강제한다.
    - 재적용 지점 세 곳: `EnableInput()`(턴마다), `OnApplicationFocus(true)`(alt-tab 복귀), 그리고 **자가 복구** — `HandleTextInput`에 라틴 글자가 들어오면 IME가 영문으로 빠진 것이므로 그 자리에서 다시 강제한다. 한/영 키는 Windows IME가 앱보다 먼저 처리해 차단할 수 없고, 이 자가 복구가 그 대체책이다(한 글자만 잃는다).
    - 자가 복구는 **라틴 글자만** 신호로 본다. 숫자·공백까지 포함하면 결과 화면의 `1`/`3` 같은 입력에도 IMM32를 건드려 조합 중인 글자가 끊길 수 있다. 과호출 방지로 `imeForceCooldown`(기본 0.2초) 가드가 있다.
    - 과거의 `ChangeHangul()`(RightAlt에서 `imeCompositionMode = On`)은 **삭제됐다.** 그건 한/영 토글이 아니라 조합 활성화를 늦게 켜주는 코드였고, "Alt를 눌러야 타이핑이 시작되는" 증상의 원인이었다. 되살리지 말 것.
  - `GetLeadConsonant(char)` / `IsValidProgress(committed, composing, target)` 정적 유틸을 제공한다. 후자는 "커밋된 문자열 + 조합 중인 글자가 target 단어를 향해 여전히 유효한가"를 판정하며, **매칭 로직과 손패 애니메이션이 같은 판정을 공유**하도록 하는 단일 기준점이다. 조합 중 글자는 표준 유니코드 한글 분해 공식으로 **초성만** 비교한다(모음 단계까지 검증하지 않는 의도적 절충).
  - `OnSubmit`(Enter)은 **구독자가 없다** — 매칭은 Enter를 쓰지 않는다.
- **`InputFieldDisplay`** — 순수 뷰. `CurrentInput + Composition`을 `TextMeshProUGUI`에 미러링한다(라벨은 `GetComponent`가 아니라 인스펙터의 `text` 필드로 연결하므로 라벨과 다른 오브젝트에 붙어도 된다). **`TMP_InputField`가 아니라 그냥 라벨인 게 의도다** — 입력 필드는 선택될 때 `imeCompositionMode`를 `On`, 해제될 때 `Auto`로 되돌려 `InputManager`가 소유해야 할 IME 상태를 뺏어가고, 클릭 가능한 UI가 되어 "여길 눌러야 하나?" 하는 오해를 만든다. 예전엔 `TMP_InputField` + `readOnly = true` 우회를 썼는데, 클릭해서 활성화한 뒤 포커스가 빠지면 입력이 죽는 문제가 있어 라벨로 교체했다. **입력 필드로 되돌리지 말 것.**
  - `SetIMECursorPosition`으로 OS IME 오버레이(조합 중인 밑줄 글자) 위치를 텍스트 끝에 맞춘다. `WorldToScreenPoint`는 좌하단 원점, `SetIMECursorPosition`은 좌상단 원점이라 Y를 뒤집는 코드가 필요하다.

### 덱 / 카드 (`02_Scripts/DeckManager/`)

> 폴더명은 `DeckManager`(공백 없음)다. 예전엔 `Deck Manager`(공백 포함)였으나 이름이 바뀌었다 — 씬의 GameObject 이름은 여전히 `Deck Manager`(공백 포함)이니 혼동하지 말 것.

- **`Cards/CardBase.cs`** — 추상 `ScriptableObject`: `CardName`(타이핑할 단어 = 표시 텍스트 = 매칭 키), `Icon`, `Description`, 추상 `Category`. `enum CardCategory { Modifier, Time, Type, Action }`.
  - **`Category`는 직렬화되지 않는 계산 프로퍼티다.** 그래서 enum 값을 바꿔도 `.asset` 마이그레이션이 필요 없다.
  - `AttributeCardData.Category`는 `effectType`에서 계산된다: `RepeatAction`→`Time`, `StatusChance*`/`Bleed`→`Type`, 나머지(`LifeDrain`/`DamageReduction`/`CritMultiplier`)→`Modifier`. 즉 GDD의 "속성 및 특수효과" 한 덩어리가 세 분류로 쪼개진다.
  - ⚠️ **`AttributeEffectType`에서 `Bleed`를 삭제하지 말 것.** 페인풀이 단어 목록에서 빠져 미사용이지만, 지우면 enum 인덱스가 밀려 `Intelli.asset`(`effectType: 5` = `CritMultiplier`)이 조용히 `RepeatAction`으로 바뀐다. 직렬화되는 건 `effectType`/`actionKind`이니 **이 enum들의 순서는 절대 건드리지 말 것.**
- **`WordDictionary`** — 플레이어가 *지금* 쓸 수 있는 단어. 직렬화 필드 없는 순수 런타임 상태. `TryGetWord`/`GetRandomWord`/`AddWords`/`Clear`, `OnWordsChanged` 이벤트.
  - **슬롯 뽑기와 타이핑 검증이 둘 다 여기 하나만 바라본다.** 예전엔 같은 24장이 `CardSlotManager`와 `WordChainManager` 양쪽 인스펙터에 중복돼 있어 "슬롯엔 뜨는데 입력은 안 되는" 버그가 실제로 났었다. 이 단일 출처 구조를 깨지 말 것.
  - `GetRandomWord()`와 `GetRandomWord(CardBase exclude)` 두 오버로드가 있다. 후자는 거절 샘플링(다를 때까지 다시 뽑기) 대신 **인덱스를 건너뛰어 남은 n-1개에 균등하게** 뽑는다. 보유 단어가 하나뿐이거나 `exclude`가 사전에 없으면 평소대로 뽑는다 — 이 폴백이 없으면 단어가 하나일 때 슬롯이 null이 되어 카드가 사라진다.
  - `AddWords`(배치)는 이벤트를 마지막에 **한 번만** 쏜다. `OnWordsChanged`는 현재 구독자가 없지만(손패를 뽑는 시점은 `CardSlotManager`가 따로 정한다), 사전 UI 같은 게 붙을 때를 대비해 배치 단위로 유지한다.
- **`WordUnlockManager`** — 게임 전체 단어 목록(인스펙터에 24장)과 지급 로직. `WordEntry { card, grantedAtStart }`. `GrantStartingWords()`(런 시작 — 사전을 비우고 `grantedAtStart` 전부 지급), `GrantStageClearReward()`(미보유 중 랜덤 N개). 시작 단어를 별도 리스트로 두지 않고 플래그로 표현하는 게 핵심 — 별도 리스트를 두면 중복 문제가 재발한다.
  - **시작 단어는 현재 3장이다: 가드 · 펀치 · 슈퍼**(액션 2 + 모디파이어 1의 최소 조합). 예전엔 9장이었고, 프리팹 기본값은 아직 9장 그대로다 — **지금의 3장은 `SampleScene.unity`의 인스턴스 오버라이드로만 존재한다.** 시작 단어를 바꾸려면 프리팹이 아니라 **씬 인스턴스**에서 체크박스를 만지고 씬을 커밋할 것(위 매니저 프리팹 주의사항과 같은 이유).
  - 액션 단어(`Category == Action`)가 시작 목록에 최소 하나는 있어야 한다. 액션 단어로만 체인이 완성되므로, 전부 빼면 **어떤 조합도 완성할 수 없어 공격이 영원히 불가능해진다.**
- **`CardSlotManager`** — 5슬롯(`CurrentCards`/`SlotCount`/`OnSlotChanged`/`ConsumeSlot`/`RefillAll`). 사전에서 균등 랜덤으로 뽑으며 **슬롯 간 중복은 의도된 동작**(중복 방지 버전을 만들었다가 요청으로 되돌린 이력이 있으니 확인 없이 "고치지" 말 것).
  - **단, 한 슬롯이 직전에 들고 있던 카드는 제외한다.** `FillSlot(index, excludeCurrent)`의 플래그로 갈리며, `ConsumeSlot`은 `true`(타이핑으로 방금 쓴 카드가 같은 자리에 곧바로 다시 오지 않게), `RefillAll`은 `false`(완전 랜덤)를 넘긴다. 제외 자체는 `WordDictionary.GetRandomWord(exclude)`가 담당한다. **슬롯 간 중복과 혼동하지 말 것** — 막는 건 *한 칸의 연속 재등장*뿐이다.
  - `ConsumeSlot`(한 칸 보충)과 `RefillAll`(손패 통째로 교체)은 쓰임이 다르다. 사전이 비어 있으면 `RefillAll`은 들고 있던 카드를 null로 지워버리므로 아예 손대지 않고 경고만 남긴다.
  - **`RefillAll`을 부르는 곳은 두 군데다**: `StageManager.BeginStageAfterDelay`(스테이지 시작)와 `DeckManager.RunTurnTransition`(**턴이 바뀔 때마다**). 후자는 쌓인 공격 재생이 끝난 직후, 적 턴 대기 전에 돈다.
  - **손패가 채워지는 경로는 `ConsumeSlot`/`RefillAll` 둘뿐이다.** `Awake`는 배열만 잡고 채우지 않으며, 사전이 채워질 때 자동으로 뽑지도 않는다. 예전엔 `WordDictionary.OnWordsChanged`를 구독해 빈 슬롯을 채웠는데, 그러면 게임 시작 시 `GrantStartingWords()` 시점에 손패가 먼저 나왔다가 `stageStartDelay` 뒤 `RefillAll()`이 **다시 뽑아** 눈에 보이는 리롤이 생긴다. 그 구독을 되살리지 말 것.
- **`CardInputHandler`** — 매칭 로직. `OnCharacterEntered`와 `OnCompositionChanged`를 **둘 다** 구독한다.
  - 조합 중에도 평가해야 하는 이유: 퀵/잽/훅 같은 **한 음절 단어는 뒤에 이어질 음절이 없어 IME가 영원히 커밋하지 않는다.** 커밋만 기다리면 이 단어들은 절대 완성되지 않는다.
  - 커밋 경로에서는 조합 문자열을 빈 문자열로 넘긴다 — 커밋 순간 `Composition`이 아직 옛 값을 들고 있어 "펀펀"처럼 중복될 수 있기 때문.
  - `_pendingEcho`: 조합 중 매칭으로 슬롯을 소비하면 OS IME는 그 글자를 아직 붙잡고 있어 다음 입력 때 뒤늦게 커밋되어 돌아온다. 그 메아리를 한 번만 걸러낸다. **IME 설정을 건드려 해결하려 하지 말 것.**
- **`MainBufferManager`** — 매칭된 카드의 단순 목록. 화면 표시/디버그용이며 `WordChainManager`와 별개로 유지된다.
- **`HandFanLayout`** (`04_UI/Card Canvas/Hand`) — `Card.prefab`을 `SlotCount`만큼 생성하고 `CardSlotView.Bind(manager, i, inputManager)` 호출 후, `LateUpdate`에서 부채꼴 배치. `[ExecuteAlways]`라 생성은 `Application.isPlaying`으로 가드된다.
  - **위치를 쓰는 건 여기 하나뿐이다.** `CardSlotView`는 `VerticalOffset`(떠 있어야 할 높이)만 계산해 들고 있고, `HandFanLayout`이 부채꼴 목표에 더한다. `CardSlotView`가 자기 `anchoredPosition`을 직접 만지면 같은 프레임에 두 스크립트가 경쟁한다.
  - `CollectChildren()`이 `_children`과 `_cards`를 같은 루프에서 나란히 재수집한다 — `centerOnTop`이 형제 순서를 바꾸므로 스폰 순서로 고정해두면 어긋난다.
- **`UI/CardSlotView`** — 한 슬롯의 표시 + 타이핑 들림/교체 애니메이션. 런타임 생성이라 `OnEnable`이 `Bind`보다 먼저 돌므로 구독이 null 관용적이고 멱등하다(`Subscribe`/`Unsubscribe`/`_subscribed`).
  - `SetCard`는 `card.Icon`이 없으면 **프리팹에 박아둔 스프라이트를 그대로 둔다.** 카드 데이터에 아이콘이 없는 게 현재 정상 상태이고, 예전엔 이걸 null로 덮어써서 Play 시작과 동시에 카드 프레임이 사라졌었다.
- **`DeckManager`** — 파사드 + **전투 배선**. `HandleChainCompleted`, `HandleTimeExpired`(코루틴으로 딜레이를 두고 턴 전환), `HandleBattleEnded`를 소유한다. `turnChangeDelay`/`postAttackDelay`가 인스펙터에 노출된다.
  - `HandleChainCompleted`의 순서가 중요하다: 적용(`CombatManager`) → UI/말풍선 → 체인 비우기 → **마지막에** `timerManager.AddTime`. 마지막인 이유는 이 호출이 타이머를 0으로 만들면 그 자리에서 `OnTimeExpired` → 턴 전환 코루틴이 시작되기 때문이다. 앞으로 옮기면 턴 전환 도중에 나머지 처리가 끼어든다.
  - `battleManager.OnBattleEnded`를 구독해 승패가 갈리는 즉시 `StopTimer` + `DisableInput`한다. 이게 없으면 적이 죽은 뒤에도 타이머가 0까지 흐르는 동안 타이핑이 먹힌다.

### 조합 (`02_Scripts/WordChainManager/`)

- **`WordChainManager`** — 현재 조합(체인) 상태. `SubmitWord(string)` → `WordSubmitResult`. 규칙: `Modifier` 무제한 · `Time` 최대 1 · `Type` 최대 1 · `Action` 정확히 1개이며 마지막(넣는 순간 완성). 같은 단어 중복 불가.
  - **오타가 나도 체인은 지우지 않는다.** GDD의 "오타 페널티: 조합 전부 초기화"는 의도적으로 적용하지 않기로 한 결정이다. 체인은 완성되어 `OnChainCompleted`로 넘어간 뒤 `DeckManager`가 `ClearChain()`을 부를 때만 비워진다.
  - 완성된 체인에 유효한 새 단어가 들어오면 그 순간을 다음 체인 시작으로 보고 자동으로 비운다.
- **`WordInstance`** — `CardBase`를 감싸는 얇은 래퍼. `UpgradeLevel`/`UseCount`/`PermanentValueBonus`는 아직 아무도 채우지 않는다.
- **`UI/WordChainView`** — 체인 단어를 공백으로 이어 표시하는 순수 뷰.

### 전투 (`02_Scripts/SkillResolver/`, `Combat/`, `Character/`, `Enemy/`, `BattleManager.cs`, `StageManager.cs`)

**이제 전부 씬에 배치되어 실제로 동작한다.** (예전엔 미배선 스케치였다.)

- **`SkillResolver.Resolve(chain, casterPower)` → `ResolvedAction`** — 체인 단어값 + 시전자의 힘만으로 계산하며 **대상의 방어도나 상태는 모른다.** 타격 횟수(더블/트리플/뎀프시롤)와 치명타(인텔리) 확률을 여기서 즉시 굴려 최종 정수로 접는다. 파워는 타이핑 순서와 무관하게 적용되도록 다른 계산 전에 개수부터 센다.
- **`ResolvedAction`** — `Damage`/`Defense`/`Heal`/`IgnoresDefense`/`BreaksEnemyDefense`/`StatusEffect`/`DamageReduction`/`TimerChange`/`LootBonusOnKill`. **소비할 시스템이 없어도 계산해서 싣는다**는 원칙이다 — 아직 안 읽히는 값이 있을 뿐 계산이 빠진 게 아니다.
- **`CombatManager.ExecutePlayerAction`** — 상대가 있어야 알 수 있는 것만 처리(방어도 파괴, 피해 적용, 방어/회복). 아직 소비처가 없는 값들은 `LogPendingEffects`가 `[미구현]` 로그로 남긴다. **`TimerChange`는 여기서 읽지 않는다** — `DeckManager`가 체인 완성 시점에 직접 소비하므로, 여기에 타이머 처리를 추가하면 이중 적용이 된다.
- **`PendingActionManager`** (`Combat/`) — 한 턴 동안 완성된 조합의 `{ SkillName, ResolvedAction }` 쌍을 쌓아두는 **순수 보관소**. `Enqueue`/`TryDequeue`/`Clear` + `OnActionQueued`/`OnActionDequeued`/`OnCleared`. 먼저 완성한 조합이 먼저 나가는 **FIFO**다(화면엔 "쌓이는" 것처럼 보이지만 재생 순서는 쌓인 순서 그대로).
  - **적용도 재생도 하지 않는다.** 꺼내서 `CombatManager`에 넘기고 사이에 간격을 두는 건 `DeckManager.PlayPendingActions`다 — 전투 배선을 `DeckManager` 한 곳에 유지하려는 의도적 분리다.
  - ⚠️ **`HandleChainCompleted`에서 `Enqueue`는 반드시 `timerManager.AddTime`보다 먼저 와야 한다.** `AddTime`이 남은 시간을 0으로 만들면 그 호출 안에서 곧바로 턴 전환 코루틴이 시작되기 때문이다. 순서가 뒤바뀌면 **훅으로 타이머를 깎아 턴을 끝낸 그 조합만 재생 목록에서 빠진다.**
  - 재생 중 적이 죽으면 남은 것을 버리고 즉시 중단한다(`Die()`가 `Destroy`를 부르므로 이후 공격은 대상이 없다). `HandleBattleEnded`와 `StageManager.LoadStage`에서도 비워, 지난 판 공격이 다음 스테이지로 넘어가지 않게 한다.
  - **`UI/PendingActionView`** — 쌓인 문장을 플레이어 옆에 세로로 보여주는 순수 뷰. 프리팹을 `Instantiate`해 목록을 만들고, 꺼내진 항목은 맨 앞부터 지운다(FIFO라 순서만 맞으면 정확하다). 한글 문장이 들어가므로 **라벨 폰트는 Paperlogy여야 한다.**
- **`CharacterStats`** — `TakeDamage(damage, ignoreDefense = false)`, `Heal`, `AddDefense`, `IncreasePower`, private `Die()` → `Destroy(gameObject)`(그래서 호출자들이 매 프레임 null 체크한다).
- **`EnemyBase : CharacterStats`** — `EnemyData`(SO)를 런타임 스탯으로 옮기는 다리. `Start()`에서 `maxHP`/`power`/`gameObject.name`을 에셋값으로 덮어쓴다. **`enemyData`가 비어 있으면 `base.Start()`로 폴백**해 프리팹에 박힌 인스펙터 값을 그대로 쓰므로, 적이 엉뚱한 체력으로 나오면 프리팹의 `enemyData` 연결부터 확인할 것(조용히 넘어간다). `enemyManager.currentEnemy`의 타입이자 `StageManager`가 스폰 직후 `GetComponent`로 집어오는 타입이다.
- **`EnemyManager`** — 가중치로 다음 의도를 굴리고(`ActionType { Attack, Defend, Buff }`) `ExecuteEnemyTurn(player)`에서 실행. **액션 enum이 두 개 있다**: 카드의 `ActionKind { Attack, Defense }`와 이것.
- **`BattleManager`** — HP/방어도/의도 UI, 말풍선, 승패 판정. `OnPlayerActionResolved(bubbleText)`는 **턴을 끝내지 않는다**(타이머가 도는 동안 여러 번 호출됨). 적 턴은 `ExecuteEnemyTurn()`으로 분리되어 있고 `DeckManager`가 부른다. 말풍선엔 스킬 이름이 아니라 적용된 수치가 뜬다.
  - `OnBattleEnded` 이벤트는 `isGameOver`가 **false→true로 바뀌는 순간에만** 발생한다. `CheckGameState`가 `UpdateUI`마다 불려 `ShowResult`도 반복 호출되므로, 가드 없이 쏘면 매 프레임 발생한다.
  - 방어도 UI는 **아이콘 오브젝트가 텍스트를 자식으로 품는 구조**다(`PlayerDefIcon > PlayerDef`). 방어도가 0이면 아이콘째 꺼서 둘 다 사라진다. 아이콘 Image엔 아직 스프라이트가 없어 흰 사각형으로 보이는 게 현재 정상이다.
  - `playerHPFill`/`enemyHPFill`은 HP 슬라이더의 Fill Image다. 방어도가 있으면 **회색**, 없으면 플레이어 초록 / 적 빨강으로 바뀐다.
- **`StageManager`** — `enemyPrefabs` 리스트를 인덱스로 참조. `Start()`에서 시작 단어 지급 후 `LoadStage(0)`, `NextStage()`에서 클리어 보상 지급 후 다음 스테이지. `RestartStage()`(사망 재시작)는 사전을 건드리지 않아 얻은 단어가 유지된다.
  - `LoadStage`는 적을 스폰한 **직후 곧바로 플레이어 턴을 열지 않는다.** 타이머를 멈추고 입력을 잠근 뒤(+ 이전 스테이지에서 쌓다 만 체인을 비운 뒤) `stageStartDelay`만큼 기다렸다가, `BeginStageAfterDelay`에서 **손패를 전부 새로 뽑고**(`CardSlotManager.RefillAll()`) 입력·타이머를 연다.
  - 체인과 입력창은 대기 후가 아니라 **`LoadStage` 시점에 즉시** 비운다 — 새 적이 등장하는데 이전 조합 텍스트가 2초 더 남아 있으면 어색하기 때문.
  - 스테이지가 연달아 바뀌어도 겹치지 않도록 진행 중인 코루틴을 `StopCoroutine`으로 정리한다.

### 타이머 (`02_Scripts/Timer/`)

- **`TimerManager`** — `baseDuration`(기본 10초) 카운트다운. 이벤트가 **두 개**인 게 핵심이다:
  - `OnTimeChanged(remaining)` — 매 프레임(자연 감소 포함). 슬라이더 위치 갱신용.
  - `OnTimeAdjusted(delta)` — `AddTime`/`ReduceTime`로 **효과에 의해** 증감했을 때만. 색 반짝임용.
  - 이 둘을 합치면 정상 카운트다운도 매 프레임 "감소"로 잡혀 반짝임이 끝날 틈 없이 재시작되어 **항상 빨간색으로 고정**된다. 실제로 겪었던 버그다.
  - **만료 판정(`CheckExpired`)은 `Update`와 `AddTime` 양쪽에서 부른다.** 시간이 0이 되는 경로가 두 개이기 때문이다 — 자연 감소뿐 아니라 훅/어퍼컷(`TimerDelta: -1`)이 남은 1초를 깎아 0으로 만들 수도 있다. 예전엔 `Update`에만 있어서, 단어 효과로 0이 되면 `OnTimeExpired`가 영영 안 나가고 **타이머가 0에 멈춘 채 입력만 계속 열려 있는** 버그가 났다.
  - **`ResetToFull()`은 게이지를 최대치로 되돌리되 카운트다운을 시작하지 않는다.** 턴 전환·스테이지 시작 대기 동안 게이지가 0에 붙어 있지 않고 가득 찬 채 멈춰 있게 하기 위한 것이고, 실제 시작은 대기가 끝날 때 `RestartTurn()`이 한다. `_running = false`를 포함하므로 `StopTimer()`를 대체한다.
    - ⚠️ **`ResetToFull`은 `OnTimeAdjusted`를 쏘지 않는다.** 그 이벤트는 "단어 효과로 시간이 변했다"는 신호 전용이라, 여기서 쏘면 대기에 들어갈 때마다 `TimerView`가 초록색으로 반짝인다.
    - `Start()`도 `RestartTurn()`이 아니라 이걸 부른다. 프로젝트에 스크립트 실행 순서 설정이 없어서(`DefaultExecutionOrder` 없음) `StageManager.Start()`와의 순서가 정해져 있지 않은데, `TimerManager`가 나중에 돌면 카운트다운이 시작되어 **첫 스테이지 대기 동안 게이지가 줄어든다.** `ResetToFull`이면 어느 순서로 돌든 최종 상태가 "가득 참 + 정지"라 결과가 같다.
- **`UI/TimerView`** — 슬라이더 + 증가 초록 / 감소 빨강 반짝임.

### 씬 전환과 일시정지 (`02_Scripts/UI/`, `GameScenes.cs`)

- **`GameScenes`** — 씬 이름 상수만 담은 정적 클래스. 씬 전환은 전부 여기를 거친다.
- **`TitleMenu`** — 타이틀 버튼 3개 배선. `Awake`에서 **`Time.timeScale = 1f`로 되돌리는 게 핵심**이다(일시정지 상태로 타이틀에 돌아오면 멈춘 채로 뜬다). `옵션`은 코드에서도 `interactable = false`로 잠가둔다 — 구현할 때 그 줄을 지울 것. 종료는 `#if UNITY_EDITOR` 분기가 있어야 에디터에서도 반응한다.
- **`PauseManager`** — ESC 토글. **버튼이 아니라 명령 단어 타이핑으로 조작한다** — 멈춘 동안 입력창에 `계속`/`타이틀`을 친다(단어는 인스펙터의 `resumeWord`/`titleWord`).
  - **`Time.timeScale = 0` 하나로 게임이 통째로 멈춘다.** 시간에 의존하는 코드가 전부 `deltaTime`/`WaitForSeconds` 기반이기 때문이다 — 턴 전환 대기, 쌓인 공격 재생, 스테이지 시작 대기, 말풍선, 타이머 감소, 카드 애니메이션. **개별 정지 로직을 만들 필요가 없고, 새로 시간에 의존하는 코드를 넣을 때 `unscaledDeltaTime`을 쓰면 일시정지 중에도 계속 돌아 이 전제가 깨진다.**
  - ⚠️ **일시정지 중에는 입력을 끄지 않고 오히려 켠다.** 명령 단어를 쳐야 하기 때문이다. 대신 멈추기 전 상태를 `InputManager.IsInputEnabled`로 기억해 두고, `Resume()`에서 **원래 잠겨 있었다면 도로 잠근다**(턴 전환 대기나 결과 화면에서 멈춘 경우). 이 복원을 빼면 열리면 안 될 타이밍에 타이핑이 열린다.
  - ⚠️ **`CardInputHandler.Evaluate`가 `timeScale == 0`이면 즉시 리턴한다.** 이게 없으면 일시정지 중 `계`를 치는 순간 손패에 없는 단어라 오타로 처리되어 그 자리에서 `ClearInput()`이 불리고, **명령 단어를 끝까지 칠 수 없다.**
  - 조합 중에도 평가한다 — `계속`의 마지막 음절 `속`은 뒤에 이어질 글자가 없어 IME가 커밋하지 않는다(`CardInputHandler`와 같은 이유). 접두사 판정도 같은 `InputManager.IsValidProgress`를 쓴다.
  - `PauseManager`는 **항상 켜져 있는 오브젝트**에 붙여야 한다. 일시정지 창 루트에 붙이면 그게 평소 비활성이라 `Update`가 돌지 않아 ESC를 못 받는다.
  - `timeScale`은 씬을 넘어가도 유지되므로 `ReturnToTitle`이 1로 되돌린 뒤 씬을 불러온다.

### 에디터 도구 (`02_Scripts/DeckManager/Editor/`)

둘 다 손으로 24줄을 드래그하다 빠뜨리거나 중복시키는 사고를 막기 위한 1회성 도구다(실제로 어퍼컷 11중복 + 3장 누락이 났던 적 있다).

- `CardDataSeeder` — `Tools > Deck Manager > Seed Missing Word Cards`. 카드 `.asset`을 `AssetDatabase`로 생성.
- `WordUnlockPopulator` — `Tools > Deck Manager > Populate Word Unlock Manager`. 씬의 `WordUnlockManager`에 카드를 채운다. 씬 컴포넌트라 `EditorSceneManager.MarkSceneDirty`가 필요하다.
  - **이미 목록에 있는 카드는 건너뛴다** — 24장이 다 들어있는 지금 다시 눌러도 아무것도 덮어쓰지 않으니, 손으로 맞춘 `grantedAtStart` 체크는 안전하다. 새 카드를 만든 뒤 추가로 채울 때만 의미가 있다.
  - ⚠️ 코드에 하드코딩된 `StartingWordNames`(9장)는 **씬의 실제 시작 단어 3장과 다르다.** 새로 추가되는 카드에만 적용되는 값이라 지금은 무해하지만, 이걸 시작 단어의 출처로 읽지 말 것 — 실제 출처는 씬의 `grantedAtStart`다.

### 아직 없는 것

상태이상 실제 부여(`StatusEffect`는 계산만 됨), 데빌의 받는 피해 감소, 럭키 보상, 어썸 누적 카운트(0 고정 + TODO 주석), 플레이어 단어 선택 UI(지금은 클리어 시 자동 지급), 단어 강화/합성, 단어별 사용 횟수 제한, 저장/불러오기, `StageData` SO.

**옵션 화면이 없다.** 타이틀의 `옵션` 버튼은 자리만 잡아둔 채 `TitleMenu.Awake`에서 `interactable = false`로 잠겨 있다.

**저장이 없어서 타이틀로 돌아가면 런이 초기화된다.** 해금한 단어와 스테이지 진행이 전부 사라지고 시작 단어 3장부터 다시 시작한다. 의도된 현재 상태다(`StageManager.RestartStage`만 사전을 유지한다).

**연출/애니메이션 전반이 아직 없다.** 적 공격 모션, 피격 반응, 스테이지 전환 연출이 전부 미구현이고, 지금은 위의 딜레이 값들이 그 시간만큼 화면을 멈춰두고 자리만 잡고 있다. 유일한 예외가 두 가지 — 플레이어 캐릭터의 눈 깜빡임(`PlayerVisual`/`PlayerBlink`), 그리고 턴 종료 시 쌓인 공격이 `pendingActionInterval` 간격으로 터지며 HP가 계단식으로 줄어드는 구간이다.

## 컨벤션

- C# 네임스페이스 없음 — 전부 전역 네임스페이스.
- 싱글턴/서비스 로케이터/DI 없음: 모든 컴포넌트 간 의존은 인스펙터에서 손으로 연결하는 `[SerializeField]` 참조. 런타임에만 알 수 있는 의존은 `Bind(...)` 메서드를 명시적으로 둔다(`CardSlotView` 참조) — 조회하지 말 것.
- 씬 오브젝트 참조는 프리팹 에셋에 저장되지 않는다. 프리팹이 씬 컴포넌트를 필요로 하면 스포너가 `Bind()`로 넘겨준다(`HandFanLayout` → `CardSlotView`의 `InputManager`).
- ⚠️ **매니저 프리팹의 인스펙터 연결은 프리팹이 아니라 씬 인스턴스에 있다.** `03_Prefabs/Managers/`의 넷은 서로와 씬 오브젝트(`player`/`enemySpawnPoint`/각종 UI)를 참조하는데, 그 참조는 프리팹 에셋에 담길 수 없으므로 전부 인스턴스 오버라이드로만 존재한다. 따라서:
  - 인스턴스에서 **Apply / Apply All을 누르지 말 것** — 씬 참조가 프리팹 쪽에서 null이 되고, 그 프리팹을 다시 인스턴스화하면 "아무 일도 안 일어남" 상태가 된다. 프리팹은 Override 상태로 두는 게 정상이다.
  - 프리팹을 다시 씬에 끌어다 놓으면 참조가 하나도 안 붙어 온다. 연결을 손으로 전부 다시 채워야 한다.
  - 매니저에 `[SerializeField]`를 추가했다면 프리팹이 아니라 **씬 인스턴스에서** 채우고 `SampleScene.unity`를 커밋할 것.
- 로직 vs 뷰 분리: "매니저"가 상태와 판단을 소유하고, "뷰"(`InputFieldDisplay`/`CardSlotView`/`WordChainView`/`TimerView`)는 그걸 UI에 비추기만 하며 게임 판단을 하지 않는다.
- 이벤트는 평범한 C# `event Action`/`event Action<T>`, `OnEnable`에서 구독하고 `OnDisable`에서 해제.
- 새 스크립트는 `[SerializeField] private` + `[Header]`/`[Tooltip]`, 식 본문 읽기 전용 프로퍼티, 한글 주석. 편집 중인 파일의 스타일에 맞출 것. (`BattleManager`/`StageManager`/`EnemyManager`는 이 컨벤션보다 먼저 작성된 코드라 스타일 참고 대상이 아니다.)
- 인스펙터 참조가 비어 있으면 조용히 `return`하지 말고 필드명을 담은 `Debug.LogWarning(..., this)`를 남길 것 — 에디터에서 조용한 실패는 진단이 매우 어렵다. **실제로 이번 프로젝트에서 연결 누락으로 인한 "아무 일도 안 일어남" 버그가 여러 번 났다.**
- 이름이 의도적인 경우가 있다(씬의 `Deck Manager` GameObject는 공백 포함). 과거 진짜 오타(`InputManger`, `DeckManger.cs`)는 이미 수정됐으니 추가로 이름을 바꾸지 말 것.
- **"일시정지 중인가"는 `PauseManager` 참조 대신 `Mathf.Approximately(Time.timeScale, 0f)`로 판단한다**(`BattleManager.Update`, `CardInputHandler.Evaluate`). `PauseManager`는 씬 오브젝트고 그 둘은 프리팹이라, 참조로 엮으면 씬 인스턴스 오버라이드가 늘어나기만 한다. 새로 같은 판단이 필요하면 이 방식을 따를 것.
- **시간에 의존하는 새 코드는 `Time.deltaTime`/`WaitForSeconds`를 쓸 것.** 일시정지가 `timeScale = 0` 하나로 성립하는 게 그 덕분이다. `unscaledDeltaTime`/`WaitForSecondsRealtime`을 쓰면 일시정지 중에도 계속 돌아 그 전제가 깨진다(현재 `unscaledTime`을 쓰는 곳은 `InputManager.ForceHangulMode`의 IME 쿨다운뿐이고, 그건 멈춰도 계속 동작해야 하므로 의도된 예외다).

## 알려진 이슈

- **한/영 IME — 해결됨.** 증상은 두 갈래였는데 원인이 하나였다.
  - "Play 직후엔 입력창을 클릭하거나 Alt를 눌러야 조합이 시작된다" → `imeCompositionMode`가 기본값 `Auto`였던 탓이다. **`EnableInput()`에서 `On`으로 고정**하고, 그 값을 되돌리는 주범이던 `TMP_InputField`를 라벨로 교체해 해결했다.
  - "IME가 영문 모드면 아무것도 입력되지 않는다" → `HangulImeMode`(IMM32 `ImmSetConversionStatus`)로 한글 모드를 강제하고, 라틴 글자가 들어오면 자가 복구한다.
  - **되돌리면 재발하는 것들**: `imeCompositionMode = On` 고정, `TMP_InputField`를 쓰지 않는 결정, `HangulImeMode` 재적용 지점 세 곳. 셋 중 하나만 빠져도 "클릭해야 입력됨" 또는 "영문일 때 먹통"이 그대로 돌아온다.
  - 이미 시도했다 되돌린 것: P/Invoke `ImmSimulateHotKey`(한/영 핫키 시뮬레이션) — 효과 없었으니 다시 시도하지 말 것. 지금 쓰는 `ImmSetConversionStatus`는 핫키를 흉내내지 않고 변환 상태를 직접 쓰는 다른 API다.
  - **앞으로 IME 쪽을 건드리면 반드시 빌드에서 재검증할 것.** 이 문제는 과거에 **에디터 전용이 아니라 스탠드얼론에서도 재현된 이력**이 있다. `HangulImeMode`가 실패하면 `Player.log`에 경고 한 줄이 남는다(첫 실패만). 실패 시의 폴백은 비한글 입력 감지 시 "한/영 키를 눌러주세요" 힌트를 띄우는 것 — 클릭 단계는 이미 없어졌으므로 그때도 남는 건 한/영 한 번뿐이다.
- **한 음절 단어 미매칭** — `CardInputHandler`가 `OnCompositionChanged`도 구독하고 `CurrentInput + Composition`으로 매칭하도록 바꿔 **해결됨.** 커밋만 기다리는 구조로 되돌리면 퀵/잽/훅이 다시 완성 불가가 된다.
- **손패 들림 판정의 절충** — 조합 중 글자는 초성만 비교한다. 모음을 잘못 짚어도 초성이 같으면 카드가 계속 떠 있다. 정밀 검증은 유니코드 분해가 훨씬 깊어져 의도적으로 하지 않았다.
- **`Colorful`(컬러풀)의 단순화** — GDD는 "화상 > 마비 > 얼음 순으로 전부 부여"지만 `StatusEffectType`이 단일 값이라 셋을 동시에 담을 수 없다. 지금은 우선순위가 가장 높은 화상 하나만 나오게 단순화되어 있다. 상태이상 다중 적용이 필요해지면 `ResolvedAction.StatusEffect`를 리스트로 바꿔야 한다.
