using UnityEngine;
// 유니티 - 스테이트머신 중계기 역할 함
public class Enemy : Entity
{
    #region Components & State Machine
    public EnemyStateMachine stateMachine { get; private set; }
    public MovementController movement { get; private set; }
    public EnemyAnimationController enemyAnimationController { get; private set; }
    public CombatController combatController { get; private set; }

    #endregion

    protected override void Awake()
    {
        base.Awake();
        stateMachine = new EnemyStateMachine(this);
        
        movement = GetComponent<MovementController>();
        
        enemyAnimationController = GetComponent<EnemyAnimationController>();
        
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