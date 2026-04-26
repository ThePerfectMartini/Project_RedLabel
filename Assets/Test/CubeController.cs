using UnityEngine;

public class CubeController : MonoBehaviour
{
    private PlayerInputAction inputActions;
    private Vector2 moveInput;
    
    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    private void Awake()
    {
        // 자동 생성된 Input Action 클래스의 인스턴스를 생성합니다.
        inputActions = new PlayerInputAction();
    }

    private void OnEnable()
    {
        // Player 액션 맵을 활성화합니다.
        inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        // 스크립트가 비활성화될 때 액션 맵도 비활성화하여 메모리 누수를 방지합니다.
        inputActions.Player.Disable();
    }

    private void Update()
    {
        // Move 액션에서 Vector2 입력값을 읽어옵니다. (방향키 입력)
        moveInput = inputActions.Player.Move.ReadValue<Vector2>();

        // 2D 입력값을 3D 공간의 이동 벡터로 변환합니다. (Y축은 점프에 사용하므로 0으로 둡니다)
        Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);

        // 프레임 속도에 상관없이 일정한 속도로 큐브를 이동시킵니다.
        transform.Translate(movement * moveSpeed * Time.deltaTime, Space.World);
    }
}