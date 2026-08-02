# 머지 노트 — 일시정지 메뉴 개편 (SeohyeonMin)

`e9549dc`(마지막 main 머지) 이후 커밋 3개(`1376348` → `15e2a19` → `ed752ae`)에서 일시정지(ESC) 메뉴를
버튼 없는 타이핑 UI로 새로 짰다. 다른 브랜치가 `SampleScene.unity` / `Pause Panel.prefab`을
같이 건드리고 있다면 이 문서를 먼저 읽을 것.

## 무엇이 바뀌었나

**기존**: 일시정지 창에 안내 문구만 있고 조작은 버튼 또는 별도 UI였다(정확한 옛 구조는 커밋 로그 참조).

**지금**: ESC를 누르면—
1. 평소 손패 5장(Card Canvas)이 화면 아래로 가라앉듯 사라진다.
2. 그 자리에 `PauseHand`(`Pause Panel` 프리팹 안의 별도 `HandFanLayout`)가 "계속"/"타이틀" 카드
   2장을 새로 만들어 떠오르듯 보여준다. **손패와 똑같은 부채꼴 배치 + 타이핑 들림 애니메이션을
   그대로 물려받는다** — 실제 카드가 아니라 `CardSlotView.BindStatic(label, ...)`로 텍스트만
   채운 가짜 카드다.
3. "PAUSE" 제목이 가운데서부터 좌우로 열리며 나타난다(`TextGateRevealAnimation`, RectMask2D 기반).
4. 명령 단어를 입력하면(`계속`/`resume`, `타이틀`/`title`) 반대 방향으로 되돌아간다 — 명령
   카드 2장은 파괴되고, 저장해뒀던 손패 5장이 원래 슬롯으로 복원되며 다시 떠오른다.

## 새/변경 파일

- **`Assets/02_Scripts/UI/PauseManager.cs`** — 카드 전환 연출(`ShowCommandCards`/`HideCommandCards`)과
  `TextGateRevealAnimation` 재생 호출이 추가됐다.
- **`Assets/02_Scripts/UI/TextGateRevealAnimation.cs`** (신규) — "PAUSE"를 감싼 `RectMask2D`의
  `sizeDelta.x`를 0→`fullWidth`로 키워서 가운데부터 열리는 것처럼 보이게 하고, 좌우 막대(`leftBar`/
  `rightBar`)가 마스크 끝을 따라가다 다 열리면 바깥으로 밀려나며 페이드아웃한다. 셰이더가 아니다.
- **`Assets/02_Scripts/DeckManager/UI/CardSlotView.cs`** — 재사용 가능한 `PlayExit`/`PlayEnter`
  공개 API 추가(기존 `HandleSlotChanged`/`PlaySwap` 게임플레이 로직은 그대로 유지). `BindStatic(label,
  input)`으로 `CardBase` 데이터 없이 텍스트만 있는 카드를 만들 수 있다. 애니메이션 시간축을
  `Time.deltaTime`에서 `Time.unscaledDeltaTime`으로 바꿨다 — 아래 "일시정지 중에도 도는 이유" 참조.
- **`Assets/02_Scripts/DeckManager/UI/CardView.cs`** — `SetText(string)` 추가. 명령 단어처럼
  `CardBase`가 없는 텍스트를 카드 모양으로 그릴 때 쓴다.
- **`Assets/02_Scripts/DeckManager/HandFanLayout.cs`** — 위치 보간도 `unscaledDeltaTime`으로 전환.
- **`Assets/03_Prefabs/Pause Panel.prefab`** — `PauseHand`(명령 카드용 HandFanLayout), `TextMask`/
  `Window (1)`/`Window (2)`(PAUSE 열림 연출의 마스크·막대), `TextGateRevealAnimation` 컴포넌트 추가.

## 알아둘 것 (다시 겪을 수 있는 함정들)

- **`Time.timeScale = 0`인데 왜 카드가 움직이나** — 일시정지 중에도 카드 전환 애니메이션은 재생돼야
  하므로 `CardSlotView`/`HandFanLayout`의 관련 보간을 `Time.unscaledDeltaTime`으로 바꿨다. 평소
  게임플레이(timeScale == 1)에는 `deltaTime`과 값이 같아 손패 스왑 애니메이션은 영향 없다.
- **`HandFanLayout.Cards`는 "활성 자식만" 매 프레임 다시 모은다** — 카드를 `SetActive(false)`로
  끄고 나면 그 순간부터 이 리스트에서 빠져 다시는 참조를 못 구한다. `PauseManager`는 끄기 *전에*
  `_pausedHandCards`로 스냅샷을 떠 두고, 복원할 때 그 스냅샷을 쓴다(리스트를 다시 조회하지 않는다).
- **`Pause Panel`과 `PauseHand`의 부모-자식 관계 때문에 난 버그** — `PauseHand`(`commandCardsLayout`)가
  `Pause Panel`(`pausePanel`)의 자식이라, `pausePanel.SetActive(false)`를 먼저 하면 그 안의 명령
  카드도 같이 `activeInHierarchy`가 꺼져서 `StartCoroutine`이 "game object is inactive" 에러를 낸다.
  그래서 `Resume()`은 **`HideCommandCards()`를 먼저 부르고 `pausePanel.SetActive(false)`를 나중에
  부른다.** 같은 이유로 명령 카드 2장은 퇴장 애니메이션 없이 즉시 `Destroy`한다 — 애니메이션을 걸어도
  같은 프레임에 부모가 꺼지며 코루틴이 잘려 `Destroy`가 끝내 호출 안 되고 비활성 오브젝트만 쌓인다.
  **새 하위 메뉴를 이 패턴으로 만들 때 부모/자식 활성화 순서를 반드시 먼저 확인할 것.**
- **`TextGateRevealAnimation.fullWidth`는 마스크의 현재 크기에서 읽지 않는다** — 씬에서 마스크를
  닫힌 상태(폭 0에 가깝게)로 배치해 두는 게 정상인데, `Awake`가 그 폭을 그대로 "목표 폭"으로
  캐시하면 항상 0으로 고정돼버린다. 그래서 `fullWidth`는 인스펙터에 직접 넣는 값이다(현재 700).
- **`ShowCommandCards`도 같은 계열 문제가 있었다** — `PauseHand` 자신이 씬에 비활성 상태로 남아 있을
  수 있어, 새로 스폰한 카드만 `SetActive(true)`해도 부모가 꺼져 있으면 여전히 `activeInHierarchy`가
  false다. `ShowCommandCards()`가 카드를 스폰하기 전에 `commandCardsLayout.gameObject.SetActive(true)`를
  먼저 부른다.

## 에디터에서 확인해야 할 배선

- `PauseManager`의 `Title Reveal` 필드가 `Pause Panel`의 `TextGateRevealAnimation` 컴포넌트를
  가리키는지 확인할 것 — 비어 있으면 콘솔에 경고만 뜨고 PAUSE 열림 연출이 재생되지 않는다.
- `commandCardsLayout`(`PauseHand`)의 `Card Slot Manager` 필드는 **비워둘 것** — 자동 스폰 없이
  `PauseManager`가 직접 2장만 채우는 구조라, 채워두면 `HandFanLayout.SpawnCards()`가 별도로
  카드를 더 만들어버린다.
- IME/빌드 관련 검증은 늘 그렇듯 에디터만으로는 부족하다 — 아직 스탠드얼론 빌드에서 재확인 전이다.
