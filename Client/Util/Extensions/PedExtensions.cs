using GTA;
using GTA.Native;
using System.Collections.Generic;

namespace CherryMP.Util
{
    public static class PedExtensions
    {
        public static bool IsExitingLeavingCar(this Ped player)
        {
            return player.IsSubtaskActive(161) || player.IsSubtaskActive(162) || player.IsSubtaskActive(163) ||
                   player.IsSubtaskActive(164) || player.IsSubtaskActive(167) || player.IsSubtaskActive(168);
        }

        public static int GetPedSeat(this Ped ped)
        {
            if (ped == null || !ped.IsInVehicle()) return -3;
            if (ped.CurrentVehicle.GetPedOnSeat(VehicleSeat.Driver) == ped) return (int)VehicleSeat.Driver;
            for (int i = 0; i < ped.CurrentVehicle.PassengerCapacity; i++)
            {
                if (ped.CurrentVehicle.GetPedOnSeat((VehicleSeat)i) == ped)
                    return i;
            }
            return -3;
        }

        public static Dictionary<int, int> GetPlayerProps(this Ped ped)
        {
            var props = new Dictionary<int, int>();
            for (int i = 0; i < 15; i++)
            {
                var mod = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, ped.Handle, i);
                if (mod == -1) continue;
                props.Add(i, mod);
            }
            return props;
        }

        public static void LoadWeapon(this Ped ped, int model)
        {
            if (model == (int)CherryMPShared.WeaponHash.Unarmed ||
                model == 0) return;

            var start = Util.TickCount;
            while (!Function.Call<bool>(Hash.HAS_WEAPON_ASSET_LOADED, model))
            {
                Function.Call(Hash.REQUEST_WEAPON_ASSET, model, 31, 0);
                Script.Yield();

                if (Util.TickCount - start > 500) break;
            }
        }

        public static void SetPlayerSkin(this Ped ped, PedHash skin)
        {
            var health = ped.Health;
            var model = new Model(skin);

            model.Request(1000);

            if (model.IsInCdImage && model.IsValid)
            {
                while (!model.IsLoaded)
                    Script.Wait(15);

                Function.Call(Hash.SET_PLAYER_MODEL, ped.Handle, model.Hash);
                Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, ped.Handle);
            }

            model.MarkAsNoLongerNeeded();

            ped.MaxHealth = 200;
            ped.Health = health;
        }
    }
}