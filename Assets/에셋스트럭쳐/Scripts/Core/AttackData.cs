using UnityEngine;

[CreateAssetMenu(fileName = "NewAttackData", menuName = "Data/Attack Data")]
public class AttackData : ScriptableObject
{
    [Header("공격 기본 정보")]
    public string attackName;      // 공격 이름 (디버깅용)
    public int damage;             // 데미지

    [Header("히트박스 설정")]
    public Vector3 hitboxSize = Vector3.one; // 박스 크기
    public Vector3 hitboxOffset = new Vector3(0, 0, 1f); // 플레이어 중심으로부터의 거리 (Z축 앞)
}