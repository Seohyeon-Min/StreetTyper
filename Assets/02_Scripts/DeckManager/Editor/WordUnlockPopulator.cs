using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 씬의 WordUnlockManager에 04_Data/Cards의 모든 카드를 채우고 시작 단어에 체크해주는 도구.
// 24줄을 손으로 드래그하다 빠뜨리거나 중복시키는 사고를 막기 위한 것이라, 다 채우고 나면 지워도 된다.
public static class WordUnlockPopulator
{
    private const string CardFolder = "Assets/04_Data/Cards";

    // 기획상 게임 시작 시 지급되는 단어들. 여기 없는 단어는 grantedAtStart가 꺼진 채로 들어간다.
    private static readonly HashSet<string> StartingWordNames = new HashSet<string>
    {
        "슈퍼", "울트라", "파이어", "일렉트릭", "아이스", "더블", "펀치", "잽", "훅"
    };

    [MenuItem("Tools/Deck Manager/Populate Word Unlock Manager")]
    private static void Populate()
    {
        var manager = Object.FindFirstObjectByType<WordUnlockManager>();
        if (manager == null)
        {
            Debug.LogError("WordUnlockPopulator: 씬에서 WordUnlockManager를 찾지 못했습니다. " +
                           "먼저 GameObject를 만들고 WordUnlockManager 컴포넌트를 붙이세요.");
            return;
        }

        var serialized = new SerializedObject(manager);
        var entries = serialized.FindProperty("allWords");

        // 이미 들어있는 카드는 건너뛴다 - 여러 번 눌러도 안전하고, 카드를 새로 만든 뒤 다시 눌러 채울 수 있다.
        var existing = new HashSet<Object>();
        for (var i = 0; i < entries.arraySize; i++)
        {
            var card = entries.GetArrayElementAtIndex(i).FindPropertyRelative("card").objectReferenceValue;
            if (card != null)
                existing.Add(card);
        }

        var guids = AssetDatabase.FindAssets("t:CardBase", new[] { CardFolder });
        var added = 0;
        var startingMarked = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var card = AssetDatabase.LoadAssetAtPath<CardBase>(path);
            if (card == null || existing.Contains(card))
                continue;

            entries.arraySize++;
            var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("card").objectReferenceValue = card;

            var isStarting = StartingWordNames.Contains(card.CardName);
            entry.FindPropertyRelative("grantedAtStart").boolValue = isStarting;

            added++;
            if (isStarting)
                startingMarked++;
        }

        // SerializedObject를 거치면 Undo가 자동으로 기록된다. 씬 컴포넌트라 에셋과 달리
        // 씬을 dirty로 표시해야 저장 대상이 된다.
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        Debug.Log($"WordUnlockPopulator: {added}개 추가 (시작 단어 {startingMarked}개 체크). " +
                  $"전체 {entries.arraySize}개. 씬을 저장하세요(Ctrl+S).", manager);
    }
}
