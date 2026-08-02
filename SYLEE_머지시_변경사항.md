# SYLEE 브랜치 머지 시 변경사항

작성일: 2026-08-02 · 기준 커밋: **`1bc534c` (Dictionary)** + 아직 커밋되지 않은 워킹 트리

> 이전 판(입력 핸들러 통일 · 카드 보상 선택제)의 내용은 **이미 `main`에 들어가 있어** 전부 걷어냈다. 이 문서는 그 이후 SYLEE가 새로 들고 있는 것만 다룬다.

---

## 0. 브랜치 상태

| 브랜치 | 커밋 | SYLEE와의 관계 |
|---|---|---|
| `SYLEE` | `1bc534c` | 기준 |
| `main` | `a74b9f5` | SYLEE가 **4 커밋 앞섬** (main에만 있는 건 없음 → fast-forward) |
| `SeohyeonMin` | `65bfa16` | SYLEE가 **1 커밋 앞섬** (fast-forward) |
| `seungju` | `cb25889` | **분기함** — SYLEE +14 / seungju +5. 유일하게 진짜 머지가 필요하다 |

SYLEE가 `main` 위에 얹는 4 커밋:

| 커밋 | 내용 |
|---|---|
| `45c8a6d` | 턴 스케일링 카드 4종 추가 + 스테이지 클리어 자동 진행("다음" 없앰) |
| `ad29c08` | 이벤트 스테이지에서 입력이 잠기던 버그 수정 |
| `65bfa16` | (머지 커밋) |
| `1bc534c` | 일시정지 중 보유 카드 목록 창 |

### ⚠️ 아직 커밋되지 않은 것

| 상태 | 파일 |
|---|---|
| 새 파일(untracked) | `Assets/02_Scripts/UI/MenuKeyboardNavigator.cs` (+`.meta`) |
| 수정 | `Assets/00_Scenes/TitleScene.unity` (+151줄) |
| 삭제 | `Assets/00_Scenes/ShortStoryScene.unity` (+`.meta`) |

앞의 둘이 **4번 항목(타이틀 키보드 조작)** 이다. `ShortStoryScene`은 `1bc534c`에 딸려 들어갔다가 워킹 트리에서 지워진 상태인데, **빌드 설정에도 없고 코드 참조도 0건**이라 지우는 게 맞아 보인다 — 다만 커밋 전에 의도한 삭제인지 확인할 것.

---

## 1. 이번 변경의 요지

### ① 턴 스케일링 카드 4종 + 클리어 자동 진행 (`45c8a6d`)

한 턴 안의 행동 수에 따라 값이 변하는 카드가 들어왔다. 액션 3장 **니킥**(위력 +10, 액션당 −1) · **춉**(액션당 +2) · **박치기**(수식어당 +2), 수식어 1장 **퍼펙트**(줄어든 초당 +2 / 늘어난 초당 −2). 단어 사전이 24장 → **28장**이 됐다.

- `CardBase`에 `TurnScalingSource` enum(`None`/`SecondsSpentThisTurn`/`ActionsThisTurn`/`ModifiersThisTurn`)이 생기고 `StatsLabel`이 `virtual`이 됐다. 액션·수식어 양쪽이 같이 쓰므로 `CardBase.cs`에 있다.
- `SkillResolver`에 **런타임 수치를 카드에 띄우는 static 후크**가 생겼다 — `AwesomeBonus`(런 단위) · `ActionsThisTurn` · `ModifiersThisTurn` · `SecondsSpentThisTurn` + `OnCardValuesChanged` 이벤트 + `ScalingBonus(source, perUnit)`. 계산과 표시가 이 함수 하나를 공유해서 카드에 뜬 숫자와 실제 피해가 어긋나지 않는다.
- `CardView`가 그 이벤트를 구독해 **손패에 남아 있는 카드의 수치 칸만** 다시 쓴다.
- **스테이지 클리어 후 `다음`을 치지 않는다.** 보상을 고르면 `StageManager.rewardAdvanceDelay`(0.6초) 뒤 코루틴이 다음 스테이지를 연다. `IsAdvancingAutomatically`가 노출되고 `BattleManager`는 그동안 `다음` 안내를 숨긴다. `다음` 자체는 전체 클리어 화면과 자동 진행이 안 걸린 경우의 탈출구로 남겨뒀다.

### ② 이벤트 스테이지 입력 잠김 버그 수정 (`ad29c08`)

