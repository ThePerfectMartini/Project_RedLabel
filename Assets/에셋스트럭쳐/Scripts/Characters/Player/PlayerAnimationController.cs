using UnityEngine;

public class PlayerAnimationController : AnimatorController
{
    protected Player _player;
    
    protected override void Awake()
    {
        base.Awake(); // 부모의 Awake를 호출하여 _animator 초기화
        _player = GetComponent<Player>();
    }
}
