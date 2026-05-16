using System.Collections;
using UnityEngine;

public class WaitState : ActionState
{
    private WaitAction actionData;

    public WaitState(CapsuleController controller, WaitAction actionData) : base(controller, actionData)
    {
        this.actionData = actionData;
    }

    public override IEnumerator Execute()
    {
        PlayAnimation();
        yield return new WaitForSeconds(actionData.timeLimit);
    }
}