`DeckManager`에 `IsEnemyDefeated()`가 생기고, 쌓인 공격 재생과 콤보 중단 판정이 `battleManager.IsGameOver` 대신 **`IsGameOver || IsEnemyDefeated()`** 를 본다.

원인: 이벤트 스테이지(마더 드래곤)는 대사가 끝날 때까지 `IsGameOver`가 false이고, `CheckGameState`가 죽은 적을 `Destroy`가 아니라 `SetActive(false)`로만 꺼서 `currentEnemy`도 null이 아니다. 그래서 죽은 적을 상대로 가짜 적 턴까지 재생하고 타이머를 다시 시작해버렸고, **보상 화면이 뜬 뒤 그 타이머가 만료되며 입력이 잠긴 채 아무도 열어주지 않았다.**

### ③ 일시정지 중 보유 카드 목록 (`1bc534c`)

일시정지에서 **`카드`**(영어 `cards`)를 치면 지금 사전에 있는 카드를 격자로 펼치고, **`닫기`**(`close`) 또는 ESC/X로 닫는다.

- 새 수신자 `CardCollectionPanel : CommandWordReceiver`, 우선순위 **`TypingPriority.CardCollection = 30`**(일시정지 20보다 위). 목록이 열려 있는 동안 `계속`/`타이틀`이 안 먹는 게 이 순서 하나로 성립한다.
- 카드는 손패·보상 화면과 **같은 `CardView.SetCard`** 로 그린다(`CardSlotView`는 꺼서 슬롯 로직을 뗀다).
- 열려 있는 동안 가리는 UI를 비켜나게 하는 **`Displaced UI`** 목록이 있다(대상 + 오프셋 px). 지금 `PauseHand`(0, −600) · `InputFieldDisplay`(0, −300)로 배선돼 있다. 원래 자리는 `Awake`에서 한 번만 읽으므로 **그 UI들의 배치는 Play 중에 옮기지 말 것.**
- `PauseManager`는 명령 카드가 **3장**이 됐다(`계속` / `카드` / `타이틀`). ESC는 목록이 떠 있으면 "한 단계 뒤로"(목록만 닫기)로 갈라진다.

### ④ 타이틀 키보드 조작 + 포인터 (**미커밋**)

`TitleScene`을 키보드만으로 조작한다. 새 컴포넌트 `MenuKeyboardNavigator` 하나를 타이틀(`Canvas`)과 옵션 창(`Option Panel`)에 하나씩 붙였다.

- 이동 **방향키 + WASD** / 확인 **Space·Enter·NumpadEnter·Z** / 닫기 **ESC·X**. 키 목록·반복 속도(0.4→0.08초)·슬라이더 증감(5%)·순환 여부가 전부 인스펙터 값이다.
- 확인은 `Button.onClick.Invoke()`, 좌우는 `Slider.value` 증감(`onValueChanged`가 돌아야 `OptionsPanel`이 볼륨과 `%`를 갱신한다). 닫기 키는 `Cancel Target`에 꽂은 버튼을 누른 것으로 친다 — **키와 닫기 버튼이 완전히 같은 경로**를 탄다.
- 포커스 표시는 `EventSystem.SetSelectedGameObject`에 맡기고, 여기에 **`Pointer`(Canvas 밑 Image)** 가 따라다닌다. 항목과 부모가 달라도 월드 좌표로 맞추므로 옵션 창 항목에도 정확히 붙는다.
- **`TitleMenu.cs` / `OptionsPanel.cs`는 한 줄도 고치지 않았다** — 기존 버튼 `onClick`과 `Close()`를 그대로 재사용한다.

---

## 2. 새로 생긴 파일

| 파일 | 무엇 |
|---|---|
| `02_Scripts/UI/CardCollectionPanel.cs` | 보유 카드 목록 수신자 + 격자 배치 + 비켜날 UI |
| `03_Prefabs/Card Collection Panel.prefab` | 목록 창 UI 프리팹 |
| `02_Scripts/UI/MenuKeyboardNavigator.cs` | **(미커밋)** 메뉴 키보드 내비게이터 + `PointerSide` enum |
| `04_Data/Cards/KneeKick·Chop·Headbutt·Perfect.asset` | 턴 스케일링 카드 4종 |

삭제된 파일 없음.

---

## 3. API — 깨지는 것은 없다, 다만 지뢰가 둘

`main...SYLEE`에서 **제거된 공개 멤버는 없다.** `CardBase.StatsLabel`이 `virtual`이 된 것(추가적 변경)뿐이라 다른 브랜치 코드는 그대로 컴파일된다.

