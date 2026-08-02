# SYLEE 브랜치 머지 시 변경사항

작성일: 2026-08-02 · 대상: **SeohyeonMin 브랜치와의 머지**

---

## 0. 먼저 알아야 할 브랜치 상태

| 브랜치 | 커밋 | 관계 |
|---|---|---|
| `main` | `ba55bae` | — |
| `SYLEE` | `ba55bae` | **main과 동일 커밋** |
| `SeohyeonMin` | `b26ea6f` | **이미 `1b63dd5`로 SYLEE에 머지 완료된 조상 커밋** |

**`SeohyeonMin`은 지금 SYLEE보다 뒤처져 있고, SeohyeonMin의 기존 작업은 전부 SYLEE 안에 들어와 있다.** 즉 지금 시점에 `SeohyeonMin ← SYLEE` 머지는 **fast-forward**라 커밋 충돌이 없다.

⚠️ **다만 이 문서의 내용은 아직 한 줄도 커밋되지 않았다.** 아래 변경은 전부 **워킹 트리 상태**다. SeohyeonMin 쪽에서 `b26ea6f` 위에 새 작업을 얹고 있다면, 그 작업이 아래 "충돌 위험" 파일을 건드리는 순간 충돌이 난다.

**권장 순서**: ① SYLEE에서 아래 변경을 커밋 → ② SeohyeonMin이 SYLEE(또는 main)를 먼저 받아 최신화 → ③ 그 위에서 새 작업 진행.

---

## 1. 이번 변경의 요지

두 덩어리다.

1. **타이핑 입력 핸들러 통일** — 입력 라우팅 소유권을 `InputManager`로 옮기고, 세 곳에 복붙돼 있던 매칭 파이프라인을 베이스 클래스로 모았다.
2. **카드 보상 선택제** — 클리어 시 3장을 자동 지급하던 것을, 3장 중 하나를 타이핑으로 고르거나 `넘기기`로 건너뛰는 방식으로 바꿨다.

여기에 **화면에 나가는 글자·이미지를 전부 인스펙터로 빼는 작업**이 함께 들어갔다.

---

## 2. SeohyeonMin과 충돌할 수 있는 파일

SeohyeonMin이 과거에 건드렸고 **이번에 우리도 건드린** 파일이다. SeohyeonMin에서 `b26ea6f` 이후 새 작업이 있다면 여기를 먼저 볼 것.

| 파일 | 우리 변경 규모 | 주의 |
|---|---|---|
| `Assets/00_Scenes/SampleScene.unity` | +136 / −10 | **상시 충돌 대상.** 손으로 해소하면 컴파일은 통과해도 연결이 조용히 빠진다 |
| `Assets/02_Scripts/BattleManager.cs` | +275 / −(대폭) | 결과 화면 API가 **바뀌었다**(아래 3-③) |
| `Assets/02_Scripts/DeckManager/DeckManager.cs` | +13 | 주석만 갱신 — 충돌해도 내용 손실 위험 낮음 |
| `Assets/02_Scripts/DeckManager/UI/RewardCardView.cs` | +125 | 거의 새로 씀 |
| `Assets/03_Prefabs/Card.prefab` | — | **우리 변경 아님**(이번 작업 전부터 수정 상태였음) |
| `Assets/03_Prefabs/MotherDragon.prefab` | — | **우리 변경 아님**(위와 동일) |

> `Card.prefab` / `MotherDragon.prefab` / `01_Arts/Card/Back.png` / `01_Arts/Mommy/`는 이번 작업 이전부터 워킹 트리에 있던 변경이다. 이 문서의 범위 밖이니 머지 때 별도로 확인할 것.

---

## 3. 깨지는 API — 다른 브랜치 코드가 부르고 있다면 고쳐야 한다

### ① `BattleManager.ShowResult`의 시그니처가 바뀌었다

```csharp
// 이전
public void ShowResult(string message)      // ShowResult("VICTORY!")

// 지금
public void ShowResult(ResultKind kind)     // ShowResult(ResultKind.Victory)
```

`ResultKind`는 `Victory` / `Defeat` / `GameClear` 셋이다. 제목 문자열은 이제 `BattleManager` 인스펙터의 `ScreenPresentation` 세 벌에서 나온다.

호출부는 우리 쪽에서 전부 고쳤다(`BattleManager` 내부 3곳, `EventManager.EndEvent` 1곳). **다른 브랜치에 `ShowResult("...")` 호출이 있으면 컴파일 에러가 난다.**

