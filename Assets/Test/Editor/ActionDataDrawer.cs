#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [SerializeReference] 다형성 ActionData 서브클래스를 위한 커스텀 PropertyDrawer.
///
/// 특징:
///   - FindPropertyRelative 결과를 딕셔너리로 캐싱 → 매 프레임 재탐색 없음
///   - ActionEditorStyles 공유 스타일 시스템으로 PhaseSOEditor와 시각 통일
///   - 타입 선택 드롭다운으로 서브클래스 변경 가능 (Add 버튼 → 컨텍스트 메뉴)
///   - 이징 곡선 미리보기 캐싱 (설정 변경 시에만 재생성)
/// </summary>
[CustomPropertyDrawer(typeof(ActionData), true)]
public class ActionDataDrawer : PropertyDrawer
{
    // ── PropertyRelative 캐싱 제거 (direct FindPropertyRelative 사용) ──
    private float LineH    => ActionEditorStyles.LineH;
    private float LineStep => ActionEditorStyles.LineStep;

    // ═══════════════════════════════════════════════════════════
    // OnGUI
    // ═══════════════════════════════════════════════════════════

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect rect = position;
        rect.height = LineH;

        // ── 타입 결정 ──
        object managedRef = property.managedReferenceValue;
        string typeName   = managedRef != null ? managedRef.GetType().Name : "null";
        string displayType = GetKoreanTypeName(typeName);
        string title       = $"[{displayType}] {label.text}";

