using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ActionData))]
public class ActionSequenceDrawer : PropertyDrawer
{
    private readonly float lineHeight = EditorGUIUtility.singleLineHeight;
    private readonly float spacing = 6f; 
    private readonly float headerSpacing = 14f; 
    private readonly float verticalPadding = 10f; 
    
    private readonly float dividerHeight = 2f;   
    private readonly float dividerSpacing = 24f; 

    private readonly string[] actionDisplayNames = { "이동", "텔레포트", "대기", "공격" };
    // ▼ 배열에서 X, Z 전용 추적을 제거
    private readonly string[] targetDisplayNames = { "지정 좌표", "오브젝트(태그) 추적", "특정 방향 이동" };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        
        var showGizmo = property.FindPropertyRelative("showGizmo");
        var actionType = property.FindPropertyRelative("actionType");
        var executeParallel = property.FindPropertyRelative("executeParallel"); 
        
        var playAnimation = property.FindPropertyRelative("playAnimation"); 
        var animationName = property.FindPropertyRelative("animationName");
        
        var useAcceleration = property.FindPropertyRelative("useAcceleration");
        var targetType = property.FindPropertyRelative("targetType");
        var targetPosition = property.FindPropertyRelative("targetPosition");
        var targetTag = property.FindPropertyRelative("targetTag"); 
        var moveDirection = property.FindPropertyRelative("moveDirection"); 
        
        var offset = property.FindPropertyRelative("offset");
        var trackMargin = property.FindPropertyRelative("trackMargin"); 
        
        var startSpeed = property.FindPropertyRelative("startSpeed");
        var speed = property.FindPropertyRelative("speed");
        var acceleration = property.FindPropertyRelative("acceleration");
        
        var stopOnTargetReached = property.FindPropertyRelative("stopOnTargetReached");
        
        var distanceMode = property.FindPropertyRelative("distanceMode");
        var targetShape = property.FindPropertyRelative("targetShape"); 
        var shapeOffset = property.FindPropertyRelative("shapeOffset"); 
        var stopDistance = property.FindPropertyRelative("stopDistance");
        var attackBoxSize = property.FindPropertyRelative("attackBoxSize"); 
        
        var stopOnTimeLimit = property.FindPropertyRelative("stopOnTimeLimit");
        var timeLimit = property.FindPropertyRelative("timeLimit");

        var damage = property.FindPropertyRelative("damage");
        var knockbackForce = property.FindPropertyRelative("knockbackForce"); // ▼ 넉백 힘 변수 가져오기
        var attackShape = property.FindPropertyRelative("attackShape");
        var attackRadius = property.FindPropertyRelative("attackRadius");
        var attackHitBoxSize = property.FindPropertyRelative("attackHitBoxSize");
        var attackOffset = property.FindPropertyRelative("attackOffset");

        var useWaitAfterAnimation = property.FindPropertyRelative("useWaitAfterAnimation");
        var waitDuration = property.FindPropertyRelative("waitDuration");

        Rect rect = new Rect(position.x, position.y + (verticalPadding / 2), position.width, lineHeight);

        EditorGUI.PropertyField(rect, showGizmo, new GUIContent("기즈모 표시"));
        rect.y += lineHeight + spacing;

        EditorGUI.LabelField(rect, "방식 설정", EditorStyles.boldLabel);
        rect.y += lineHeight + spacing;
        
        EditorGUI.BeginChangeCheck();
        actionType.enumValueIndex = EditorGUI.Popup(rect, "행동 방식", actionType.enumValueIndex, actionDisplayNames);
        
        if (EditorGUI.EndChangeCheck())
        {
            switch ((ActionType)actionType.enumValueIndex)
            {
                case ActionType.Move: animationName.stringValue = "Move"; break;
                case ActionType.Teleport: animationName.stringValue = "Teleport"; break;
                case ActionType.Wait: animationName.stringValue = "Idle"; break;
                case ActionType.Attack: animationName.stringValue = "Attack"; break;
            }
        }
        
        rect.y += lineHeight + spacing;
        
