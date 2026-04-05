using UnityEngine.InputSystem;

public class PlayerState : State
{
    protected Player Player => (Player)Entity;
    protected new PlayerStateMachine StateMachine => (PlayerStateMachine)base.StateMachine;
    
    protected PlayerState(Player player, PlayerStateMachine stateMachine) 
        : base(player, stateMachine) {}
}
