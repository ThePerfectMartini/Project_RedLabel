public abstract class StateMachine
{
    public State CurrentState { get; protected set; }
    public State PreviousState { get; protected set; }
    
    protected StateMachine(Entity entity)
    {
        // 플레이어 스테이트에서 플레이어를 조작할수 있도록
        // 플레이어 컨트롤러를 매개변수로 받아 생성하도록 함
        // 이후 스테이트들을 추가해 할당할때 player가 들어가도록 할 것
    }

    public virtual void Initialize(State startingState)
    {
        CurrentState = startingState;
        CurrentState.Enter();
        PreviousState = null;
    }

    public virtual void ChangeState(State newState)
    {
        CurrentState.Exit();
        PreviousState = CurrentState;
        CurrentState = newState;
        CurrentState.Enter();
    }
}