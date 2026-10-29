using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppOpsive.UltimateCharacterController.Character;
using Il2CppOpsive.UltimateCharacterController.Game;
using Il2CppOpsive.UltimateCharacterController.Character.Abilities;
using UnityEngine;

namespace DevourMonsterFreeze;

internal sealed class NoclipLease : INoclipLease
{
    private readonly NolanBehaviour player;
    private readonly UltimateCharacterLocomotion motor;
    private readonly CapsuleCollider capsule;
    private readonly Action<string> log;
    private readonly bool gravity, horizontal, vertical, stick;
    private readonly Vector3 entry;
    private readonly int mask;
    private readonly List<string> crouchInputs = new();
    private readonly List<string> jumpInputs = new(), sprintInputs = new(), heldInputs = new();
    private readonly VrHeldActions vrHeld = new();
    private readonly string horizontalInput, forwardInput;
    private string inputSource = "Waiting for input";
    private Vector3 desired;
    public int MotorId { get; }
    public long PositionWrites { get; private set; }
    public long MotorBlocks { get; set; }
    public long InputFrames { get; private set; }
    public long RiseFrames { get; private set; }
    public long DescendFrames { get; private set; }
    public float MaxPositionCorrection { get; private set; }
    public bool ReturnedToEntry { get; private set; }
    public bool IsAlive => player && motor;
    public bool CanControl => ControlBlockReason.Length == 0;

    public string ControlBlockReason
    {
        get
        {
            if (!IsAlive) return "Local player or movement motor is no longer available.";
            if (!player.gameObject.activeInHierarchy) return "Local player is inactive.";
            if (!motor.enabled) return "Local movement motor is disabled.";
            var current = InGameHelpers.singleton?.GetLocalNolanBehaviour();
            if (!current || current!.GetInstanceID() != player.GetInstanceID())
                return "DEVOUR's current local player changed or is unavailable.";
            var entity = player.entity;
            if (!entity || !entity.IsAttached) return "Local player is not attached to the session.";
            // DEVOUR's own NolanBehaviour.Update uses IsOwner. HasControl is a
            // separate Bolt flag, not a requirement for this owner-driven game.
            // Ownership alone never selects a player: the exact local-player
            // lookup above must also match, including on a multiplayer host.
            if (!entity.IsOwner && !entity.HasControl) return "Local player has neither ownership nor control.";
            if (player.IsCrawling()) return "Local player is downed/crawling.";
            if (player.IsBeingKnockedOut() || player.inputDisabledFromKnockout) return "Local player is being knocked out.";
            if (player.IsBeingCarried()) return "Local player is being carried.";
            if (player.IsBeingHeld()) return "Local player is held by an enemy.";
            if (player.IsHiding()) return "Leave the hiding spot before enabling noclip.";
            if (player.m_Survival && (player.m_Survival.IsEndingPlaying() || player.m_Survival.StartingToPlayFailEnding()))
                return "The round ending is playing.";
            // Motor.Alive and CameraController.Character are informative only.
            // Local identity and DEVOUR's own incapacitation flags govern flight.
            return "";
        }
    }

    public NoclipLease(NolanBehaviour target, Action<string> logger)
    {
        player = target; log = logger;
        motor = player.locomotion;
        capsule = player.m_CapsuleCollider;
        if (!motor || !capsule || capsule.direction != 1 || Vector3.Dot(player.transform.up, Vector3.up) < 0.99f)
            throw new InvalidOperationException("Local player's upright movement capsule is not ready.");
        if (motor.ManualMove || motor.TimeScale <= 0f)
            throw new InvalidOperationException("Player movement is currently controlled by a scene or ability.");
        var body = player.GetComponent<Rigidbody>();
        if (!body || !body.isKinematic)
            throw new InvalidOperationException("Expected the game's kinematic player motor.");
        MotorId = motor.GetInstanceID();
        gravity = motor.UseGravity; horizontal = motor.DetectHorizontalCollisions;
        vertical = motor.DetectVerticalCollisions; stick = motor.StickToGround;
        mask = motor.ColliderLayerMask.value;
        var names = player.heightChangeAbility?.InputNames;
        if (names != null) foreach (var name in names) if (!string.IsNullOrWhiteSpace(name)) crouchInputs.Add(name);
        if (!player.devourInput || crouchInputs.Count == 0)
            throw new InvalidOperationException("The game's crouch binding is not ready. Try again after spawning.");
        var handler = player.locomotionHandler;
        horizontalInput = handler ? handler.HorizontalInputName : "";
        forwardInput = handler ? handler.ForwardInputName : "";
        var abilities = motor.Abilities;
        if (abilities != null) foreach (var ability in abilities) {
            var jump = ability?.TryCast<Jump>();
            if (jump?.InputNames != null) foreach (var name in jump.InputNames)
                if (!string.IsNullOrWhiteSpace(name) && !jumpInputs.Contains(name)) jumpInputs.Add(name);
        }
        names = player.speedChangeAbility?.InputNames;
        if (names != null) foreach (var name in names) if (!string.IsNullOrWhiteSpace(name)) sprintInputs.Add(name);
        if (PlayerControls.IsVR(player) &&
            (string.IsNullOrWhiteSpace(horizontalInput) || string.IsNullOrWhiteSpace(forwardInput) || jumpInputs.Count == 0))
            throw new InvalidOperationException("The game's VR movement/jump bindings are not ready. Try again after spawning.");
        heldInputs.AddRange(jumpInputs); heldInputs.AddRange(crouchInputs); heldInputs.AddRange(sprintInputs);
        entry = desired = player.transform.position;
        // Flight deliberately ignores ground contact. Do not gate entry on a
        // raycast, overlap query, or the character's current grounded state.
        log($"NOCLIP CAPTURE {player.name} motor={MotorId}; gravity={gravity}; horizontal={horizontal}; vertical={vertical}; stick={stick}; position={entry}; axes={horizontalInput}/{forwardInput}; jumpInputs={string.Join(",", jumpInputs)}; crouchInputs={string.Join(",", crouchInputs)}");
    }

