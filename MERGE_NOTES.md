# 머지 전 변경사항 메모 (임시 문서)

`SeohyeonMin` 브랜치에서 작업 중 건드린 것들을 머지 충돌 대비용으로 정리한 문서. **머지 끝나면 지워도 됨.**

## 1. 이 대화(Claude)에서 만든 것

### 셰이더 / 이펙트
- `Assets/01_Arts/Shader/UnderwaterGodRaySprite.shader` — 대폭 수정
  - `_LineCount1`/`_LineCount2`/`_LineWidth`/`_LineJitter`/`_LineSharpness`로 광선을 각도 등분 방식(결정론적 "N개 선")으로 재작성. 기존 `_Freq1`/`_Freq2`(보로노이 기반) 완전히 대체됨 — **머티리얼에 남아있는 옛 `_Freq1`/`_Freq2` 값은 이제 안 쓰임.**
  - `_BackgroundTex` 추가 — 발광 닷지(Glow Dodge, `blend/(1-base)`) 합성용 배경 텍스처를 직접 참조. **`UnderwaterGodraySprite.mat`에 배경 스프라이트 텍스처를 수동으로 연결해야 정상 작동.**
  - Blend 모드가 `SrcAlpha One` → `One One`(시도) → 최종적으로 `SrcAlpha OneMinusSrcAlpha`로 여러 번 바뀜. 지금은 일반 알파 블렌드.
- `Assets/01_Arts/Shader/SpriteOutline.shader` — 신규. 캐릭터 스프라이트 윤곽선용. `Blend One OneMinusSrcAlpha`(프리멀티플라이드 알파), 유클리드 거리 기반 팽창(dilation)으로 윤곽선 계산, 안티앨리어싱 가장자리 포함.
  - `Assets/01_Arts/Material/SpriteOutline.mat` — 위 셰이더용 머티리얼. **사용자가 `_OutlineThickness: 3.82`, `_OutlineColor`를 보라색 계열로 이미 조정해둠(내가 만든 게 아니라 사용자가 에디터에서 만짐).**
  - **아직 아무 SpriteRenderer에도 연결 안 됨** — player에 붙일지 말지는 사용자 판단 대기 중.

### 코드
- `Assets/02_Scripts/Combat/EffectManager.cs` — 신규. 피격 이펙트 등 여러 종류의 VFX를 `EffectType` enum + 인스펙터 등록 리스트로 관리하는 매니저(싱글턴 아님, `StatusEffectManager` 스타일 참고해서 만듦).
  - `PlayAtRandomPosition(EffectType, SpriteRenderer)` — 스프라이트 bounds 내 랜덤 위치 재생
  - `PlayAt(EffectType, Vector3)` — 특정 좌표 재생
  - **씬에 아직 배치 안 됨.** `02_SYSTEM`에 오브젝트 만들고 `Effects` 리스트에 `Hit` 타입 프리팹 등록 + `Deck Manager`/`EnemyManager` 인스턴스의 새 필드에 연결 필요.
- `Assets/02_Scripts/DeckManager/DeckManager.cs` — `effectManager` 필드 추가, `PlayPendingActions` 루프 안(펀치 1회당 1회, `Damage > 0`일 때만)에서 `EffectManager.EffectType.Hit` 재생 호출 추가.
- `Assets/02_Scripts/Enemy/EnemyManager.cs` — `effectManager` 필드(public, 기존 파일 스타일 따름) 추가, `ExecuteEnemyTurn`의 Attack 케이스에서 플레이어 피격 시 1회 재생 호출 추가.

### 문서
- `CLAUDE.md`에 "Known gap — Hangul cards can fail to match" 항목 추가 (`CardInputHandler`가 `Composition`을 안 보고 `CurrentInput`만 봐서, 한글 단어 마지막 음절이 커밋될 계기가 없으면 영원히 매칭 안 될 수 있다는 내용). **아직 코드는 안 고침, 문서만 남김.**

