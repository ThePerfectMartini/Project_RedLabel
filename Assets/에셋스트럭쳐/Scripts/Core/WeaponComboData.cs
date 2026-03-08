using UnityEngine;

// 1타, 2타 등 개별 공격의 타격 데이터를 담는 구조체
[System.Serializable]
public struct AttackHitboxData
{
    [Tooltip("3D 타격 박스의 크기")]
    public Vector3 halfExtents;
    [Tooltip("캐릭터 중심점으로부터 타격 박스가 생성될 위치(오프셋)")]
    public Vector3 offset;
    [Tooltip("이 타수에 요귀(적)에게 입힐 피해량")]
    public float damage;
}

// 프로젝트 창에서 우클릭으로 생성할 수 있는 하나의 무기 콤보 세트
[CreateAssetMenu(fileName = "NewAttackCombo", menuName = "Data/Attack Combo Data")]
public class AttackComboData : ScriptableObject
{
    [Tooltip("이 무기의 콤보 단계별 타격 데이터 (예: 배열 크기 3이면 1~3타)")]
    public AttackHitboxData[] comboAttacks;
}