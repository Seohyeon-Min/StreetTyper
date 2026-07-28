using System;
using System.Collections.Generic;
using UnityEngine;

public class MainBufferManager : MonoBehaviour
{
    private readonly List<CardBase> _buffer = new List<CardBase>();

    public IReadOnlyList<CardBase> Buffer => _buffer;

    public event Action<CardBase> OnCardAdded;
    public event Action OnBufferCleared;

    public void AddCard(CardBase card)
    {
        _buffer.Add(card);
        OnCardAdded?.Invoke(card);
    }

    public void ClearBuffer()
    {
        _buffer.Clear();
        OnBufferCleared?.Invoke();
    }
}