        // ── Foldout ──
        property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, new GUIContent(title), true);
        rect.y += LineStep;

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;

        if (managedRef == null)
        {
            EditorGUI.HelpBox(new Rect(rect.x, rect.y, rect.width, LineH * 2f),
                "액션 데이터가 비어 있습니다. 아래에서 타입을 선택하여 생성하세요.", MessageType.Info);
            rect.y += LineH * 2f + 4f;

            Rect buttonRect = new Rect(rect.x, rect.y, rect.width, LineH);
            if (GUI.Button(buttonRect, "액션 타입 선택 및 생성...", EditorStyles.popup))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("이동 (MoveActionData)"), false, () => SetManagedReference(property, new MoveActionData()));
                menu.AddItem(new GUIContent("대기 (WaitActionData)"), false, () => SetManagedReference(property, new WaitActionData()));
                menu.AddItem(new GUIContent("근접 타격 (AttackActionData)"), false, () => SetManagedReference(property, new AttackActionData()));
                menu.AddItem(new GUIContent("원거리 타격 (RangedAttackActionData)"), false, () => SetManagedReference(property, new RangedAttackActionData()));
                menu.ShowAsContext();
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
            return;
        }

        // ── 공통 필드 ──
        DrawProp(ref rect, property, "executeParallel", "병렬 실행 (동시 실행)");
        ActionEditorStyles.DrawDivider(ref rect);

        DrawProp(ref rect, property, "playAnimation", "애니메이션 재생");
        if (GetProp(property, "playAnimation").boolValue)
            DrawProp(ref rect, property, "animationName", " ㄴ 애니메이션 이름");
        ActionEditorStyles.DrawDivider(ref rect);

        // ── 방어 상태 ──
        ActionEditorStyles.DrawHeader(ref rect, "▶ 방어 상태 설정");
        var superArmorProp  = GetProp(property, "isSuperArmor");
        var invincibleProp  = GetProp(property, "isInvincible");

        EditorGUI.BeginChangeCheck();
        DrawPropDirect(ref rect, superArmorProp, "슈퍼 아머 (경직 면역)");
        if (EditorGUI.EndChangeCheck() && superArmorProp.boolValue)
            invincibleProp.boolValue = false;

        EditorGUI.BeginChangeCheck();
        DrawPropDirect(ref rect, invincibleProp, "무적 상태 (피격 면역)");
        if (EditorGUI.EndChangeCheck() && invincibleProp.boolValue)
            superArmorProp.boolValue = false;

        ActionEditorStyles.DrawDivider(ref rect);

        // ── 타입별 필드 ──
        if (managedRef is MoveActionData)
            DrawMoveSection(ref rect, property);
        else if (managedRef is WaitActionData)
            DrawWaitSection(ref rect, property);
        else if (managedRef is RangedAttackActionData)
            DrawRangedAttackSection(ref rect, property);
        else if (managedRef is AttackActionData)
            DrawAttackSection(ref rect, property, isRanged: false);

        ActionEditorStyles.DrawDivider(ref rect);
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    // ═══════════════════════════════════════════════════════════
    // GetPropertyHeight
    // ═══════════════════════════════════════════════════════════

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded) return LineH;

        float h = LineStep; // Foldout 한 줄

        object managedRef = property.managedReferenceValue;
        if (managedRef == null)
            return LineStep + LineH * 2f + LineStep + 4f;

        // 공통
        h += LineStep; // executeParallel
        h += ActionEditorStyles.DividerHeight;      // divider
        h += LineStep; // playAnimation
        if (GetPropSafe(property, "playAnimation")?.boolValue == true) h += LineStep;
        h += ActionEditorStyles.DividerHeight;

        // 방어 상태
        h += ActionEditorStyles.HeaderHeight; // header
        h += LineStep * 2; // superArmor + invincible
        h += ActionEditorStyles.DividerHeight;

        // 타입별
        if      (managedRef is MoveActionData)           h += GetMoveHeight(property);
        else if (managedRef is WaitActionData)           h += GetWaitHeight();
        else if (managedRef is RangedAttackActionData)   h += GetRangedAttackHeight(property);
        else if (managedRef is AttackActionData)         h += GetAttackHeight(property);

        h += ActionEditorStyles.DividerHeight; // 하단 divider
        return h;
    }

    // ═══════════════════════════════════════════════════════════
    // 이동 섹션
    // ═══════════════════════════════════════════════════════════

    private void DrawMoveSection(ref Rect rect, SerializedProperty prop)
    {
        ActionEditorStyles.DrawHeader(ref rect, "▶ 이동 설정");

        DrawProp(ref rect, prop, "isTeleport", "순간이동(Teleport) 모드");
        bool isTeleport = GetProp(prop, "isTeleport").boolValue;

        if (!isTeleport)
        {
            DrawProp(ref rect, prop, "useJump", "점프 사용");
            if (GetProp(prop, "useJump").boolValue)
                DrawProp(ref rect, prop, "jumpForce", " ㄴ 점프 힘");

            DrawProp(ref rect, prop, "speed", "목표 속도");
            DrawProp(ref rect, prop, "useStartEase", "출발 가속 활성화");
            if (GetProp(prop, "useStartEase").boolValue)
            {
                DrawProp(ref rect, prop, "startSpeed",       " ㄴ 출발 속도");
                DrawProp(ref rect, prop, "startEaseType",    " ㄴ 출발 이징 타입");
                DrawProp(ref rect, prop, "startEaseExponent"," ㄴ 출발 곡선 기울기 강도");
                DrawProp(ref rect, prop, "startEaseDuration"," ㄴ 출발 가속 도달 시간(초)");
                EaseType et  = (EaseType)GetProp(prop, "startEaseType").enumValueIndex;
                float    exp = GetProp(prop, "startEaseExponent").floatValue;
                ActionEditorStyles.DrawCurvePreview(ref rect, et, exp, "  ㄴ 가속 곡선 미리보기");
            }
            DrawProp(ref rect, prop, "allowSlideAfterAction", "행동 후 미끄러짐 허용");
            DrawProp(ref rect, prop, "use8DirectionMovement", "8방향 이동 제한");
        }

        ActionEditorStyles.DrawHeader(ref rect, "▶ 목표 기준 설정");
        var targetTypeProp = GetProp(prop, "targetType");
        DrawPopup(ref rect, targetTypeProp, "목표 기준",
            new[] { "지정 좌표", "오브젝트 추적", "특정 방향 고정", "복귀 (스폰 위치)" });

        TargetType targetType = (TargetType)targetTypeProp.enumValueIndex;
        if (targetType == TargetType.SpecificPosition)
            DrawXZField(ref rect, prop, "targetPosition", "지정 좌표");
        else if (targetType == TargetType.Direction)
            DrawProp(ref rect, prop, "moveDirection8", "이동 방향");
        else if (targetType == TargetType.TrackObject)
        {
            DrawProp(ref rect, prop, "targetTag",  "추적할 타겟 태그");
            DrawProp(ref rect, prop, "trackXOnly", "X축만 추적");
            DrawProp(ref rect, prop, "trackZOnly", "Z축만 추적");
        }
        
        if (targetType != TargetType.Direction && targetType != TargetType.ReturnToSpawn)
        {
            ActionEditorStyles.DrawHeader(ref rect, "▶ 타겟팅 부가 설정");
            DrawXZField(ref rect, prop, "targetOffset", "목표 오프셋");

            if (!isTeleport)
            {
                DrawProp(ref rect, prop, "usePositionRefresh", "동적 새로고침 사용");
                if (GetProp(prop, "usePositionRefresh").boolValue)
                {
                    DrawProp(ref rect, prop, "positionRefreshInterval",   " ㄴ 새로고침 간격(초)");
                    DrawProp(ref rect, prop, "repeatRefreshUntilReached", " ㄴ 도착 시까지 반복");
                    if (!GetProp(prop, "repeatRefreshUntilReached").boolValue)
                        DrawProp(ref rect, prop, "refreshRepeatCount", " ㄴ 반복 횟수");
                }
            }
        }

        ActionEditorStyles.DrawHeader(ref rect, "▶ 종료 조건 설정");
        DrawProp(ref rect, prop, "stopOnDestinationReached", "목표 좌표 도달 시 종료");
        if (GetProp(prop, "stopOnDestinationReached").boolValue)
            DrawProp(ref rect, prop, "destinationStopDistance", " ㄴ 정지 허용 오차");

        if (targetType == TargetType.TrackObject)
        {
            DrawProp(ref rect, prop, "stopOnTargetDetected", "이동 중 타겟 감지 시 종료");
            if (GetProp(prop, "stopOnTargetDetected").boolValue)
            {
                var detectOriginProp = GetProp(prop, "detectOrigin");
                DrawPopup(ref rect, detectOriginProp, " ㄴ 감지 기준",
                    new[] { "자신 오브젝트 기준", "타겟 오브젝트 기준" });

                var detectShapeProp = GetProp(prop, "detectShape");
                DrawPopup(ref rect, detectShapeProp, " ㄴ 감지 도형",
                    new[] { "원형 (Sphere)", "사각형 (Box)" });

                DrawProp(ref rect, prop, "detectOffset", " ㄴ 감지 도형 오프셋");
                if (detectShapeProp.enumValueIndex == (int)DetectShape.Sphere)
                    DrawProp(ref rect, prop, "detectRadius",  " ㄴ 감지 반지름");
                else
                    DrawProp(ref rect, prop, "detectBoxSize", " ㄴ 감지 박스 크기");
            }
        }

        DrawProp(ref rect, prop, "stopOnTimeLimit", "시간 제한으로 종료");
        if (GetProp(prop, "stopOnTimeLimit").boolValue)
            DrawProp(ref rect, prop, "timeLimit", " ㄴ 제한 시간(초)");
    }

    private float GetMoveHeight(SerializedProperty prop)
    {
        float h = 0f;
        h += ActionEditorStyles.HeaderHeight; // 이동 설정 header
        h += GetFieldHeight(prop, "isTeleport");
        bool isTeleport = GetPropSafe(prop, "isTeleport")?.boolValue ?? false;
        if (!isTeleport)
        {
            h += GetFieldHeight(prop, "useJump");
            if (GetPropSafe(prop, "useJump")?.boolValue == true) 
                h += GetFieldHeight(prop, "jumpForce");

            h += GetFieldHeight(prop, "speed");
            h += GetFieldHeight(prop, "useStartEase");
            if (GetPropSafe(prop, "useStartEase")?.boolValue == true)
            {
                h += GetFieldHeight(prop, "startSpeed");
                h += GetFieldHeight(prop, "startEaseType");
                h += GetFieldHeight(prop, "startEaseExponent");
                h += GetFieldHeight(prop, "startEaseDuration");
                h += ActionEditorStyles.CurvePreviewHeight; // curve preview
            }
            h += GetFieldHeight(prop, "allowSlideAfterAction");
            h += GetFieldHeight(prop, "use8DirectionMovement");
        }

        h += ActionEditorStyles.HeaderHeight; // 목표 기준 설정 header
        h += GetFieldHeight(prop, "targetType");
        TargetType tt = (TargetType)(GetPropSafe(prop, "targetType")?.enumValueIndex ?? 0);
        if (tt == TargetType.SpecificPosition)      
            h += GetFieldHeight(prop, "targetPosition");
        else if (tt == TargetType.Direction)        
            h += GetFieldHeight(prop, "moveDirection8");
        else if (tt == TargetType.TrackObject)
        {
            h += GetFieldHeight(prop, "targetTag");
            h += GetFieldHeight(prop, "trackXOnly");
            h += GetFieldHeight(prop, "trackZOnly");
        }

        if (tt != TargetType.Direction && tt != TargetType.ReturnToSpawn)
        {
            h += ActionEditorStyles.HeaderHeight; // 타겟팅 부가 설정 header
            h += GetFieldHeight(prop, "targetOffset");
            if (!isTeleport)
            {
                h += GetFieldHeight(prop, "usePositionRefresh");
                if (GetPropSafe(prop, "usePositionRefresh")?.boolValue == true)
                {
                    h += GetFieldHeight(prop, "positionRefreshInterval");
                    h += GetFieldHeight(prop, "repeatRefreshUntilReached");
                    if (GetPropSafe(prop, "repeatRefreshUntilReached")?.boolValue == false) 
                        h += GetFieldHeight(prop, "refreshRepeatCount");
                }
            }
        }

        h += ActionEditorStyles.HeaderHeight; // 종료 조건 설정 header
        h += GetFieldHeight(prop, "stopOnDestinationReached");
        if (GetPropSafe(prop, "stopOnDestinationReached")?.boolValue == true) 
            h += GetFieldHeight(prop, "destinationStopDistance");

        if (tt == TargetType.TrackObject)
        {
            h += GetFieldHeight(prop, "stopOnTargetDetected");
            if (GetPropSafe(prop, "stopOnTargetDetected")?.boolValue == true)
            {
                h += GetFieldHeight(prop, "detectOrigin");
                h += GetFieldHeight(prop, "detectShape");
                h += GetFieldHeight(prop, "detectOffset");
                int ds = GetPropSafe(prop, "detectShape")?.enumValueIndex ?? 0;
                if (ds == (int)DetectShape.Sphere) 
                    h += GetFieldHeight(prop, "detectRadius");
                else 
                    h += GetFieldHeight(prop, "detectBoxSize");
            }
        }
        h += GetFieldHeight(prop, "stopOnTimeLimit");
        if (GetPropSafe(prop, "stopOnTimeLimit")?.boolValue == true) 
            h += GetFieldHeight(prop, "timeLimit");

        return h;
    }

    // ═══════════════════════════════════════════════════════════
    // 대기 섹션
    // ═══════════════════════════════════════════════════════════

    private void DrawWaitSection(ref Rect rect, SerializedProperty prop)
    {
        ActionEditorStyles.DrawHeader(ref rect, "▶ 대기 설정");
        DrawProp(ref rect, prop, "timeLimit", "대기 시간(초)");
    }

    private float GetWaitHeight() => ActionEditorStyles.HeaderHeight + LineStep;

    // ═══════════════════════════════════════════════════════════
    // 공격 섹션 공통
    // ═══════════════════════════════════════════════════════════

    private void DrawAttackSection(ref Rect rect, SerializedProperty prop, bool isRanged)
    {
        ActionEditorStyles.DrawHeader(ref rect, "▶ 공격 기본 설정");

        if (isRanged)
        {
            var ttProp = GetProp(prop, "targetType");
            DrawPopup(ref rect, ttProp, "타겟 기준",
                new[] { "지정 좌표", "오브젝트 추적", "특정 방향 고정" });
            TargetType tt = (TargetType)ttProp.enumValueIndex;
            if (tt == TargetType.SpecificPosition)
                DrawXZField(ref rect, prop, "targetPosition", "지정 좌표");
            else if (tt == TargetType.Direction)
                DrawProp(ref rect, prop, "moveDirection8", "발사 방향");
            else
            {
                DrawProp(ref rect, prop, "targetTag",          "타겟 태그");
                DrawProp(ref rect, prop, "snapToPlayerXAxis",  "X축 방향 스냅");
            }
        }
        else
        {
            // FixedAttack 여부
            DrawProp(ref rect, prop, "isFixedAttack", "고정 좌표 타격 모드");
            if (GetProp(prop, "isFixedAttack").boolValue)
            {
                var ttProp = GetProp(prop, "targetType");
                DrawPopup(ref rect, ttProp, " ㄴ 고정 좌표 기준",
                    new[] { "지정 좌표", "오브젝트 추적", "특정 방향 고정" });
                TargetType tt = (TargetType)ttProp.enumValueIndex;
                if (tt == TargetType.SpecificPosition)
                    DrawXZField(ref rect, prop, "targetPosition", " ㄴ 지정 좌표");
                else if (tt == TargetType.TrackObject)
                    DrawProp(ref rect, prop, "targetTag", " ㄴ 타겟 태그");
            }
        }

        DrawProp(ref rect, prop, "damage",         "데미지");
        DrawProp(ref rect, prop, "knockbackForce", "넉백 힘 (X, Y, Z)");
        DrawProp(ref rect, prop, "attackOffset",   "공격/생성 오프셋");

        ActionEditorStyles.DrawHeader(ref rect, "▶ 전투 상호작용 설정");
        DrawProp(ref rect, prop, "hitStunDuration", "피격 경직 시간(초)");
        DrawProp(ref rect, prop, "hitStopDuration", "히트스톱 시간(초)");
        DrawProp(ref rect, prop, "staggerValue",    "그로기 게이지 축적량");
        DrawProp(ref rect, prop, "isLauncher",      "에어 런처 여부");
        DrawProp(ref rect, prop, "canBeParried",    "패링 가능 여부");
        DrawProp(ref rect, prop, "isUnblockable",   "가드 불가 여부");

        DrawProp(ref rect, prop, "castDamageOnStart",  "공격 시작 시 즉시 타격 판정");
        DrawProp(ref rect, prop, "useJumpInAttack", "공격 시 점프(도약) 사용");
        if (GetProp(prop, "useJumpInAttack").boolValue)
            DrawProp(ref rect, prop, "attackJumpForce", " └ 점프 힘 (도약 높이)");

        if (!isRanged)
        {
            ActionEditorStyles.DrawHeader(ref rect, "▶ 지속 타격 설정");
            DrawProp(ref rect, prop, "isContinuousAttack", "지속 타격 활성화");
            if (GetProp(prop, "isContinuousAttack").boolValue)
            {
                DrawProp(ref rect, prop, "continuousDuration", " └ 지속 시간(초)");
                DrawProp(ref rect, prop, "hitTickCooldown",    " └ 타깃별 피격 쿨다운(초)");
            }

            ActionEditorStyles.DrawHeader(ref rect, "▶ 타격 범위 설정");
            var shapeProp = GetProp(prop, "attackShape");
            DrawPopup(ref rect, shapeProp, "공격 범위 도형",
                new[] { "원형 (Sphere)", "사각형 (Box)", "원기둥 (Cylinder)" });
            AttackShape shape = (AttackShape)shapeProp.enumValueIndex;
            if (shape == AttackShape.Sphere)
                DrawProp(ref rect, prop, "attackRadius", "공격 반지름");
            else if (shape == AttackShape.Cylinder)
            {
                DrawProp(ref rect, prop, "attackRadius", "공격 반지름");
                DrawProp(ref rect, prop, "attackHeight", "공격 높이(원기둥)");
            }
            else
                DrawProp(ref rect, prop, "attackHitBoxSize", "공격 박스 크기");
        }
    }

    private float GetAttackHeight(SerializedProperty prop)
    {
        float h = ActionEditorStyles.HeaderHeight; // header
        
        bool isFixed = GetPropSafe(prop, "isFixedAttack")?.boolValue ?? false;
        h += GetFieldHeight(prop, "isFixedAttack");
        if (isFixed)
        {
            h += GetFieldHeight(prop, "targetType");
            TargetType tt = (TargetType)(GetPropSafe(prop, "targetType")?.enumValueIndex ?? 0);
            if (tt == TargetType.SpecificPosition) h += GetFieldHeight(prop, "targetPosition");
            else if (tt == TargetType.TrackObject) h += GetFieldHeight(prop, "targetTag");
        }
        
        h += GetFieldHeight(prop, "damage");
        h += GetFieldHeight(prop, "knockbackForce");
        h += GetFieldHeight(prop, "attackOffset");

        h += ActionEditorStyles.HeaderHeight; // interaction header
        h += GetFieldHeight(prop, "hitStunDuration");
        h += GetFieldHeight(prop, "hitStopDuration");
        h += GetFieldHeight(prop, "staggerValue");
        h += GetFieldHeight(prop, "isLauncher");
        h += GetFieldHeight(prop, "canBeParried");
        h += GetFieldHeight(prop, "isUnblockable");

        h += GetFieldHeight(prop, "castDamageOnStart");
        h += GetFieldHeight(prop, "useJumpInAttack");
        if (GetPropSafe(prop, "useJumpInAttack")?.boolValue == true)
            h += GetFieldHeight(prop, "attackJumpForce");

        h += ActionEditorStyles.HeaderHeight; // 지속 타격 header
        h += GetFieldHeight(prop, "isContinuousAttack");
        if (GetPropSafe(prop, "isContinuousAttack")?.boolValue == true)
        {
            h += GetFieldHeight(prop, "continuousDuration");
            h += GetFieldHeight(prop, "hitTickCooldown");
        }

        h += ActionEditorStyles.HeaderHeight; // range header
        h += GetFieldHeight(prop, "attackShape");
        AttackShape shape = (AttackShape)(GetPropSafe(prop, "attackShape")?.enumValueIndex ?? 0);
        if (shape == AttackShape.Sphere)
            h += GetFieldHeight(prop, "attackRadius");
        else if (shape == AttackShape.Cylinder)
        {
            h += GetFieldHeight(prop, "attackRadius");
            h += GetFieldHeight(prop, "attackHeight");
        }
        else
            h += GetFieldHeight(prop, "attackHitBoxSize");

        return h;
    }

    // ═══════════════════════════════════════════════════════════
    // 원거리 섹션
    // ═══════════════════════════════════════════════════════════

    private void DrawRangedAttackSection(ref Rect rect, SerializedProperty prop)
    {
        DrawAttackSection(ref rect, prop, isRanged: true);
        ActionEditorStyles.DrawHeader(ref rect, "▶ 투사체 설정");
        DrawProp(ref rect, prop, "projectilePrefab",    "투사체 프리팹");
        DrawProp(ref rect, prop, "projectileCount",     "발사 갯수");
        DrawProp(ref rect, prop, "projectileInterval",  "발사 간격(초)");
        DrawProp(ref rect, prop, "projectileSpeed",     "투사체 속도");
        DrawProp(ref rect, prop, "projectileLifeTime",  "투사체 수명(초)");
    }

    private float GetRangedAttackHeight(SerializedProperty prop)
    {
        float h = ActionEditorStyles.HeaderHeight; // header
        h += GetFieldHeight(prop, "targetType");
        TargetType tt = (TargetType)(GetPropSafe(prop, "targetType")?.enumValueIndex ?? 0);
        if (tt == TargetType.SpecificPosition) 
            h += GetFieldHeight(prop, "targetPosition");
        else if (tt == TargetType.Direction)   
            h += GetFieldHeight(prop, "moveDirection8");
        else
        {
            h += GetFieldHeight(prop, "targetTag");
            h += GetFieldHeight(prop, "snapToPlayerXAxis");
        }

        h += GetFieldHeight(prop, "damage");
        h += GetFieldHeight(prop, "knockbackForce");
        h += GetFieldHeight(prop, "attackOffset");

        h += ActionEditorStyles.HeaderHeight; // interaction header
        h += GetFieldHeight(prop, "hitStunDuration");
        h += GetFieldHeight(prop, "hitStopDuration");
        h += GetFieldHeight(prop, "staggerValue");
        h += GetFieldHeight(prop, "isLauncher");
        h += GetFieldHeight(prop, "canBeParried");
        h += GetFieldHeight(prop, "isUnblockable");

        h += GetFieldHeight(prop, "castDamageOnStart");
        h += GetFieldHeight(prop, "useJumpInAttack");
        if (GetPropSafe(prop, "useJumpInAttack")?.boolValue == true) 
            h += GetFieldHeight(prop, "attackJumpForce");

        h += ActionEditorStyles.HeaderHeight; // 투사체 설정 header
        h += GetFieldHeight(prop, "projectilePrefab");
        h += GetFieldHeight(prop, "projectileCount");
        h += GetFieldHeight(prop, "projectileInterval");
        h += GetFieldHeight(prop, "projectileSpeed");
        h += GetFieldHeight(prop, "projectileLifeTime");

        return h;
    }

    // ═══════════════════════════════════════════════════════════
    // 드로잉 헬퍼
    // ═══════════════════════════════════════════════════════════

    private void DrawProp(ref Rect rect, SerializedProperty parent, string propName, string customLabel = null)
    {
        SerializedProperty prop = GetProp(parent, propName);
        if (prop == null) return;

        Rect r = rect;
        r.height = EditorGUI.GetPropertyHeight(prop, true);
        EditorGUI.PropertyField(r, prop, customLabel != null ? new GUIContent(customLabel) : null, true);
        rect.y += r.height + 2f;
    }

    private void DrawPropDirect(ref Rect rect, SerializedProperty prop, string label)
    {
        Rect r = rect;
        r.height = EditorGUI.GetPropertyHeight(prop, true);
        EditorGUI.PropertyField(r, prop, new GUIContent(label), true);
        rect.y += r.height + 2f;
    }

    private void DrawPopup(ref Rect rect, SerializedProperty prop, string label, string[] options)
    {
        Rect r = rect;
        r.height = LineH;
        prop.enumValueIndex = EditorGUI.Popup(r, label, prop.enumValueIndex, options);
        rect.y += LineH + 2f;
    }

    private void DrawXZField(ref Rect rect, SerializedProperty parent, string propName, string label)
    {
        SerializedProperty prop = GetProp(parent, propName);
        if (prop == null) return;

        Rect r = rect;
        r.height = LineH;
        float[] values    = new[] { prop.vector3Value.x, prop.vector3Value.z };
        GUIContent[] subs = new[] { new GUIContent("X"), new GUIContent("Z") };
        EditorGUI.MultiFloatField(r, new GUIContent(label), subs, values);
        prop.vector3Value = new Vector3(values[0], prop.vector3Value.y, values[1]);
        rect.y += LineH + 2f;
    }

    // ═══════════════════════════════════════════════════════════
    // 프로퍼티 캐싱
    // ═══════════════════════════════════════════════════════════

    private SerializedProperty GetProp(SerializedProperty parent, string name)
    {
        if (parent == null) return null;
        return parent.FindPropertyRelative(name);
    }

    private SerializedProperty GetPropSafe(SerializedProperty parent, string name)
    {
        return GetProp(parent, name);
    }

    private float GetFieldHeight(SerializedProperty parent, string propName)
    {
        var prop = parent.FindPropertyRelative(propName);
        if (prop == null) return 0f;
        return EditorGUI.GetPropertyHeight(prop, true) + 2f;
    }

    // ═══════════════════════════════════════════════════════════
    // 타입명 한국어 변환
    // ═══════════════════════════════════════════════════════════

    private static string GetKoreanTypeName(string typeName)
    {
        return typeName switch
        {
            nameof(MoveActionData)         => "이동",
            nameof(WaitActionData)         => "대기",
            nameof(RangedAttackActionData) => "원거리 타격",
            nameof(AttackActionData)       => "근접 타격",
            _ => "미설정"
        };
    }

    private void SetManagedReference(SerializedProperty property, object obj)
    {
        property.serializedObject.Update();
        property.managedReferenceValue = obj;
        property.serializedObject.ApplyModifiedProperties();
    }
}
#endif