주의할 것 둘:

1. ⚠️ **`TurnScalingSource`와 `ModifierEffectType`의 값 순서를 바꾸지 말 것.** 직렬화되는 건 인덱스라, 중간에 끼워 넣으면 기존 `.asset`이 조용히 다른 효과가 된다(`AttributeEffectType`에서 `Bleed`를 지우면 안 되는 것과 같은 이유). 새 값은 **맨 뒤에만**.
2. ⚠️ **`statsLabel`이 포맷 문자열인 카드가 있다.** 어썸 `"+{0}"`, 퍼펙트·니킥·춉·박치기 `"{0}"`. 고정 문구로 덮으면 숫자가 사라지고 적힌 글자만 뜬다 — 값이 안 보이는 게 아니라 옛 방식으로 조용히 되돌아가는 것이라 눈치채기 어렵다.

새로 생긴 공개 API(호출 가능): `SkillResolver.ScalingBonus/ResetTurn/ResetRun/OnCardValuesChanged`, `StageManager.IsAdvancingAutomatically`, `CardCollectionPanel.Open/Close/IsOpen`, `TypingPriority.CardCollection`.

---

## 4. 씬·프리팹 배선

⚠️ **매니저 프리팹 인스턴스에서 Apply / Apply All을 절대 누르지 말 것.** 씬 오브젝트 참조가 프리팹 쪽에서 null이 되어 배선이 통째로 날아간다.

### `SampleScene.unity` (커밋됨)
- `Pause Canvas`에 **`CardCollectionPanel` 컴포넌트**가 붙어 있다(패널 오브젝트가 아니라 **PauseManager와 같은 오브젝트**다 — 항상 켜져 있어야 해서다).
- `Card Collection Panel` 프리팹 인스턴스가 `Pause Canvas`의 **두 번째 자식**(`Pause Panel` 다음 = 위에 그려짐)으로 들어가 있다.
- `PauseManager.cardCollectionPanel` 연결됨. 목록 배치는 7열 / 셀 150×220 / 배율 0.6.

### `TitleScene.unity` (**미커밋**)
- `Canvas`에 내비게이터: items = `GameStart`·`Option`·`Exit`, `Blocked While Active` = `Option Panel`, `Cancel Target` 비움(타이틀 ESC는 아무 일도 안 함).
- `Option Panel`에 내비게이터: items = 볼륨 3종 → `Language` → `Exit`, `Cancel Target` = `Exit`(닫기).
- 양쪽 모두 `Pointer` 연결, `Pointer Side = Center`, 오프셋 (0, 50).
- **`EventSystem`의 `Send Navigation Events`가 꺼져 있다.** 내장 모듈이 같은 방향키·Enter를 함께 처리하면 포커스가 두 칸씩 뛴다. **머지 후 이 체크가 되살아나지 않았는지 반드시 확인할 것.**

---

## 5. `seungju`와의 충돌 (여기만 진짜 머지다)

머지 베이스 `5e01634` 기준으로 **양쪽이 같이 건드린 파일**:

| 파일 | 충돌 정도 |
|---|---|
| `Assets/00_Scenes/SampleScene.unity` | **상시 충돌.** YAML을 손으로 해소하면 컴파일은 통과해도 연결이 조용히 빠진다 |
| `Assets/02_Scripts/StageManager.cs` | **겹친다.** 클래스 필드 선언부(~40행)와 `LoadStage` 내부 둘 다 — SYLEE는 보상 자동 진행/코루틴 취소, seungju는 스테이지 시작 메시지 |
| `Assets/03_Prefabs/Pause Panel.prefab` | **내용이 완전히 동일**(확인함). 충돌하지 않는다 |

seungju 단독 변경(SYLEE는 안 건드림): `IntroScene` + `IntroManager.cs`, `GameScenes.cs`, `SoundManager.cs`, `TitleMenu.cs`, `EditorBuildSettings.asset`, FMOD 뱅크·메타데이터.

⚠️ **파일이 갈려서 git은 조용하지만 실제로 부딪히는 곳**: seungju가 `TitleMenu.cs`와 빌드 설정(인트로 씬 추가)을 고쳤고, SYLEE는 `TitleScene.unity`에 키보드 조작을 얹었다. 머지 후 **타이틀 진입 흐름이 바뀌면 `EventSystem` 설정과 내비게이터 배선을 다시 확인**해야 한다. 인트로 씬에도 같은 조작이 필요하면 `MenuKeyboardNavigator`를 그대로 붙이면 된다.

