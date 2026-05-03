using UnityEngine;

public class EnemyAttackState : EnemyState
{
    public EnemyAttackState(Enemy enemy, EnemyStateMachine stateMachine)
        : base(enemy, stateMachine)
    {}

    public override void Enter()
    {
        base.Enter();
        
        Enemy.animationController.Play("Attack");
        Enemy.animationController.OnAnimEnded += AttackEnded;
        Enemy.animationController.OnAttackHitCheck += TriggerHitCheck;
		Enemy.audioManager.PlayAttackSound();//공격소리를 재생

	}

	private void AttackEnded()
    {
        Enemy.StateMachine.ChangeState(StateMachine.IdleState);
    }

    
    public override void Exit()
    {
        base.Exit();
        Enemy.animationController.OnAnimEnded -= AttackEnded;
        Enemy.animationController.OnAttackHitCheck -= TriggerHitCheck;
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

    }
    
    private void TriggerHitCheck()
    {
        // 물리적인 타격 연산은 CombatController에게 위임
        Enemy.combatController.PerformHitCheck();
    }
    
}