새로 생긴 것: `public void RefreshResult()` — 결과 화면을 지금 상태로 다시 그린다(보상 선택이 끝난 뒤 `StageManager`가 부른다).

### ② `WordUnlockManager.GrantStageClearReward()`가 사라졌다

```csharp
// 이전 — 뽑기와 사전 등록을 한 번에 했다
IReadOnlyList<CardBase> GrantStageClearReward();

// 지금 — 둘로 갈렸다
IReadOnlyList<CardBase> RollRewardCandidates();   // 후보만 뽑는다. 사전에 넣지 않는다
bool ConfirmReward(CardBase card);                 // 고른 한 장만 사전에 넣는다
bool TryConsumeBonusRound();                       // 럭키 라운드가 남았으면 하나 소비
```

⚠️ **인스펙터 필드 이름도 바뀌었다**: `luckyBonusWords` → **`luckyBonusRounds`**. 럭키의 의미가 "보상 카드 +1장"에서 **"보상 창을 한 번 더 띄움"**으로 바뀌었기 때문이다. 프리팹에 직렬화돼 있던 값 `1`은 유실되지만 새 코드 기본값도 `1`이라 결과는 같다.

`wordsPerReward`(3)는 이름 그대로지만 의미가 "지급 장수" → **"보여줄 후보 장수"**로 바뀌었다.

### ③ `StatusEffectManager`의 표시 이름 두 개를 삭제했다

```csharp
public static string GetDisplayName(StatusEffectType effect);   // 삭제
public static string DevilDisplayName { get; }                  // 삭제
```

`DevilDisplayName`은 **호출자가 0곳인 죽은 코드**였고, `GetDisplayName`은 화상 말풍선 한 곳만 쓰는 간접층이었다. 그 한 곳을 인스펙터 필드 `burnBubbleFormat` / `burnBubbleFormatEn`(`화상 {0}` / `BURN {0}`)로 접었다. **다른 브랜치가 이 둘을 부르고 있으면 컴파일 에러가 난다.**

### ④ `InputManager.OnSubmit`(엔터) 이벤트를 삭제했다

구독자가 0명이었다. 대신 `OnCancel`(ESC) / `OnAdvance`(스페이스)가 생겼다.

### ⑤ `EventManager.IsEventActive` 공개 프로퍼티를 삭제했다

읽는 코드가 0곳이었다. private 필드는 내부 가드용으로 남아 있다.

### ⑥ `ResultPresentation` → `ScreenPresentation` 이름 변경

결과 화면과 보상 화면이 같은 구조를 쓰게 되어 이름을 일반화했다. `MonoBehaviour`가 아니고 필드 이름을 바꾸지 않아 **씬 배선은 그대로 살아 있다.**

---

## 4. 새로 생긴 파일

| 파일 | 무엇 |
|---|---|
| `02_Scripts/InputManager/TypingReceiver.cs` | 타이핑 수신자 베이스 + `TypingPriority` enum |
| `02_Scripts/InputManager/CommandWordReceiver.cs` | 명령 단어 수신자 베이스(안내 문구 조립 포함) |
| `02_Scripts/InputManager/TypedCommand.cs` | 명령 단어 한/영 + 안내 문구를 묶은 `[Serializable]` |
| `02_Scripts/UI/ScreenPresentation.cs` | 화면 제목(한/영)·이미지·색 묶음 + `ResultKind` enum |
| `02_Scripts/DeckManager/RewardInputHandler.cs` | 보상 카드 선택 수신자 |
| `03_Prefabs/Managers/RewardInputHandler.prefab` | 위 컴포넌트의 매니저 프리팹 |

삭제된 파일: `02_Scripts/UI/ResultPresentation.cs`(→ `ScreenPresentation.cs`로 대체).

---

## 5. 구조 변경 — 머지 후 코드를 읽을 때 알아야 할 것

### 입력은 이제 `InputManager`가 **하나에게만** 넘긴다

예전에는 `CardInputHandler` / `PauseManager` / `ResultInputHandler` 셋이 전부 `OnCharacterEntered`·`OnCompositionChanged`를 구독해 놓고, 각자 `timeScale`·`IsGameOver`·`_isPaused`를 보며 스스로 비켜섰다.

지금은 각 수신자가 `WantsInput()`으로 "내 차례다"만 선언하고, `InputManager.DispatchToReceiver`가 우선순위로 **딱 하나**를 골라 넘긴다.

