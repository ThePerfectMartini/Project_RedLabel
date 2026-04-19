using UnityEngine;

public class BossAttackState : BossState
{
    public BossAttackState(Boss Boss, BossStateMachine stateMachine)
        : base(Boss, stateMachine)
    {}

    public override void Enter()
    {
        base.Enter();
        Boss.animationController.OnAnimEnded += AttackEnded;
        Boss.animationController.OnAttackHitCheck += TriggerHitCheck;

        if (Boss.pattern == 0)
        {
            Boss.animationController.Play("Attack");
        }else if (Boss.pattern == 1)
        {
            Boss.animationController.Play("Pattern1");
        }else if (Boss.pattern == 2)
        {
            
        }
        else
        {
            
        }
    }
    
    private void AttackEnded()
    {
        Boss.StateMachine.ChangeState(StateMachine.IdleState);
    }

    
    public override void Exit()
    {
        base.Exit();
        Boss.animationController.OnAnimEnded -= AttackEnded;
        Boss.animationController.OnAttackHitCheck -= TriggerHitCheck;
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

    }
    
    private void TriggerHitCheck()
    {
        // 물리적인 타격 연산은 CombatController에게 위임
        Boss.combatController.PerformHitCheck();
    }
    
}