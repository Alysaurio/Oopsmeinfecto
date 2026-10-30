namespace Oopsmeinfecto
{
    public enum TileType
    {
        Wall = 0,
        Floor = 1,
        Damage = 2,
        Switch = 3
    }

    public static class TileTypes
    {
        public static bool IsWalkable(TileType type)
        {
            return type == TileType.Floor || type == TileType.Damage || type == TileType.Switch;
        }
    }
}
