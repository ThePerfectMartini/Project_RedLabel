using UnityEngine;

// 인터페이스 이름은 보통 'I'로 시작합니다.
public interface IDamageable
{
    // 데미지를 입는 함수를 선언만 합니다. (구현은 상속받는 클래스에서 진행)
    void TakeDamage(HitData hitData);
}