        EditorGUI.PropertyField(rect, executeParallel, new GUIContent("동시 실행 (완료 대기 안 함)"));
        rect.y += lineHeight + spacing;
        
        EditorGUI.PropertyField(rect, playAnimation, new GUIContent("애니메이션 재생"));
        rect.y += lineHeight + spacing;

        if (playAnimation.boolValue)
        {
            EditorGUI.PropertyField(rect, animationName, new GUIContent(" ㄴ 애니메이션 이름"));
            rect.y += lineHeight + spacing;
        }

        int typeIndex = actionType.enumValueIndex;

        if (typeIndex == (int)ActionType.Move)
        {
            EditorGUI.PropertyField(rect, useAcceleration, new GUIContent("가속/감속 설정"));
            rect.y += lineHeight + spacing;

            rect.y += headerSpacing - spacing;
            EditorGUI.LabelField(rect, "속도 파라미터", EditorStyles.boldLabel);
            rect.y += lineHeight + spacing;
            
            EditorGUI.PropertyField(rect, speed, new GUIContent("목표 속도"));
            rect.y += lineHeight + spacing;
            
            if (useAcceleration.boolValue)
            {
                EditorGUI.PropertyField(rect, startSpeed, new GUIContent("출발 속도"));
                rect.y += lineHeight + spacing;
                EditorGUI.PropertyField(rect, acceleration, new GUIContent("가속/감속 수치"));
                rect.y += lineHeight + spacing;
            }

            rect.y += headerSpacing - spacing; 
            EditorGUI.LabelField(rect, "목표 설정", EditorStyles.boldLabel);
            rect.y += lineHeight + spacing;
            
            targetType.enumValueIndex = EditorGUI.Popup(rect, "목표 설정 방식", targetType.enumValueIndex, targetDisplayNames);
            rect.y += lineHeight + spacing;

            if (targetType.enumValueIndex == (int)TargetType.SpecificPosition)
            {
                EditorGUI.PropertyField(rect, targetPosition, new GUIContent("목표 좌표"));
                rect.y += lineHeight + spacing;
                EditorGUI.PropertyField(rect, offset, new GUIContent("오프셋(Offset)"));
                rect.y += lineHeight + spacing;
            }
            else if (targetType.enumValueIndex == (int)TargetType.Direction)
            {
                EditorGUI.PropertyField(rect, moveDirection, new GUIContent("이동 방향(Direction)"));
                rect.y += lineHeight + spacing;
            }
            else // TrackObject
            {
                EditorGUI.PropertyField(rect, targetTag, new GUIContent("타겟 태그")); 
                rect.y += lineHeight + spacing;
                
                // ▼ X축, Z축 추적 체크박스를 한 줄에 나란히 배치
                Rect toggleRect1 = new Rect(rect.x, rect.y, rect.width / 2, rect.height);
                Rect toggleRect2 = new Rect(rect.x + rect.width / 2, rect.y, rect.width / 2, rect.height);
                
                float originalLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 80f;
                EditorGUI.PropertyField(toggleRect1, property.FindPropertyRelative("trackXOnly"), new GUIContent("X축만 추적"));
                EditorGUI.PropertyField(toggleRect2, property.FindPropertyRelative("trackZOnly"), new GUIContent("Z축만 추적"));
                EditorGUIUtility.labelWidth = originalLabelWidth;
                rect.y += lineHeight + spacing;

                EditorGUI.PropertyField(rect, trackMargin, new GUIContent("추적 마진 거리(Margin)"));
                rect.y += lineHeight + spacing;

                // ▼ 인스펙터 출력 이름을 '타겟 도형'으로 수정
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("targetGizmoShape"), new GUIContent("타겟 도형"));
                rect.y += lineHeight + spacing;
            }

            rect.y += headerSpacing - spacing;
            EditorGUI.LabelField(rect, "종료 조건 설정", EditorStyles.boldLabel);
            rect.y += lineHeight + spacing;
            
