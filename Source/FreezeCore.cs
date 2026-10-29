using System;
using System.Collections.Generic;
using System.Linq;

namespace DevourMonsterFreeze;

// Game-independent state transitions; the same implementation is used by tests.
public interface IFreezeLease
{
    long Key { get; }
    bool IsAlive { get; }
    bool CanHold { get; }
    void Hold();
    void Restore();
}

public sealed class FreezeCore
{
    private readonly Dictionary<long, IFreezeLease> held = new();
    private readonly Dictionary<long, IFreezeLease> pendingRestore = new();
    public bool Requested { get; private set; }
    public string Fault { get; private set; } = "";
    public int Count => held.Count;
    public int PendingRestoreCount => pendingRestore.Count;
    public bool Contains(long key) => held.ContainsKey(key);
    public IEnumerable<IFreezeLease> Leases => held.Values.ToArray();

    public bool Arm(bool hasAuthority)
    {
        if (!hasAuthority || pendingRestore.Count != 0) return false;
        Fault = "";
        Requested = true;
        return true;
    }

    public bool Track(long key, Func<IFreezeLease> capture)
    {
        if (!Requested) return false;
        if (held.ContainsKey(key)) return true;
        IFreezeLease? lease = null;
        try
        {
            lease = capture(); // Capture every original value before the first write.
            if (!lease.IsAlive || !lease.CanHold) return false;
            lease.Hold();
            held.Add(key, lease);
            return true;
        }
        catch (Exception e)
        {
            if (lease != null) pendingRestore[key] = lease;
            Fault = e.Message;
            ReleaseAll();
            return false;
        }
    }

    public void Maintain(bool hasAuthority)
    {
        if (!hasAuthority) { ReleaseAll(); return; }
        if (!Requested) return;
        foreach (var pair in held.ToArray())
        {
            try
            {
                if (!pair.Value.IsAlive) { held.Remove(pair.Key); continue; }
                if (!pair.Value.CanHold)
                {
                    pendingRestore[pair.Key] = pair.Value;
                    held.Remove(pair.Key);
                    RetryRestore();
                    continue;
                }
                pair.Value.Hold();
            }
            catch (Exception e)
            {
                Fault = e.Message;
                ReleaseAll();
                return;
            }
        }
    }

    public void ReleaseAll()
    {
        Requested = false; // Prefixes allow normal execution before restoration.
        foreach (var pair in held) pendingRestore[pair.Key] = pair.Value;
        held.Clear();
        RetryRestore();
    }

    public void RetryRestore()
    {
        foreach (var pair in pendingRestore.ToArray())
        {
            try
            {
                if (pair.Value.IsAlive) pair.Value.Restore();
                pendingRestore.Remove(pair.Key);
            }
            catch (Exception e) { Fault = "Restore needs retry: " + e.Message; }
        }
    }
}
