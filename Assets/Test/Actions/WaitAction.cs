using System;
using UnityEngine;

[Serializable]
public class WaitAction : ActionBase
{
    [Header("■ 대기 설정")]
    public float timeLimit = 1f;

    public override ActionState CreateState(CapsuleController controller)
    {
        return new WaitState(controller, this);
    }

    public override string GetActionName() => "대기 (Wait)";
}
