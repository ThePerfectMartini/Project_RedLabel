using UnityEngine;

// 공격에 대한 모든 정보를 담는 구조체입니다.
public struct HitData
{
    public float damage;           // 데미지량
    public Vector3 knockbackForce; // 밀려나는 방향과 힘 (물리적인 힘)
    // 추후 필요하다면 여기에 hitStopDuration(역경직 시간), stunTime(기절 시간) 등을 추가할 수 있습니다.
}