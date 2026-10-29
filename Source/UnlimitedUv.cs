using System;
using Il2Cpp;
using Il2CppHorror;
using Battery = Il2CppOpsive.UltimateCharacterController.Traits.Attribute;

namespace DevourMonsterFreeze;

// Only the battery field of the exact locally selected, controlled player is
// eligible. A host can own remote entities too; ownership alone is not identity.
internal sealed class UnlimitedUv
{
    private readonly Action<string> log;
    private NolanBehaviour? player;
    private Battery? battery;
    private bool writing, releasing, drainSuspended;
    private string lastLoggedFault = "";
    private int playerId;
    private string difficulty = "", batteryPointer = "";
    private float charge, capacity;
    private long fills, blockedTicks, rewrittenValues, blockedResets;
    public bool Active { get; private set; }
    public string Fault { get; private set; } = "";

    public UnlimitedUv(Action<string> log) { this.log = log; }

    private static bool Eligible(bool host, out NolanBehaviour? p, out Battery? b)
    {
        p = null; b = null;
        if (!host) return false;
        p = InGameHelpers.singleton?.GetLocalNolanBehaviour();
        if (!p || !p!.gameObject.activeInHierarchy) return false;
        var entity = p.entity;
        if (!entity || !entity.IsAttached || !(entity.IsOwner || entity.HasControl)) return false;
        b = p.batteryAttribute;
        return b != null && b.Pointer != IntPtr.Zero && b.Name == "Battery";
    }

    private bool Select(bool host, Battery? candidate, out NolanBehaviour? p, out Battery? b)
    {
        p = null; b = null;
        if (releasing || !Eligible(host, out p, out b)) return false;
        if (candidate != null && candidate.Pointer != b!.Pointer) return false;
        if (battery != null && (battery.Pointer != b!.Pointer || playerId != p!.GetInstanceID())) Clear();
        player = p; battery = b; playerId = p!.GetInstanceID();
        batteryPointer = b!.Pointer.ToString("X"); difficulty = p.m_gameMode.ToString();
        return true;
    }

    private float Full(NolanBehaviour p, Battery b)
    {
        // Native UseItem.AbilityStarted enables this state and charges 60 on
        // Nightmare. Do not remove Nightmare from the character or alter its
        // difficulty. Other modes retain their current base/bonus capacity.
        if (p.m_gameMode == DevourGameMode.Nightmare) {
            if (b.MaxValue != 60f) b.SetState("NightmareModeBattery", true);
            if (b.MaxValue != 60f) throw new InvalidOperationException("Nightmare battery charge state is unavailable.");
            p.isUsingBatteryBonus = true;
        }
        var full = b.MaxValue;
        if (!float.IsFinite(full) || full <= 0 || full <= b.MinValue)
            throw new InvalidOperationException("UV battery capacity is invalid.");
        return full;
    }

    private void Fill(NolanBehaviour p, Battery b, bool forceEvent = false)
    {
        if (writing) return;
        writing = true;
        try {
            var full = Full(p, b);
            if (forceEvent || b.Value != full) { b.Value = full; fills++; }
            capacity = full; charge = b.Value; Active = true; Fault = "";
        } finally { writing = false; }
    }

    public void Maintain(bool host)
    {
        try {
            if (!Select(host, null, out var p, out var b)) { Clear(); return; }
            Fill(p!, b!);
        } catch (Exception e) { Failed(e); }
    }

    // The native setter still emits the battery/UI event and schedules updates.
    public void BeforeSetValue(bool host, Battery instance, ref float value)
    {
        if (writing) return;
        try {
            if (!Select(host, instance, out var p, out var b)) return;
            writing = true;
            try {
                var full = Full(p!, b!);
                if (value != full) rewrittenValues++;
                value = full; capacity = full; charge = full; Active = true; Fault = "";
            } finally { writing = false; }
        } catch (Exception e) { Failed(e); }
    }

    // UpdateValue writes m_Value directly and can fire the empty-battery event
    // before a later refill. Suppress only this exact battery's tick. Clear the
    // scheduler handle after cancellation; do not leave a pooled event cached.
    // The setter can schedule UpdateValue synchronously when start delay is 0:
    // that nested tick must also be suppressed, even while Fill is in progress.
    public bool BeforeUpdateValue(bool host, Battery instance)
    {
        try {
            if (!Select(host, instance, out var p, out var b)) return true;
            instance.CancelAutoUpdate(); instance.m_AutoUpdateEvent = null;
            drainSuspended = true; blockedTicks++;
            Fill(p!, b!);
            return false;
        } catch (Exception e) { Failed(e); return true; }
    }

    // ResetValue also bypasses the setter. Replace its empty/start-value write
    // with a full setter event so the native gauge is updated without a dip.
    public bool BeforeResetValue(bool host, Battery instance)
    {
        try {
            if (!Select(host, instance, out var p, out var b)) return true;
            instance.CancelAutoUpdate(); instance.m_AutoUpdateEvent = null;
            drainSuspended = true; blockedResets++;
            Fill(p!, b!, true);
            return false;
        } catch (Exception e) { Failed(e); return true; }
    }

    // Rates, start values and character difficulty retain their native settings.
    // If this player stops being eligible, restart native scheduling once.
    public void Clear()
    {
        var oldPlayer = player; var oldBattery = battery; var resume = drainSuspended;
        player = null; battery = null; Active = false; drainSuspended = false;
        if (!resume || oldBattery == null || !oldPlayer || !oldPlayer!.gameObject.activeInHierarchy) return;
        releasing = true;
        try { oldBattery.StateChange(); }
        catch (Exception e) { Failed(e); }
        finally { releasing = false; }
    }

    private void Failed(Exception e)
    {
        Active = false; Fault = e.GetBaseException().Message;
        if (Fault != lastLoggedFault) { lastLoggedFault = Fault; log("UNLIMITED UV ERROR (freeze/flight independent): " + e); }
    }

    public object Describe() => new {
        Active, Fault, PlayerId = playerId, Difficulty = difficulty,
        BatteryPointer = batteryPointer, LastCharge = charge, LastCapacity = capacity,
        Fills = fills, BlockedTicks = blockedTicks, RewrittenValues = rewrittenValues,
        BlockedResets = blockedResets, DrainSuspended = drainSuspended
    };
}
