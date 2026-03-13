using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Riftward;

internal class BorderConfig
{
    [JsonConverter(typeof(StringEnumConverter))]
    public BorderShape BorderShape { get; set; } = BorderShape.Square;

    public int? WorldRadiusChunks { get; set; }
    public VecXZ WorldRadiusChunksXZ { get; set; }
    public VecXZ WorldCenter { get; set; }
    public int WorldBorderPaddingBlocks { get; set; } = 8;
    public bool PreventFarTranslocatorExits { get; set; } = true;
    public int NewPlayerGraceSeconds { get; set; } = 60;
    public float CheckIntervalSeconds { get; set; } = 0.2f;
    public bool ShowWarningMessage { get; set; } = true;
    public float WarningCooldownSeconds { get; set; } = 3f;

    internal VecXZ GetRadiusChunks()
    {
        if (WorldRadiusChunksXZ != null)
            return WorldRadiusChunksXZ;
        if (WorldRadiusChunks.HasValue)
            return new VecXZ(WorldRadiusChunks.Value, WorldRadiusChunks.Value);
        return null;
    }
}

internal enum BorderShape
{
    Square,
    Circle
}

internal class VecXZ
{
    public int X { get; set; }
    public int Z { get; set; }

    public VecXZ() { }

    public VecXZ(int x, int z)
    {
        X = x;
        Z = z;
    }

    public override string ToString() => $"{X},{Z}";

    public static VecXZ operator *(VecXZ self, int n) => new VecXZ(self.X * n, self.Z * n);
    public static VecXZ operator -(VecXZ self, int n) => new VecXZ(self.X - n, self.Z - n);
}
