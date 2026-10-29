using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppRewired;
using InputAction = UnityEngine.InputSystem.InputAction;
using UnityEngine;

namespace DevourMonsterFreeze;

// Delay a right-only click until release so a later left click can turn it into
// a freeze chord. Once a chord fires, both clicks must be released to rearm.
// Reset discards pending clicks across menus, scene changes and lost devices.
internal sealed class ThumbstickGestures
{
    private bool armed;
    private bool chordConsumed;
    public bool NoclipPending { get; private set; }
    public void Reset() { armed = false; chordConsumed = false; NoclipPending = false; }
    public (bool freeze, bool noclip) Sample(bool available, bool left, bool right)
    {
        if (!available) { Reset(); return (false, false); }
        if (!armed) {
            armed = !left && !right;
            return (false, false);
        }
        if (chordConsumed) {
            if (!left && !right) chordConsumed = false;
            return (false, false);
        }
        if (left && right) {
            chordConsumed = true;
            NoclipPending = false;
            return (true, false);
        }
        if (right) { NoclipPending = true; return (false, false); }
        if (NoclipPending) { NoclipPending = false; return (false, true); }
        return (false, false);
    }
}

internal static class PlayerControls
{
    public static bool IsVR(NolanBehaviour player) => SessionHelpers.singleton != null &&
        SessionHelpers.singleton.vrMode && player.m_SurvivalVR;

    public static bool InputAllowed(NolanBehaviour player)
    {
        var ui = player.gameUI;
        if (Time.timeScale <= 0 || (ui && (ui.MenuIsOpen() || ui.TextChatIsOpen()))) return false;
        if (player.locomotionHandler && !player.locomotionHandler.m_InputEnabled) return false;
        if (!IsVR(player)) return Application.isFocused;
        var presence = player.m_SurvivalVR.m_VRUserPresence;
        // VR can keep input focus while the desktop mirror is unfocused.
        // Use the same headset-presence state as DEVOUR in that case.
        return presence ? presence.IsUserPresent() : Application.isFocused;
    }

    public static VRCustomController? Controller(NolanBehaviour player)
    {
        if (!IsVR(player)) return null;
        var controller = player.m_SurvivalVR.m_CustomController;
        return controller && controller.isActiveAndEnabled ? controller : null;
    }

    public static int Device(InputAction? action)
    {
        if (action == null || !action.enabled) return 0;
        var controls = action.controls;
        for (var i = 0; i < controls.Count; i++) {
            var device = controls[i].device;
            if (device != null && device.added && device.enabled) return device.deviceId;
        }
        return 0;
    }

    public static bool Held(InputAction? action) => Device(action) != 0 && action!.IsPressed();

    public static Vector3 Forward(NolanBehaviour player)
    {
        var forward = player.transform.forward;
        if (IsVR(player)) {
            var vr = player.m_SurvivalVR;
            if (player.vrMovementDirection == VRMovementDirection.Head && vr.m_VRCamera)
                forward = vr.m_VRCamera.transform.forward;
            else if (player.vrMovementDirection == VRMovementDirection.Controller) {
                var hand = vr.m_LeftHandedMode ? vr.m_RightVRHand : vr.m_LeftVRHand;
                if (hand) forward = hand.transform.forward;
            }
        } else {
            var camera = player.cameraController ? player.cameraController.CameraTransform : null;
            if (camera) forward = camera!.forward;
        }
        forward.y = 0;
        if (forward.sqrMagnitude < 0.001f) { forward = player.transform.forward; forward.y = 0; }
        if (forward.sqrMagnitude < 0.001f) forward = new Vector3(0, 0, 1);
        return forward.normalized;
    }

    public static float Axis(NolanBehaviour player, string? action)
    {
        if (!player.devourInput || string.IsNullOrWhiteSpace(action)) return 0;
        var value = player.devourInput.GetAxisRaw(action);
        // Rewired already applies the user's controller dead zones. Only reject
        // non-finite values and tiny residue; do not add a second large dead zone.
        return float.IsFinite(value) && Mathf.Abs(value) > 0.001f ? Mathf.Clamp(value, -1, 1) : 0;
    }
}

internal sealed class VrToggleControls
{
    private readonly ThumbstickGestures gestures = new();
    private int playerId, controllerId;
    public long FreezePresses { get; private set; }
    public long NoclipPresses { get; private set; }
    public string Status { get; private set; } = "Waiting for VR player";
    public string Profile { get; private set; } = "";
    public int LeftDevice { get; private set; }
    public int RightDevice { get; private set; }
    public void Reset() { gestures.Reset(); playerId = controllerId = 0; LeftDevice = RightDevice = 0; }

