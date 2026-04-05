using UnityEngine.InputSystem;

public class EnemyState : State
{
    protected Enemy Enemy => (Enemy)Entity;
    protected new EnemyStateMachine StateMachine => (EnemyStateMachine)base.StateMachine;
    
    protected EnemyState(Enemy enemy, EnemyStateMachine stateMachine) 
        : base(enemy, stateMachine) {}
}