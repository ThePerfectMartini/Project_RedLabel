using UnityEngine;

public class PlayerMoveState : PlayerState
{
    Vector2 input;
    public PlayerMoveState(Player player, PlayerStateMachine stateMachine)
        : base(player, stateMachine)
    {}
    
    public override void Enter()
    {
        base.Enter();
        // 공격 구독
        Player.InputHandler.OnAttackEvent += HandleAttackInput;

        if (Player.InputHandler.RunTriggered)
        {
            Player.animationController.Play("Run");
        }
        else
        {
            Player.animationController.Play("Move");
        }
        
    }

    public override void Exit()
    {
        base.Exit();
        // 공격 구독
        Player.InputHandler.OnAttackEvent -= HandleAttackInput;

    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();
        
        input = Player.InputHandler.MoveInput;

        if (input == Vector2.zero)
        {
            StateMachine.ChangeState(StateMachine.IdleState);
        }
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();
        if (Player.InputHandler.RunTriggered)
        {
            Player.movement.Move(input, Player.runSpeed);    
        }
        else
        {
            Player.movement.Move(input, Player.moveSpeed);
        }
    }
    
    private void HandleAttackInput()
    {
        StateMachine.ChangeState(StateMachine.AttackState); 
    }
}