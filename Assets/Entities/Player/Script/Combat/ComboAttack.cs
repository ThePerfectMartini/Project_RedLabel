using UnityEngine;

[CreateAssetMenu(fileName = "New Combo Attack", menuName = "Combat/Combo Attack Data")]
public class ComboAttack : ScriptableObject
{
    [Header("애니메이션 설정")]
    [Tooltip("실행할 애니메이션 상태 이름 (예: Attack 1)")]
    public string animationName;
    
    [Header("공격 판정 범위 (3D)")]
    [Tooltip("공격 판정 박스 크기")]
    public Vector3 attackBoxSize = new Vector3(2f, 2f, 3f);
    
    [Tooltip("캐릭터 중심 기준 공격 판정 위치 오프셋")]
    public Vector3 attackOffset = new Vector3(1.5f, 0f, 0f);
    
    [Header("공격시 전진 힘")]
    public float thrustPower = 3f;

    
    [Header("데미지 및 물리력")]
    public float damage = 10f;
    
    [Tooltip("적을 뒤로 밀어내는 힘")]
    public float knockbackPower = 5f;
    
    [Tooltip("적을 위로 띄우는 힘")]
    public float launchPower = 0f;
}