using System.Threading;
using Content.Server.Construction;
using Content.Server.DeviceLinking.Events;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using JetBrains.Annotations;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;
using Timer = Robust.Shared.Timing.Timer;
using Content.Shared._FD.Components;
using Content.Shared.RepulseAttract;
using Content.Shared._FD.EntitySystems;
using System.Numerics;
using Content.Shared.Whitelist;
using Content.Shared.Throwing;
using Content.Shared.Physics;
using Content.Shared.Conveyor;

namespace Content.Server._FD.EntitySystems
{
    [UsedImplicitly]
    public sealed class ResourceMagnetSystem : SharedResourceMagnetSystem
    {
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly RepulseAttractSystem _attract = default!;
        [Dependency] private readonly SharedTransformSystem _xForm = default!;
        [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
        [Dependency] private readonly ThrowingSystem _throw = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        private EntityQuery<PhysicsComponent> _physicsQuery;
        private HashSet<EntityUid> _entSet = new();
        public override void Initialize()
        {
            base.Initialize();

            _physicsQuery = GetEntityQuery<PhysicsComponent>();
            SubscribeLocalEvent<ResourceMagnetComponent, PowerConsumerReceivedChanged>(ReceivedChanged);
            SubscribeLocalEvent<ResourceMagnetComponent, PowerChangedEvent>(OnApcChanged);
            SubscribeLocalEvent<ResourceMagnetComponent, ActivateInWorldEvent>(OnActivate);
            SubscribeLocalEvent<ResourceMagnetComponent, RefreshPartsEvent>(OnRefreshParts);
            SubscribeLocalEvent<ResourceMagnetComponent, UpgradeExamineEvent>(OnUpgradeExamine);
            SubscribeLocalEvent<ResourceMagnetComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
            SubscribeLocalEvent<ResourceMagnetComponent, SignalReceivedEvent>(OnSignalReceived);
        }

        private void OnAnchorStateChanged(EntityUid uid, ResourceMagnetComponent component, ref AnchorStateChangedEvent args)
        {
            if (args.Anchored)
                return;

            SwitchOff(uid, component);
        }

        private void OnActivate(EntityUid uid, ResourceMagnetComponent component, ActivateInWorldEvent args)
        {
            if (args.Handled)
                return;

            if (TryComp(uid, out PhysicsComponent? phys) && phys.BodyType == BodyType.Static)
            {
                if (!component.IsOn)
                {
                    SwitchOn(uid, component);
                    _popup.PopupEntity(Loc.GetString("comp-magnet-turned-on",
                        ("target", uid)), uid, args.User);
                }
                else
                {
                    SwitchOff(uid, component);
                    _popup.PopupEntity(Loc.GetString("comp-magnet-turned-off",
                        ("target", uid)), uid, args.User);
                }

            }
            else
            {
                _popup.PopupEntity(Loc.GetString("comp-magnet-not-anchored",
                    ("target", uid)), uid, args.User);
            }
        }

        private void ReceivedChanged(
            EntityUid uid,
            ResourceMagnetComponent component,
            ref PowerConsumerReceivedChanged args)
        {
            if (!component.IsOn)
            {
                return;
            }

            if (args.ReceivedPower < args.DrawRate)
            {
                PowerOff(uid, component);
            }
            else
            {
                PowerOn(uid, component);
            }
        }

        private void OnApcChanged(EntityUid uid, ResourceMagnetComponent component, ref PowerChangedEvent args)
        {
            if (!component.IsOn)
            {
                return;
            }

            if (!args.Powered)
            {
                PowerOff(uid, component);
            }
            else
            {
                PowerOn(uid, component);
            }
        }

        private void OnRefreshParts(EntityUid uid, ResourceMagnetComponent component, RefreshPartsEvent args)
        {
            var attractRangeRating = args.PartRatings[component.MachinePartAttractRange];

            component.AttractRange = component.BaseAttractRange * MathF.Pow(component.AttractRangeMultiplier, attractRangeRating - 1);
        }

        private void OnUpgradeExamine(EntityUid uid, ResourceMagnetComponent component, UpgradeExamineEvent args)
        {
            args.AddPercentageUpgrade("magnet-component-upgrade-attract-range", (float)(component.BaseAttractRange / component.AttractRange));
        }

        public void SwitchOff(EntityUid uid, ResourceMagnetComponent component)
        {
            component.IsOn = false;
            if (TryComp<PowerConsumerComponent>(uid, out var powerConsumer))
                powerConsumer.DrawRate = 1; // this needs to be not 0 so that the visuals still work.
            if (TryComp<ApcPowerReceiverComponent>(uid, out var apcReceiver))
                apcReceiver.Load = 1;
            PowerOff(uid, component);
            UpdateAppearance(uid, component);
        }

        public void SwitchOn(EntityUid uid, ResourceMagnetComponent component)
        {
            component.IsOn = true;
            if (TryComp<PowerConsumerComponent>(uid, out var powerConsumer))
                powerConsumer.DrawRate = component.PowerUseActive;
            if (TryComp<ApcPowerReceiverComponent>(uid, out var apcReceiver))
            {
                apcReceiver.Load = component.PowerUseActive;
                if (apcReceiver.Powered)
                    PowerOn(uid, component);
            }
            // Do not directly PowerOn().
            // OnReceivedPowerChanged will get fired due to DrawRate change which will turn it on.
            UpdateAppearance(uid, component);
        }

        public void PowerOff(EntityUid uid, ResourceMagnetComponent component)
        {
            if (!component.IsPowered)
            {
                return;
            }

            component.IsPowered = false;

            // Must be set while emitter powered.
            DebugTools.AssertNotNull(component.TimerCancel);
            component.TimerCancel?.Cancel();

            UpdateAppearance(uid, component);
        }

        public void PowerOn(EntityUid uid, ResourceMagnetComponent component)
        {
            if (component.IsPowered)
            {
                return;
            }

            component.IsPowered = true;
            component.TimerCancel = new CancellationTokenSource();

            Timer.Spawn(component.AttractRate, () => AttractTimerCallback(uid, component), component.TimerCancel.Token);

            UpdateAppearance(uid, component);
        }

        private void AttractTimerCallback(EntityUid uid, ResourceMagnetComponent component)
        {
            if (component.Deleted)
                return;

            // Any power-off condition should result in the timer for this method being cancelled
            // and thus not attracting things around
            DebugTools.Assert(component.IsPowered);
            DebugTools.Assert(component.IsOn);

            Attract(uid, component);

            TimeSpan delay = component.AttractRate; // add an upgrade method for interval, maybe

            // Must be set while emitter powered.
            DebugTools.AssertNotNull(component.TimerCancel);
            Timer.Spawn(delay, () => AttractTimerCallback(uid, component), component.TimerCancel!.Token);
        }

        private void Attract(EntityUid uid, ResourceMagnetComponent component)
        {
            _entSet.Clear();
            var position = _xForm.GetMapCoordinates(uid);
            var epicenter = position.Position;
            var range = component.AttractRange;
            _lookup.GetEntitiesInRange(position.MapId, epicenter, range, _entSet, flags: LookupFlags.Dynamic | LookupFlags.Sundries);

            foreach (var target in _entSet)
            {
                if (!_physicsQuery.TryGetComponent(target, out var physics)
                    || (physics.CollisionLayer & (int)CollisionGroup.SingularityLayer) != 0x0) // exclude layers like ghosts
                    continue;

                if (_whitelist.IsWhitelistFail(component.Whitelist, target))
                    continue;

                if (HasComp<ConveyedComponent>(target))
                    continue;

                var targetPos = _xForm.GetWorldPosition(target);

                // vector from epicenter to target entity
                var direction = -(targetPos - epicenter);

                if (direction.Length() <= 0.3f)
                    continue;

                var speed = component.AttractSpeed * (1 - component.AttractRange / direction.Length());

                _throw.TryThrow(target, direction / 2, Math.Abs(speed), uid, compensateFriction: true, recoil: false, doSpin: false, animated: false, fly: false);
            }
        }

        private void UpdateAppearance(EntityUid uid, ResourceMagnetComponent component)
        {
            MagnetVisualState state;
            if (component.IsPowered)
            {
                state = MagnetVisualState.On;
            }
            else
            {
                state = MagnetVisualState.Off;
            }
            _appearance.SetData(uid, MagnetVisuals.VisualState, state);
        }

        private void OnSignalReceived(EntityUid uid, ResourceMagnetComponent component, ref SignalReceivedEvent args)
        {
            // must anchor the emitter for signals to work
            if (TryComp<PhysicsComponent>(uid, out var phys) && phys.BodyType != BodyType.Static)
                return;

            if (args.Port == component.OffPort)
            {
                SwitchOff(uid, component);
            }
            else if (args.Port == component.OnPort)
            {
                SwitchOn(uid, component);
            }
            else if (args.Port == component.TogglePort)
            {
                if (component.IsOn)
                {
                    SwitchOff(uid, component);
                }
                else
                {
                    SwitchOn(uid, component);
                }
            }
        }
    }
}
