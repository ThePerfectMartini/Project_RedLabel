using System.Collections;
using UnityEngine;

public abstract class ActionState
{
    protected CapsuleController controller;
    protected ActionData actionData;
    protected InterruptToken interruptToken;
    protected Transform cachedTarget;

    /// <param name="token">시퀀스 중단 토큰 (null 허용 — 중단 기능 미사용 시)</param>
    /// <param name="cachedTarget">PhaseRunner 가 미리 캐싱한 타겟 Transform (null 허용)</param>
    public ActionState(CapsuleController controller, ActionData actionData,
                       InterruptToken token = null, Transform cachedTarget = null)
    {
        this.controller    = controller;
        this.actionData    = actionData;
        this.interruptToken = token;
        this.cachedTarget  = cachedTarget;
    }

    public abstract IEnumerator Execute();

    /// <summary>
    /// 중단 토큰이 설정되었는지 확인합니다.
    /// 코루틴 루프 내 매 프레임 호출해 안전하게 탈출하세요.
    /// </summary>
    protected bool ShouldInterrupt()
        => interruptToken != null && interruptToken.IsInterrupted;

    protected void PlayAnimation(bool forceRestart = false)
    {
        if (actionData.playAnimation && controller.animController != null && !string.IsNullOrEmpty(actionData.animationName))
        {
            if (forceRestart && controller.animController.animator != null)
                controller.animController.animator.Play(actionData.animationName, -1, 0f);
            else
                controller.animController.Play(actionData.animationName);
        }
    }

    protected Transform GetResolvedTarget()
    {
        if (actionData.targetType == TargetType.TrackObject)
        {
            // 캐싱된 타겟 우선 사용 → FindWithTag 횟수 최소화
            if (cachedTarget) return cachedTarget;
            GameObject targetGO = GameObject.FindWithTag(actionData.targetTag);
            if (targetGO != null) return targetGO.transform;
        }
        return null;
    }
}


public class MoveState : ActionState
{
    public MoveState(CapsuleController controller, ActionData actionData,
                     InterruptToken token = null, Transform cachedTarget = null)
        : base(controller, actionData, token, cachedTarget) { }

    public override IEnumerator Execute()
    {
        PlayAnimation(); 
        
        if (actionData.isTeleport)
        {
            if (actionData.targetType == TargetType.Direction)
            {
                // 방향 이동에 대해서는 순간이동(오프셋/새로고침) 기능 적용 대상이 아님
                // 만약 방향 순간이동이 필요하다면 기존처럼 처리하거나, 현재는 구현 생략
            }
            else
            {
                bool isTracking = actionData.targetType != TargetType.SpecificPosition;
                Transform trackingTarget = isTracking ? GetResolvedTarget() : null;
                Vector3 specificPos = actionData.targetPosition;
                
                controller.TeleportToTarget(trackingTarget, specificPos, isTracking, actionData);
            }
            yield return null;
            yield break;
        }

        if (actionData.useJump)
        {
            controller.Jump(actionData.jumpForce);
        }

        if (actionData.targetType == TargetType.Direction)
        {
            Vector3 dir = GetDirectionFromEnum(actionData.moveDirection8);
            yield return controller.StartCoroutine(controller.MoveInDirection(
                dir,
                actionData.startSpeed, actionData.speed, actionData.useAcceleration, actionData.acceleration,
                actionData.stopOnTimeLimit, actionData.timeLimit,
                interruptToken
            ));
        }
        else 
        {
            bool isTracking = actionData.targetType != TargetType.SpecificPosition;
            Transform trackingTarget = isTracking ? GetResolvedTarget() : null;
            Vector3 specificPos = actionData.targetPosition;
            
            if (isTracking && trackingTarget == null)
            {
                Debug.LogWarning($"Target object with tag '{actionData.targetTag}' not found.");
                yield break;
            }

            yield return controller.StartCoroutine(controller.MoveToTarget(
                trackingTarget, specificPos, isTracking, actionData, interruptToken
            ));
        }
    }

