using UnityEngine;

public class EnemyMoveState : EnemyState
{
    public float moveSpeed = 3f;

    public EnemyMoveState(Enemy enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine)
    {}
    
    public override void Enter()
    {
        base.Enter();
        Enemy.animationController.Play("Move");
    }

    public override void Exit()
    {
        base.Exit();
        // 이동 상태가 끝날 때 컨트롤러를 통해 즉시 정지시킵니다.
        Enemy.Movement.StopImmediately();
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();

        // 1. AI에게서 8방향 3D 벡터(X, Z)를 가져옵니다.
        Vector3 direction = Enemy.aiController.Get8WayDirectionToTarget();

        // 2. 이동 컨트롤러에게 이동과 방향 전환(Flip) 처리를 맡깁니다.
        Enemy.Movement.Move(direction, moveSpeed);
    }
}