using Content.Server.Gatherable.Components;
using Content.Shared.Mining.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Server._NF.Gatherable.Components;
using Content.Shared.Maps;
using System.Linq;

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

        // Frontier: gathering changes
        // bad gatherer - not strong enough
        if (_whitelistSystem.IsWhitelistFail(gatherable.ToolWhitelist, gathering.Owner))
        {
            QueueDel(gathering);
            return;
        }

        // DAN EDIT START
        if (HasComp<MiningGatheringUnfloorComponent>(gathering.Owner))
        {
            EntityUid? gridId = _entManager.GetComponent<TransformComponent>(args.OtherEntity).GridUid;
            if (_entManager.TryGetComponent(gridId, out MapGridComponent? grid))
            {
                var childTransform = _entManager.GetComponent<TransformComponent>(args.OtherEntity);
                TileRef tile = _mapSystem.GetTileRef(gridId.Value, grid, childTransform.Coordinates);
                if (tile.GetContentTileDefinition().BaseTurf == "Space" && tile.GetContentTileDefinition().DeconstructTools.Count == 0)
                {
                    _mapSystem.SetTile(gridId.Value, grid, childTransform.Coordinates, Tile.Empty);
                }

            }
        }
        // DAN EDIT END

        // Too strong (e.g. overpen) - gathers ore but destroys it
        else if (TryComp<OreVeinComponent>(args.OtherEntity, out var oreVein)
            && _whitelistSystem.IsWhitelistPass(oreVein.GatherDestructionWhitelist, gathering.Owner))
        {
            oreVein.PreventSpawning = true;
        }
        // End Frontier: gathering changes

        Gather(args.OtherEntity, gathering, gatherable);
        gathering.Comp.Amount--;

        if (gathering.Comp.Amount <= 0)
            QueueDel(gathering);
    }
}
