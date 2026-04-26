using UnityEngine;
using UnityEngine.UI; 

public class VolumeController : MonoBehaviour
{
	[Header("연결할 설정들")]
	public AudioSource bgmAudioSource; // BGM 스피커
	public Slider volumeSlider;        // 슬라이더

	void Start()
	{
		// 게임 시작 시, 슬라이더의 위치를 현재 음악 볼륨과 똑같이 맞춰줍니다.
		if (bgmAudioSource != null && volumeSlider != null)
		{
			// 슬라이더의 최소/최대값 설정 (볼륨은 0부터 1 사이입니다)
			volumeSlider.minValue = 0f;
			volumeSlider.maxValue = 1f;
			volumeSlider.value = bgmAudioSource.volume;
		}
	}

	// 슬라이더를 마우스로 끌어서 움직일 때마다 이 함수가 실행됩니다!
	public void SetVolume(float sliderValue)
	{
		if (bgmAudioSource != null)
		{
			bgmAudioSource.volume = sliderValue; // 음악 볼륨을 슬라이더 값으로 변경!
		}
	}
}