using GTA;
using GTA.Math;
using GTA.Native;

namespace CherryMP.Util
{
    public static class EntityExtensions
    {
        public static bool IsPed(this Entity ent)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_A_PED, ent);
        }

        public static bool IsInRangeOfEx(this Entity ent, Vector3 pos, float range)
        {
            return ent.Position.DistanceToSquared(pos) < (range * range);
        }

        public static Vector3 GetOffsetInWorldCoords(this Entity ent, Vector3 offset)
        {
            return ent.GetOffsetPosition(offset);
        }

        public static Vector3 GetOffsetFromWorldCoords(this Entity ent, Vector3 pos)
        {
            return ent.GetPositionOffset(pos);
        }
    }
}