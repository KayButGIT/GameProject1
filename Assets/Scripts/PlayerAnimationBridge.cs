using UnityEngine;

public sealed class PlayerAnimationBridge : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int DieHash = Animator.StringToHash("Die");

    private Animator animator;
    private bool hasSpeed;
    private bool hasIsMoving;
    private bool hasDie;

    public bool HasAnimator => animator != null;

    public void Initialize(Animator targetAnimator)
    {
        animator = targetAnimator;
        CacheParameters();
    }

    public void SetMovement(float speed)
    {
        if (animator == null)
        {
            return;
        }

        if (hasSpeed)
        {
            animator.SetFloat(SpeedHash, speed);
        }

        if (hasIsMoving)
        {
            animator.SetBool(IsMovingHash, speed > 0.05f);
        }
    }

    public void SetDeath()
    {
        if (animator == null)
        {
            return;
        }

        SetMovement(0f);
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
