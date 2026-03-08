public class EnemyStateMachine : StateMachine
{
    public EnemyIdleState idleState { get; private set; }
    public EnemyMoveState moveState { get; private set; }
    public EnemyAttackState attackState { get; private set; }
    
    public EnemyStateMachine(Enemy enemy) : base(enemy)
    {
        // 'this'는 StateMachine 자신을 의미하므로 상태 생성자에 넘겨주기 편합니다.
        idleState = new EnemyIdleState(enemy, this, "Idle");
        moveState = new EnemyMoveState(enemy, this, "Move");
        attackState = new EnemyAttackState(enemy, this, "Attack");
    }
}