    public void Enter() => Hold();

    public void Hold()
    {
        motor.UseGravity = false;
        motor.DetectHorizontalCollisions = false;
        motor.DetectVerticalCollisions = false;
        motor.StickToGround = false;
        ZeroMotion();
    }

    private void ZeroMotion()
    {
        motor.MoveDirection = Vector3.zero;
        motor.MotorThrottle = Vector3.zero;
        motor.GravityAmount = 0f;
        motor.Velocity = Vector3.zero;
        motor.ExternalForce = Vector3.zero;
    }

    public void UpdateInput()
    {
        if (PlayerControls.InputAllowed(player))
        {
            vrHeld.Refresh(player, heldInputs);
            var forward = PlayerControls.Forward(player);
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var x = PlayerControls.Axis(player, horizontalInput);
            var z = PlayerControls.Axis(player, forwardInput);
            // Preserve keyboard controls without doubling a mapped keyboard axis.
            float keysX = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            float keysZ = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            if (Mathf.Abs(keysX) > Mathf.Abs(x)) x = keysX;
            if (Mathf.Abs(keysZ) > Mathf.Abs(z)) z = keysZ;
            bool Held(List<string> actions) {
                foreach (var name in actions) if (vrHeld.Held(player, name)) return true;
                return false;
            }
            float y = (Held(jumpInputs) || Input.GetKey(KeyCode.Space) ? 1 : 0) - (Held(crouchInputs) ? 1 : 0);
            var move = Vector3.ClampMagnitude(right * x + forward * z + Vector3.up * y, 1f);
            var speed = Held(sprintInputs) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 8f : 3f;
            desired += move * speed * Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.05f);
            inputSource = PlayerControls.IsVR(player) ? "VR mapped controls / " + player.vrMovementDirection : "Mapped controls / keyboard";
            if (move.sqrMagnitude > 0) InputFrames++;
            if (y > 0) RiseFrames++;
            if (y < 0) DescendFrames++;
        }
        else inputSource = "Gameplay input suspended";
        ApplyPose();
    }

    public void ApplyPose()
    {
        if (!IsAlive) return;
        MaxPositionCorrection = Mathf.Max(MaxPositionCorrection, (player.transform.position - desired).magnitude);
        ZeroMotion();
        // Synchronize the motor's interpolation cache as well as the transform.
        // Unlike SetPositionAndRotation this does not reset abilities every frame.
        if (motor.KinematicObjectIndex >= 0)
            KinematicObjectManager.SetCharacterPosition(motor.KinematicObjectIndex, desired);
        player.transform.position = desired;
        PositionWrites++;
    }

    private bool IsObstacle(Collider other)
    {
        if (!other || !other.enabled || other.isTrigger || other.transform.IsChildOf(player.transform)) return false;
        var ignored = motor.IgnoredColliders;
        if (ignored != null) foreach (var own in ignored) if (own == other) return false;
        return true;
    }

    private void Shape(Vector3 position, out Vector3 center, out float radius, out float halfHeight)
    {
        var scale = capsule.transform.lossyScale;
        center = position + capsule.transform.TransformPoint(capsule.center) - player.transform.position;
        radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        halfHeight = Mathf.Max(radius, capsule.height * Mathf.Abs(scale.y) * 0.5f);
    }

    private bool Clear(Vector3 position)
    {
        Shape(position, out var center, out var radius, out var halfHeight);
        // Tiny skin clearance avoids treating contact with the floor as a wall.
        var spacing = Mathf.Max(0.03f, motor.SkinWidth);
        var a = center + Vector3.up * (halfHeight - radius);
        var b = center - Vector3.up * (halfHeight - radius) + Vector3.up * spacing;
        foreach (var hit in Physics.OverlapCapsule(a, b, Mathf.Max(0.05f, radius - 0.01f), mask, QueryTriggerInteraction.Ignore))
            if (IsObstacle(hit)) return false;
        return true;
    }

    public bool TryPrepareExit()
    {
        ReturnedToEntry = false;
        // F7 OFF also does not require a ground raycast. Restore normal movement
        // where the user is; only an actual body overlap requests entry recovery.
        try
        {
            Physics.SyncTransforms();
            if (!Clear(desired))
            {
                desired = entry;
                ReturnedToEntry = true;
            }
        }
        catch (Exception e) { log("NOCLIP EXIT overlap check unavailable; releasing at current position: " + e.Message); }
        ApplyPose();
        log("NOCLIP EXIT " + (ReturnedToEntry ? "returned to entry after body overlap " : "released at current position ") + desired);
        return true;
    }

    public void Restore()
    {
        var errors = new List<string>();
        void Attempt(Action a) { try { a(); } catch (Exception e) { errors.Add(e.Message); } }
        if (motor)
        {
            Attempt(ZeroMotion);
            Attempt(() => motor.UseGravity = gravity);
            Attempt(() => motor.DetectHorizontalCollisions = horizontal);
            Attempt(() => motor.DetectVerticalCollisions = vertical);
            Attempt(() => motor.StickToGround = stick);
        }
        if (errors.Count != 0) throw new InvalidOperationException(string.Join("; ", errors));
        log("NOCLIP RESTORED motor=" + MotorId);
    }

    public object Describe() => new {
        MotorId, PlayerName = player ? player.name : "destroyed", PositionWrites, MotorBlocks,
        InputFrames, RiseFrames, DescendFrames, MaxPositionCorrection,
        InputSource = inputSource, HorizontalInput = horizontalInput, ForwardInput = forwardInput,
        JumpInputs = jumpInputs.ToArray(), CrouchInputs = crouchInputs.ToArray(), SprintInputs = sprintInputs.ToArray(),
        HeldVrBindings = vrHeld.Status,
        Eligibility = InspectPlayer(player),
        OriginalGravity = gravity, OriginalHorizontalCollisions = horizontal,
        OriginalVerticalCollisions = vertical, OriginalStickToGround = stick,
        CollisionMask = mask, Entry = new { entry.x, entry.y, entry.z },
        Position = new { desired.x, desired.y, desired.z }, ReturnedToEntry
    };

    public static Dictionary<string, object?> InspectPlayer(NolanBehaviour? target)
    {
        var data = new Dictionary<string, object?>();
        void Read(string key, Func<object?> get) {
            try { data[key] = get(); } catch (Exception e) { data[key] = "Unavailable: " + e.GetBaseException().Message; }
        }
        Read("PlayerPresent", () => (bool)target!);
        if (!target) return data;
        Read("Name", () => target!.name);
        Read("PlayerId", () => target!.GetInstanceID());
        Read("Active", () => target!.gameObject.activeInHierarchy);
        Read("MatchesLocalPlayer", () => {
            var local = InGameHelpers.singleton?.GetLocalNolanBehaviour();
            return local && local!.GetInstanceID() == target!.GetInstanceID();
        });
        Read("EntityAttached", () => target!.entity && target.entity.IsAttached);
        Read("EntityIsOwner", () => target!.entity && target.entity.IsOwner);
        Read("EntityHasControl", () => target!.entity && target.entity.HasControl);
        Read("MotorEnabled", () => target!.locomotion && target.locomotion.enabled);
        Read("MotorAlive", () => target!.locomotion && target.locomotion.Alive);
        Read("ManualMove", () => target!.locomotion && target.locomotion.ManualMove);
        Read("MotorTimeScale", () => target!.locomotion ? target.locomotion.TimeScale : null);
        Read("Crawling", () => target!.IsCrawling());
        Read("BeingKnockedOut", () => target!.IsBeingKnockedOut());
        Read("BeingCarried", () => target!.IsBeingCarried());
        Read("BeingHeld", () => target!.IsBeingHeld());
        Read("Hiding", () => target!.IsHiding());
        Read("InputDisabledFromKnockout", () => target!.inputDisabledFromKnockout);
        Read("CameraPresent", () => (bool)target!.cameraController);
        Read("CameraMatchesPlayer", () => target!.cameraController && target.cameraController.Character == target.gameObject);
        Read("VRMode", () => PlayerControls.IsVR(target!));
        Read("VRMovementDirection", () => target!.vrMovementDirection.ToString());
        Read("GameplayInputAllowed", () => PlayerControls.InputAllowed(target!));
        Read("Ending", () => target!.m_Survival && (target.m_Survival.IsEndingPlaying() || target.m_Survival.StartingToPlayFailEnding()));
        Read("ActualPosition", () => { var p = target!.transform.position; return new { p.x, p.y, p.z }; });
        return data;
    }
}
