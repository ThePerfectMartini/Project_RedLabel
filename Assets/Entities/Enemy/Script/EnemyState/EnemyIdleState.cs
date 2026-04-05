using UnityEngine;

public class EnemyIdleState : EnemyState
{
    public EnemyIdleState(Enemy enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine)
    {}

    public override void Enter()
    {
        base.Enter();
        
        Enemy.animationController.Play("Idle");
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