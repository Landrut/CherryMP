using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA.Math;
using GTA.Native;

namespace GTA
{
	public enum CargobobHook
	{
		Hook,
		Magnet
	}
	public enum LicensePlateStyle
	{
		BlueOnWhite1 = 3,
		BlueOnWhite2 = 0,
		BlueOnWhite3 = 4,
		YellowOnBlack = 1,
		YellowOnBlue = 2,		
		NorthYankton = 5
	}
	public enum LicensePlateType
	{
		FrontAndRearPlates,
		FrontPlate,
		RearPlate,
		None
	}
	public enum VehicleClass
	{
		Compacts,
		Sedans,
		SUVs,
		Coupes,
		Muscle,
		SportsClassics,
		Sports,
		Super,
		Motorcycles,
		OffRoad,
		Industrial,
		Utility,
		Vans,
		Cycles,
		Boats,
		Helicopters,
		Planes,
		Service,
		Emergency,
		Military,
		Commercial,
		Trains
	}
	public enum VehicleColor
	{
		MetallicBlack,
		MetallicGraphiteBlack,
		MetallicBlackSteel,
		MetallicDarkSilver,
		MetallicSilver,
		MetallicBlueSilver,
		MetallicSteelGray,
		MetallicShadowSilver,
		MetallicStoneSilver,
		MetallicMidnightSilver,
		MetallicGunMetal,
		MetallicAnthraciteGray,
		MatteBlack,
		MatteGray,
		MatteLightGray,
		UtilBlack,
		UtilBlackPoly,
		UtilDarksilver,
		UtilSilver,
		UtilGunMetal,
		UtilShadowSilver,
		WornBlack,
		WornGraphite,
		WornSilverGray,
		WornSilver,
		WornBlueSilver,
		WornShadowSilver,
		MetallicRed,
		MetallicTorinoRed,
		MetallicFormulaRed,
		MetallicBlazeRed,
		MetallicGracefulRed,
		MetallicGarnetRed,
		MetallicDesertRed,
		MetallicCabernetRed,
		MetallicCandyRed,
		MetallicSunriseOrange,
		MetallicClassicGold,
		MetallicOrange,
		MatteRed,
		MatteDarkRed,
		MatteOrange,
		MatteYellow,
		UtilRed,
		UtilBrightRed,
		UtilGarnetRed,
		WornRed,
		WornGoldenRed,
		WornDarkRed,
		MetallicDarkGreen,
		MetallicRacingGreen,
		MetallicSeaGreen,
		MetallicOliveGreen,
		MetallicGreen,
		MetallicGasolineBlueGreen,
		MatteLimeGreen,
		UtilDarkGreen,
		UtilGreen,
		WornDarkGreen,
		WornGreen,
		WornSeaWash,
		MetallicMidnightBlue,
		MetallicDarkBlue,
		MetallicSaxonyBlue,
		MetallicBlue,
		MetallicMarinerBlue,
		MetallicHarborBlue,
		MetallicDiamondBlue,
		MetallicSurfBlue,
		MetallicNauticalBlue,
		MetallicBrightBlue,
		MetallicPurpleBlue,
		MetallicSpinnakerBlue,
		MetallicUltraBlue,
		UtilDarkBlue = 75,
		UtilMidnightBlue,
		UtilBlue,
		UtilSeaFoamBlue,
		UtilLightningBlue,
		UtilMauiBluePoly,
		UtilBrightBlue,
		MatteDarkBlue,
		MatteBlue,
		MatteMidnightBlue,
		WornDarkBlue,
		WornBlue,
		WornLightBlue,
		MetallicTaxiYellow,
		MetallicRaceYellow,
		MetallicBronze,
		MetallicYellowBird,
		MetallicLime,
		MetallicChampagne,
		MetallicPuebloBeige,
		MetallicDarkIvory,
		MetallicChocoBrown,
		MetallicGoldenBrown,
		MetallicLightBrown,
		MetallicStrawBeige,
		MetallicMossBrown,
		MetallicBistonBrown,
		MetallicBeechwood,
		MetallicDarkBeechwood,
		MetallicChocoOrange,
		MetallicBeachSand,
		MetallicSunBleechedSand,
		MetallicCream,
		UtilBrown,
		UtilMediumBrown,
		UtilLightBrown,
		MetallicWhite,
		MetallicFrostWhite,
		WornHoneyBeige,
		WornBrown,
		WornDarkBrown,
		WornStrawBeige,
		BrushedSteel,
		BrushedBlackSteel,
		BrushedAluminium,
		Chrome,
		WornOffWhite,
		UtilOffWhite,
		WornOrange,
		WornLightOrange,
		MetallicSecuricorGreen,
		WornTaxiYellow,
		PoliceCarBlue,
		MatteGreen,
		MatteBrown,
		MatteWhite = 131,
		WornWhite,
		WornOliveArmyGreen,
		PureWhite,
		HotPink,
		Salmonpink,
		MetallicVermillionPink,
		Orange,
		Green,
		Blue,
		MettalicBlackBlue,
		MetallicBlackPurple,
		MetallicBlackRed,
		HunterGreen,
		MetallicPurple,
		MetaillicVDarkBlue,
		ModshopBlack1,
		MattePurple,
		MatteDarkPurple,
		MetallicLavaRed,
		MatteForestGreen,
		MatteOliveDrab,
		MatteDesertBrown,
		MatteDesertTan,
		MatteFoliageGreen,
		DefaultAlloyColor,
		EpsilonBlue,
		PureGold,
		BrushedGold
	}
	public enum VehicleLandingGearState
	{
		Deployed,
		Closing,
		Opening,
		Retracted
	}
	public enum VehicleLockStatus
	{
		None,
		Unlocked,
		Locked,
		LockedForPlayer,
		StickPlayerInside,
		CanBeBrokenInto = 7,
		CanBeBrokenIntoPersist,
		CannotBeTriedToEnter = 10
	}
	public enum VehicleNeonLight
	{
		Left,
		Right,
		Front,
		Back
	}
	public enum VehicleRoofState
	{
		Closed,
		Opening,
		Opened,
		Closing
	}
	public enum VehicleSeat
	{
		None = -3,
		Any,
		Driver,
		Passenger,
		LeftFront = -1,
		RightFront,
		LeftRear,
		RightRear,
		ExtraSeat1,
		ExtraSeat2,
		ExtraSeat3,
		ExtraSeat4,
		ExtraSeat5,
		ExtraSeat6,
		ExtraSeat7,
		ExtraSeat8,
		ExtraSeat9,
		ExtraSeat10,
		ExtraSeat11,
		ExtraSeat12
	}
	public enum VehicleWindowTint
	{
		None,
		PureBlack,
		DarkSmoke,
		LightSmoke,
		Stock,
		Limo,
		Green
	}