---

## 6. 검증

### 컴파일 — 터미널에서 된다

`CLAUDE.md`는 "컴파일 경로 없음"이라 적고 있지만 Roslyn으로 검증된다. **이번에 소스 목록 만드는 법을 고쳤다**:

1. `Assembly-CSharp.csproj`에서 `<HintPath>`(329개)와 `<DefineConstants>` 추출.
2. 소스는 **csproj의 `<Compile Include>` 목록**을 쓰고, 거기에 없는 새 `.cs`만 더한다. ⚠️ `Assets/**/*.cs`를 통째로 글롭하면 **FMOD 에디터 소스까지 딸려와 `CS0579`/`CS0227`(unsafe)로 실패**한다.
3. `-r:Library/ScriptAssemblies/FMODUnity.dll`을 손으로 추가(FMOD만 csproj에 HintPath가 없다).
4. `dotnet "<SDK>/Roslyn/bincore/csc.dll" @response.rsp` — `-target:library -langversion:9.0 -nostdlib+ -noconfig`.
5. rsp 안의 경로는 **Windows 형식(`C:/...`)**. Git Bash의 `/c/...`는 CS0006이 난다.

이번 변경은 이 방법으로 **에러 0건**을 확인했다(소스 68개). 경고는 프로젝트 전반의 기존 `CS0649`(SerializeField)뿐이다.

### Play Mode (`TitleScene`부터)

- **타이틀 키보드**: 방향키/WASD로 3버튼 순환 → `Space`/`Enter`/`Z`로 실행 → 포인터가 따라오는지.
- **옵션 창**: 상하로 5행 이동, 좌우로 볼륨(`%` 라벨 + **실제 소리**가 같이 변하는지), `Language` 확인 키로 한/영 전환, `ESC`·`X`·닫기 버튼이 모두 같게 닫히는지, 닫으면 포커스가 `Option`으로 돌아오는지. 마우스 클릭 병행도 확인.
- **카드 목록**: 전투 중 ESC → `카드` → 보유 카드가 다 뜨는지 → `PauseHand`와 입력창이 비켜나는지 → `닫기`/ESC/X로 닫고 원래 자리로 돌아오는지 → 목록이 떠 있는 동안 `계속`/`타이틀`이 안 먹는지.
- **턴 스케일링 카드**: 한 턴에 액션을 여러 번 완성하며 니킥/춉/박치기의 **카드 위 숫자가 실시간으로 변하는지**, 그 숫자와 실제 피해가 같은지. 니킥이 음수까지 내려가도 회복으로 둔갑하지 않는지(클램프).
- **클리어 자동 진행**: 보상을 고르면 `다음` 없이 넘어가는지, 안내가 뜨지 않는지. 반대로 배선이 빠져 자동 진행이 안 걸리면 안내가 **반드시** 떠야 한다.
- **이벤트 스테이지(마더 드래곤)**: 처치 → 대사 → 보상까지 **입력이 잠기지 않고** 이어지는지(`ad29c08`이 고친 그 경로다).

### 빌드

이번 변경은 `InputManager`를 건드리지 않았으므로 IME 재검증 부담은 없다. 다만 `MenuKeyboardNavigator`가 `Keyboard.current`를 직접 읽으므로 **스탠드얼론에서 키 입력이 그대로 먹는지**는 한 번 봐야 한다. 로그: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\StreetTyper\Player.log`

---

## 7. 알려진 잔여 이슈 (이번 범위 밖)

- `EventManager`의 `normalEventLinesEn` / `dragonEventLinesEn`이 **빈 배열**이라 영어 모드에서도 이벤트 대사만 한국어다. 한국어와 **같은 개수로** 채워야 한다(개수가 어긋나면 스페이스를 눌러야 하는 횟수가 언어마다 달라진다).
- `StageManager.prefab`의 `enemyPrefabs` 3번째 항목이 삭제된 `strongEnemy`의 **깨진 GUID**다(씬 인스턴스가 배열 크기를 1로 덮어써 가려져 있다).
- 카드 목록은 **열 때마다 다시 그린다.** 28장을 7열 × 4줄로 배치하는데, 앞으로 카드가 더 늘면 `columns`/`cellSize`/`cardScale`을 같이 줄여야 화면에 들어간다(스크롤은 없다).
- 타이틀의 버튼 `Selected Color`가 기본값(0.96 흰색)이라 **포커스 자체는 거의 안 보인다.** 지금은 포인터가 그 역할을 하고 있다.
