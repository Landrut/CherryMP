using CherryMPShared;
using GTA;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CherryMP.Util
{
    public static class VehicleExtensions
    {
        public static bool IsTireBurst(this Vehicle veh, int wheel)
        {
            return Function.Call<bool>(Hash.IS_VEHICLE_TYRE_BURST, veh, wheel, false);
        }

        public static int GetMod(this Vehicle veh, int id)
        {
            return veh.Mods[(VehicleModType)id].Index;
        }

        public static int SetMod(this Vehicle veh, int id, int var, bool useless)
        {
            return veh.Mods[(VehicleModType)id].Index = var;
        }

        public static VehicleDamageModel GetVehicleDamageModel(this Vehicle veh)
        {
            if (veh == null || !veh.Exists()) return new VehicleDamageModel();
            var mod = new VehicleDamageModel();


            mod.BrokenDoors = 0;
            mod.BrokenWindows = 0;

            for (int i = 0; i < 8; i++)
            {
                if (veh.Doors[(VehicleDoorIndex)i].IsBroken) mod.BrokenDoors |= (byte)(1 << i);
                if (!veh.Windows[(VehicleWindowIndex)i].IsIntact) mod.BrokenWindows |= (byte)(1 << i);
            }
            /*
            var memAdd = veh.MemoryAddress;
            if (memAdd != IntPtr.Zero)
            {
                mod.BrokenLights = MemoryAccess.ReadInt(memAdd + 0x79C); // Old: 0x77C
            }
            */
            return mod;
        }

        public static void SetVehicleDamageModel(this Vehicle veh, VehicleDamageModel model, bool leavedoors = true)
        {
            if (veh == null || model == null || !veh.Exists()) return;

            bool isinvincible = veh.IsInvincible;

            veh.IsInvincible = false;

            // set doors
            for (int i = 0; i < 8; i++)
            {
                if ((model.BrokenDoors & (byte)(1 << i)) != 0)
                {
                    veh.Doors[(VehicleDoorIndex)i].Break(leavedoors);
                }

                if ((model.BrokenWindows & (byte)(1 << i)) != 0)
                {
                    veh.Windows[(VehicleWindowIndex)i].Smash();
                }
                else if (!veh.Windows[(VehicleWindowIndex)i].IsIntact)
                {
                    veh.Windows[(VehicleWindowIndex)i].Repair();
                }
            }
            /*
            var addr = veh.MemoryAddress;
            if (addr != IntPtr.Zero)
            {
                MemoryAccess.WriteInt(addr + 0x79C, model.BrokenLights); // 0x784 ?
            }
            */

            veh.IsInvincible = isinvincible;
        }

        public static bool IsVehicleEmpty(this Vehicle veh)
        {
            if (veh == null) return true;
            if (!veh.IsSeatFree(VehicleSeat.Driver)) return false;
            for (int i = 0; i < veh.PassengerCapacity; i++)
            {
                if (!veh.IsSeatFree((VehicleSeat)i))
                    return false;
            }
            return true;
        }

        public static Dictionary<int, int> GetVehicleMods(this Vehicle veh)
        {
            var dict = new Dictionary<int, int>();
            for (int i = 0; i < 50; i++)
            {
                dict.Add(i, veh.GetMod(i));
            }
            return dict;
        }

        public static unsafe void SetVehicleSteeringAngle(this Vehicle veh, float angle)
        {
            var address = veh.MemoryAddress + 0x8AC;
            var bytes = BitConverter.GetBytes(angle);
            Marshal.Copy(bytes, 0, address, bytes.Length);
        }

        public static int GetFreePassengerSeat(this Vehicle veh)
        {
            if (veh == null) return -3;
            for (int i = 0; i < veh.PassengerCapacity; i++)
            {
                if (veh.IsSeatFree((VehicleSeat)i))
                    return i;
            }
            return -3;
        }

        public static Ped GetResponsiblePed(this Vehicle veh)
        {
            if (veh.GetPedOnSeat(GTA.VehicleSeat.Driver).Handle != 0) return veh.GetPedOnSeat(GTA.VehicleSeat.Driver);

            for (int i = 0; i < veh.PassengerCapacity; i++)
            {
                if (veh.GetPedOnSeat((VehicleSeat)i).Handle != 0) return veh.GetPedOnSeat((VehicleSeat)i);
            }

            return new Ped(0);
        }

        public static void SetNonStandardVehicleMod(this Vehicle veh, int slot, int value)
        {
            var eSlot = (NonStandardVehicleMod)slot;

            switch (eSlot)
            {
                case NonStandardVehicleMod.BulletproofTyres:
                    Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, veh, value != 0);
                    break;
                case NonStandardVehicleMod.NumberPlateStyle:
                    Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT_INDEX, veh, value);
                    break;
                case NonStandardVehicleMod.PearlescentColor:
                    veh.Mods.PearlescentColor = (VehicleColor)value;
                    break;
                case NonStandardVehicleMod.WheelColor:
                    veh.Mods.RimColor = (VehicleColor)value;
                    break;
                case NonStandardVehicleMod.WheelType:
                    veh.Mods.WheelType = (VehicleWheelType)value;
                    break;
                case NonStandardVehicleMod.ModColor1:
                    Function.Call(Hash.SET_VEHICLE_MOD_COLOR_1, veh, (value & 0xFF00) >> 8, (value & 0xFF));
                    break;
                case NonStandardVehicleMod.ModColor2:
                    Function.Call(Hash.SET_VEHICLE_MOD_COLOR_2, veh, (value & 0xFF00) >> 8, (value & 0xFF));
                    break;
                case NonStandardVehicleMod.TyreSmokeColor:
                    Function.Call(Hash.SET_VEHICLE_TYRE_SMOKE_COLOR, veh, (value & 0xFF0000) >> 16, (value & 0xFF00) >> 8, (value & 0xFF));
                    break;
                case NonStandardVehicleMod.WindowTint:
                    Function.Call(Hash.SET_VEHICLE_WINDOW_TINT, veh, value);
                    break;
                case NonStandardVehicleMod.EnginePowerMultiplier:
                    Function.Call(Hash._SET_VEHICLE_ENGINE_POWER_MULTIPLIER, veh, BitConverter.ToSingle(BitConverter.GetBytes(value), 0));
                    break;
                case NonStandardVehicleMod.EngineTorqueMultiplier:
                    Function.Call(Hash._SET_VEHICLE_ENGINE_TORQUE_MULTIPLIER, veh, BitConverter.ToSingle(BitConverter.GetBytes(value), 0));
                    break;
                case NonStandardVehicleMod.NeonLightPos:
                    for (int i = 0; i < 8; i++)
                    {
                        Function.Call(Hash._SET_VEHICLE_NEON_LIGHT_ENABLED, veh, i, (value & 1 << i) != 0);
                    }
                    break;
                case NonStandardVehicleMod.NeonLightColor:
                    Function.Call(Hash._SET_VEHICLE_NEON_LIGHTS_COLOUR, veh, (value & 0xFF0000) >> 16, (value & 0xFF00) >> 8, (value & 0xFF));
                    break;
                case NonStandardVehicleMod.DashboardColor:
                    Function.Call((Hash)6956317558672667244uL, veh, value);
                    break;
                case NonStandardVehicleMod.TrimColor:
                    Function.Call((Hash)17585947422526242585uL, veh, value);
                    break;
            }
        }

    }
}