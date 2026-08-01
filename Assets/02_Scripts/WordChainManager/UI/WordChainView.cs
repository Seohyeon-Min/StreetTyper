using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class WordChainView : MonoBehaviour
{
    [SerializeField] private WordChainManager wordChainManager;

    [Tooltip("조합이 완성된 뒤 그 문장을 화면에 남겨두는 시간(초). 0이면 예전처럼 곧바로 사라진다. " +
             "DeckManager가 완성 직후 ClearChain을 부르기 때문에, 이게 없으면 완성된 조합이 " +
             "같은 프레임에 지워져 플레이어가 무엇을 만들었는지 볼 수 없다.")]
    [SerializeField] private float completedHoldDuration = 0.5f;

    private TMP_Text _text;
    private Coroutine _holdCoroutine;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        wordChainManager.OnWordAdded += HandleWordAdded;
        wordChainManager.OnWordRemoved += HandleWordRemoved;
        wordChainManager.OnChainCleared += HandleChainCleared;
        wordChainManager.OnChainCompleted += HandleChainCompleted;
        Refresh();
    }

    private void OnDisable()
    {
        wordChainManager.OnWordAdded -= HandleWordAdded;
        wordChainManager.OnWordRemoved -= HandleWordRemoved;
        wordChainManager.OnChainCleared -= HandleChainCleared;
        wordChainManager.OnChainCompleted -= HandleChainCompleted;

        // 오브젝트가 꺼지면 코루틴도 멈추므로 표시 상태만 정리해 둔다.
        _holdCoroutine = null;
    }

    // 새 단어가 들어왔다는 건 다음 조합이 시작됐다는 뜻이다. 붙잡아 두던 문장을 놓고 새로 그린다.
    private void HandleWordAdded(WordInstance _)
    {
        StopHold();
        Refresh();
    }

    private void HandleWordRemoved()
    {
        StopHold();
        Refresh();
    }

    // 완성 직후의 비움은 무시한다 - 지금 화면에 떠 있는 게 방금 완성한 조합이고,
    // 그걸 보여주려고 붙잡아 두는 중이다.
    private void HandleChainCleared()
    {
        if (_holdCoroutine != null)
            return;

        Refresh();
    }

    private void HandleChainCompleted(IReadOnlyList<WordInstance> _)
    {
        Refresh();

        if (completedHoldDuration <= 0f)
            return;

        StopHold();
        _holdCoroutine = StartCoroutine(HoldThenRefresh());
    }

    // 일시정지(timeScale = 0) 중에는 같이 멈춰야 하므로 WaitForSeconds를 쓴다.
    private IEnumerator HoldThenRefresh()
    {
        yield return new WaitForSeconds(completedHoldDuration);

        _holdCoroutine = null;
        Refresh();
    }

    private void StopHold()
    {
        if (_holdCoroutine == null)
            return;

        StopCoroutine(_holdCoroutine);
        _holdCoroutine = null;
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
