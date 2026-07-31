using UnityEngine;
using FMODUnity;
using FMOD.Studio;

// FMOD 재생 창구이자 볼륨 설정의 소유자다. 씬을 넘어 유지되어야 하므로 싱글턴 +
// DontDestroyOnLoad다(프로젝트의 인스펙터 배선 컨벤션에 대한 기존 예외 - 새 코드를
// 이 패턴으로 확장하지 말 것).
//
// 볼륨 3종은 거는 지점이 서로 다르다. 이 FMOD 프로젝트에는 VCA도 버스(그룹)도 없어서
// 이벤트가 전부 마스터 버스로 직결되기 때문이다.
//   마스터 - bus:/ 의 볼륨. 어느 FMOD 프로젝트에나 항상 있어 별도 저작이 필요 없고,
//            하류라서 BGM/SFX에 자동으로 곱해진다.
//   BGM    - 들고 있는 bgmInstance에 직접 건다.
//   SFX    - 원샷이라 핸들을 남기지 않으므로 값만 들고 있다가 재생 시점에 건다.
// FMOD Studio에 VCA를 만들게 되면 이 셋을 GetVCA(...).setVolume 하나로 통일할 수 있다.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    private const string MasterVolumeKey = "option.volume.master";
    private const string BGMVolumeKey = "option.volume.bgm";
    private const string SFXVolumeKey = "option.volume.sfx";

    // setVolume은 진폭(선형)이라 슬라이더를 그대로 넘기면 절반 위치가 이미 거의 최대
    // 크기로 들린다. 청감은 로그에 가까우므로 제곱해서 넘긴다 - UI가 보여주는 %와
    // 실제 게인은 일부러 다르다.
    private const float VolumeCurve = 2f;

    private EventInstance bgmInstance;

    private float _masterVolume = 1f;
    private float _bgmVolume = 1f;
    private float _sfxVolume = 1f;

    private bool _busWarningLogged;

    public float MasterVolume => _masterVolume;
    public float BGMVolume => _bgmVolume;
    public float SFXVolume => _sfxVolume;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 여기선 값만 읽어둔다. FMOD 호출은 RuntimeManager가 뱅크를 올린 뒤라야
            // 안전하므로 Start로 미룬다.
            LoadVolumes();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 옵션 창을 한 번도 열지 않아도 지난 실행의 설정이 그대로 적용되어야 한다.
        ApplyMasterVolume();
    }

    // ── 볼륨 ────────────────────────────────────────────────────────────────

    public void SetMasterVolume(float value)
    {
        _masterVolume = Mathf.Clamp01(value);
        ApplyMasterVolume();
    }

    public void SetBGMVolume(float value)
    {
        _bgmVolume = Mathf.Clamp01(value);

        // 재생 중인 BGM에 즉시 반영한다.
        if (bgmInstance.isValid())
            bgmInstance.setVolume(ToGain(_bgmVolume));
    }

    // SFX는 원샷이라 이미 나가고 있는 소리를 되돌릴 수 없다. 다음 재생부터 적용된다.
    public void SetSFXVolume(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
    }

    public void LoadVolumes()
    {
        _masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        _bgmVolume = PlayerPrefs.GetFloat(BGMVolumeKey, 1f);
        _sfxVolume = PlayerPrefs.GetFloat(SFXVolumeKey, 1f);
    }

    public void SaveVolumes()
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, _masterVolume);
        PlayerPrefs.SetFloat(BGMVolumeKey, _bgmVolume);
        PlayerPrefs.SetFloat(SFXVolumeKey, _sfxVolume);
        PlayerPrefs.Save();
    }

    private void ApplyMasterVolume()
    {
        if (TryGetMasterBus(out var bus))
            bus.setVolume(ToGain(_masterVolume));
    }

    // RuntimeManager.GetBus는 못 찾으면 예외를 던지므로 RESULT를 직접 확인한다.
    private bool TryGetMasterBus(out Bus bus)
    {
        bus = default;

        try
        {
            // StudioSystem에 접근하는 것 자체가 FMOD 초기화를 유발한다.
            // RuntimeManager.IsInitialized로 먼저 막으면 안 된다 - 그건 초기화를
            // 유발하지 않아서, 아직 아무 소리도 재생하지 않은 타이틀 씬에서는 항상
            // false가 되고 마스터 볼륨이 조용히 적용되지 않는다.
            var result = RuntimeManager.StudioSystem.getBus("bus:/", out bus);
            if (result == FMOD.RESULT.OK)
                return true;

            LogBusWarning(result.ToString());
            return false;
        }
        catch (System.Exception e)
        {
            // 뱅크가 없는 등의 이유로 FMOD 초기화 자체가 실패한 경우.
            LogBusWarning(e.Message);
            return false;
        }
    }

    // 적용 지점이 여럿이라 매번 찍으면 로그가 폭주한다. 첫 실패만 남긴다.
    private void LogBusWarning(string reason)
    {
        if (_busWarningLogged)
            return;

        _busWarningLogged = true;
        Debug.LogWarning($"SoundManager: 마스터 버스(bus:/)를 찾지 못했습니다({reason}). 마스터 볼륨이 적용되지 않습니다.", this);
    }

    private static float ToGain(float volume) => Mathf.Pow(Mathf.Clamp01(volume), VolumeCurve);

    // ── 재생 ────────────────────────────────────────────────────────────────

    public void PlayBGM(EventReference bgmEvent)
    {
        if (bgmEvent.IsNull) return;

        StopBGM();

        bgmInstance = RuntimeManager.CreateInstance(bgmEvent);

        // start 전에 걸어야 한다. 빠뜨리면 옵션에서 낮춰둔 값이 무시되고
        // 새 BGM만 원래 크기로 시작한다.
        bgmInstance.setVolume(ToGain(_bgmVolume));
        bgmInstance.start();
    }

    public void StopBGM()
    {
        // 한 번도 재생하지 않은 상태에서도 PlayBGM이 먼저 이걸 부른다.
        // 가드가 없으면 초기화되지 않은 핸들을 건드리게 된다.
        if (!bgmInstance.isValid()) return;

        // STOP_MODE는 FMODUnity와 FMOD.Studio 양쪽에 있어 풀네임이 아니면 모호해진다(CS0104).
        bgmInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        bgmInstance.release();
        bgmInstance.clearHandle();
    }

    public void PlaySFX(EventReference sfxEvent)
    {
        if (sfxEvent.IsNull) return;

        // PlayOneShot은 핸들을 주지 않아 볼륨을 걸 수 없다. 그래서 직접 만들어 건다.
        // start 직후 release해도 재생이 끝나면 FMOD가 알아서 정리하므로 누수는 없다.
        var instance = RuntimeManager.CreateInstance(sfxEvent);
        instance.setVolume(ToGain(_sfxVolume));
        instance.start();
        instance.release();
    }

    public void PlaySFX(EventReference sfxEvent, Vector3 position)
    {
        if (sfxEvent.IsNull) return;

        var instance = RuntimeManager.CreateInstance(sfxEvent);
        instance.setVolume(ToGain(_sfxVolume));
        instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
        instance.start();
        instance.release();
    }

    public void SetBGMParameter(string parameterName, float value)
    {
        if (!bgmInstance.isValid()) return;

        bgmInstance.setParameterByName(parameterName, value);
    }
}
