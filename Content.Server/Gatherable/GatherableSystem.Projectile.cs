using Content.Server.Gatherable.Components;
using Content.Shared.Mining.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Coordinates;

namespace Content.Server.Gatherable;

public sealed partial class GatherableSystem
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    private void InitializeProjectile()
    {
        SubscribeLocalEvent<GatheringProjectileComponent, StartCollideEvent>(OnProjectileCollide);
    }

    private void OnProjectileCollide(Entity<GatheringProjectileComponent> gathering, ref StartCollideEvent args)
    {
        if (!args.OtherFixture.Hard ||
            args.OurFixtureId != SharedProjectileSystem.ProjectileFixture ||
            gathering.Comp.Amount <= 0 ||
            !TryComp<GatherableComponent>(args.OtherEntity, out var gatherable))
        {
            return;
        }

        // DAN CHANGES START

        EntityUid? gridId = _entManager.GetComponent<TransformComponent>(args.OtherEntity).GridUid;
        if (_entManager.TryGetComponent(gridId, out MapGridComponent? grid))
        {
            var childTransform = _entManager.GetComponent<TransformComponent>(args.OtherEntity);
            _mapSystem.SetTile(gridId.Value, grid, childTransform.Coordinates, Tile.Empty);
        }

        // DAN CHANGES END

        // Frontier: gathering changes
        // bad gatherer - not strong enough
        if (_whitelistSystem.IsWhitelistFail(gatherable.ToolWhitelist, gathering.Owner))
        {
            QueueDel(gathering);
            return;
        }
        /* DAN CHANGES START
        // Too strong (e.g. overpen) - gathers ore but destroys it
        if (TryComp<OreVeinComponent>(args.OtherEntity, out var oreVein)
            && _whitelistSystem.IsWhitelistPass(oreVein.GatherDestructionWhitelist, gathering.Owner))
        {
            oreVein.PreventSpawning = true;
        }
        DAN CHANGES END */
        // End Frontier: gathering changes

        Gather(args.OtherEntity, gathering, gatherable);
        gathering.Comp.Amount--;

        if (gathering.Comp.Amount <= 0)
            QueueDel(gathering);
    }
}
