# 도전과제 키맵

`Assets/04_Data/Resources/AchievementDefinitions.json`의 **19종**을 Steam API Name ↔ 게임 내 달성 조건으로 대조한 표.

**파트너 사이트에 올릴 원본이 이 JSON이다.** 게임은 이름·설명을 화면에 그리지 않는다 — 표시 문구는 Steam이 언어별로 들고 있고, `koName`/`enName`을 읽는 코드는 에디터 디버그 메뉴(`Tools > Achievements`) 하나뿐이다. 그런데도 JSON에 두는 이유는 "이 게임에 어떤 도전과제가 있는가"의 단일 출처를 한 파일로 만들기 위해서다(`AchievementDefinition.cs` 주석 참조).

| | |
|---|---|
| 도전과제 | 19종 |
| 달성 시점 | 3구간 (실행 1 · 전투 중 6 · 전체 클리어 12) |
| 조건 종류 | `AchievementCondition` 13개 — **전부 배선됨** |
| 현재 실제 해금 | **0종** (아래 Blocker) |

---

## ⚠️ Blocker — App ID가 아직 `480`(Spacewar)이다

저장소 루트 `steam_appid.txt`가 Valve의 테스트 앱 `480`으로 되어 있다. 아래 19개 id는 Spacewar에 존재하지 않으므로 `SetAchievement`가 `false`를 돌려주고 **실제로는 아무것도 해금되지 않는다.**

초기화·콜백 펌프는 정상적으로 돌고 예외도 안 나기 때문에, 반환값 로그를 보지 않으면 **"조건을 아직 못 채운 것"과 구분되지 않는다.** 도전과제는 조건이 맞을 때까지 아무 일도 안 일어나는 시스템이라 증상이 조용하다.

진짜 App ID를 받으면 **① `steam_appid.txt` ② 파트너 사이트의 API Name 19개**를 아래 키와 글자 그대로 맞출 것. 대소문자·언더바 하나만 달라도 같은 증상이 난다.

## ⚠️ Gap — 표시 문구가 한국어·영어 두 벌뿐이다

게임은 5개 언어(한·영·불·서·일)를 지원하지만 JSON의 문구 칸은 `koName`/`koDesc`·`enName`/`enDesc` 두 벌뿐이다. 파트너 사이트는 언어별 문구를 따로 받으므로 **불어·스페인어·일본어 플레이어는 Steam 폴백(영어)을 보게 된다.** 카드·대사가 5개국어인 것과 어긋나는 유일한 자리다.

- `ACH_CLEAR_ENGLISH`·`ACH_CLEAR_SPANISH`·`ACH_CLEAR_FRENCH`는 **koName도 비한국어**다(`Easy as ABC`, `¡Olé!`, `C'est la vie`) — 의도된 말장난이라 오타가 아니다.
- `¡Olé!` · `C'est la vie` · `一撃必殺`는 비ASCII 문자를 포함한다. 파트너 사이트에 붙여 넣은 뒤 인코딩이 깨지지 않았는지 확인할 것.

---

## 시점 1 — 게임 실행 (1종)

씬과 무관해서 `AchievementManager`(전투 씬 전용)가 아니라 `SteamAchievementService.UnlockLaunchAchievements`가 처리한다. `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`라 로고 스플래시에서 이미 올라간다. id를 코드에 박지 않고 조건으로 찾으므로 같은 조건을 더 만들어도 JSON만 고치면 된다.

| API Name | 이름 | 조건 |
|---|---|---|
| `ACH_JOURNEY_BEGINS` | 모험의 시작 / A Journey Begins | `GameLaunched` |

## 시점 2 — 전투 중 (6종)

`AchievementManager`가 `BattleManager`·`StageManager`·`WordChainManager`의 이벤트를 받아 그 자리에서 판정한다. 런을 끝까지 갈 필요가 없어 **검증이 가장 쉬운 구간**이다 — 디버그 킬스위치 `9`(적 즉사)로 대부분 재현된다.

| API Name | 이름 | 조건 | 비고 |
|---|---|---|---|
| `ACH_FIRST_STRIKE` | 첫 번째 펀치 / First Strike | `EnemyDefeated` | `HandleEnemyDefeated` |
| `ACH_HELICOPTER_MOM` | 헬리콥터맘 / Helicopter Mom | `BossEncountered` | `HandleStageLoaded` · 전투 인덱스 4 / 9 |
| `ACH_SUCCEEDING_MAMA` | 패륜아 / Succeeding you MAMA | `BossDefeated` | `isMotherDragon` |
| `ACH_DIE_HARD` | 다이하드 / Die Hard | `PlayerHPAtKillAtMost` ≤ **10** | 처치 *순간*의 플레이어 HP |
| `ACH_COMBO_10` | 10단 콤보 / 10-Hit Combo | `ActionsInTurnAtLeast` ≥ **10** | 자체 카운터 `_actionsThisTurn` — 턴 전환과 스테이지 로드 **양쪽**에서 리셋 |
| `ACH_SYNERGY_5` | 궁극의 필살기 / Ultimate Finisher | `SynergiesInChainAtLeast` ≥ **5** | 시너지 = `Action`·`Command`가 아닌 카드 |

> ⚠️ `ACH_COMBO_10`의 카운터를 `SkillResolver.ActionsThisTurn`으로 바꾸지 말 것. 그 값은 `Resolve` 맨 끝에서 증가하는데 스크립트 실행 순서 설정이 없어 1 차이로 흔들린다.

## 시점 3 — 전체 클리어 (12종)

