using UnityEngine;

public class CameraResolution : MonoBehaviour
{
	void Start()
	{
		// 16:9 고정
		float targetWidthAspect = 16.0f;
		float targetHeightAspect = 9.0f;

		// 카메라 컴포넌트 가져오기
		Camera camera = GetComponent<Camera>();

		// 현재 모니터의 화면 비율 계산
		float targetRatio = targetWidthAspect / targetHeightAspect;
		float currentRatio = (float)Screen.width / (float)Screen.height;
		float scaleHeight = currentRatio / targetRatio;

		// 화면 영역(Viewport Rect) 조절을 위한 변수
		Rect rect = camera.rect;

		// 1. 현재 화면이 설정한 비율보다 세로로 길 때 (위아래로 블랙바 생성)
		if (scaleHeight < 1.0f)
		{
			rect.width = 1.0f;
			rect.height = scaleHeight;
			rect.x = 0;
			rect.y = (1.0f - scaleHeight) / 2.0f;
		}
		// 2. 현재 화면이 설정한 비율보다 가로로 길 때 (양옆으로 블랙바 생성)
		else
		{
			float scaleWidth = 1.0f / scaleHeight;
			rect.width = scaleWidth;
			rect.height = 1.0f;
			rect.x = (1.0f - scaleWidth) / 2.0f;
			rect.y = 0;
		}

		// 계산된 비율을 카메라에 적용!
		camera.rect = rect;
	}
}