    private Vector3 GetDirectionFromEnum(MoveDirection8 dir8)
    {
        switch (dir8)
        {
            case MoveDirection8.Up: return Vector3.forward;
            case MoveDirection8.Down: return Vector3.back;
            case MoveDirection8.Left: return Vector3.left;
            case MoveDirection8.Right: return Vector3.right;
            case MoveDirection8.UpLeft: return new Vector3(-1, 0, 1).normalized;
            case MoveDirection8.UpRight: return new Vector3(1, 0, 1).normalized;
            case MoveDirection8.DownLeft: return new Vector3(-1, 0, -1).normalized;
            case MoveDirection8.DownRight: return new Vector3(1, 0, -1).normalized;
            case MoveDirection8.Forward:
                // 캐릭터가 현재 바라보는 월드 기준 앞 방향 (로컬 left 방향이 2.5D 앞)
                return controller.transform.rotation * Vector3.left;
            case MoveDirection8.Backward:
                // 캐릭터가 현재 바라보는 월드 기준 뒤 방향 (로컬 right 방향이 2.5D 뒤)
                return controller.transform.rotation * Vector3.right;
            default: return Vector3.zero;
        }
    }
}

public class WaitState : ActionState
{
    public WaitState(CapsuleController controller, ActionData actionData,
                     InterruptToken token = null, Transform cachedTarget = null)
        : base(controller, actionData, token, cachedTarget) { }

    public override IEnumerator Execute()
    {
        PlayAnimation();
        float elapsed = 0f;
        while (elapsed < actionData.timeLimit)
        {
            if (ShouldInterrupt()) yield break;
            elapsed += UnityEngine.Time.deltaTime;
            yield return null;
        }
    }
}

public class AttackState : ActionState
{
    public AttackState(CapsuleController controller, ActionData actionData,
                       InterruptToken token = null, Transform cachedTarget = null)
        : base(controller, actionData, token, cachedTarget) { }

    public override IEnumerator Execute()
    {
        if (ShouldInterrupt()) yield break;

        AttackCaster caster = controller.GetComponent<AttackCaster>();
        if (caster)
        {
            Vector3 fixedPos = Vector3.zero;
            bool isFixed = actionData.actionType == ActionType.FixedAttack;

            if (isFixed)
            {
                Transform target = GetResolvedTarget();
                if (target)
                    fixedPos = target.position + actionData.attackOffset;
                else if (actionData.targetType == TargetType.SpecificPosition)
                    fixedPos = actionData.targetPosition + actionData.attackOffset;
                else
                    fixedPos = controller.transform.position + actionData.attackOffset;

                caster.SetAttackData(actionData, true, fixedPos);
            }
            else
            {
                caster.SetAttackData(actionData, false, Vector3.zero);
            }

            // 시작 시 즉시 타격 판정 플래그가 참일 때만 명시적으로 CastDamage 호출
            if (actionData.castDamageOnStart)
            {
                caster.CastDamage();
            }
        }

        // 공격 시 동시 점프(도약) 물리 적용 (승룡권 등)
        if (actionData.useJumpInAttack)
        {
            controller.Jump(actionData.attackJumpForce);
        }

        PlayAnimation(true);

        if (controller.animController != null && actionData.playAnimation)
        {
            float animLength = controller.animController.GetAnimationLength(actionData.animationName);
            float elapsed = 0f;
            while (elapsed < animLength)
            {
                if (ShouldInterrupt()) yield break;
                elapsed += UnityEngine.Time.deltaTime;
                yield return null;
            }
        }
        else if (!actionData.playAnimation)
        {
            yield return null;
        }
    }
}

public class RangedAttackState : ActionState
{
    public RangedAttackState(CapsuleController controller, ActionData actionData,
                              InterruptToken token = null, Transform cachedTarget = null)
        : base(controller, actionData, token, cachedTarget) { }

    public override IEnumerator Execute()
    {
        if (ShouldInterrupt()) yield break;

        AttackCaster caster = controller.GetComponent<AttackCaster>();
        if (caster != null)
        {
            Transform target = GetResolvedTarget();
            bool hasTarget = false;
            Vector3 targetPos = Vector3.zero;

            if (target != null)
            {
                targetPos = target.position;
                hasTarget = true;
            }
            else if (actionData.targetType == TargetType.SpecificPosition)
            {
                targetPos = actionData.targetPosition;
                hasTarget = true;
            }

            caster.SetAttackData(actionData, hasTarget, targetPos);
        }

        PlayAnimation(true);

        float animLength = 0f;
        if (controller.animController != null && actionData.playAnimation)
            animLength = controller.animController.GetAnimationLength(actionData.animationName);

        float totalShootTime = actionData.projectileCount > 1
            ? (actionData.projectileCount - 1) * actionData.projectileInterval
            : 0f;

        float waitTime = Mathf.Max(animLength, totalShootTime);
        if (waitTime > 0f)
        {
            float elapsed = 0f;
            while (elapsed < waitTime)
            {
                if (ShouldInterrupt()) yield break;
                elapsed += UnityEngine.Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return null; 
        }
    }
}