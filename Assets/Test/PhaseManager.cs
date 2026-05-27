using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적(보스/일반)의 멀티 페이즈 전환을 관리하는 컴포넌트.
/// PhaseRunner 와 연동해 특정 조건(체력 임계값, 시간, 외부 트리거)에 따라
/// PhaseSO 를 교체하고 연출 이벤트를 발행합니다.
///
/// 부착 대상: PhaseRunner 가 붙은 보스/적 오브젝트
/// </summary>
[RequireComponent(typeof(PhaseRunner))]
public class PhaseManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // 인스펙터 설정
    // ═══════════════════════════════════════════════════════════

    [Header("페이즈 목록 (위에서부터 순서대로)")]
    [Tooltip("순서대로 등록하세요. PhaseEntry.phase 가 전환될 PhaseSO 입니다.")]
    public List<PhaseManagerEntry> phases = new List<PhaseManagerEntry>();

    [Header("체력 참조")]
    [Tooltip("체력을 제공하는 컴포넌트. IHasHealth 인터페이스를 구현해야 합니다.")]
    public MonoBehaviour healthProvider;

    [Header("옵션")]
    [Tooltip("true 면 게임 시작 시 phases[0] 으로 자동 전환합니다.")]
    public bool autoStartFirstPhase = true;

    // ═══════════════════════════════════════════════════════════
    // 이벤트
    // ═══════════════════════════════════════════════════════════

    /// <summary>페이즈가 전환될 때 발행됩니다 (이전 인덱스, 새 인덱스).</summary>
    public event Action<int, int> OnPhaseChanged;

    // ═══════════════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════════════

    private PhaseRunner phaseRunner;
    private IHasHealth health;
    private int currentPhaseIndex = -1;
    private bool isTransitioning;

    // ═══════════════════════════════════════════════════════════
    // 초기화
    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        phaseRunner = GetComponent<PhaseRunner>();

        if (healthProvider != null)
            health = healthProvider as IHasHealth;

        if (health == null && healthProvider != null)
            Debug.LogWarning($"[PhaseManager] {healthProvider.GetType().Name} 은 IHasHealth 를 구현하지 않습니다.");
    }

    private void Start()
    {
        if (autoStartFirstPhase && phases.Count > 0)
            TransitionToPhase(0);
    }

    // ═══════════════════════════════════════════════════════════
    // 매 프레임 체력 기반 자동 전환 체크
    // ═══════════════════════════════════════════════════════════

    private void Update()
    {
        if (isTransitioning || health == null) return;

        float hpRatio = health.CurrentHealthRatio;

        for (int i = 0; i < phases.Count; i++)
        {
            var entry = phases[i];
            if (i <= currentPhaseIndex) continue; // 이미 지난 페이즈는 스킵
            if (entry.transitionCondition != PhaseTransitionCondition.HealthThreshold) continue;

            if (hpRatio <= entry.healthThreshold)
            {
                TransitionToPhase(i);
                return;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 외부 API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 지정된 인덱스의 페이즈로 즉시 전환합니다.
    /// 외부 이벤트(특수 공격 성공, 연출 완료 등)에서 호출하세요.
    /// </summary>
    public void TransitionToPhase(int index)
    {
        if (index < 0 || index >= phases.Count) return;
        if (index == currentPhaseIndex) return;
        if (isTransitioning) return;

        isTransitioning = true;

        int prevIndex = currentPhaseIndex;
        currentPhaseIndex = index;

        PhaseManagerEntry entry = phases[index];

        // 1) 현재 시퀀스 중단
        phaseRunner.InterruptCurrentSequence(InterruptReason.PhaseTransition);

        // 2) PhaseRunner 에 새 PhaseSO 주입
        phaseRunner.StopPhase();
        phaseRunner.SetPhase(entry.phase);

        // 3) 연출 이벤트 발행 (VFX / 컷씬 연동용)
        OnPhaseChanged?.Invoke(prevIndex, index);

        Debug.Log($"[PhaseManager] 페이즈 전환: {prevIndex} → {index} ({entry.phase?.name ?? "없음"})");

        // 4) 대기 후 새 페이즈 시작 (전환 연출 시간 확보)
        if (entry.transitionDelay > 0f)
            StartCoroutine(DelayedStart(entry.transitionDelay));
        else
            StartNewPhase();
    }

    /// <summary>
    /// 다음 페이즈로 순서대로 전환합니다.
    /// </summary>
    public void TransitionToNextPhase()
        => TransitionToPhase(currentPhaseIndex + 1);

    /// <summary>
    /// 현재 페이즈 인덱스를 반환합니다.
    /// </summary>
    public int CurrentPhaseIndex => currentPhaseIndex;

    // ═══════════════════════════════════════════════════════════
    // 내부 헬퍼
    // ═══════════════════════════════════════════════════════════

    private void StartNewPhase()
    {
        phaseRunner.StartPhase();
        isTransitioning = false;
    }

    private System.Collections.IEnumerator DelayedStart(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartNewPhase();
    }
}

// ───────────────────────────────────────────────────────────────
// 보조 데이터 타입
// ───────────────────────────────────────────────────────────────

/// <summary>
/// PhaseManager 에서 관리하는 단일 페이즈 엔트리.
/// </summary>
[Serializable]
public class PhaseManagerEntry
{
    [Tooltip("전환할 PhaseSO 에셋")]
    public PhaseSO phase;

    [Tooltip("이 페이즈로 전환될 조건")]
    public PhaseTransitionCondition transitionCondition = PhaseTransitionCondition.HealthThreshold;

    [Tooltip("체력 임계값 (0~1). 예: 0.5 = 체력 50% 이하일 때 전환")]
    [Range(0f, 1f)]
    public float healthThreshold = 0.5f;

    [Tooltip("전환 시작 후 새 페이즈가 실행되기까지의 대기 시간(초). 연출 시간 확보용.")]
    public float transitionDelay = 0f;
}

/// <summary>
/// 페이즈 전환 조건 열거형.
/// </summary>
public enum PhaseTransitionCondition
{
    /// <summary>체력이 지정된 임계값 이하로 떨어질 때</summary>
    HealthThreshold,
    /// <summary>외부에서 TransitionToPhase() 를 직접 호출할 때</summary>
    Manual,
    /// <summary>현재 페이즈의 반복이 모두 완료될 때 (PhaseSO.isInfiniteLoop = false)</summary>
    OnPhaseComplete,
}

/// <summary>
/// 체력 정보를 PhaseManager 에 제공하기 위한 인터페이스.
/// 보스/적 컴포넌트가 이 인터페이스를 구현하면 자동으로 체력 기반 전환이 활성화됩니다.
/// </summary>
public interface IHasHealth
{
    /// <summary>현재 체력을 0~1 사이의 비율로 반환합니다 (currentHP / maxHP).</summary>
    float CurrentHealthRatio { get; }
}
