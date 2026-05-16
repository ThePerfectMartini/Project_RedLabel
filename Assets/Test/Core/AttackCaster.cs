using System.Collections;
using UnityEngine;

public class AttackCaster : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("이 캐릭터가 공격할 대상의 레이어 (예: Player 레이어)")]
    public LayerMask targetLayer;
    
    private BaseAttackAction currentAttackData;

    private float attackTriggerTime = -1f;
    private float gizmoBlueDuration = 0.2f; 
    
    private bool isFixedAttack;
    private Vector3 fixedWorldPosition;

    public void SetAttackData(BaseAttackAction data, bool isFixed = false, Vector3 fixedPos = default)
    {
        currentAttackData = data;
        isFixedAttack = isFixed;
        fixedWorldPosition = fixedPos;
    }

    public void CastDamage()
    {
        if (currentAttackData == null) return;
        attackTriggerTime = Time.time;

        if (currentAttackData is RangedAttackAction rangedData)
        {
            if (rangedData.projectilePrefab != null)
            {
                StartCoroutine(FireRangedProjectiles(rangedData, isFixedAttack, fixedWorldPosition));
            }
            return;
        }

        Vector3 centerPoint;
        if (isFixedAttack)
        {
            centerPoint = fixedWorldPosition;
        }
        else
        {
            centerPoint = transform.position + (transform.rotation * currentAttackData.attackOffset);
        }

        Collider[] hits = new Collider[0];

        AttackShape shape = AttackShape.Sphere;
        float radius = 1.5f;
        float height = 2f;
        Vector3 boxSize = Vector3.one;

        if (currentAttackData is FixedAttackAction fixedAction) 
        { 
            shape = fixedAction.attackShape; radius = fixedAction.attackRadius; height = fixedAction.attackHeight; boxSize = fixedAction.attackHitBoxSize; 
        }
        else if (currentAttackData is VariableAttackAction varAction) 
        { 
            shape = varAction.attackShape; radius = varAction.attackRadius; height = varAction.attackHeight; boxSize = varAction.attackHitBoxSize; 
        }

        if (shape == AttackShape.Sphere)
        {
            hits = Physics.OverlapSphere(centerPoint, radius, targetLayer);
        }
        else if (shape == AttackShape.Box)
        {
            Quaternion boxRot = isFixedAttack ? Quaternion.identity : transform.rotation;
            hits = Physics.OverlapBox(centerPoint, boxSize * 0.5f, boxRot, targetLayer);
        }
        else if (shape == AttackShape.Cylinder)
        {
            float offsetForCapsule = Mathf.Max(0, height * 0.5f - radius);
            Vector3 capTop = centerPoint + Vector3.up * offsetForCapsule;
            Vector3 capBottom = centerPoint - Vector3.up * offsetForCapsule;

            hits = Physics.OverlapCapsule(capBottom, capTop, radius, targetLayer);
        }

        foreach (Collider hitCol in hits)
        {
            Debug.Log($"[{gameObject.name}]가 {hitCol.name} 타격 성공! (데미지: {currentAttackData.damage})");

            AnimationController targetAnimCtrl = hitCol.GetComponentInChildren<AnimationController>();
            if (targetAnimCtrl != null)
            {
                targetAnimCtrl.Play("Hit"); 
            }
            else
            {
                Animator targetAnim = hitCol.GetComponentInChildren<Animator>();
                if (targetAnim != null)
                {
                    targetAnim.Play("Hit");
                }
            }

            Rigidbody targetRb = hitCol.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                Vector3 knockbackDir = (hitCol.transform.position - transform.position).normalized;
                
                Vector3 finalKnockback = new Vector3(
                    knockbackDir.x * currentAttackData.knockbackForce.x,
                    currentAttackData.knockbackForce.y,
                    knockbackDir.z * currentAttackData.knockbackForce.z
                );
                
                targetRb.linearVelocity = Vector3.zero;
                targetRb.AddForce(finalKnockback, ForceMode.Impulse);
            }
        }
    }

    private IEnumerator FireRangedProjectiles(RangedAttackAction data, bool fixedAttack, Vector3 fixedPos)
    {
        int count = Mathf.Max(1, data.projectileCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = transform.position + (transform.rotation * data.attackOffset);
            Vector3 shootDir = transform.rotation * CapsuleController.GetDirectionFromEnum(data.moveDirection8);
            
            if (data.targetType == TargetType.TrackObject)
            {
                if (!string.IsNullOrEmpty(data.targetTag))
                {
                    GameObject tGO = GameObject.FindWithTag(data.targetTag);
                    if (tGO != null)
                    {
                        Vector3 targetPos = tGO.transform.position;
                        if (data.trackXOnly && !data.trackZOnly) targetPos.z = spawnPos.z;
                        if (!data.trackXOnly && data.trackZOnly) targetPos.x = spawnPos.x;
                        
                        shootDir = (targetPos - spawnPos).normalized;
                    }
                }
            }
            else if (data.targetType == TargetType.SpecificPosition)
            {
                shootDir = (data.targetPosition - spawnPos).normalized;
            }
            else if (fixedAttack)
            {
                shootDir = (fixedPos - spawnPos).normalized;
            }

            if (shootDir == Vector3.zero) shootDir = transform.forward;

            GameObject projObj = Instantiate(data.projectilePrefab, spawnPos, Quaternion.identity);
            
            Projectile proj = projObj.GetComponent<Projectile>();
            if (proj == null) proj = projObj.AddComponent<Projectile>(); 
            
            // proj.Initialize needs to be updated or we pass RangedAttackAction
            // Since we didn't rewrite Projectile, we assume it has an Initialize method that can take ActionBase or something.
            // Wait, Projectile uses ActionData. We must update Projectile.cs too.
            proj.Initialize(data, shootDir, targetLayer, gameObject);

            if (i < count - 1 && data.projectileInterval > 0f)
            {
                yield return new WaitForSeconds(data.projectileInterval);
            }
        }
    }

    // OnDrawGizmos has been moved to ActionGizmoUtility!
}
