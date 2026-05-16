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
    
    protected void PlayAnimation(bool forceRestart = false)
    {
        if (actionData.playAnimation && controller.animController != null && !string.IsNullOrEmpty(actionData.animationName))
        {
            if (forceRestart && controller.animController.animator != null)
            {
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
        if (actionData.targetType == TargetType.TrackObject)
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
                actionData.stopOnTimeLimit, actionData.timeLimit
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
                trackingTarget, specificPos, isTracking, actionData
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
            default: return Vector3.zero;
        }
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
                    fixedPos = target.position + actionData.attackOffset;
                }
                else if (actionData.targetType == TargetType.SpecificPosition)
                {
                    fixedPos = actionData.targetPosition + actionData.attackOffset;
                }
                else 
                {
                    fixedPos = controller.transform.position + actionData.attackOffset;
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
        {
            animLength = controller.animController.GetAnimationLength(actionData.animationName);
        }

        float totalShootTime = 0f;
        if (actionData.projectileCount > 1)
        {
            totalShootTime = (actionData.projectileCount - 1) * actionData.projectileInterval;
        }

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