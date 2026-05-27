using System.Collections.Generic;
using UnityEngine;

public enum ActionType { Move, Wait, VariableAttack, FixedAttack, RangedAttack }
public enum TargetType { SpecificPosition, TrackObject, Direction }
public enum MoveDirection8 { None, Up, Down, Left, Right, UpLeft, UpRight, DownLeft, DownRight, Forward, Backward }

public enum DetectOrigin { Self, Target }
public enum DetectShape { Sphere, Box }
public enum AttackShape { Sphere, Box, Cylinder } 

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
    
    public bool isTeleport;
    public MoveDirection8 moveDirection8 = MoveDirection8.None;
    
    public bool useJump;
    public float jumpForce = 5f;

    public bool trackXOnly;
    public bool trackZOnly;
    public bool snapToPlayerXAxis; // [원거리 투사체 + 오브젝트 추적 전용] 정확한 방향 대신 플레이어가 있는 좌/우로 스냅 발사
    
    
    // ▼ 목표 오프셋 (도착 지점을 정하는 유일한 오프셋)
    public Vector3 targetOffset = Vector3.zero;
    
    // ▼ 동적 새로고침
    public bool usePositionRefresh;
    public float positionRefreshInterval = 0.5f;
    public bool repeatRefreshUntilReached = true;
    public int refreshRepeatCount = 3;

    public float startSpeed = 0f;
    public float speed = 5f;
    public bool useAcceleration; 
    public float acceleration = 2f;

    // ▼ 종료 조건 1: 목표 좌표 도달 시 종료 (절대 좌표 기준)
    public bool stopOnDestinationReached;
    public float destinationStopDistance = 0.1f;
    
    // ▼ 종료 조건 2: 이동 중 타겟 감지 시 종료 (오브젝트 추적 모드에서만 유효)
    public bool stopOnTargetDetected;
    public DetectOrigin detectOrigin = DetectOrigin.Self;
    public DetectShape detectShape = DetectShape.Sphere;
    public Vector3 detectOffset = Vector3.zero;
    public float detectRadius = 1.5f;
    public Vector3 detectBoxSize = new Vector3(2f, 2f, 2f);

    // ▼ 종료 조건 3: 시간 제한
    public bool stopOnTimeLimit;
    public float timeLimit = 3f; 

    public float damage = 10f;
    public Vector3 knockbackForce = new Vector3(15f, 5f, 15f); 
    public AttackShape attackShape = AttackShape.Sphere;
    public float attackRadius = 1.5f;
    public float attackHeight = 2f; 
    public Vector3 attackHitBoxSize = new Vector3(2f, 1f, 2f);
    public Vector3 attackOffset = new Vector3(0, 1f, 1f); 
    
    public GameObject projectilePrefab;
    public int projectileCount = 1;
    public float projectileInterval = 0.1f;
    public float projectileSpeed = 15f;
    public float projectileLifeTime = 3f;

    // ── 전투 확장 필드 (CombatHitData.Create()와 연동) ────────────────
    [Header("타격 연출 · 상호작용")]
    [Tooltip("피격 경직 시간(초). 이 값만큼 피격자의 시퀀스 진행이 멈춥니다.")]
    public float hitStunDuration = 0.15f;
    [Tooltip("히트스톱 시간(초). 0이면 기본값을 사용합니다.")]
    public float hitStopDuration = 0f;
    [Tooltip("그로기 게이지 축적량. 높을수록 적이 빠르게 그로기 상태가 됩니다.")]
    public float staggerValue = 10f;
    [Tooltip("에어 런처 여부. true면 피격체를 공중으로 띄웁니다.")]
    public bool isLauncher = false;
    [Tooltip("패링(저스트 가드) 가능 여부. false면 패링으로 막을 수 없습니다.")]
    public bool canBeParried = true;
    [Tooltip("가드 불능 여부. true면 가드/패링 모두 뚫고 피해를 줍니다.")]
    public bool isUnblockable = false;

    [Header("공격 중 이동 설정")]
    [Tooltip("공격 동작 중에도 캐릭터가 이동할 수 있도록 허용합니다. 기본값은 차단입니다.")]
    public bool allowMoveWhileAttacking = false;
    [Tooltip("true면 현재 바라보는 방향(앞)으로 이동 입력이 있을 때만 전진합니다. false면 전 방향 이동을 허용합니다.")]
    public bool forwardMoveOnly = true;
    [Tooltip("공격 중 이동 속도. 일반 이동 속도보다 낮게 설정하는 것을 권장합니다.")]
    public float attackMoveSpeed = 2f;

    [Header("공격 중 동적 이동 제어")]
    [Tooltip("공격 시 몬스터/플레이어가 기본 제공하는 자동 이동 및 관성 물리를 적용할지 여부")]
    public bool enableDynamicMovement = false;

    [Tooltip("공격 시 입력이 없어도 기본적으로 이동하는 자동 속도 (양수 = 전진, 음수 = 후진, 최종 속도)")]
    public float autoMoveSpeed = 0f;

    [Tooltip("공격 시작 시점의 초기 이동 속도. 가감속 Lerp 사용 시에만 유효합니다.")]
    public float startMoveSpeed = 0f;



    [Tooltip("반대 방향 입력 시 제동 여부. true이면 플레이어가 반대(뒤) 방향 입력을 주면 속도가 감속됩니다.")]
    public bool brakeOnOppositeInput = false;
    [Tooltip("반대 방향 입력 시 제동할(감속할) 속도 값.")]
    public float oppositeBrakeSpeed = 2f;

    [Tooltip("순방향 입력 시 가속 여부. true이면 플레이어가 진행 방향(앞) 입력을 주면 속도가 추가로 빨라집니다.")]
    public bool accelerateOnForwardInput = false;

    [Tooltip("순방향 입력 시 증가할 속도 값.")]
    public float forwardAccelerationSpeed = 2f;

    [Header("공격 중 물리 및 연출 제어")]
    [Tooltip("시작 시 즉시 타격 판정을 수행할지 여부. 애니메이션 이벤트(OnAttackImpact)로 데미지를 가하고 싶다면 이를 비활성화(false) 하십시오.")]
    public bool castDamageOnStart = true;

    [Tooltip("공격과 동시에 물리적 점프(도약)를 적용합니다. (예: 승룡권, 도약 베기 등)")]
    public bool useJumpInAttack = false;

    [Tooltip("공격 중 도약할 점프 힘(도약 높이)")]
    public float attackJumpForce = 5f;

    [Tooltip("공격 중 속도 변화 시 부드러운 가감속(Lerp 보간)을 적용할지 여부")]
    public bool useLerpMovement = false;


}

