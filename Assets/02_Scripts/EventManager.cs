using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class EventManager : MonoBehaviour
{
    [Header("References")]
    public BattleManager battleManager;
    public CharacterStats player;

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

    private GameObject dialogueBubbleObj;
    private SpeechBubble dialogueBubbleScript;

    private int currentLineIndex = 0;
    private List<string> activeDialogueLines = new List<string>();

    private bool isEventActive = false;
    public bool IsEventActive => isEventActive;

    private int pendingHealAmount = 0;
    private bool wasMotherDragon = false;

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

        string[] linesToUse = isMotherDragon ? dragonEventLines : normalEventLines;

        foreach (string line in linesToUse)
        {
            activeDialogueLines.Add(string.Format(line, healAmount));
        }

        if (motherDragonVisual != null) motherDragonVisual.SetActive(true);
        if (dialogueBubbleObj != null) dialogueBubbleObj.SetActive(true);

        ShowNextDialogue();
    }

    void Update()
    {
        if (!isEventActive || Keyboard.current == null) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ShowNextDialogue();
        }

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
                dialogueBubbleScript.Setup(activeDialogueLines[currentLineIndex], false);
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
            // 안내 문구("다음을 입력하세요")는 ShowResult가 ResultInputHandler에서 받아 붙인다.
            battleManager.ShowResult("VICTORY!");
        }
    }
}