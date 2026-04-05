using UnityEngine;

public class PlayerMovementController : MonoBehaviour
{
    // [컴포넌트]
    private Rigidbody RB;
    
    // [변수]
    private Vector3 workspace; 
    public int FacingDirection { get; private set; } = 1; 

    [Header("Checks")]
    public Transform groundCheck; 
    public float groundCheckRadius = 0.3f;
    public LayerMask whatIsGround; 
    
    private void Awake()
    {
        RB = GetComponent<Rigidbody>();
    }

    // --- [핵심 기능] ---

    // 1. 이동 (걷기, 공중 이동 등)
    public void Move(Vector2 input, float speed)
    {
        // 벨트스크롤: X, Y 입력을 X, Z 이동으로 변환
        workspace.Set(input.x * speed, RB.linearVelocity.y, input.y * speed);
        RB.linearVelocity = workspace;

        if (input.x != 0)
        {
            // input.x의 부호(1 또는 -1)를 정수로 변환하여 전달
            CheckIfShouldFlip(Mathf.RoundToInt(Mathf.Sign(input.x)));
        }
    }
    
    public void AttackThrust(float thrustSpeed)
    {
        // FacingDirection(1 또는 -1) 방향으로 thrustSpeed만큼 X축 속도를 지정
        // Y축(중력) 속도는 그대로 유지하고, Z축(깊이)은 0으로 고정하여 앞으로만 밀리게 처리
        workspace.Set(FacingDirection * thrustSpeed, RB.linearVelocity.y, 0f);
        RB.linearVelocity = workspace;
    }


    // 3. 즉시 정지 (공격 시 미끄러짐 방지)
    public void StopImmediately()
    {
        workspace.Set(0, RB.linearVelocity.y, 0); // 중력(Y)은 유지해야 함
        RB.linearVelocity = workspace;
    }
    

    // --- [Helper Methods] ---

    public bool CheckIfGrounded()
    {
        return Physics.CheckSphere(groundCheck.position, groundCheckRadius, whatIsGround);
    }

    public float GetCurrentVelocityY()
    {
        return RB.linearVelocity.y;
    }

    private void CheckIfShouldFlip(int xDirection)
    {
        if (xDirection != 0 && xDirection != FacingDirection)
        {
            Flip();
        }
    }

    private void Flip()
    {
        FacingDirection *= -1;
        transform.Rotate(0.0f, 180.0f, 0.0f);
    }
}