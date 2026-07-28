using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class WordChainView : MonoBehaviour
{
    [SerializeField] private WordChainManager wordChainManager;

    private TMP_Text _text;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        wordChainManager.OnWordAdded += HandleWordAdded;
        wordChainManager.OnWordRemoved += Refresh;
        wordChainManager.OnChainCleared += Refresh;
        wordChainManager.OnChainCompleted += HandleChainCompleted;
        Refresh();
    }

    private void OnDisable()
    {
        wordChainManager.OnWordAdded -= HandleWordAdded;
        wordChainManager.OnWordRemoved -= Refresh;
        wordChainManager.OnChainCleared -= Refresh;
        wordChainManager.OnChainCompleted -= HandleChainCompleted;
    }

    private void HandleWordAdded(WordInstance _)
    {
        Refresh();
    }

    private void HandleChainCompleted(IReadOnlyList<WordInstance> _)
    {
        Refresh();
    }

    private void Refresh()
    {
        var chain = wordChainManager.CurrentChain;
        var sb = new StringBuilder();

        for (var i = 0; i < chain.Count; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(chain[i].WordName);
        }

        _text.text = sb.ToString();
    }
}