[CreateAssetMenu(fileName = "NewActionSequence", menuName = "Action Sequence")]
public class ActionSequenceSO : ScriptableObject
{
    public List<ActionData> actions = new List<ActionData>();

    private void OnValidate()
    {
        foreach (var action in actions)
        {
            if (action.speed <= 0f) action.speed = 0.01f;
            if (action.timeLimit < 0f) action.timeLimit = 0f;

            // 가속도 미사용 시 관련 값 초기화
            if (!action.useAcceleration)
            {
                action.startSpeed = 0f;
                action.acceleration = 0.01f;
            }
            else
            {
                if (action.startSpeed < 0f) action.startSpeed = 0f;
                if (action.acceleration <= 0f) action.acceleration = 0.01f;
            }
            
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

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(ActionData))]
public class ActionDataDrawer : UnityEditor.PropertyDrawer
{
    private float headerHeight => UnityEditor.EditorGUIUtility.singleLineHeight + 10f;
    private float dividerHeight => 11f;

    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        UnityEditor.EditorGUI.BeginProperty(position, label, property);
        
        Rect rect = position;
        rect.height = UnityEditor.EditorGUIUtility.singleLineHeight;
        
        // Foldout 텍스트 커스텀: [이동] Action 0
        string actionName = "액션";
        UnityEditor.SerializedProperty actionTypeProp = property.FindPropertyRelative("actionType");
        if (actionTypeProp != null)
        {
            ActionType at = (ActionType)actionTypeProp.enumValueIndex;
            actionName = at.ToString();
            switch (at)
            {
                case ActionType.Move: actionName = "이동"; break;
                case ActionType.Wait: actionName = "대기"; break;
                case ActionType.VariableAttack: actionName = "변동 타격"; break;
                case ActionType.FixedAttack: actionName = "고정 타격"; break;
                case ActionType.RangedAttack: actionName = "원거리 타격"; break;
            }
        }
        string title = $"[{actionName}] {label.text}";
        
