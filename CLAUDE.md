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
- **테스트**: `com.unity.test-framework`는 설치되어 있으나 **테스트 어셈블리가 하나도 없다.** 여기서 "테스트"란 Play Mode 수동 확인이며, 보통 `Debug.Log` 출력을 읽는 것이다(`DeckManager.logDebugEvents`, `WordChainManager.logDebugEvents`, `WordUnlockManager.logDebugEvents`).
- **실행**: 에디터에서 Play. 헤드리스/CLI 실행 경로 없음.
- C# `LangVersion` 9.0, .NET Standard 2.1 (Mono) — 그 이상 문법은 컴파일 실패한다.

## 프로젝트 구조

최상위 에셋 폴더는 에셋 브라우저 정렬을 위해 `NN_Name` 접두사를 쓴다. 새 폴더도 이 규칙을 따를 것.

- `Assets/00_Scenes/SampleScene.unity` — **실질적으로 유일한 씬**이자 빌드 설정에 등록된 유일한 씬. 루트: `00_BOOT`, `01_CAMERA`, `02_SYSTEM`, `03_WORLD`, `04_UI`, `05_DEBUG`, `EventSystem`.
  - `02_SYSTEM`: `InputManager`, `Deck Manager`, `StageManager`, `BattleManager`, `WordDictionary`, `WordUnlockManager`
  - `03_WORLD`: `player`(자식 `PlayerVisual`/`PlayerBlink`가 눈 깜빡임 담당), `enemySpawnPoint`
  - `04_UI`: `Card Canvas`(손패·입력창·체인 텍스트), `Field Canvas`(HP/방어도/의도/타이머/결과)
- `Assets/BattleScene.unity` — 최상위에 있는 **미사용 씬**(빌드 설정 미등록, 병합 잔재). 여기에 작업하지 말 것.
- `Assets/01_Arts/Fonts/` — Paperlogy 계열 TMP 폰트. **한글 글리프를 포함한 폰트를 써야 한다.** 기본 `LiberationSans SDF`는 라틴 전용이라 한글이 `□`로 나오고 문자당 경고 하나씩 찍힌다.
- `Assets/01_Arts/Demi/` — 플레이어 캐릭터 스프라이트(`DemiOpenEyes`/`DemiClosedEyes`, Git LFS)와 애니메이터 컨트롤러·애님 클립. **눈 뜬/감은 스프라이트를 각각 별도 오브젝트로 겹쳐두고 각자 애니메이터로 깜빡임을 만드는 구조**다(씬의 `player > PlayerVisual` / `PlayerBlink`). 컨트롤러 파일명 `DemiOpneEyes_0`의 오타는 그대로 두었다.
- `Assets/03_Prefabs/` — `Card.prefab`(런타임 생성되는 손패 카드), `PlayerSpeechBubble`/`EnemySpeechBubble`, `enemy`/`strongEnemy`(스테이지별 적).
- `Assets/04_Data/Cards/` — **24개 `CardBase` 에셋**(GDD 4장 단어 사전 전체, 페인풀만 제외). `Assets > Create > Deck Manager > Cards > ...` 메뉴로 만들 것. `.asset` YAML을 손으로 작성하면 스크립트 GUID가 조용히 어긋날 수 있다.
- `Assets/04_Data/EnemyTutorial.asset` — 유일한 `EnemyData`.
- `Assets/InputSystem_Actions.inputactions` — Input System 기본 템플릿. **미사용.** 게임플레이 입력은 의도적으로 이걸 거치지 않는다(아래).

## 아키텍처

### 전체 흐름

```
키보드 → InputManager → CardInputHandler(5슬롯 매칭) → WordChainManager(조합 검증)
   → [액션 단어로 완성] → DeckManager.HandleChainCompleted
      → SkillResolver(수치 계산) → CombatManager(실제 적용) → BattleManager(UI/말풍선)
   → [타이머 0] → DeckManager.HandleTimeExpired → 적 턴 → 다음 플레이어 턴
```

`DeckManager`는 단순 파사드가 아니라 **전투 배선의 중심**이다 — 체인 완성과 타이머 만료를 받아 나머지 시스템을 순서대로 호출한다.

### 턴 전환 딜레이 — 애니메이션 자리를 미리 잡아둔 값이다

턴이 바뀔 때마다 코루틴이 사이사이 대기를 넣는다. **이 숫자들은 임의로 고른 게 아니라, 앞으로 들어올 적 공격 모션·스테이지 전환 연출의 길이를 어림잡아 미리 자리를 비워둔 것이다.** 지금은 그 시간 동안 화면에 아무 일도 안 일어나므로 "불필요한 대기"처럼 보이지만, 줄이거나 없애면 나중에 애니메이션을 넣을 자리가 사라진다. 연출이 실제로 붙을 때 그 길이에 맞춰 조정할 값들이다.

