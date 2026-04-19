using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MovementModule
{
    private Rigidbody RB;
    private Transform transform; // 회전을 위해 Transform 참조 보관
    private Vector3 workspace; 
    public int FacingDirection { get; private set; } = 1; 

    public MovementModule(Rigidbody rb, Transform enemyTransform)
    {
        this.RB = rb;
        this.transform = enemyTransform;
    }

    /// <summary>
    /// 외부에서 주어진 방향(direction)과 속도(speed)로 물리적 이동을 수행합니다.
    /// </summary>
    public void Move(Vector3 direction, float speed)
    {
        // X, Z 방향으로 이동하되 현재의 중력(Y)은 유지합니다.
        workspace.Set(direction.x * speed, RB.linearVelocity.y, direction.z * speed);
        RB.linearVelocity = workspace;

        // X축 이동 방향에 맞춰 좌우를 바라보도록 처리합니다.
        if (direction.x != 0)
        {
            CheckIfShouldFlip((int)Mathf.Sign(direction.x));
        }
    }

    // 상태를 벗어날 때 미끄러짐을 방지합니다.
    public void StopImmediately()
    {
        workspace.Set(0, RB.linearVelocity.y, 0); 
        RB.linearVelocity = workspace;
    }
    
    public void FaceTarget(Vector3 direction)
    {
        // X축 방향이 0이 아닐 때만 회전 체크를 합니다. (위치는 이동하지 않음)
        if (direction.x != 0)
        {
            CheckIfShouldFlip((int)Mathf.Sign(direction.x));
        }
    }

    private void CheckIfShouldFlip(int xInput)
    {
        if (xInput != 0 && xInput != FacingDirection)
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