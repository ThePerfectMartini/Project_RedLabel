using UnityEngine;
// using UnityEngine.InputSystem; // 필요 없음
using System;

public class PlayerAttack : MonoBehaviour
{
    [Header("입력 (InputReader 연결)")]
    [SerializeField] private InputReader inputReader; // InputActionReference 교체

    [Header("현재 장착된 공격 데이터")]
    public AttackData_legacy currentAttack;

    [Header("환경 설정")]
    public LayerMask enemyLayer;
    public Transform attackPoint;

    public event Action OnAttackPerformed;

    private void OnEnable()
    {
        if (inputReader != null)
        {
            // 공격 버튼 눌렀을 때 실행될 함수 연결
            inputReader.AttackEvent += PerformAttack;
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.AttackEvent -= PerformAttack;
        }
    }

    // 매개변수(CallbackContext) 제거 -> InputReader가 단순히 "공격해!"라고 신호만 줌
    private void PerformAttack()
    {
        if (currentAttack == null)
        {
            Debug.LogWarning("공격 데이터가 없습니다!");
            return;
        }

        // 1. 히트박스 위치 계산
        Vector3 center = attackPoint.TransformPoint(currentAttack.hitboxOffset);

        // 2. 물리 판정
        Collider[] hitEnemies = Physics.OverlapBox(
            center,
            currentAttack.hitboxSize / 2,
            attackPoint.rotation,
            enemyLayer
        );

        // 3. 데미지 처리
        foreach (Collider enemyCollider in hitEnemies)
        {
            EnemyController enemy = enemyCollider.GetComponent<EnemyController>();
            if (enemy != null) enemy.TakeDamage(currentAttack.damage);
            Debug.Log($"{enemyCollider.name} 공격 적중! (데미지: {currentAttack.damage})");
        }

        // 4. 이벤트 발송
        OnAttackPerformed?.Invoke();
    }

    // 기즈모 그리는 부분은 그대로 두셔도 됩니다.
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null || currentAttack == null) return;
        Gizmos.color = Color.red;
        Vector3 center = attackPoint.TransformPoint(currentAttack.hitboxOffset);
        Gizmos.matrix = Matrix4x4.TRS(center, attackPoint.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, currentAttack.hitboxSize);
    }
}