| 필드 | 코드 기본값 | 씬 현재값 | 비우고 있는 자리 |
|---|---|---|---|
| `DeckManager.turnChangeDelay` | 2 | 1 | 타이머 만료 → 적이 공격하기까지 |
| `DeckManager.postAttackDelay` | 4 | 2 | 적 공격 → 플레이어 턴 재개까지 |
| `StageManager.stageStartDelay` | 2 | 2 | 스테이지 등장 → 플레이어 턴 시작까지 |
| `BattleManager.actionBubbleDuration` | 1 | 1 | 공격 말풍선이 떠 있는 시간 |

대기 중에는 **입력이 잠기고 타이머도 멈춘다**(`DisableInput` + `StopTimer`). 대기가 끝나는 쪽에서 다시 열어주므로, 새 대기 구간을 추가할 땐 반드시 짝을 맞출 것.

### 입력 파이프라인 (`02_Scripts/InputManager/`)

타이핑 입력은 Unity Input Action 에셋/바인딩을 완전히 우회하고 `Keyboard.current`를 직접 쓴다.

- **`InputManager`** — `CurrentInput`(커밋된 문자), `Composition`(IME 조합 중 문자), 이벤트 `OnCharacterEntered(char)`/`OnCompositionChanged(string)`/`OnBackspace`/`OnSubmit`/`OnInputCleared`. `EnableInput()`/`DisableInput()`/`ClearInput()`으로 제어.
  - **한글만 받는다.** `HandleTextInput`이 `IsHangul` 아니면 즉시 리턴 — ASCII/영문은 `CurrentInput`에 도달조차 못 한다. 타이핑 경로 어디에서도 영어가 동작한다고 가정하지 말 것.
  - 문자는 `Keyboard.onTextInput`에서 온다(키 폴링 아님) — 그래야 조합된 한글 음절이 나온다.
  - `ChangeHangul()`이 RightAlt로 `Input.imeCompositionMode`를 `Auto`↔`On` 토글한다. **이게 한/영 전환 문제의 해결책이므로 건드리지 말 것.**
  - `GetLeadConsonant(char)` / `IsValidProgress(committed, composing, target)` 정적 유틸을 제공한다. 후자는 "커밋된 문자열 + 조합 중인 글자가 target 단어를 향해 여전히 유효한가"를 판정하며, **매칭 로직과 손패 애니메이션이 같은 판정을 공유**하도록 하는 단일 기준점이다. 조합 중 글자는 표준 유니코드 한글 분해 공식으로 **초성만** 비교한다(모음 단계까지 검증하지 않는 의도적 절충).
  - `OnSubmit`(Enter)은 **구독자가 없다** — 매칭은 Enter를 쓰지 않는다.
- **`InputFieldDisplay`** — 순수 뷰. `CurrentInput + Composition`을 **읽기 전용** `TMP_InputField`에 미러링한다(`readOnly = true` — `InputManager`가 OS IME 컨텍스트를 소유하므로 이게 자체 포커스를 가지면 입력이 두 번 들어간다). `SetIMECursorPosition`으로 OS IME 오버레이 위치도 맞춘다.

### 덱 / 카드 (`02_Scripts/DeckManager/`)

> 폴더명은 `DeckManager`(공백 없음)다. 예전엔 `Deck Manager`(공백 포함)였으나 이름이 바뀌었다 — 씬의 GameObject 이름은 여전히 `Deck Manager`(공백 포함)이니 혼동하지 말 것.

- **`Cards/CardBase.cs`** — 추상 `ScriptableObject`: `CardName`(타이핑할 단어 = 표시 텍스트 = 매칭 키), `Icon`, `Description`, 추상 `Category`. `enum CardCategory { Modifier, Time, Type, Action }`.
  - **`Category`는 직렬화되지 않는 계산 프로퍼티다.** 그래서 enum 값을 바꿔도 `.asset` 마이그레이션이 필요 없다.
  - `AttributeCardData.Category`는 `effectType`에서 계산된다: `RepeatAction`→`Time`, `StatusChance*`/`Bleed`→`Type`, 나머지(`LifeDrain`/`DamageReduction`/`CritMultiplier`)→`Modifier`. 즉 GDD의 "속성 및 특수효과" 한 덩어리가 세 분류로 쪼개진다.
  - ⚠️ **`AttributeEffectType`에서 `Bleed`를 삭제하지 말 것.** 페인풀이 단어 목록에서 빠져 미사용이지만, 지우면 enum 인덱스가 밀려 `Intelli.asset`(`effectType: 5` = `CritMultiplier`)이 조용히 `RepeatAction`으로 바뀐다. 직렬화되는 건 `effectType`/`actionKind`이니 **이 enum들의 순서는 절대 건드리지 말 것.**
