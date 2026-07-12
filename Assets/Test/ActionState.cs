using System.Collections;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════
// ActionState — 추상 기반 클래스
// ═══════════════════════════════════════════════════════════════

public abstract class ActionState
{
    protected CapsuleController controller;
    protected ActionData        actionData;
    protected InterruptToken    interruptToken;
    protected Transform         cachedTarget;

    /// <param name="token">시퀀스 중단 토큰 (null 허용)</param>
    /// <param name="cachedTarget">PhaseRunner가 미리 캐싱한 타겟 Transform (null 허용)</param>
    public ActionState(CapsuleController controller, ActionData actionData,
                       InterruptToken token = null, Transform cachedTarget = null)
    {
        this.controller    = controller;
        this.actionData    = actionData;
        this.interruptToken = token;
        this.cachedTarget  = cachedTarget;
    }

    public abstract IEnumerator Execute();

    /// <summary>중단 토큰이 발동됐는지 확인합니다. 코루틴 루프 내 매 프레임 호출하세요.</summary>
    protected bool ShouldInterrupt()
        => interruptToken != null && interruptToken.IsInterrupted;

    protected void PlayAnimation(bool forceRestart = false)
    {
        if (!actionData.playAnimation) return;
        if (controller.animController == null) return;
        if (string.IsNullOrEmpty(actionData.animationName)) return;

        if (forceRestart && controller.animController.animator != null)
            controller.animController.animator.Play(actionData.animationName, -1, 0f);
        else
            controller.animController.Play(actionData.animationName);
    }

    /// <summary>
    /// 태그로 타겟을 조회합니다. cachedTarget 우선 사용 후 Registry 폴백.
    /// FindWithTag는 사용하지 않습니다.
    /// </summary>
    protected Transform GetResolvedTarget(TargetType targetType, string targetTag)
    {
        if (targetType != TargetType.TrackObject && targetType != TargetType.ReturnToSpawn) return null;
        if (cachedTarget) return cachedTarget;
        if (targetType == TargetType.ReturnToSpawn) return null; // 복귀인데 타겟이 없으면 멈춤
        return CombatTargetRegistry.GetFirst(targetTag);
    }
}

// ═══════════════════════════════════════════════════════════════
// MoveState
// ═══════════════════════════════════════════════════════════════

public class MoveState : ActionState
{
    private readonly MoveActionData moveData;

    public MoveState(CapsuleController controller, MoveActionData data,
                     InterruptToken token = null, Transform cachedTarget = null)
        : base(controller, data, token, cachedTarget)
    {
        moveData = data;
    }

    public override IEnumerator Execute()
    {
        PlayAnimation();

        if (moveData.isTeleport)
        {
            if (moveData.targetType != TargetType.Direction)
            {
                bool isTracking = moveData.targetType != TargetType.SpecificPosition;
                Transform trackingTarget = isTracking
                    ? GetResolvedTarget(moveData.targetType, moveData.targetTag)
                    : null;

                controller.TeleportToTarget(trackingTarget, moveData.targetPosition, isTracking, moveData);
            }
            yield return null;
            yield break;
        }

        if (moveData.useJump)
            controller.Jump(moveData.jumpForce);

        if (moveData.targetType == TargetType.Direction)
        {
            // Forward/Backward는 캐릭터의 실제 회전을 반영하여 월드 방향을 구합니다.
            Vector3 dir = controller.GetWorldDirectionFromEnum(moveData.moveDirection8);
            yield return controller.StartCoroutine(
                controller.MoveInDirection(dir, moveData, interruptToken)
            );
        }
        else
        {
            bool isTracking = moveData.targetType != TargetType.SpecificPosition;
            Transform trackingTarget = isTracking
                ? GetResolvedTarget(moveData.targetType, moveData.targetTag)
                : null;

            if (isTracking && !trackingTarget)
            {
                Debug.LogWarning($"[MoveState] 태그 '{moveData.targetTag}' 타겟을 찾을 수 없습니다.");
                yield break;
            }

            yield return controller.StartCoroutine(
                controller.MoveToTarget(trackingTarget, moveData.targetPosition, isTracking, moveData, interruptToken)
            );
        }
    }


}

