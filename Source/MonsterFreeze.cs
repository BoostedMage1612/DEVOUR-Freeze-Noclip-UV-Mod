using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using Il2Cpp;
using Il2CppBehaviorDesigner.Runtime;
using Il2CppPhoton.Bolt;
using Il2CppOpsive.UltimateCharacterController.Character;
using UnityEngine;
using UObj = UnityEngine.Object;
using AIBehavior = Il2CppBehaviorDesigner.Runtime.Behavior;
using Battery = Il2CppOpsive.UltimateCharacterController.Traits.Attribute;

[assembly: MelonInfo(typeof(DevourMonsterFreeze.MonsterFreeze), "DEVOUR Monster Freeze", "0.4.0", "Jimmy / ChatGPT")]
[assembly: MelonGame("Straight Back Games", "DEVOUR")]

namespace DevourMonsterFreeze;

public sealed class MonsterFreeze : MelonMod
{
    public const string Version = "0.4.0";
    public const string ExpectedNative = "B57399A48DFFFA243A837589516260C7842314EC3B373755027C39845A8F7292";
    public const string ExpectedMetadata = "ED8FF8264BECC4D3B2D33F20F0840C3435ACFC9C12D0BECBD4D175F3A57ED45E";
    internal static MonsterFreeze? Instance;
    internal readonly FreezeCore State = new();
    internal readonly NoclipCore Flight = new();
    private readonly VrToggleControls vrControls = new();
    private UnlimitedUv? uv;
    private HarmonyLib.Harmony? uvHarmony;
    private bool uvReady;
    private string uvError = "";
    private HarmonyLib.Harmony? flightHarmony;
    private bool flightReady;
    private string flightError = "", lastFlightFault = "";
    private object? lastFlightAttempt, lastFlightSession;
    private double flightTraceAt;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly List<string> hooks = new();
    private string root = "", trace = "", statusFile = "", scene = "starting";
    private string message = "Initializing...", startupError = "";
    private string lastFault = "", role = "menu";
    private bool ready, host, guiFailed, shuttingDown;
    private double scanAt, snapshotAt, restoreAt;
    private int visibleBosses;
    private long blockedTicks, blockedAttacks, newlyFrozen;
    private Process? exporter;
    private GUIStyle? titleStyle, statusStyle;
    internal Action<string> LogAction => Log;

    public override void OnInitializeMelon()
    {
        Instance = this;
        root = MelonEnvironment.GameRootDirectory;
        var work = Path.Combine(root, "DEVOURFreeze");
        Directory.CreateDirectory(work);
        trace = Path.Combine(work, "freeze.log");
        statusFile = Path.Combine(work, "status.json");
        try
        {
            if (File.Exists(trace) && new FileInfo(trace).Length > 8 * 1024 * 1024)
                File.Move(trace, trace + ".previous", true);
            Log("START v" + Version + "; verified build target 25725702 / DEVOUR 6.0.34");
            if (Sha256(Path.Combine(root, "GameAssembly.dll")) != ExpectedNative ||
                Sha256(Path.Combine(root, "DEVOUR_Data", "il2cpp_data", "Metadata", "global-metadata.dat")) != ExpectedMetadata)
                throw new InvalidOperationException("Game build changed. Export a report; this test stays disabled.");
            InstallHooks();
            ready = true;
            InstallNoclipHooks();
            InstallUvHooks();
            message = "Ready. Start solo or host a game.";
            Log("READY; both stick clicks / F6 freeze; right-only release / F7 noclip; automatic unlimited UV; F9 report");
        }
        catch (Exception e)
        {
            startupError = e.GetBaseException().Message;
            message = "Setup failed - F9 exports diagnostics";
            try { HarmonyInstance.UnpatchSelf(); } catch { }
            Log("INITIALIZATION ERROR: " + e);
        }
        Snapshot();
    }

