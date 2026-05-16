using System;
using UnityEngine;

[Serializable]
public class FixedAttackAction : BaseAttackAction
{
    [Header("■ 고정 타격 설정")]
    public Vector3 targetPosition;

    [Header("■ 타격 범위 설정")]
    public AttackShape attackShape = AttackShape.Sphere;
    public float attackRadius = 1.5f;
    public float attackHeight = 2f;
    public Vector3 attackHitBoxSize = new Vector3(2f, 1f, 2f);

    public override ActionState CreateState(CapsuleController controller)
    {
        return new AttackState(controller, this);
    }

    public override string GetActionName() => "고정 좌표 타격";
}
