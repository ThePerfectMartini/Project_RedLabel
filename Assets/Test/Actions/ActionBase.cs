using System;
using UnityEngine;

[Serializable]
public abstract class ActionBase
{
    [Header("■ 기본 설정")]
    public bool showGizmo = true;
    public bool executeParallel = false;
    
    [Header("■ 애니메이션 설정")]
    public bool playAnimation = true;
    public string animationName;

    // 다형성 기반 팩토리 메서드
    public abstract ActionState CreateState(CapsuleController controller);

    // 에디터에서 식별하기 위한 액션 이름 (선택적)
    public virtual string GetActionName() => "Action";
}
