using UnityEngine;

public class PlayerHitState : PlayerState
{
    public PlayerHitState(Player player, PlayerStateMachine stateMachine)
        : base(player, stateMachine)
    {}

    public override void Enter()
    {
        base.Enter();
        
        Player.animationController.Play("Hit");
        Player.animationController.OnAnimEnded += HitEnded;
    }
    
    private void HitEnded()
    {
        Player.StateMachine.ChangeState(StateMachine.IdleState);
    }

    
    public override void Exit()
    {
        base.Exit();
        Player.animationController.OnAnimEnded -= HitEnded;
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

    }
}