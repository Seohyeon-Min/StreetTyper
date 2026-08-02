using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 타이틀의 옵션 창. 순수 뷰다 - 볼륨 값은 SoundManager가 소유하고 여기선 슬라이더에
// 비추고 되돌려주기만 한다.
//
// SoundManager는 DontDestroyOnLoad라 타이틀에서 바꾼 값이 전투 씬까지 그대로 따라간다.
// 다만 그러려면 타이틀 씬에도 SoundManager 인스턴스가 있어야 한다(없으면 아래 경고).
public class OptionsPanel : MonoBehaviour
{
    [Header("슬라이더")]
    [Tooltip("셋 다 0~1 범위여야 한다. Whole Numbers는 꺼둘 것.")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("값 표시 (선택)")]
    [SerializeField] private TMP_Text masterValueText;
    [SerializeField] private TMP_Text bgmValueText;
    [SerializeField] private TMP_Text sfxValueText;

    [Header("버튼")]
    [SerializeField] private Button closeButton;

    [Header("언어")]
    [Tooltip("누를 때마다 한국어 ↔ 영어를 오간다. 전투 중에는 바꿀 수 없고 타이틀에만 둔다 - " +
             "CardName이 곧 타이핑 매칭 키라서 런 도중에 바꾸면 사전과 손패가 어긋난다.")]
    [SerializeField] private Button languageButton;

    [Tooltip("언어 버튼에 표시할 글자. 지금 언어가 아니라 '누르면 바뀔 언어'를 보여준다. " +
             "비워두면 버튼 안의 TMP 라벨을 자동으로 찾는다.")]
    [SerializeField] private TMP_Text languageButtonText;

    [Tooltip("지금 영어일 때 언어 버튼에 띄울 글자. 라벨은 '지금 언어'가 아니라 " +
             "'누르면 바뀔 언어'를 보여주므로, 영어일 때 한국어를 적는 게 맞다.")]
    [SerializeField] private string toKoreanLabel = "한국어";

    [Tooltip("지금 한국어일 때 언어 버튼에 띄울 글자. 위와 같은 이유로 영어를 적는다.")]
    [SerializeField] private string toEnglishLabel = "English";

    [Header("닫기 버튼 글자")]
    [SerializeField] private string closeKorean = "닫기";
    [SerializeField] private string closeEnglish = "CLOSE";

    private void OnEnable()
    {
        var sound = SoundManager.Instance;
        if (sound == null)
        {
            Debug.LogWarning("OptionsPanel: SoundManager.Instance가 없습니다. 이 씬에 SoundManager 프리팹을 배치해야 볼륨이 적용됩니다.", this);
        }
        else
        {
            // 저장된 값으로 슬라이더를 맞춘다. SetValueWithoutNotify가 아니면 여기서
            // onValueChanged가 되쏘아 방금 읽은 값을 그대로 덮어쓴다.
            SyncSlider(masterSlider, sound.MasterVolume, masterValueText);
            SyncSlider(bgmSlider, sound.BGMVolume, bgmValueText);
            SyncSlider(sfxSlider, sound.SFXVolume, sfxValueText);
        }

        Subscribe(masterSlider, HandleMasterChanged, nameof(masterSlider));
        Subscribe(bgmSlider, HandleBGMChanged, nameof(bgmSlider));
        Subscribe(sfxSlider, HandleSFXChanged, nameof(sfxSlider));

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        else
            Debug.LogWarning("OptionsPanel: closeButton이 연결되지 않아 창을 닫을 수 없습니다.", this);

        if (languageButton != null)
            languageButton.onClick.AddListener(HandleLanguageClicked);
        else
            Debug.LogWarning("OptionsPanel: languageButton이 연결되지 않아 언어를 바꿀 수 없습니다.", this);

        RefreshLanguageButton();
    }

    private void OnDisable()
    {
        if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(HandleMasterChanged);
        if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(HandleBGMChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(HandleSFXChanged);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        if (languageButton != null)
            languageButton.onClick.RemoveListener(HandleLanguageClicked);

        // 드래그 중에는 적용만 하고, 창을 닫을 때 한 번만 디스크에 쓴다.
        if (SoundManager.Instance != null)
            SoundManager.Instance.SaveVolumes();
    }

    // 값의 소유자는 LanguageSettings다. 여기선 누르고 비추기만 한다(볼륨이 SoundManager를 대하는 방식과 같다).
    private void HandleLanguageClicked()
    {
        LanguageSettings.Toggle();
        RefreshLanguageButton();
    }

    // 지금 언어가 아니라 "누르면 바뀔 언어"를 보여준다 - 버튼은 눌렀을 때 무슨 일이 일어나는지를
    // 알려주는 게 맞고, 지금 언어는 나머지 UI가 이미 그 언어로 떠 있어서 알 수 있다.
    private void RefreshLanguageButton()
    {
        var label = languageButtonText;
        if (label == null && languageButton != null)
            label = languageButton.GetComponentInChildren<TMP_Text>(true);

        // ⚠️ LanguageSettings.Pick을 쓰지 않는다. Pick은 "지금 언어에 맞는 값"을 고르는데
        // 여기는 일부러 반대를 고르기 때문이다 - 지금 영어면 "한국어"를 띄워야 한다.
        if (label != null)
            label.text = LanguageSettings.IsEnglish ? toKoreanLabel : toEnglishLabel;

        // 닫기 버튼도 같이 갱신한다. 라벨을 따로 배선하지 않아도 되게 버튼에서 자식을 찾는다.
        if (closeButton != null)
        {
            var closeLabel = closeButton.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel != null)
                closeLabel.text = LanguageSettings.Pick(closeKorean, closeEnglish, closeButton, "closeEnglish");
        }
    }

    private void Subscribe(Slider slider, UnityEngine.Events.UnityAction<float> handler, string fieldName)
    {
        if (slider == null)
        {
            Debug.LogWarning($"OptionsPanel: {fieldName}이(가) 연결되지 않았습니다.", this);
            return;
        }

        slider.onValueChanged.AddListener(handler);
    }

    private void SyncSlider(Slider slider, float value, TMP_Text valueText)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(value);

        UpdateValueText(valueText, value);
    }

    private static void UpdateValueText(TMP_Text valueText, float value)
    {
        if (valueText != null)
            valueText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void HandleMasterChanged(float value)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.SetMasterVolume(value);

        UpdateValueText(masterValueText, value);
    }

    private void HandleBGMChanged(float value)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.SetBGMVolume(value);

        UpdateValueText(bgmValueText, value);
    }

    private void HandleSFXChanged(float value)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.SetSFXVolume(value);

        UpdateValueText(sfxValueText, value);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
