using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class VolumeController : MonoBehaviour
{
	[Header("오디오 믹서 연결")]
	public AudioMixer audioMixer;

	[Header("UI 슬라이더 연결")]
	public Slider bgmSlider;
	public Slider seSlider;

	// BGM 슬라이더 조절 시 실행되는 함수
	public void SetBGMVolume(float sliderValue)
	{
		// 값이 0이 되면 소리가 완전히 꺼지도록 -80dB로 설정, 아니면 데시벨 공식 적용
		float volume = (sliderValue <= 0.0001f) ? -80f : Mathf.Log10(sliderValue) * 20f;
		audioMixer.SetFloat("BGMVolume", volume);
	}

	// SE 슬라이더 조절 시 실행되는 함수
	public void SetSEVolume(float sliderValue)
	{
		// 값이 0이 되면 소리가 완전히 꺼지도록 -80dB로 설정, 아니면 데시벨 공식 적용
		float volume = (sliderValue <= 0.0001f) ? -80f : Mathf.Log10(sliderValue) * 20f;
		audioMixer.SetFloat("SEVolume", volume);
	}
}