```
Pause = 20  >  Reward = 15  >  Result = 10  >  Battle = 0
```

**새 타이핑 대상을 추가할 때는 `TypingReceiver`(또는 `CommandWordReceiver`)를 상속하고 우선순위만 정하면 된다.** 기존 핸들러의 가드를 손볼 필요가 없다 — 예전 구조에서는 그게 필요했다.

`InputFieldDisplay`(입력창 미러링)와 `CardSlotView`(손패 들림 폴링)는 **바뀌지 않았다.** 뷰는 여전히 모든 이벤트를 받고, 배타적 라우팅은 게임 판단을 하는 수신자에게만 적용된다.

### ESC·스페이스도 `InputManager`를 거친다

`PauseManager`(ESC)와 `EventManager`(스페이스)가 `Keyboard.current`를 직접 폴링하던 것을 걷어냈다. `InputManager.Update`에서 **입력 잠금(`_inputEnabled`) 가드보다 위에** 읽어 이벤트로 쏜다 — 턴 전환 대기처럼 타이핑이 잠긴 구간에서도 일시정지가 걸려야 하기 때문이다. **이 순서를 뒤집지 말 것.**

### 오타가 나도 입력창을 비우지 않는다 (동작 변경)

예전에는 `PauseManager`/`ResultInputHandler`가 잘못 친 순간 입력창을 비웠고 손패만 남겨뒀다. 이제 **셋 다 남겨둔다**(베이스 기본 정책). 플레이어가 무엇을 틀렸는지 보고 백스페이스로 지운다.

막다른 골목은 없다 — 결과 화면에서 버퍼가 막혀도 ESC로 일시정지하면 `Pause()`/`Resume()`이 `ClearInput()`을 부른다.

### 보상 흐름

```
적 처치 → BattleManager.ShowResult(Victory)
            └ OnBattleEnded → StageManager.HandleBattleEnded
                 └ BeginRewardRound()
                      ├ wordUnlockManager.RollRewardCandidates()   (사전에 안 넣음)
                      ├ rewardCardView.Show(후보)
                      └ rewardInputHandler.BeginSelection(후보)
                           ↓ 플레이어가 카드 이름 또는 "넘기기"를 타이핑
                      OnSelectionFinished
                           └ TryConsumeBonusRound() ? 라운드 한 번 더 : FinishReward()
                                                                        └ RefreshResult()
```

**보상을 정하기 전에는 `다음`이 안 먹는다.** 별도 잠금 코드가 아니라 우선순위(`Reward 15 > Result 10`) 하나로 성립한다.

⚠️ `DeckManager.PlayPendingActions`에서 **`AddLuckyBonus()`는 반드시 `battleManager.UpdateUI()`보다 앞**이어야 한다. `UpdateUI → CheckGameState → ShowResult → OnBattleEnded`가 한 호출 안에서 이어지며 그 안에서 보상 라운드가 열린다.

---

## 6. 씬·프리팹 (`SampleScene.unity`)

⚠️ **매니저 프리팹 인스턴스에서 Apply / Apply All을 절대 누르지 말 것.** 씬 오브젝트 참조가 프리팹 쪽에서 null이 되어 배선이 통째로 날아간다. Override 상태로 두는 게 정상이다.

### 이미 진행된 것
- `RewardInputHandler.prefab` 인스턴스가 `SampleScene.unity`에 올라가 있다(GUID `55cb090f…` 확인).

### 머지 후 반드시 눈으로 확인할 것

1. **`StageManager`** → `Reward Input Handler` 필드. 비면 후보를 전부 지급하는 옛 동작으로 떨어지고 경고가 뜬다.
2. **`BattleManager`** → `Reward Input Handler` 필드. 비면 보상 중에도 `"다음"을 입력하세요`가 같이 떠 있다(동작 자체는 정상).
3. **`BattleManager`** → `Victory / Defeat / Game Clear Presentation` 세 개의 제목이 비어 있지 않은지. **비어 있으면 배선이 깨진 것.**
4. **`RewardInputHandler`** → `Input Manager` / `Word Unlock Manager` / `Reward Card View` / `Hint Label`.
5. **`RewardCardView`** → `Title Label`, (쓴다면) `Title Image`. **이미지를 안 쓸 거면 비워둘 것** — 비면 코드가 건너뛴다.
6. **`EventManager`** → `Input Manager`. ⚠️ **비면 스페이스로 대사를 못 넘겨 마더 드래곤 이벤트에서 진행이 막힌다**(`EndEvent`가 안 불려 결과 화면도 보상도 안 나온다). 콘솔에 경고가 남는다.
7. **폰트** — 보상 화면에 새로 만든 라벨은 **Paperlogy 계열**이어야 한다. 한글이 흐르는 라벨에 `LiberationSans SDF`를 쓰면 `□`가 된다.

