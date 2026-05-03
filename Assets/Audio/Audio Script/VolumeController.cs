using UnityEngine;
using UnityEngine.UI; 

public class VolumeController : MonoBehaviour
{
	[Header("BGM 연결 설정")]
	public AudioSource bgmAudioSource; // 기존에 있던 BGM 스피커
	public Slider volumeSlider;        // 기존에 있던 BGM 슬라이더

	[Header("효과음(SE) 연결 설정")]
	public AudioSource seAudioSource;  // 🌟 새로 추가: 플레이어 효과음 스피커
	public Slider seVolumeSlider;      // 🌟 새로 추가: 효과음 슬라이더

	void Start()
	{
		// 1. 기존에 작성하신 BGM 슬라이더 초기화
		if (bgmAudioSource != null && volumeSlider != null)
		{
			volumeSlider.minValue = 0f;
			volumeSlider.maxValue = 1f;
			volumeSlider.value = bgmAudioSource.volume;
		}

		// 2. 🌟 새로 추가: 게임 시작 시 효과음 슬라이더 초기화
		if (seAudioSource != null && seVolumeSlider != null)
		{
			seVolumeSlider.minValue = 0f;
			seVolumeSlider.maxValue = 1f;
			seVolumeSlider.value = seAudioSource.volume;
		}
	}

	// 기존에 있던 BGM 조절 함수
	public void SetVolume(float sliderValue)
	{
		if (bgmAudioSource != null)
		{
			bgmAudioSource.volume = sliderValue;
		}
	}

	// 🌟 새로 추가: 슬라이더를 움직일 때 효과음 볼륨을 조절하는 함수
	public void SetSEVolume(float sliderValue)
	{
		if (seAudioSource != null)
		{
			seAudioSource.volume = sliderValue; // 효과음 볼륨을 슬라이더 값으로 변경!
		}
	}
}