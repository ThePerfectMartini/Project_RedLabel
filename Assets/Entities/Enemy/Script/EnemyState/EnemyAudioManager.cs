using UnityEngine;

public class EnemyAudioManager : MonoBehaviour
{
	public AudioSource audioSource;
	public AudioClip attackSound;
	public AudioClip hitSound;


	public void PlayAttackSound()
	{
		if (audioSource != null && attackSound != null)
		{
			audioSource.PlayOneShot(attackSound);
		}
	}

	public void PlayHitSound()
	{
		if (audioSource != null && hitSound != null)
		{
			audioSource.PlayOneShot(hitSound);
		}
	}
}