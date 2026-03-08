using UnityEngine;

public class PlayerJumpState : PlayerState
{
    public bool IsRunJump { get; private set; }
    public PlayerJumpState(Player player, PlayerStateMachine stateMachine, string animName)
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
        
        if (StateMachine.PreviousState is PlayerRunState) IsRunJump = true;
        else IsRunJump = false;
        
        // 속도 확인 및 그라운드 체크
        if (Player.movement.GetCurrentVelocityY() < 0.01f && Player.movement.CheckIfGrounded())
        {
            Player.movement.Jump(Player.characterData.jumpForce);
        }
        
        Player.inputHandler.UseJumpInput();
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();
        
        // 공중에서 점프 키를 또 누르면? -> 즉시 삭제(무시)해버림!
        if (Player.inputHandler.JumpTriggered) Player.inputHandler.UseJumpInput();
        
        // 1. 입력값
        Vector2 input = Player.inputHandler.MoveInput;
        
        float currentAirSpeed = IsRunJump 
            ? Player.characterData.runSpeed 
            : Player.characterData.moveSpeed;
        
        // 2. 방향전환
        Player.movement.MoveAir(input, currentAirSpeed);
        
        // 4. 공격
        if (Player.inputHandler.AttackTriggered)
        {
            StateMachine.ChangeState(StateMachine.jumpAttackState);
        }

        
        // 속도 확인 및 그라운드 체크
        if (Player.movement.GetCurrentVelocityY() < 0.01f && Player.movement.CheckIfGrounded())
        {
            StateMachine.ChangeState(StateMachine.idleState);
        }
    }
}