using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CombatController : MonoBehaviour
{
    private Player _player;
    
    [Header("무기 설정")]
    [Tooltip("현재 장착 중인 무기의 콤보 데이터 에셋 (SO)")]
    [SerializeField] private AttackComboData _attackComboData;
    
    [Header("환경 설정")]
    public LayerMask enemyLayer;

    public bool debug_drawGizmos;


    private void Awake()
    {
        _player = GetComponent<Player>();
    }

    private void AttackScan(int comboIndex)
    {
// 예외 처리: 장착된 무기가 없거나, 인덱스가 무기의 콤보 타수를 벗어났을 때 방지
        if (_attackComboData == null || comboIndex < 0 || comboIndex >= _attackComboData.comboAttacks.Length)
        {
            Debug.LogWarning("잘못된 콤보 인덱스이거나 무기 데이터가 없습니다!");
            return;
        }

        // SO 에셋에서 현재 타수에 맞는 데이터 꺼내기
        AttackHitboxData currentHitbox = _attackComboData.comboAttacks[comboIndex];

        // 3D 공간의 타격 중심점 계산 (캐릭터가 바라보는 방향 기준)
        Vector3 attackCenter = transform.TransformPoint(currentHitbox.offset);

        // 해당 범위 내의 적들을 검출 (3D 물리)
        Collider[] hitEnemies = Physics.OverlapBox(attackCenter, currentHitbox.halfExtents, transform.rotation, enemyLayer);

        // 3. 데미지 처리
        foreach (Collider enemyCollider in hitEnemies)
        {
            EnemyController enemy = enemyCollider.GetComponent<EnemyController>();
            if (enemy != null) enemy.TakeDamage(1);
            Debug.Log($"{enemyCollider.name} 공격 적중! (데미지: {1} 이거 어디서 가져올지 고민해볼것)");
        }
    }
    
    // 이하 테스트용도
    
    [Header("에디터 디버그용")]
    [Tooltip("에디터 씬 뷰에서 범위를 미리 볼 콤보 인덱스 (0부터 시작)")]
    [SerializeField, Range(0, 2)] private int debugComboIndex = 0;
    
    private void OnDrawGizmos()
    {
        if (!debug_drawGizmos) return;
        if (_attackComboData == null || _attackComboData.comboAttacks == null) return;
        if (debugComboIndex >= _attackComboData.comboAttacks.Length) return;

        AttackHitboxData debugData = _attackComboData.comboAttacks[debugComboIndex];

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f); // 반투명 붉은색
        Vector3 attackCenter = transform.TransformPoint(debugData.offset);
        
        Gizmos.matrix = Matrix4x4.TRS(attackCenter, transform.rotation, debugData.halfExtents * 2f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);    }
}