- **`WordDictionary`** — 플레이어가 *지금* 쓸 수 있는 단어. 직렬화 필드 없는 순수 런타임 상태. `TryGetWord`/`GetRandomWord`/`AddWords`/`Clear`, `OnWordsChanged` 이벤트.
  - **슬롯 뽑기와 타이핑 검증이 둘 다 여기 하나만 바라본다.** 예전엔 같은 24장이 `CardSlotManager`와 `WordChainManager` 양쪽 인스펙터에 중복돼 있어 "슬롯엔 뜨는데 입력은 안 되는" 버그가 실제로 났었다. 이 단일 출처 구조를 깨지 말 것.
  - `AddWords`(배치)는 이벤트를 마지막에 **한 번만** 쏜다. 하나씩 넣으면 첫 단어가 들어간 순간 슬롯 5칸이 전부 그 한 단어로 채워진다.
- **`WordUnlockManager`** — 게임 전체 단어 목록(인스펙터에 24장)과 지급 로직. `WordEntry { card, grantedAtStart }`. `GrantStartingWords()`(런 시작 — 사전을 비우고 `grantedAtStart` 전부 지급), `GrantStageClearReward()`(미보유 중 랜덤 N개). 시작 단어를 별도 리스트로 두지 않고 플래그로 표현하는 게 핵심 — 별도 리스트를 두면 중복 문제가 재발한다.
- **`CardSlotManager`** — 5슬롯(`CurrentCards`/`SlotCount`/`OnSlotChanged`/`ConsumeSlot`/`RefillAll`). 사전에서 균등 랜덤으로 뽑으며 **슬롯 간 중복은 의도된 동작**(중복 방지 버전을 만들었다가 요청으로 되돌린 이력이 있으니 확인 없이 "고치지" 말 것).
  - `ConsumeSlot`(한 칸 보충)과 `RefillAll`(손패 통째로 교체)은 쓰임이 다르다. `RefillAll`은 스테이지 전환처럼 손패를 갈아엎을 때만 쓰며, 사전이 비어 있으면 들고 있던 카드를 null로 지워버리므로 아예 손대지 않고 경고만 남긴다.
  - **채우는 시점이 미묘하다.** 사전은 `StageManager.Start()`가 채우는데 Unity는 모든 `Awake`를 모든 `Start`보다 먼저 돌린다. 그래서 `Awake`에서는 배열만 잡고, `OnWordsChanged`를 받아 **빈 슬롯만** 채운다. 이 구조를 `Awake` 직접 채우기로 되돌리면 반드시 빈 사전을 보게 된다.
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
- **`DeckManager`** — 파사드 + **전투 배선**. `HandleChainCompleted`(계산→적용→타이머 반영→UI→체인 비우기)와 `HandleTimeExpired`(코루틴으로 딜레이를 두고 턴 전환)를 소유한다. `turnChangeDelay`/`postAttackDelay`가 인스펙터에 노출된다.

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
- **`CombatManager.ExecutePlayerAction`** — 상대가 있어야 알 수 있는 것만 처리(방어도 파괴, 피해 적용, 방어/회복). 아직 소비처가 없는 값들은 `LogPendingEffects`가 `[미구현]` 로그로 남긴다.
- **`CharacterStats`** — `TakeDamage(damage, ignoreDefense = false)`, `Heal`, `AddDefense`, `IncreasePower`, private `Die()` → `Destroy(gameObject)`(그래서 호출자들이 매 프레임 null 체크한다).
- **`EnemyManager`** — 가중치로 다음 의도를 굴리고(`ActionType { Attack, Defend, Buff }`) `ExecuteEnemyTurn(player)`에서 실행. **액션 enum이 두 개 있다**: 카드의 `ActionKind { Attack, Defense }`와 이것.
- **`BattleManager`** — HP/방어도/의도 UI, 말풍선, 승패 판정. `OnPlayerActionResolved(bubbleText)`는 **턴을 끝내지 않는다**(타이머가 도는 동안 여러 번 호출됨). 적 턴은 `ExecuteEnemyTurn()`으로 분리되어 있고 `DeckManager`가 부른다. 말풍선엔 스킬 이름이 아니라 적용된 수치가 뜬다.
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
- **`UI/TimerView`** — 슬라이더 + 증가 초록 / 감소 빨강 반짝임.

### 에디터 도구 (`02_Scripts/DeckManager/Editor/`)

둘 다 손으로 24줄을 드래그하다 빠뜨리거나 중복시키는 사고를 막기 위한 1회성 도구다(실제로 어퍼컷 11중복 + 3장 누락이 났던 적 있다).