⚠️ **CLAUDE.md의 SpeechBubble 관련 서술이 지금 실제 코드와 어긋나기 시작했다** — 아래 2번 항목(사용자가 에디터에서 직접 바꾼 것) 때문. `isNormalTail`/`ThinkBubbleTailx2` 관련 문서 내용이 이제 stale함. 머지 후 CLAUDE.md 갱신 필요.

## 2. 이 대화 밖에서(Unity 에디터로 직접) 건드린 것 — git diff로 감지됨

대화 세션 도중 Unity 에디터에서 직접 작업한 것으로 보이는 변경들. 내가 만든 게 아니라 감지만 한 것이라 의도는 파악 못 함, 참고용으로만 정리.

- **말풍선 시스템 단순화**: `SpeechBubble.cs`, `SpeechBubbleManager.cs`, `BattleManager.cs`, `Combat/StatusEffectManager.cs`, `EventManager.cs`
  - `SpeechBubble.Setup(string, bool isPlayer, bool isNormalTail)` → `Setup(string)`으로 시그니처 단순화. `thinkTailSprite`/`playerTailPos`/`enemyTailPos` 필드 제거.
  - 꼬리가 항상 **우측 고정**(`anchorMin/Max = (1,0)`, `pivot = (1,0)`)으로 바뀜 — 예전엔 플레이어/적 좌우 반전 로직이 있었는데 지금은 없음.
  - `SpeechBubbleManager.ShowBubble(...)`도 `isNormalTail` 매개변수 제거.
- **`EnemyBase.cs`**: `bubbleAnchor` (Transform, SerializeField) + `BubblePosition` (프로퍼티, 비어있으면 `transform.position` 폴백) 추가. 말풍선 위치 계산이 이제 `enemy.transform.position` 대신 `enemy.BubblePosition`을 씀 (BattleManager, StatusEffectManager에서 호출부 갱신됨).
- **`enemy.prefab`**: 자식 `Pos` 오브젝트 추가(`localPosition: -1.3, 1.84, 0`) — `bubbleAnchor`가 이걸 가리킴. `animator` 필드도 `{fileID: 0}`(비어있음)에서 실제 Animator 컴포넌트로 연결됨.
- **`SpeechBubble.prefab`**: 위 스크립트 변경에 맞춰 인스턴스 오버라이드 값 조정(23줄 diff).
- **`Assets/01_Arts/UI/SpeechBubbleBody.png.meta`**: 텍스처 임포트 설정 변경(12줄 diff, 상세 미확인 — Sprite 관련 설정으로 추정).
- **폴더 이름 오타 수정**: `Assets/03_Prefab/UI/UIStyle/**` (단수, 옛 이름) → `Assets/03_Prefabs/UI/UIStyle/**` (복수)로 이동. `SimpleGradient.cs`, `SimpleGradientEditor.cs`, `UIBlurRendererFeature.cs`, `UIStyle.cs`, `UIStyleEditor.cs`, `UIStylePreset.cs` 전부 포함. git 입장에서는 옛 경로 전부 삭제 + 새 경로 전부 신규 추가로 보임(실제로는 이름만 바뀐 폴더 이동).

## 3. 머지할 때 주의할 것

- `EffectManager.cs`, `SpriteOutline.shader`(+`.mat`), `UnderwaterGodRaySprite.shader` 관련 `.meta` 파일들은 전부 **untracked 신규 파일**이라 `git add` 안 하면 커밋에 안 들어감.
- `03_Prefab` → `03_Prefabs` 폴더 이동은 git이 rename으로 인식 못 하고 삭제+추가로 볼 가능성이 높음 — 다른 브랜치에서 `03_Prefab/UI/UIStyle/*`를 건드렸다면 충돌보다는 "우리 쪽엔 파일이 없다"는 식으로 조용히 어긋날 수 있으니 머지 후 그 폴더가 실제로 존재하는지 직접 확인할 것.
- `CardInputHandler`, `WordChainManager` 등 손패/매칭 관련 파일은 이번 세션에서 안 건드림 — 다른 브랜치 변경과 충돌 소지 적음.
