using UnityEngine;
using System.Collections.Generic;

public class PlayerDashAttackState : PlayerState
{
    private bool isAnimEnded;
    public PlayerDashAttackState(Player player, PlayerStateMachine stateMachine, string animName) 
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
        
        Player.playerAnimationController.PlayAnimation(Animator.StringToHash("DashAttack"));
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();
        
        if (!isAnimEnded) return;
        
        Player.stateMachine.ChangeState(StateMachine.idleState);
    }

    public override void AnimationEndTrigger()
    {
        base.AnimationEndTrigger();
        isAnimEnded = true;
    }
}