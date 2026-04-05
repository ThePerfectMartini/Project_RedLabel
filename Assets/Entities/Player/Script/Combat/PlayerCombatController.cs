using UnityEngine;
using System.Collections;

public class PlayerCombatController : MonoBehaviour
{
    [Header("콤보 공격 데이터 (ScriptableObject)")]
    [Tooltip("프로젝트 창에서 생성한 Combo Attack Data 에셋을 순서대로 넣어주세요.")]
    public ComboAttack[] comboAttacks; // 타입을 ComboAttack으로 변경

    [SerializeField] private LayerMask enemyLayer;

    [Header("디버그 설정")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private int showIndex = 1;
    [SerializeField] private float gizmoFlashDuration = 0.1f;

    private bool _isHitCheckActive = false;
    private int _debugComboIndex = 0;
    private Coroutine _gizmoFlashCoroutine;

    public void PerformHitCheck(int comboIndex)
    {
        if (comboAttacks == null || comboIndex < 0 || comboIndex >= comboAttacks.Length) return;

        // 타입 변경
        ComboAttack currentAttack = comboAttacks[comboIndex];
        _debugComboIndex = comboIndex;

        if (_gizmoFlashCoroutine != null) StopCoroutine(_gizmoFlashCoroutine);
        _gizmoFlashCoroutine = StartCoroutine(FlashGizmoCoroutine());

        Vector3 boxCenter = transform.TransformPoint(currentAttack.attackOffset);
        Collider[] hitEnemies = Physics.OverlapBox(
            boxCenter,
            currentAttack.attackBoxSize / 2f,
            transform.rotation,
            enemyLayer
        );

        foreach (Collider collider in hitEnemies)
        {
            Entity targetEntity = collider.GetComponent<Entity>();
            
            if (targetEntity != null)
            {
                Vector3 pushDirection = transform.right * currentAttack.knockbackPower;
                Vector3 upwardForce = Vector3.up * currentAttack.launchPower;

                HitData attackData = new HitData
                {
                    damage = currentAttack.damage,
                    knockbackForce = pushDirection + upwardForce
                };

                targetEntity.TakeDamage(attackData);
            }
        }
    }

    private IEnumerator FlashGizmoCoroutine()
    {
        _isHitCheckActive = true;
        yield return new WaitForSeconds(gizmoFlashDuration);
        _isHitCheckActive = false;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || comboAttacks == null || comboAttacks.Length == 0) return;
        
        if (showIndex >= comboAttacks.Length || !comboAttacks[showIndex]) return; 

        // 타입 변경
        ComboAttack info = comboAttacks[showIndex];

        Gizmos.color = _isHitCheckActive ? Color.green : new Color(1f, 0f, 0f, 0.5f);

        Vector3 boxCenter = transform.TransformPoint(info.attackOffset);
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(boxCenter, transform.rotation, Vector3.one);
        
        Gizmos.DrawWireCube(Vector3.zero, info.attackBoxSize);
        
        if (_isHitCheckActive)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawCube(Vector3.zero, info.attackBoxSize);
        }

        Gizmos.matrix = originalMatrix;
    }
}