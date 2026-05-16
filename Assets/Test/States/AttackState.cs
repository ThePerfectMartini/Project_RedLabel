using System.Collections;
using UnityEngine;

public class AttackState : ActionState
{
    private BaseAttackAction action;
    private bool isFixed;

    public AttackState(CapsuleController controller, BaseAttackAction action, bool isFixed = false) : base(controller, action)
    {
        this.action = action;
        
        if (action is FixedAttackAction) this.isFixed = true;
        else this.isFixed = isFixed;
    }

    public override IEnumerator Execute()
    {
        AttackCaster caster = controller.GetComponent<AttackCaster>();
        if (caster != null)
        {
            Vector3 fixedPos = Vector3.zero;

            if (isFixed && action is FixedAttackAction fixedAction)
            {
                fixedPos = fixedAction.targetPosition + fixedAction.attackOffset;
                caster.SetAttackData(action, true, fixedPos);
            }
            else
            {
                caster.SetAttackData(action, false, Vector3.zero);
            }
        }

        PlayAnimation(true);

        if (controller.animController != null && action.playAnimation)
        {
            float animLength = controller.animController.GetAnimationLength(action.animationName);
            yield return new WaitForSeconds(animLength);
        }
        else if (!action.playAnimation)
        {
            yield return null; 
        }
    }
}
