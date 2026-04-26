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
    private readonly string[] targetDisplayNames = { "지정 좌표", "오브젝트(태그) 추적" };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var showGizmo = property.FindPropertyRelative("showGizmo");
        var actionType = property.FindPropertyRelative("actionType");
        var useAcceleration = property.FindPropertyRelative("useAcceleration");
        var targetType = property.FindPropertyRelative("targetType");
        var targetPosition = property.FindPropertyRelative("targetPosition");
        var targetTag = property.FindPropertyRelative("targetTag"); 
        var offset = property.FindPropertyRelative("offset");
        var startSpeed = property.FindPropertyRelative("startSpeed");
        var speed = property.FindPropertyRelative("speed");
        var acceleration = property.FindPropertyRelative("acceleration");
        
        var stopOnTargetReached = property.FindPropertyRelative("stopOnTargetReached");
        var stopDistance = property.FindPropertyRelative("stopDistance");
        var stopOnTimeLimit = property.FindPropertyRelative("stopOnTimeLimit");
        var timeLimit = property.FindPropertyRelative("timeLimit");

        Rect rect = new Rect(position.x, position.y + (verticalPadding / 2), position.width, lineHeight);

        EditorGUI.PropertyField(rect, showGizmo, new GUIContent("기즈모 표시"));
        rect.y += lineHeight + spacing;

        EditorGUI.LabelField(rect, "방식 설정", EditorStyles.boldLabel);
        rect.y += lineHeight + spacing;
        
        actionType.enumValueIndex = EditorGUI.Popup(rect, "행동 방식", actionType.enumValueIndex, actionDisplayNames);
        rect.y += lineHeight + spacing;

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
                EditorGUI.PropertyField(rect, targetPosition, new GUIContent("목표 좌표"));
            else
                EditorGUI.PropertyField(rect, targetTag, new GUIContent("타겟 태그")); 
            
            rect.y += lineHeight + spacing;
            EditorGUI.PropertyField(rect, offset, new GUIContent("오프셋(Offset)"));
            rect.y += lineHeight + spacing;

            rect.y += headerSpacing - spacing;
            EditorGUI.LabelField(rect, "종료 조건 설정", EditorStyles.boldLabel);
            rect.y += lineHeight + spacing;
            EditorGUI.PropertyField(rect, stopOnTargetReached, new GUIContent("목표 도달 시 종료"));
            rect.y += lineHeight + spacing;
            if (stopOnTargetReached.boolValue)
            {
                EditorGUI.PropertyField(rect, stopDistance, new GUIContent(" ㄴ 도달 인정 거리"));
                rect.y += lineHeight + spacing;
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
            
            targetType.enumValueIndex = EditorGUI.Popup(rect, "목표 설정 방식", targetType.enumValueIndex, targetDisplayNames);
            rect.y += lineHeight + spacing;

            if (targetType.enumValueIndex == (int)TargetType.SpecificPosition)
                EditorGUI.PropertyField(rect, targetPosition, new GUIContent("목표 좌표"));
            else
                EditorGUI.PropertyField(rect, targetTag, new GUIContent("타겟 태그"));
            
            rect.y += lineHeight + spacing;
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

        int fieldLineCount = 2; 
        int headerCount = 1;    

        if (typeIndex == (int)ActionType.Move)
        {
            fieldLineCount += 7; 
            headerCount += 3;    
            if (property.FindPropertyRelative("useAcceleration").boolValue) fieldLineCount += 2;
            if (property.FindPropertyRelative("stopOnTargetReached").boolValue) fieldLineCount++; 
            if (property.FindPropertyRelative("stopOnTimeLimit").boolValue) fieldLineCount++; 
        }
        else if (typeIndex == (int)ActionType.Teleport)
        {
            fieldLineCount += 3; 
            headerCount += 1;    
        }
        else if (typeIndex == (int)ActionType.Wait)
        {
            fieldLineCount += 1; 
            headerCount += 1;    
        }

        float totalHeight = ((lineHeight + spacing) * fieldLineCount) 
                          + ((lineHeight + spacing) * headerCount) 
                          + (headerSpacing * headerCount - (spacing * headerCount))
                          + verticalPadding;

        return totalHeight + dividerSpacing;
    }
}