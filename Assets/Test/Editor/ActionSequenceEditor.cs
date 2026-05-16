using UnityEditor;
using UnityEngine;
using System;

#if UNITY_EDITOR
[CustomEditor(typeof(ActionSequenceSO))]
public class ActionSequenceEditor : Editor
{
    private SerializedProperty isInfiniteLoopProp;
    private SerializedProperty repeatCountProp;
    private SerializedProperty actionsProp;

    private void OnEnable()
    {
        isInfiniteLoopProp = serializedObject.FindProperty("isInfiniteLoop");
        repeatCountProp = serializedObject.FindProperty("repeatCount");
        actionsProp = serializedObject.FindProperty("actions");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(isInfiniteLoopProp);
        if (!isInfiniteLoopProp.boolValue)
        {
            EditorGUILayout.PropertyField(repeatCountProp);
        }

        EditorGUILayout.LabelField("■ 액션 리스트", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(actionsProp, true);

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("이동(Move) 추가")) AddAction(new MoveAction());
        if (GUILayout.Button("대기(Wait) 추가")) AddAction(new WaitAction());
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("변동 타격 추가")) AddAction(new VariableAttackAction());
        if (GUILayout.Button("고정 타격 추가")) AddAction(new FixedAttackAction());
        if (GUILayout.Button("원거리 타격 추가")) AddAction(new RangedAttackAction());
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    private void AddAction(ActionBase newAction)
    {
        ActionSequenceSO so = (ActionSequenceSO)target;
        Undo.RecordObject(so, "Add Action");
        so.actions.Add(newAction);
        EditorUtility.SetDirty(so);
        serializedObject.Update();
    }
}

public static class ActionDrawerUtility
{
    public static void DrawXZField(Rect rect, SerializedProperty prop, string label)
    {
        if (prop == null) return;
        Vector3 val = prop.vector3Value;
        
        float[] values = new float[] { val.x, val.z };
        GUIContent[] subLabels = new GUIContent[] { new GUIContent("X"), new GUIContent("Z") };
        
        EditorGUI.MultiFloatField(rect, new GUIContent(label), subLabels, values);
        prop.vector3Value = new Vector3(values[0], val.y, values[1]);
    }
    public static string GetKoreanName(string englishName)
    {
        switch (englishName)
        {
            case "showGizmo": return "기즈모 표시";
            case "executeParallel": return "비동기 (병렬) 실행";
            case "playAnimation": return "애니메이션 재생";
            case "animationName": return "애니메이션 이름";
            case "isTeleport": return "순간이동(Teleport) 모드";
            case "useJump": return "점프 사용";
            case "jumpForce": return "점프 힘";
            case "startSpeed": return "시작 속도";
            case "speed": return "최대 속도";
            case "useAcceleration": return "가속도 사용";
            case "acceleration": return "가속도";
            case "targetType": return "목표 기준";
            case "targetPosition": return "지정 좌표";
            case "moveDirection8": return "이동 방향";
            case "targetTag": return "추적 대상 태그";
            case "trackXOnly": return "X축만 추적";
            case "trackZOnly": return "Z축만 추적";
            case "targetOffset": return "목표 오프셋";
            case "usePositionRefresh": return "동적 새로고침 사용";
            case "positionRefreshInterval": return "새로고침 간격(초)";
            case "repeatRefreshUntilReached": return "도착 시까지 반복";
            case "refreshRepeatCount": return "반복 횟수";
            case "stopOnDestinationReached": return "목표 도달 시 종료";
            case "destinationStopDistance": return "정지 허용 오차";
            case "stopOnTargetDetected": return "타겟 감지 시 종료";
            case "detectOrigin": return "감지 기준";
            case "detectShape": return "감지 범위 도형";
            case "detectOffset": return "감지 오프셋";
            case "detectRadius": return "감지 반지름";
            case "detectBoxSize": return "감지 박스 크기";
            case "stopOnTimeLimit": return "시간 제한으로 종료";
            case "timeLimit": return "시간 제한(초)";
            case "damage": return "데미지";
            case "knockbackForce": return "넉백 힘 (X, Y, Z)";
            case "attackOffset": return "공격 오프셋";
            case "attackShape": return "공격 범위 도형";
            case "attackRadius": return "공격 반지름";
            case "attackHeight": return "공격 높이(원기둥)";
            case "attackHitBoxSize": return "공격 박스 크기";
            case "projectilePrefab": return "투사체 프리팹";
            case "projectileCount": return "발사 횟수";
            case "projectileInterval": return "발사 간격(초)";
            case "projectileSpeed": return "투사체 속도";
            case "projectileLifeTime": return "투사체 수명(초)";
            default: return null;
        }
    }
}

