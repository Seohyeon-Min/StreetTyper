# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

**스트리트타이퍼 (Street Typer)** is a Unity project (Editor **6000.3.19f1**, Universal Render Pipeline, 2D template) — a turn-based "typing action text RPG" prototype (10-day prototype scope per the design doc). Core loop: on the player's turn, 5 word "cards" are shown on screen; the player types them back-to-back with **no spaces or Enter needed** to build a combo before a countdown timer runs out.

**The game is typed in Korean.** Word data, matching, and the input pipeline are all Hangul-based — see the input section below; this is load-bearing, not a localization detail.

Words fall into 3 categories, mapping 1:1 onto the card data hierarchy:

- **수식어 (Modifier)** — stat amplifiers (슈퍼 +1, 울트라 +2, 파워 amplifies the next modifier, 어썸 scales with success count, 퀵 adds timer time).
- **속성/특수효과 (Attribute)** — status effects and combat modifiers (burn/paralysis/freeze chance, life drain, damage reduction, crit, repeat-action).
- **액션 (Action)** — attack/defense words that resolve a combo (펀치/잽/훅/어퍼컷/페인트/뎀프시롤/가드), each with a power bonus, timer delta, and special rules (ignores/breaks defense, combo re-trigger chance).

## Working with this repository

Unity project, not an npm/CLI project — there are no build/lint/test scripts to invoke from a terminal.

- **Editor**: open the project root in Unity Hub / Unity Editor `6000.3.19f1` (must match `ProjectSettings/ProjectVersion.txt`).
- **Compiling**: Unity compiles C# automatically on focus/save. No `dotnet build` step — `Assembly-CSharp.csproj`/`StreetTyper.sln` are Unity-generated and gitignored; never hand-edit them. Single implicit `Assembly-CSharp` assembly, no `.asmdef` split.
- **Tests**: `com.unity.test-framework` is installed but unused — no test assemblies exist. "Testing" here means manual Play Mode verification, usually by reading `Debug.Log` output (see `DeckManager.logDebugEvents`).
- **Running**: press Play in the Editor. No headless/CLI run path.
- C# `LangVersion` 9.0, .NET Standard 2.1 (Mono) — avoid newer C# syntax the compiler will reject.

## Project structure

Top-level asset folders use an `NN_Name` prefix to control asset-browser ordering; follow that convention for new ones.

- `Assets/00_Scenes/SampleScene.unity` — the only scene. Root convention: `00_BOOT`, `01_CAMERA`, `02_SYSTEM` (`InputManager`, `Deck Manager`), `03_WORLD`, `04_UI` (`Canvas` → `InputField (TMP)`, `Hand`), `05_DEBUG`, `EventSystem`.
- `Assets/01_Arts/Fonts/` — `Paperlogy-4Regular SDF.asset`, a TMP font asset that **includes Hangul glyphs**. Every `TMP_Text`/`TMP_InputField` showing Korean must use it; the default `LiberationSans SDF` is Latin-only and silently renders Korean as `□` with one console warning per character.
- `Assets/02_Scripts/` — see Architecture.
- `Assets/03_Prefabs/Card.prefab` — the runtime-instantiated card (see Hand presentation below).
- `Assets/04_Data/Cards/` — 8 `CardBase` test assets (파이어, 인텔리, 파워, 퀵, 슈퍼, 어썸, 펀치, 어퍼컷). Create these via `Assets > Create > Deck Manager > Cards > ...`, never by hand-authoring `.asset` YAML — hand-written assets risk silently mismatched script GUIDs. All 8 currently have an empty `icon` and `description`.
- `Assets/04_Data/EnemyTutorial.asset` — the only `EnemyData`.
- `Assets/TextMesh Pro/` — imported TMP Essentials resources.
- `Assets/InputSystem_Actions.inputactions` — the Input System's default template asset. **Unused**; gameplay input deliberately does not go through it (see below). Don't wire gameplay to it.

## Architecture

### Input pipeline (`02_Scripts/InputManager/`)

Typing input bypasses Unity's Input Action asset/binding system entirely and polls `UnityEngine.InputSystem.Keyboard.current` directly.

