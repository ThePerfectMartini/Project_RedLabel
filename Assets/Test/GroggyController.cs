using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 그로기 게이지 관리 및 카운터 히트 시스템 컴포넌트.
///
/// 상태 흐름:
///   Normal (게이지 자연 감소 중)
///   → 게이지가 groggyThreshold 초과 → Groggy 진입
///       → PhaseRunner.InterruptCurrentSequence(Groggy) 호출
///       → 그로기 지속 (grOGgyDuration 초)
///           → 이 구간에 공격 받으면 → 카운터 히트 (데미지 × counterDamageMultiplier)
///       → 그로기 종료 → 게이지 초기화 → Normal
///
/// 적(PhaseRunner 보유) 전용이지만, 플레이어에도 부착 가능합니다.
/// </summary>
public class GroggyController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // 인스펙터 설정
    // ═══════════════════════════════════════════════════════════

    [Header("게이지")]
    [Tooltip("이 값을 초과하면 그로기 상태로 진입합니다.")]
    public float groggyThreshold = 100f;

    [Tooltip("초당 자연 감소량. 0 이면 감소 없음.")]
    public float decayPerSecond = 8f;

    [Header("그로기")]
    [Tooltip("그로기 지속 시간(초)")]
    [Range(0.5f, 5f)]
    public float groggyDuration = 2.5f;

    [Tooltip("그로기 종료 후 다시 그로기 상태가 되지 않는 내성 시간(초)")]
    [Range(0f, 5f)]
    public float groggyImmunityDuration = 3f;

    [Header("카운터 히트")]
    [Tooltip("그로기 중 받는 피해 배율. 2.0 = 2배 데미지")]
    [Range(1f, 5f)]
    public float counterDamageMultiplier = 2f;

    [Tooltip("그로기 카운터 히트 가능 최대 횟수. 이후엔 일반 피해로 처리.")]
    public int maxCounterHits = 3;

    // ═══════════════════════════════════════════════════════════
    // 이벤트
    // ═══════════════════════════════════════════════════════════

    /// <summary>그로기 진입 시 발행됩니다.</summary>
    public event Action OnGroggyEnter;

    /// <summary>그로기 종료 시 발행됩니다.</summary>
    public event Action OnGroggyExit;

    /// <summary>카운터 히트 발생 시 발행됩니다. 배율 적용된 최종 데미지를 전달합니다.</summary>
    public event Action<float> OnCounterHit;

    // ═══════════════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════════════

    private float currentGauge;
    private bool isGroggy;
    private bool isImmune;
    private int counterHitsRemaining;
    private Coroutine groggyRoutine;
    private PhaseRunner phaseRunner; // 있으면 적 시퀀스 중단용

    // ── 공개 상태 쿼리 ─────────────────────────────────────────
    public bool IsGroggy  => isGroggy;
    public bool IsImmune  => isImmune;
    public float GaugeRatio => groggyThreshold > 0f ? currentGauge / groggyThreshold : 0f;

    // ═══════════════════════════════════════════════════════════
    // 초기화
    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        phaseRunner = GetComponent<PhaseRunner>();
        currentGauge = 0f;
    }

    // ═══════════════════════════════════════════════════════════
    // 게이지 자연 감소
    // ═══════════════════════════════════════════════════════════

    private void Update()
    {
        if (isGroggy || isImmune) return;
        if (currentGauge <= 0f) return;

        currentGauge = Mathf.Max(0f, currentGauge - decayPerSecond * Time.deltaTime);
    }

    // ═══════════════════════════════════════════════════════════
    // 외부 API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// IHittable.OnHit() 에서 피격 후 호출합니다.
    /// 그로기 중이면 카운터 히트 처리 후 (배율 적용) 데미지를 반환합니다.
    /// 일반 상태면 게이지를 쌓고 임계값 초과 시 그로기를 발동합니다.
    /// </summary>
    /// <param name="hitData">원본 피격 데이터</param>
    /// <returns>최종 적용할 데미지 (카운터 시 배율 적용됨)</returns>
    public float ProcessHit(CombatHitData hitData)
    {
        if (isGroggy)
        {
            return ProcessCounterHit(hitData);
        }

        // 그로기 아님 → 게이지 누적
        if (!isImmune)
        {
            AccumulateGauge(hitData.staggerValue);
        }

        return hitData.damage;
    }

    /// <summary>
    /// 외부에서 직접 게이지를 누적합니다 (런처 히트 등 특수 케이스용).
    /// </summary>
    public void AccumulateGauge(float amount)
    {
        if (isGroggy || isImmune) return;

        currentGauge += amount;

        if (currentGauge >= groggyThreshold)
        {
            currentGauge = groggyThreshold;
            EnterGroggy();
        }
    }

    /// <summary>
    /// 그로기를 강제로 종료합니다 (부활 아이템, 특수 스킬 등).
    /// </summary>
    public void ForceExitGroggy()
    {
        if (!isGroggy) return;
        if (groggyRoutine != null) StopCoroutine(groggyRoutine);
        ExitGroggy();
    }

    // ═══════════════════════════════════════════════════════════
    // 그로기 상태 진입/종료
    // ═══════════════════════════════════════════════════════════

    private void EnterGroggy()
    {
        isGroggy = true;
        counterHitsRemaining = maxCounterHits;

        // PhaseRunner 가 있으면 현재 시퀀스 중단
        phaseRunner?.InterruptCurrentSequence(InterruptReason.Groggy);

        // 이동 정지 (Rigidbody 가 있으면)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb) rb.linearVelocity = Vector3.zero;

        // 애니메이션
        AnimationController anim = GetComponentInChildren<AnimationController>();
        if (anim) anim.Play("Groggy");

        // 이벤트 발행
        OnGroggyEnter?.Invoke();
        CombatEventBus.Instance.RaiseGroggy(transform);

        Debug.Log($"[GroggyController] {gameObject.name} 그로기 진입! 게이지: {currentGauge:F1}");

        if (groggyRoutine != null) StopCoroutine(groggyRoutine);
        groggyRoutine = StartCoroutine(GroggyRoutine());
    }

    private IEnumerator GroggyRoutine()
    {
        yield return new WaitForSeconds(groggyDuration);
        ExitGroggy();
    }

    private void ExitGroggy()
    {
        isGroggy = false;
        currentGauge = 0f;

        OnGroggyExit?.Invoke();
        Debug.Log($"[GroggyController] {gameObject.name} 그로기 종료");

        // 내성 시간 시작 (연속 그로기 방지)
        if (groggyImmunityDuration > 0f)
            StartCoroutine(ImmunityRoutine());
    }

    private IEnumerator ImmunityRoutine()
    {
        isImmune = true;
        yield return new WaitForSeconds(groggyImmunityDuration);
        isImmune = false;
    }

    // ═══════════════════════════════════════════════════════════
    // 카운터 히트 처리
    // ═══════════════════════════════════════════════════════════

    private float ProcessCounterHit(CombatHitData hitData)
    {
        if (counterHitsRemaining <= 0)
        {
            // 카운터 한도 초과 → 일반 데미지
            return hitData.damage;
        }

        counterHitsRemaining--;
        float counterDamage = hitData.damage * counterDamageMultiplier;

        OnCounterHit?.Invoke(counterDamage);
        CombatEventBus.Instance.RaiseCounter(transform);

        Debug.Log($"[GroggyController] 카운터 히트! " +
                  $"데미지: {hitData.damage} × {counterDamageMultiplier} = {counterDamage:F1} " +
                  $"(남은 카운터 횟수: {counterHitsRemaining})");

        return counterDamage;
    }

    // ═══════════════════════════════════════════════════════════
    // 디버그
    // ═══════════════════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        // 게이지 비율을 씬 뷰에 색상으로 표시
        if (!Application.isPlaying) return;
        float ratio = GaugeRatio;
        Gizmos.color = isGroggy
            ? Color.magenta
            : Color.Lerp(Color.green, Color.red, ratio);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.3f);
    }
}
