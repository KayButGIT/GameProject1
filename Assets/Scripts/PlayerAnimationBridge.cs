using UnityEngine;

public sealed class PlayerAnimationBridge : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int WalkCycleSpeedHash = Animator.StringToHash("WalkCycleSpeed");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int DieHash = Animator.StringToHash("Die");
    private const float MovementStartThreshold = 0.18f;
    private const float MovementStopThreshold = 0.08f;
    private const float SpeedDampTime = 0.12f;
    private const float WalkCycleDampTime = 0.1f;
    private const float MinWalkCycleSpeed = 0.55f;
    private const float MaxWalkCycleSpeed = 1.35f;

    private Animator animator;
    private bool hasSpeed;
    private bool hasWalkCycleSpeed;
    private bool hasIsMoving;
    private bool hasDie;
    private bool isMoving;

    public bool HasAnimator => animator != null;

    public void Initialize(Animator targetAnimator)
    {
        animator = targetAnimator;
        CacheParameters();
    }

    public void SetMovement(float speed, float maxSpeed)
    {
        if (animator == null)
        {
            return;
        }

        if (hasSpeed)
        {
            animator.SetFloat(SpeedHash, speed, SpeedDampTime, Time.deltaTime);
        }

        if (hasWalkCycleSpeed)
        {
            float speedRatio = maxSpeed > 0.001f ? Mathf.Clamp01(speed / maxSpeed) : 0f;
            float walkCycleSpeed = Mathf.Lerp(MinWalkCycleSpeed, MaxWalkCycleSpeed, speedRatio);
            animator.SetFloat(WalkCycleSpeedHash, walkCycleSpeed, WalkCycleDampTime, Time.deltaTime);
        }

        if (hasIsMoving)
        {
            if (!isMoving && speed > MovementStartThreshold)
            {
                isMoving = true;
            }
            else if (isMoving && speed < MovementStopThreshold)
            {
                isMoving = false;
            }

            animator.SetBool(IsMovingHash, isMoving);
        }
    }

    public void SetDeath()
    {
        if (animator == null)
        {
            return;
        }

        SetMovement(0f, 0f);
        isMoving = false;
        if (hasWalkCycleSpeed)
        {
            animator.SetFloat(WalkCycleSpeedHash, 1f);
        }

        if (hasDie)
        {
            animator.SetTrigger(DieHash);
        }
    }

    private void CacheParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == SpeedHash && parameter.type == AnimatorControllerParameterType.Float)
            {
                hasSpeed = true;
            }
            else if (parameter.nameHash == WalkCycleSpeedHash && parameter.type == AnimatorControllerParameterType.Float)
            {
                hasWalkCycleSpeed = true;
            }
            else if (parameter.nameHash == IsMovingHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                hasIsMoving = true;
            }
            else if (parameter.nameHash == DieHash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasDie = true;
            }
        }
    }
}