        property.isExpanded = UnityEditor.EditorGUI.Foldout(rect, property.isExpanded, new GUIContent(title), true);
        rect.y += UnityEditor.EditorGUIUtility.singleLineHeight + 2;

        if (property.isExpanded)
        {
            UnityEditor.EditorGUI.indentLevel++;

            DrawProperty(ref rect, property, "executeParallel", "병렬 실행 (동시 실행)");
            
            DrawDivider(ref rect);
            
            DrawProperty(ref rect, property, "playAnimation", "애니메이션 재생");
            if (property.FindPropertyRelative("playAnimation").boolValue)
                DrawProperty(ref rect, property, "animationName", " ㄴ 애니메이션 이름");
            
            DrawDivider(ref rect);

            string[] actionTypeNames = { "이동 (Move)", "대기 (Wait)", "변동 좌표 타격", "고정 좌표 타격", "원거리 투사체 공격" };
            DrawPopup(ref rect, actionTypeProp, "액션 타입", actionTypeNames);

            ActionType actionType = (ActionType)actionTypeProp.enumValueIndex;

            if (actionType == ActionType.Move)
            {
                DrawHeader(ref rect, "▶ 이동 설정");
                DrawProperty(ref rect, property, "isTeleport", "순간이동(Teleport) 모드");
                
                UnityEditor.SerializedProperty isTeleportProp = property.FindPropertyRelative("isTeleport");
                if (!isTeleportProp.boolValue)
                {
                    DrawProperty(ref rect, property, "useJump", "점프 사용");
                    if (property.FindPropertyRelative("useJump").boolValue)
                        DrawProperty(ref rect, property, "jumpForce", " ㄴ 점프 힘");
                    
                    DrawProperty(ref rect, property, "speed", "속도");
                    DrawProperty(ref rect, property, "useAcceleration", "가속도 사용");
                    if (property.FindPropertyRelative("useAcceleration").boolValue)
                    {
                        DrawProperty(ref rect, property, "startSpeed", " ㄴ 시작 속도");
                        DrawProperty(ref rect, property, "acceleration", " ㄴ 가속도");
                    }
                }

                DrawHeader(ref rect, "▶ 목표 기준 설정");
                UnityEditor.SerializedProperty targetTypeProp = property.FindPropertyRelative("targetType");
                string[] targetTypeNames = { "지정 좌표", "오브젝트 추적", "특정 방향 고정" };
                DrawPopup(ref rect, targetTypeProp, "목표 기준", targetTypeNames);

                TargetType targetType = (TargetType)targetTypeProp.enumValueIndex;

                if (targetType == TargetType.SpecificPosition)
                {
                    DrawXZField(ref rect, property, "targetPosition", "지정 좌표");
                }
                else if (targetType == TargetType.Direction)
                {
                    DrawProperty(ref rect, property, "moveDirection8", "이동 방향");
                }
                else 
                {
                    DrawProperty(ref rect, property, "targetTag", "추적할 타겟 태그");
                    DrawProperty(ref rect, property, "trackXOnly", "X축만 추적");
                    DrawProperty(ref rect, property, "trackZOnly", "Z축만 추적");
                }

                if (targetType != TargetType.Direction)
                {
                    DrawHeader(ref rect, "▶ 타겟팅 부가 설정");
                    DrawXZField(ref rect, property, "targetOffset", "목표 오프셋");
                    
                    if (!property.FindPropertyRelative("isTeleport").boolValue)
                    {
                        DrawProperty(ref rect, property, "usePositionRefresh", "동적 새로고침 사용");
                        if (property.FindPropertyRelative("usePositionRefresh").boolValue)
                        {
                            DrawProperty(ref rect, property, "positionRefreshInterval", " ㄴ 새로고침 간격(초)");
                            DrawProperty(ref rect, property, "repeatRefreshUntilReached", " ㄴ 도착 시까지 반복");
                            if (!property.FindPropertyRelative("repeatRefreshUntilReached").boolValue)
                            {
                                DrawProperty(ref rect, property, "refreshRepeatCount", " ㄴ 반복 횟수");
                            }
                        }
                    }
                }

                DrawHeader(ref rect, "▶ 종료 조건 설정");

                // 종료 조건 1: 목표 좌표 도달 시 종료
                DrawProperty(ref rect, property, "stopOnDestinationReached", "목표 좌표 도달 시 종료");
                if (property.FindPropertyRelative("stopOnDestinationReached").boolValue)
                {
                    DrawProperty(ref rect, property, "destinationStopDistance", " ㄴ 정지 허용 오차");
                }

                bool isTrackingMode = (targetType == TargetType.TrackObject);

                // 종료 조건 2: 타겟 감지 시 종료 (오브젝트 추적 모드에서만 표시)
                if (isTrackingMode)
                {
                    DrawProperty(ref rect, property, "stopOnTargetDetected", "이동 중 타겟 감지 시 종료");
                    if (property.FindPropertyRelative("stopOnTargetDetected").boolValue)
                    {
                        UnityEditor.SerializedProperty detectOriginProp = property.FindPropertyRelative("detectOrigin");
                        string[] detectOriginNames = { "자신 오브젝트 기준", "타겟 오브젝트 기준" };
                        DrawPopup(ref rect, detectOriginProp, " ㄴ 감지 기준", detectOriginNames);

                        UnityEditor.SerializedProperty detectShapeProp = property.FindPropertyRelative("detectShape");
                        string[] detectShapeNames = { "원형 (Sphere)", "사각형 (Box)" };
                        DrawPopup(ref rect, detectShapeProp, " ㄴ 감지 도형", detectShapeNames);

                        DrawProperty(ref rect, property, "detectOffset", " ㄴ 감지 도형 오프셋");

                        if (detectShapeProp.enumValueIndex == (int)DetectShape.Sphere)
                        {
                            DrawProperty(ref rect, property, "detectRadius", " ㄴ 감지 반지름");
                        }
                        else
                        {
                            DrawProperty(ref rect, property, "detectBoxSize", " ㄴ 감지 박스 크기");
                        }
                    }
                }

                // 종료 조건 3: 시간 제한
                DrawProperty(ref rect, property, "stopOnTimeLimit", "시간 제한으로 종료");
                if (property.FindPropertyRelative("stopOnTimeLimit").boolValue)
                    DrawProperty(ref rect, property, "timeLimit", " ㄴ 제한 시간(초)");
            }
            else if (actionType == ActionType.Wait)
            {
                DrawHeader(ref rect, "▶ 대기 설정");
                DrawProperty(ref rect, property, "timeLimit", "대기 시간(초)");
            }
            else 
            {
                DrawHeader(ref rect, "▶ 공격 기본 설정");
                
                if (actionType == ActionType.RangedAttack)
                {
                    UnityEditor.SerializedProperty targetTypeProp = property.FindPropertyRelative("targetType");
                    string[] targetTypeNames = { "지정 좌표", "오브젝트 추적", "특정 방향 고정" };
                    DrawPopup(ref rect, targetTypeProp, "타겟 기준", targetTypeNames);

                    TargetType targetType = (TargetType)targetTypeProp.enumValueIndex;
                    if (targetType == TargetType.SpecificPosition)
                    {
                        DrawXZField(ref rect, property, "targetPosition", "지정 좌표");
                    }
                    else if (targetType == TargetType.Direction)
                    {
                        DrawProperty(ref rect, property, "moveDirection8", "발사 방향");
                    }
                    else // TrackObject
                    {
                        DrawProperty(ref rect, property, "targetTag", "타겟 태그");
                        DrawProperty(ref rect, property, "snapToPlayerXAxis", "X축 방향 스냅 (좌/우 자동 판단)");
                    }
                }
                else if (actionType == ActionType.FixedAttack)
                {
                    DrawXZField(ref rect, property, "targetPosition", "지정 좌표");
                }

                DrawProperty(ref rect, property, "damage", "데미지");
                DrawProperty(ref rect, property, "knockbackForce", "넉백 힘 (X, Y, Z)");
                DrawProperty(ref rect, property, "attackOffset", "공격/생성 오프셋");
                
                // ── 추가된 부분: 전투 상호작용 피처 노출 ──
                DrawHeader(ref rect, "▶ 전투 상호작용 설정");
                DrawProperty(ref rect, property, "hitStunDuration", "피격 경직 시간(초)");
                DrawProperty(ref rect, property, "hitStopDuration", "히트스톱 시간(초)");
                DrawProperty(ref rect, property, "staggerValue", "그로기 게이지 축적량");
                DrawProperty(ref rect, property, "isLauncher", "에어 런처 여부 (공중 띄우기)");
                DrawProperty(ref rect, property, "canBeParried", "패링 가능 여부");
                DrawProperty(ref rect, property, "isUnblockable", "가드 불가 여부");

                DrawHeader(ref rect, "▶ 공격 중 이동 & 물리 제어 (통합)");
                
                // 1. 수동 조작 이동
                DrawProperty(ref rect, property, "allowMoveWhileAttacking", "수동 조작 이동 허용");
                if (property.FindPropertyRelative("allowMoveWhileAttacking").boolValue)
                {
                    DrawProperty(ref rect, property, "forwardMoveOnly", " └ 앞 방향으로만 제한");
                    DrawProperty(ref rect, property, "attackMoveSpeed", " └ 조작 이동 속도");
                }

                // 2. 자동 이동/돌진 (동적 이동 제어)
                DrawProperty(ref rect, property, "enableDynamicMovement", "동적 이동 제어 허용");
                if (property.FindPropertyRelative("enableDynamicMovement").boolValue)
                {
                    DrawProperty(ref rect, property, "autoMoveSpeed", " └ 자동 이동 속도 (최종)");
                    DrawProperty(ref rect, property, "brakeOnOppositeInput", " └ 반대 방향 입력 시 제동");
                    if (property.FindPropertyRelative("brakeOnOppositeInput").boolValue)
                    {
                        DrawProperty(ref rect, property, "oppositeBrakeSpeed", "    └ 제동 속도 수치");
                    }
                    DrawProperty(ref rect, property, "accelerateOnForwardInput", " └ 순방향 입력 시 가속");
                    if (property.FindPropertyRelative("accelerateOnForwardInput").boolValue)
                    {
                        DrawProperty(ref rect, property, "forwardAccelerationSpeed", "    └ 추가 가속 속도");
                    }

                    // 4. 가감속 Lerp 보간 설정 (동적 이동 하위로 이동)
                    DrawProperty(ref rect, property, "useLerpMovement", " └ 가감속 부드럽게 (Lerp)");
                    if (property.FindPropertyRelative("useLerpMovement").boolValue)
                    {
                        DrawProperty(ref rect, property, "startMoveSpeed", "    └ 출발 이동 속도");
                    }
                }

                // 3. 점프/도약 설정
                DrawProperty(ref rect, property, "useJumpInAttack", "공격 시 점프(도약) 사용");
                if (property.FindPropertyRelative("useJumpInAttack").boolValue)
                {
                    DrawProperty(ref rect, property, "attackJumpForce", " └ 점프 힘 (도약 높이)");
                }

                // 5. 타격 이벤트 개시 시점 제어 (이중 판정 방지)
                DrawProperty(ref rect, property, "castDamageOnStart", "공격 시작 시 즉시 타격 판정");

                if (actionType == ActionType.RangedAttack)
                {
                    DrawHeader(ref rect, "▶ 투사체 설정");
                    DrawProperty(ref rect, property, "projectilePrefab", "투사체 프리팹");
                    DrawProperty(ref rect, property, "projectileCount", "발사 갯수");
                    DrawProperty(ref rect, property, "projectileInterval", "발사 간격(초)");
                    DrawProperty(ref rect, property, "projectileSpeed", "투사체 속도");
                    DrawProperty(ref rect, property, "projectileLifeTime", "투사체 수명(초)");
                }
                else
                {
                    DrawHeader(ref rect, "▶ 타격 범위 설정");
                    UnityEditor.SerializedProperty attackShapeProp = property.FindPropertyRelative("attackShape");
                    string[] attackShapeNames = { "원형 (Sphere)", "사각형 (Box)", "원기둥 (Cylinder)" };
                    DrawPopup(ref rect, attackShapeProp, "공격 범위 도형", attackShapeNames);

                    AttackShape shape = (AttackShape)attackShapeProp.enumValueIndex;
                    if (shape == AttackShape.Sphere)
                    {
                        DrawProperty(ref rect, property, "attackRadius", "공격 반지름");
                    }
                    else if (shape == AttackShape.Cylinder)
                    {
                        DrawProperty(ref rect, property, "attackRadius", "공격 반지름");
                        DrawProperty(ref rect, property, "attackHeight", "공격 높이(원기둥)");
                    }
                    else if (shape == AttackShape.Box)
                    {
                        DrawProperty(ref rect, property, "attackHitBoxSize", "공격 박스 크기");
                    }
                }
            }
            
            DrawDivider(ref rect);
            UnityEditor.EditorGUI.indentLevel--;
        }
        
