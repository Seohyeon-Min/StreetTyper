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
    }

    private void OnDisable()
    {
        if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(HandleMasterChanged);
        if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(HandleBGMChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(HandleSFXChanged);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        // 드래그 중에는 적용만 하고, 창을 닫을 때 한 번만 디스크에 쓴다.
        if (SoundManager.Instance != null)
            SoundManager.Instance.SaveVolumes();
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
