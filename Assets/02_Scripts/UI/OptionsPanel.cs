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
    [Tooltip("누를 때마다 한국어 -> 영어 -> 프랑스어 -> 스페인어 순으로 오갑니다.")]
    [SerializeField] private Button languageButton;

    [Tooltip("언어 버튼에 표시할 글자. 비워두면 버튼 안의 TMP 라벨을 자동으로 찾는다.")]
    [SerializeField] private TMP_Text languageButtonText;

    [Header("언어 표시 텍스트")]
    [Tooltip("현재 한국어일 때 띄울 글자")]
    [SerializeField] private string labelKorean = "< 한국어 >";

    [Tooltip("현재 영어일 때 띄울 글자")]
    [SerializeField] private string labelEnglish = "< English >";

    [Tooltip("현재 프랑스어일 때 띄울 글자")]
    [SerializeField] private string labelFrench = "< Français >";

    [Tooltip("현재 스페인어일 때 띄울 글자")]
    [SerializeField] private string labelSpanish = "< Español >";

    [Tooltip("현재 일본어일 때 띄울 글자")]
    [SerializeField] private string labelJapanese = "< 日本語 >";

    [Header("닫기 버튼 글자")]
    [SerializeField] private string closeKorean = "닫기";
    [SerializeField] private string closeEnglish = "CLOSE";
    [SerializeField] private string closeFrench = "FERMER";
    [SerializeField] private string closeSpanish = "CERRAR";
    [SerializeField] private string closeJapanese = "閉じる";

    [Header("열림 연출")]
    [Tooltip("옵션 창이 열릴 때(OnEnable) 같이 재생할 마스크 리빌 연출들(제목, 배경 윈도우 등 - 마스크마다 " +
             "컴포넌트가 하나씩 따로 필요하다). BattleManager.resultReveals/RewardCardView.reveals와 같은 패턴 - " +
             "OnEnable에서 자동 재생되지 않는 컴포넌트라 여기서 직접 Play()를 불러야 한다.")]
    [SerializeField] private TextGateRevealAnimation[] titleReveals;

    [Tooltip("옵션 창 전체에 거는 확대/축소+페이드 연출(선택, StageStartEffect 재사용). OnEnable에서 " +
             "자기가 알아서 재생하므로 여기서 따로 부르지 않는다 - Close()에서 titleReveals와 함께 " +
             "\"둘 다 끝난 뒤에만\" 패널을 끄는 데만 쓴다.")]
    [SerializeField] private StageStartEffect openCloseEffect;

    private void OnEnable()
    {
        if (titleReveals != null)
        {
            foreach (var reveal in titleReveals)
            {
                if (reveal != null)
                    reveal.Play();
            }
        }

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

    // 2. Update 메서드 추가 (방향키 감지)
    private void Update()
    {
        // 언어 버튼이 선택(포커스)된 상태일 때만 방향키 입력을 받습니다.
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == languageButton.gameObject)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                // 왼쪽 화살표나 A 키: 이전 언어
                if (UnityEngine.InputSystem.Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                    UnityEngine.InputSystem.Keyboard.current.aKey.wasPressedThisFrame)
                {
                    LanguageSettings.ChangeLanguage(-1);
                    RefreshLanguageButton();
                }
                // 오른쪽 화살표나 D 키: 다음 언어
                else if (UnityEngine.InputSystem.Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                         UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame)
                {
                    LanguageSettings.ChangeLanguage(1);
                    RefreshLanguageButton();
                }
            }
        }
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
        LanguageSettings.ChangeLanguage(1);
        RefreshLanguageButton();
    }

    // 지금 언어가 아니라 "누르면 바뀔 언어"를 보여준다 - 버튼은 눌렀을 때 무슨 일이 일어나는지를
    // 알려주는 게 맞고, 지금 언어는 나머지 UI가 이미 그 언어로 떠 있어서 알 수 있다.
    private void RefreshLanguageButton()
    {
        var label = languageButtonText;
        if (label == null && languageButton != null)
            label = languageButton.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
        {
            // [수정] '다음' 언어가 아닌 '현재' 언어를 표시합니다.
            switch (LanguageSettings.Current)
            {
                case GameLanguage.Korean: label.text = labelKorean; break;
                case GameLanguage.English: label.text = labelEnglish; break;
                case GameLanguage.French: label.text = labelFrench; break;
                case GameLanguage.Spanish: label.text = labelSpanish; break;
                case GameLanguage.Japanese: label.text = labelJapanese; break;
            }
        }

        if (closeButton != null)
        {
            var closeLabel = closeButton.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel != null)
            {
                switch (LanguageSettings.Current)
                {
                    case GameLanguage.Korean: closeLabel.text = closeKorean; break;
                    case GameLanguage.English: closeLabel.text = closeEnglish; break;
                    case GameLanguage.French: closeLabel.text = closeFrench; break;
                    case GameLanguage.Spanish: closeLabel.text = closeSpanish; break;
                    case GameLanguage.Japanese: closeLabel.text = closeJapanese; break;
                }
            }
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

    // 열릴 때(OnEnable)와 반대로 titleReveals + openCloseEffect가 전부 닫히는 연출을 마친
    // 뒤에야 패널을 끈다 - 곧바로 SetActive(false)하면 재생 중이던 코루틴이 그 자리에서
    // 끊겨(같은 오브젝트라 전부 같이 죽는다) 마스크나 확대/축소가 멈춘 잔상으로 남는다.
    // 연출마다 duration이 다를 수 있어 "가장 늦게 끝나는 것"을 기준으로 잡아야 해서, 개수를
    // 세어뒀다가 마지막 콜백에서만 실제로 끈다. openCloseEffect.PlayExit엔 반드시
    // deactivateOnComplete: false를 넘긴다 - 그게 없으면 이 연출이 titleReveals보다 먼저
    // 끝났을 때 자기가 먼저 SetActive(false)를 불러 아직 도는 리빌 코루틴을 끊어버린다.
    public void Close()
    {
        var pending = 0;
        if (titleReveals != null)
        {
            foreach (var reveal in titleReveals)
            {
                if (reveal != null)
                    pending++;
            }
        }

        if (openCloseEffect != null)
            pending++;

        if (pending == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        void HandleOneFinished()
        {
            pending--;
            if (pending <= 0)
                gameObject.SetActive(false);
        }

        if (titleReveals != null)
        {
            foreach (var reveal in titleReveals)
            {
                if (reveal != null)
                    reveal.PlayReverse(HandleOneFinished);
            }
        }

        if (openCloseEffect != null)
            openCloseEffect.PlayExit(HandleOneFinished, deactivateOnComplete: false);
    }
}
