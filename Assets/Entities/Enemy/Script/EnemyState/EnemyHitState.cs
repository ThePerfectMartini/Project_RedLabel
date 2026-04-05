using UnityEngine;

public class EnemyHitState : EnemyState
{
    public EnemyHitState(Enemy enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine)
    {}

    public override void Enter()
    {
        base.Enter();

        Enemy.animationController.Play("Hit");
        Enemy.animationController.OnAnimEnded += HitEnded;
    }

    public void AnimationReset()
    {
        Enemy.animationController.PlaySetTime("Hit",0f);
    } 
    
    private void HitEnded()
    {
        Enemy.StateMachine.ChangeState(StateMachine.IdleState);
    }

    
    public override void Exit()
    {
        base.Exit();
        Enemy.animationController.OnAnimEnded -= HitEnded;
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

    }
}