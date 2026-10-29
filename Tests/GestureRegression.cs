using System;
using DevourMonsterFreeze;
using Il2Cpp;
using UnityEngine;

static partial class Program {
    static (bool freeze,bool noclip) Clicks(VrToggleControls v,NolanBehaviour p,bool left,bool right) {
        var c=p.m_SurvivalVR.m_CustomController;
        c.leftHandThumbstickButton.pressed=left;c.rightHandThumbstickButton.pressed=right;
        return v.Poll(p,true);
    }
    static void GestureTests() {
        Test("left click alone leaves run input available without a mod toggle",()=> {
            var p=VrPlayer();var v=new VrToggleControls();Clicks(v,p,false,false);
            for(int press=0;press<3;press++) {
                for(int frame=0;frame<20;frame++)Check(Clicks(v,p,true,false)==(false,false),"left hold");
                var action=p.m_SurvivalVR.m_CustomController.leftHandThumbstickButton;
                Check(action.enabled&&action.pressed,"native action unmodified");
                Check(Clicks(v,p,false,false)==(false,false),"left release");
            }
            Check(v.FreezePresses==0&&v.NoclipPresses==0,"no mod action on run clicks");
        });
        Test("right click toggles noclip once on release",()=> {
            var p=VrPlayer();var v=new VrToggleControls();Clicks(v,p,false,false);
            for(int click=0;click<2;click++) {
                for(int i=0;i<100;i++)Check(Clicks(v,p,false,true)==(false,false),"no toggle while held");
                Check(Clicks(v,p,false,false)==(false,true),"one toggle on release");
                Check(Clicks(v,p,false,false)==(false,false),"release does not repeat");
            }
            Check(v.NoclipPresses==2&&v.FreezePresses==0,"right click counts");
        });
        foreach(var order in new[]{"together","left first","right first"})
        Test("freeze chord consumes noclip with "+order,()=> {
            var p=VrPlayer();var v=new VrToggleControls();Clicks(v,p,false,false);
            if(order=="left first")Check(Clicks(v,p,true,false)==(false,false),"left lead");
            if(order=="right first") {
                // No narrow timing window: an overlapping press still wins even
                // if the right click has already been held for many frames.
                for(int i=0;i<100;i++)Check(Clicks(v,p,false,true)==(false,false),"right lead");
            }
            Check(Clicks(v,p,true,true)==(true,false),"only freeze on overlap");
            for(int i=0;i<100;i++)Check(Clicks(v,p,true,true)==(false,false),"no held repeat");
            Check(Clicks(v,p,false,false)==(false,false),"no deferred noclip");
            Check(v.FreezePresses==1&&v.NoclipPresses==0,"chord counts");
        });
        foreach(var release in new[]{"together","left first","right first"})
        Test("chord releases do not toggle noclip: "+release,()=> {
            var p=VrPlayer();var v=new VrToggleControls();Clicks(v,p,false,false);
            Check(Clicks(v,p,true,true)==(true,false),"chord");
            if(release=="left first")Check(Clicks(v,p,false,true)==(false,false),"left released");
            if(release=="right first")Check(Clicks(v,p,true,false)==(false,false),"right released");
            Check(Clicks(v,p,false,false)==(false,false),"both released");
            Check(Clicks(v,p,false,true)==(false,false),"next independent right down");
            Check(Clicks(v,p,false,false)==(false,true),"next independent right release works");
            Check(v.FreezePresses==1&&v.NoclipPresses==1,"no swallowed next click");
        });
        Test("freeze chord requires both released before another toggle",()=> {
            var p=VrPlayer();var v=new VrToggleControls();Clicks(v,p,false,false);
            Check(Clicks(v,p,true,true)==(true,false),"first toggle");
            Check(Clicks(v,p,false,true)==(false,false),"one released");
            Check(Clicks(v,p,true,true)==(false,false),"left repress ignored");
            Check(Clicks(v,p,true,false)==(false,false),"other released");
            Check(Clicks(v,p,true,true)==(false,false),"right repress ignored");
            Check(Clicks(v,p,false,false)==(false,false),"rearm both");
            Check(Clicks(v,p,true,true)==(true,false),"second toggle");
        });
        Test("nonoverlapping left and right clicks do not freeze",()=> {
            var p=VrPlayer();var v=new VrToggleControls();Clicks(v,p,false,false);
            Check(Clicks(v,p,true,false)==(false,false),"run press");
            Check(Clicks(v,p,false,false)==(false,false),"run release");
            Check(Clicks(v,p,false,true)==(false,false),"right press");
            Check(Clicks(v,p,false,false)==(false,true),"separate noclip click");
            Check(v.FreezePresses==0,"requires actual overlap");
        });
        foreach(var held in new[]{"left","right","both"})
        Test("held clicks at spawn cannot trigger a gesture: "+held,()=> {
            var p=VrPlayer();var v=new VrToggleControls();
            Check(Clicks(v,p,held!="right",held!="left")== (false,false),"held at spawn");
            Check(Clicks(v,p,true,true)==(false,false),"adding partner before release ignored");
            Check(Clicks(v,p,false,false)==(false,false),"initial release cannot toggle noclip");
            Check(Clicks(v,p,true,true)==(true,false),"fresh chord works");
        });
        foreach(var interruption in new[]{"menu","chat","pause","headset","disabled","disconnect","device_disabled","host","scene","player","profile","input","controller","left_disconnect","left_device_swap","right_device_swap"})
        Test("pending right click discarded after "+interruption,()=> {
            var p=VrPlayer();var v=new VrToggleControls();var c=p.m_SurvivalVR.m_CustomController;Clicks(v,p,false,false);
            Check(Clicks(v,p,false,true)==(false,false),"pending right press");
            switch(interruption) {
                case "menu":p.gameUI.menu=true;break;case "chat":p.gameUI.chat=true;break;
                case "pause":Time.timeScale=0;break;case "headset":p.m_SurvivalVR.m_VRUserPresence.present=false;break;
                case "disabled":c.rightHandThumbstickButton.enabled=false;break;
                case "disconnect":c.rightHandThumbstickButton.controls[0].device.added=false;break;
                case "device_disabled":c.rightHandThumbstickButton.controls[0].device.enabled=false;break;
                case "scene":v.Reset();break;
                case "player":p=VrPlayer();p.m_SurvivalVR.m_CustomController=c;break;
                case "profile":p.m_SurvivalVR.m_CustomController=new();c=p.m_SurvivalVR.m_CustomController;c.rightHandThumbstickButton.pressed=true;break;
                case "input":p.locomotionHandler.m_InputEnabled=false;break;
                case "controller":c.isActiveAndEnabled=false;break;
                case "left_disconnect":c.leftHandThumbstickButton.controls[0].device.added=false;break;
                case "left_device_swap":c.leftHandThumbstickButton.controls[0].device=new();break;
                case "right_device_swap":c.rightHandThumbstickButton.controls[0].device=new();break;
            }
            Check(v.Poll(p,interruption!="host")== (false,false),"blocked/context change");
            p.gameUI.menu=p.gameUI.chat=false;Time.timeScale=1;p.m_SurvivalVR.m_VRUserPresence.present=true;
            c.rightHandThumbstickButton.enabled=true;c.rightHandThumbstickButton.controls[0].device.added=true;
            c.rightHandThumbstickButton.controls[0].device.enabled=true;c.leftHandThumbstickButton.controls[0].device.added=true;
            p.locomotionHandler.m_InputEnabled=true;c.isActiveAndEnabled=true;
            Check(v.Poll(p,true)==(false,false),"held on resume");
            Check(Clicks(v,p,false,false)==(false,false),"stale pending click discarded");
            Check(Clicks(v,p,false,true)==(false,false),"fresh down");
            Check(Clicks(v,p,false,false)==(false,true),"fresh release");
        });
        Test("release in menu cannot become noclip on return",()=> {
            var p=VrPlayer();var v=new VrToggleControls();Clicks(v,p,false,false);Clicks(v,p,false,true);
            p.gameUI.menu=true;Check(Clicks(v,p,false,false)==(false,false),"released in menu");
            p.gameUI.menu=false;Check(Clicks(v,p,false,false)==(false,false),"return idle");
            Check(Clicks(v,p,true,true)==(true,false),"new chord after return");
        });
        Test("right-only toggle remains usable with left controller absent",()=> {
            var p=VrPlayer();p.m_SurvivalVR.m_CustomController.leftHandThumbstickButton.controls[0].device.added=false;
            var v=new VrToggleControls();Clicks(v,p,false,false);
            Check(Clicks(v,p,false,true)==(false,false),"right down without left device");
            Check(Clicks(v,p,false,false)==(false,true),"right release without left device");
        });
        Test("left-handed mode keeps physical right-release and two-stick chord",()=> {
            var p=VrPlayer();p.m_SurvivalVR.m_LeftHandedMode=true;var v=new VrToggleControls();Clicks(v,p,false,false);
            Check(Clicks(v,p,true,false)==(false,false),"left alone unchanged");Clicks(v,p,false,false);
            Clicks(v,p,false,true);Check(Clicks(v,p,false,false)==(false,true),"physical right single");
            Check(Clicks(v,p,true,true)==(true,false),"two-stick chord");
        });
        Test("desktop ignores pending and new VR gestures",()=> {
            var p=VrPlayer();var v=new VrToggleControls();Clicks(v,p,false,false);Clicks(v,p,false,true);
            SessionHelpers.singleton.vrMode=false;Check(Clicks(v,p,false,false)==(false,false),"pending discarded");
            Check(Clicks(v,p,true,true)==(false,false),"chord ignored on desktop");
        });
        Test("left stick can still supply mapped sprint during noclip",()=> {
            var p=VrPlayer();Bind(p,"Fast",9);var(core,lease)=Start(p);var v=new VrToggleControls();Clicks(v,p,false,false);
            p.devourInput.Axes["MyMoveZ"]=1;Check(Clicks(v,p,true,false)==(false,false),"run input without freeze");
            lease.UpdateInput();Near(p.transform.position.z,.16f,"mapped left-click sprint speed");
            Check(core.Active&&v.FreezePresses==0,"flight retained");
        });
        Test("freeze chord leaves an already active noclip session active",()=> {
            var p=VrPlayer();var(core,lease)=Start(p);var v=new VrToggleControls();Clicks(v,p,false,false);
            Clicks(v,p,false,true);var chord=Clicks(v,p,true,true);
            if(chord.noclip)core.TryExit();Check(chord.freeze,"freeze requested");
            var release=Clicks(v,p,false,false);if(release.noclip)core.TryExit();
            Check(core.Active&&v.NoclipPresses==0,"no noclip exit from chord or release");
            Clicks(v,p,false,true);var single=Clicks(v,p,false,false);if(single.noclip)core.TryExit();
            Check(!core.Active&&p.locomotion.UseGravity,"single right release still exits normally");
        });
    }
}