- **`InputManager`** exposes `CurrentInput` (committed characters since the last clear), `Composition` (in-progress IME text), and events `OnCharacterEntered(char)`, `OnBackspace`, `OnSubmit` (Enter/NumpadEnter), `OnCompositionChanged`, `OnInputCleared`. Control via `EnableInput()`/`DisableInput()`/`ClearInput()`; auto-enables in `Start()`.
  - **Only Hangul is accepted.** `HandleTextInput` early-returns unless `IsHangul(character)` — Hangul Syllables (가-힣) or Compatibility Jamo (ㄱ-ㅣ). ASCII/English keystrokes are silently dropped and never reach `CurrentInput` or `OnCharacterEntered`. An earlier `IsAlphabet` branch was removed; do not assume English works anywhere in the typing path.
  - Characters come from `Keyboard.onTextInput` (not raw key polling), which is what yields correctly composed Hangul syllables.
  - IME composition is handled via `onIMECompositionChange` + `SetIMEEnabled(true)`. Backspace is deliberately suppressed while `Composition` is non-empty, so it edits the IME's own buffer instead of double-deleting committed characters. Backspace has hold-to-repeat (`backspaceRepeatDelay`/`backspaceRepeatInterval`).
  - `ChangeHangul()` toggles `Input.imeCompositionMode` between `Auto` and `On` on **RightAlt** — a workaround for the IME issue below, not a general keybind.
  - `OnSubmit` currently has **no subscribers** — a free hook if a submit-based flow is ever needed. Note it does not force the IME to commit its composition.
- **`InputFieldDisplay`** is a pure view: mirrors `CurrentInput + Composition` into a **read-only** `TMP_InputField` (`readOnly = true` — it must never take its own keyboard/IME focus, since `InputManager` owns the OS-level IME context; making it interactive causes double input). It also calls `Keyboard.current.SetIMECursorPosition(...)`, computed from the TMP text's last-character screen position, so the OS IME overlay draws in the right place.

### Deck / card system (`02_Scripts/Deck Manager/`)

- **`Cards/CardBase.cs`** — abstract `ScriptableObject`: `CardName` (the exact word to type *and* the display text *and* the match key), `Icon`, `Description`, abstract `Category`; plus `enum CardCategory { Modifier, Attribute, Action }`. Three concrete subclasses (`ModifierCardData`, `AttributeCardData`, `ActionCardData`) each model a whole category with an effect-type enum + a few numeric fields, rather than one class per word. **Data only — no damage/timer/status execution exists**, deliberately deferred until a combat system consumes it.
- **`CardSlotManager`** — owns the 5 slots (`CurrentCards`, `SlotCount`, `OnSlotChanged`, `ConsumeSlot(i)`), filling them from a serialized `availableCards` pool uniformly at random. **Duplicates across slots are intentional** — a no-duplicate variant was built and reverted on request; don't "fix" it without checking first.
- **`CardInputHandler`** — the matching logic. Subscribes only to `InputManager.OnCharacterEntered` (per keystroke; `OnSubmit`/Enter is intentionally not used for matching). Each keystroke: exact match against a current card → consume (buffer + refill + clear input); still a prefix of some card → wait; prefix of nothing → typo → clear buffer + clear input.
- **`MainBufferManager`** — ordered list of cards matched so far (`AddCard`/`ClearBuffer`, `OnCardAdded`/`OnBufferCleared`). Appends every match regardless of category; it does **not** auto-flush or execute on an Action card.
- **`DeckManager`** — thin read-only facade (`Slots`/`Input`/`Buffer`) plus a `logDebugEvents` flag that logs match/typo/buffer events. That log is currently the only way to observe buffer state, since no combat/HUD UI exists.

These four sit as sibling components on one `Deck Manager` GameObject under `02_SYSTEM`.

### Hand presentation (`Deck Manager/HandFanLayout.cs`, `Deck Manager/UI/CardSlotView.cs`)

- **`HandFanLayout`** (on `04_UI/Canvas/Hand`) instantiates `Card.prefab` once in `Start()`, `cardSlotManager.SlotCount` times, calling `CardSlotView.Bind(manager, i)` on each, then arranges all active `RectTransform` children into a fan in `LateUpdate` (spacing / arc height / tilt / smoothing, optional center-on-top draw ordering). It is `[ExecuteAlways]`, so spawning is guarded by `Application.isPlaying` — otherwise Play Mode cards would accumulate in the saved scene.
- **`CardSlotView`** displays one slot's `CardName`/`Icon`. Because it is instantiated at runtime, its `OnEnable` fires *before* `Bind()`, so subscription is null-tolerant and idempotent (`Subscribe`/`Unsubscribe`/`_subscribed`) rather than a bare `+=` in `OnEnable`. It hides `iconImage` when the card has no sprite — all test cards currently lack sprites, so cards render as text only until art is added.
- There are **no `CardSlotView` instances saved in the scene**; every card is spawned. Don't add static card children under `Hand`.

