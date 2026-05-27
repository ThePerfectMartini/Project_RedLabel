/// <summary>
/// 코루틴 기반 액션 시퀀스의 중단을 제어하는 경량 토큰.
/// PhaseRunner가 시퀀스 실행 전에 생성하고, ActionState / CapsuleController 의
/// 코루틴 루프 내에서 IsInterrupted 를 확인해 즉시 탈출합니다.
/// 외부에서 Interrupt() 를 호출하면 현재 실행 중인 모든 관련 코루틴이 다음
/// yield 지점에서 안전하게 종료됩니다.
/// </summary>
public class InterruptToken
{
    /// <summary>중단이 요청되었는지 여부</summary>
    public bool IsInterrupted { get; private set; }

    /// <summary>중단 사유 (디버그 및 로직 분기용)</summary>
    public InterruptReason Reason { get; private set; }

    /// <summary>
    /// 중단을 요청합니다. 이미 중단된 토큰에 다시 호출해도 무해합니다.
    /// </summary>
    public void Interrupt(InterruptReason reason = InterruptReason.External)
    {
        IsInterrupted = true;
        Reason = reason;
    }

    /// <summary>
    /// 토큰을 재사용하기 위해 초기화합니다.
    /// PhaseRunner가 새 시퀀스를 시작하기 전에 호출합니다.
    /// </summary>
    public void Reset()
    {
        IsInterrupted = false;
        Reason = InterruptReason.None;
    }
}

/// <summary>
/// 시퀀스 중단의 원인을 나타내는 열거형.
/// </summary>
public enum InterruptReason
{
    None,
    /// <summary>외부 스크립트가 직접 호출한 중단</summary>
    External,
    /// <summary>그로기 상태 진입으로 인한 중단</summary>
    Groggy,
    /// <summary>페이즈 전환으로 인한 중단</summary>
    PhaseTransition,
    /// <summary>사망으로 인한 중단</summary>
    Death,
    /// <summary>피격 경직으로 인한 중단</summary>
    Stagger,
}
