using Unity.VisualScripting;
using UnityEngine;

public class PlayerMoveState : PlayerState
{
    Vector2 input;
    public PlayerMoveState(Player player, PlayerStateMachine stateMachine, string animName)
        : base(player, stateMachine, animName)
    {
        this.Player = player;
        this.StateMachine = stateMachine;
        AnimHash = Animator.StringToHash(animName);
    }
    
    public override void Enter()
    {
        base.Enter();
        
        Player.playerAnimationController.PlayAnimation(AnimHash);
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();
        
        // 1. 입력값
        input = Player.inputHandler.MoveInput;

        // 3. 상태 전이
        if (input == Vector2.zero)
        {
            StateMachine.ChangeState(StateMachine.idleState);
        }

        // [점프] CheckIfGrounded도 Movement에 있는 것을 사용
        if (Player.inputHandler.JumpTriggered && Player.movement.CheckIfGrounded())
        {
            StateMachine.ChangeState(StateMachine.jumpState);
        }

        // [달리기]
        if (Player.inputHandler.runTriggered)
        {
            StateMachine.ChangeState(StateMachine.runState);
        }
        
        // [공격]
        if (Player.inputHandler.AttackTriggered)
        {
            StateMachine.ChangeState(StateMachine.attackState);
        }
    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();


        // 2. [변경점] Movement 컴포넌트를 통해 이동 명령
        Player.movement.Move(input, Player.characterData.moveSpeed);
    }
}