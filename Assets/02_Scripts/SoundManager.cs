using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    private EventInstance bgmInstance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayBGM(EventReference bgmEvent)
    {
        if (bgmEvent.IsNull) return;

        StopBGM();

        bgmInstance = RuntimeManager.CreateInstance(bgmEvent);
        bgmInstance.start();
    }

    public void StopBGM()
    {
        bgmInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        bgmInstance.release();
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
        bgmInstance.setParameterByName(parameterName, value);
    }
}