// Test doubles only. These are never referenced by or shipped in the mod DLL.
#nullable disable
using System;
using System.Collections.Generic;

namespace UnityEngine {
    public class Object {
        static int nextId;
        readonly int id = ++nextId;
        public bool destroyed;
        public string name = "test";
        public int GetInstanceID() => id;
        public static implicit operator bool(Object o) => o != null && !o.destroyed;
    }
    public class Component : Object {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    }
    public class GameObject : Object {
        public bool activeInHierarchy = true;
        public Transform transform;
        readonly Dictionary<Type, Component> components = new();
        public GameObject() { transform = new Transform { gameObject = this }; }
        public T Add<T>(T item) where T : Component { item.gameObject = this; components[typeof(T)] = item; return item; }
        public T GetComponent<T>() where T : Component => components.TryGetValue(typeof(T), out var c) ? (T)c : null;
    }
    public class Transform : Component {
        public Vector3 position;
        public Vector3 up = Vector3.up;
        public Vector3 forward = new(0,0,1);
        public Vector3 lossyScale = new(1,1,1);
        public Transform parent;
        public bool IsChildOf(Transform t) => this == t || (parent && parent.IsChildOf(t));
        public Vector3 TransformPoint(Vector3 p) => position + p;
    }
    public class Collider : Component { public bool enabled = true, isTrigger; }
    public class CapsuleCollider : Collider { public int direction = 1; public Vector3 center = new(0,1,0); public float radius = .4f, height = 2; }
    public class Rigidbody : Component { public bool isKinematic = true; }
    public struct LayerMask { public int value; }
    public struct Vector3 {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 zero => new(0,0,0);
        public static Vector3 up => new(0,1,0);
        public float sqrMagnitude => x*x+y*y+z*z;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public Vector3 normalized => magnitude > 0 ? this * (1/magnitude) : zero;
        public void Normalize() { this = normalized; }
        public static float Dot(Vector3 a,Vector3 b) => a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 Cross(Vector3 a,Vector3 b) => new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        public static Vector3 ClampMagnitude(Vector3 v,float n) => v.magnitude>n?v.normalized*n:v;
        public static Vector3 operator +(Vector3 a,Vector3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b) => new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float f) => new(a.x*f,a.y*f,a.z*f);
        public override string ToString() => $"({x:F3},{y:F3},{z:F3})";
    }
    public static class Mathf {
        public static float Max(float a,float b)=>MathF.Max(a,b);
        public static float Abs(float f)=>MathF.Abs(f);
        public static float Clamp(float f,float a,float b)=>Math.Clamp(f,a,b);
    }
    public enum QueryTriggerInteraction { Ignore }
    public static class Physics {
        public static int Queries;
        public static bool ThrowQueries;
        public static Collider[] Hits = Array.Empty<Collider>();
        public static void SyncTransforms() { if(ThrowQueries)throw new Exception("physics unavailable"); }
        public static Collider[] OverlapCapsule(Vector3 a,Vector3 b,float r,int mask,QueryTriggerInteraction q) {
            Queries++;if(ThrowQueries)throw new Exception("physics unavailable");return Hits;
        }
    }
    public enum KeyCode { W,A,S,D,Space,LeftShift,RightShift }
    public static class Input {
        public static HashSet<KeyCode> Keys = new();
        public static bool GetKey(KeyCode k) => Keys.Contains(k);
    }
    public static class Time { public static float timeScale = 1, unscaledDeltaTime = .02f, unscaledTime; }
    public static class Application { public static bool isFocused = true; }
}
namespace Il2Cpp {
    using UnityEngine;
    using Il2CppOpsive.UltimateCharacterController.Character;
    public class Entity : Object { public bool IsAttached=true,IsOwner=true,HasControl; }
    public class CameraController : Object { public GameObject Character; public Transform CameraTransform; }
    public class GameUI : Object {
        public bool menu,chat;
        public bool MenuIsOpen()=>menu;
        public bool TextChatIsOpen()=>chat;
    }
    public class DevourInput : Object {
        public HashSet<string> Buttons=new();
        public Dictionary<string,float> Axes=new();
        public bool GetButton(string s)=>Buttons.Contains(s);
        public float GetAxisRaw(string s)=>Axes.TryGetValue(s,out var v)?v:0;
    }
    public class HeightChange { public string[] InputNames = new[]{"Crouch"}; }
    public class Survival : Object { public bool ending,failEnding; public bool IsEndingPlaying()=>ending;public bool StartingToPlayFailEnding()=>failEnding; }
    public class NolanBehaviour : Component {
        public Il2CppOpsive.UltimateCharacterController.Traits.Attribute batteryAttribute;
        public Il2CppHorror.DevourGameMode m_gameMode;
        public bool isUsingBatteryBonus;
        public UltimateCharacterLocomotion locomotion;
        public CapsuleCollider m_CapsuleCollider;
        public Entity entity=new();
        public CameraController cameraController=new();
        public DevourInput devourInput=new();
        public HeightChange heightChangeAbility=new();
        public HeightChange speedChangeAbility=new(){InputNames=new[]{"Sprint"}};
        public UltimateCharacterLocomotionHandler locomotionHandler=new();
        public SurvivalVR m_SurvivalVR;
        public VRMovementDirection vrMovementDirection;
        public GameUI gameUI=new();
        public Survival m_Survival=new();
        public bool crawling,knockout,carried,held,hiding,inputDisabledFromKnockout;
        public bool IsCrawling()=>crawling;
        public bool IsBeingKnockedOut()=>knockout;
        public bool IsBeingCarried()=>carried;
        public bool IsBeingHeld()=>held;
        public bool IsHiding()=>hiding;
    }
    public class InGameHelpers {
        public static InGameHelpers singleton=new();
        public NolanBehaviour local;
        public NolanBehaviour GetLocalNolanBehaviour()=>local;
    }
}
namespace Il2CppOpsive.UltimateCharacterController.Character {
    using UnityEngine;
    public class UltimateCharacterLocomotion : Component {
        public bool enabled=true, Alive=true, ManualMove;
        public bool UseGravity=true, DetectHorizontalCollisions=true, DetectVerticalCollisions=true, StickToGround=true;
        public float TimeScale=1,SkinWidth=.03f,GravityAmount;
        public LayerMask ColliderLayerMask=new(){value=-1};
        public Vector3 MoveDirection,MotorThrottle,Velocity,ExternalForce;
        public int KinematicObjectIndex=0;
        public Collider[] IgnoredColliders=Array.Empty<Collider>();
        public Abilities.Ability[] Abilities=new Abilities.Ability[]{new Abilities.Jump()};
    }
    public class UltimateCharacterLocomotionHandler : Object {
        public bool m_InputEnabled=true;
        public string HorizontalInputName="Horizontal",ForwardInputName="Vertical";
    }
}
namespace Il2CppOpsive.UltimateCharacterController.Game {
    using UnityEngine;
    public static class KinematicObjectManager {
        public static Vector3 LastPosition;
        public static int Writes;
        public static void SetCharacterPosition(int i, Vector3 p) { LastPosition=p;Writes++; }
    }
}
