public class PlayerStateMachine : StateMachine
{
    public PlayerIdleState idleState { get; private set; }
    public PlayerMoveState moveState { get; private set; }
    public PlayerJumpState jumpState { get; private set; }
    public PlayerRunState runState { get; private set; }
    public PlayerAttackState attackState { get; private set; }
    public PlayerDashAttackState dashAttackState { get; private set; }
    public PlayerJumpAttackState jumpAttackState { get; private set; }
    
    public PlayerStateMachine(Player player) : base(player)
    {
        // 'this'는 StateMachine 자신을 의미하므로 상태 생성자에 넘겨주기 편합니다.
        idleState = new PlayerIdleState(player, this, "Idle");
        moveState = new PlayerMoveState(player, this, "Move");
        jumpState = new PlayerJumpState(player, this, "Jump");
        runState = new PlayerRunState(player, this, "Run");
        attackState = new PlayerAttackState(player, this, "Attack");
        dashAttackState = new PlayerDashAttackState(player, this, "DashAttack");
        jumpAttackState = new PlayerJumpAttackState(player, this, "JumpAttack");
    }
}