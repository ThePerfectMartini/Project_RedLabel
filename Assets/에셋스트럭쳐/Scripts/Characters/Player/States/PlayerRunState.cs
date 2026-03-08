using UnityEngine;

public class PlayerRunState : PlayerState
{
    public PlayerRunState(Player player, PlayerStateMachine stateMachine, string animName)
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
        Player.inputHandler.UseRunInput();
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

        // [변경점 1] FacingDirection 가져오기 (Player -> Player.Movement)
        // 1. 입력값
        Vector2 input = Player.inputHandler.MoveInput;
        
        // 2. [정지 조건 1] 입력이 아예 없으면 -> Idle
        if (input == Vector2.zero)
        {
            StateMachine.ChangeState(StateMachine.idleState);
            return;
        }

        // 3. [정지 조건 2] 반대 방향 키 입력 시 -> Idle (브레이크)
        // 설명: X축 입력이 있고(좌우 이동 중), 그 입력 방향이 현재 캐릭터가 보는 방향과 다르다면
        if (input.x != 0 && (int)Mathf.Sign(input.x) != Player.movement.FacingDirection)
        {
            StateMachine.ChangeState(StateMachine.idleState);
            return;
        }

        // 4. 달리기 이동 실행
        // MovementController에게 'runSpeed'로 이동하라고 명령
        Player.movement.Move(input, Player.characterData.runSpeed);

        // 5. [점프] 달리기 점프
        if (Player.inputHandler.JumpTriggered && Player.movement.CheckIfGrounded())
        {
            StateMachine.ChangeState(StateMachine.jumpState);
        }
        
        // [공격]
        if (Player.inputHandler.AttackTriggered)
        {
            StateMachine.ChangeState(StateMachine.dashAttackState);
        }
    }
}