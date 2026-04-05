// 모든 상태 객체의 최상위 부모 클래스
public abstract class State
{
    protected Entity Entity;
    protected StateMachine StateMachine;

    protected State(Entity entity, StateMachine stateMachine)
    {
        this.Entity = entity;
        this.StateMachine = stateMachine;
    }
    
    
    public virtual void Enter(){}
    public virtual void Exit(){}


    public virtual void LogicUpdate() {} // Update

    public virtual void PhysicsUpdate() {} // FixedUpdate
}