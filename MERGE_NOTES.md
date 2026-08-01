# 머지 전 변경사항 메모 (임시 문서)

`SeohyeonMin` 브랜치에서 이번 세션 동안 건드린 것들을 머지 충돌/혼동 방지용으로 정리한 문서. **대부분 이미 커밋됨** — 아래는 "무엇이 왜 바뀌었는지" 요약이라, 머지 후 지워도 됨.

## 1. 지금 커밋 안 된 것 (uncommitted)

- `Assets/02_Scripts/DeckManager/DeckManager.cs` — `DeckManager.TurnPhase` enum 추가 (`PlayerInput` → `ResolvingPlayerActions` → `TurnChangeRest` → `EnemyTurn` → `PostAttackRest` → 다시 `PlayerInput`). `CurrentPhase` 프로퍼티 + `OnTurnPhaseChanged` 이벤트로 지금이 턴의 어느 구간인지 다른 시스템이 알 수 있게 함. 상세는 CLAUDE.md의 `DeckManager` 항목 참고.
- `CLAUDE.md` — 위 `TurnPhase` 설명 추가.

## 2. 이번 세션에서 커밋된 것들 (요약)

### 피격 이펙트
- `HitEffectManager`(싱글턴, `Assets/02_Scripts/Combat/HitEffectManager.cs`) — 피격 시 스프라이트 범위 내 랜덤 위치에 이펙트 하나 생성. `CharacterStats.TakeDamage`가 아니라 **`DeckManager`의 펀치 루프(적 쪽)와 `EnemyManager.ExecuteEnemyTurn`(플레이어 쪽)에서 직접 호출**한다 — 데미지가 0인 조합(방어 등)도 펀치 애니메이션이 뜨면 흔들림/이펙트가 같이 나가야 해서, `Damage > 0` 조건이 아니라 "펀치가 재생됐는가" 기준으로 바뀌었다.
- `EffectBase`(`Assets/02_Scripts/Combat/EffectBase.cs`) — 이펙트 프리팹 공통 베이스. `team17_gamejam` 프로젝트의 같은 이름 스크립트를 그대로 가져옴. 머티리얼의 `_Progress`(0~1) 프로퍼티를 `lifetime` 동안 자동으로 채우고, `_Duration`/`_Seed` 프로퍼티가 있으면 레이어별 속도/랜덤 시드도 처리. 다 되면 스스로 파괴.
- `Assets/01_Arts/Shader/HitImpact.shader` — 히트 이펙트용 셰이더. `_Progress`/`_FadeStart`로 원형 윤곽선이 나타났다 사라지고, `_GrowthPower`로 커지는 속도(이징) 조절 가능(1=선형, <1=빨리 커졌다 느려짐, >1=느리게 시작해 막판에 확 커짐).

### 카메라 흔들림
- `DeckManager`에 `hitShakeDuration`/`hitShakeMagnitude` 인스펙터 필드 추가(하드코딩 `Shake(0.1f, 0.8f)` 제거).
- 펀치마다 흔들던 걸 **시퀀스당 첫 펀치 한 번만** 흔들도록 변경 — `CameraShake.Shake`가 매번 `StopAllCoroutines`로 이전 흔들림을 끊고 다시 시작해서, 펀치가 여러 번이면 쉴 새 없이 흔들리는 것처럼 보였음.

### 상태 아이콘 (HP 바)
- `StatusIconRow`(`Assets/02_Scripts/Combat/UI/StatusIconRow.cs`) — 기존 텍스트 한 줄짜리 `StatusEffectView`를 대체. 화상/마비/냉동/데빌을 아이콘 + 남은 턴 숫자로 표시. **방어(쉴드)는 상태이상이 아니라서 여기서 뺐다** — `HealthBarUI`의 기존 `defIcon`/`defText`가 담당.
  - 배치(왼쪽부터 순서대로)는 좌표 계산 없이 부모의 `Horizontal Layout Group` + 자식 순서(화상→마비→냉동→데빌)로 처리. 꺼진 아이콘은 레이아웃에서 자동으로 빠짐.
  - 숫자 텍스트는 인스펙터 연결 없이, 각 아이콘 자식의 `TMP_Text`를 `GetComponentInChildren`으로 자동 탐색.
  - 화상/마비/냉동은 적 전용, 데빌은 플레이어 전용 — `WorldAnchoredUI.Target` + `StatusEffectManager.IsCurrentEnemy`로 판별. **이 판별이 깨지면(예: 플레이어 바의 `WorldAnchoredUI.Target`이 잘못 연결) 플레이어 바에 적 상태이상이 같이 뜨는 버그가 남** — CLAUDE.md에 이미 기록된 과거 버그와 같은 패턴.