    public (bool freeze, bool noclip) Poll(NolanBehaviour? player, bool host)
    {
        try {
            if (!player || !host) { Reset(); Status = "Waiting for local solo/host VR player"; return (false, false); }
            var controller = PlayerControls.Controller(player!);
            if (!controller) { Reset(); Status = "VR controller not active"; return (false, false); }
            if (playerId != player!.GetInstanceID() || controllerId != controller!.GetInstanceID()) {
                Reset(); playerId = player.GetInstanceID(); controllerId = controller!.GetInstanceID();
            }
            Profile = controller!.type.ToString();
            var available = PlayerControls.InputAllowed(player);
            var leftDevice = PlayerControls.Device(controller.leftHandThumbstickButton);
            var rightDevice = PlayerControls.Device(controller.rightHandThumbstickButton);
            if (leftDevice != LeftDevice || rightDevice != RightDevice) gestures.Reset();
            LeftDevice = leftDevice; RightDevice = rightDevice;
            var (freeze, noclip) = gestures.Sample(available && RightDevice != 0,
                PlayerControls.Held(controller.leftHandThumbstickButton),
                PlayerControls.Held(controller.rightHandThumbstickButton));
            if (freeze) FreezePresses++;
            if (noclip) NoclipPresses++;
            Status = !available ? "Gameplay input suspended" : $"Listening; leftDevice={LeftDevice}; rightDevice={RightDevice}";
            return (freeze, noclip);
        } catch (Exception e) {
            Reset(); Status = "VR input unavailable: " + e.GetBaseException().Message;
            return (false, false);
        }
    }

    public object Describe() => new {
        Status, Profile, LeftDevice, RightDevice, FreezePresses, NoclipPresses,
        Layout = "Both stick clicks: freeze; right-only release: noclip; left alone: normal game action",
        gestures.NoclipPending
    };
}

// Some DEVOUR VR face-button mappings emit only a one-frame performed pulse.
// Resolve their current Rewired bindings to the original InputActions so a
// held jump/crouch still means continuous ascent/descent. No mappings are edited.
internal sealed class VrHeldActions
{
    private readonly Dictionary<string, List<(ControllerMap map, ActionElementMap binding, int button)>> bindings = new();
    private int controllerId;
    private float refreshAt;
    public string Status { get; private set; } = "Not needed";

    public void Refresh(NolanBehaviour player, IEnumerable<string> actions)
    {
        var controller = PlayerControls.Controller(player);
        if (!controller) { bindings.Clear(); controllerId = 0; Status = "No active VR controller"; return; }
        if (controllerId == controller!.GetInstanceID() && Time.unscaledTime < refreshAt) return;
        controllerId = controller.GetInstanceID(); refreshAt = Time.unscaledTime + 1;
        bindings.Clear();
        if (controller.type != VRCustomControllerType.OCULUS_TOUCH &&
            controller.type != VRCustomControllerType.VALVE_INDEX &&
            controller.type != VRCustomControllerType.HP_REVERB_G2) {
            Status = "Using native held actions for " + controller.type; return;
        }
        try {
            if (controller.player == null || controller.controller == null) { Status = "VR mapping not ready"; return; }
            int count = 0;
            foreach (var action in actions) {
                if (bindings.ContainsKey(action)) continue;
                var found = new List<(ControllerMap, ActionElementMap, int)>();
                var elements = new Il2CppSystem.Collections.Generic.List<ActionElementMap>();
                controller.player.controllers.maps.GetElementMapsWithAction(controller.controller, action, true, elements);
                for (var i = 0; i < elements.Count; i++) {
                    var binding = elements[i];
                    if (!binding.enabled || binding.elementType != ControllerElementType.Button || binding.controllerMap == null) continue;
                    var button = controller.controller.GetButtonIndexById(binding.elementIdentifierId);
                    if (button >= 0 && button <= 9) { found.Add((binding.controllerMap, binding, button)); count++; }
                }
                bindings[action] = found;
            }
            Status = "Resolved held VR bindings: " + count;
        } catch (Exception e) {
            bindings.Clear(); Status = "Using native actions; held lookup unavailable: " + e.GetBaseException().Message;
        }
    }

    public bool Held(NolanBehaviour player, string action)
    {
        if (player.devourInput.GetButton(action)) return true;
        var controller = PlayerControls.Controller(player);
        if (!controller || controller!.GetInstanceID() != controllerId || !bindings.TryGetValue(action, out var found)) return false;
        var leftHanded = player.m_SurvivalVR.m_LeftHandedMode;
        foreach (var entry in found) {
            if (!entry.map.enabled || !entry.binding.enabled) continue;
            InputAction? input = entry.button switch {
                0 => controller.rightHandPrimaryButton,
                1 => leftHanded ? controller.rightHandSecondaryButtonLeftHanded : controller.rightHandSecondaryButton,
                2 => controller.leftHandPrimaryButton,
                3 => leftHanded ? controller.leftHandSecondaryButtonLeftHanded : controller.leftHandSecondaryButton,
                4 => controller.rightHandTriggerButton,
                5 => controller.leftHandTriggerButton,
                6 => controller.rightHandGripButton,
                7 => controller.leftHandGripButton,
                8 => controller.rightHandThumbstickButton,
                9 => controller.leftHandThumbstickButton,
                _ => null
            };
            if (PlayerControls.Held(input)) return true;
        }
        return false;
    }
}
