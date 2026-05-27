using System.Collections;
using UnityEngine;

/// <summary>
/// CombatEventBus 의 전투 이벤트를 구독해 히트스톱(시간 정지) 연출을 수행하는 컴포넌트.
///
/// 씬에 하나만 배치하면 됩니다.
/// CombatHitData.hitStopDuration > 0 이면 그 값을 사용하고,
/// 0 이면 이벤트 종류별 기본값을 사용합니다.
///
/// 주의: Time.timeScale 을 직접 조작하므로 DodgeController / ParryController 의
///       저스트 슬로우와 겹칠 수 있습니다. 겹침 방지를 위해 큰 값이 우선합니다.
/// </summary>
public class HitStopHandler : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // 인스펙터 설정
    // ═══════════════════════════════════════════════════════════

    [Header("기본 히트스톱 시간 (초, 실제 시간 기준)")]
    [Tooltip("일반 타격 히트스톱 기본값")]
    public float defaultHitDuration   = 0.06f;

    [Tooltip("패링(저스트 가드) 히트스톱 기본값")]
    public float defaultParryDuration = 0.12f;

    [Tooltip("그로기 진입 히트스톱 기본값")]
    public float defaultGroggyDuration = 0.18f;

    [Header("히트스톱 타임스케일")]
    [Tooltip("히트스톱 중 Time.timeScale 값 (0 = 완전 정지)")]
    [Range(0f, 0.1f)]
    public float hitStopTimeScale = 0f;

    // ═══════════════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════════════

    private float requestedDuration;   // 현재 요청된 히트스톱 시간
    private bool  isHitStopped;
    private Coroutine hitStopRoutine;

    // ═══════════════════════════════════════════════════════════
    // 구독 관리
    // ═══════════════════════════════════════════════════════════

    private void OnEnable()
    {
        CombatEventBus.Instance.OnHitDealt    += HandleHitDealt;
        CombatEventBus.Instance.OnParrySuccess += HandleParry;
        CombatEventBus.Instance.OnGroggyEnter += HandleGroggy;
    }

    private void OnDisable()
    {
        // Instance null 체크 (씬 종료 시 싱글톤이 먼저 파괴될 수 있음)
        if (!CombatEventBus.Instance) return;
        CombatEventBus.Instance.OnHitDealt    -= HandleHitDealt;
        CombatEventBus.Instance.OnParrySuccess -= HandleParry;
        CombatEventBus.Instance.OnGroggyEnter -= HandleGroggy;
    }

    // ═══════════════════════════════════════════════════════════
    // 이벤트 핸들러
    // ═══════════════════════════════════════════════════════════

    private void HandleHitDealt(CombatHitData data)
    {
        float dur = data.hitStopDuration > 0f ? data.hitStopDuration : defaultHitDuration;
        RequestHitStop(dur);
    }

    private void HandleParry(CombatHitData data)
    {
        float dur = data.hitStopDuration > 0f ? data.hitStopDuration : defaultParryDuration;
        RequestHitStop(dur);
    }

    private void HandleGroggy(Transform _)
    {
        RequestHitStop(defaultGroggyDuration);
    }

    // ═══════════════════════════════════════════════════════════
    // 히트스톱 실행
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 히트스톱을 요청합니다. 이미 실행 중이면 더 긴 시간으로 교체합니다.
    /// </summary>
    public void RequestHitStop(float duration)
    {
        // 이미 히트스톱 중이고 새 요청이 더 짧으면 무시 (큰 값 우선)
        if (isHitStopped && duration <= requestedDuration) return;

        requestedDuration = duration;

        if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
        hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        isHitStopped = true;
        float prev = Time.timeScale;

        // 이미 저스트 슬로우 중이면 더 낮은 타임스케일만 적용
        Time.timeScale = Mathf.Min(prev, hitStopTimeScale);

        // unscaledTime 기준으로 duration 대기
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 히트스톱 종료: 이전 타임스케일로 복구
        // 단, 저스트 슬로우가 아직 진행 중일 수 있으므로 1.0 으로 강제하지 않음
        // → 저스트 슬로우 코루틴이 별도로 복구를 담당
        if (Mathf.Approximately(Time.timeScale, hitStopTimeScale))
            Time.timeScale = prev > hitStopTimeScale ? prev : 1f;

        isHitStopped = false;
        requestedDuration = 0f;
    }
}
