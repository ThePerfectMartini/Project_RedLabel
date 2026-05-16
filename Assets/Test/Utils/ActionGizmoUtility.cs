using UnityEngine;

public static class ActionGizmoUtility
{
    public static void DrawGizmos(CapsuleController controller, ActionSequenceSO actionSequence, int currentActionIndex)
    {
        if (actionSequence == null || actionSequence.actions == null || actionSequence.actions.Count == 0) return;

        int indexToDraw = Application.isPlaying ? currentActionIndex : 0;
        if (indexToDraw >= actionSequence.actions.Count) return;

        ActionBase action = actionSequence.actions[indexToDraw];
        if (action == null || !action.showGizmo) return;

        if (action is MoveAction move)
        {
            DrawMoveGizmos(controller, move);
        }
        else if (action is FixedAttackAction fixedAttack)
        {
            DrawAttackGizmos(controller, fixedAttack, true, fixedAttack.targetPosition);
        }
        else if (action is VariableAttackAction varAttack)
        {
            DrawAttackGizmos(controller, varAttack, false, Vector3.zero);
        }
        else if (action is RangedAttackAction rangedAttack)
        {
            DrawRangedAttackGizmos(controller, rangedAttack);
        }
    }

    private static void DrawMoveGizmos(CapsuleController controller, MoveAction action)
    {
        if (Application.isPlaying) return;

        Gizmos.color = action.isTeleport ? Color.magenta : Color.green;
        Vector3 finalTargetPos = action.targetPosition;
        bool isTracking = action.targetType == TargetType.TrackObject;
        Vector3 rawTargetPos = finalTargetPos;

        if (isTracking && !string.IsNullOrEmpty(action.targetTag))
        {
            GameObject targetGO = GameObject.FindWithTag(action.targetTag);
            if (targetGO != null)
            {
                finalTargetPos = targetGO.transform.position;
                rawTargetPos = finalTargetPos;
            }
        }

        if (isTracking)
        {
            if (action.trackXOnly && !action.trackZOnly) finalTargetPos.z = controller.transform.position.z;
            else if (!action.trackXOnly && action.trackZOnly) finalTargetPos.x = controller.transform.position.x;
        }

        Vector3 offsetPos = finalTargetPos + action.targetOffset;
        offsetPos.y = controller.transform.position.y;

        Gizmos.DrawWireSphere(offsetPos, 0.5f);

        if (action.targetType == TargetType.Direction)
        {
            Vector3 dir = CapsuleController.GetDirectionFromEnum(action.moveDirection8);
            if (dir != Vector3.zero)
            {
                DrawArrow(controller.transform.position, dir * 3f);
            }
        }
        else
        {
            if (!action.isTeleport)
            {
                Gizmos.DrawLine(controller.transform.position, offsetPos);
                Gizmos.DrawWireSphere(offsetPos, action.destinationStopDistance);
            }
            
            if (isTracking)
            {
                Gizmos.color = new Color(0, 1, 0, 0.3f);
                Gizmos.DrawLine(rawTargetPos, finalTargetPos);
                Gizmos.DrawWireCube(rawTargetPos, Vector3.one * 0.3f);
            }
        }

        if (action.stopOnTargetDetected && !action.isTeleport && action.targetType != TargetType.Direction)
        {
            Gizmos.color = Color.yellow;
            Vector3 originPos = (action.detectOrigin == DetectOrigin.Self) ? controller.transform.position : offsetPos;
            Vector3 center = originPos + action.detectOffset;

            if (action.detectShape == DetectShape.Sphere)
                Gizmos.DrawWireSphere(center, action.detectRadius);
            else if (action.detectShape == DetectShape.Box)
                Gizmos.DrawWireCube(center, action.detectBoxSize);
        }
    }

    private static void DrawAttackGizmos(CapsuleController controller, BaseAttackAction action, bool isFixed, Vector3 fixedTargetPos)
    {
        if (Application.isPlaying) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.4f); 
        
