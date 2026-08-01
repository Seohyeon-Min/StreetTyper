using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 타이틀 씬의 버튼 배선. 게임 상태를 들고 있지 않은 순수 진입점이다.
public class TitleMenu : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button startButton;

    [SerializeField] private Button optionsButton;

    [SerializeField] private Button quitButton;

    [Header("옵션")]
    [Tooltip("옵션 창 루트. 평소엔 비활성이어야 한다.")]
    [SerializeField] private GameObject optionsPanel;

    private void Awake()
    {
        // 일시정지 상태에서 "타이틀로"를 눌러 돌아온 경우 timeScale이 0인 채로 남아 있다.
        // 씬을 넘어가도 리셋되지 않는 전역 값이라 진입할 때마다 되돌린다.
        Time.timeScale = 1f;

        if (optionsPanel != null)
            optionsPanel.SetActive(false);
        else
            Debug.LogWarning("TitleMenu: optionsPanel이 연결되지 않아 옵션 창을 열 수 없습니다.", this);
    }

    private void OnEnable()
    {
        if (startButton != null)
            startButton.onClick.AddListener(HandleStart);
        else
            Debug.LogWarning("TitleMenu: startButton이 연결되지 않아 게임을 시작할 수 없습니다.", this);

        if (optionsButton != null)
            optionsButton.onClick.AddListener(HandleOptions);
        else
            Debug.LogWarning("TitleMenu: optionsButton이 연결되지 않았습니다.", this);

        if (quitButton != null)
            quitButton.onClick.AddListener(HandleQuit);
        else
            Debug.LogWarning("TitleMenu: quitButton이 연결되지 않았습니다.", this);
    }

    private void OnDisable()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(HandleStart);

        if (optionsButton != null)
            optionsButton.onClick.RemoveListener(HandleOptions);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(HandleQuit);
    }

    private void HandleStart()
    {
        SceneManager.LoadScene(GameScenes.Battle);
    }

    // 닫기는 OptionsPanel이 자기 닫기 버튼으로 직접 처리한다.
    private void HandleOptions()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(true);
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
