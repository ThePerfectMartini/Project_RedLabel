using UnityEngine;

/// <summary>
/// IHittable 의 완전한 구현 컴포넌트.
/// 동일 오브젝트에 부착된 DodgeController, ParryController, GroggyController 와
/// 연동해 피해 처리 파이프라인을 수행합니다.
///
/// 피해 파이프라인 순서:
///   1. DodgeController.TryInvincibleBlock() — 회피 무적 프레임 체크
///   2. ParryController.TryParry()           — 패링/저스트 가드 체크
///   3. GroggyController.ProcessHit()        — 그로기 카운터 or 게이지 축적
///   4. 실제 데미지 및 넉백 적용
///   5. CombatEventBus.RaiseHitReceived()    — 피격 이벤트 발행
/// </summary>
[RequireComponent(typeof(Collider))]
public class TestHittableComponent : MonoBehaviour, IHittable, IHasHealth
{
    [Header("디버그")]
    [SerializeField] private bool logOnHit = true;

    [Header("간단 체력")]
    [SerializeField] private float maxHP = 100f;
    private float currentHP;

    /// <summary>IHasHealth 구현 — PhaseManager 연동용</summary>
    public float CurrentHealthRatio => maxHP > 0f ? currentHP / maxHP : 0f;

    // ── 전투 컴포넌트 참조 (선택적 — 없어도 기본 동작) ────────
    private DodgeController        dodgeController;
    private ParryController        parryController;
    private GroggyController       groggyController;
    private Rigidbody              rb;
    private PlayerAttackController playerAttackController;
    private PhaseRunner            phaseRunner;

    private void Awake()
    {
        currentHP              = maxHP;
        dodgeController        = GetComponent<DodgeController>();
        parryController        = GetComponent<ParryController>();
        groggyController       = GetComponent<GroggyController>();
        rb                     = GetComponent<Rigidbody>();
        playerAttackController = GetComponent<PlayerAttackController>();
        phaseRunner            = GetComponent<PhaseRunner>();
    }

    // ═══════════════════════════════════════════════════════════
    // IHittable 구현
    // ═══════════════════════════════════════════════════════════

    public bool OnHit(CombatHitData hitData)
    {
        // ── 0단계: 무적 상태 체크 (피격 판정 원천 무효화) ──
        if (IsInvincibleActive())
            return false;

        // ── 1단계: 회피 무적 프레임 ─────────────────────────────
        if (dodgeController != null && dodgeController.TryInvincibleBlock())
            return false; // 피해 차단

        // ── 2단계: 패링 / 저스트 가드 ──────────────────────────
        if (parryController != null)
        {
            bool wasJustGuard;
            if (parryController.TryParry(hitData, out wasJustGuard))
                return false; // 피해 차단
        }

        // ── 3단계: 그로기 처리 (카운터 or 게이지 축적) ──────────
        float finalDamage = hitData.damage;
        if (groggyController != null)
            finalDamage = groggyController.ProcessHit(hitData);

        // ── 4단계: 실제 피해 적용 ───────────────────────────────
        currentHP -= finalDamage;

        if (logOnHit)
        {
            string groggyTag = (groggyController != null && groggyController.IsGroggy)
                ? " <color=magenta>[카운터]</color>" : "";
            string reactionTag = hitData.hitReactionType == HitReactionType.Launched ? " <color=cyan>[런처]</color>" : "";
            Debug.Log($"[TestHittable] <color=red>{gameObject.name}</color> 피격!{groggyTag}{reactionTag}" +
                      $"  최종 데미지: {finalDamage:F1}" +
                      $"  남은 HP: {currentHP:F1}/{maxHP}" +
                      $"  경직: {hitData.hitStunDuration:F2}s");
        }

        // ── 슈퍼 아머 (경직 및 넉백 면역) 체크 ──
        bool isSuperArmor = IsSuperArmorActive();

        if (!isSuperArmor)
        {
            // ── 낙백: AddForce 대신 linearVelocity 직접 설정 (지면 마찰로 인한 속도 소실 방지) ──
            Vector3 knockVel = Vector3.zero;
            if (rb)
            {
                Vector3 dir = hitData.hitDirection;
                knockVel = new Vector3(
                    dir.x * hitData.knockbackForce.x,
                    hitData.knockbackForce.y,
                    dir.z * hitData.knockbackForce.z
                );
                rb.linearVelocity = knockVel;
            }

            // ── 피격 반응 애니메이션 코루틴 (hitReactionType 기반 분기) ──
            AnimationController animCtrl = GetComponentInChildren<AnimationController>();
            if (animCtrl)
            {
                StopAllCoroutines(); // 이전 피격 반응 중단 후 새 반응 시작
                switch (hitData.hitReactionType)
                {
                    case HitReactionType.Launched:
                        StartCoroutine(LaunchedReactionRoutine(animCtrl, hitData.hitStunDuration, knockVel));
                        break;
                    case HitReactionType.Normal:
                    default:
                        StartCoroutine(NormalHitRoutine(animCtrl, hitData.hitStunDuration, knockVel));
                        break;
                }
            }
        }
        else
        {
            if (logOnHit)
            {
                Debug.Log($"[TestHittable] {gameObject.name} 슈퍼 아머 활성화로 인해 경직 및 넉백 무시!");
            }
        }

        // ── 5단계: 피격 이벤트 발행 ────────────────────────────
        CombatEventBus.Instance.RaiseHitReceived(hitData);

        // ── 사망 판정 ───────────────────────────────────────────
        if (currentHP <= 0f)
        {
            currentHP = 0f;
            Debug.Log($"[TestHittable] {gameObject.name} 사망!");
            // 실제 게임에서는 StateMachine 사망 상태로 전환
        }

        return true; // 피해 적용 완료
    }

