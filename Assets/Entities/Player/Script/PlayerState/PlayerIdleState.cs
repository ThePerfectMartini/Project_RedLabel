using UnityEngine;

public class PlayerIdleState : PlayerState
{
    public PlayerIdleState(Player player, PlayerStateMachine stateMachine)
        : base(player, stateMachine)
    {}

    public override void Enter()
    {
        base.Enter();
        Player.movement.StopImmediately();
        
        Player.animationController.Play("Idle");
        // 공격 구독
        Player.InputHandler.OnAttackEvent += HandleAttackInput;
    }
    
    public override void Exit()
    {
        base.Exit();
        // 공격 구독해제
        Player.InputHandler.OnAttackEvent -= HandleAttackInput;
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

        // 1. 이동 입력이 있으면 MoveState로
        if (Player.InputHandler.MoveInput != Vector2.zero)
        {
            StateMachine.ChangeState(StateMachine.MoveState);
        }
    }
    
    private void HandleAttackInput()
    {
        StateMachine.ChangeState(StateMachine.AttackState); 
    }
}