            if (targetType.enumValueIndex != (int)TargetType.Direction)
            {
                EditorGUI.PropertyField(rect, stopOnTargetReached, new GUIContent("목표 도달 시 종료"));
                rect.y += lineHeight + spacing;
                
                if (stopOnTargetReached.boolValue)
                {
                    distanceMode.enumValueIndex = EditorGUI.Popup(rect, " ㄴ 거리 측정 방식", distanceMode.enumValueIndex, new string[] { "Distance 3D", "Attack Box" });
                    rect.y += lineHeight + spacing;

                    // ▼ 감지 조건용 도형 선택
                    targetShape.enumValueIndex = EditorGUI.Popup(rect, " ㄴ 감지 도형 선택", targetShape.enumValueIndex, new string[] { "원형 (Sphere)", "사각형 (Box)", "점 (Point)" });
                    rect.y += lineHeight + spacing;

                    // ▼ 도형 오프셋 축별 비활성화 처리
                    bool isTrackXOnly = targetType.enumValueIndex == (int)TargetType.TrackObject && property.FindPropertyRelative("trackXOnly").boolValue;
                    bool isTrackZOnly = targetType.enumValueIndex == (int)TargetType.TrackObject && property.FindPropertyRelative("trackZOnly").boolValue;

                    SerializedProperty xProp = shapeOffset.FindPropertyRelative("x");
                    SerializedProperty yProp = shapeOffset.FindPropertyRelative("y");
                    SerializedProperty zProp = shapeOffset.FindPropertyRelative("z");

                    // 비활성화되는 축의 오프셋 값을 0으로 강제 고정하여 오작동 방지
                    if (isTrackZOnly) xProp.floatValue = 0f;
                    if (isTrackXOnly) zProp.floatValue = 0f;

                    Rect offsetLabelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, lineHeight);
                    EditorGUI.LabelField(offsetLabelRect, " ㄴ 도형 오프셋(Offset)");

                    float fieldWidth = (rect.width - EditorGUIUtility.labelWidth) / 3f;
                    Rect xRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y, fieldWidth - 2f, lineHeight);
                    Rect yRect = new Rect(xRect.x + fieldWidth, rect.y, fieldWidth - 2f, lineHeight);
                    Rect zRect = new Rect(yRect.x + fieldWidth, rect.y, fieldWidth - 2f, lineHeight);

                    float tempLabelWidth = EditorGUIUtility.labelWidth;
                    int tempIndent = EditorGUI.indentLevel; 
                    
                    EditorGUI.indentLevel = 0; // 수동 배치 시 위치 중복 적용 방지
                    EditorGUIUtility.labelWidth = 14f;

                    // Z축만 추적할 경우 X 오프셋 비활성화
                    EditorGUI.BeginDisabledGroup(isTrackZOnly);
                    EditorGUI.PropertyField(xRect, xProp, new GUIContent("X"));
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.PropertyField(yRect, yProp, new GUIContent("Y"));

                    // X축만 추적할 경우 Z 오프셋 비활성화
                    EditorGUI.BeginDisabledGroup(isTrackXOnly);
                    EditorGUI.PropertyField(zRect, zProp, new GUIContent("Z"));
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.indentLevel = tempIndent;
                    EditorGUIUtility.labelWidth = tempLabelWidth;
                    
                    rect.y += lineHeight + spacing;

