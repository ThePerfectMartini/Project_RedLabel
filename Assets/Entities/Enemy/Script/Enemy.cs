using UnityEngine;

public class Enemy : Entity
{
    // 전용 스크립트
    public EnemyStateMachine StateMachine { get; private set; }
    public EnemyCombatController combatController;
    
    public EnemyAI aiController; // 추가됨
    
    // 범용 스크립트
    public MovementModule Movement;
    public AnimationController animationController;
    
    protected override void Awake()
    {
        base.Awake();
        StateMachine = new EnemyStateMachine(this);
        Movement = new MovementModule(GetComponent<Rigidbody>(), transform);
        
        animationController = GetComponentInChildren<AnimationController>();
        combatController = GetComponent<EnemyCombatController>();
        
        aiController = GetComponent<EnemyAI>();
    }
    
    protected override void Start()
    {
        base.Start();
        StateMachine.Initialize(StateMachine.IdleState);
    }

    private void Update()
    {
        StateMachine.CurrentState.LogicUpdate();
    }

    private void FixedUpdate()
    {
        StateMachine.CurrentState.PhysicsUpdate();
    }
    
    public override void TakeDamage(HitData hitData)
    {
        base.TakeDamage(hitData);
        
        if (StateMachine.CurrentState == StateMachine.HitState)
        {
            EnemyHitState hitState = (EnemyHitState)StateMachine.CurrentState;
            hitState.AnimationReset();
        } 
        else if (StateMachine.CurrentState != StateMachine.AttackState)
        {
            StateMachine.ChangeState(StateMachine.HitState);
        }
        Debug.Log($"[{gameObject.name}] 피격! 받은 데미지: {hitData.damage} / 남은 체력: {currentHealth}");
    }

    protected override void Die()
    {
        Debug.Log($"[{gameObject.name}] 체력이 모두 소진되어 사망했습니다!");
        
        // 사망 시 미끄러지지 않도록 정지
        if (Movement != null) Movement.StopImmediately();
        
        base.Die(); 
    }
}