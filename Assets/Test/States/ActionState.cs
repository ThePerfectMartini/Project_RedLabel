using System.Collections;
using UnityEngine;

public abstract class ActionState
{
    protected CapsuleController controller;
    protected ActionBase baseAction;

    public ActionState(CapsuleController controller, ActionBase action)
    {
        this.controller = controller;
        this.baseAction = action;
    }

    public abstract IEnumerator Execute();

    protected void PlayAnimation(bool forceRestart = false)
    {
        if (baseAction.playAnimation && controller.animController != null && !string.IsNullOrEmpty(baseAction.animationName))
        {
            if (forceRestart && controller.animController.animator != null)
            {
                controller.animController.animator.Play(baseAction.animationName, -1, 0f);
            }
            else
            {
                controller.animController.Play(baseAction.animationName);
            }
        }
    }
}
