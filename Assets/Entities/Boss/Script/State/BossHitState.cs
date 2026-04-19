using UnityEngine;

public class BossHitState : BossState
{
    public BossHitState(Boss Boss, BossStateMachine stateMachine)
        : base(Boss, stateMachine)
    {}

    public override void Enter()
    {
        base.Enter();

        Boss.animationController.Play("Hit");
        Boss.animationController.OnAnimEnded += HitEnded;
    }

    public void AnimationReset()
    {
        Boss.animationController.PlaySetTime("Hit",0f);
    } 
    
    private void HitEnded()
    {
        Boss.StateMachine.ChangeState(StateMachine.IdleState);
    }

    
    public override void Exit()
    {
        base.Exit();
        Boss.animationController.OnAnimEnded -= HitEnded;
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

    }
}