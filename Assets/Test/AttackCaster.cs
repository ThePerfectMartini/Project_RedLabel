using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackCaster : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("이 캐릭터가 공격할 대상의 레이어 (예: Player 레이어)")]
    public LayerMask targetLayer;

    private AttackActionData currentAttackData;

    /// <summary>타격이 1회 이상 성공했을 때 발행되는 이벤트 (콤보 판정용)</summary>
    public event Action OnHitConfirmed;

    private float attackTriggerTime = -1f;
    private float gizmoBlueDuration = 0.2f;

    private bool    isFixedAttack;
    private Vector3 fixedWorldPosition;
    private bool    rangedHitConfirmed;

    /// <summary>현재 공격에서 이미 타격한 콜라이더 (단일 공격 다중 히트 방지)</summary>
    private readonly HashSet<Collider>           hitTargetsThisAttack = new HashSet<Collider>();
    private readonly Dictionary<Collider, float> targetLastHitTimes   = new Dictionary<Collider, float>();

    private Coroutine continuousCastCoroutine;

    /// <summary>
    /// 캐릭터가 바라보는 가로축 실제 방향 부호 (오른쪽 = +1, 왼쪽 = -1)
    /// </summary>
    private float FacingSignX => Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, 180f)) < 90f ? 1f : -1f;

    /// <summary>
    /// 캐릭터가 바라보는 실제 방향을 반영하여 공격 오프셋의 X축을 정면 기준으로 변환합니다.
    /// </summary>
    private Vector3 GetOrientedOffset(Vector3 offset)
    {
        return new Vector3(offset.x * FacingSignX, offset.y, offset.z);
    }

    /// <summary>
    /// 최근 공격이 실행되어 판정이 유효한 상태(기즈모 강조 시간 내)인지 여부
    /// </summary>
    public bool IsGizmoHighlighted
    {
        get
        {
            if (!Application.isPlaying) return false;
            if (currentAttackData == null) return false;

            if (currentAttackData.isContinuousAttack)
                return continuousCastCoroutine != null;

            return Time.time - attackTriggerTime <= gizmoBlueDuration;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 외부 API
    // ═══════════════════════════════════════════════════════════

    public void StopContinuousCast()
    {
        if (continuousCastCoroutine != null)
        {
            StopCoroutine(continuousCastCoroutine);
            continuousCastCoroutine = null;
        }
        hitTargetsThisAttack.Clear();
        targetLastHitTimes.Clear();
    }

    public void SetAttackData(AttackActionData data, bool isFixed = false, Vector3 fixedPos = default)
    {
        currentAttackData   = data;
        isFixedAttack       = isFixed;
        fixedWorldPosition  = fixedPos;
        rangedHitConfirmed  = false;
    }

    /// <summary>투사체가 타겟에 명중했을 때 호출. 첫 명중에만 콤보 이벤트를 발동합니다.</summary>
    public void NotifyRangedHit()
    {
        if (rangedHitConfirmed) return;
        rangedHitConfirmed = true;
        OnHitConfirmed?.Invoke();
    }

    public void CastDamage()
    {
        if (currentAttackData == null) return;
        attackTriggerTime = Time.time;

        StopContinuousCast();

        if (currentAttackData is RangedAttackActionData rangedData)
        {
            if (rangedData.projectilePrefab != null)
                StartCoroutine(FireRangedProjectiles(rangedData));
            return;
        }

        if (currentAttackData.isContinuousAttack)
            continuousCastCoroutine = StartCoroutine(ContinuousCastRoutine());
        else
            PerformOverlapCast(false);
    }

    // ═══════════════════════════════════════════════════════════
    // 오버랩 캐스트 (단일 + 지속 통합)
    // ═══════════════════════════════════════════════════════════

    /// <param name="isContinuous">지속 타격 모드 여부 (쿨다운 체크 여부를 결정)</param>
    private void PerformOverlapCast(bool isContinuous)
    {
        Vector3 centerPoint = isFixedAttack
            ? fixedWorldPosition
            : transform.position + GetOrientedOffset(currentAttackData.attackOffset);

        Collider[] hits = CastByShape(centerPoint);

        bool anyHit = false;
        foreach (Collider hitCol in hits)
        {
            if (hitCol.transform.IsChildOf(transform) || hitCol.gameObject == gameObject) continue;

            // ── 히트 쿨다운 체크 ──
            if (isContinuous)
            {
                float cooldown = currentAttackData.hitTickCooldown;
                if (cooldown <= 0f)
                {
                    if (hitTargetsThisAttack.Contains(hitCol)) continue;
                }
                else
                {
                    if (targetLastHitTimes.TryGetValue(hitCol, out float lastHit)
                        && Time.time - lastHit < cooldown) continue;
                }
                targetLastHitTimes[hitCol] = Time.time;
            }
            else
            {
                if (hitTargetsThisAttack.Contains(hitCol)) continue;
            }

            hitTargetsThisAttack.Add(hitCol);

            IHittable hittable = hitCol.GetComponentInParent<IHittable>();
            if (hittable == null)
            {
                Debug.Log($"[AttackCaster] {hitCol.name} 타격 (IHittable 미구현 — 폴백)");
                continue;
            }

            Vector3       hitDir   = (hitCol.transform.position - transform.position).normalized;
            Vector3       hitPoint = hitCol.ClosestPoint(transform.position);
            CombatHitData hitData  = CombatHitData.Create(currentAttackData, hitPoint, hitDir, transform);

            bool applied = hittable.OnHit(hitData);
            if (applied)
            {
                anyHit = true;
                CombatEventBus.Instance.RaiseHitDealt(hitData);
            }
        }

        if (anyHit)
            OnHitConfirmed?.Invoke();
    }

    private IEnumerator ContinuousCastRoutine()
    {
        float elapsed  = 0f;
        float duration = currentAttackData.continuousDuration;

        while (elapsed < duration)
        {
            PerformOverlapCast(true);
            yield return null;
            elapsed += Time.deltaTime;
        }

        continuousCastCoroutine = null;
    }

    // ═══════════════════════════════════════════════════════════
    // 형상별 오버랩 캐스트 헬퍼
    // ═══════════════════════════════════════════════════════════

    private Collider[] CastByShape(Vector3 center)
    {
        switch (currentAttackData.attackShape)
        {
            case AttackShape.Sphere:
                return Physics.OverlapSphere(center, currentAttackData.attackRadius, targetLayer);

            case AttackShape.Box:
            {
                Quaternion boxRot = isFixedAttack ? Quaternion.identity : transform.rotation;
                return Physics.OverlapBox(center, currentAttackData.attackHitBoxSize * 0.5f, boxRot, targetLayer);
            }

            case AttackShape.Cylinder:
            {
                float offset    = Mathf.Max(0f, currentAttackData.attackHeight * 0.5f - currentAttackData.attackRadius);
                Vector3 capTop  = center + Vector3.up * offset;
                Vector3 capBot  = center - Vector3.up * offset;
                return Physics.OverlapCapsule(capBot, capTop, currentAttackData.attackRadius, targetLayer);
            }

            default:
                return Array.Empty<Collider>();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 원거리 투사체 발사
    // ═══════════════════════════════════════════════════════════

    private IEnumerator FireRangedProjectiles(RangedAttackActionData data)
    {
        int count = Mathf.Max(1, data.projectileCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = transform.position + GetOrientedOffset(data.attackOffset);
            CapsuleController controller = GetComponent<CapsuleController>();
            Vector3 shootDir = controller != null ? controller.GetWorldDirectionFromEnum(data.moveDirection8) : transform.forward;

            // 매 발사 시점마다 최신 타겟 위치를 갱신
            if (data.targetType == TargetType.TrackObject && !string.IsNullOrEmpty(data.targetTag))
            {
                // CombatTargetRegistry로 FindWithTag 대체
                Transform target = CombatTargetRegistry.GetFirst(data.targetTag);
                if (target)
                {
                    Vector3 targetPos = target.position;

                    if (data.snapToPlayerXAxis)
                    {
                        float xSign = Mathf.Sign(targetPos.x - spawnPos.x);
                        if (xSign == 0f) xSign = transform.right.x >= 0f ? 1f : -1f;
                        shootDir = new Vector3(xSign, 0f, 0f);
                    }
                    else
                    {
                        shootDir = (targetPos - spawnPos).normalized;
                    }
                }
            }
            else if (data.targetType == TargetType.SpecificPosition)
            {
                shootDir = (data.targetPosition - spawnPos).normalized;
            }
            else if (isFixedAttack)
            {
                shootDir = (fixedWorldPosition - spawnPos).normalized;
            }

            if (shootDir == Vector3.zero) shootDir = transform.forward;

            GameObject projObj = Instantiate(data.projectilePrefab, spawnPos, Quaternion.identity);
            Projectile proj    = projObj.GetComponent<Projectile>() ?? projObj.AddComponent<Projectile>();
            proj.Initialize(data, shootDir, targetLayer, gameObject);

            if (i < count - 1 && data.projectileInterval > 0f)
                yield return new WaitForSeconds(data.projectileInterval);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 기즈모
    // ═══════════════════════════════════════════════════════════


}