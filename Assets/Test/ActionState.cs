using System.Collections;
using UnityEngine;

public abstract class ActionState
{
    protected CapsuleController controller;
    protected ActionData actionData;

    public ActionState(CapsuleController controller, ActionData actionData)
    {
        this.controller = controller;
        this.actionData = actionData;
    }

    public abstract IEnumerator Execute();
    
    // ▼ 버그 수정 2: 무한 반복 루프에서 애니메이션이 씹히는 것을 방지하기 위해 강제 재시작 옵션(forceRestart) 추가
    protected void PlayAnimation(bool forceRestart = false)
    {
        if (actionData.playAnimation && controller.animController != null && !string.IsNullOrEmpty(actionData.animationName))
        {
            if (forceRestart && controller.animController.animator != null)
            {
                // 이미 재생중인 상태여도 무조건 0초부터 다시 재생하여 애니메이션 이벤트를 확실히 터뜨립니다.
                controller.animController.animator.Play(actionData.animationName, -1, 0f);
            }
            else
            {
                controller.animController.Play(actionData.animationName);
            }
        }
    }

    protected Transform GetResolvedTarget()
    {
        if (actionData.targetType == TargetType.TrackObject || 
            actionData.targetType == TargetType.TrackObjectXOnly || 
            actionData.targetType == TargetType.TrackObjectZOnly)
        {
            GameObject targetGO = GameObject.FindWithTag(actionData.targetTag);
            if (targetGO != null) return targetGO.transform;
        }
        return null;
    }
}

public class MoveState : ActionState
{
    public MoveState(CapsuleController controller, ActionData actionData) : base(controller, actionData) { }

    public override IEnumerator Execute()
    {
        PlayAnimation(); 
        
        if (actionData.targetType == TargetType.SpecificPosition)
        {
            yield return controller.StartCoroutine(controller.MoveToPosition(
                actionData.targetPosition + actionData.offset,
                actionData.startSpeed, actionData.speed, actionData.useAcceleration, actionData.acceleration,
                actionData.stopOnTargetReached, actionData.stopDistance,
                actionData.stopOnTimeLimit, actionData.timeLimit,
                actionData.distanceMode, actionData.targetShape, actionData.shapeOffset, actionData.attackBoxSize
            ));
        }
        else if (actionData.targetType == TargetType.Direction)
        {
            yield return controller.StartCoroutine(controller.MoveInDirection(
                actionData.moveDirection,
                actionData.startSpeed, actionData.speed, actionData.useAcceleration, actionData.acceleration,
                actionData.stopOnTimeLimit, actionData.timeLimit
            ));
        }
        else 
        {
            Transform resolvedTarget = GetResolvedTarget();
            if (resolvedTarget != null)
            {
                bool trackX = actionData.targetType == TargetType.TrackObjectXOnly;
                bool trackZ = actionData.targetType == TargetType.TrackObjectZOnly;

                yield return controller.StartCoroutine(controller.TrackObject(
                    resolvedTarget, trackX, trackZ, actionData.trackMargin,
                    actionData.startSpeed, actionData.speed, actionData.useAcceleration, actionData.acceleration,
                    actionData.stopOnTargetReached, actionData.stopDistance,
                    actionData.stopOnTimeLimit, actionData.timeLimit,
                    actionData.distanceMode, actionData.targetShape, actionData.shapeOffset, actionData.attackBoxSize
                ));
            }
            else
            {
                Debug.LogWarning($"Target object with tag '{actionData.targetTag}' not found.");
            }
        }
    }
}

public class TeleportState : ActionState
{
    public TeleportState(CapsuleController controller, ActionData actionData) : base(controller, actionData) { }

    public override IEnumerator Execute()
    {
        PlayAnimation(); 
        
        if (actionData.targetType == TargetType.SpecificPosition)
        {
            controller.TeleportToPosition(actionData.targetPosition + actionData.offset);
        }
        else 
        {
            Transform resolvedTarget = GetResolvedTarget();
            bool trackX = actionData.targetType == TargetType.TrackObjectXOnly;
            bool trackZ = actionData.targetType == TargetType.TrackObjectZOnly;
            controller.TeleportToObject(resolvedTarget, trackX, trackZ, actionData.offset); 
        }
        
        yield return null; 
    }
}

public class WaitState : ActionState
{
    public WaitState(CapsuleController controller, ActionData actionData) : base(controller, actionData) { }

    public override IEnumerator Execute()
    {
        PlayAnimation(); 
        yield return new WaitForSeconds(actionData.timeLimit);
    }
}

public class AttackState : ActionState
{
    public AttackState(CapsuleController controller, ActionData actionData) : base(controller, actionData) { }

    public override IEnumerator Execute()
    {
        AttackCaster caster = controller.GetComponent<AttackCaster>();
        if (caster != null)
        {
            Vector3 fixedPos = Vector3.zero;
            bool isFixed = actionData.actionType == ActionType.FixedAttack;

            if (isFixed)
            {
                Transform target = GetResolvedTarget();
                if (target != null)
                {
                    fixedPos = target.position + actionData.offset;
                }
                else if (actionData.targetType == TargetType.SpecificPosition)
                {
                    fixedPos = actionData.targetPosition + actionData.offset;
                }
                else 
                {
                    fixedPos = controller.transform.position + actionData.offset;
                }
                
                caster.SetAttackData(actionData, true, fixedPos);
            }
            else
            {
                caster.SetAttackData(actionData, false, Vector3.zero);
            }
        }

        PlayAnimation(true);

        if (controller.animController != null && actionData.playAnimation)
        {
            float animLength = controller.animController.GetAnimationLength(actionData.animationName);
            yield return new WaitForSeconds(animLength);
        }
        else if (!actionData.playAnimation)
        {
            yield return null; 
        }
    }
}

public class RangedAttackState : ActionState
{
    public RangedAttackState(CapsuleController controller, ActionData actionData) : base(controller, actionData) { }

    public override IEnumerator Execute()
    {
        AttackCaster caster = controller.GetComponent<AttackCaster>();
        if (caster != null)
        {
            // 원거리 공격 시 고정 좌표 타격 여부는 내부 코루틴에서 처리하므로 false 처리
            caster.SetAttackData(actionData, false, Vector3.zero);
        }

        PlayAnimation(true);

        // ▼ 버그 수정 2: 지정된 발사 갯수(Count)가 전부 발사될 때까지 상태를 종료하지 않고 기다리게 합니다.
        float animLength = 0f;
        if (controller.animController != null && actionData.playAnimation)
        {
            animLength = controller.animController.GetAnimationLength(actionData.animationName);
        }

        float totalShootTime = 0f;
        if (actionData.projectileCount > 1)
        {
            totalShootTime = (actionData.projectileCount - 1) * actionData.projectileInterval;
        }

        // 애니메이션 길이와 투사체 총 발사 시간 중 더 긴 쪽을 선택하여 확실히 기다립니다.
        float waitTime = Mathf.Max(animLength, totalShootTime);
        
        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }
        else
        {
            yield return null; 
        }
    }
}