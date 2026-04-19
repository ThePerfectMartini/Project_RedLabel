using UnityEngine;

public class UI_Billboard : MonoBehaviour
{
	private Transform cam;

	void Start()
	{
		// 메인 카메라의 위치를 찾습니다.
		cam = Camera.main.transform;
	}

	void LateUpdate()
	{
		// UI가 항상 카메라를 똑바로 바라보게 만듭니다.
		transform.LookAt(transform.position + cam.forward);
	}
}
