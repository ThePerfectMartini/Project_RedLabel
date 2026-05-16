using System;
using UnityEngine;

[Serializable]
public abstract class BaseAttackAction : ActionBase
{
    [Header("■ 공통 공격 설정")]
    public float damage = 10f;
    public Vector3 knockbackForce = new Vector3(15f, 5f, 15f);
    public Vector3 attackOffset = new Vector3(0, 1f, 1f);
}