	public sealed class Vehicle : Entity
	{
		#region Fields
		VehicleDoorCollection _doors;
		VehicleModCollection _mods;
		VehicleWheelCollection _wheels;
		VehicleWindowCollection _windows;
		#endregion

		public Vehicle(int handle) : base(handle)
		{
		}

		public string DisplayName
		{
			get
			{
				return GetModelDisplayName(base.Model);
			}
		}
		public string FriendlyName
		{
			get
			{
				return Game.GetGXTEntry(DisplayName);
			}
		}

		public string ClassDisplayName
		{
			get
			{
				return GetClassDisplayName(ClassType);
			}
		}

		public string ClassFriendlyName
		{
			get
			{
				return Game.GetGXTEntry(ClassDisplayName);
			}
		}

		public VehicleClass ClassType
		{
			get
			{
				return Function.Call<VehicleClass>(Hash.GET_VEHICLE_CLASS, Handle);
			}
		}

		public float BodyHealth
		{
			get
			{
				return Function.Call<float>(Hash.GET_VEHICLE_BODY_HEALTH, Handle);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_BODY_HEALTH, Handle, value);
			}
		}
		public float EngineHealth
		{
			get
			{
				return Function.Call<float>(Hash.GET_VEHICLE_ENGINE_HEALTH, Handle);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_ENGINE_HEALTH, Handle, value);
			}
		}
		public float PetrolTankHealth
		{
			get
			{
				return Function.Call<float>(Hash.GET_VEHICLE_PETROL_TANK_HEALTH, Handle);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_PETROL_TANK_HEALTH, Handle, value);
			}
		}
		public float FuelLevel
		{
			get
			{
				if (MemoryAddress == IntPtr.Zero)
				{
					return 0.0f;
				}

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x768 : 0x758;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x788 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x7A8 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x7B8 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x7D4 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x7F4 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x834 : offset;

                return MemoryAccess.ReadFloat(MemoryAddress + offset);
			}
			set
			{
				if (MemoryAddress == IntPtr.Zero)
				{
					return;
				}

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x768 : 0x758;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x788 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x7A8 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x7B8 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x7D4 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x7F4 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x834 : offset;

                MemoryAccess.WriteFloat(MemoryAddress + offset, value);
			}
		}

		public bool IsEngineRunning
		{
			get
			{
				return Function.Call<bool>(Hash.GET_IS_VEHICLE_ENGINE_RUNNING, Handle);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_ENGINE_ON, Handle, value, true);
			}
		}
		public bool IsRadioEnabled
		{
			set
			{
				Function.Call(Hash.SET_VEHICLE_RADIO_ENABLED, Handle, value);
			}
		}
		public RadioStation RadioStation
		{
			set
			{
				if (value == RadioStation.RadioOff)
				{
					Function.Call(Hash.SET_VEH_RADIO_STATION, "OFF");
				}
				else if (Enum.IsDefined(typeof(RadioStation), value))
				{
					Function.Call(Hash.SET_VEH_RADIO_STATION, Game._radioNames[(int)value]);
				}
			}
		}

		public float Speed
		{
			get
			{
				return Function.Call<float>(Hash.GET_ENTITY_SPEED, Handle);
			}
			set
			{
				if (Model.IsTrain)
				{
					Function.Call(Hash.SET_TRAIN_SPEED, Handle, value);
					Function.Call(Hash.SET_TRAIN_CRUISE_SPEED, Handle, value);
				}
				else
				{
					Function.Call(Hash.SET_VEHICLE_FORWARD_SPEED, Handle, value);
				}
			}
		}
		public float WheelSpeed
		{
			get
			{
				if (MemoryAddress == IntPtr.Zero)
				{
					return 0.0f;
				}

                //old game version hasnt been tested, just following the patterns above for old game ver
                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x9A4 : 0x994;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x9C4 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x9F0 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0xA00 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0xA10 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0xA30 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0xA80 : offset;

                return MemoryAccess.ReadFloat(MemoryAddress + offset);
			}
		}
		public float Acceleration
		{
			get
			{
				if (MemoryAddress == IntPtr.Zero)
				{
					return 0.0f;
				}

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x7E4 : 0x7D4;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x804 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x824 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x834 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x854 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x874 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x8C4 : offset;

                return MemoryAccess.ReadFloat(MemoryAddress + offset);
			}
		}
		public float CurrentRPM
		{
			get
			{
				if (MemoryAddress == IntPtr.Zero)
				{
					return 0.0f;
				}

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x7D4 : 0x7C4;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x7F4 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x814 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x824 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x844 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x864 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x8B4 : offset;

                return MemoryAccess.ReadFloat(MemoryAddress + offset);
			}
			set
			{
				if (MemoryAddress == IntPtr.Zero)
				{
					return;
				}

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x7D4 : 0x7C4;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x7F4 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x814 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x824 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x844 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x864 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x8B4 : offset;

                MemoryAccess.WriteFloat(MemoryAddress + offset, value);
			}
		}

		public int HighGear
		{
			get
			{
				if (MemoryAddress == IntPtr.Zero)
				{
					return 0;
				}

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x7A6 : 0x796;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x7C6 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x7E6 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x7F6 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x816 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x836 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x876 : offset;

                return (int)MemoryAccess.ReadByte(MemoryAddress + offset);
			}
			set
			{
				if (value < 0 || value > byte.MaxValue)
				{
					throw new ArgumentOutOfRangeException("value", "Values must be between 0 and 255, inclusive.");
				}

				if (MemoryAddress == IntPtr.Zero)
				{
					return;
				}

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x7A6 : 0x796;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x7C6 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x7E6 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x7F6 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x816 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x836 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x876 : offset;

                MemoryAccess.WriteByte(MemoryAddress + offset, (byte)value);
			}
		}
		public int CurrentGear
		{
			get
			{
				if (MemoryAddress == IntPtr.Zero)
				{
					return 0;
				}

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x7A2 : 0x792;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x7C2 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x7E2 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x7F2 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x812 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x832 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x872 : offset;

                return (int)MemoryAccess.ReadByte(MemoryAddress + offset);
			}
		}

        public float SteeringAngle
        {
            get
            {
                if (MemoryAddress == IntPtr.Zero)
                {
                    return 0.0f;
                }

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x8AC : 0x89C;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x8CC : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x8F4 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x904 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x924 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x944 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x994 : offset;

                return (float)(MemoryAccess.ReadFloat(MemoryAddress + offset) * (180.0 / System.Math.PI));
            }
            set
            {
                if (MemoryAddress == IntPtr.Zero)
                {
                    return;
                }

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x8AC : 0x89C;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x8CC : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x8F4 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x904 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x924 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x944 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x994 : offset;

                MemoryAccess.WriteFloat(MemoryAddress + offset, value);
            }
        }

        public float SteeringScale
        {
            get
            {
                if (MemoryAddress == IntPtr.Zero)
                {
                    return 0.0f;
                }

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x8A4 : 0x894;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x8C4 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x8EC : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x8FC : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x91C : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x93C : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x98C : offset;

                return MemoryAccess.ReadFloat(MemoryAddress + offset);
            }
            set
            {
                if (MemoryAddress == IntPtr.Zero)
                {
                    return;
                }

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x8A4 : 0x894;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x8C4 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x8EC : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x8FC : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x91C : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x93C : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x98C : offset;

                MemoryAccess.WriteFloat(MemoryAddress + offset, value);
            }
        }

        public bool HasForks
		{
			get
			{
				return HasBone("forks");
			}
		}

		public bool HasAlarm
		{
			set
			{
				Function.Call(Hash.SET_VEHICLE_ALARM, Handle, value);
			}
		}
		public bool AlarmActive
		{
			get
			{
				return Function.Call<bool>(Hash.IS_VEHICLE_ALARM_ACTIVATED, Handle);
			}
		}
		public void StartAlarm()
		{
			Function.Call(Hash.START_VEHICLE_ALARM, Handle);
		}

		public bool HasSiren
		{
			get
			{
				return HasBone("siren1");
			}
		}
		public bool SirenActive
		{
			get
			{
				return Function.Call<bool>(Hash.IS_VEHICLE_SIREN_ON, Handle);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_SIREN, Handle, value);
			}
		}
		public bool IsSirenSilent
		{
			set
			{
				// Sets if the siren is silent actually 
				Function.Call(Hash.DISABLE_VEHICLE_IMPACT_EXPLOSION_ACTIVATION, Handle, value);
			}
		}
		public void SoundHorn(int duration)
		{
			Function.Call(Hash.START_VEHICLE_HORN, Handle, duration, Game.GenerateHash("HELDDOWN"), 0);
		}
		public bool IsWanted
		{
			get
			{
				IntPtr memoryAddress = MemoryAddress;
				if (memoryAddress == IntPtr.Zero)
				{
					return false;
				}
                //Unsure of the exact version this switched, but all others in the rangs are the same
                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x84C : 0x83C;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x86C : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x894 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x8A4 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x8C4 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x8E4 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x934 : offset;


                return MemoryAccess.IsBitSet(MemoryAddress + offset, 3);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_IS_WANTED, Handle, value);
			}
		}

		public bool ProvidesCover
		{
			get
			{
				IntPtr memoryAddress = MemoryAddress;
				if (memoryAddress == IntPtr.Zero)
				{
					return false;
				}
                //Unsure of the exact version this switched, but all others in the rangs are the same
                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x83C : 0x82C;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x85C : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x884 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x894 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x8B4 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x8D4 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x924 : offset;

                return MemoryAccess.IsBitSet(MemoryAddress + offset, 2);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_PROVIDES_COVER, Handle, value);
			}
		}

		public bool DropsMoneyOnExplosion
		{		   
			get
			{
				IntPtr memoryAddress = MemoryAddress;
				if (memoryAddress == IntPtr.Zero)
				{
					return false;
				}
                //Unsure of the exact version this switched or if it switched over a few title updates
                //as its shifted by 0x20 bytes where as rest are only 0x10 bytes
                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0xA98 : 0xA78;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0xAD8 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0xB18 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0xB58 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0xBA8 : offset;

                int maxVehType = Game.Version >= GameVersion.v1_0_944_2_Steam ? 10 : 8;

                if (MemoryAccess.ReadInt(memoryAddress + offset) <= maxVehType)
                {
                    offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x1319 : 0x12F9;
                    offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x1349 : offset;
                    offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x13B9 : offset;
                    offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x1409 : offset;
                    offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x1459 : offset;

                    return MemoryAccess.IsBitSet(memoryAddress + offset, 1);
                }

                return false;
            }
            set
            {
                Function.Call(Hash._SET_VEHICLE_CREATES_MONEY_PICKUPS_WHEN_EXPLODED, Handle, value);
            }
        }

		public bool PreviouslyOwnedByPlayer
		{
			get
			{
				if (MemoryAddress == IntPtr.Zero)
				{
					return false;
				}

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x844 : 0x834;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x864 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x88C : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x89C : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x8BC : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x8DC : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x92C : offset;

                return MemoryAccess.IsBitSet(MemoryAddress + offset, 1);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER, Handle, value);
			}
		}

		public bool NeedsToBeHotwired
		{
			get
			{
				if (MemoryAddress == IntPtr.Zero)
				{
					return false;
				}

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x844 : 0x834;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x864 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x88C : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x89C : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x8BC : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x8DC : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x92C : offset;

                return MemoryAccess.IsBitSet(MemoryAddress + offset, 2);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_NEEDS_TO_BE_HOTWIRED, Handle, value);
			}
		}

		public bool LightsOn
		{
			get
			{
				var lightState1 = new OutputArgument();
				var lightState2 = new OutputArgument();
				Function.Call(Hash.GET_VEHICLE_LIGHTS_STATE, Handle, lightState1, lightState2);

				return lightState1.GetResult<bool>();
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_LIGHTS, Handle, value ? 3 : 4);
			}
		}
		public bool HighBeamsOn
		{
			get
			{
				var lightState1 = new OutputArgument();
				var lightState2 = new OutputArgument();
				Function.Call(Hash.GET_VEHICLE_LIGHTS_STATE, Handle);

				return lightState2.GetResult<bool>();
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_FULLBEAM, Handle, value);
			}
		}
		public bool InteriorLightOn
		{
			get
			{
				if (MemoryAddress == IntPtr.Zero)
				{
					return false;
				}

                int offset = Game.Version >= GameVersion.v1_0_372_2_Steam ? 0x841 : 0x831;
                offset = Game.Version >= GameVersion.v1_0_877_1_Steam ? 0x861 : offset;
                offset = Game.Version >= GameVersion.v1_0_944_2_Steam ? 0x889 : offset;
                offset = Game.Version >= GameVersion.v1_0_1103_2_Steam ? 0x899 : offset;
                offset = Game.Version >= GameVersion.v1_0_1180_2_Steam ? 0x8B9 : offset;
                offset = Game.Version >= GameVersion.v1_0_1290_1_Steam ? 0x8D9 : offset;
                offset = Game.Version >= GameVersion.v1_0_1604_0_Steam ? 0x929 : offset;

                return MemoryAccess.IsBitSet(MemoryAddress + offset, 6);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_INTERIORLIGHT, Handle, value);
			}
		}
		public bool SearchLightOn
		{
			get
			{
				return Function.Call<bool>(Hash.IS_VEHICLE_SEARCHLIGHT_ON, Handle);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_SEARCHLIGHT, Handle, value, 0);
			}
		}
		public bool TaxiLightOn
		{
			get
			{
				return Function.Call<bool>(Hash.IS_TAXI_LIGHT_ON, Handle);
			}
			set
			{
				Function.Call(Hash.SET_TAXI_LIGHTS, Handle, value);
			}
		}
		public bool LeftIndicatorLightOn
		{
			set
			{
				Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, Handle, true, value);
			}
		}
		public bool RightIndicatorLightOn
		{
			set
			{
				Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, Handle, false, value);
			}
		}
		public bool HandbrakeOn
		{
			set
			{
				Function.Call(Hash.SET_VEHICLE_HANDBRAKE, Handle, value);
			}
		}
		public bool BrakeLightsOn
		{
			set
			{
				Function.Call(Hash.SET_VEHICLE_BRAKE_LIGHTS, Handle, value);
			}
		}
		public float LightsMultiplier
		{
			set
			{
				Function.Call(Hash.SET_VEHICLE_LIGHT_MULTIPLIER, Handle, value);
			}
		}

		public bool CanBeVisiblyDamaged
		{
			set
			{
				Function.Call(Hash.SET_VEHICLE_CAN_BE_VISIBLY_DAMAGED, Handle, value);
			}
		}

		public bool IsDamaged
		{
			get
			{
				return Function.Call<bool>(Hash._IS_VEHICLE_DAMAGED, Handle);
			}
		}
		public bool IsDriveable
		{
			get
			{
				return Function.Call<bool>(Hash.IS_VEHICLE_DRIVEABLE, Handle, 0);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_UNDRIVEABLE, Handle, !value);
			}
		}
		public bool HasRoof
		{
			get
			{
				return Function.Call<bool>(Hash.DOES_VEHICLE_HAVE_ROOF, Handle);
			}
		}
		public bool IsLeftHeadLightBroken
		{
			get
			{
				return Function.Call<bool>(Hash.GET_IS_LEFT_VEHICLE_HEADLIGHT_DAMAGED, Handle);
			}
			set
			{
				if (MemoryAddress == IntPtr.Zero)
				{
					return;
				}

				IntPtr address = MemoryAddress + 1916;
                if (Game.Version > (GameVersion)25) address += 0x20;
                if (Game.Version > (GameVersion)27) address += 0x20;


                if (value)
				{
					MemoryAccess.SetBit(address, 0);
				}
				else
				{
					MemoryAccess.ClearBit(address, 0);
				}
			}
		}
		public bool IsRightHeadLightBroken
		{
			get
			{
				return Function.Call<bool>(Hash.GET_IS_RIGHT_VEHICLE_HEADLIGHT_DAMAGED, Handle);
			}
			set
			{
				if (MemoryAddress == IntPtr.Zero)
				{
					return;
				}

				IntPtr address = MemoryAddress + 1916;
                if (Game.Version > (GameVersion)25) address += 0x20;
                if (Game.Version > (GameVersion)27) address += 0x20;


                if (value)
				{
					MemoryAccess.SetBit(address, 1);
				}
				else
				{
					MemoryAccess.ClearBit(address, 1);
				}
			}
		}
		public bool IsRearBumperBrokenOff
		{
			get
			{
				return Function.Call<bool>(Hash.IS_VEHICLE_BUMPER_BROKEN_OFF, Handle, false);
			}
		}
		public bool IsFrontBumperBrokenOff
		{
			get
			{
				return Function.Call<bool>(Hash.IS_VEHICLE_BUMPER_BROKEN_OFF, Handle, true);
			}
		}

		public bool IsAxlesStrong
		{
			set
			{
				Function.Call<bool>(Hash.SET_VEHICLE_HAS_STRONG_AXLES, Handle, value);
			}
		}

		public bool CanEngineDegrade
		{
			set
			{
				Function.Call(Hash.SET_VEHICLE_ENGINE_CAN_DEGRADE, Handle, value);
			}
		}
		public float EnginePowerMultiplier
		{
			set
			{
				Function.Call(Hash._SET_VEHICLE_ENGINE_POWER_MULTIPLIER, Handle, value);
			}
		}
		public float EngineTorqueMultiplier
		{
			set
			{
				Function.Call(Hash._SET_VEHICLE_ENGINE_TORQUE_MULTIPLIER, Handle, value);
			}
		}

		public VehicleLandingGearState LandingGearState
		{
			get
			{
				return Function.Call<VehicleLandingGearState>(Hash.GET_LANDING_GEAR_STATE, Handle);
			}
			set
			{
				Function.Call(Hash.CONTROL_LANDING_GEAR, Handle, value);
			}
		}
		public VehicleRoofState RoofState
		{
			get
			{
				return Function.Call<VehicleRoofState>(Hash.GET_CONVERTIBLE_ROOF_STATE, Handle);
			}
			set
			{
				switch (value)
				{
					case VehicleRoofState.Closed:
						Function.Call(Hash.RAISE_CONVERTIBLE_ROOF, Handle, true);
						Function.Call(Hash.RAISE_CONVERTIBLE_ROOF, Handle, false);
						break;
					case VehicleRoofState.Closing:
						Function.Call(Hash.RAISE_CONVERTIBLE_ROOF, Handle, false);
						break;
					case VehicleRoofState.Opened:
						Function.Call(Hash.LOWER_CONVERTIBLE_ROOF, Handle, true);
						Function.Call(Hash.LOWER_CONVERTIBLE_ROOF, Handle, false);
						break;
					case VehicleRoofState.Opening:
						Function.Call(Hash.LOWER_CONVERTIBLE_ROOF, Handle, false);
						break;
				}
			}
		}
		public VehicleLockStatus LockStatus
		{
			get
			{
				return Function.Call<VehicleLockStatus>(Hash.GET_VEHICLE_DOOR_LOCK_STATUS, Handle);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED, Handle, value);
			}
		}

		public float MaxBraking
		{
			get
			{
				return Function.Call<float>(Hash.GET_VEHICLE_MAX_BRAKING, Handle);
			}
		}
		public float MaxTraction
		{
			get
			{
				return Function.Call<float>(Hash.GET_VEHICLE_MAX_TRACTION, Handle);
			}
		}

		public bool IsOnAllWheels
		{

			get
			{
				return Function.Call<bool>(Hash.IS_VEHICLE_ON_ALL_WHEELS, Handle);
			}
		}

		public bool IsStopped
		{

			get
			{
				return Function.Call<bool>(Hash.IS_VEHICLE_STOPPED, Handle);
			}
		}
		public bool IsStoppedAtTrafficLights
		{

			get
			{
				return Function.Call<bool>(Hash.IS_VEHICLE_STOPPED_AT_TRAFFIC_LIGHTS, Handle);
			}
		}

		public bool IsStolen
		{
			get
			{
				return Function.Call<bool>(Hash.IS_VEHICLE_STOLEN, Handle);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_IS_STOLEN, Handle, value);
			}
		}

		public bool IsConvertible
		{

			get
			{
				return Function.Call<bool>(Hash.IS_VEHICLE_A_CONVERTIBLE, Handle, 0);
			}
		}

		public bool IsBurnoutForced
		{
			set
			{
				Function.Call<bool>(Hash.SET_VEHICLE_BURNOUT, Handle, value);
			}
		}
		public bool IsInBurnout
		{
			get
			{
				return Function.Call<bool>(Hash.IS_VEHICLE_IN_BURNOUT, Handle);
			}
		}

		public Ped Driver
		{
			get
			{
				return GetPedOnSeat(VehicleSeat.Driver);
			}
		}
		public Ped[] Occupants
		{
			get
			{
				Ped driver = Driver;

				if (!Ped.Exists(driver))
				{
					return Passengers;
				}

				var result = new Ped[PassengerCount + 1];
				result[0] = driver;

				for (int i = 0, j = 0, seats = PassengerCapacity; i < seats && j < result.Length; i++)
				{												  
					if (!IsSeatFree((VehicleSeat)i))
					{
						result[j++ + 1] = GetPedOnSeat((VehicleSeat)i);
					}
				}

				return result;
			}
		}
		public Ped[] Passengers
		{
			get
			{
				var result = new Ped[PassengerCount];

				if (result.Length == 0)
				{
					return result;
				}

				for (int i = 0, j = 0, seats = PassengerCapacity; i < seats && j < result.Length; i++)
				{
					if (!IsSeatFree((VehicleSeat)i))
					{
						result[j++] = GetPedOnSeat((VehicleSeat)i);
					}
				}

				return result;
			}
		}
		public int PassengerCapacity
		{
			get
			{
				return Function.Call<int>(Hash.GET_VEHICLE_MAX_NUMBER_OF_PASSENGERS, Handle);
			}
		}
		public int PassengerCount
		{
			get
			{
				return Function.Call<int>(Hash.GET_VEHICLE_NUMBER_OF_PASSENGERS, Handle);
			}
		}

		public VehicleDoorCollection Doors
		{
			get
			{
				if (_doors == null)
				{
					_doors = new VehicleDoorCollection(this);
				}

				return _doors;
			}
		}
		public VehicleModCollection Mods
		{
			get
			{
				if (_mods == null)
				{
					_mods = new VehicleModCollection(this);
				}

				return _mods;
			}
		}
		public VehicleWheelCollection Wheels
		{
			get
			{
				if (_wheels == null)
				{
					_wheels = new VehicleWheelCollection(this);
				}

				return _wheels;
			}
		}
		public VehicleWindowCollection Windows
		{
			get
			{
				if (_windows == null)
				{
					_windows = new VehicleWindowCollection(this);
				}

				return _windows;
			}
		}

		public bool ExtraExists(int extra)
		{
			return Function.Call<bool>(Hash.DOES_EXTRA_EXIST, Handle, extra);
		}
		public bool IsExtraOn(int extra)
		{
			return Function.Call<bool>(Hash.IS_VEHICLE_EXTRA_TURNED_ON, Handle, extra);
		}
		public void ToggleExtra(int extra, bool toggle)
		{
			Function.Call(Hash.SET_VEHICLE_EXTRA, Handle, extra, !toggle);
		}

		public Ped GetPedOnSeat(VehicleSeat seat)
		{
			return new Ped(Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, Handle, seat));
		}
		public bool IsSeatFree(VehicleSeat seat)
		{
			return Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, Handle, seat);
		}

		public void Wash()
		{
			DirtLevel = 0f;
		}
		public float DirtLevel
		{
			get
			{
				return Function.Call<float>(Hash.GET_VEHICLE_DIRT_LEVEL, Handle);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_DIRT_LEVEL, Handle, value);
			}
		}

		public bool PlaceOnGround()
		{
			return Function.Call<bool>(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, Handle);
		}
		public void PlaceOnNextStreet()
		{
			Vector3 currentPosition = Position;
			var headingArg = new OutputArgument();
			var newPositionArg = new OutputArgument();

			for (int i = 1; i < 40; i++)
			{
				Function.Call(Hash.GET_NTH_CLOSEST_VEHICLE_NODE_WITH_HEADING, currentPosition.X, currentPosition.Y, currentPosition.Z, i, newPositionArg, headingArg, new OutputArgument(), 1, 0x40400000, 0);

				var newPosition = newPositionArg.GetResult<Vector3>();

				if (!Function.Call<bool>(Hash.IS_POINT_OBSCURED_BY_A_MISSION_ENTITY, newPosition.X, newPosition.Y, newPosition.Z, 5.0f, 5.0f, 5.0f, 0))
				{
					Position = newPosition;
					PlaceOnGround();
					Heading = headingArg.GetResult<float>();
					break;
				}
			}
		}

		public void Repair()
		{
			Function.Call(Hash.SET_VEHICLE_FIXED, Handle);
		}
		public void Explode()
		{
			Function.Call(Hash.EXPLODE_VEHICLE, Handle, true, false);
		}

		public bool CanTiresBurst
		{
			get
			{
				return Function.Call<bool>(Hash.GET_VEHICLE_TYRES_CAN_BURST, Handle);
			}
			set
			{
				Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, Handle, value);
			}
		}
		public bool CanWheelsBreak
		{
			set
			{
				Function.Call(Hash.SET_VEHICLE_WHEELS_CAN_BREAK, Handle, value);
			}
		}

		public bool HasBombBay
		{
			get
			{
				return HasBone("door_hatch_l") && HasBone("door_hatch_r");
			}
		}
		public void OpenBombBay()
		{
			if (HasBombBay)
			{
				Function.Call(Hash.OPEN_BOMB_BAY_DOORS, Handle);
			}
		}
		public void CloseBombBay()
		{
			if (HasBombBay)
			{
				Function.Call(Hash.CLOSE_BOMB_BAY_DOORS, Handle);
			}
		}

		public void SetHeliYawPitchRollMult(float mult)
		{
			if (Model.IsHelicopter && mult >= 0 && mult <= 1)
			{
				Function.Call(Hash._SET_HELICOPTER_ROLL_PITCH_YAW_MULT_HEALTH, Handle, mult);
			}
		}

		public void DropCargobobHook(CargobobHook hook)
		{
			if (Model.IsCargobob)
			{
				Function.Call(Hash.CREATE_PICK_UP_ROPE_FOR_CARGOBOB, Handle, hook);
			}
		}
		public void RetractCargobobHook()
		{
			if (Model.IsCargobob)
			{
				Function.Call(Hash.REMOVE_PICK_UP_ROPE_FOR_CARGOBOB, Handle);
			}
		}
		public bool IsCargobobHookActive()
		{
			if (Model.IsCargobob)
			{
				return Function.Call<bool>(Hash.DOES_CARGOBOB_HAVE_PICK_UP_ROPE, Handle) || Function.Call<bool>(Hash._DOES_CARGOBOB_HAVE_PICKUP_MAGNET, Handle);
			}

			return false;
		}
		public bool IsCargobobHookActive(CargobobHook hook)
		{
			if (Model.IsCargobob)
			{
				switch (hook)
				{
					case CargobobHook.Hook:
						return Function.Call<bool>(Hash.DOES_CARGOBOB_HAVE_PICK_UP_ROPE, Handle);
					case CargobobHook.Magnet:
						return Function.Call<bool>(Hash._DOES_CARGOBOB_HAVE_PICKUP_MAGNET, Handle);
				}
			}

			return false;
		}
		public void CargoBobMagnetGrabVehicle()
		{
			if (IsCargobobHookActive(CargobobHook.Magnet))
			{
				Function.Call(Hash._SET_CARGOBOB_PICKUP_MAGNET_ACTIVE, Handle, true);
			}
		}
		public void CargoBobMagnetReleaseVehicle()
		{
			if (IsCargobobHookActive(CargobobHook.Magnet))
			{
				Function.Call(Hash._SET_CARGOBOB_PICKUP_MAGNET_ACTIVE, Handle, false);
			}
		}

		public bool HasTowArm
		{
			get
			{
				return HasBone("tow_arm");
			}
		}
		public float TowingCraneRaisedAmount
		{
			set
			{
				Function.Call(Hash._SET_TOW_TRUCK_CRANE_HEIGHT, Handle, value);
			}
		}
		public Vehicle TowedVehicle
		{
			get
			{
				return new Vehicle(Function.Call<int>(Hash.GET_ENTITY_ATTACHED_TO_TOW_TRUCK, Handle));
			}
		}
		public void TowVehicle(Vehicle vehicle, bool rear)
		{
			Function.Call(Hash.ATTACH_VEHICLE_TO_TOW_TRUCK, Handle, vehicle.Handle, rear, 0f, 0f, 0f);
		}
		public void DetachFromTowTruck()
		{
			Function.Call(Hash.DETACH_VEHICLE_FROM_ANY_TOW_TRUCK, Handle);
		}
		public void DetachTowedVehicle()
		{
			Vehicle vehicle = TowedVehicle;

			if (Exists(vehicle))
			{
				Function.Call(Hash.DETACH_VEHICLE_FROM_TOW_TRUCK, Handle, vehicle.Handle);
			}
		}

		public void ApplyDamage(Vector3 position, float damageAmount, float radius)
		{
			Function.Call(Hash.SET_VEHICLE_DAMAGE, position.X, position.Y, position.Z, damageAmount, radius);
		}

		public Ped CreatePedOnSeat(VehicleSeat seat, Model model)
		{
			if (!IsSeatFree(seat))
			{
				throw new ArgumentException("The VehicleSeat selected was not free", "seat");
			}
			if (!model.IsPed || !model.Request(1000))
			{
				return null;
			}

			return new Ped(Function.Call<int>(Hash.CREATE_PED_INSIDE_VEHICLE, Handle, 26, model.Hash, seat, 1, 1));
		}
		public Ped CreateRandomPedOnSeat(VehicleSeat seat)
		{
			if (!IsSeatFree(seat))
			{
				throw new ArgumentException("The VehicleSeat selected was not free", "seat");
			}
			if (seat == VehicleSeat.Driver)
			{
				return new Ped(Function.Call<int>(Hash.CREATE_RANDOM_PED_AS_DRIVER, Handle, true));
			}
			else
			{
				int pedHandle = Function.Call<int>(Hash.CREATE_RANDOM_PED, 0f, 0f, 0f);
				Function.Call(Hash.SET_PED_INTO_VEHICLE, pedHandle, Handle, seat);

				return new Ped(pedHandle);
			}
		}

		public static string GetModelDisplayName(Model vehicleModel)
		{
			return Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, vehicleModel.Hash);
		}

		public static VehicleClass GetModelClass(Model vehicleModel)
		{
			return Function.Call<VehicleClass>(Hash.GET_VEHICLE_CLASS_FROM_NAME, vehicleModel.Hash);
		}

		public static string GetClassDisplayName(VehicleClass vehicleClass)
		{
			return "VEH_CLASS_" + ((int)vehicleClass).ToString();
		}

		public static VehicleHash[] GetAllModelsOfClass(VehicleClass vehicleClass)
		{
			return Array.ConvertAll<int, VehicleHash>(MemoryAccess.VehicleModels[(int) vehicleClass].ToArray(), item => (VehicleHash)item);
		}

		public static VehicleHash[] GetAllModels()
		{
			List<VehicleHash> allModels = new List<VehicleHash>();
			for (int i = 0; i < 0x20; i++)
			{
				allModels.AddRange(Array.ConvertAll<int, VehicleHash>(MemoryAccess.VehicleModels[i].ToArray(), item => (VehicleHash)item));
			}
			return allModels.ToArray();
		}

	}
}
