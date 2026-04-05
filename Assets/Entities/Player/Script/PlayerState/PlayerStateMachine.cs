public class PlayerStateMachine : StateMachine
{
    public new PlayerState CurrentState => (PlayerState)base.CurrentState;
    public new PlayerState PreviousState => (PlayerState)base.PreviousState;

    public PlayerIdleState IdleState { get; private set; }
    public PlayerMoveState MoveState { get; private set; }
    public PlayerAttackState AttackState { get; private set; }
    public PlayerHitState HitState { get; private set; }
    
    public PlayerStateMachine(Player player) : base(player)
    {
        // 'this'는 StateMachine 자신을 의미하므로 상태 생성자에 넘겨주기 편합니다.
        IdleState = new PlayerIdleState(player, this);
        MoveState = new PlayerMoveState(player, this);
        AttackState = new PlayerAttackState(player, this);
        HitState = new PlayerHitState(player, this);
    }
}