using System.Collections.Generic;
using UnityEngine;

public enum ActionType { Move, Teleport, Wait, Attack }
// ▼ X, Z 전용 추적 타입을 제거하고 TrackObject로 통합
public enum TargetType { SpecificPosition, TrackObject, Direction }

public enum DistanceCheckMode { Distance3D, AttackBox }
public enum TargetShape { Sphere = 0, Box = 1, Point = 2 }
// ▼ 타겟 자체에 표시할 도형 Enum에서 Point 옵션 제거
public enum TargetGizmoShape { Sphere = 0, Box = 1 } 
public enum AttackShape { Sphere, Box } 

[System.Serializable]
public class ActionData
{
    public bool showGizmo;
    public ActionType actionType;
    public bool executeParallel;
    
    public bool playAnimation = true; 
    public string animationName;
    
    public TargetType targetType;
    public Vector3 targetPosition;
    public string targetTag; 

    // ▼ 오브젝트 추적 축 제한 체크박스 변수 추가
    public bool trackXOnly;
    public bool trackZOnly;
    
    // ▼ 타겟 오브젝트에 표시할 기즈모 도형 분리
    public TargetGizmoShape targetGizmoShape = TargetGizmoShape.Sphere;

    public Vector3 moveDirection = Vector3.forward; 
    
    public Vector3 offset;         
    public float trackMargin = 0f; 

    public float startSpeed = 0f;
    public float speed = 5f;
    public bool useAcceleration; 
    public float acceleration = 2f;

    public bool stopOnTargetReached;
    
    public DistanceCheckMode distanceMode = DistanceCheckMode.Distance3D; 
    public TargetShape targetShape = TargetShape.Sphere;
    public Vector3 shapeOffset = Vector3.zero;
    
    public float stopDistance = 0.1f;
    public Vector3 attackBoxSize = new Vector3(5f, 1f, 1f); 
    
    public bool stopOnTimeLimit;
    public float timeLimit = 3f; 

    [Header("공격 설정 (ActionType이 Attack일 때만 사용)")]
    public float damage = 10f;
    // ▼ float에서 Vector3로 변경하여 축별 넉백 힘 개별 설정 가능
    public Vector3 knockbackForce = new Vector3(15f, 5f, 15f); 
    public AttackShape attackShape = AttackShape.Sphere;
    public float attackRadius = 1.5f;
    public Vector3 attackHitBoxSize = new Vector3(2f, 1f, 2f);
    public Vector3 attackOffset = new Vector3(0, 1f, 1f); 
    
    public bool useWaitAfterAnimation; 
    public float waitDuration;         
}

[CreateAssetMenu(fileName = "NewActionSequence", menuName = "Action Sequence")]
public class ActionSequenceSO : ScriptableObject
{
    [Header("시퀀스 반복 설정")]
    public bool isInfiniteLoop;
    public int repeatCount = 1;

    public List<ActionData> actions = new List<ActionData>();

    private void OnValidate()
    {
        foreach (var action in actions)
        {
            if (action.speed <= 0f) action.speed = 0.01f;
            if (action.startSpeed < 0f) action.startSpeed = 0f;
            if (action.acceleration <= 0f) action.acceleration = 0.01f;
            if (action.timeLimit < 0f) action.timeLimit = 0f;
            
            if (action.playAnimation && string.IsNullOrEmpty(action.animationName))
            {
                switch (action.actionType)
                {
                    case ActionType.Move: action.animationName = "Move"; break;
                    case ActionType.Wait: action.animationName = "Idle"; break;
                    case ActionType.Attack: action.animationName = "Attack"; break;
                }
            }
        }
    }
}