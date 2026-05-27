using System.Collections;
using UnityEngine;

/// <summary>
/// 패링(가드) 및 저스트 가드 시스템 컴포넌트.
///
/// 상태 흐름:
///   Idle
///   → [패링 입력] → Parrying (가드 윈도우 활성)
///       → 윈도우 시작 직후 justGuardWindow 이내 피격 → JustGuard 성공
///       → 이후 parryWindowDuration 이내 피격    → 일반 패링 성공
///       → 윈도우 종료 (피격 없음)               → 쿨다운 → Idle
///
/// IHittable.OnHit() 에서 TryParry() 를 호출하면:
///   - 패링 성공 시 false 반환 (피해 차단)
///   - 실패 시 true 반환 (피해 적용)
/// </summary>
public class ParryController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // 인스펙터 설정
    // ═══════════════════════════════════════════════════════════

    [Header("타이밍")]
    [Tooltip("패링 입력 후 가드 판정이 활성화되는 전체 윈도우 시간(초)")]
    [Range(0.1f, 1f)]
    public float parryWindowDuration = 0.4f;

    [Tooltip("윈도우 시작 직후 이 시간(초) 이내에 피격되면 저스트 가드로 판정")]
    [Range(0.05f, 0.3f)]
    public float justGuardWindow = 0.1f;

    [Tooltip("패링 성공 후 다음 패링 입력을 받을 수 없는 쿨다운 시간(초)")]
    [Range(0f, 2f)]
    public float parryCooldown = 0.6f;

    [Header("보상")]
    [Tooltip("일반 패링 성공 시 체력 회복량")]
    public float parryHealAmount = 0f;

    [Tooltip("저스트 가드 성공 시 체력 회복량")]
    public float justGuardHealAmount = 5f;

    [Tooltip("저스트 가드 성공 시 발동하는 슬로우 배율 (0.1 = 10% 속도)")]
    [Range(0.01f, 0.5f)]
    public float justGuardTimeScale = 0.1f;

    [Tooltip("저스트 가드 슬로우 지속 시간(초, 실제 시간 기준)")]
    public float justGuardSlowDuration = 0.3f;

    // ═══════════════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════════════

    private enum ParryState { Idle, Parrying, Cooldown }
    private ParryState state = ParryState.Idle;
    private float windowStartTime;
    private Coroutine parryRoutine;

    // ── 공개 상태 쿼리 ─────────────────────────────────────────
    public bool IsParrying   => state == ParryState.Parrying;
    public bool IsOnCooldown => state == ParryState.Cooldown;

    // ═══════════════════════════════════════════════════════════
    // 외부 API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 패링 입력이 들어왔을 때 TestPlayerController 에서 호출합니다.
    /// 쿨다운 중이거나 이미 패링 중이면 무시합니다.
    /// </summary>
    public void RequestParry()
    {
        if (state != ParryState.Idle) return;

        if (parryRoutine != null) StopCoroutine(parryRoutine);
        parryRoutine = StartCoroutine(ParryRoutine());
    }

    /// <summary>
    /// IHittable.OnHit() 내부에서 호출합니다.
    /// 패링이 성공하면 true(피해 차단), 실패하면 false(피해 통과)를 반환합니다.
    /// </summary>
    /// <param name="hitData">들어온 공격 데이터</param>
    /// <param name="wasJustGuard">저스트 가드 성공 여부를 out으로 반환</param>
    public bool TryParry(CombatHitData hitData, out bool wasJustGuard)
    {
        wasJustGuard = false;

        // 패링 불가능한 공격 (isUnblockable) 은 즉시 통과
        if (hitData.isUnblockable) return false;

        // 패링 대상이 아닌 공격 (canBeParried = false) 도 통과
        if (!hitData.canBeParried) return false;

        // 가드 윈도우 밖이면 통과
        if (state != ParryState.Parrying) return false;

        float elapsed = Time.time - windowStartTime;

        // 저스트 가드 판정 (윈도우 극초반)
        if (elapsed <= justGuardWindow)
        {
            wasJustGuard = true;
            OnJustGuard(hitData);
        }
        else
        {
            OnNormalParry(hitData);
        }

        // 패링 성공 → 즉시 쿨다운으로 전환
        if (parryRoutine != null) StopCoroutine(parryRoutine);
        parryRoutine = StartCoroutine(CooldownRoutine());

        return true; // 피해 차단
    }

    // ═══════════════════════════════════════════════════════════
    // 내부 처리
    // ═══════════════════════════════════════════════════════════

    private IEnumerator ParryRoutine()
    {
        state = ParryState.Parrying;
        windowStartTime = Time.time;

        // 애니메이션 트리거 (AnimationController 가 있으면 재생)
        AnimationController anim = GetComponentInChildren<AnimationController>();
        if (anim) anim.Play("Parry");

        yield return new WaitForSeconds(parryWindowDuration);

        // 윈도우 종료 → 피격 없이 끝나면 쿨다운
        if (state == ParryState.Parrying)
            parryRoutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        state = ParryState.Cooldown;
        yield return new WaitForSeconds(parryCooldown);
        state = ParryState.Idle;
    }

    private void OnNormalParry(CombatHitData hitData)
    {
        Debug.Log($"[ParryController] 패링 성공! 공격자: {hitData.attacker?.name}");
        CombatEventBus.Instance.RaiseParry(hitData);

        // 체력 회복 (IHasHealth 구현 시)
        // healAmount 는 실제 플레이어 HP 컴포넌트에 전달해야 하므로
        // 여기서는 이벤트만 발행하고 HP 컴포넌트가 구독하도록 설계
    }

    private void OnJustGuard(CombatHitData hitData)
    {
        Debug.Log($"[ParryController] 저스트 가드! 공격자: {hitData.attacker?.name}");
        CombatEventBus.Instance.RaiseParry(hitData);

        // 슬로우 연출 (Time.timeScale 사용)
        StartCoroutine(JustGuardSlowRoutine());
    }

    private IEnumerator JustGuardSlowRoutine()
    {
        float prevTimeScale = Time.timeScale;
        Time.timeScale = justGuardTimeScale;

        // unscaledTime 기준으로 실제 시간 대기
        float elapsed = 0f;
        while (elapsed < justGuardSlowDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = prevTimeScale;
    }
}
