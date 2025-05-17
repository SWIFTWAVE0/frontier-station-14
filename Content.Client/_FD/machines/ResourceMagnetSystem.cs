using Content.Shared._FD.Components;
using Content.Shared._FD.EntitySystems;
using Robust.Client.GameObjects;

namespace Content.Client._FD.Systems;

public sealed class ResourceMagnetSystem : SharedResourceMagnetSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ResourceMagnetComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, ResourceMagnetComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearance.TryGetData<MagnetVisualState>(uid, MagnetVisuals.VisualState, out var state, args.Component))
            state = MagnetVisualState.Off;

        if (!args.Sprite.LayerMapTryGet(MagnetVisualLayers.Lights, out var layer))
            return;

        switch (state)
        {
            case MagnetVisualState.On:
                if (component.OnState == null)
                    break;
                args.Sprite.LayerSetVisible(layer, true);
                args.Sprite.LayerSetState(layer, component.OnState);
                break;
            case MagnetVisualState.Off:
                args.Sprite.LayerSetVisible(layer, false);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
