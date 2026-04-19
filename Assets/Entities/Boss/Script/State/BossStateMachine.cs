public class BossStateMachine : StateMachine
{
    public new BossState CurrentState => (BossState)base.CurrentState;
    public new BossState PreviousState => (BossState)base.PreviousState;

    public BossIdleState IdleState { get; private set; }
    public BossHitState HitState { get; private set; }
    public BossMoveState MoveState { get; private set; }
    public BossAttackState AttackState { get; private set; }
    //public BossChaseState  ChaseState { get; private set; }
    
    public BossStateMachine(Boss Boss) : base(Boss)
    {
        // 'this'는 StateMachine 자신을 의미하므로 상태 생성자에 넘겨주기 편합니다.
        IdleState = new BossIdleState(Boss, this);
        HitState = new BossHitState(Boss, this);
        MoveState = new BossMoveState(Boss, this);
        AttackState = new BossAttackState(Boss, this);
        //ChaseState = new BossChaseState(Boss, this);
    }
}