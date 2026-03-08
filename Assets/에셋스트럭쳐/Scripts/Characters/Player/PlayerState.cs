public class PlayerState : State
{
    protected Player Player;
    protected PlayerStateMachine StateMachine;
    
    // 실행할 애니메이션의 상태(State) 이름 해시값
    protected int AnimHash;
    
    protected PlayerState(Player player, PlayerStateMachine stateMachine, string animName) 
        : base(player, stateMachine) {}
}