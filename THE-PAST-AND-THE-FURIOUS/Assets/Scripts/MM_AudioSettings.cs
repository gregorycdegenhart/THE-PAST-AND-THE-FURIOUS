using UnityEngine;
using UnityEngine.UI;

public class AudioSettings : MonoBehaviour
{
    [Header("UI")]
    public Slider volumeSlider;

    [Header("Audio")]
    public AudioSource musicSource;

    private const string VolumeKey = "volume";

    void Start()
    {
        float savedVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        savedVolume = Mathf.Clamp01(savedVolume);

        if (volumeSlider != null)
            volumeSlider.value = savedVolume;

        ApplyVolume(savedVolume);
    }

    public void SetVolume(float value)
    {
        value = Mathf.Clamp01(value);

        ApplyVolume(value);

        PlayerPrefs.SetFloat(VolumeKey, value);
        PlayerPrefs.Save();
    }

    private void ApplyVolume(float value)
    {
        if (musicSource == null) return;

        musicSource.volume = value;
    }
}