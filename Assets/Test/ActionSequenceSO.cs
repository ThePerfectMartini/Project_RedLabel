using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ActionType { Move, Teleport, Wait, Attack }
public enum TargetType { SpecificPosition, TrackObject }

[System.Serializable]
public class ActionData
{
    public bool showGizmo;
    public ActionType actionType;
    public TargetType targetType;
    public Vector3 targetPosition;
    public string targetTag = "Player"; 
    public Vector3 offset;

    public float startSpeed = 0f;
    public float speed = 5f;
    public bool useAcceleration; 
    public float acceleration = 2f;

    public bool stopOnTargetReached;
    public float stopDistance = 0.1f;
    
    public bool stopOnTimeLimit;
    public float timeLimit = 3f; 
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
        }
    }

    public IEnumerator ExecuteSequence(MonoBehaviour runner)
    {
        CapsuleController mover = runner.GetComponent<CapsuleController>();
        if (mover == null) yield break;

        int currentRepeat = 0;
        while (isInfiniteLoop || currentRepeat < repeatCount)
        {
            foreach (var action in actions)
            {
                yield return runner.StartCoroutine(PerformAction(mover, action));
            }
            currentRepeat++;
        }
    }

    private IEnumerator PerformAction(CapsuleController mover, ActionData action)
    {
        Transform resolvedTarget = null;
        if (action.targetType == TargetType.TrackObject)
        {
            GameObject targetGO = GameObject.FindWithTag(action.targetTag);
            if (targetGO != null) resolvedTarget = targetGO.transform;
        }

        switch (action.actionType)
        {
            case ActionType.Move:
                if (action.targetType == TargetType.SpecificPosition)
                    yield return mover.MoveToPosition(action.targetPosition + action.offset, action.startSpeed, action.speed, action.useAcceleration, action.acceleration, action.stopOnTargetReached, action.stopDistance, action.stopOnTimeLimit, action.timeLimit);
                else
                    yield return mover.TrackObject(resolvedTarget, action.offset, action.startSpeed, action.speed, action.useAcceleration, action.acceleration, action.stopOnTargetReached, action.stopDistance, action.stopOnTimeLimit, action.timeLimit);
                break;

            case ActionType.Teleport:
                if (action.targetType == TargetType.SpecificPosition)
                    mover.TeleportToPosition(action.targetPosition + action.offset);
                else
                    mover.TeleportToObject(resolvedTarget, action.offset);
                yield return null;
                break;

            case ActionType.Wait:
                yield return new WaitForSeconds(action.timeLimit);
                break;

            case ActionType.Attack:
                yield return null; 
                break;
        }
    }
}