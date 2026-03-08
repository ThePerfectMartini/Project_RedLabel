using UnityEngine;
// 유니티 - 스테이트머신 중계기 역할 함
public class Player : Entity
{
    #region Components & State Machine
    public PlayerInputHandler inputHandler { get; private set; }
    public PlayerStateMachine stateMachine { get; private set; }
    public MovementController movement { get; private set; }
    public PlayerAnimationController playerAnimationController { get; private set; }
    public CombatController combatController { get; private set; }

    #endregion

    protected override void Awake()
    {
        base.Awake();
        stateMachine = new PlayerStateMachine(this);

        inputHandler = GetComponent<PlayerInputHandler>();
        
        movement = GetComponent<MovementController>();
        
        playerAnimationController = GetComponent<PlayerAnimationController>();
        
        combatController = GetComponent<CombatController>();
    }

    private void Start()
    {
        stateMachine.Initialize(stateMachine.idleState);
    }

    private void Update()
    {
        stateMachine.CurrentState.LogicUpdate();
    }

    private void FixedUpdate()
    {
        stateMachine.CurrentState.PhysicsUpdate();
    }
    
    public void AnimationEnded()
    {
        stateMachine.CurrentState.AnimationEndTrigger();
    }
}