        if (isFixed)
        {
            Vector3 attackCenter = fixedTargetPos + action.attackOffset;
            Gizmos.matrix = Matrix4x4.identity;

            DrawShape(attackCenter, action);
        }
        else
        {
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(controller.transform.position, controller.transform.rotation, Vector3.one);
            Gizmos.matrix = rotationMatrix;
            Vector3 localCenter = action.attackOffset;

            DrawShape(localCenter, action);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }

    private static void DrawShape(Vector3 center, BaseAttackAction action)
    {
        AttackShape shape = AttackShape.Sphere;
        float radius = 1.5f;
        float height = 2f;
        Vector3 boxSize = Vector3.one;

        if (action is FixedAttackAction f) { shape = f.attackShape; radius = f.attackRadius; height = f.attackHeight; boxSize = f.attackHitBoxSize; }
        else if (action is VariableAttackAction v) { shape = v.attackShape; radius = v.attackRadius; height = v.attackHeight; boxSize = v.attackHitBoxSize; }

        if (shape == AttackShape.Sphere)
        {
            Gizmos.DrawWireSphere(center, radius);
        }
        else if (shape == AttackShape.Box)
        {
            Gizmos.DrawWireCube(center, boxSize);
            Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
            Gizmos.DrawCube(center, boxSize);
        }
        else if (shape == AttackShape.Cylinder)
        {
            DrawWireCylinder(center, radius, height);
        }
    }

    private static void DrawRangedAttackGizmos(CapsuleController controller, RangedAttackAction action)
    {
        if (Application.isPlaying) return;

        Gizmos.color = Color.cyan;
        Vector3 spawnPos = controller.transform.position + (controller.transform.rotation * action.attackOffset);
        Gizmos.DrawWireSphere(spawnPos, 0.2f);
        
        Vector3 targetDir = controller.transform.forward;
        
        if (action.targetType == TargetType.TrackObject)
        {
            if (!string.IsNullOrEmpty(action.targetTag))
            {
                GameObject t = GameObject.FindWithTag(action.targetTag);
                if (t != null)
                {
                    Vector3 targetPos = t.transform.position;
                    if (action.trackXOnly && !action.trackZOnly) targetPos.z = spawnPos.z;
                    if (!action.trackXOnly && action.trackZOnly) targetPos.x = spawnPos.x;
                    targetDir = (targetPos - spawnPos).normalized;
                }
            }
        }
        else if (action.targetType == TargetType.SpecificPosition)
        {
            Vector3 tPos = action.targetPosition;
            targetDir = (tPos - spawnPos).normalized;
        }
        
        if (targetDir == Vector3.zero) targetDir = controller.transform.forward;
        
        DrawArrow(spawnPos, targetDir * 2f);
    }

    private static void DrawArrow(Vector3 start, Vector3 dir)
    {
        Gizmos.DrawRay(start, dir);
        Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 + 20, 0) * new Vector3(0, 0, 1);
        Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 - 20, 0) * new Vector3(0, 0, 1);
        Gizmos.DrawRay(start + dir, right * 0.5f);
        Gizmos.DrawRay(start + dir, left * 0.5f);
    }

    private static void DrawWireCylinder(Vector3 center, float radius, float height)
    {
        float halfHeight = height * 0.5f;
        Vector3 topCenter = center + Vector3.up * halfHeight;
        Vector3 bottomCenter = center - Vector3.up * halfHeight;

        DrawGizmoCircle(topCenter, radius);
        DrawGizmoCircle(bottomCenter, radius);

        Gizmos.DrawLine(topCenter + Vector3.right * radius, bottomCenter + Vector3.right * radius);
        Gizmos.DrawLine(topCenter - Vector3.right * radius, bottomCenter - Vector3.right * radius);
        Gizmos.DrawLine(topCenter + Vector3.forward * radius, bottomCenter + Vector3.forward * radius);
        Gizmos.DrawLine(topCenter - Vector3.forward * radius, bottomCenter - Vector3.forward * radius);
    }

    private static void DrawGizmoCircle(Vector3 center, float radius)
    {
        int segments = 24;
        float angle = 0f;
        Vector3 lastPoint = center + new Vector3(Mathf.Cos(0) * radius, 0, Mathf.Sin(0) * radius);
        for (int i = 1; i <= segments; i++)
        {
            angle += (360f / segments) * Mathf.Deg2Rad;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }
    }
}
