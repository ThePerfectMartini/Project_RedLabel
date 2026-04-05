using UnityEngine;

public class PlayerAttackState : PlayerState
{
    private int _comboStep = 0;
    private bool _isNextAttackBuffered = false;

    public PlayerAttackState(Player player, PlayerStateMachine stateMachine)
        : base(player, stateMachine)
    {}

    public override void Enter()
    {
        base.Enter();
        
        Player.movement.StopImmediately();

        // 상태 진입 시 항상 1타(인덱스 0)부터 시작하고, 선입력을 초기화합니다.
        _comboStep = 0; 
        _isNextAttackBuffered = false;

        // 애니메이션 이벤트 및 입력 이벤트 구독
        Player.animationController.OnAnimEnded += AttackEnded;
        Player.animationController.OnAttackHitCheck += TriggerHitCheck;
        Player.InputHandler.OnAttackEvent += HandleAttackInput; // 연속 공격 선입력 받기

        PlayComboAnimation();
    }

    public override void Exit()
    {
        // 상태를 빠져나갈 때 이벤트 구독 해제 (메모리 누수 및 오작동 방지)
        Player.animationController.OnAnimEnded -= AttackEnded;
        Player.animationController.OnAttackHitCheck -= TriggerHitCheck;
        Player.InputHandler.OnAttackEvent -= HandleAttackInput;
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();
    }

    // 공격 키가 눌렸을 때 호출되는 메서드
    private void HandleAttackInput()
    {
        // 공격 애니메이션 도중에 키가 눌리면 다음 타수를 예약합니다.
        _isNextAttackBuffered = true;
    }

    // 현재 콤보에 맞는 애니메이션 재생
    private void PlayComboAnimation()
    {
        // PlayerCombatController가 들고 있는 ScriptableObject 배열에서 애니메이션 이름을 가져옵니다.
        string animName = Player.combatController.comboAttacks[_comboStep].animationName;
        Player.animationController.Play(animName);
        Player.movement.AttackThrust(Player.combatController.comboAttacks[_comboStep].thrustPower);
    }

    // 애니메이션 이벤트에서 데미지 판정 프레임에 도달했을 때 호출
    private void TriggerHitCheck()
    {
        // 현재 몇 타째인지 전투 컨트롤러에게 넘겨주어 알맞은 범위와 데미지로 판정하게 합니다.
        Player.combatController.PerformHitCheck(_comboStep);
    }

    // 애니메이션이 완전히 끝났을 때 호출
    private void AttackEnded()
    {
        int maxCombo = Player.combatController.comboAttacks.Length;

        // 다음 공격이 예약되어 있고, 아직 배열의 끝(막타)에 도달하지 않았다면?
        if (_isNextAttackBuffered && _comboStep < maxCombo - 1)
        {
            _comboStep++;                  // 다음 타수로 이동
            _isNextAttackBuffered = false; // 선입력 플래그 초기화
            PlayComboAnimation();          // 다음 공격 실행!
        }
        else
        {
            // 예약된 공격이 없거나 이미 막타를 쳤다면 대기 상태로 돌아갑니다.
            Player.StateMachine.ChangeState(StateMachine.IdleState);
        }
    }
}