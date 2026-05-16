using System;
using UnityEngine;

[Serializable]
public class RangedAttackAction : BaseAttackAction
{
    [Header("■ 타겟팅 기준 설정")]
    public TargetType targetType;
    public Vector3 targetPosition;
    public string targetTag;

    [Header("■ 투사체 설정")]
    public GameObject projectilePrefab;
    public int projectileCount = 1;
    public float projectileInterval = 0.1f;
    public float projectileSpeed = 15f;
    public float projectileLifeTime = 3f;

    // RangedAttack에 필요한 moveDirection8 (특정 방향 고정 시 사용)
    public MoveDirection8 moveDirection8 = MoveDirection8.None;
    
    // 축 추적 필요
    public bool trackXOnly;
    public bool trackZOnly;

    public override ActionState CreateState(CapsuleController controller)
    {
        return new RangedAttackState(controller, this);
    }

    public override string GetActionName() => "원거리 투사체 공격";
}
