// Test doubles, excluded from the production build.
#nullable disable
using System.Collections.Generic;

namespace Il2CppOpsive.UltimateCharacterController.Character.Abilities {
    public class Ability {
        public string[] InputNames=new[]{"Jump"};
        public T TryCast<T>() where T:class=>this as T;
    }
    public class Jump : Ability {}
}
namespace UnityEngine.InputSystem {
    public class InputDevice { static int next; public int deviceId=++next;public bool added=true,enabled=true; }
    public class InputControl { public InputDevice device=new(); }
    public class InputAction {
        public bool enabled=true,pressed;
        public List<InputControl> controls=new(){new InputControl()};
        public bool IsPressed()=>pressed;
    }
}
namespace Il2CppSystem.Collections.Generic {
    public class List<T> : System.Collections.Generic.List<T> {}
}
namespace Il2CppRewired {
    public enum ControllerElementType { Axis,Button }
    public class ControllerMap { public bool enabled=true; }
    public class ActionElementMap {
        public bool enabled=true;
        public ControllerElementType elementType=ControllerElementType.Button;
        public int elementIdentifierId;
        public ControllerMap controllerMap=new();
    }
    public class CustomController {
        public Dictionary<int,int> indices=new();
        public int GetButtonIndexById(int id)=>indices.TryGetValue(id,out var i)?i:-1;
    }
    public class Player { public ControllerHelper controllers=new(); }
    public class ControllerHelper { public MapHelper maps=new(); }
    public class MapHelper {
        public Dictionary<string,List<ActionElementMap>> actions=new();
        public int GetElementMapsWithAction(CustomController c,string action,bool skipDisabled,Il2CppSystem.Collections.Generic.List<ActionElementMap> result) {
            if(actions.TryGetValue(action,out var elements)) foreach(var e in elements)
                if(!skipDisabled||e.controllerMap.enabled) result.Add(e);
            return result.Count;
        }
    }
}
namespace Il2Cpp {
    using UnityEngine;
    using UnityEngine.InputSystem;
    public class SessionHelpers { public static SessionHelpers singleton=new();public bool vrMode; }
    public enum VRMovementDirection { Head,Controller,Character }
    public enum VRCustomControllerType { OCULUS_TOUCH,HTC_VIVE,VALVE_INDEX,HP_REVERB_G2,SAMSUNG_ODYSSEY }
    public class VRUserPresence : Object { public bool present=true;public bool IsUserPresent()=>present; }
    public class VRHand : Component {}
    public class SurvivalVR : Object {
        public VRCustomController m_CustomController=new();
        public bool m_LeftHandedMode;
        public GameObject m_VRCamera=new();
        public VRHand m_LeftVRHand=new GameObject().Add(new VRHand()),m_RightVRHand=new GameObject().Add(new VRHand());
        public VRUserPresence m_VRUserPresence=new();
    }
    public class VRCustomController : Object {
        public VRCustomControllerType type;
        public bool isActiveAndEnabled=true;
        public Il2CppRewired.Player player=new();
        public Il2CppRewired.CustomController controller=new();
        public InputAction leftHandThumbstickButton=new(),rightHandThumbstickButton=new();
        public InputAction leftHandPrimaryButton=new(),rightHandPrimaryButton=new();
        public InputAction leftHandSecondaryButton=new(),rightHandSecondaryButton=new();
        public InputAction leftHandSecondaryButtonLeftHanded=new(),rightHandSecondaryButtonLeftHanded=new();
        public InputAction leftHandTriggerButton=new(),rightHandTriggerButton=new();
        public InputAction leftHandGripButton=new(),rightHandGripButton=new();
    }
}
