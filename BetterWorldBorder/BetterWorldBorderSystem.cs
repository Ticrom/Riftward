using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Riftward;

public class RiftwardSystem : ModSystem
{
    private ICoreServerAPI _sapi;
    private Harmony _harmony;

    private readonly HashSet<int> _readyPlayers = new();

    internal BorderConfig Config { get; private set; }
    internal bool IsReady { get; private set; }

    internal int MaxBlocksX { get; private set; }
    internal int MaxBlocksZ { get; private set; }
    internal int PaddedBlocksX { get; private set; }
    internal int PaddedBlocksZ { get; private set; }
    internal int CenterX { get; private set; }
    internal int CenterZ { get; private set; }

    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Server;
    }

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        _harmony = new Harmony("Riftward");

        // Always unpatch first to handle restarts where statics persist
        _harmony.UnpatchAll("Riftward");
        api.Logger.Notification("Riftward: cleared previous patches");

        // Patch each class individually so one failure doesn't block the rest
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.GetCustomAttribute<HarmonyPatch>() == null)
                continue;

            try
            {
                var processor = _harmony.CreateClassProcessor(type);
                processor.Patch();
                api.Logger.Notification("Riftward: patched {0}", type.Name);
            }
            catch (Exception ex)
            {
                api.Logger.Error("Riftward: failed to patch {0}: {1}", type.Name, ex.Message);
            }
        }

        var patched = _harmony.GetPatchedMethods();
        api.Logger.Notification("Riftward: {0} methods patched total", patched.Count());
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        _sapi = api;
        api.RegisterEntityBehaviorClass("RiftwardBehavior", typeof(BorderBehavior));
        api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, OnRunGame);
        api.Event.OnEntitySpawn += OnEntitySpawn;
        api.Event.OnEntityLoaded += OnEntitySpawn;
    }

    private void OnEntitySpawn(Entity entity)
    {
        if (entity is EntityPlayer)
        {
            // Attach behavior directly if not already present
            if (!entity.HasBehavior("RiftwardBehavior"))
            {
                entity.AddBehavior(new BorderBehavior(entity));
                _sapi.Logger.Debug("[Riftward] Attached behavior to player entity via code");
            }
        }
    }

    private void OnRunGame()
    {
        Config = _sapi.LoadModConfig<BorderConfig>("riftward.json") ?? new BorderConfig();

        var spawn = _sapi.World.DefaultSpawnPosition;

        // Resolve radius
        var radiusChunks = Config.GetRadiusChunks();
        if (radiusChunks == null)
        {
            int halfMap = _sapi.World.BlockAccessor.MapSizeX / 2 / 32;
            radiusChunks = new VecXZ(halfMap, halfMap);
        }
        Config.WorldRadiusChunksXZ = radiusChunks;
        Config.WorldRadiusChunks = null;

        // Resolve center
        if (Config.WorldCenter == null)
            Config.WorldCenter = new VecXZ((int)spawn.X, (int)spawn.Z);

        CenterX = Config.WorldCenter.X;
        CenterZ = Config.WorldCenter.Z;
        MaxBlocksX = radiusChunks.X * 32;
        MaxBlocksZ = radiusChunks.Z * 32;
        PaddedBlocksX = MaxBlocksX - Config.WorldBorderPaddingBlocks;
        PaddedBlocksZ = MaxBlocksZ - Config.WorldBorderPaddingBlocks;

        // Validate
        if (!_sapi.World.BlockAccessor.IsValidPos(new BlockPos(CenterX, 1, CenterZ, 0)))
        {
            _sapi.Logger.Error("Riftward: WorldCenter {0} is not a valid position", Config.WorldCenter);
            IsReady = false;
            return;
        }

        if (IsOutsideBorder(spawn.X, spawn.Z))
        {
            _sapi.Logger.Error("Riftward: Default spawn is outside the world border!");
            IsReady = false;
            return;
        }

        _sapi.Event.PlayerNowPlaying += OnPlayerJoin;
        _sapi.Event.PlayerDisconnect += OnPlayerLeave;

        _sapi.StoreModConfig(Config, "riftward.json");
        IsReady = true;

        _sapi.Logger.Notification("Riftward active: {0} border, center={1}, radius={2}x{3} chunks ({4}x{5} blocks)",
            Config.BorderShape, Config.WorldCenter, radiusChunks.X, radiusChunks.Z, MaxBlocksX, MaxBlocksZ);
    }

    private void OnPlayerJoin(IServerPlayer player)
    {
        int delay = Config.NewPlayerGraceSeconds * 1000;
        _sapi.Event.RegisterCallback(dt =>
        {
            _readyPlayers.Add(player.ClientId);
        }, delay);
    }

    private void OnPlayerLeave(IServerPlayer player)
    {
        _readyPlayers.Remove(player.ClientId);
    }

    internal bool IsPlayerReady(int clientId)
    {
        return _readyPlayers.Contains(clientId);
    }

    /// <summary>
    /// Check if a position is outside the border.
    /// </summary>
    internal bool IsOutsideBorder(double x, double z)
    {
        if (Config.BorderShape == BorderShape.Circle)
        {
            double dx = x - CenterX;
            double dz = z - CenterZ;
            return (dx * dx) / ((double)MaxBlocksX * MaxBlocksX) +
                   (dz * dz) / ((double)MaxBlocksZ * MaxBlocksZ) > 1.0;
        }
        else
        {
            return x > CenterX + MaxBlocksX || x < CenterX - MaxBlocksX ||
                   z > CenterZ + MaxBlocksZ || z < CenterZ - MaxBlocksZ;
        }
    }

    /// <summary>
    /// Clamp a position to be inside the border (with padding).
    /// Returns true if the position was modified.
    /// </summary>
    internal bool ClampToBorder(ref double x, ref double z)
    {
        double origX = x, origZ = z;

        if (Config.BorderShape == BorderShape.Circle)
        {
            double dx = x - CenterX;
            double dz = z - CenterZ;
            double normalizedDist = (dx * dx) / ((double)PaddedBlocksX * PaddedBlocksX) +
                                    (dz * dz) / ((double)PaddedBlocksZ * PaddedBlocksZ);

            if (normalizedDist > 1.0)
            {
                double angle = Math.Atan2(dz, dx);
                x = CenterX + Math.Cos(angle) * PaddedBlocksX;
                z = CenterZ + Math.Sin(angle) * PaddedBlocksZ;
            }
        }
        else
        {
            x = GameMath.Clamp(x, CenterX - PaddedBlocksX, CenterX + PaddedBlocksX);
            z = GameMath.Clamp(z, CenterZ - PaddedBlocksZ, CenterZ + PaddedBlocksZ);
        }

        return x != origX || z != origZ;
    }

    /// <summary>
    /// Check if a position is at or past the border edge (within padding zone).
    /// Used for warning messages.
    /// </summary>
    internal bool IsAtBorderEdge(double x, double z)
    {
        if (Config.BorderShape == BorderShape.Circle)
        {
            double dx = x - CenterX;
            double dz = z - CenterZ;
            return (dx * dx) / ((double)PaddedBlocksX * PaddedBlocksX) +
                   (dz * dz) / ((double)PaddedBlocksZ * PaddedBlocksZ) >= 1.0;
        }
        else
        {
            return x >= CenterX + PaddedBlocksX || x <= CenterX - PaddedBlocksX ||
                   z >= CenterZ + PaddedBlocksZ || z <= CenterZ - PaddedBlocksZ;
        }
    }

    /// <summary>
    /// Check if a translocator exit is valid (inside border).
    /// </summary>
    internal bool IsValidTranslocatorExit(int x, int z)
    {
        if (Config.PreventFarTranslocatorExits)
            return !IsOutsideBorder(x, z);
        return _sapi.World.BlockAccessor.IsValidPos(new BlockPos(x, 1, z, 0));
    }

    public override void Dispose()
    {
        if (_sapi != null)
        {
            _sapi.Event.PlayerNowPlaying -= OnPlayerJoin;
            _sapi.Event.PlayerDisconnect -= OnPlayerLeave;
        }
        _harmony?.UnpatchAll("Riftward");
        _readyPlayers.Clear();
        Config = null;
        IsReady = false;
        base.Dispose();
    }
}
