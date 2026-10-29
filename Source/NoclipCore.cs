using System;

namespace DevourMonsterFreeze;

public interface INoclipLease
{
    bool IsAlive { get; }
    bool CanControl { get; }
    string ControlBlockReason { get; }
    void Enter();
    void Hold();
    bool TryPrepareExit();
    void Restore();
}

// Retain original movement settings until cleanup completes.
public sealed class NoclipCore
{
    public INoclipLease? Lease { get; private set; }
    public bool Active { get; private set; }
    public bool Restoring => Lease != null && !Active;
    public string Fault { get; private set; } = "";

    public bool Start(bool authority, Func<INoclipLease> capture)
    {
        if (!authority || Lease != null) return false;
        Fault = "";
        try
        {
            var candidate = capture();
            if (!candidate.IsAlive) { Fault = "Local player or movement motor is no longer available."; return false; }
            var reason = candidate.ControlBlockReason;
            if (reason.Length > 0) { Fault = reason; return false; }
            Lease = candidate; // Retain the snapshot before the first write.
            candidate.Enter();
            Active = true;
            return true;
        }
        catch (Exception e) { Fault = e.Message; Stop(); return false; }
    }

    public bool TryExit()
    {
        if (!Active) { RetryRestore(); return Lease == null; }
        try
        {
            if (Lease!.IsAlive && Lease.CanControl && !Lease.TryPrepareExit())
            { Fault = "Noclip exit could not complete. F9 exports details."; return false; }
            Stop();
            return Lease == null;
        }
        catch (Exception e) { Fault = "Exit check: " + e.Message; return false; }
    }

    public void Maintain(bool authority)
    {
        if (!Active) return;
        try
        {
            if (!authority) { Fault = "Solo/host session ended."; Stop(); return; }
            if (Lease == null || !Lease.IsAlive) { Fault = "Local player or movement motor was removed."; Stop(); return; }
            var reason = Lease.ControlBlockReason;
            if (reason.Length > 0) { Fault = reason; Stop(); return; }
            Lease.Hold();
        }
        catch (Exception e) { Fault = e.Message; Stop(); }
    }

    // Lifecycle cleanup never teleports a dying/replaced player or a new scene.
    public void Stop()
    {
        Active = false;
        RetryRestore();
    }

    public void RetryRestore()
    {
        if (Active || Lease == null) return;
        try
        {
            if (Lease.IsAlive) Lease.Restore();
            Lease = null;
        }
        catch (Exception e) { Fault = "Noclip restore needs retry: " + e.Message; }
    }
}