### 아직 안 된 것
- `EventManager`의 `Normal Event Lines En` / `Dragon Event Lines En`이 **빈 배열**이라 영어 모드에서도 이벤트 대사만 한국어로 나온다. 한국어와 **같은 개수로** 채워야 한다(개수가 어긋나면 스페이스를 눌러야 하는 횟수가 언어마다 달라진다).

---

## 7. 검증

### 컴파일
**터미널에서 검증 가능하다.** `CLAUDE.md`는 "컴파일 경로 없음"이라고 적고 있지만 실제로는 된다:

1. `Assembly-CSharp.csproj`에서 `<HintPath>`와 `<DefineConstants>`를 추출
2. 소스는 `Assets/02_Scripts/**/*.cs` 글롭 + csproj의 `02_Scripts` 밖 항목 4개(`03_Prefabs/UI/UIStyle/Runtime/*.cs`)
3. `-r:` 하나를 손으로 추가 — **FMOD는 csproj에 HintPath가 없다**(`Library/ScriptAssemblies/FMODUnity.dll`)
4. `dotnet "<SDK>/Roslyn/bincore/csc.dll" @response.rsp` (`-target:library -langversion:9.0 -nostdlib+`)
5. rsp 안의 경로는 **Windows 형식(`C:/...`)**이어야 한다

이번 변경은 이 방법으로 **에러·경고 0건**을 확인했다(소스 65개).

### Play Mode (`TitleScene`부터)
- **보상 기본 흐름**: 클리어 → 카드 3장 + 안내 → 이름을 치면 그 카드만 획득 → 카드가 사라지고 `"다음"을 입력하세요` → `다음`
- **게이트**: 보상이 떠 있는 동안 `다음`을 쳐도 아무 일도 없어야 한다
- **하이라이트**: 치는 카드가 떠오르고 나머지가 흐려진다. **아무것에도 안 맞으면 3장 전부 흐려진다**
- **럭키**: 럭키 처치 시 보상 창이 **두 번** 뜬다
- **후보 고갈**: 디버그 킬스위치 `9`로 빠르게 진행 → 후보가 0장이면 보상 창 없이 결과 화면으로 (여기서 멈추면 안 된다)
- **오타 정책**: 일시정지·결과 화면에서 엉뚱한 글자를 쳐도 입력창에 **남아야** 한다
- **ESC**: 턴 전환 대기 중(입력 잠김)에도 일시정지가 걸려야 한다
- **디버그 키**: 일시정지·결과 화면에서 `0`/`9`가 먹지 않아야 한다
- **마더 드래곤**: 보스 클리어는 `EventManager.EndEvent` 경로로 들어온다. 이쪽 보상도 별도 확인

### 빌드
`InputManager.Update` 구조를 바꿨으므로 **IME는 스탠드얼론에서 재확인**해야 한다(에디터와 빌드가 다르게 동작한 이력이 있다). 로그: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\StreetTyper\Player.log`

---

## 8. 알려진 잔여 이슈 (이번 범위 밖)

- **이벤트 대사 중에도 손패 타이핑이 먹는다.** `IsEventActive`를 삭제하면서 라우팅에 Event 계층을 두지 않기로 했다. 대사가 끝나면 `ShowResult`로 넘어가고 `LoadStage`가 쌓인 공격을 비우므로 실질 피해는 없다.
- **밸런스**: 선택제로 바뀌면서 런 전체 획득 카드가 **3장(시작) + 스테이지당 최대 1장** 수준으로 줄었다(이전엔 스테이지당 3장). 의도한 변경이지만 손패 다양성이 크게 달라지므로 플레이 후 `wordsPerReward`나 럭키 빈도 조정 여지가 있다.
- **`StageManager`의 스테이지 상수 불일치**(`totalBattles = 10` / `totalStages = 8` / 보스 인덱스 `4, 9`) — 인덱스 9의 두 번째 보스전은 여전히 도달 불가. 이번에 통계창 분모가 `totalStages`를 읽게 되면서 이 값이 화면에 노출된다.
