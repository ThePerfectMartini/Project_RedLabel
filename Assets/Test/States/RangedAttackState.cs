using System.Collections;
using UnityEngine;

public class RangedAttackState : ActionState
{
    private RangedAttackAction action;

    public RangedAttackState(CapsuleController controller, RangedAttackAction action) : base(controller, action)
    {
        this.action = action;
    }

    private Transform GetResolvedTarget()
    {
        if (action.targetType == TargetType.TrackObject)
        {
            GameObject targetGO = GameObject.FindWithTag(action.targetTag);
            if (targetGO != null) return targetGO.transform;
        }
        return null;
    }

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
                if (action.trackXOnly && !action.trackZOnly) targetPos.z = controller.transform.position.z;
                if (!action.trackXOnly && action.trackZOnly) targetPos.x = controller.transform.position.x;
                hasTarget = true;
            }
            else if (action.targetType == TargetType.SpecificPosition)
            {
                targetPos = action.targetPosition;
                hasTarget = true;
            }

            caster.SetAttackData(action, hasTarget, targetPos);
        }

        PlayAnimation(true);

        float animLength = 0f;
        if (controller.animController != null && action.playAnimation)
        {
            animLength = controller.animController.GetAnimationLength(action.animationName);
        }

        float totalShootTime = 0f;
        if (action.projectileCount > 1)
        {
            totalShootTime = (action.projectileCount - 1) * action.projectileInterval;
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
