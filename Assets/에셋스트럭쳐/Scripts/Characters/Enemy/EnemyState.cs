public class EnemyState : State
{
    protected Enemy Enemy;
    protected EnemyStateMachine StateMachine;
    
    // 실행할 애니메이션의 상태(State) 이름 해시값
    protected int AnimHash;
    
    protected EnemyState(Enemy Enemy, EnemyStateMachine stateMachine, string animName) 
        : base(Enemy, stateMachine) {}
}