[CustomPropertyDrawer(typeof(ActionBase), true)]
public class ActionBaseDrawer : PropertyDrawer
{
    private bool ShouldHideField(SerializedProperty property, string propName)
    {
        SerializedProperty shapeProp = property.FindPropertyRelative("attackShape");
        if (shapeProp != null)
        {
            AttackShape currentShape = (AttackShape)shapeProp.enumValueIndex;
            if (propName == "attackRadius" && currentShape == AttackShape.Box) return true;
            if (propName == "attackHeight" && currentShape != AttackShape.Cylinder) return true;
            if (propName == "attackHitBoxSize" && currentShape != AttackShape.Box) return true;
        }

        SerializedProperty useAccel = property.FindPropertyRelative("useAcceleration");
        if (useAccel != null && !useAccel.boolValue && (propName == "acceleration" || propName == "startSpeed")) return true;

        SerializedProperty useJump = property.FindPropertyRelative("useJump");
        if (useJump != null && !useJump.boolValue && propName == "jumpForce") return true;

        SerializedProperty isTeleport = property.FindPropertyRelative("isTeleport");
        if (isTeleport != null && isTeleport.boolValue)
        {
            if (propName == "useJump" || propName == "jumpForce" || propName == "startSpeed" || 
                propName == "speed" || propName == "useAcceleration" || propName == "acceleration" ||
                propName == "usePositionRefresh" || propName == "positionRefreshInterval" || propName == "repeatRefreshUntilReached" || propName == "refreshRepeatCount" ||
                propName == "stopOnDestinationReached" || propName == "destinationStopDistance")
                return true;
        }

        SerializedProperty targetTypeProp = property.FindPropertyRelative("targetType");
        if (targetTypeProp != null)
        {
            TargetType currentTargetType = (TargetType)targetTypeProp.enumValueIndex;
            
            if (currentTargetType == TargetType.SpecificPosition)
            {
                if (propName == "moveDirection8" || propName == "targetTag" || propName == "trackXOnly" || propName == "trackZOnly" ||
                    propName == "usePositionRefresh" || propName == "positionRefreshInterval" || propName == "repeatRefreshUntilReached" || propName == "refreshRepeatCount" ||
                    propName == "stopOnTargetDetected" || propName == "detectOrigin" || propName == "detectShape" || propName == "detectOffset" || propName == "detectRadius" || propName == "detectBoxSize")
                    return true;
            }
            else if (currentTargetType == TargetType.TrackObject)
            {
                if (propName == "targetPosition" || propName == "moveDirection8")
                    return true;
            }
            else if (currentTargetType == TargetType.Direction)
            {
                if (propName == "targetPosition" || propName == "targetTag" || propName == "trackXOnly" || propName == "trackZOnly" || propName == "targetOffset" ||
                    propName == "usePositionRefresh" || propName == "positionRefreshInterval" || propName == "repeatRefreshUntilReached" || propName == "refreshRepeatCount" ||
                    propName == "stopOnDestinationReached" || propName == "destinationStopDistance" ||
                    propName == "stopOnTargetDetected" || propName == "detectOrigin" || propName == "detectShape" || propName == "detectOffset" || propName == "detectRadius" || propName == "detectBoxSize")
                    return true;
            }
        }

        SerializedProperty usePosRef = property.FindPropertyRelative("usePositionRefresh");
        if (usePosRef != null && !usePosRef.boolValue)
        {
            if (propName == "positionRefreshInterval" || propName == "repeatRefreshUntilReached" || propName == "refreshRepeatCount") return true;
        }
        else
        {
            SerializedProperty repRef = property.FindPropertyRelative("repeatRefreshUntilReached");
            if (repRef != null && repRef.boolValue && propName == "refreshRepeatCount") return true;
        }

        SerializedProperty stopDest = property.FindPropertyRelative("stopOnDestinationReached");
        if (stopDest != null && !stopDest.boolValue && propName == "destinationStopDistance") return true;

        SerializedProperty stopTarget = property.FindPropertyRelative("stopOnTargetDetected");
        if (stopTarget != null && !stopTarget.boolValue)
        {
            if (propName == "detectOrigin" || propName == "detectShape" || propName == "detectOffset" || propName == "detectRadius" || propName == "detectBoxSize") return true;
        }

        SerializedProperty stopTime = property.FindPropertyRelative("stopOnTimeLimit");
        if (stopTime != null && !stopTime.boolValue && propName == "timeLimit") return true;

        return false;
    }