    // ═══════════════════════════════════════════════════════════
    // 피격 반응 코루틴
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 일반 경직: Hit 모션을 hitStunDuration 만큼 재생한 뒤 Idle로 복귀합니다.
    /// 낙백 속도를 코루틴 내부에서 점진적으로 lerp하여 지면 마찰 없이 화면 거리를 정확히 톹걱납니다.
    /// </summary>
    private System.Collections.IEnumerator NormalHitRoutine(AnimationController animCtrl, float stunDuration, Vector3 knockVel)
    {
        // 플레이어인 경우 이동 입력 잠금
        TestPlayerController playerCtrl = GetComponent<TestPlayerController>();
        playerCtrl?.LockInput(stunDuration);

        animCtrl.Play("Hit");

        // 경직 시간 동안 낙백 속도를 점진적으로 0으로 감소
        // (rb.linearVelocity를 매 FixedUpdate마다 덮어써서 PhysX 마찰을 무효화)
        float elapsed = 0f;
        Vector3 initialKnockH = new Vector3(knockVel.x, 0f, knockVel.z);

        while (elapsed < stunDuration)
        {
            if (rb)
            {
                float t = stunDuration > 0f ? elapsed / stunDuration : 1f;
                float curX = Mathf.Lerp(initialKnockH.x, 0f, t);
                float curZ = Mathf.Lerp(initialKnockH.z, 0f, t);
                rb.linearVelocity = new Vector3(curX, rb.linearVelocity.y, curZ);
            }
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (rb) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        animCtrl.Play("Idle");
    }

    /// <summary>
    /// 런쳃 반응: AirHit(체공) → Land(착지) → GetUp(일어서기) → Idle 순서로 재생합니다.
    /// ActionData.isLauncher == true 인 경우에만 실행됩니다.
    /// hitStunDuration은 체공 시간으로 사용됩니다.
    /// </summary>
    private System.Collections.IEnumerator LaunchedReactionRoutine(AnimationController animCtrl, float airtime, Vector3 knockVel)
    {
        // 플레이어인 경우 이동 입력 잠금 (정리 동작 전체 동안)
        float totalDuration = airtime + 0.3f + 0.5f; // airtime + land + getup 추정치
        TestPlayerController playerCtrl = GetComponent<TestPlayerController>();
        playerCtrl?.LockInput(totalDuration);

        // 1) 공중에 뜨는 모션
        animCtrl.Play("AirHit");

        // 체공 중 초반에 낙백 속도를 점진적으로 감소
        float elapsed = 0f;
        Vector3 initialKnockH = new Vector3(knockVel.x, 0f, knockVel.z);
        while (elapsed < airtime)
        {
            if (rb)
            {
                float t = airtime > 0f ? elapsed / airtime : 1f;
                float curX = Mathf.Lerp(initialKnockH.x, 0f, t);
                rb.linearVelocity = new Vector3(curX, rb.linearVelocity.y, rb.linearVelocity.z);
            }
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // 2) 착지 모션
        animCtrl.Play("Land");
        float landLength = animCtrl.GetAnimationLength("Land");
        yield return new WaitForSeconds(landLength > 0f ? landLength : 0.3f);

        // 3) 일어서는 모션
        animCtrl.Play("GetUp");
        float getUpLength = animCtrl.GetAnimationLength("GetUp");
        yield return new WaitForSeconds(getUpLength > 0f ? getUpLength : 0.5f);

        // 4) 대기 상태 복귀
        animCtrl.Play("Idle");
    }

    // ═══════════════════════════════════════════════════════════
    // 체력 직접 조작 (외부 테스트용)
    // ═══════════════════════════════════════════════════════════

    public void Heal(float amount)
    {
        currentHP = Mathf.Min(currentHP + amount, maxHP);
    }

    public void SetHP(float hp)
    {
        currentHP = Mathf.Clamp(hp, 0f, maxHP);
    }

    // ═══════════════════════════════════════════════════════════
    // 방어 상태 판단 헬퍼 메서드
    // ═══════════════════════════════════════════════════════════

    private bool IsInvincibleActive()
    {
        // 1. 플레이어 공격 액션 내 무적 체크
        if (playerAttackController && playerAttackController.IsAttacking)
        {
            var action = playerAttackController.CurrentExecutingAction;
            if (action != null && action.isInvincible)
                return true;
        }

        // 2. 적 패턴 액션 내 무적 체크
        if (phaseRunner)
        {
            var action = phaseRunner.CurrentExecutingAction;
            if (action != null && action.isInvincible)
                return true;
        }

        return false;
    }

    private bool IsSuperArmorActive()
    {
        // 1. 플레이어 공격 액션 내 슈퍼 아머 체크
        if (playerAttackController && playerAttackController.IsAttacking)
        {
            var action = playerAttackController.CurrentExecutingAction;
            if (action != null && action.isSuperArmor)
                return true;
        }

        // 2. 적 패턴 액션 내 슈퍼 아머 체크
        if (phaseRunner)
        {
            var action = phaseRunner.CurrentExecutingAction;
            if (action != null && action.isSuperArmor)
                return true;
        }

        return false;
    }
}
