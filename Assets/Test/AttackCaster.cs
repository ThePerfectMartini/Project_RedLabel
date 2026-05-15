using System.Collections;
using UnityEngine;

public class AttackCaster : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("이 캐릭터가 공격할 대상의 레이어 (예: Player 레이어)")]
    public LayerMask targetLayer;
    
    private ActionData currentAttackData;

    private float attackTriggerTime = -1f;
    private float gizmoBlueDuration = 0.2f; 
    
    private bool isFixedAttack;
    private Vector3 fixedWorldPosition;

    public void SetAttackData(ActionData data, bool isFixed = false, Vector3 fixedPos = default)
    {
        currentAttackData = data;
        isFixedAttack = isFixed;
        fixedWorldPosition = fixedPos;
    }

    public void CastDamage()
    {
        if (currentAttackData == null) return;
        attackTriggerTime = Time.time;

        if (currentAttackData.actionType == ActionType.RangedAttack)
        {
            if (currentAttackData.projectilePrefab != null)
            {
                StartCoroutine(FireRangedProjectiles(currentAttackData, isFixedAttack, fixedWorldPosition));
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

        if (currentAttackData.attackShape == AttackShape.Sphere)
        {
            hits = Physics.OverlapSphere(centerPoint, currentAttackData.attackRadius, targetLayer);
        }
        else if (currentAttackData.attackShape == AttackShape.Box)
        {
            Quaternion boxRot = isFixedAttack ? Quaternion.identity : transform.rotation;
            hits = Physics.OverlapBox(centerPoint, currentAttackData.attackHitBoxSize * 0.5f, boxRot, targetLayer);
        }
        else if (currentAttackData.attackShape == AttackShape.Cylinder)
        {
            float offsetForCapsule = Mathf.Max(0, currentAttackData.attackHeight * 0.5f - currentAttackData.attackRadius);
            Vector3 capTop = centerPoint + Vector3.up * offsetForCapsule;
            Vector3 capBottom = centerPoint - Vector3.up * offsetForCapsule;

            hits = Physics.OverlapCapsule(capBottom, capTop, currentAttackData.attackRadius, targetLayer);
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

    private IEnumerator FireRangedProjectiles(ActionData data, bool fixedAttack, Vector3 fixedPos)
    {
        int count = Mathf.Max(1, data.projectileCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = transform.position + (transform.rotation * data.attackOffset);
            Vector3 shootDir = transform.rotation * CapsuleController.GetDirectionFromEnum(data.moveDirection8);
            
            // ▼ 버그 수정 3: 매 발사 시점마다 타겟의 최신 위치를 새로 찾아서 방향을 갱신합니다.
            if (data.targetType == TargetType.TrackObject || data.targetType == TargetType.TrackObjectXOnly || data.targetType == TargetType.TrackObjectZOnly)
            {
                if (!string.IsNullOrEmpty(data.targetTag))
                {
                    GameObject tGO = GameObject.FindWithTag(data.targetTag);
                    if (tGO != null)
                    {
                        Vector3 targetPos = tGO.transform.position;
                        if (data.targetType == TargetType.TrackObjectXOnly) targetPos.z = spawnPos.z;
                        if (data.targetType == TargetType.TrackObjectZOnly) targetPos.x = spawnPos.x;
                        
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
            
            proj.Initialize(data, shootDir, targetLayer, gameObject);

            if (i < count - 1 && data.projectileInterval > 0f)
            {
                yield return new WaitForSeconds(data.projectileInterval);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (currentAttackData != null)
        {
            if (currentAttackData.actionType == ActionType.RangedAttack) return;

            Color gizmoColor = new Color(1f, 0f, 0f, 0.5f);
            if (Application.isPlaying && Time.time - attackTriggerTime <= gizmoBlueDuration)
            {
                gizmoColor = new Color(0f, 0f, 1f, 0.5f); 
            }
            
            Gizmos.color = gizmoColor;
            
            if (isFixedAttack)
            {
                Gizmos.matrix = Matrix4x4.identity;
                Vector3 localCenter = fixedWorldPosition;

                if (currentAttackData.attackShape == AttackShape.Sphere)
                {
                    Gizmos.DrawWireSphere(localCenter, currentAttackData.attackRadius);
                }
                else if (currentAttackData.attackShape == AttackShape.Box)
                {
                    Gizmos.DrawWireCube(localCenter, currentAttackData.attackHitBoxSize);
                    Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.1f);
                    Gizmos.DrawCube(localCenter, currentAttackData.attackHitBoxSize);
                }
                else if (currentAttackData.attackShape == AttackShape.Cylinder)
                {
                    DrawWireCylinder(localCenter, currentAttackData.attackRadius, currentAttackData.attackHeight);
                }
            }
            else
            {
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.matrix = rotationMatrix;
                Vector3 localCenter = currentAttackData.attackOffset;

                if (currentAttackData.attackShape == AttackShape.Sphere)
                {
                    Gizmos.DrawWireSphere(localCenter, currentAttackData.attackRadius);
                }
                else if (currentAttackData.attackShape == AttackShape.Box)
                {
                    Gizmos.DrawWireCube(localCenter, currentAttackData.attackHitBoxSize);
                    Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.1f);
                    Gizmos.DrawCube(localCenter, currentAttackData.attackHitBoxSize);
                }
                else if (currentAttackData.attackShape == AttackShape.Cylinder)
                {
                    DrawWireCylinder(localCenter, currentAttackData.attackRadius, currentAttackData.attackHeight);
                }
            }
            
            Gizmos.matrix = Matrix4x4.identity;
        }
    }

    private void DrawWireCylinder(Vector3 center, float radius, float height)
    {
        float halfHeight = height * 0.5f;
        Vector3 topCenter = center + Vector3.up * halfHeight;
        Vector3 bottomCenter = center - Vector3.up * halfHeight;

        DrawGizmoCircle(topCenter, radius);
        DrawGizmoCircle(bottomCenter, radius);

        Gizmos.DrawLine(topCenter + Vector3.right * radius, bottomCenter + Vector3.right * radius);
        Gizmos.DrawLine(topCenter - Vector3.right * radius, bottomCenter - Vector3.right * radius);
        Gizmos.DrawLine(topCenter + Vector3.forward * radius, bottomCenter + Vector3.forward * radius);
        Gizmos.DrawLine(topCenter - Vector3.forward * radius, bottomCenter - Vector3.forward * radius);
    }

    private void DrawGizmoCircle(Vector3 center, float radius)
    {
        int segments = 24;
        float angle = 0f;
        Vector3 lastPoint = center + new Vector3(Mathf.Cos(0) * radius, 0, Mathf.Sin(0) * radius);
        for (int i = 1; i <= segments; i++)
        {
            angle += (360f / segments) * Mathf.Deg2Rad;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }
    }
}