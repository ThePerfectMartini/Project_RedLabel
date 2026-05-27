using System.Collections;
using UnityEngine;

/// <summary>
/// 회피(구르기/대시) 및 저스트 회피 시스템 컴포넌트.
///
/// 상태 흐름:
///   Idle
///   → [회피 입력] → Startup (선딜)
///       → Active (무적 프레임 시작)
///           → 공격이 무적 프레임에 닿으면 → 저스트 회피 판정
///       → Recovery (후딜)
///   → Cooldown → Idle
///
/// 저스트 회피:
///   회피 Startup 구간에 공격이 들어오는 순간, 또는
///   Active 구간 초반(justDodgeWindowEnd 이내)에 닿으면 슬로우 발동
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DodgeController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // 인스펙터 설정
    // ═══════════════════════════════════════════════════════════

    [Header("타이밍 (초)")]
    [Tooltip("회피 입력 후 실제 이동이 시작되기 전 선딜 시간")]
    [Range(0f, 0.2f)]
    public float startupDuration = 0.05f;

    [Tooltip("무적 프레임(이동) 지속 시간")]
    [Range(0.1f, 0.8f)]
    public float activeDuration = 0.3f;

    [Tooltip("이동 종료 후 다음 행동을 할 수 없는 후딜 시간")]
    [Range(0f, 0.4f)]
    public float recoveryDuration = 0.15f;

    [Tooltip("다음 회피 입력을 받을 수 없는 전체 쿨다운 시간(선딜+무적+후딜 이후)")]
    [Range(0f, 1f)]
    public float dodgeCooldown = 0.5f;

    [Header("이동")]
    [Tooltip("회피 이동 속도 (초당 유닛)")]
    public float dodgeSpeed = 12f;

    [Header("저스트 회피")]
    [Tooltip("무적 구간 시작 후 이 시간(초) 이내에 공격이 닿으면 저스트 회피로 판정")]
    [Range(0.05f, 0.3f)]
    public float justDodgeWindowEnd = 0.08f;

    [Tooltip("저스트 회피 슬로우 배율")]
    [Range(0.01f, 0.5f)]
    public float justDodgeTimeScale = 0.08f;

    [Tooltip("저스트 회피 슬로우 지속 시간(실제 시간, 초)")]
    public float justDodgeSlowDuration = 0.4f;

    [Header("최대 회피 횟수")]
    [Tooltip("연속 회피 가능 횟수. 착지 시 자동으로 회복됩니다.")]
    [Range(1, 3)]
    public int maxDodgeCount = 1;

    // ═══════════════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════════════

    private enum DodgeState { Idle, Startup, Active, Recovery, Cooldown }
    private DodgeState state = DodgeState.Idle;

    private Rigidbody rb;
    private Vector3 dodgeDirection;
    private float activeStartTime;
    private int remainingDodges;
    private bool justDodgeTriggered;
    private Coroutine dodgeRoutine;

    // ── 공개 상태 쿼리 ─────────────────────────────────────────
    /// <summary>현재 무적 프레임(Active 구간)인지 여부. IHittable.OnHit() 에서 확인합니다.</summary>
    public bool IsInvincible => state == DodgeState.Active;
    public bool IsDodging    => state == DodgeState.Active || state == DodgeState.Startup;

    // ═══════════════════════════════════════════════════════════
    // 초기화
    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        remainingDodges = maxDodgeCount;
    }

    // ═══════════════════════════════════════════════════════════
    // 착지 감지 (점프 횟수처럼 회피 횟수 복구)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 착지 시 TestPlayerController 에서 호출해 회피 횟수를 복구합니다.
    /// </summary>
    public void OnLanded() => remainingDodges = maxDodgeCount;

    // ═══════════════════════════════════════════════════════════
    // 외부 API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 회피 입력이 들어왔을 때 호출합니다.
    /// moveInput 을 전달하면 해당 방향으로 회피하고, zero 면 뒤(반대 방향)로 회피합니다.
    /// </summary>
    public void RequestDodge(Vector2 moveInput)
    {
        if (state != DodgeState.Idle) return;
        if (remainingDodges <= 0) return;

        // 회피 방향 결정: 입력 방향 우선, 없으면 캐릭터 뒤쪽
        if (moveInput.sqrMagnitude > 0.1f)
            dodgeDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        else
            dodgeDirection = -transform.forward; // 뒤로 구르기

        remainingDodges--;
        justDodgeTriggered = false;

        if (dodgeRoutine != null) StopCoroutine(dodgeRoutine);
        dodgeRoutine = StartCoroutine(DodgeRoutine());
    }

    /// <summary>
    /// IHittable.OnHit() 내부에서 호출합니다.
    /// 무적 프레임 중이면 true(피해 차단), 아니면 false(피해 통과)를 반환합니다.
    /// </summary>
    public bool TryInvincibleBlock()
    {
        if (!IsInvincible) return false;

        // 저스트 회피 판정: 무적 시작 직후 justDodgeWindowEnd 이내
        float elapsed = Time.time - activeStartTime;
        if (elapsed <= justDodgeWindowEnd && !justDodgeTriggered)
        {
            justDodgeTriggered = true;
            StartCoroutine(JustDodgeSlowRoutine());
            CombatEventBus.Instance.RaiseDodge(transform);
            Debug.Log("[DodgeController] 저스트 회피!");
        }
        else
        {
            CombatEventBus.Instance.RaiseDodge(transform);
            Debug.Log("[DodgeController] 회피 성공 (무적 프레임)");
        }

        return true; // 피해 차단
    }

    // ═══════════════════════════════════════════════════════════
    // 코루틴
    // ═══════════════════════════════════════════════════════════

    private IEnumerator DodgeRoutine()
    {
        // ── Startup ──────────────────────────────────────────
        state = DodgeState.Startup;
        AnimationController anim = GetComponentInChildren<AnimationController>();
        if (anim) anim.Play("Dodge");

        yield return new WaitForSeconds(startupDuration);

        // ── Active (무적 프레임 + 이동) ───────────────────────
        state = DodgeState.Active;
        activeStartTime = Time.time;

        float elapsed = 0f;
        while (elapsed < activeDuration)
        {
            // 회피 이동 (Y속도 유지)
            rb.linearVelocity = new Vector3(
                dodgeDirection.x * dodgeSpeed,
                rb.linearVelocity.y,
                dodgeDirection.z * dodgeSpeed
            );
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // 이동 정지
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        // ── Recovery ──────────────────────────────────────────
        state = DodgeState.Recovery;
        yield return new WaitForSeconds(recoveryDuration);

        // ── Cooldown ──────────────────────────────────────────
        state = DodgeState.Cooldown;
        yield return new WaitForSeconds(dodgeCooldown);

        state = DodgeState.Idle;
    }

    private IEnumerator JustDodgeSlowRoutine()
    {
        float prevTimeScale = Time.timeScale;
        Time.timeScale = justDodgeTimeScale;

        float elapsed = 0f;
        while (elapsed < justDodgeSlowDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = prevTimeScale;
    }
}
