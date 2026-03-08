using UnityEngine;
using System.Collections.Generic;

public class PlayerAttackState : PlayerState
{
    private int currentAttack;
    List<int> attackAnims = new ();
    private bool isAnimEnded;
    public PlayerAttackState(Player player, PlayerStateMachine stateMachine, string animName) 
        : base(player, stateMachine, animName)
    {
        this.Player = player;
        this.StateMachine = stateMachine;
        Debug.Log($"{animName}{currentAttack}");
        attackAnims.Add(Animator.StringToHash($"{animName}{0}"));
        attackAnims.Add(Animator.StringToHash($"{animName}{1}"));
        attackAnims.Add(Animator.StringToHash($"{animName}{2}"));
    }

    public override void Enter()
    {
        base.Enter();
        
        currentAttack = 0;
        isAnimEnded = false;
        
        Player.inputHandler.UseAttackInput();
        
        Player.playerAnimationController.PlayAnimation(attackAnims[0]);
        
        Player.movement.StopImmediately();
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();
        
        if (!isAnimEnded) return;
        
        if (Player.inputHandler.AttackTriggered && currentAttack < attackAnims.Count - 1)
        {
            currentAttack++;
            isAnimEnded = false;
            
            Player.inputHandler.UseAttackInput();
            Player.playerAnimationController.PlayAnimation(attackAnims[currentAttack]);
        }
        else
        {
            Player.inputHandler.UseAttackInput();
            StateMachine.ChangeState(StateMachine.idleState);
        }
    }

    public override void AnimationEndTrigger()
    {
        base.AnimationEndTrigger();
        isAnimEnded = true;
    }
}