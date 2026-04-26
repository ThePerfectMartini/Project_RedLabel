using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
	[Header("일시정지 UI 연결")]
	public GameObject pausePanel;

	private bool isPaused = false;

	void Start()
	{
		// 게임 시작 시 무조건 숨기기
		if (pausePanel != null)
		{
			pausePanel.SetActive(false);
			Time.timeScale = 1f;
		}
	}

	void Update()
	{
		
		if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
		{
			if (isPaused)
			{
				ResumeGame();
			}
			else
			{
				PauseGame();
			}
		}
	}

	// 일시정지 시키는 함수
	public void PauseGame()
	{
		pausePanel.SetActive(true);
		Time.timeScale = 0f;
		isPaused = true;
	}

	// 게임 다시 굴러가게 하는 함수
	public void ResumeGame()
	{
		pausePanel.SetActive(false);
		Time.timeScale = 1f;
		isPaused = false;
	}
}