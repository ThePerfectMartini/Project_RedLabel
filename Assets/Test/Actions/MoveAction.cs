using System;
using UnityEngine;

[Serializable]
public class MoveAction : ActionBase
{
    [Header("■ 이동 설정")]
    public bool isTeleport;
    public bool useJump;
    public float jumpForce = 5f;
    
    public float speed = 5f;
    public bool useAcceleration;
    public float startSpeed = 0f;
    public float acceleration = 2f;

    [Header("■ 목표 기준 설정")]
    public TargetType targetType;
    public Vector3 targetPosition;
    public MoveDirection8 moveDirection8 = MoveDirection8.None;
    
    public string targetTag;
    public bool trackXOnly;
    public bool trackZOnly;

    [Header("■ 타겟팅 부가 설정")]
    public Vector3 targetOffset = Vector3.zero;
    public bool usePositionRefresh;
    public float positionRefreshInterval = 0.5f;
    public bool repeatRefreshUntilReached = true;
    public int refreshRepeatCount = 3;

    [Header("종료 조건 1: 목표 좌표 도달")]
    public bool stopOnDestinationReached;
    public float destinationStopDistance = 0.1f;

    [Header("종료 조건 2: 이동 중 타겟 감지")]
    public bool stopOnTargetDetected;
    public DetectOrigin detectOrigin = DetectOrigin.Self;
    public DetectShape detectShape = DetectShape.Sphere;
    public Vector3 detectOffset = Vector3.zero;
    public float detectRadius = 1.5f;
    public Vector3 detectBoxSize = new Vector3(2f, 2f, 2f);

    [Header("종료 조건 3: 시간 제한")]
    public bool stopOnTimeLimit;
    public float timeLimit = 3f;

    public override ActionState CreateState(CapsuleController controller)
    {
        return new MoveState(controller, this);
    }

    public override string GetActionName() => "이동 (Move)";
}