결과 화면이 **`GameClear`일 때만** 평가한다(일반 스테이지 승리 `Victory`로도 같은 이벤트가 오므로 반드시 걸러낸다). 19종 중 **12종이 여기** 묶여 있어서 하나를 확인하려면 13전투를 끝까지 가야 한다 — `Tools > Achievements`의 초기화 메뉴가 사실상 필수인 이유다(Steam은 한 번 올린 도전과제를 게임 재실행으로 되돌리지 않는다).

| API Name | 이름 | 조건 |
|---|---|---|
| `ACH_CLEAR_KOREAN` | 세종대왕 / King Sejong | `ClearInLanguage` = `Korean` |
| `ACH_CLEAR_ENGLISH` | Easy as ABC | `ClearInLanguage` = `English` |
| `ACH_CLEAR_SPANISH` | ¡Olé! | `ClearInLanguage` = `Spanish` |
| `ACH_CLEAR_FRENCH` | C'est la vie | `ClearInLanguage` = `French` |
| `ACH_CLEAR_JAPANESE` | 一撃必殺 / Ichigeki Hissatsu | `ClearInLanguage` = `Japanese` |
| `ACH_NO_DAMAGE` | 금강불괴 / Invulnerable | `ClearWithNoDamage` |
| `ACH_FEW_ACTIONS` | 선택과 집중 / Narrow and Deep | `ClearWithActionsAtMost` ≤ **3** |
| `ACH_FEW_SYNERGIES` | 어휘력 부족 / Lost for Words | `ClearWithSynergiesAtMost` ≤ **3** |
| `ACH_DECK_15` | 덱이야 사전이야 / Deck or Dictionary? | `ClearWithDeckSizeAtLeast` ≥ **15** |
| `ACH_MINIMALIST` | 미니멀리스트 / Minimalist | `ClearWithDeckChange` · added `Zero` · removed `AtLeastOne` |
| `ACH_HOARDER` | 수집광의 고집 / Hoarder's Pride | `ClearWithDeckChange` · added `AtLeastOne` · removed `Zero` |
| `ACH_STOCK_DECK` | 튜닝의 끝은 순정 / Stock Standard | `ClearWithDeckChange` · added `Zero` · removed `Zero` |

> ⚠️ **`ACH_NO_DAMAGE`는 `다시하기` 뒤에는 안 뜬다.** `StageManager.RestartStage()`가 씬을 다시 읽지 않고 `StatisticsManager`를 그대로 두기 때문에 누적 피해가 이어진다. 의도된 동작이지만 버그로 오해하기 쉽다.

---

## 부록 — 파트너 사이트 입력용

Steam 파트너 사이트의 *Achievement Configuration*에 그대로 넣을 값. **API Name 열이 JSON의 `id`와 글자 그대로 같아야 한다.**

| API Name | Display (KO) | Display (EN) | Description (EN) |
|---|---|---|---|
| `ACH_JOURNEY_BEGINS` | 모험의 시작 | A Journey Begins | Launch the game. |
| `ACH_FIRST_STRIKE` | 첫 번째 펀치 | First Strike | Defeat your first enemy. |
| `ACH_HELICOPTER_MOM` | 헬리콥터맘 | Helicopter Mom | Encounter Magmamater. |
| `ACH_SUCCEEDING_MAMA` | 패륜아 | Succeeding you MAMA | You defeated Mom! But why...? |
| `ACH_CLEAR_KOREAN` | 세종대왕 | King Sejong | Clear the game in Korean. |
| `ACH_CLEAR_ENGLISH` | Easy as ABC | Easy as ABC | Clear the game in English. |
| `ACH_CLEAR_SPANISH` | ¡Olé! | ¡Olé! | Clear the game in Spanish. |
| `ACH_CLEAR_FRENCH` | C'est la vie | C'est la vie | Clear the game in French. |
| `ACH_CLEAR_JAPANESE` | 一撃必殺 | Ichigeki Hissatsu | Clear the game in Japanese. |
| `ACH_NO_DAMAGE` | 금강불괴 | Invulnerable | Clear the game without taking any damage. |
| `ACH_DIE_HARD` | 다이하드 | Die Hard | Defeat an enemy with 10 HP or less remaining. |
| `ACH_COMBO_10` | 10단 콤보 | 10-Hit Combo | Play 10 or more Action cards in a single turn. |
| `ACH_SYNERGY_5` | 궁극의 필살기 | Ultimate Finisher | Execute an attack with 5 or more active Synergies. |
| `ACH_FEW_ACTIONS` | 선택과 집중 | Narrow and Deep | Clear the game with 3 or fewer Action cards in your deck. |
| `ACH_FEW_SYNERGIES` | 어휘력 부족 | Lost for Words | Clear the game with 3 or fewer Synergy cards in your deck. |
| `ACH_MINIMALIST` | 미니멀리스트 | Minimalist | Clear the game after removing at least 1 card and adding zero new cards. |
| `ACH_HOARDER` | 수집광의 고집 | Hoarder's Pride | Clear the game after adding at least 1 card and removing zero cards. |
| `ACH_DECK_15` | 덱이야 사전이야 | Deck or Dictionary? | Clear the game with 15 or more unique card types in your deck. |
| `ACH_STOCK_DECK` | 튜닝의 끝은 순정 | Stock Standard | Clear the game maintaining your exact starting deck. |

---

출처 · `Assets/04_Data/Resources/AchievementDefinitions.json` (19행) · 조건 종류 `AchievementCondition` 13개 전부 배선됨(12개 `AchievementManager`, `GameLaunched` 1개 `SteamAchievementService`) · `AchievementDatabase`가 로드 시점에 id 중복 · 조건 이름 오타 · `threshold` 0 · `addedRule` 누락을 전부 **에러**로 낸다.
