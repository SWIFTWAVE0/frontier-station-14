using System.Threading;
using Content.Shared.Construction.Prototypes;
using Content.Shared.DeviceLinking;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._FD.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ResourceMagnetComponent : Component
{
    public CancellationTokenSource? TimerCancel;

    // whether the power switch is in "on"
    [ViewVariables] public bool IsOn;
    // Whether the power switch is on AND the machine has enough power
    [ViewVariables] public bool IsPowered;

    /// <summary>
    /// The current amount of power being used.
    /// </summary>
    [DataField("powerUseActive"), ViewVariables(VVAccess.ReadWrite)]
    public int PowerUseActive = 1000;

    /// <summary>
    /// The time between each pull.
    /// </summary>
    [DataField("AttractRate"), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan AttractRate = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// The pull speed.
    /// </summary>
    [DataField("AttractSpeed"), ViewVariables(VVAccess.ReadWrite)]
    public float AttractSpeed = 0.5f;

    /// <summary>
    /// The current attract range.
    /// </summary>
    [DataField("AttractRange")]
    public float AttractRange = 4.0f;

    /// <summary>
    /// The base attract range.
    /// </summary>
    [DataField("BaseAttractRange"), ViewVariables(VVAccess.ReadWrite)]
    public float BaseAttractRange = 4.0f;

    /// <summary>
    /// The multiplier for the base attract range
    /// </summary>
    [DataField("AttractRangeMultiplier"), ViewVariables(VVAccess.ReadWrite)]
    public float AttractRangeMultiplier = 1.25f;

    /// <summary>
    /// The machine part that affects magnet range.
    /// </summary>
    [DataField("MachinePartAttractRange", customTypeSerializer: typeof(PrototypeIdSerializer<MachinePartPrototype>))]
    public string MachinePartAttractRange = "Capacitor";

    /// <summary>
    /// What kind of entities should we attract?
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// The visual state that is set when the magnet is turned on.
    /// </summary>
    [DataField("onState")]
    public string? OnState = "salvage-magnet-ready-blinking";

    /// <summary>
    /// Signal port that turns on the magnet.
    /// </summary>
    [DataField("onPort", customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string OnPort = "On";

    /// <summary>
    /// Signal port that turns off the emitter.
    /// </summary>
    [DataField("offPort", customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string OffPort = "Off";

    /// <summary>
    /// Signal port that toggles the emitter on or off.
    /// </summary>
    [DataField("togglePort", customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string TogglePort = "Toggle";

    /// <summary>
    /// Map of signal ports to entity prototype IDs of the entity that will be fired.
    /// </summary>
    [DataField("setTypePorts", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<string, SinkPortPrototype>))]
    public Dictionary<string, string> SetTypePorts = new();
}

[NetSerializable, Serializable]
public enum MagnetVisuals : byte
{
    VisualState
}

[Serializable, NetSerializable]
public enum MagnetVisualLayers : byte
{
    Lights
}

[NetSerializable, Serializable]
public enum MagnetVisualState : byte
{
    On,
    Off
}
