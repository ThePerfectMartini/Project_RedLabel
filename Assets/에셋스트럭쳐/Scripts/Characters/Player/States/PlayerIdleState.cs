using UnityEngine;

public class PlayerIdleState : PlayerState
{
    public PlayerIdleState(Player player, PlayerStateMachine stateMachine, string animName)
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
        Player.movement.StopImmediately();
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

        // 1. 이동 입력이 있으면 MoveState로
        if (Player.inputHandler.MoveInput != Vector2.zero)
        {
            StateMachine.ChangeState(StateMachine.moveState);
        }

        // 2. 점프 (땅 체크를 Movement 컴포넌트에게 물어봄)
        if (Player.inputHandler.JumpTriggered && Player.movement.CheckIfGrounded())
        {
            StateMachine.ChangeState(StateMachine.jumpState);
        }

        // 3. 달리기
        if (Player.inputHandler.runTriggered)
        {
            StateMachine.ChangeState(StateMachine.runState);
        }
        
        // 4. 공격
        if (Player.inputHandler.AttackTriggered)
        {
            StateMachine.ChangeState(StateMachine.attackState);
        }
    }
}