### 적 인텐트 (아이콘 + 색)
- `EnemyManager`에 `attackIcon`/`defendIcon`/`buffIcon` 스프라이트 3개, `attackColor`/`defendColor`/`buffColor` 색 3개 추가. `GetIntentIcon()`/`GetIntentColor()`로 조회.
- `GetIntentString()`이 `"Intent: Attack (10)"` 같은 라벨 문구 대신 **숫자만** 반환하도록 변경.
- `SpeechBubble.SetupIntent(icon, message, textColor)` 추가 — 아이콘과 색 입힌 텍스트를 같이 보여줌(가로 배치는 프리팹의 Horizontal Layout Group + 자식 순서가 담당). 기존 `Setup`(텍스트만)/`SetupIcon`(아이콘만)은 그대로 유지.
- `BattleManager.UpdateUI()`가 마더 드래곤은 기존 `Setup(mdIntentString)`(대사), 일반 적은 `SetupIntent(...)`을 쓰도록 분기.
- ⚠️ `BattleManager.cs`의 **적 행동 실행 직후 뜨는 별도의 임시 결과 말풍선**(`ExecuteEnemyTurn` 안 `ShowBubble(enemyManager.GetIntentString(), ...)`)은 아직 텍스트 그대로임 — `SpeechBubbleManager.ShowBubble`이 문자열만 받아서, 이것도 아이콘화하려면 별도 오버로드가 필요함(안 함).

### 텍스트 워프
- `TMPCornerWarp`(`Assets/02_Scripts/UI/TMPCornerWarp.cs`) — TMP 텍스트 각 글자의 위쪽 두 꼭짓점(좌상/우상)을 `topSkewX`만큼 오른쪽으로 밀어 이탤릭처럼 기울인다. 애니메이션 없는 정적 값. `[ExecuteAlways]`라 Play 안 눌러도 씬 뷰에서 바로 확인 가능.

### 게이지 (UIStyle)
- `TimerView`/`HealthBarUI`에서 `Slider` 필드/로직 제거, `UIStyle.SetFillAmount(ratio)`로 채우기를 대신함. 씬의 `Timer Bar`/`HPBar.prefab`에서 이제 안 쓰는 `Slider` 컴포넌트는 지워도 안전함.

### 셰이더 (배경 광선 이펙트)
- `Assets/01_Arts/Shader/UnderwaterGodRaySprite.shader` — 광선을 각도 등분 방식(`_LineCount1`/`_LineCount2`/`_LineWidth`/`_LineJitter`/`_LineSharpness`)으로 재작성, 발광 닷지 합성용 `_BackgroundTex` 추가(배경 스프라이트 텍스처를 머티리얼에 수동 연결 필요), 최종 Blend는 일반 알파(`SrcAlpha OneMinusSrcAlpha`).
- `Assets/01_Arts/Shader/SpriteOutline.shader` + `SpriteOutline.mat` — 캐릭터 스프라이트 윤곽선. 유클리드 거리 기반 팽창(dilation) + 프리멀티플라이드 알파(`Blend One OneMinusSrcAlpha`)로 반투명 가장자리에서도 윤곽선이 끊기지 않게 함. **아직 player SpriteRenderer에 실제로 연결은 안 함.**

## 3. 알아두면 좋은 것

- **`EffectManager`(범용 EffectType enum 버전)는 만들었다가 폐기했다.** 최종적으로는 단순한 `HitEffectManager` 하나로 정리됨 — 다른 브랜치에 `EffectManager` 관련 코드가 있다면 이번 작업과 무관.
- `CardInputHandler`, `WordChainManager` 등 손패/매칭 관련 파일은 이번 세션에서 안 건드림.
