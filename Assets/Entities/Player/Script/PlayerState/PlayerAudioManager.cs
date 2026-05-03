using UnityEngine;

public class PlayerAudioManager : MonoBehaviour
{
	[Header("사운드 설정")]
	public AudioSource audioSource;
	public AudioClip attackSound;
	public AudioClip hitSound; // 나중에 맞을 때 쓸 소리

	// 공격 소리를 재생하는 함수
	public void PlayAttackSound()
	{
		if (audioSource != null && attackSound != null)
		{
			audioSource.PlayOneShot(attackSound);
		}
	}

	// 피격 소리를 재생하는 함수
	public void PlayHitSound()
	{
		if (audioSource != null && hitSound != null)
		{
			audioSource.PlayOneShot(hitSound);
		}
	}
}