        UnityEditor.EditorGUI.EndProperty();
    }

    private void DrawPopup(ref Rect rect, UnityEditor.SerializedProperty prop, string label, string[] options)
    {
        Rect pRect = rect;
        pRect.height = UnityEditor.EditorGUIUtility.singleLineHeight;
        prop.enumValueIndex = UnityEditor.EditorGUI.Popup(pRect, label, prop.enumValueIndex, options);
        rect.y += UnityEditor.EditorGUIUtility.singleLineHeight + 2;
    }

    private void DrawHeader(ref Rect rect, string title)
    {
        rect.y += 5f;
        Rect headerRect = rect;
        headerRect.height = UnityEditor.EditorGUIUtility.singleLineHeight;
        UnityEditor.EditorGUI.LabelField(headerRect, title, UnityEditor.EditorStyles.boldLabel);
        rect.y += UnityEditor.EditorGUIUtility.singleLineHeight + 5f;
    }

    private void DrawDivider(ref Rect rect)
    {
        rect.y += 5f;
        Rect divRect = new Rect(rect.x + 15f, rect.y, rect.width - 15f, 1f);
        UnityEditor.EditorGUI.DrawRect(divRect, new Color(0.5f, 0.5f, 0.5f, 1f));
        rect.y += 6f;
    }

