using UnityEngine;
using System.Collections.Generic;

public class EventManager : MonoBehaviour
{
    [Header("References")]
    public BattleManager battleManager;
    public CharacterStats player;

    [Tooltip("대사를 넘기는 스페이스 입력을 받기 위해 참조한다. 키보드를 직접 읽지 않는다.")]
    public InputManager inputManager;

    [Header("Mother Dragon Setup")]
    public GameObject motherDragonVisual;
    public Transform motherDragonTransform;

    [Header("Speech Bubble Settings")]
    public GameObject speechBubblePrefab;
    public Transform canvasTransform;
    public Vector3 bubbleOffset = new Vector3(0f, 2.5f, 0f);

    [Header("Dialogues (Inspector에서 변경 가능)")]
    public string[] normalEventLines = { "새로운 단어 카드를 획득했다!", "placeholder1", "placeholder2" };
    public string[] dragonEventLines = { "placeholder0", "placeholder1", "placeholder2" };

    // ⚠️ 기본값을 비워 둔다. EventManager.prefab의 한국어 배열이 둘 다 빈 배열이라서,
    // 여기에 대사를 채우면 영어 모드에서만 대사가 생겨 스페이스를 여러 번 눌러야 넘어가게 된다.
    // 그 사이 ShowResult가 불리지 않아 결과 화면도 클리어 보상도 나오지 않는다 - 실제로 겪은 회귀다.
    // 한국어 대사를 채우게 되면 이쪽도 같은 개수로 함께 채울 것.
    [Header("Dialogues - English")]
    public string[] normalEventLinesEn = { };
    public string[] dragonEventLinesEn = { };

    private GameObject dialogueBubbleObj;
    private SpeechBubble dialogueBubbleScript;

    private int currentLineIndex = 0;
    private List<string> activeDialogueLines = new List<string>();

    private bool isEventActive = false;

    private int pendingHealAmount = 0;
    private bool wasMotherDragon = false;

    private void OnEnable()
    {
        if (inputManager != null)
            inputManager.OnAdvance += HandleAdvance;
        else
            Debug.LogWarning("EventManager: inputManager가 연결되지 않았습니다. 스페이스로 대사를 넘길 수 없어 " +
                             "마더 드래곤 이벤트에서 진행이 막힙니다(EndEvent가 불리지 않아 결과 화면도 " +
                             "클리어 보상도 나오지 않습니다). 씬 인스턴스에서 연결하세요.", this);
    }

    private void OnDisable()
    {
        if (inputManager != null)
            inputManager.OnAdvance -= HandleAdvance;
    }

    void Start()
    {
        if (motherDragonVisual != null) motherDragonVisual.SetActive(false);

        if (speechBubblePrefab != null && canvasTransform != null)
        {
            dialogueBubbleObj = Instantiate(speechBubblePrefab, canvasTransform);
            dialogueBubbleScript = dialogueBubbleObj.GetComponent<SpeechBubble>();
            dialogueBubbleObj.SetActive(false);
        }
    }

    public void StartEvent(bool isMotherDragon, int healAmount = 0)
    {
        isEventActive = true;
        currentLineIndex = 0;
        pendingHealAmount = healAmount;
        wasMotherDragon = isMotherDragon;

        activeDialogueLines.Clear();

        // 영문 배열이 비어 있으면 한국어로 넘어간다 - 대사는 타이핑 대상이 아니라
        // 읽고 스페이스로 넘기기만 하므로, 비어 있어도 진행이 막히지는 않는다.
        string[] linesToUse;
        if (LanguageSettings.IsEnglish)
        {
            var en = isMotherDragon ? dragonEventLinesEn : normalEventLinesEn;
            linesToUse = en != null && en.Length > 0 ? en : (isMotherDragon ? dragonEventLines : normalEventLines);
        }
        else
        {
            linesToUse = isMotherDragon ? dragonEventLines : normalEventLines;
        }

        foreach (string line in linesToUse)
        {
            activeDialogueLines.Add(string.Format(line, healAmount));
        }

        if (motherDragonVisual != null) motherDragonVisual.SetActive(true);
        if (dialogueBubbleObj != null) dialogueBubbleObj.SetActive(true);

        ShowNextDialogue();
    }

    // 스페이스는 InputManager가 준다. 대사가 떠 있을 때만 의미가 있다.
    private void HandleAdvance()
    {
        if (isEventActive)
            ShowNextDialogue();
    }

    void Update()
    {
        if (!isEventActive) return;

        if (dialogueBubbleObj != null && motherDragonTransform != null && dialogueBubbleObj.activeSelf)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(motherDragonTransform.position + bubbleOffset);
            dialogueBubbleObj.GetComponent<RectTransform>().position = screenPos;
        }
    }

    private void ShowNextDialogue()
    {
        if (currentLineIndex < activeDialogueLines.Count)
        {
            if (dialogueBubbleScript != null)
            {
                dialogueBubbleScript.Setup(activeDialogueLines[currentLineIndex]);
            }

            if (motherDragonVisual != null)
            {
                Animator anim = motherDragonVisual.GetComponent<Animator>();
                if (anim != null) anim.SetTrigger("Speak");
            }

            currentLineIndex++;
        }
        else
        {
            EndEvent();
        }
    }

    private void EndEvent()
    {
        isEventActive = false;

        if (motherDragonVisual != null) motherDragonVisual.SetActive(false);
        if (dialogueBubbleObj != null) dialogueBubbleObj.SetActive(false);

        if (wasMotherDragon && player != null && pendingHealAmount > 0)
        {
            player.currentHP += pendingHealAmount;
            if (player.currentHP > player.maxHP) player.currentHP = player.maxHP;
        }

        if (battleManager != null)
        {
            battleManager.UpdateUI();
            // 제목("VICTORY!")은 BattleManager의 ScreenPresentation에서, 안내 문구는
            // ResultInputHandler에서 나온다 - 둘 다 인스펙터에서 바꿀 수 있다.
            battleManager.ShowResult(ResultKind.Victory);
        }
    }
}