using UnityEngine;

/// <summary>One cat. Holds its Animator, its "tap me" ring and where it started.</summary>
public class Cat : MonoBehaviour
{
    public Animator animator;
    public Transform ring;
    public float ringSize = 1.5f;

    [HideInInspector] public bool boarded;
    [HideInInspector] public bool missed;
    [HideInInspector] public Vector3 homePosition;
    [HideInInspector] public Quaternion homeRotation;
    [HideInInspector] public Transform homeParent;

    public void Hop()
    {
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger("Hop");
    }
}
