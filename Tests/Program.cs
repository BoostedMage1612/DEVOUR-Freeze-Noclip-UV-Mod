using System;
using System.Collections.Generic;
using DevourMonsterFreeze;
using Il2Cpp;
using Il2CppOpsive.UltimateCharacterController.Character;
using Il2CppOpsive.UltimateCharacterController.Game;
using UnityEngine;

static partial class Program {
    static int passed;
    static readonly List<string> logs = new();
    static void Check(bool b,string message) { if(!b)throw new Exception(message); }
    static void Test(string name,Action action) { Reset();action();passed++;Console.WriteLine("PASS "+name); }
    static void Reset() {
        Input.Keys.Clear();Physics.Queries=0;Physics.ThrowQueries=false;Physics.Hits=Array.Empty<Collider>();
        Application.isFocused=true;Time.timeScale=1;Time.unscaledDeltaTime=.02f;logs.Clear();
        Time.unscaledTime=0;SessionHelpers.singleton=new();
    }
    static NolanBehaviour Player() {
        var go=new GameObject();
        var p=go.Add(new NolanBehaviour());p.name="SurvivalKai(Clone)";
        p.locomotion=go.Add(new UltimateCharacterLocomotion());
        p.m_CapsuleCollider=go.Add(new CapsuleCollider());go.Add(new Rigidbody());
        p.cameraController.Character=go;p.cameraController.CameraTransform=new GameObject().transform;
        InGameHelpers.singleton.local=p;return p;
    }
    static (NoclipCore core,NoclipLease lease) Start(NolanBehaviour p) {
        var c=new NoclipCore();var l=new NoclipLease(p,logs.Add);
        Check(c.Start(true,()=>l),"Start failed: "+c.Fault);return(c,l);
    }
    static void Near(float a,float b,string why) => Check(MathF.Abs(a-b)<.0001f,why+$": {a} vs {b}");
    public static void Main() {
        Test("owner without Bolt HasControl can activate",()=> {
            var p=Player();Check(!p.entity.HasControl,"fixture must reproduce absent HasControl");
            var(c,l)=Start(p);Check(c.Active&&!p.locomotion.UseGravity,"flight is active");
        });
        Test("remote character owned by host is rejected",()=> {
            var remote=Player();Player();var c=new NoclipCore();
            Check(!c.Start(true,()=>new NoclipLease(remote,logs.Add)),"remote must be rejected");
            Check(c.Fault.Contains("local player")&&remote.locomotion.UseGravity,"reason retained; remote untouched");
        });
        Test("nonowner controller remains eligible when selected local",()=> {var p=Player();p.entity.IsOwner=false;p.entity.HasControl=true;Start(p);});
        Test("neither owner nor controller gives explicit reason",()=> {
            var p=Player();p.entity.IsOwner=false;var c=new NoclipCore();
            Check(!c.Start(true,()=>new NoclipLease(p,logs.Add))&&c.Fault.Contains("neither"),"rejection reason");
        });
        Test("no ground queries on activation or hover",()=> {var p=Player();Physics.ThrowQueries=true;var(c,l)=Start(p);c.Maintain(true);l.UpdateInput();Check(Physics.Queries==0&&c.Active,"ground must not gate flight");});
        Test("motor Alive and alternate camera attachment are not identity gates",()=> {var p=Player();p.locomotion.Alive=false;p.cameraController.Character=new GameObject();Start(p);});
        Test("missing camera uses character direction",()=> {var p=Player();p.cameraController=null!;var(c,l)=Start(p);Input.Keys.Add(KeyCode.W);l.UpdateInput();Check(p.transform.position.z>0&&c.Active,"fallback direction");});
        Test("Space rises and hover holds altitude",()=> {
            var p=Player();var(c,l)=Start(p);Input.Keys.Add(KeyCode.Space);l.UpdateInput();Near(p.transform.position.y,.06f,"rise");
            Input.Keys.Clear();l.UpdateInput();Near(p.transform.position.y,.06f,"hover");Check(l.RiseFrames==1&&l.InputFrames==1,"input counts");
        });
        Test("rebound crouch action descends",()=> {var p=Player();p.heightChangeAbility.InputNames=new[]{"MyCrouchAction"};var(c,l)=Start(p);p.devourInput.Buttons.Add("MyCrouchAction");l.UpdateInput();Near(p.transform.position.y,-.06f,"descend");Check(l.DescendFrames==1,"crouch counted");});
        Test("WASD preserves altitude when looking up",()=> {var p=Player();p.cameraController.CameraTransform.forward=new(0,1,1);var(c,l)=Start(p);Input.Keys.Add(KeyCode.W);l.UpdateInput();Near(p.transform.position.y,0,"horizontal altitude");Near(p.transform.position.z,.06f,"forward");});
        Test("diagonal normalized and long frame capped",()=> {var p=Player();var(c,l)=Start(p);Input.Keys.UnionWith(new[]{KeyCode.W,KeyCode.D,KeyCode.Space,KeyCode.LeftShift});Time.unscaledDeltaTime=2;l.UpdateInput();Near(p.transform.position.magnitude,.4f,"capped diagonal boost");});
        foreach(var flag in new[]{"menu","chat","focus","pause"}) Test("input suspended for "+flag,()=> {
            var p=Player();var(c,l)=Start(p);Input.Keys.Add(KeyCode.W);
            if(flag=="menu")p.gameUI.menu=true;if(flag=="chat")p.gameUI.chat=true;
            if(flag=="focus")Application.isFocused=false;if(flag=="pause")Time.timeScale=0;
            l.UpdateInput();Near(p.transform.position.magnitude,0,"movement while UI paused");Check(c.Active,"hover maintained");
        });
        Test("position and interpolation cache agree",()=> {var p=Player();var(c,l)=Start(p);Input.Keys.Add(KeyCode.D);l.UpdateInput();Near(KinematicObjectManager.LastPosition.x,p.transform.position.x,"cache sync");p.transform.position=new(8,4,3);l.ApplyPose();Near(p.transform.position.x,.06f,"recover overridden position");Check(l.MaxPositionCorrection>5,"correction recorded");});
        Test("exit restores exact nondefault flags",()=> {var p=Player();p.locomotion.DetectHorizontalCollisions=false;p.locomotion.StickToGround=false;var(c,l)=Start(p);Check(c.TryExit(),"exit");Check(p.locomotion.UseGravity&&!p.locomotion.DetectHorizontalCollisions&&p.locomotion.DetectVerticalCollisions&&!p.locomotion.StickToGround,"original flags");});
        Test("overlap exit returns to entry",()=> {var p=Player();var(c,l)=Start(p);Input.Keys.Add(KeyCode.W);l.UpdateInput();Physics.Hits=new[]{new GameObject().Add(new Collider())};Check(c.TryExit(),"exit");Near(p.transform.position.z,0,"entry recovery");Check(l.ReturnedToEntry,"overlap recorded");});
        Test("unavailable physics does not prevent exit",()=> {var p=Player();var(c,l)=Start(p);Physics.ThrowQueries=true;Check(c.TryExit()&&!c.Active,"exit when queries fail");});
        Test("local replacement restores previous motor without teleport",()=> {var old=Player();var(c,l)=Start(old);var next=Player();next.transform.position=new(50,6,7);c.Maintain(true);Check(!c.Active&&old.locomotion.UseGravity,"old motor restored");Near(next.transform.position.x,50,"new player untouched");Check(c.Fault.Contains("local player"),"lifecycle reason");});
        Test("host loss restores captured settings",()=> {var p=Player();var(c,l)=Start(p);c.Maintain(false);Check(!c.Active&&p.locomotion.UseGravity&&c.Fault.Contains("session"),"authority cleanup");});
        foreach(var flag in new[]{"crawling","knockout","carried","held","hiding","inactive","detached","disabled","ending"}) Test("release on "+flag,()=> {
            var p=Player();var(c,l)=Start(p);
            switch(flag) {case "crawling":p.crawling=true;break;case "knockout":p.knockout=true;break;case "carried":p.carried=true;break;case "held":p.held=true;break;case "hiding":p.hiding=true;break;case "inactive":p.gameObject.activeInHierarchy=false;break;case "detached":p.entity.IsAttached=false;break;case "disabled":p.locomotion.enabled=false;break;case "ending":p.m_Survival.ending=true;break;}
            c.Maintain(true);Check(!c.Active&&p.locomotion.UseGravity&&c.Fault.Length>0,"specific cleanup reason and restore");
        });
        Test("rejected session never captures",()=> {var c=new NoclipCore();Check(!c.Start(false,()=>throw new Exception("must not execute")),"host guard");});
        Test("partial entry failure restores before retry",()=> {var f=new FakeLease{FailEnter=true};var c=new NoclipCore();Check(!c.Start(true,()=>f)&&f.Restores==1&&!c.Restoring,"entry rollback");});
        Test("failed restoration retains snapshot then retries",()=> {var f=new FakeLease();var c=new NoclipCore();Check(c.Start(true,()=>f),"start fake");f.FailRestore=true;c.Stop();Check(c.Restoring&&!c.Active,"pending restore");f.FailRestore=false;c.RetryRestore();Check(!c.Restoring&&f.Restores==2,"retry");});
        Test("diagnostic captures absent HasControl",()=> {var p=Player();var d=NoclipLease.InspectPlayer(p);Check((bool)d["EntityIsOwner"]!&&!(bool)d["EntityHasControl"]!&&(bool)d["MatchesLocalPlayer"]!,"eligibility evidence");});
        Test("freeze original capture preserved through repeated arming",()=> {var c=new FreezeCore();var l=new FreezeFake();Check(c.Arm(true)&&c.Track(1,()=>l),"freeze");Check(c.Track(1,()=>throw new Exception("duplicate capture")),"idempotence");c.ReleaseAll();Check(l.Restores==1&&!c.Requested,"release once");});
        VrTests();
        UvTests();
        Console.WriteLine($"ALL {passed} REGRESSION CHECKS PASSED (stand-ins, not live Unity)");
    }
    sealed class FakeLease : INoclipLease {
        public bool IsAlive=>true;public bool CanControl=>true;public string ControlBlockReason=>"";
        public bool FailEnter,FailRestore;public int Restores;
        public void Enter(){if(FailEnter)throw new Exception("entry fault");}public void Hold(){}public bool TryPrepareExit()=>true;
        public void Restore(){Restores++;if(FailRestore)throw new Exception("restore fault");}
    }
    sealed class FreezeFake : IFreezeLease {
        public long Key=>1;public bool IsAlive=>true;public bool CanHold=>true;public int Restores;
        public void Hold(){}public void Restore(){Restores++;}
    }
}
