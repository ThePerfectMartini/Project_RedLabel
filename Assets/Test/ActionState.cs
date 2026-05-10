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
    
    protected void PlayAnimation()
    {
        if (actionData.playAnimation && controller.animController != null && !string.IsNullOrEmpty(actionData.animationName))
        {
            controller.animController.Play(actionData.animationName);
        }
    }

    protected Transform GetResolvedTarget()
    {
        // ▼ 통합된 TargetType에 맞추어 수정
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
        
        if (actionData.targetType == TargetType.SpecificPosition)
        {
            yield return controller.StartCoroutine(controller.MoveToPosition(
                actionData.targetPosition + actionData.offset, 
                actionData.startSpeed, actionData.speed, 
                actionData.useAcceleration, actionData.acceleration, 
                actionData.stopOnTargetReached, actionData.stopDistance, 
                actionData.stopOnTimeLimit, actionData.timeLimit,
                actionData.distanceMode, actionData.targetShape, actionData.shapeOffset, actionData.attackBoxSize)); 
        }
        else if (actionData.targetType == TargetType.Direction)
        {
            yield return controller.StartCoroutine(controller.MoveInDirection(
                actionData.moveDirection, 
                actionData.startSpeed, actionData.speed, 
                actionData.useAcceleration, actionData.acceleration, 
                actionData.stopOnTimeLimit, actionData.timeLimit));
        }
        else
        {
            // ▼ 체크박스에 따른 축 제한 파라미터 전달
            Transform resolvedTarget = GetResolvedTarget();
            
            yield return controller.StartCoroutine(controller.TrackObject(
                resolvedTarget, actionData.trackXOnly, actionData.trackZOnly, actionData.trackMargin, 
                actionData.startSpeed, actionData.speed, 
                actionData.useAcceleration, actionData.acceleration, 
                actionData.stopOnTargetReached, actionData.stopDistance, 
                actionData.stopOnTimeLimit, actionData.timeLimit,
                actionData.distanceMode, actionData.targetShape, actionData.shapeOffset, actionData.attackBoxSize));
        }
    }
}

public class TeleportState : ActionState
{
    public TeleportState(CapsuleController controller, ActionData actionData) : base(controller, actionData) { }

    public override IEnumerator Execute()
    {
        if (actionData.targetType == TargetType.SpecificPosition)
        {
            controller.TeleportToPosition(actionData.targetPosition + actionData.offset);
        }
        else // TrackObject
        {
            // ▼ 체크박스에 따른 축 제한 파라미터 전달
            Transform resolvedTarget = GetResolvedTarget();
            controller.TeleportToObject(resolvedTarget, actionData.trackXOnly, actionData.trackZOnly, actionData.offset); 
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
            caster.SendMessage("SetAttackData", actionData, SendMessageOptions.DontRequireReceiver);
        }

        PlayAnimation();

        if (controller.animController != null && actionData.playAnimation)
        {
            float animLength = controller.animController.GetAnimationLength(actionData.animationName);
            yield return new WaitForSeconds(animLength);
        }
        else if (!actionData.playAnimation)
        {
            yield return null; 
        }

        if (actionData.useWaitAfterAnimation)
        {
            yield return new WaitForSeconds(actionData.waitDuration);
        }
    }
}