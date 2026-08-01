using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

// FMOD 재생 창구이자 볼륨 설정의 소유자다. 씬을 넘어 유지되어야 하므로 싱글턴 +
// DontDestroyOnLoad다(프로젝트의 인스펙터 배선 컨벤션에 대한 기존 예외 - 새 코드를
// 이 패턴으로 확장하지 말 것).
//
// 볼륨 3종은 전부 FMOD의 "버스"에 건다. FMOD Studio 프로젝트의 믹서 구조가 이렇다:
//
//   bus:/            (Master Bus)
//     ├─ bus:/BGM    ← BGM 이벤트
//     └─ bus:/SFX    ← Kick, Punch 이벤트
//
// 버스에 거는 게 핵심이다. 인스턴스(EventInstance.setVolume)에 걸면 그 인스턴스를
// 만들 때 볼륨이 박혀서 재생 중에는 바꿀 수 없고, SoundManager가 만들지 않은 소리에는
// 아예 걸리지 않는다. 버스는 믹서 하류라 누가 언제 재생했든 실시간으로 적용된다.
// (이 클래스는 실제로 인스턴스 방식이었다가 그 이유로 갈아엎었다 - 되돌리지 말 것.)
//
// 버스를 새로 추가하려면 FMOD Studio의 Mixer > Routing에서 New Group으로 만들고
// 이벤트를 그 그룹으로 드래그한 뒤 File > Build로 뱅크를 다시 빌드해야 한다.
// 뱅크를 다시 빌드하지 않으면 Master.strings.bank에 경로가 없어 여기서 못 찾는다.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    private const string MasterBusPath = "bus:/";
    private const string BGMBusPath = "bus:/BGM";
    private const string SFXBusPath = "bus:/SFX";

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

    // 경로별로 첫 실패만 경고한다. 볼륨을 움직일 때마다 불리므로 그대로 두면 폭주한다.
    private readonly HashSet<string> _warnedBuses = new HashSet<string>();

    public float MasterVolume => _masterVolume;
    public float BGMVolume => _bgmVolume;
    public float SFXVolume => _sfxVolume;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 여기선 값만 읽어둔다. FMOD 호출은 뱅크가 올라온 뒤라야 안전하므로 Start로 미룬다.
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
        ApplyAllVolumes();
    }

    // ── 볼륨 ────────────────────────────────────────────────────────────────

    public void SetMasterVolume(float value)
    {
        _masterVolume = Mathf.Clamp01(value);
        ApplyVolume(MasterBusPath, _masterVolume);
    }

    public void SetBGMVolume(float value)
    {
        _bgmVolume = Mathf.Clamp01(value);
        ApplyVolume(BGMBusPath, _bgmVolume);
    }

    public void SetSFXVolume(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
        ApplyVolume(SFXBusPath, _sfxVolume);
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

    private void ApplyAllVolumes()
    {
        ApplyVolume(MasterBusPath, _masterVolume);
        ApplyVolume(BGMBusPath, _bgmVolume);
        ApplyVolume(SFXBusPath, _sfxVolume);
    }

    private void ApplyVolume(string busPath, float volume)
    {
        if (TryGetBus(busPath, out var bus))
            bus.setVolume(ToGain(volume));
    }

    // RuntimeManager.GetBus는 못 찾으면 예외를 던지므로 RESULT를 직접 확인한다.
    private bool TryGetBus(string busPath, out Bus bus)
    {
        bus = default;

        try
        {
            // StudioSystem에 접근하는 것 자체가 FMOD 초기화를 유발한다.
            // RuntimeManager.IsInitialized로 먼저 막으면 안 된다 - 그건 초기화를
            // 유발하지 않아서, 아직 아무 소리도 재생하지 않은 타이틀 씬에서는 항상
            // false가 되고 볼륨이 조용히 적용되지 않는다.
            var result = RuntimeManager.StudioSystem.getBus(busPath, out bus);
            if (result == FMOD.RESULT.OK)
                return true;

            LogBusWarning(busPath, result.ToString());
            return false;
        }
        catch (System.Exception e)
        {
            // 뱅크가 없는 등의 이유로 FMOD 초기화 자체가 실패한 경우.
            LogBusWarning(busPath, e.Message);
            return false;
        }
    }

    private void LogBusWarning(string busPath, string reason)
    {
        if (!_warnedBuses.Add(busPath))
            return;

        Debug.LogWarning(
            $"SoundManager: 버스 '{busPath}'를 찾지 못했습니다({reason}). 해당 볼륨이 적용되지 않습니다. " +
            "FMOD Studio에서 그룹을 만든 뒤 File > Build로 뱅크를 다시 빌드했는지 확인하세요.", this);
    }

    private static float ToGain(float volume) => Mathf.Pow(Mathf.Clamp01(volume), VolumeCurve);

    // ── 재생 ────────────────────────────────────────────────────────────────
    //
    // 볼륨은 버스가 담당하므로 재생 쪽에서는 신경 쓰지 않는다.

    public void PlayBGM(EventReference bgmEvent)
    {
        if (bgmEvent.IsNull) return;

        StopBGM();

        bgmInstance = RuntimeManager.CreateInstance(bgmEvent);
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

        RuntimeManager.PlayOneShot(sfxEvent);
    }

    public void PlaySFX(EventReference sfxEvent, Vector3 position)
    {
        if (sfxEvent.IsNull) return;

        RuntimeManager.PlayOneShot(sfxEvent, position);
    }

    public void SetBGMParameter(string parameterName, float value)
    {
        if (!bgmInstance.isValid()) return;

        bgmInstance.setParameterByName(parameterName, value);
    }
}
