using UnityEngine;

public class AttackCaster : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("이 캐릭터가 공격할 대상의 레이어 (예: Player 레이어)")]
    public LayerMask targetLayer;
    
    private ActionData currentAttackData;

    // 기즈모 색상 변경을 위한 타이머 변수
    private float attackTriggerTime = -1f;
    private float gizmoBlueDuration = 0.2f; // 파란색 기즈모 유지 시간 (초)

    // ActionState의 AttackState에서 공격 시작 시 호출하여 공격 데이터를 넘겨받음
    public void SetAttackData(ActionData data)
    {
        currentAttackData = data;
    }

    // AnimationController의 애니메이션 이벤트(OnAttackImpact)에서 호출됨
    public void CastDamage()
    {
        if (currentAttackData == null) return;

        // 공격이 실행된 시간 기록 (기즈모 색상을 잠시 파란색으로 변경하기 위함)
        attackTriggerTime = Time.time;

        // ▼ 회전(rotation) 기반 로직 복원: 현재 오브젝트의 위치와 회전을 기준으로 오프셋 계산
        Vector3 centerPoint = transform.position + (transform.rotation * currentAttackData.attackOffset);
        Collider[] hits;

        // 공격 형태에 따른 범위 내 콜라이더 감지 (targetLayer를 통해 최적화)
        if (currentAttackData.attackShape == AttackShape.Sphere)
        {
            hits = Physics.OverlapSphere(centerPoint, currentAttackData.attackRadius, targetLayer);
        }
        else // Box
        {
            // Box 판정 시, 크기의 절반(HalfExtents)을 사용하고 오브젝트의 현재 회전값을 반영합니다.
            hits = Physics.OverlapBox(centerPoint, currentAttackData.attackHitBoxSize * 0.5f, transform.rotation, targetLayer);
        }

        foreach (Collider hitCol in hits)
        {
            // LayerMask로 필터링했으므로, 닿은 것은 모두 유효한 타겟입니다.
            Debug.Log($"[{gameObject.name}]가 {hitCol.name} 타격 성공! (데미지: {currentAttackData.damage})");

            // 1. 피격 애니메이션 재생
            AnimationController targetAnimCtrl = hitCol.GetComponentInChildren<AnimationController>();
            if (targetAnimCtrl != null)
            {
                targetAnimCtrl.Play("Hit"); // "Hit" 애니메이션 재생
            }
            else
            {
                Animator targetAnim = hitCol.GetComponentInChildren<Animator>();
                if (targetAnim != null)
                {
                    targetAnim.Play("Hit");
                }
            }

            // 2. 리지드바디 넉백 적용
            Rigidbody targetRb = hitCol.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                // 공격자에서 타겟 방향으로 밀어내는 벡터 계산
                Vector3 knockbackDir = (hitCol.transform.position - transform.position).normalized;
                
                // Vector3로 지정한 넉백 힘을 각 축별로 적용
                Vector3 finalKnockback = new Vector3(
                    knockbackDir.x * currentAttackData.knockbackForce.x,
                    currentAttackData.knockbackForce.y,
                    knockbackDir.z * currentAttackData.knockbackForce.z
                );
                
                // 기존의 속도를 초기화하고 새로운 힘을 가해 넉백이 일정하게 들어가게 함
                targetRb.linearVelocity = Vector3.zero;
                targetRb.AddForce(finalKnockback, ForceMode.Impulse);
            }
        }
    }

    // 에디터에서 선택 여부와 상관없이 항상 공격 범위를 확인할 수 있도록 OnDrawGizmos 로 변경
    private void OnDrawGizmos()
    {
        if (currentAttackData != null)
        {
            // 최근 gizmoBlueDuration 초 안에 공격이 실행되었으면 파란색, 아니면 빨간색 출력
            Color gizmoColor = new Color(1f, 0f, 0f, 0.5f);
            if (Application.isPlaying && Time.time - attackTriggerTime <= gizmoBlueDuration)
            {
                gizmoColor = new Color(0f, 0f, 1f, 0.5f); // 파란색
            }
            
            Gizmos.color = gizmoColor;
            
            // ▼ 회전 로직으로 복귀: 캐릭터의 현재 방향(rotation)만 적용 (스케일은 배제)
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.matrix = rotationMatrix;

            // 로컬 기준에서의 중심점
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
            
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}