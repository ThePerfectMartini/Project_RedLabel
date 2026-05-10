using UnityEngine;

public class UISoundManager : MonoBehaviour
{
	[Header("UI 사운드 설정")]
	public AudioSource uiAudioSource; // 방금 캔버스에 달아준 스피커
	public AudioClip clickSound;      // 마우스 클릭 소리 파일

	// 버튼의 OnClick 이벤트에 연결해 줄 함수
	public void PlayClickSound()
	{
		if (uiAudioSource != null && clickSound != null)
		{
			uiAudioSource.PlayOneShot(clickSound);
		}
	}
}