    private int GetIndentLevelOffset(string propName)
    {
        switch (propName)
        {
            case "startSpeed":
            case "acceleration":
            case "jumpForce":
            case "positionRefreshInterval":
            case "repeatRefreshUntilReached":
            case "destinationStopDistance":
            case "detectOrigin":
            case "detectShape":
            case "detectOffset":
            case "detectRadius":
            case "detectBoxSize":
            case "timeLimit":
            case "attackRadius":
            case "attackHeight":
            case "attackHitBoxSize":
                return 1;
            case "refreshRepeatCount":
                return 2;
            default:
                return 0;
        }
    }

    private string GetParentPrefix(SerializedProperty prop)
    {
        if (prop.propertyType == SerializedPropertyType.Boolean)
        {
            switch (prop.name)
            {
                case "useAcceleration":
                case "useJump":
                case "usePositionRefresh":
                case "stopOnDestinationReached":
                case "stopOnTargetDetected":
                case "stopOnTimeLimit":
                    return prop.boolValue ? "▼ " : "▶ ";
                case "repeatRefreshUntilReached":
                    return !prop.boolValue ? "▼ " : "▶ ";
            }
        }
        return "";
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        
        Rect rect = position;
        rect.height = EditorGUIUtility.singleLineHeight;
        
        string title = label.text;
        string typeName = property.managedReferenceFullTypename;
        
        if (!string.IsNullOrEmpty(typeName))
        {
            if (typeName.EndsWith("MoveAction")) title = "[이동] " + title;
            else if (typeName.EndsWith("WaitAction")) title = "[대기] " + title;
            else if (typeName.EndsWith("FixedAttackAction")) title = "[고정 타격] " + title;
            else if (typeName.EndsWith("VariableAttackAction")) title = "[변동 타격] " + title;
            else if (typeName.EndsWith("RangedAttackAction")) title = "[원거리 타격] " + title;
        }

        property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, new GUIContent(title), true);
        rect.y += EditorGUIUtility.singleLineHeight + 2;

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            
            SerializedProperty prop = property.Copy();
            bool enterChildren = true;

            while (prop.NextVisible(enterChildren))
            {
                if (SerializedProperty.EqualContents(prop, property.GetEndProperty())) break;
                enterChildren = false;

                if (ShouldHideField(property, prop.name)) continue;

                string korName = ActionDrawerUtility.GetKoreanName(prop.name);
                int indent = GetIndentLevelOffset(prop.name);
                string prefix = (indent > 0 ? "└ " : "") + GetParentPrefix(prop);
                GUIContent labelContent = korName != null ? new GUIContent(prefix + korName) : new GUIContent(prefix + prop.displayName);

                EditorGUI.indentLevel += indent;

                if (prop.name == "targetType")
                {
                    Rect r = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                    string[] targetTypeNames = { "지정 좌표", "오브젝트 추적", "특정 방향 고정" };
                    prop.enumValueIndex = EditorGUI.Popup(r, labelContent.text, prop.enumValueIndex, targetTypeNames);
                    rect.y += EditorGUIUtility.singleLineHeight + 2;
                }
                else if (prop.name == "targetPosition" || prop.name == "targetOffset")
                {
                    Rect r = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                    ActionDrawerUtility.DrawXZField(r, prop, labelContent.text);
                    rect.y += EditorGUIUtility.singleLineHeight + 2;
                }
                else
                {
                    float h = EditorGUI.GetPropertyHeight(prop, true);
                    Rect r = new Rect(rect.x, rect.y, rect.width, h);
                    EditorGUI.PropertyField(r, prop, labelContent, true);
                    rect.y += h + 2;
                }

                EditorGUI.indentLevel -= indent;
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;
        
        float height = EditorGUIUtility.singleLineHeight + 2;
        SerializedProperty prop = property.Copy();
        bool enterChildren = true;

        while (prop.NextVisible(enterChildren))
        {
            if (SerializedProperty.EqualContents(prop, property.GetEndProperty())) break;
            enterChildren = false;

            if (ShouldHideField(property, prop.name)) continue;

            if (prop.name == "targetType" || prop.name == "targetPosition" || prop.name == "targetOffset")
            {
                height += EditorGUIUtility.singleLineHeight + 2;
            }
            else
            {
                height += EditorGUI.GetPropertyHeight(prop, true) + 2;
            }
        }
        return height;
    }
}
#endif
