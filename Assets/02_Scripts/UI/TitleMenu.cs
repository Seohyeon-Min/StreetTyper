using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 타이틀 씬의 버튼 배선. 게임 상태를 들고 있지 않은 순수 진입점이다.
public class TitleMenu : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button startButton;

    [Tooltip("옵션 화면은 아직 없다. 지금은 비활성으로만 자리를 잡아둔다.")]
    [SerializeField] private Button optionsButton;

    [SerializeField] private Button quitButton;

    private void Awake()
    {
        // 일시정지 상태에서 "타이틀로"를 눌러 돌아온 경우 timeScale이 0인 채로 남아 있다.
        // 씬을 넘어가도 리셋되지 않는 전역 값이라 진입할 때마다 되돌린다.
        Time.timeScale = 1f;

        // 인스펙터 설정에만 기대지 않고 코드로도 잠가둔다.
        // 옵션 화면을 실제로 붙일 때 이 줄을 지울 것.
        if (optionsButton != null)
            optionsButton.interactable = false;
    }

    private void OnEnable()
    {
        if (startButton != null)
            startButton.onClick.AddListener(HandleStart);
        else
            Debug.LogWarning("TitleMenu: startButton이 연결되지 않아 게임을 시작할 수 없습니다.", this);

        if (quitButton != null)
            quitButton.onClick.AddListener(HandleQuit);
        else
            Debug.LogWarning("TitleMenu: quitButton이 연결되지 않았습니다.", this);
    }

    private void OnDisable()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(HandleStart);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(HandleQuit);
    }

    private void HandleStart()
    {
        SceneManager.LoadScene(GameScenes.Battle);
    }

    private void HandleQuit()
    {
        // 에디터에서는 Application.Quit()이 아무 일도 하지 않아 눌러도 반응이 없어 보인다.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
