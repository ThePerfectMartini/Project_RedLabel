using UnityEngine;

public class EnemyAttackState : EnemyState
{
    public EnemyAttackState(Enemy enemy, EnemyStateMachine stateMachine, string animName)
        : base(enemy, stateMachine, animName)
    {
        this.Enemy = enemy;
        this.StateMachine = stateMachine;
        AnimHash = Animator.StringToHash(animName);
    }

    public override void Enter()
    {
        base.Enter();
        
        Enemy.enemyAnimationController.PlayAnimation(AnimHash);
        Enemy.movement.StopImmediately();
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();
    }
}