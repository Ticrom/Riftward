using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Riftward;

/// <summary>
/// Lightweight behavior that only handles showing the border warning message.
/// Actual position clamping is done in the Harmony patches on the position packet handlers.
/// </summary>
internal class BorderBehavior : EntityBehavior
{
    private readonly ICoreServerAPI _sapi;
    private readonly RiftwardSystem _system;
    private IPlayer _player;
    private float _warningCooldown;

    public BorderBehavior(Entity entity) : base(entity)
    {
        _sapi = entity.Api as ICoreServerAPI;
        _system = entity.Api.ModLoader.GetModSystem<RiftwardSystem>(true);
    }

    public override void OnGameTick(float deltaTime)
    {
        if (_sapi == null || !_system.IsReady)
            return;

        if (_player == null)
        {
            if (entity is EntityPlayer ep)
                _player = ep.Player;
            if (_player == null)
                return;
        }

        if (!_system.Config.ShowWarningMessage)
            return;

        _warningCooldown -= deltaTime;
        if (_warningCooldown > 0)
            return;

        // Check if player is at the border edge
        double x = entity.ServerPos.X;
        double z = entity.ServerPos.Z;
        if (_system.IsAtBorderEdge(x, z))
        {
            _warningCooldown = _system.Config.WarningCooldownSeconds;
            if (_player is IServerPlayer sp)
            {
                _sapi.SendIngameError(sp, "worldborder",
                    Lang.Get("riftward:border-reached"));
            }
        }
    }

    public override string PropertyName() => "RiftwardBehavior";
}
