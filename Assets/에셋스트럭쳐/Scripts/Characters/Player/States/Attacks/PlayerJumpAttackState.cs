using UnityEngine;
using System.Collections.Generic;

public class PlayerJumpAttackState : PlayerState
{
    private bool isAnimEnded;
    public PlayerJumpAttackState(Player player, PlayerStateMachine stateMachine, string animName) 
        : base(player, stateMachine, animName)
    {
        this.Player = player;
        this.StateMachine = stateMachine;
    }

    public override void Enter()
    {
        base.Enter();
        
        isAnimEnded = false;
        
        Player.inputHandler.UseAttackInput();
        
        Player.playerAnimationController.PlayAnimation(Animator.StringToHash("JumpAttack"));
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();
        
        // [변경점] 착지 체크 (속도 확인 및 땅 체크)
        if (Player.movement.GetCurrentVelocityY() < 0.01f && Player.movement.CheckIfGrounded())
        {
            StateMachine.ChangeState(StateMachine.idleState);
        }

        if (isAnimEnded)
        {
            StateMachine.ChangeState(StateMachine.jumpState);
        }
    }
    
    public override void AnimationEndTrigger()
    {
        base.AnimationEndTrigger();
        isAnimEnded = true;
    }
}