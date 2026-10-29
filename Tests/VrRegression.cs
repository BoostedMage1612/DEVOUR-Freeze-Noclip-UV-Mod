using System;
using System.Collections.Generic;
using DevourMonsterFreeze;
using Il2Cpp;
using Il2CppRewired;
using UnityEngine;

static partial class Program {
    static NolanBehaviour VrPlayer() {
        var p=Player();SessionHelpers.singleton.vrMode=true;p.m_SurvivalVR=new();
        p.locomotionHandler.HorizontalInputName="MyMoveX";p.locomotionHandler.ForwardInputName="MyMoveZ";
        p.locomotion.Abilities[0].InputNames=new[]{"Leap"};p.heightChangeAbility.InputNames=new[]{"Duck"};
        p.speedChangeAbility.InputNames=new[]{"Fast"};
        Bind(p,"Leap",0);Bind(p,"Duck",3);Bind(p,"Fast",4);return p;
    }
    static ActionElementMap Bind(NolanBehaviour p,string action,int button) {
        var c=p.m_SurvivalVR.m_CustomController;
        var binding=new ActionElementMap{elementIdentifierId=100+button};
        c.controller.indices[100+button]=button;
        c.player.controllers.maps.actions[action]=new List<ActionElementMap>{binding};return binding;
    }
    static void VrTests() {
        GestureTests();
        Test("VR analog mapped movement follows headset and holds altitude",()=> {
            var p=VrPlayer();p.m_SurvivalVR.m_VRCamera.transform.forward=new(1,1,0);
            var(c,l)=Start(p);p.devourInput.Axes["MyMoveZ"]=.5f;l.UpdateInput();
            Near(p.transform.position.x,.03f,"half speed headset forward");Near(p.transform.position.y,0,"no pitch flight");
            p.devourInput.Axes.Clear();l.UpdateInput();Near(p.transform.position.x,.03f,"hover");
        });
        Test("VR controller direction respects locomotion hand",()=> {
            var p=VrPlayer();p.vrMovementDirection=VRMovementDirection.Controller;
            p.m_SurvivalVR.m_LeftVRHand.transform.forward=new(1,1,0);p.m_SurvivalVR.m_RightVRHand.transform.forward=new(-1,1,0);
            var(c,l)=Start(p);p.devourInput.Axes["MyMoveZ"]=1;l.UpdateInput();Near(p.transform.position.x,.06f,"left controller");
            p.m_SurvivalVR.m_LeftHandedMode=true;l.UpdateInput();Near(p.transform.position.x,0,"right controller for left-handed mode");
        });
        Test("VR character direction and changing native rotation are honored",()=> {
            var p=VrPlayer();p.vrMovementDirection=VRMovementDirection.Character;var(c,l)=Start(p);
            p.devourInput.Axes["MyMoveZ"]=1;l.UpdateInput();p.transform.forward=new(1,0,0);l.UpdateInput();
            Near(p.transform.position.x,.06f,"turned forward");Near(p.transform.position.z,.06f,"original forward");
            Near(p.transform.forward.x,1,"rotation preserved");
        });
        Test("vertical headset fallback keeps horizontal motion",()=> {
            var p=VrPlayer();p.m_SurvivalVR.m_VRCamera.transform.forward=Vector3.up;
            var(c,l)=Start(p);p.devourInput.Axes["MyMoveZ"]=1;l.UpdateInput();
            Near(p.transform.position.z,.06f,"upright fallback");Near(p.transform.position.y,0,"horizontal only");
        });
        Test("VR focus follows headset while desktop mirror is unfocused",()=> {
            var p=VrPlayer();Application.isFocused=false;var(c,l)=Start(p);
            p.devourInput.Axes["MyMoveZ"]=1;l.UpdateInput();Near(p.transform.position.z,.06f,"VR still moves");
            p.m_SurvivalVR.m_VRUserPresence.present=false;l.UpdateInput();Near(p.transform.position.z,.06f,"removed headset holds");
        });
        Test("native mapped jump/crouch work without keyboard",()=> {
            var p=VrPlayer();var(c,l)=Start(p);p.devourInput.Buttons.Add("Leap");l.UpdateInput();l.UpdateInput();
            Near(p.transform.position.y,.12f,"mapped rise");p.devourInput.Buttons.Clear();p.devourInput.Buttons.Add("Duck");
            l.UpdateInput();Near(p.transform.position.y,.06f,"mapped descend");p.devourInput.Buttons.Clear();l.UpdateInput();Near(p.transform.position.y,.06f,"hover");
        });
        foreach(var profile in new[]{VRCustomControllerType.OCULUS_TOUCH,VRCustomControllerType.VALVE_INDEX,VRCustomControllerType.HP_REVERB_G2})
        Test("VR pulse buttons sustain jump and crouch for "+profile,()=> {
            var p=VrPlayer();var v=p.m_SurvivalVR.m_CustomController;v.type=profile;var(c,l)=Start(p);
            v.rightHandPrimaryButton.pressed=true;for(int i=0;i<10;i++)l.UpdateInput();Near(p.transform.position.y,.6f,"held rise past one frame");
            v.rightHandPrimaryButton.pressed=false;v.leftHandSecondaryButton.pressed=true;
            for(int i=0;i<10;i++)l.UpdateInput();Near(p.transform.position.y,0,"held descend past one frame");
            v.leftHandSecondaryButton.pressed=false;l.UpdateInput();Near(p.transform.position.y,0,"release hover");
        });
        Test("VR held mapping respects rebinds rather than hardcoded jump button",()=> {
            var p=VrPlayer();Bind(p,"Leap",2);var(c,l)=Start(p);var v=p.m_SurvivalVR.m_CustomController;
            v.rightHandPrimaryButton.pressed=true;l.UpdateInput();Near(p.transform.position.y,0,"old default ignored");
            v.leftHandPrimaryButton.pressed=true;l.UpdateInput();Near(p.transform.position.y,.06f,"rebound jump");
            Bind(p,"Leap",1);v.leftHandPrimaryButton.pressed=false;v.rightHandSecondaryButton.pressed=true;
            Time.unscaledTime=2;l.UpdateInput();Near(p.transform.position.y,.12f,"live rebind refreshed");
        });
        Test("disabled binding or controller map cannot supply held motion",()=> {
            var p=VrPlayer();var b=Bind(p,"Leap",0);var(c,l)=Start(p);p.m_SurvivalVR.m_CustomController.rightHandPrimaryButton.pressed=true;
            l.UpdateInput();b.enabled=false;l.UpdateInput();Near(p.transform.position.y,.06f,"disabled element");
            b.enabled=true;b.controllerMap.enabled=false;l.UpdateInput();Near(p.transform.position.y,.06f,"disabled map");
        });
        Test("VR left-handed secondary action and combined rise/descend",()=> {
            var p=VrPlayer();p.m_SurvivalVR.m_LeftHandedMode=true;var(c,l)=Start(p);var v=p.m_SurvivalVR.m_CustomController;
            v.leftHandSecondaryButton.pressed=true;l.UpdateInput();Near(p.transform.position.y,0,"wrong handed variant ignored");
            v.leftHandSecondaryButtonLeftHanded.pressed=true;l.UpdateInput();Near(p.transform.position.y,-.06f,"correct crouch hold");
            v.rightHandPrimaryButton.pressed=true;l.UpdateInput();Near(p.transform.position.y,-.06f,"up/down cancel");
        });
        Test("VR mapped sprint and analog diagonal speed remain bounded",()=> {
            var p=VrPlayer();var(c,l)=Start(p);p.devourInput.Axes["MyMoveX"]=1;p.devourInput.Axes["MyMoveZ"]=1;
            p.devourInput.Buttons.Add("Fast");l.UpdateInput();Near(p.transform.position.magnitude,.16f,"run diagonal cap");
        });
        Test("mapped keyboard axis is not doubled",()=> {
            var p=Player();var(c,l)=Start(p);p.devourInput.Axes["Horizontal"]=.5f;Input.Keys.Add(KeyCode.W);
            l.UpdateInput();Check(p.transform.position.x>0&&p.transform.position.z>0,"combined independent axes");
            Near(p.transform.position.magnitude,.06f,"vector cap");
        });
        Test("invalid or tiny axis values cannot drift or corrupt flight",()=> {
            var p=VrPlayer();var(c,l)=Start(p);p.devourInput.Axes["MyMoveX"]=float.NaN;p.devourInput.Axes["MyMoveZ"]=.0001f;
            l.UpdateInput();Near(p.transform.position.magnitude,0,"invalid rejected");
        });
        Test("missing VR jump binding gives reason without changing motor",()=> {
            var p=VrPlayer();p.locomotion.Abilities=Array.Empty<Il2CppOpsive.UltimateCharacterController.Character.Abilities.Ability>();
            var c=new NoclipCore();Check(!c.Start(true,()=>new NoclipLease(p,logs.Add))&&c.Fault.Contains("bindings"),"binding reason");
            Check(p.locomotion.UseGravity,"rollback before mutation");
        });
        Test("VR flight still honors menus and input disable",()=> {
            var p=VrPlayer();var(c,l)=Start(p);p.m_SurvivalVR.m_CustomController.rightHandPrimaryButton.pressed=true;
            p.gameUI.menu=true;l.UpdateInput();Near(p.transform.position.y,0,"menu");p.gameUI.menu=false;p.locomotionHandler.m_InputEnabled=false;
            l.UpdateInput();Near(p.transform.position.y,0,"gameplay disabled");
        });
    }
}