                    if (targetShape.enumValueIndex == (int)TargetShape.Point)
                    {
                        EditorGUI.PropertyField(rect, stopDistance, new GUIContent(" ㄴ 판정 거리(오차 범위)"));
                        rect.y += lineHeight + spacing;
                    }
                    else if (targetShape.enumValueIndex == (int)TargetShape.Sphere)
                    {
                        EditorGUI.PropertyField(rect, stopDistance, new GUIContent(" ㄴ 원형 반지름"));
                        rect.y += lineHeight + spacing;
                    }
                    else
                    {
                        EditorGUI.PropertyField(rect, attackBoxSize, new GUIContent(" ㄴ 사각형 크기(X,Y,Z)"));
                        rect.y += lineHeight + spacing;
                    }
                }
            }

            EditorGUI.PropertyField(rect, stopOnTimeLimit, new GUIContent("시간 제한으로 종료"));
            rect.y += lineHeight + spacing;
            if (stopOnTimeLimit.boolValue)
            {
                EditorGUI.PropertyField(rect, timeLimit, new GUIContent(" ㄴ 제한 시간(초)"));
                rect.y += lineHeight + spacing;
            }
        }
        else if (typeIndex == (int)ActionType.Teleport)
        {
            rect.y += headerSpacing - spacing; 
            EditorGUI.LabelField(rect, "목표 설정", EditorStyles.boldLabel);
            rect.y += lineHeight + spacing;
            
            targetType.enumValueIndex = EditorGUI.Popup(rect, "목표 설정 방식", targetType.enumValueIndex, new string[] { "지정 좌표", "오브젝트(태그) 추적" });
            rect.y += lineHeight + spacing;

            if (targetType.enumValueIndex == 0) // SpecificPosition
            {
                EditorGUI.PropertyField(rect, targetPosition, new GUIContent("목표 좌표"));
                rect.y += lineHeight + spacing;
            }
            else
            {
                EditorGUI.PropertyField(rect, targetTag, new GUIContent("타겟 태그"));
                rect.y += lineHeight + spacing;

                // ▼ 텔레포트에서도 체크박스로 축 제한
                Rect toggleRect1 = new Rect(rect.x, rect.y, rect.width / 2, rect.height);
                Rect toggleRect2 = new Rect(rect.x + rect.width / 2, rect.y, rect.width / 2, rect.height);
                
                float originalLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 90f;
                EditorGUI.PropertyField(toggleRect1, property.FindPropertyRelative("trackXOnly"), new GUIContent("X축 텔레포트"));
                EditorGUI.PropertyField(toggleRect2, property.FindPropertyRelative("trackZOnly"), new GUIContent("Z축 텔레포트"));
                EditorGUIUtility.labelWidth = originalLabelWidth;
                rect.y += lineHeight + spacing;

                // ▼ 텔레포트에서도 이름을 '타겟 도형'으로 수정
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("targetGizmoShape"), new GUIContent("타겟 도형"));
                rect.y += lineHeight + spacing;
            }
            
            EditorGUI.PropertyField(rect, offset, new GUIContent("오프셋(Offset)"));
            rect.y += lineHeight + spacing;
        }
        else if (typeIndex == (int)ActionType.Wait)
        {
            rect.y += headerSpacing - spacing; 
            EditorGUI.LabelField(rect, "대기 설정", EditorStyles.boldLabel);
            rect.y += lineHeight + spacing;
            EditorGUI.PropertyField(rect, timeLimit, new GUIContent("대기 시간(초)"));
            rect.y += lineHeight + spacing;
        }
        else if (typeIndex == (int)ActionType.Attack)
        {
            rect.y += headerSpacing - spacing; 
            EditorGUI.LabelField(rect, "공격 설정", EditorStyles.boldLabel);
            rect.y += lineHeight + spacing;

            damage.floatValue = EditorGUI.FloatField(rect, "공격력(Damage)", damage.floatValue);
            rect.y += lineHeight + spacing;

            // ▼ 인스펙터 라벨 수정 (Vector3 입력임을 명시)
            EditorGUI.PropertyField(rect, knockbackForce, new GUIContent("넉백 힘(X, Y, Z)"));
            rect.y += lineHeight + spacing;

            attackShape.enumValueIndex = EditorGUI.Popup(rect, "공격 범위 도형", attackShape.enumValueIndex, new string[] { "원형 (Sphere)", "사각형 (Box)" });
            rect.y += lineHeight + spacing;

            if (attackShape.enumValueIndex == 0) // Sphere
            {
                EditorGUI.PropertyField(rect, attackRadius, new GUIContent(" ㄴ 원형 반지름"));
                rect.y += lineHeight + spacing;
            }
            else // Box
            {
                EditorGUI.PropertyField(rect, attackHitBoxSize, new GUIContent(" ㄴ 사각형 크기(X,Y,Z)"));
                rect.y += lineHeight + spacing;
            }

            EditorGUI.PropertyField(rect, attackOffset, new GUIContent(" ㄴ 중심점 오프셋(Offset)"));
            rect.y += lineHeight + spacing;

            rect.y += headerSpacing - spacing; 
            EditorGUI.LabelField(rect, "대기 설정", EditorStyles.boldLabel);
            rect.y += lineHeight + spacing;

            EditorGUI.PropertyField(rect, useWaitAfterAnimation, new GUIContent("애니메이션 후 추가 대기"));
            rect.y += lineHeight + spacing;

            if (useWaitAfterAnimation != null && useWaitAfterAnimation.boolValue)
            {
                EditorGUI.PropertyField(rect, waitDuration, new GUIContent(" ㄴ 대기 시간(초)"));
                rect.y += lineHeight + spacing;
            }
        }

        Rect dividerRect = new Rect(
            position.x, 
            position.y + position.height - (dividerSpacing / 2f) - (dividerHeight / 2f), 
            position.width, 
            dividerHeight
        );

        Color dividerColor = EditorGUIUtility.isProSkin 
            ? new Color(0.2f, 0.2f, 0.2f, 1f) 
            : new Color(0.7f, 0.7f, 0.7f, 1f);

        EditorGUI.DrawRect(dividerRect, dividerColor);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var actionType = property.FindPropertyRelative("actionType");
        int typeIndex = actionType.enumValueIndex;

        int fieldLineCount = 4; 
        if (property.FindPropertyRelative("playAnimation").boolValue) fieldLineCount++; 

        int headerCount = 1;    

        if (typeIndex == (int)ActionType.Move)
        {
            var targetType = property.FindPropertyRelative("targetType");
            
            fieldLineCount += 5; // useAcceleration, speed, targetType 팝업, 종료조건체크, timeLimit체크
            headerCount += 3;    
            if (property.FindPropertyRelative("useAcceleration").boolValue) fieldLineCount += 2;
            
            // ▼ 동적 라인 수 재계산
            if (targetType.enumValueIndex == (int)TargetType.SpecificPosition)
            {
                fieldLineCount += 2; // 목표좌표, 오프셋
            }
            else if (targetType.enumValueIndex == (int)TargetType.TrackObject)
            {
                // 타겟태그(1), 축제한토글(1), 추적마진(1), 타겟기즈모도형(1)
                fieldLineCount += 4; 
            }
            else if (targetType.enumValueIndex == (int)TargetType.Direction)
            {
                fieldLineCount += 1; // 방향 변수
            }
            
            if (targetType.enumValueIndex != (int)TargetType.Direction)
            {
                if (property.FindPropertyRelative("stopOnTargetReached").boolValue) 
                {
                    fieldLineCount += 4; 
                } 
            }
            
            if (property.FindPropertyRelative("stopOnTimeLimit").boolValue) fieldLineCount++; 
        }
        else if (typeIndex == (int)ActionType.Teleport)
        {
            // Popup 1줄, Offset 1줄
            fieldLineCount += 2; 

            if (property.FindPropertyRelative("targetType").enumValueIndex == 0) // SpecificPosition
            {
                fieldLineCount += 1; // 목표좌표
            }
            else // TrackObject
            {
                // 타겟태그(1), 축제한토글(1), 타겟기즈모도형(1)
                fieldLineCount += 3; 
            }

            headerCount += 1;    
        }
        else if (typeIndex == (int)ActionType.Wait)
        {
            fieldLineCount += 1; 
            headerCount += 1;    
        }
        else if (typeIndex == (int)ActionType.Attack)
        {
            fieldLineCount += 6; // ▼ 넉백 힘 필드가 추가되어 기존 5에서 6으로 변경
            if (property.FindPropertyRelative("useWaitAfterAnimation").boolValue) fieldLineCount++;
            headerCount += 2; 
        }

        float totalHeight = ((lineHeight + spacing) * fieldLineCount) 
                          + ((lineHeight + spacing) * headerCount) 
                          + (headerSpacing * headerCount - (spacing * headerCount))
                          + verticalPadding;

        return totalHeight + dividerSpacing;
    }
}