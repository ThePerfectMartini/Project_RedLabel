using UnityEngine;

public class BossIdleState : BossState
{
    public BossIdleState(Boss Boss, BossStateMachine stateMachine)
        : base(Boss, stateMachine)
    {}

    public override void Enter()
    {
        base.Enter();
        
        Boss.animationController.Play("Idle");
    }
    
    public override void Exit()
    {
        base.Exit();
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();
        
    }
}