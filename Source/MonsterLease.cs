using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppOpsive.UltimateCharacterController.Character;
using Il2CppPathfinding;
using UnityEngine;

namespace DevourMonsterFreeze;

internal sealed class MonsterLease : IFreezeLease
{
    private readonly SurvivalAzazelBehaviour boss;
    private readonly UltimateCharacterLocomotion locomotion;
    private readonly RichAI path;
    private readonly Animator animator;
    private readonly Transform transform;
    private readonly Action<string> log;
    private readonly float originalScale, originalAnimationSpeed;
    private readonly bool originalCanMove, originalCanSearch, originalStopped, originalRotate;
    private readonly Vector3 position;
    private readonly Quaternion rotation;
    private readonly string name;
    public long Key { get; }
    public bool IsAlive => boss;
    public bool CanHold => boss && boss.gameObject.activeInHierarchy && boss.entity && boss.entity.IsAttached && boss.entity.IsOwner;

    public MonsterLease(SurvivalAzazelBehaviour target, Action<string> logger)
    {
        boss = target; log = logger; Key = target.GetInstanceID(); name = target.name;
        locomotion = target.locomotion;
        path = target.richAI;
        animator = target.animator;
        transform = target.transform;
        if (!locomotion || !path || !animator || !transform)
            throw new InvalidOperationException("Main-monster components are not ready: " + name);
        // Complete the entire snapshot before Hold can change anything.
        originalScale = locomotion.TimeScale;
        originalAnimationSpeed = animator.speed;
        originalCanMove = path.canMove;
        originalCanSearch = path.canSearch;
        originalStopped = path.isStopped;
        originalRotate = path.enableRotation;
        position = transform.position;
        rotation = transform.rotation;
        log($"CAPTURE {name} id={Key}: localScale={originalScale}; animator={originalAnimationSpeed}; canMove={originalCanMove}; canSearch={originalCanSearch}; stopped={originalStopped}");
    }

    public void Hold()
    {
        if (!locomotion || !path || !animator || !transform)
            throw new InvalidOperationException("A captured monster component was destroyed.");
        if (locomotion.TimeScale != 0f) locomotion.TimeScale = 0f;
        path.canMove = false;
        path.canSearch = false;
        path.isStopped = true;
        path.enableRotation = false;
        animator.speed = 0f;
        // This is the monster's transform only. Unity's global time scale is untouched.
        transform.SetPositionAndRotation(position, rotation);
        if (locomotion.TimeScale != 0f || path.canMove || !path.isStopped || animator.speed != 0f)
            throw new InvalidOperationException("Monster freeze values did not remain applied.");
    }

    public void Restore()
    {
        var failures = new List<string>();
        void Try(Action restore) { try { restore(); } catch (Exception e) { failures.Add(e.Message); } }
        // Restore all surviving components even if one independent write fails.
        if (locomotion) Try(() => locomotion.TimeScale = originalScale);
        if (path)
        {
            Try(() => path.canMove = originalCanMove);
            Try(() => path.canSearch = originalCanSearch);
            Try(() => path.isStopped = originalStopped);
            Try(() => path.enableRotation = originalRotate);
        }
        if (animator) Try(() => animator.speed = originalAnimationSpeed);
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("; ", failures));
        log("RESTORED " + name + " id=" + Key);
    }

    public object Describe() => new {
        Key, Name = name, Alive = IsAlive,
        OriginalLocalTimeScale = originalScale,
        OriginalAnimationSpeed = originalAnimationSpeed,
        OriginalCanMove = originalCanMove,
        OriginalCanSearch = originalCanSearch,
        OriginalStopped = originalStopped,
        FrozenPosition = new { position.x, position.y, position.z }
    };
}
