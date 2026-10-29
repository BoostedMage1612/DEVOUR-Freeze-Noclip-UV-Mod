// Test doubles only. Event and scheduling order follows the verified native
// Attribute setter / UpdateValue / ResetValue paths; no Unity runtime is used.
using System;
using System.Collections.Generic;
using DevourMonsterFreeze;

namespace Il2CppHorror {
    public enum DevourGameMode { Easy, Normal, Hard, Nightmare }
}
namespace Il2CppOpsive.UltimateCharacterController.Traits {
    public sealed class Attribute {
        public enum AutoUpdateValue { None, Decrease, Increase }
        public sealed class NativeData {
            public string Name = "Battery";
            public float Min, Max = 100, Current, Start, Amount = 1, Delay = .1f, Interval = .1f;
            public AutoUpdateValue UpdateType = AutoUpdateValue.Decrease;
            public object? Event;
            public bool NightmareStateAvailable = true, ThrowReads;
            public int Cancellations, StateChanges, NightmareCharges, EmptyEvents, SetEvents, NativeTicks, NativeResets;
            public readonly List<float> Gauge = new();
            public Action? OnStateChange, OnValue;
        }
        static long nextPointer;
        readonly NativeData data;
        public readonly IntPtr Pointer;
        internal UnlimitedUv? Mod;
        public Func<bool> Host = () => true;
        public Attribute() { data = new(); Pointer = new(++nextPointer); }
        public Attribute(Attribute sameNative) { data = sameNative.data; Pointer = sameNative.Pointer; Mod = sameNative.Mod; Host = sameNative.Host; }
        public NativeData Data => data;
        public string Name => data.ThrowReads ? throw new InvalidOperationException("attribute unavailable") : data.Name;
        public float MaxValue { get => data.Max; set => data.Max = value; }
        public float MinValue => data.Min;
        public float m_Value { get => data.Current; set => data.Current = value; }
        public float Value {
            get => data.Current;
            set {
                Mod?.BeforeSetValue(Host(), this, ref value);
                data.Current = Math.Clamp(value, data.Min, data.Max);
                data.SetEvents++; Notify(); ScheduleAutoUpdate();
            }
        }
        public object? m_AutoUpdateEvent { get => data.Event; set => data.Event = value; }
        public void CancelAutoUpdate() { data.Cancellations++; }
        public void SetState(string name, bool enabled) {
            if (name != "NightmareModeBattery" || !enabled) throw new Exception("Unexpected state change");
            data.NightmareCharges++;
            if (data.NightmareStateAvailable) data.Max = 60;
            StateChange();
        }
        public void StateChange() { data.StateChanges++; data.OnStateChange?.Invoke(); ScheduleAutoUpdate(); }
        void ScheduleAutoUpdate() {
            CancelAutoUpdate();
            if ((data.UpdateType == AutoUpdateValue.Increase && data.Current != data.Max) ||
                (data.UpdateType == AutoUpdateValue.Decrease && data.Current != data.Min)) {
                if (data.Delay == 0) UpdateValue(); else data.Event = new object();
            }
        }
        public void UpdateValue() {
            if (Mod?.BeforeUpdateValue(Host(), this) == false) return;
            data.NativeTicks++;
            if (data.UpdateType == AutoUpdateValue.Increase) data.Current = Math.Min(data.Max, data.Current + data.Amount);
            else data.Current = Math.Max(data.Min, data.Current - data.Amount);
            if (data.Current == data.Min) data.EmptyEvents++;
            else if (data.Current != data.Max || data.UpdateType == AutoUpdateValue.Decrease) data.Event = new object();
            Notify();
        }
        public void ResetValue() {
            if (Mod?.BeforeResetValue(Host(), this) == false) return;
            data.NativeResets++; CancelAutoUpdate(); data.Current = data.Start; Notify();
        }
        void Notify() { data.Gauge.Add(data.Current); data.OnValue?.Invoke(); }
    }
}
