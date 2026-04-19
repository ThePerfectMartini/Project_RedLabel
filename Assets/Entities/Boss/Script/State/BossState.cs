using UnityEngine.InputSystem;

public class BossState : State
{
    protected Boss Boss => (Boss)Entity;
    protected new BossStateMachine StateMachine => (BossStateMachine)base.StateMachine;
    
    protected BossState(Boss Boss, BossStateMachine stateMachine) 
        : base(Boss, stateMachine) {}
}