    private void DrawProperty(ref Rect rect, UnityEditor.SerializedProperty parent, string propName, string customLabel = null)
    {
        UnityEditor.SerializedProperty prop = parent.FindPropertyRelative(propName);
        if (prop != null)
        {
            Rect r = rect;
            r.height = UnityEditor.EditorGUI.GetPropertyHeight(prop, true);
            if (customLabel != null)
            {
                UnityEditor.EditorGUI.PropertyField(r, prop, new GUIContent(customLabel), true);
            }
            else
            {
                UnityEditor.EditorGUI.PropertyField(r, prop, true);
            }
            rect.y += r.height + 2;
        }
    }

    private void DrawXZField(ref Rect rect, UnityEditor.SerializedProperty parent, string propName, string label)
    {
        UnityEditor.SerializedProperty prop = parent.FindPropertyRelative(propName);
        if (prop != null)
        {
            Rect r = rect;
            r.height = UnityEditor.EditorGUIUtility.singleLineHeight;
            float[] values = new float[] { prop.vector3Value.x, prop.vector3Value.z };
            GUIContent[] subLabels = new GUIContent[] { new GUIContent("X"), new GUIContent("Z") };
            
            UnityEditor.EditorGUI.MultiFloatField(r, new GUIContent(label), subLabels, values);
            
            prop.vector3Value = new Vector3(values[0], prop.vector3Value.y, values[1]);
            rect.y += r.height + 2;
        }
    }

