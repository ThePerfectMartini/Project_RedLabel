using System.Collections.Generic;
using UnityEngine;

public enum ActionType { Move, Teleport, Wait, VariableAttack, FixedAttack, RangedAttack }
public enum TargetType { SpecificPosition, TrackObject, Direction, TrackObjectXOnly, TrackObjectZOnly }

public enum DistanceCheckMode { Distance3D, AttackBox }
public enum TargetShape { Sphere, Box, Point }
public enum AttackShape { Sphere, Box, Cylinder } 
public enum TargetGizmoShape { Sphere = 0, Box = 1 } 

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
    
    public Vector3 moveDirection = Vector3.forward; 
    
    public Vector3 offset;         
    public float trackMargin = 0f; 

    public bool trackXOnly;
    public bool trackZOnly;
    public TargetGizmoShape targetGizmoShape = TargetGizmoShape.Sphere;

    public float startSpeed = 0f;
    public float speed = 5f;
    public bool useAcceleration; 
    public float acceleration = 2f;

    public bool stopOnTargetReached;
    
    public DistanceCheckMode distanceMode = DistanceCheckMode.Distance3D; 
    public TargetShape targetShape = TargetShape.Sphere;
    public Vector3 shapeOffset = Vector3.zero;
    public Vector3 attackBoxSize = new Vector3(1f, 1f, 1f);
    public float stopDistance = 0.1f; 

    public bool stopOnTimeLimit;
    public float timeLimit = 3f; 

    public float damage = 10f;
    public Vector3 knockbackForce = new Vector3(15f, 5f, 15f); 
    public AttackShape attackShape = AttackShape.Sphere;
    public float attackRadius = 1.5f;
    public float attackHeight = 2f; 
    public Vector3 attackHitBoxSize = new Vector3(2f, 1f, 2f);
    public Vector3 attackOffset = new Vector3(0, 1f, 1f); 
    
    // ▼ 원거리 투사체 설정 (발사 갯수, 간격 추가됨)
    public GameObject projectilePrefab;
    public int projectileCount = 1;
    public float projectileInterval = 0.1f;
    public float projectileSpeed = 15f;
    public float projectileLifeTime = 3f;
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
            
            // ▼ 발사 갯수는 최소 1개, 간격은 0초 이상 보장
            if (action.projectileCount < 1) action.projectileCount = 1;
            if (action.projectileInterval < 0f) action.projectileInterval = 0f;
            
            if (action.playAnimation && string.IsNullOrEmpty(action.animationName))
            {
                switch (action.actionType)
                {
                    case ActionType.Move: action.animationName = "Walk"; break;
                    case ActionType.VariableAttack: 
                    case ActionType.FixedAttack: action.animationName = "Attack"; break;
                    case ActionType.RangedAttack: action.animationName = "Shoot"; break;
                    case ActionType.Wait: action.animationName = "Idle"; break;
                }
            }
        }
    }
}