- `CardDataSeeder` — `Tools > Deck Manager > Seed Missing Word Cards`. 카드 `.asset`을 `AssetDatabase`로 생성.
- `WordUnlockPopulator` — `Tools > Deck Manager > Populate Word Unlock Manager`. 씬의 `WordUnlockManager`에 24장을 채우고 시작 9장에 체크. 씬 컴포넌트라 `EditorSceneManager.MarkSceneDirty`가 필요하다.

### 아직 없는 것

상태이상 실제 부여(`StatusEffect`는 계산만 됨), 데빌의 받는 피해 감소, 럭키 보상, 어썸 누적 카운트(0 고정 + TODO 주석), 플레이어 단어 선택 UI(지금은 클리어 시 자동 지급), 단어 강화/합성, 단어별 사용 횟수 제한, 저장/불러오기, `StageData` SO.

**연출/애니메이션 전반이 아직 없다.** 적 공격 모션, 피격 반응, 스테이지 전환 연출이 전부 미구현이고, 지금은 위의 딜레이 값들이 그 시간만큼 화면을 멈춰두고 자리만 잡고 있다. 플레이어 캐릭터의 눈 깜빡임(`PlayerVisual`/`PlayerBlink`)만 유일하게 붙어 있다.

## 컨벤션

- C# 네임스페이스 없음 — 전부 전역 네임스페이스.
- 싱글턴/서비스 로케이터/DI 없음: 모든 컴포넌트 간 의존은 인스펙터에서 손으로 연결하는 `[SerializeField]` 참조. 런타임에만 알 수 있는 의존은 `Bind(...)` 메서드를 명시적으로 둔다(`CardSlotView` 참조) — 조회하지 말 것.
- 씬 오브젝트 참조는 프리팹 에셋에 저장되지 않는다. 프리팹이 씬 컴포넌트를 필요로 하면 스포너가 `Bind()`로 넘겨준다(`HandFanLayout` → `CardSlotView`의 `InputManager`).
- 로직 vs 뷰 분리: "매니저"가 상태와 판단을 소유하고, "뷰"(`InputFieldDisplay`/`CardSlotView`/`WordChainView`/`TimerView`)는 그걸 UI에 비추기만 하며 게임 판단을 하지 않는다.
- 이벤트는 평범한 C# `event Action`/`event Action<T>`, `OnEnable`에서 구독하고 `OnDisable`에서 해제.
- 새 스크립트는 `[SerializeField] private` + `[Header]`/`[Tooltip]`, 식 본문 읽기 전용 프로퍼티, 한글 주석. 편집 중인 파일의 스타일에 맞출 것. (`BattleManager`/`StageManager`/`EnemyManager`는 이 컨벤션보다 먼저 작성된 코드라 스타일 참고 대상이 아니다.)
- 인스펙터 참조가 비어 있으면 조용히 `return`하지 말고 필드명을 담은 `Debug.LogWarning(..., this)`를 남길 것 — 에디터에서 조용한 실패는 진단이 매우 어렵다. **실제로 이번 프로젝트에서 연결 누락으로 인한 "아무 일도 안 일어남" 버그가 여러 번 났다.**
- 이름이 의도적인 경우가 있다(씬의 `Deck Manager` GameObject는 공백 포함). 과거 진짜 오타(`InputManger`, `DeckManger.cs`)는 이미 수정됐으니 추가로 이름을 바꾸지 말 것.

## 알려진 이슈

- **한/영 IME 토글** — `ChangeHangul()`(RightAlt로 `Input.imeCompositionMode` 토글)로 **해결됨.** 이 메서드를 제거하면 재발한다. 과거에 시도했다 되돌린 P/Invoke(`GetAsyncKeyState` + `ImmSimulateHotKey`) 방식은 효과가 없었으니 다시 시도하지 말 것.
- **한 음절 단어 미매칭** — `CardInputHandler`가 `OnCompositionChanged`도 구독하고 `CurrentInput + Composition`으로 매칭하도록 바꿔 **해결됨.** 커밋만 기다리는 구조로 되돌리면 퀵/잽/훅이 다시 완성 불가가 된다.
- **손패 들림 판정의 절충** — 조합 중 글자는 초성만 비교한다. 모음을 잘못 짚어도 초성이 같으면 카드가 계속 떠 있다. 정밀 검증은 유니코드 분해가 훨씬 깊어져 의도적으로 하지 않았다.
- **`Colorful`(컬러풀)의 단순화** — GDD는 "화상 > 마비 > 얼음 순으로 전부 부여"지만 `StatusEffectType`이 단일 값이라 셋을 동시에 담을 수 없다. 지금은 우선순위가 가장 높은 화상 하나만 나오게 단순화되어 있다. 상태이상 다중 적용이 필요해지면 `ResolvedAction.StatusEffect`를 리스트로 바꿔야 한다.