    private static string Sha256(string path)
    {
        using var f = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(f));
    }

    private void InstallHooks()
    {
        Patch(typeof(BehaviorManager), "Tick", new[] { typeof(BehaviorManager.BehaviorTree) }, nameof(TreePrefix));
        Patch(typeof(BehaviorManager), "FixedTick", new[] { typeof(BehaviorManager.BehaviorTree) }, nameof(TreePrefix));
        Patch(typeof(Knockout), "AbilityWillStart", Type.EmptyTypes, nameof(KnockoutPrefix));
        Patch(typeof(SurvivalAzazelBehaviour), "OnPickedUpPlayer", new[] { typeof(GameObject), typeof(GameObject), typeof(bool) }, nameof(BossAttackPrefix));
        Patch(typeof(SurvivalAzazelBehaviour), "OnKnockout", new[] { typeof(GameObject), typeof(GameObject) }, nameof(BossAttackPrefix));
    }

    private void InstallNoclipHooks()
    {
        // New movement hooks have their own owner: an error cannot unpatch freeze.
        flightHarmony = new HarmonyLib.Harmony("Jimmy.DevourMonsterFreeze.Noclip");
        try
        {
            foreach (var name in new[] { "UpdatePosition", "ApplyPosition" })
            {
                var method = AccessTools.DeclaredMethod(typeof(UltimateCharacterLocomotion), name, Type.EmptyTypes)
                    ?? throw new MissingMethodException("UltimateCharacterLocomotion", name);
                flightHarmony.Patch(method, prefix: new HarmonyMethod(typeof(MonsterFreeze),
                    name == "ApplyPosition" ? nameof(FlightApplyPrefix) : nameof(FlightPositionPrefix)));
                Log("NOCLIP HOOK OK: " + name);
            }
            flightReady = true;
        }
        catch (Exception e)
        {
            flightError = e.GetBaseException().Message;
            flightHarmony.UnpatchSelf();
            Log("NOCLIP SETUP ERROR (freeze still available): " + e);
        }
    }

    private void InstallUvHooks()
    {
        uv = new UnlimitedUv(Log);
        uvHarmony = new HarmonyLib.Harmony("Jimmy.DevourMonsterFreeze.UnlimitedUv");
        try {
            foreach (var (type, name, args, prefix) in new[] {
                (typeof(Battery), "set_Value", new[] { typeof(float) }, nameof(UvValuePrefix)),
                (typeof(Battery), "UpdateValue", Type.EmptyTypes, nameof(UvUpdatePrefix)),
                (typeof(Battery), "ResetValue", Type.EmptyTypes, nameof(UvResetPrefix))
            }) {
                var method = AccessTools.DeclaredMethod(type, name, args)
                    ?? throw new MissingMethodException(type.FullName, name);
                uvHarmony.Patch(method, prefix: new HarmonyMethod(typeof(MonsterFreeze), prefix));
                Log("UV HOOK OK: " + name);
            }
            var attached = AccessTools.DeclaredMethod(typeof(NolanBehaviour), "Attached", Type.EmptyTypes)
                ?? throw new MissingMethodException("NolanBehaviour", "Attached");
            uvHarmony.Patch(attached, postfix: new HarmonyMethod(typeof(MonsterFreeze), nameof(UvAttachedPostfix)));
            Log("UV HOOK OK: NolanBehaviour.Attached");
            uvReady = true;
        } catch (Exception e) {
            uvError = e.GetBaseException().Message;
            uvHarmony.UnpatchSelf();
            Log("UV SETUP ERROR (freeze/flight still available): " + e);
        }
    }

    // Attached and scheduled ticks can precede OnUpdate's role snapshot.
    private bool UvHost => ready && uvReady && !shuttingDown && BoltNetwork.IsRunning &&
        (BoltNetwork.IsServer || BoltNetwork.IsSinglePlayer);
    private static void UvValuePrefix(Battery __instance, ref float __0)
    {
        var mod = Instance;
        if (mod != null) mod.uv?.BeforeSetValue(mod.UvHost, __instance, ref __0);
    }
    private static bool UvUpdatePrefix(Battery __instance)
    {
        var mod = Instance;
        return mod?.uv?.BeforeUpdateValue(mod.UvHost, __instance) ?? true;
    }
    private static bool UvResetPrefix(Battery __instance)
    {
        var mod = Instance;
        return mod?.uv?.BeforeResetValue(mod.UvHost, __instance) ?? true;
    }
    private static void UvAttachedPostfix()
    {
        var mod = Instance;
        if (mod != null) mod.uv?.Maintain(mod.UvHost);
    }

    private static bool FlightPositionPrefix(UltimateCharacterLocomotion __instance) => FlightPrefix(__instance, false);
    private static bool FlightApplyPrefix(UltimateCharacterLocomotion __instance) => FlightPrefix(__instance, true);
    private static bool FlightPrefix(UltimateCharacterLocomotion motor, bool apply)
    {
        var mod = Instance;
        if (mod == null || !mod.Flight.Active || mod.Flight.Lease is not NoclipLease lease ||
            !motor || motor.GetInstanceID() != lease.MotorId) return true;
        try
        {
            mod.Flight.Maintain(mod.host);
            if (!mod.Flight.Active) {
                mod.RememberFlight(lease, "released by motor hook: " + mod.Flight.Fault);
                return true;
            }
            lease.Hold();
            if (apply) lease.ApplyPose();
            lease.MotorBlocks++;
            return false;
        }
        catch (Exception e) { mod.Flight.Stop(); mod.Log("NOCLIP HOOK ERROR: " + e); return true; }
    }

    private void ToggleFreeze(string source)
    {
        if (State.Requested) Release(source + " - freeze OFF");
        else if (!ready) message = "Setup failed - export diagnostics";
        else if (!host) message = "Freeze requires your solo/host session";
        else if (State.Arm(true)) { message = "Freeze ON"; scanAt = 0; Log(source + " - FREEZE ARMED"); }
    }

    private void ToggleNoclip(string source)
    {
        if (Flight.Active)
        {
            var previous = Flight.Lease as NoclipLease;
            message = Flight.TryExit() ? "Noclip OFF - normal movement restored" : Flight.Fault;
            if (previous != null) RememberFlight(previous, message);
            Log(source + " - " + message);
        }
        else if (!ready || !flightReady) message = "Noclip setup failed - F9 for report";
        else if (!host) message = "Noclip requires your solo/host session";
        else if (Flight.Restoring) message = "Noclip restore pending - F9 for report";
        else
        {
            var player = InGameHelpers.singleton?.GetLocalNolanBehaviour();
            var eligibility = NoclipLease.InspectPlayer(player);
            var ok = Flight.Start(host, () => {
                if (!player) throw new InvalidOperationException("Start a round and wait for your player to appear.");
                return new NoclipLease(player!, Log);
            });
            message = ok ? "Noclip ON - Jump up / Crouch down" :
                string.IsNullOrEmpty(Flight.Fault) ? "Noclip could not activate; F9 records the player checks." : Flight.Fault;
            lastFlightAttempt = new { Utc = DateTimeOffset.UtcNow, Source = source, Accepted = ok, Reason = message, Player = eligibility };
            Log("NOCLIP ATTEMPT " + JsonSerializer.Serialize(lastFlightAttempt));
            if (ok) { lastFlightFault = ""; flightTraceAt = 0; }
            Log(source + " - " + message);
        }
        Snapshot();
    }

    private void UpdateNoclip()
    {
        try
        {
            var wasActive = Flight.Active;
            var previous = Flight.Lease as NoclipLease;
            Flight.Maintain(host);
            if (Flight.Active && Flight.Lease is NoclipLease lease) {
                lease.UpdateInput();
                if (clock.Elapsed.TotalSeconds >= flightTraceAt) {
                    flightTraceAt = clock.Elapsed.TotalSeconds + 5;
                    RememberFlight(lease, "active");
                }
            }
            if (wasActive && !Flight.Active) {
                message = "Noclip released: " + Flight.Fault;
                if (previous != null) RememberFlight(previous, message);
                Log(message);
            }
            if (Flight.Fault.Length > 0 && Flight.Fault != lastFlightFault)
            { lastFlightFault = Flight.Fault; message = Flight.Fault; Log("NOCLIP: " + Flight.Fault); }
        }
        catch (Exception e) { Flight.Stop(); message = "Noclip error - F9 for report"; Log("NOCLIP ERROR: " + e); }
    }

    private void RememberFlight(NoclipLease lease, string reason)
    {
        try {
            lastFlightSession = new { Utc = DateTimeOffset.UtcNow, Reason = reason, State = lease.Describe() };
            Log("NOCLIP SESSION " + JsonSerializer.Serialize(lastFlightSession));
        } catch (Exception e) { Log("NOCLIP SESSION snapshot failed: " + e.Message); }
    }

    private void Patch(Type type, string name, Type[] args, string prefix)
    {
        var method = AccessTools.Method(type, name, args) ?? throw new MissingMethodException(type.FullName, name);
        HarmonyInstance.Patch(method, prefix: new HarmonyMethod(typeof(MonsterFreeze), prefix));
        hooks.Add(type.FullName + "." + name);
        Log("HOOK OK: " + hooks[^1]);
    }

    private static bool TreePrefix(BehaviorManager.BehaviorTree __0)
    {
        var mod = Instance;
        if (mod == null || !mod.ready || !mod.State.Requested || !mod.host) return true;
        try
        {
            var behavior = __0?.behavior;
            if (behavior && mod.ShouldFreeze(behavior!.gameObject))
            { mod.blockedTicks++; return false; }
        }
        catch (Exception e) { mod.Fail(e); }
        return true;
    }

    private static bool KnockoutPrefix(Knockout __instance, ref bool __result)
    {
        var mod = Instance;
        if (mod == null || !mod.ready || !mod.State.Requested || !mod.host) return true;
        try
        {
            if (__instance != null && mod.ShouldFreeze(__instance.m_GameObject))
            { mod.blockedAttacks++; __result = false; return false; }
        }
        catch (Exception e) { mod.Fail(e); }
        return true;
    }

    private static bool BossAttackPrefix(SurvivalAzazelBehaviour __instance)
    {
        var mod = Instance;
        if (mod == null || !mod.ready || !mod.State.Requested || !mod.host) return true;
        try
        {
            if (__instance && mod.Acquire(__instance)) { mod.blockedAttacks++; return false; }
        }
        catch (Exception e) { mod.Fail(e); }
        return true;
    }

    private bool ShouldFreeze(GameObject obj)
    {
        if (!obj) return false;
        var boss = obj.GetComponentInParent<SurvivalAzazelBehaviour>();
        return boss && Acquire(boss);
    }

    private bool Acquire(SurvivalAzazelBehaviour boss)
    {
        if (!State.Requested || !host || !boss || !boss.gameObject.activeInHierarchy) return false;
        var entity = boss.entity;
        if (!entity || !entity.IsAttached || !entity.IsOwner) return false;
        var id = boss.GetInstanceID();
        if (State.Contains(id)) return true;
        var ok = State.Track(id, () => new MonsterLease(boss, Log));
        if (ok) { newlyFrozen++; Log("FROZEN: " + boss.name + " id=" + id); }
        return ok;
    }

    public override void OnUpdate()
    {
        if (shuttingDown) return;
        try
        {
            host = ready && BoltNetwork.IsRunning && (BoltNetwork.IsServer || BoltNetwork.IsSinglePlayer);
            role = !BoltNetwork.IsRunning ? "menu" : host ? "host / solo" : "joined client";
            if (Input.GetKeyDown(KeyCode.F9)) Export();
            var clicks = vrControls.Poll(host ? InGameHelpers.singleton?.GetLocalNolanBehaviour() : null, host);
            // Sample both sources each frame, then toggle once if either fired.
            var freezeKey = Input.GetKeyDown(KeyCode.F6);
            var noclipKey = Input.GetKeyDown(KeyCode.F7);
            if (clicks.freeze || freezeKey) ToggleFreeze(clicks.freeze ? "VR BOTH STICKS" : "F6");
            if (clicks.noclip || noclipKey) ToggleNoclip(clicks.noclip ? "VR RIGHT STICK" : "F7");
            UpdateNoclip();
            uv?.Maintain(UvHost);
            if (State.Requested && !host) Release("Host session ended - released");
            if (ready && clock.Elapsed.TotalSeconds >= scanAt)
            {
                scanAt = clock.Elapsed.TotalSeconds + 0.25;
                var bosses = UObj.FindObjectsOfType<SurvivalAzazelBehaviour>();
                visibleBosses = 0;
                foreach (var boss in bosses)
                {
                    if (!boss || !boss.entity || !boss.entity.IsAttached) continue;
                    visibleBosses++;
                    // Let a win/fail ending proceed normally.
                    if (State.Requested && boss.m_Survival &&
                        (boss.m_Survival.IsEndingPlaying() || boss.m_Survival.StartingToPlayFailEnding()))
                    { Release("Ending started - released"); break; }
                    if (State.Requested) Acquire(boss);
                }
            }
            if (State.Requested) State.Maintain(host);
            if (clock.Elapsed.TotalSeconds >= restoreAt)
            { restoreAt = clock.Elapsed.TotalSeconds + 1; State.RetryRestore(); Flight.RetryRestore(); }
            if (!string.IsNullOrEmpty(State.Fault) && State.Fault != lastFault)
            { lastFault = State.Fault; message = "Freeze error - F9 exports details"; Log("FREEZE ERROR: " + State.Fault); }
            if (exporter != null && exporter.HasExited)
            {
                message = exporter.ExitCode == 0 ? "Report ready - see the opened folder" : "Report export failed - use EXPORT_REPORT.cmd";
                exporter.Dispose(); exporter = null;
            }
            if (clock.Elapsed.TotalSeconds >= snapshotAt)
            { snapshotAt = clock.Elapsed.TotalSeconds + 3; Snapshot(); }
        }
        catch (Exception e) { Fail(e); }
    }

    public override void OnLateUpdate()
    {
        if (Flight.Active)
        {
            try { Flight.Maintain(host); if (Flight.Active && Flight.Lease is NoclipLease lease) lease.ApplyPose(); }
            catch (Exception e) { Flight.Stop(); Log("NOCLIP LATE UPDATE ERROR: " + e); }
        }
        if (State.Requested)
        {
            try { State.Maintain(host); }
            catch (Exception e) { Fail(e); }
        }
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        scene = sceneName;
        vrControls.Reset();
        uv?.Clear();
        if (Flight.Lease is NoclipLease lease) RememberFlight(lease, "scene changed to " + sceneName);
        Flight.Stop();
        // Release on scene transitions; no stale native object references cross maps.
        Release("Scene loaded: " + sceneName + " - freeze reset to OFF");
        visibleBosses = 0; scanAt = 0;
    }

    private void Release(string reason)
    {
        var hadState = State.Requested || State.Count != 0 || State.PendingRestoreCount != 0;
        State.ReleaseAll();
        message = State.PendingRestoreCount == 0 ? "OFF - normal monster behavior" : "Restoring - F9 for diagnostics";
        if (hadState || reason.StartsWith("Scene")) Log(reason);
        Snapshot();
    }

    private void Fail(Exception e)
    {
        var detail = e.GetBaseException().Message;
        Flight.Stop();
        State.ReleaseAll();
        message = "Error - freeze released; F9 exports details";
        if (lastFault != detail) { lastFault = detail; Log("RUNTIME ERROR: " + e); }
    }

    private void Log(string text)
    {
        LoggerInstance.Msg(text);
        try { if (trace.Length > 0) File.AppendAllText(trace, DateTimeOffset.UtcNow.ToString("o") + " " + text + Environment.NewLine); } catch { }
    }

    private void Snapshot()
    {
        if (statusFile.Length == 0) return;
        try
        {
            var data = new {
                Version, TargetSteamBuild = "25725702", CreatedUtc = DateTimeOffset.UtcNow,
                Ready = ready, StartupError = startupError, Role = role, Scene = scene,
                FreezeRequested = State.Requested, FrozenCount = State.Count,
                PendingRestore = State.PendingRestoreCount, State.Fault, Message = message,
                NoclipReady = flightReady, NoclipSetupError = flightError,
                NoclipActive = Flight.Active, NoclipRestoring = Flight.Restoring, NoclipFault = Flight.Fault,
                Noclip = (Flight.Lease as NoclipLease)?.Describe(),
                LastNoclipAttempt = lastFlightAttempt, LastNoclipSession = lastFlightSession,
                VRControls = vrControls.Describe(),
                UnlimitedUvReady = uvReady, UnlimitedUvSetupError = uvError, UnlimitedUv = uv?.Describe(),
                VisibleBosses = visibleBosses, TotalAcquisitions = newlyFrozen,
                BlockedBehaviorTicks = blockedTicks, BlockedAttackCalls = blockedAttacks,
                Hooks = hooks.ToArray(),
                Targets = State.Leases.Cast<MonsterLease>().Select(t => t.Describe()).ToArray()
            };
            File.WriteAllText(statusFile + ".tmp", JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(statusFile + ".tmp", statusFile, true);
        }
        catch (Exception e) { if (lastFault != e.Message) LoggerInstance.Warning("Snapshot: " + e.Message); }
    }

    private void Export()
    {
        if (exporter != null && !exporter.HasExited) { message = "Report is already being prepared"; return; }
        Snapshot();
        var script = Path.Combine(root, "DEVOURFreeze", "Support", "Export_Report.ps1");
        if (!File.Exists(script)) { message = "Exporter missing - use package EXPORT_REPORT.cmd"; Log(message); return; }
        try
        {
            var start = new ProcessStartInfo {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                UseShellExecute = false, CreateNoWindow = true
            };
            foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-GamePath", root }) start.ArgumentList.Add(arg);
            exporter = Process.Start(start);
            message = "Preparing report...";
            Log("REPORT requested with F9");
        }
        catch (Exception e) { message = "Export failed - use EXPORT_REPORT.cmd"; Log("REPORT ERROR: " + e); }
    }

    public override void OnGUI()
    {
        if (guiFailed) return;
        try
        {
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            statusStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            var width = Math.Min(225f, Screen.width - 24f);
            var x = Math.Max(12f, Screen.width - width - 16f);
            GUI.Box(new Rect(x, 16, width, 94), "");
            GUI.Label(new Rect(x + 12, 24, width - 24, 22), "DEVOUR Mod v" + Version, titleStyle);
            var freeze = State.Requested || State.Count > 0 || State.PendingRestoreCount > 0 ? "ON" : "OFF";
            var noclip = Flight.Active || Flight.Restoring ? "ON" : "OFF";
            GUI.Label(new Rect(x + 12, 50, width - 24, 24), "Freeze: " + freeze, statusStyle);
            GUI.Label(new Rect(x + 12, 78, width - 24, 24), "No-clip: " + noclip, statusStyle);
        }
        catch (Exception e) { guiFailed = true; Log("OVERLAY ERROR: " + e); }
    }

    public override void OnApplicationQuit()
    {
        shuttingDown = true;
        uv?.Clear();
        if (Flight.Lease is NoclipLease lease) RememberFlight(lease, "game quit");
        Flight.Stop();
        State.ReleaseAll();
        Log("QUIT - freeze released");
        Snapshot();
    }
}
