using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class WordChainView : MonoBehaviour
{
    [SerializeField] private WordChainManager wordChainManager;

    [Tooltip("조합이 완성된 뒤 그 문장을 화면에 남겨두는 시간(초). 0이면 곧바로 사라진다. " +
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
        Refresh();
    }

    private void OnDisable()
    {
        wordChainManager.OnWordAdded -= HandleWordAdded;
        wordChainManager.OnWordRemoved -= HandleWordRemoved;
        wordChainManager.OnChainCleared -= HandleChainCleared;

        // 오브젝트가 꺼지면 코루틴도 멈추므로 표시 상태만 정리해 둔다.
        _holdCoroutine = null;
    }

    /// <summary>
    /// 액션 단어가 들어온 순간이 곧 조합 완성이다(WordChainManager.SubmitWord 참조).
    ///
    /// ⚠️ 완성 감지를 OnChainCompleted가 아니라 여기서 하는 이유가 있다. DeckManager도
    /// OnChainCompleted를 구독하고 그 핸들러 안에서 ClearChain을 부르는데, 구독 순서가 보장되지
    /// 않아 DeckManager가 먼저 돌면 우리가 붙잡기도 전에 OnChainCleared가 도착해 화면이 비워진다.
    /// OnWordAdded는 OnChainCompleted보다 반드시 먼저 발생하므로, 여기서 걸면 순서와 무관하게 안전하다.
    ///
    /// (덧붙여 OnChainCompleted는 _chain 리스트를 그대로 넘기고 ClearChain은 그 인스턴스를 비우므로,
    ///  파라미터로 받은 체인을 그리는 방법도 통하지 않는다.)
    /// </summary>
    private void HandleWordAdded(WordInstance word)
    {
        // 새 단어가 들어왔다는 건 다음 조합이 시작됐다는 뜻이다. 붙잡아 두던 문장을 놓는다.
        StopHold();
        Refresh();

        if (word == null || word.Category != CardCategory.Action || completedHoldDuration <= 0f)
            return;

        // 지금 화면에 그려진 게 방금 완성된 조합이다. 뒤따라올 ClearChain을 무시하고 잠시 붙잡는다.
        _holdCoroutine = StartCoroutine(HoldThenRefresh());
    }

    private void HandleWordRemoved()
    {
        StopHold();
        Refresh();
    }

    // 완성 직후의 비움은 무시한다 - 지금 떠 있는 게 방금 완성한 조합이고, 그걸 보여주는 중이다.
    private void HandleChainCleared()
    {
        if (_holdCoroutine != null)
            return;

        Refresh();
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
