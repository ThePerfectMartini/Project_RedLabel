using UnityEngine;

public class EnemyAnimationController : AnimatorController
{
    protected Enemy _enemy;
    
    protected override void Awake()
    {
        base.Awake(); // 부모의 Awake를 호출하여 _animator 초기화
        _enemy = GetComponent<Enemy>();
    }
}