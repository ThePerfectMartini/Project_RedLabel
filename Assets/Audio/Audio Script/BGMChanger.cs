using UnityEngine;

public class BGMChanger : MonoBehaviour
{
	[Header("BGM 설정")]
	public AudioSource mainBGMPlayer; // 기존 BGM이 나오고 있는 스피커
	public AudioClip bossBGM;         // 새로 틀어줄 보스방 브금

	// 플레이어가 포탈(콜라이더)에 닿았을 때 실행되는 마법의 함수!
	private void OnTriggerEnter(Collider other)
	{
		// 닿은 물체의 태그가 "Player"인지 확인합니다.
		if (other.CompareTag("Player"))
		{
			if (mainBGMPlayer != null && bossBGM != null)
			{
				// 1. 현재 나오고 있는 스피커의 음악(Clip)을 보스 브금으로 바꿔치기합니다!
				mainBGMPlayer.clip = bossBGM;

				// 2. 바꾼 음악을 다시 재생합니다!
				mainBGMPlayer.Play();
			}
		}
	}
}