public class EnemyStateMachine : StateMachine
{
    public new EnemyState CurrentState => (EnemyState)base.CurrentState;
    public new EnemyState PreviousState => (EnemyState)base.PreviousState;

    public EnemyIdleState IdleState { get; private set; }
    public EnemyHitState HitState { get; private set; }
    public EnemyMoveState MoveState { get; private set; }
    public EnemyAttackState AttackState { get; private set; }
    //public EnemyChaseState  ChaseState { get; private set; }
    
    public EnemyStateMachine(Enemy enemy) : base(enemy)
    {
        // 'this'는 StateMachine 자신을 의미하므로 상태 생성자에 넘겨주기 편합니다.
        IdleState = new EnemyIdleState(enemy, this);
        HitState = new EnemyHitState(enemy, this);
        MoveState = new EnemyMoveState(enemy, this);
        AttackState = new EnemyAttackState(enemy, this);
        //ChaseState = new EnemyChaseState(enemy, this);
    }
}