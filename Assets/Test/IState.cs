// 모든 상태가 구현해야 하는 인터페이스 (또는 추상 클래스)

using UnityEngine;

public interface IState
{
    void Enter();
    void Update();
    void PhysicsUpdate();
    void Exit();
}

// FSM의 심장 역할 (상태 전환 및 실행)
public abstract class BaseStateMachine : MonoBehaviour
{
    protected IState currentState;

    public void ChangeState(IState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }

    protected virtual void Update()
    {
        currentState?.Update();
    }

    protected virtual void FixedUpdate()
    {
        currentState?.PhysicsUpdate();
    }
}