    public override float GetPropertyHeight(UnityEditor.SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return UnityEditor.EditorGUIUtility.singleLineHeight;
            
        float height = UnityEditor.EditorGUIUtility.singleLineHeight + 2;

        bool playAnim = property.FindPropertyRelative("playAnimation").boolValue;
        
        ActionType actionType = (ActionType)property.FindPropertyRelative("actionType").enumValueIndex;

        height += AddProp(property, "executeParallel");
        height += dividerHeight;
        height += AddProp(property, "playAnimation");
        if (playAnim) height += AddProp(property, "animationName");
        
        height += dividerHeight;
        height += AddProp(property, "actionType"); 

        if (actionType == ActionType.Move)
        {
            height += headerHeight;
            height += AddProp(property, "isTeleport");
            bool isTeleport = property.FindPropertyRelative("isTeleport").boolValue;
            if (!isTeleport)
            {
                height += AddProp(property, "useJump");
                if (property.FindPropertyRelative("useJump").boolValue)
                    height += AddProp(property, "jumpForce");
                
                height += AddPropsCount(property, "speed", "useAcceleration");
                if (property.FindPropertyRelative("useAcceleration").boolValue)
                    height += AddPropsCount(property, "startSpeed", "acceleration");
            }

            height += headerHeight;
            height += AddProp(property, "targetType"); 
            TargetType targetType = (TargetType)property.FindPropertyRelative("targetType").enumValueIndex;

            if (targetType == TargetType.SpecificPosition)
            {
                float vHeight = UnityEditor.EditorGUIUtility.singleLineHeight;
                height += vHeight + 2;
            }
            else if (targetType == TargetType.Direction) height += AddProp(property, "moveDirection8");
            else height += AddPropsCount(property, "targetTag", "trackXOnly", "trackZOnly");

            if (targetType != TargetType.Direction)
            {
                height += headerHeight;
                height += UnityEditor.EditorGUIUtility.singleLineHeight + 2;
                if (!property.FindPropertyRelative("isTeleport").boolValue)
                {
                    height += AddProp(property, "usePositionRefresh");
                    if (property.FindPropertyRelative("usePositionRefresh").boolValue)
                    {
                        height += AddPropsCount(property, "positionRefreshInterval", "repeatRefreshUntilReached");
                        if (!property.FindPropertyRelative("repeatRefreshUntilReached").boolValue)
                            height += AddProp(property, "refreshRepeatCount");
                    }
                }
            }

            // ▶ 종료 조건 설정
            height += headerHeight;
            
            // 종료 조건 1: 목표 좌표 도달
            height += AddProp(property, "stopOnDestinationReached");
            if (property.FindPropertyRelative("stopOnDestinationReached").boolValue)
            {
                height += AddProp(property, "destinationStopDistance");
            }

            bool isTrackingMode = (targetType == TargetType.TrackObject);

            // 종료 조건 2: 타겟 감지 (추적 모드에서만)
            if (isTrackingMode)
            {
                height += AddProp(property, "stopOnTargetDetected");
                if (property.FindPropertyRelative("stopOnTargetDetected").boolValue)
                {
                    height += (UnityEditor.EditorGUIUtility.singleLineHeight + 2) * 2; // For 2 popups
                    height += AddProp(property, "detectOffset");

                    if (property.FindPropertyRelative("detectShape").enumValueIndex == (int)DetectShape.Sphere)
                        height += AddProp(property, "detectRadius");
                    else
                        height += AddProp(property, "detectBoxSize");
                }
            }

            // 종료 조건 3: 시간 제한
            height += AddProp(property, "stopOnTimeLimit");
            if (property.FindPropertyRelative("stopOnTimeLimit").boolValue)
                height += AddProp(property, "timeLimit");
        }
        else if (actionType == ActionType.Wait)
        {
            height += headerHeight;
            height += AddProp(property, "timeLimit");
        }
        else 
        {
            height += headerHeight;
            
            if (actionType == ActionType.RangedAttack)
            {
                height += AddProp(property, "targetType"); 
                TargetType targetType = (TargetType)property.FindPropertyRelative("targetType").enumValueIndex;
                if (targetType == TargetType.SpecificPosition) height += UnityEditor.EditorGUIUtility.singleLineHeight + 2;
                else if (targetType == TargetType.Direction)   height += AddProp(property, "moveDirection8");
                else
                {
                    height += AddProp(property, "targetTag");
                    height += AddProp(property, "snapToPlayerXAxis");
                }
            }
            else if (actionType == ActionType.FixedAttack)
            {
                height += UnityEditor.EditorGUIUtility.singleLineHeight + 2;
            }

            height += AddPropsCount(property, "damage", "knockbackForce", "attackOffset");
            
            // ── 추가된 부분: 높이 계산 추가 ──
            height += headerHeight;
            height += AddPropsCount(property, "hitStunDuration", "hitStopDuration", "staggerValue", "isLauncher", "canBeParried", "isUnblockable");

            height += headerHeight; // ▶ 공격 중 이동 & 물리 제어 (통합)
            
            // 1. 수동 조작 이동
            height += AddProp(property, "allowMoveWhileAttacking");
            if (property.FindPropertyRelative("allowMoveWhileAttacking").boolValue)
                height += AddPropsCount(property, "forwardMoveOnly", "attackMoveSpeed");

            // 2. 자동 이동/돌진 (동적 이동 제어)
            height += AddProp(property, "enableDynamicMovement");
            if (property.FindPropertyRelative("enableDynamicMovement").boolValue)
            {
                height += AddPropsCount(property, "autoMoveSpeed", "brakeOnOppositeInput");
                if (property.FindPropertyRelative("brakeOnOppositeInput").boolValue)
                    height += AddProp(property, "oppositeBrakeSpeed");

                height += AddProp(property, "accelerateOnForwardInput");
                if (property.FindPropertyRelative("accelerateOnForwardInput").boolValue)
                    height += AddProp(property, "forwardAccelerationSpeed");

                // 4. 가감속 Lerp 설정
                height += AddProp(property, "useLerpMovement");
                if (property.FindPropertyRelative("useLerpMovement").boolValue)
                    height += AddProp(property, "startMoveSpeed");
            }

            // 3. 점프/도약 설정
            height += AddProp(property, "useJumpInAttack");
            if (property.FindPropertyRelative("useJumpInAttack").boolValue)
                height += AddProp(property, "attackJumpForce");

            // 5. 타격 개시 제어
            height += AddProp(property, "castDamageOnStart");
            
            if (actionType == ActionType.RangedAttack)
            {
                height += headerHeight;
                height += AddPropsCount(property, "projectilePrefab", "projectileCount", "projectileInterval", "projectileSpeed", "projectileLifeTime");
            }
            else
            {
                height += headerHeight;
                height += AddProp(property, "attackShape"); 
                AttackShape shape = (AttackShape)property.FindPropertyRelative("attackShape").enumValueIndex;
                if (shape == AttackShape.Sphere) height += AddProp(property, "attackRadius");
                else if (shape == AttackShape.Cylinder) height += AddPropsCount(property, "attackRadius", "attackHeight");
                else if (shape == AttackShape.Box) height += AddProp(property, "attackHitBoxSize");
            }
        }

        height += dividerHeight;
        return height;
    }

    private float AddProp(UnityEditor.SerializedProperty parent, string propName)
    {
        var p = parent.FindPropertyRelative(propName);
        if (p == null) return 0;
        return UnityEditor.EditorGUI.GetPropertyHeight(p, true) + 2;
    }
    
    private float AddPropsCount(UnityEditor.SerializedProperty parent, params string[] propNames)
    {
        float h = 0;
        foreach(var name in propNames) h += AddProp(parent, name);
        return h;
    }
}
#endif