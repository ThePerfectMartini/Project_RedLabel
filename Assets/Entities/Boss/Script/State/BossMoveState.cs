using UnityEngine;

public class BossMoveState : BossState
{
    public float moveSpeed = 3f;

    public BossMoveState(Boss Boss, BossStateMachine stateMachine)
        : base(Boss, stateMachine)
    {}
    
    public override void Enter()
    {
        base.Enter();
        Boss.animationController.Play("Move");
    }

    public override void Exit()
    {
        base.Exit();
        // 이동 상태가 끝날 때 컨트롤러를 통해 즉시 정지시킵니다.
        Boss.Movement.StopImmediately();
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();
    }
}