// ═══════════════════════════════════════════════════════════════
// WaitState
// ═══════════════════════════════════════════════════════════════

public class WaitState : ActionState
{
    private readonly WaitActionData waitData;

    public WaitState(CapsuleController controller, WaitActionData data,
                     InterruptToken token = null, Transform cachedTarget = null)
        : base(controller, data, token, cachedTarget)
    {
        waitData = data;
    }

    public override IEnumerator Execute()
    {
        PlayAnimation();

        float elapsed = 0f;
        while (elapsed < waitData.timeLimit)
        {
            if (ShouldInterrupt()) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// AttackState
// ═══════════════════════════════════════════════════════════════

public class AttackState : ActionState
{
    private readonly AttackActionData attackData;

    public AttackState(CapsuleController controller, AttackActionData data,
                       InterruptToken token = null, Transform cachedTarget = null)
        : base(controller, data, token, cachedTarget)
    {
        attackData = data;
    }

    public override IEnumerator Execute()
    {
        if (ShouldInterrupt()) yield break;

        AttackCaster caster = controller.GetComponent<AttackCaster>();
        try
        {
            if (caster)
            {
                Vector3 fixedPos = Vector3.zero;

                if (attackData.isFixedAttack)
                {
                    Transform target = GetResolvedTarget(attackData.targetType, attackData.targetTag);
                    if (target)
                        fixedPos = target.position + attackData.attackOffset;
                    else if (attackData.targetType == TargetType.SpecificPosition)
                        fixedPos = attackData.targetPosition + attackData.attackOffset;
                    else
                        fixedPos = controller.transform.position + attackData.attackOffset;

                    caster.SetAttackData(attackData, true, fixedPos);
                }
                else
                {
                    caster.SetAttackData(attackData, false, Vector3.zero);
                }

                if (attackData.castDamageOnStart)
                    caster.CastDamage();
            }

            // 공격 시 동시 점프(승룡권 등)
            if (attackData.useJumpInAttack)
                controller.Jump(attackData.attackJumpForce);

            PlayAnimation(true);

            float animLength = 0f;
            if (controller.animController != null && attackData.playAnimation)
                animLength = controller.animController.GetAnimationLength(attackData.animationName);

            float duration = attackData.isContinuousAttack
                ? Mathf.Max(animLength, attackData.continuousDuration)
                : animLength;

            if (duration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    if (ShouldInterrupt()) yield break;
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return null;
            }
        }
        finally
        {
            if (caster)
                caster.StopContinuousCast();
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// RangedAttackState
// ═══════════════════════════════════════════════════════════════

public class RangedAttackState : ActionState
{
    private readonly RangedAttackActionData rangedData;

    public RangedAttackState(CapsuleController controller, RangedAttackActionData data,
                              InterruptToken token = null, Transform cachedTarget = null)
        : base(controller, data, token, cachedTarget)
    {
        rangedData = data;
    }

    public override IEnumerator Execute()
    {
        if (ShouldInterrupt()) yield break;

        AttackCaster caster = controller.GetComponent<AttackCaster>();
        if (caster)
        {
            Transform target   = GetResolvedTarget(rangedData.targetType, rangedData.targetTag);
            bool      hasTarget = false;
            Vector3   targetPos = Vector3.zero;

            if (target)
            {
                targetPos = target.position;
                hasTarget = true;
            }
            else if (rangedData.targetType == TargetType.SpecificPosition)
            {
                targetPos = rangedData.targetPosition;
                hasTarget = true;
            }

            caster.SetAttackData(rangedData, hasTarget, targetPos);
        }

        PlayAnimation(true);

        float animLength = 0f;
        if (controller.animController != null && rangedData.playAnimation)
            animLength = controller.animController.GetAnimationLength(rangedData.animationName);

        float totalShootTime = rangedData.projectileCount > 1
            ? (rangedData.projectileCount - 1) * rangedData.projectileInterval
            : 0f;

        float waitTime = Mathf.Max(animLength, totalShootTime);
        if (waitTime > 0f)
        {
            float elapsed = 0f;
            while (elapsed < waitTime)
            {
                if (ShouldInterrupt()) yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return null;
        }
    }
}