### Combat scaffolding (`02_Scripts/BattleManager.cs`, `Character/`, `Enemy/`)

Exists as code but is **not present in `SampleScene`** — no GameObject carries `BattleManager`, `CharacterStats`, `EnemyManager`, or `EnemyBase`, and nothing connects them to the deck system. Treat it as an unwired sketch, not a working system.

- `CharacterStats` — public `maxHP`/`currentHP`/`power`/`defense`, `TakeDamage` (defense absorbs first), `AddDefense`, `IncreasePower`, private `Die()` → `Destroy(gameObject)` (which is why callers null-check every frame).
- `EnemyBase : CharacterStats` copies stats from an `EnemyData` SO in `Start()`. `EnemyManager` rolls a weighted intent (`ActionType { Attack, Defend, Buff }`), exposes `GetIntentString()`, and resolves it in `ExecuteEnemyTurn(player)`.
- `BattleManager` is a debug harness: digit1 = damage enemy, digit2 = player defends, digit3 = enemy turn. All fields are `public` and it drives `TextMeshProUGUI` directly — it predates the conventions below and is not a style reference.

Note there are already two unrelated action enums: `ActionKind { Attack, Defense }` (cards) and `EnemyManager.ActionType { Attack, Defend, Buff }` (enemy AI).

### Not implemented yet

No countdown timer, damage math, status effects, crit, life drain, or combo resolution exists. `ActionCardData.TimerDelta` and `ModifierEffectType.TimerBonus` are dead data. `02_Scripts/WordChainManager/WordChainManager.cs` is an untouched Unity template stub — planned, not started, referenced by nothing.

## Conventions

- No C# namespaces — everything is in the global namespace.
- No singletons / service locators / DI: every cross-component dependency is a `[SerializeField]` reference wired by hand in the Inspector. When a dependency can only be known at runtime, add an explicit `Bind(...)` method (see `CardSlotView`) rather than a lookup.
- Logic vs. view separation: "manager" scripts own state and decisions; "view" scripts (`InputFieldDisplay`, `CardSlotView`) only mirror that state onto UI and never make gameplay decisions.
- Event-driven wiring with plain C# `event Action`/`event Action<T>`, subscribed in `OnEnable` and unsubscribed in `OnDisable`.
- Newer scripts use `[SerializeField] private` fields with `[Header]`/`[Tooltip]`, expression-bodied read-only properties, and Korean tooltips/comments. Match the file you're editing.
- When a required Inspector reference is missing, log a `Debug.LogWarning(..., this)` naming the field instead of returning silently — a silent `return` is very hard to diagnose in the Editor.
- Some names reflect deliberate choices (the `Deck Manager` folder has a space) — not typos to fix. Past real typos (`InputManger` → `InputManager`, `DeckManger.cs` → `DeckManager.cs`) were corrected once; don't reintroduce drift or rename further without being asked.

## Known issues

- **한/영 IME toggle**: the OS Hangul/English toggle key sometimes stops working while the Unity window has focus (it works fine in other apps on the same machine, including Unity's own Inspector fields). It is **not Editor-only** — it reproduces in standalone builds too, confirmed via `Player.log`, so it is not a Play Mode quirk. A native workaround (`GetAsyncKeyState` on `VK_HANGUL` + `ImmSimulateHotKey` via P/Invoke) was tried, had no effect, and was reverted. `ChangeHangul()` (RightAlt) is the current stopgap. Root cause unresolved — don't blindly re-attempt the P/Invoke fix; Unity's Input System raw-input handling needs deeper investigation first.
- **Last Hangul syllable may never match**: `OnCharacterEntered`/`CurrentInput` only see IME-*committed* characters; in-progress jamo live in `Composition`, which `CardInputHandler` never reads. Every syllable but the last commits when the next one starts composing — but the word's **final syllable has no following keystroke to force a commit**, so it can sit in `Composition` indefinitely and the card silently never matches, no matter how correctly it was typed. Not fixed. A fix means matching against `CurrentInput + Composition`, which changes core matching behavior — confirm the approach before doing it.
