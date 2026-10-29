using System;
using System.Linq;
using DevourMonsterFreeze;
using Il2Cpp;
using Il2CppHorror;
using Battery = Il2CppOpsive.UltimateCharacterController.Traits.Attribute;

static partial class Program {
    static (NolanBehaviour p, Battery b, UnlimitedUv uv) UvPlayer(DevourGameMode mode = DevourGameMode.Normal) {
        var p = Player(); p.m_gameMode = mode;
        var b = new Battery(); p.batteryAttribute = b;
        b.MaxValue = mode == DevourGameMode.Hard ? 60 : 100;
        b.m_Value = 0;
        var uv = new UnlimitedUv(logs.Add); b.Mod = uv;
        return (p,b,uv);
    }
    static void FullGauge(Battery b) => Check(b.Data.Gauge.All(v => v == b.MaxValue), "all UI events must see full charge");
    static void UvTests() {
        foreach (var mode in Enum.GetValues<DevourGameMode>()) {
            Test("UV starts full and never drains in " + mode, () => {
                var (p,b,uv) = UvPlayer(mode); uv.Maintain(true);
                var full = mode is DevourGameMode.Hard or DevourGameMode.Nightmare ? 60 : 100;
                Near(b.Value,full,"spawn charge");
                for (int i=0;i<200;i++) { b.UpdateValue(); b.Value -= 30; uv.Maintain(true); }
                Near(b.Value,full,"sustained charge"); FullGauge(b);
                Check(b.Data.EmptyEvents==0 && b.Data.NativeTicks==0 && uv.Active,"no empty tick or shutoff");
                Check(p.m_gameMode==mode,"difficulty unchanged");
                Near(b.Data.Amount,1,"drain amount unchanged"); Near(b.Data.Start,0,"start value unchanged");
                Check(b.Data.UpdateType==Battery.AutoUpdateValue.Decrease,"drain type unchanged");
            });
        }
        Test("UV Nightmare activates native battery state even with nonzero default max", () => {
            var (p,b,uv)=UvPlayer(DevourGameMode.Nightmare); uv.Maintain(true); uv.Maintain(true);
            Check(b.Data.NightmareCharges==1 && p.isUsingBatteryBonus,"native battery state and bonus flag");
            Near(b.Value,60,"native Nightmare full");
        });
        Test("UV Nightmare supports zero-capacity default and later state reset", () => {
            var (p,b,uv)=UvPlayer(DevourGameMode.Nightmare); b.MaxValue=0; uv.Maintain(true);
            b.MaxValue=0; b.m_Value=0; p.isUsingBatteryBonus=false; uv.Maintain(true);
            Check(b.Data.NightmareCharges==2 && p.isUsingBatteryBonus,"reapply charged battery state");
            Near(b.Value,60,"full after reset");
        });
        Test("UV Nightmare return-from-purgatory zero setter never reaches UI", () => {
            var (_,b,uv)=UvPlayer(DevourGameMode.Nightmare); uv.Maintain(true); b.Value=0;
            Near(b.Value,60,"zero write intercepted"); FullGauge(b);
        });
        foreach (var mode in new[]{DevourGameMode.Easy,DevourGameMode.Normal,DevourGameMode.Hard}) {
            Test("UV preserves active bonus capacity in " + mode, () => {
                var (_,b,uv)=UvPlayer(mode); uv.Maintain(true);
                b.MaxValue=mode==DevourGameMode.Hard?100:160; b.Value=1;
                Near(b.Value,b.MaxValue,"bonus full");
                Check(b.Data.NightmareCharges==0,"no Nightmare state on other modes");
            });
        }
        Test("UV zero-delay scheduling does not recurse or drain", () => {
            var (_,b,uv)=UvPlayer(DevourGameMode.Nightmare); b.Data.Delay=0;
            uv.Maintain(true); b.Value=0; b.ResetValue();
            Check(uv.Active && b.Data.EmptyEvents==0 && b.Data.NativeTicks==0,"nested ticks suppressed");
            Check(b.m_AutoUpdateEvent==null,"no cached canceled event"); FullGauge(b);
        });
        Test("UV reset replaces direct start-value write before its event", () => {
            var (_,b,uv)=UvPlayer(); uv.Maintain(true); b.ResetValue();
            Check(b.Data.NativeResets==0 && b.Data.SetEvents==2,"full setter emits reset UI event");
            Near(b.Value,100,"reset full"); FullGauge(b);
        });
        Test("UV depleted scheduled tick is blocked before empty destination callback", () => {
            var (_,b,uv)=UvPlayer(); b.m_Value=.5f; b.Data.Amount=500; b.UpdateValue();
            Check(b.Data.EmptyEvents==0 && b.Data.NativeTicks==0,"empty event never fires");
            Near(b.Value,100,"tick refills");
        });
        Test("UV native recharge tick keeps full gauge without altering recharge settings", () => {
            var (_,b,uv)=UvPlayer(); b.Data.UpdateType=Battery.AutoUpdateValue.Increase; b.Data.Amount=7;
            b.UpdateValue(); Near(b.Value,100,"recharge immediately full");
            Check(b.Data.UpdateType==Battery.AutoUpdateValue.Increase && b.Data.Amount==7,"settings preserved");
        });
        Test("UV allows only exact battery, preserving poison health and remote battery", () => {
            var (_,b,uv)=UvPlayer(); uv.Maintain(true);
            foreach (var name in new[]{"Poison","Health","Battery"}) {
                var other=new Battery { Mod=uv }; other.Data.Name=name; other.m_Value=50;
                other.UpdateValue(); Near(other.Value,49,"unrelated drain");
                other.Value=4; Near(other.Value,4,"unrelated setter");
                other.ResetValue(); Near(other.Value,0,"unrelated reset");
                Check(other.Data.NativeTicks==1 && other.Data.NativeResets==1,"native paths preserved");
            }
            Near(b.Value,100,"selected battery full");
        });
        Test("UV matches separate wrappers by native pointer", () => {
            var (_,b,uv)=UvPlayer(); var wrapper=new Battery(b);
            Check(!ReferenceEquals(b,wrapper),"different managed wrappers"); wrapper.Value=0;
            Near(b.Value,100,"same native identity protected");
        });
        foreach(var reason in new[]{"client","detached","nonowner","inactive","missing","wrongname"}) {
            Test("UV ignores ineligible " + reason, () => {
                var (p,b,uv)=UvPlayer(); var host=reason!="client"; b.Host=()=>host;
                if(reason=="detached")p.entity.IsAttached=false;
                if(reason=="nonowner")p.entity.IsOwner=false;
                if(reason=="inactive")p.gameObject.activeInHierarchy=false;
                if(reason=="missing")p.batteryAttribute=null!;
                if(reason=="wrongname")b.Data.Name="Poison";
                uv.Maintain(host); b.Value=50; b.UpdateValue();
                Near(b.Value,49,"ineligible battery native drain"); Check(!uv.Active,"inactive adapter");
            });
        }
        Test("UV accepts selected local controller without ownership", () => {
            var (p,b,uv)=UvPlayer(); p.entity.IsOwner=false; p.entity.HasControl=true;
            uv.Maintain(true); Near(b.Value,100,"controlled local full");
        });
        Test("UV late battery initialization is retried", () => {
            var (p,b,uv)=UvPlayer(); p.batteryAttribute=null!; uv.Maintain(true);
            p.batteryAttribute=b; uv.Maintain(true); Near(b.Value,100,"delayed spawn full");
        });
        Test("UV host loss resumes native updates once with zero-delay recursion guard", () => {
            var (_,b,uv)=UvPlayer(); bool host=true; b.Host=()=>host; uv.Maintain(true); b.UpdateValue();
            b.Data.Delay=0; host=false; uv.Maintain(false); uv.Maintain(false);
            Check(!uv.Active && b.Data.StateChanges==1 && b.Data.NativeTicks==1,"resumed once");
            Near(b.Value,99,"native drain restored"); Check(b.m_AutoUpdateEvent!=null,"native followup scheduled");
        });
        Test("UV respawn protects new player and releases previous local battery", () => {
            var (_,old,uv)=UvPlayer(); uv.Maintain(true); old.UpdateValue();
            var next=Player(); next.m_gameMode=DevourGameMode.Hard;
            next.batteryAttribute=new Battery { MaxValue=60, Mod=uv };
            uv.Maintain(true); Near(next.batteryAttribute.Value,60,"new player full");
            old.Value=25; old.UpdateValue(); Near(old.Value,24,"old local now native");
            Check(old.Data.StateChanges==1,"old scheduling restored");
        });
        Test("UV scene clear releases references and resumes scheduling only once", () => {
            var (_,b,uv)=UvPlayer(); uv.Maintain(true); b.UpdateValue(); uv.Clear(); uv.Clear();
            Check(!uv.Active && b.Data.StateChanges==1,"clear once");
        });
        Test("UV retains native knockout and input state", () => {
            var (p,b,uv)=UvPlayer(); p.knockout=true; p.inputDisabledFromKnockout=true;
            uv.Maintain(true); Near(b.Value,100,"resource full");
            Check(p.knockout && p.inputDisabledFromKnockout,"does not force ability availability");
        });
        Test("UV missing Nightmare state reports fault without guessing capacity", () => {
            var (p,b,uv)=UvPlayer(DevourGameMode.Nightmare); b.Data.NightmareStateAvailable=false;
            uv.Maintain(true); uv.Maintain(true);
            Check(!uv.Active && uv.Fault.Contains("Nightmare") && logs.Count==1,"isolated deduplicated error");
            Near(b.Value,0,"no unverified charge"); Check(p.m_gameMode==DevourGameMode.Nightmare,"mode retained");
            b.Data.NightmareStateAvailable=true; uv.Maintain(true);
            Check(uv.Active && uv.Fault=="","retry recovers");
        });
        Test("UV native access fault is contained and existing freeze flight remain active", () => {
            var (p,b,uv)=UvPlayer(); var (flight,_)=Start(p); var freeze=new FreezeCore(); freeze.Arm(true);
            b.Data.ThrowReads=true; uv.Maintain(true); b.UpdateValue();
            Check(!uv.Active && uv.Fault.Length>0 && flight.Active && freeze.Requested,"independent systems");
        });
        Test("UV steady maintenance does not spam full gauge events", () => {
            var (_,b,uv)=UvPlayer(); uv.Maintain(true); for(int i=0;i<300;i++)uv.Maintain(true);
            Check(b.Data.SetEvents==1,"only initial refill event");
        });
    }
}
