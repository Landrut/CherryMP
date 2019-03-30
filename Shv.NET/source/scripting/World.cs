using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;

namespace GTA
{
	class GTACalender : System.Globalization.GregorianCalendar
	{
		public override int GetDaysInYear(int year, int era)
		{
			return 31 * 12;
		}
		public override int GetDaysInMonth(int year, int month, int era)
		{
			return 31;
		}
	}
	enum ZoneID
	{
		AIRP,
		ALAMO,
		ALTA,
		ARMYB,
		BANHAMC,
		BANNING,
		BEACH,
		BHAMCA,
		BRADP,
		BRADT,
		BURTON,
		CALAFB,
		CANNY,
		CCREAK,
		CHAMH,
		CHIL,
		CHU,
		CMSW,
		CYPRE,
		DAVIS,
		DELBE,
		DELPE,
		DELSOL,
		DESRT,
		DOWNT,
		DTVINE,
		EAST_V,
		EBURO,
		ELGORL,
		ELYSIAN,
		GALFISH,
		golf,
		GRAPES,
		GREATC,
		HARMO,
		HAWICK,
		HORS,
		HUMLAB,
		JAIL,
		KOREAT,
		LACT,
		LAGO,
		LDAM,
		LEGSQU,
		LMESA,
		LOSPUER,
		MIRR,
		MORN,
		MOVIE,
		MTCHIL,
		MTGORDO,
		MTJOSE,
		MURRI,
		NCHU,
		NOOSE,
		OCEANA,
		PALCOV,
		PALETO,
		PALFOR,
		PALHIGH,
		PALMPOW,
		PBLUFF,
		PBOX,
		PROCOB,
		RANCHO,
		RGLEN,
		RICHM,
		ROCKF,
		RTRAK,
		SanAnd,
		SANCHIA,
		SANDY,
		SKID,
		SLAB,
		STAD,
		STRAW,
		TATAMO,
		TERMINA,
		TEXTI,
		TONGVAH,
		TONGVAV,
		VCANA,
		VESP,
		VINE,
		WINDF,
		WVINE,
		ZANCUDO,
		ZP_ORT,
		ZQ_UAR
	}

	public enum Weather
	{
		Unknown = -1,
		ExtraSunny,
		Clear,
		Clouds,
		Smog,
		Foggy,
		Overcast,
		Raining,
		ThunderStorm,
		Clearing,
		Neutral,
		Snowing,
		Blizzard,
		Snowlight,
		Christmas
	}
	public enum IntersectOptions
	{
		Everything = -1,
		Map = 1,
		MissionEntities,
		Peds1 = 12,
		Objects = 16,
		Unk1 = 32,
		Unk2 = 64,
		Unk3 = 128,
		Vegetation = 256,
		Unk4 = 512
	}
	public enum MarkerType
	{
		UpsideDownCone,
		VerticalCylinder,
		ThickChevronUp,
		ThinChevronUp,
		CheckeredFlagRect,
		CheckeredFlagCircle,
		VerticleCircle,
		PlaneModel,
		LostMCDark,
		LostMCLight,
		Number0,
		Number1,
		Number2,
		Number3,
		Number4,
		Number5,
		Number6,
		Number7,
		Number8,
		Number9,
		ChevronUpx1,
		ChevronUpx2,
		ChevronUpx3,
		HorizontalCircleFat,
		ReplayIcon,
		HorizontalCircleSkinny,
		HorizontalCircleSkinnyArrow,
		HorizontalSplitArrowCircle,
		DebugSphere
	}
	public enum ExplosionType
	{
		Grenade,
		GrenadeL,
		StickyBomb,
		Molotov1,
		Rocket,
		TankShell,
		HiOctane,
		Car,
		Plane,
		PetrolPump,
		Bike,
		Steam,
		Flame,
		WaterHydrant,
		GasCanister,
		Boat,
		ShipDestroy,
		Truck,
		Bullet,
		SmokeGL,
		SmokeG,
		BZGas,
		Flare,
		GasCanister2,
		Extinguisher,
		ProgramAR,
		Train,
		Barrel,
		Propane,
		Blimp,
		FlameExplode,
		Tanker,
		PlaneRocket,
		VehicleBullet,
		GasTank,
		FireWork,
		SnowBall,
		ProxMine,
		Valkyrie
	}

	public static class World
	{
		#region Fields
		internal static readonly string[] _weatherNames = {
			"EXTRASUNNY",
			"CLEAR",
			"CLOUDS",
			"SMOG",
			"FOGGY",
			"OVERCAST",
			"RAIN",
			"THUNDER",
			"CLEARING",
			"NEUTRAL",
			"SNOW",
			"BLIZZARD",
			"SNOWLIGHT",
			"XMAS"
		};
		#endregion

		/// <summary>
		/// Gets or sets the current date and time in the GTA World.
		/// </summary>
		/// <value>
		/// The current date and time.
		/// </value>
		public static DateTime CurrentDate
		{
			get
			{
				int year = Function.Call<int>(Hash.GET_CLOCK_YEAR);
				int month = Function.Call<int>(Hash.GET_CLOCK_MONTH);
				int day = Function.Call<int>(Hash.GET_CLOCK_DAY_OF_MONTH);
				int hour = Function.Call<int>(Hash.GET_CLOCK_HOURS);
				int minute = Function.Call<int>(Hash.GET_CLOCK_MINUTES);
				int second = Function.Call<int>(Hash.GET_CLOCK_SECONDS);

				return new DateTime(year, month, day, hour, minute, second, new GTACalender());
			}
			set
			{
				Function.Call(Hash.SET_CLOCK_DATE, value.Year, value.Month, value.Day);
				Function.Call(Hash.SET_CLOCK_TIME, value.Hour, value.Minute, value.Second);
			}
		}
		/// <summary>
		/// Gets or sets the current time of day in the GTA World.
		/// </summary>
		/// <value>
		/// The current time of day
		/// </value>
		public static TimeSpan CurrentDayTime
		{
			get
			{
				int hours = Function.Call<int>(Hash.GET_CLOCK_HOURS);
				int minutes = Function.Call<int>(Hash.GET_CLOCK_MINUTES);
				int seconds = Function.Call<int>(Hash.GET_CLOCK_SECONDS);

				return new TimeSpan(hours, minutes, seconds);
			}
			set
			{
				Function.Call(Hash.SET_CLOCK_TIME, value.Hours, value.Minutes, value.Seconds);
			}
		}

		/// <summary>
		/// Sets a value indicating whether lights in the <see cref="World"/> should be rendered.
		/// </summary>
		/// <value>
		///   <c>true</c> if blackout; otherwise, <c>false</c>.
		/// </value>
		public static bool Blackout
		{
			set
			{
				Function.Call(Hash.SET_ARTIFICIAL_LIGHTS_STATE, value);
			}
		}
		/// <summary>
		/// Gets or sets the weather.
		/// </summary>
		/// <value>
		/// The weather.
		/// </value>
		public static Weather Weather
		{
			get
			{
				for (int i = 0; i < _weatherNames.Length; i++)
				{
					if (Function.Call<int>(Hash.GET_PREV_WEATHER_TYPE_HASH_NAME) == Game.GenerateHash(_weatherNames[i]))
					{
						return (Weather)i;
					}
				}

				return Weather.Unknown;
			}
			set
			{
				if (Enum.IsDefined(typeof(Weather), value) && value != Weather.Unknown)
				{
					Function.Call(Hash.SET_WEATHER_TYPE_NOW, _weatherNames[(int)value]);
				}
			}
		}
		/// <summary>
		/// Gets or sets the next weather.
		/// </summary>
		/// <value>
		/// The next weather.
		/// </value>
		public static Weather NextWeather
		{
			get
			{
				for (int i = 0; i < _weatherNames.Length; i++)
				{
					if (Function.Call<bool>(Hash.IS_NEXT_WEATHER_TYPE, _weatherNames[i]))
					{
						return (Weather)i;
					}
				}

				return Weather.Unknown;
			}
			set
			{
				if (Enum.IsDefined(typeof(Weather), value) && value != Weather.Unknown)
				{
					var currentWeatherHash = new OutputArgument();
					var nextWeatherHash = new OutputArgument();
					var weatherTransition = new OutputArgument();
					Function.Call(Hash._GET_WEATHER_TYPE_TRANSITION, currentWeatherHash, nextWeatherHash, weatherTransition);
					Function.Call(Hash._SET_WEATHER_TYPE_TRANSITION, currentWeatherHash.GetResult<int>(), Game.GenerateHash(_weatherNames[(int)value]), 0.0f);
				}
			}
		}
		/// <summary>
		/// Gets or sets the weather transition.
		/// </summary>
		/// <value>
		/// The weather transition.
		/// </value>
		public static float WeatherTransition
		{
			get
			{
				var currentWeatherHash = new OutputArgument();
				var nextWeatherHash = new OutputArgument();
				var weatherTransition = new OutputArgument();
				Function.Call(Hash._GET_WEATHER_TYPE_TRANSITION, currentWeatherHash, nextWeatherHash, weatherTransition);

				return weatherTransition.GetResult<float>();
			}
			set
			{
				Function.Call(Hash._SET_WEATHER_TYPE_TRANSITION, 0, 0, value);
			}
		}
		/// <summary>
		/// Transitions to weather.
		/// </summary>
		/// <param name="weather">The weather.</param>
		/// <param name="duration">The duration.</param>
		public static void TransitionToWeather(Weather weather, float duration)
		{
			if (Enum.IsDefined(typeof(Weather), weather) && weather != Weather.Unknown)
			{
				Function.Call(Hash._SET_WEATHER_TYPE_OVER_TIME, _weatherNames[(int)weather], duration);
			}
		}

		/// <summary>
		/// Sets the gravity level for all <see cref="World"/> objects.
		/// </summary>
		/// <value>
		/// The gravity level
		/// 9.8f - Default gravity
		/// 2.4f - Moon gravity
		/// 0.1f - Very low gravity
		/// 0.0f - No gravity 
		/// </value>
		public static float GravityLevel
		{
			get { return MemoryAccess.ReadWorldGravity(); }
			set
			{
				//write the value you want to the first item in the array where the native reads the gravity level choices from
				MemoryAccess.WriteWorldGravity(value);
				//call set_gravity_level normally using 0 as gravity type
				//the native will then set the gravity level to what we just wrote
				Function.Call(Hash.SET_GRAVITY_LEVEL, 0);
				//reset the array item back to 9.8 so as to restore behaviour of the native
				MemoryAccess.WriteWorldGravity(9.800000f);
			}
		}

		/// <summary>
		/// Gets or sets the rendering camera.
		/// </summary>
		/// <value>
		/// The rendering <see cref="Camera"/>.
		/// </value>
		/// <remarks>
		/// Setting to <see langword="null"/> sets the rendering <see cref="Camera"/> to <see cref="GameplayCamera"/>
		/// </remarks>
		public static Camera RenderingCamera
		{
			get
			{
				return new Camera(Function.Call<int>(Hash.GET_RENDERING_CAM));
			}
			set
			{
				if (value == null)
				{
					Function.Call(Hash.RENDER_SCRIPT_CAMS, false, 0, 3000, 1, 0);
				}
				else
				{
					value.IsActive = true;
					Function.Call(Hash.RENDER_SCRIPT_CAMS, true, 0, 3000, 1, 0);
				}
			}
		}
		/// <summary>
		/// Destroys all user created <see cref="Camera"/>s.
		/// </summary>
		public static void DestroyAllCameras()
		{
			Function.Call(Hash.DESTROY_ALL_CAMS, 0);
		}

		/// <summary>
		/// Gets or sets the waypoint position.
		/// </summary>
		/// <returns>The <see cref="Vector3"/> coordinates of the Waypoint <see cref="Blip"/></returns>
		/// <remarks>
		/// Returns an empty <see cref="Vector3"/> if a waypoint <see cref="Blip"/> hasn't been set
		/// If the game engine cant extract height information the Z component will be 0.0f
		/// </remarks>
		public static Vector3 WaypointPosition
		{
			get
			{
				Blip waypointBlip = GetWaypointBlip();

				if (waypointBlip == null)
				{
					return Vector3.Zero;
				}

				Vector3 position = waypointBlip.Position;
				var heightResult = new OutputArgument();

				if (Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, position.X, position.Y, 1000f, heightResult))
				{
					position.Z = heightResult.GetResult<float>();
					return position;
				}

				return Vector3.Zero;
			}
			set
			{
				Function.Call(Hash.SET_NEW_WAYPOINT, value.X, value.Y);
			}
		}

		/// <summary>
		/// Gets the waypoint blip.
		/// </summary>
		/// <returns>The <see cref="Vector3"/> coordinates of the Waypoint <see cref="Blip"/></returns>
		/// <remarks>
		/// Returns <see langword="null"/> if a waypoint <see cref="Blip"/> hasn't been set
		/// </remarks>
		public static Blip GetWaypointBlip()
		{
			if (!Game.IsWaypointActive)
			{
				return null;
			}

			for (int it = Function.Call<int>(Hash._GET_BLIP_INFO_ID_ITERATOR), blip = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, it); Function.Call<bool>(Hash.DOES_BLIP_EXIST, blip); blip = Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID, it))
			{
				if (Function.Call<int>(Hash.GET_BLIP_INFO_ID_TYPE, blip) == 4)
				{
					return new Blip(blip);
				}
			}

			return null;
		}

		/// <summary>
		/// Removes the waypoint.
		/// </summary>
		public static void RemoveWaypoint()
		{
			Function.Call(Hash.SET_WAYPOINT_OFF);
		}

		/// <summary>
		/// Gets the straight line distance between 2 positions.
		/// </summary>
		/// <param name="origin">The origin.</param>
		/// <param name="destination">The destination.</param>
		/// <returns>The distance</returns>
		public static float GetDistance(Vector3 origin, Vector3 destination)
		{
			return Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS, origin.X, origin.Y, origin.Z, destination.X, destination.Y, destination.Z, 1);
		}
		/// <summary>
		/// Calculates the travel distance using roads and paths between 2 positions.
		/// </summary>
		/// <param name="origin">The origin.</param>
		/// <param name="destination">The destination.</param>
		/// <returns>The travel distance</returns>
		public static float CalculateTravelDistance(Vector3 origin, Vector3 destination)
		{
			return Function.Call<float>(Hash.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS, origin.X, origin.Y, origin.Z, destination.X, destination.Y, destination.Z);
		}
		/// <summary>
		/// Gets the height of the ground at a given position.
		/// </summary>
		/// <param name="position">The position.</param>
		/// <returns>The height measured in meters</returns>
		public static float GetGroundHeight(Vector3 position)
		{
			return GetGroundHeight(new Vector2(position.X, position.Y));
		}
		/// <summary>
		/// Gets the height of the ground at a given position.
		/// </summary>
		/// <param name="position">The position.</param>
		/// <returns>The height measured in meters</returns>
		public static float GetGroundHeight(Vector2 position)
		{
			var resultArg = new OutputArgument();

			Function.Call(Hash.GET_GROUND_Z_FOR_3D_COORD, position.X, position.Y, 1000f, resultArg);

			return resultArg.GetResult<float>();
		}

        public static float GetWaterHeight(Vector3 position)
	    {
	        var resultArg = new OutputArgument();

	        Function.Call(Hash.GET_WATER_HEIGHT, position.X, position.Y, position.Z, resultArg);

	        return resultArg.GetResult<float>();
	    }

        /// <summary>
        /// Gets an <see langword="array"/> of all the <see cref="Blip"/>s on the Map
        /// </summary>
        /// <returns><see cref="Blip"/>[] of all the blips</returns>
        public static Blip[] GetAllBlips()
		{
			var res = new List<Blip>();

			foreach (BlipSprite sprite in Enum.GetValues(typeof(BlipSprite)))
			{
				int handle = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, sprite);

				while (Function.Call<bool>(Hash.DOES_BLIP_EXIST, handle))
				{
					res.Add(new Blip(handle));

					handle = Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID, sprite);
				}
			}

			return res.ToArray();
		}

		public static Blip[] GetAllBlipsOfType(params BlipSprite[] blipTypes)
		{
			var res = new List<Blip>();
			foreach (BlipSprite sprite in blipTypes)
			{
				int handle = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, sprite);

				while (Function.Call<bool>(Hash.DOES_BLIP_EXIST, handle))
				{
					res.Add(new Blip(handle));

					handle = Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID, sprite);
				}
			}
			return res.ToArray();
		}

		public static Checkpoint[] GetActiveCheckpoints()
		{					   
			return Array.ConvertAll<int, Checkpoint>(MemoryAccess.GetCheckpointHandles(), element => new Checkpoint(element));
		}

		static int[] ModelListToHashList(Model[] models)
		{
			return Array.ConvertAll<Model, int>(models, model => model.Hash);
		}

		public static Ped[] GetAllPeds(params Model[] models)
		{
			return Array.ConvertAll<int, Ped>(MemoryAccess.GetPedHandles(ModelListToHashList(models)), handle => new Ped(handle));
		}
		public static Ped[] GetNearbyPeds(Vector3 position, float radius, params Model[] models)
		{
			return Array.ConvertAll<int, Ped>(MemoryAccess.GetPedHandles(position, radius, ModelListToHashList(models)), handle => new Ped(handle));
		}
		public static Ped[] GetNearbyPeds(Ped ped, float radius, params Model[] models)
		{
			int[] handles = MemoryAccess.GetPedHandles(ped.Position, radius, ModelListToHashList(models));

			var result = new List<Ped>();

			foreach (int handle in handles)
			{
				if (handle == ped.Handle)
				{
					continue;
				}

				result.Add(new Ped(handle));
			}

			return result.ToArray();
		}
		public static Ped GetClosestPed(Vector3 position, float radius, params Model[] models)
		{
			Ped[] peds =
				Array.ConvertAll<int, Ped>(MemoryAccess.GetPedHandles(position, radius, ModelListToHashList(models)),
					handle => new Ped(handle));
			return GetClosest<Ped>(position, peds);
		}

		public static Vehicle[] GetAllVehicles(params Model[] models)
		{
			return Array.ConvertAll<int, Vehicle>(MemoryAccess.GetVehicleHandles(ModelListToHashList(models)), handle => new Vehicle(handle));
		}
		public static Vehicle[] GetNearbyVehicles(Vector3 position, float radius, params Model[] models)
		{
			return Array.ConvertAll<int, Vehicle>(MemoryAccess.GetVehicleHandles(position, radius, ModelListToHashList(models)), handle => new Vehicle(handle));
		}
		public static Vehicle[] GetNearbyVehicles(Ped ped, float radius, params Model[] models)
		{
			int[] handles = MemoryAccess.GetVehicleHandles(ped.Position, radius, ModelListToHashList(models));

			var result = new List<Vehicle>();
			Vehicle ignore = ped.CurrentVehicle;
			int ignoreHandle = Vehicle.Exists(ignore) ? ignore.Handle : 0;

			foreach (int handle in handles)
			{
				if (handle == ignoreHandle)
				{
					continue;
				}

				result.Add(new Vehicle(handle));
			}

			return result.ToArray();
		}
		public static Vehicle GetClosestVehicle(Vector3 position, float radius, params Model[] models)
		{
			Vehicle[] vehicles = 
				Array.ConvertAll<int, Vehicle>(MemoryAccess.GetVehicleHandles(position, radius, ModelListToHashList(models)),
					handle => new Vehicle(handle));
			return GetClosest<Vehicle>(position, vehicles);

		}

		public static Prop[] GetAllProps(params Model[] models)
		{						
			return Array.ConvertAll<int, Prop>(MemoryAccess.GetPropHandles(ModelListToHashList(models)), handle => new Prop(handle));
		}
		public static Prop[] GetNearbyProps(Vector3 position, float radius, params Model[] models)
		{
			return Array.ConvertAll<int, Prop>(MemoryAccess.GetPropHandles(position, radius, ModelListToHashList(models)), handle => new Prop(handle));
		}

		public static Entity[] GetAllEntities()
		{									   
			return Array.ConvertAll<int, Entity>(MemoryAccess.GetEntityHandles(), Entity.FromHandle);
		}
		public static Entity[] GetNearbyEntities(Vector3 position, float radius)
		{
			return Array.ConvertAll<int, Entity>(MemoryAccess.GetEntityHandles(position, radius), Entity.FromHandle);
		}

		public static T GetClosest<T>(Vector3 position, params T[] spatials) where T : ISpatial
		{
			ISpatial closest = null;
			float closestDistance = 3e38f;

			foreach (var spatial in spatials)
			{
				float distance = Vector3.Subtract(spatial.Position, position).LengthSquared();

				if (distance <= closestDistance)
				{
					closest = spatial;
					closestDistance = distance;
				}
			}
			return (T)closest;
		}

		public static Vector3 GetSafeCoordForPed(Vector3 position, bool sidewalk = true, int flags = 0)
		{
			var outPos = new OutputArgument();

			if (Function.Call<bool>(Hash.GET_SAFE_COORD_FOR_PED, position.X, position.Y, position.Z, sidewalk, outPos, flags))
			{
				return outPos.GetResult<Vector3>();
			}

			return Vector3.Zero;
		}

		public static Vector3 GetNextPositionOnStreet(Vector2 position, bool unoccupied = false)
		{
			return GetNextPositionOnStreet(new Vector3(position.X, position.Y, 0f), unoccupied);
		}
		public static Vector3 GetNextPositionOnStreet(Vector3 position, bool unoccupied = false)
		{
			var outPos = new OutputArgument();

			if (unoccupied)
			{
				for (int i = 1; i < 40; i++)
				{
					Function.Call(Hash.GET_NTH_CLOSEST_VEHICLE_NODE, position.X, position.Y, position.Z, i, outPos, 1, 0x40400000, 0);

					position = outPos.GetResult<Vector3>();

					if (!Function.Call<bool>(Hash.IS_POINT_OBSCURED_BY_A_MISSION_ENTITY, position.X, position.Y, position.Z, 5.0f, 5.0f, 5.0f, 0))
					{
						return position;
					}
				}
			}
			else if (Function.Call<bool>(Hash.GET_NTH_CLOSEST_VEHICLE_NODE, position.X, position.Y, position.Z, 1, outPos, 1, 0x40400000, 0))
			{
				return outPos.GetResult<Vector3>();		
			}

			return Vector3.Zero;
		}
		public static Vector3 GetNextPositionOnSidewalk(Vector2 position)
		{
			return GetNextPositionOnSidewalk(new Vector3(position.X, position.Y, 0f));
		}
		public static Vector3 GetNextPositionOnSidewalk(Vector3 position)
		{
			var outPos = new OutputArgument();

			if (Function.Call<bool>(Hash.GET_SAFE_COORD_FOR_PED, position.X, position.Y, position.Z, true, outPos, 0))
			{
				return outPos.GetResult<Vector3>();
			}
			else if (Function.Call<bool>(Hash.GET_SAFE_COORD_FOR_PED, position.X, position.Y, position.Z, false, outPos, 0))
			{
				return outPos.GetResult<Vector3>();
			}

			return Vector3.Zero;
		}

		public static string GetZoneName(Vector2 position)
		{
			return GetZoneName(new Vector3(position.X, position.Y, 0f));
		}
		public static string GetZoneName(Vector3 position)
		{
			ZoneID id;

			if (Enum.TryParse(GetZoneNameLabel(position), out id))
			{
				switch (id)
				{
					case ZoneID.AIRP:
						return "Los Santos International Airport";
					case ZoneID.ALAMO:
						return "Alamo Sea";
					case ZoneID.ALTA:
						return "Alta";
					case ZoneID.ARMYB:
						return "Fort Zancudo";
					case ZoneID.BANHAMC:
						return "Banham Canyon";
					case ZoneID.BANNING:
						return "Banning";
					case ZoneID.BEACH:
						return "Vespucci Beach";
					case ZoneID.BHAMCA:
						return "Banham Canyon";
					case ZoneID.BRADP:
						return "Braddock Pass";
					case ZoneID.BRADT:
						return "Braddock Tunnel";
					case ZoneID.BURTON:
						return "Burton";
					case ZoneID.CALAFB:
						return "Calafia Bridge";
					case ZoneID.CANNY:
						return "Raton Canyon";
					case ZoneID.CCREAK:
						return "Cassidy Creek";
					case ZoneID.CHAMH:
						return "Chamberlain Hills";
					case ZoneID.CHIL:
						return "Vinewood Hills";
					case ZoneID.CHU:
						return "Chumash";
					case ZoneID.CMSW:
						return "Chiliad Mountain State Wilderness";
					case ZoneID.CYPRE:
						return "Cypress Flats";
					case ZoneID.DAVIS:
						return "Davis";
					case ZoneID.DELBE:
						return "Del Perro Beach";
					case ZoneID.DELPE:
						return "Del Perro";
					case ZoneID.DELSOL:
						return "Puerto Del Sol";
					case ZoneID.DESRT:
						return "Grand Senora Desert";
					case ZoneID.DOWNT:
						return "Downtown";
					case ZoneID.DTVINE:
						return "Downtown Vinewood";
					case ZoneID.EAST_V:
						return "East Vinewood";
					case ZoneID.EBURO:
						return "El Burro Heights";
					case ZoneID.ELGORL:
						return "El Gordo Lighthouse";
					case ZoneID.ELYSIAN:
						return "Elysian Island";
					case ZoneID.GALFISH:
						return "Galilee";
					case ZoneID.golf:
						return "GWC and Golfing Society";
					case ZoneID.GRAPES:
						return "Grapeseed";
					case ZoneID.GREATC:
						return "Great Chaparral";
					case ZoneID.HARMO:
						return "Harmony";
					case ZoneID.HAWICK:
						return "Hawick";
					case ZoneID.HORS:
						return "Vinewood Racetrack";
					case ZoneID.HUMLAB:
						return "Humane Labs and Research";
					case ZoneID.JAIL:
						return "Bolingbroke Penitentiary";
					case ZoneID.KOREAT:
						return "Little Seoul";
					case ZoneID.LACT:
						return "Land Act Reservoir";
					case ZoneID.LAGO:
						return "Lago Zancudo";
					case ZoneID.LDAM:
						return "Land Act Dam";
					case ZoneID.LEGSQU:
						return "Legion Square";
					case ZoneID.LMESA:
						return "La Mesa";
					case ZoneID.LOSPUER:
						return "La Puerta";
					case ZoneID.MIRR:
						return "Mirror Park";
					case ZoneID.MORN:
						return "Morningwood";
					case ZoneID.MOVIE:
						return "Richards Majestic";
					case ZoneID.MTCHIL:
						return "Mount Chiliad";
					case ZoneID.MTGORDO:
						return "Mount Gordo";
					case ZoneID.MTJOSE:
						return "Mount Josiah";
					case ZoneID.MURRI:
						return "Murrieta Heights";
					case ZoneID.NCHU:
						return "North Chumash";
					case ZoneID.NOOSE:
						return "N.O.O.S.E.";
					case ZoneID.OCEANA:
						return "Pacific Ocean";
					case ZoneID.PALCOV:
						return "Paleto Cove";
					case ZoneID.PALETO:
						return "Paleto Bay";
					case ZoneID.PALFOR:
						return "Paleto Forest";
					case ZoneID.PALHIGH:
						return "Palomino Highlands";
					case ZoneID.PALMPOW:
						return "Palmer-Taylor Power Station";
					case ZoneID.PBLUFF:
						return "Pacific Bluffs";
					case ZoneID.PBOX:
						return "Pillbox Hill";
					case ZoneID.PROCOB:
						return "Procopio Beach";
					case ZoneID.RANCHO:
						return "Rancho";
					case ZoneID.RGLEN:
						return "Richman Glen";
					case ZoneID.RICHM:
						return "Richman";
					case ZoneID.ROCKF:
						return "Rockford Hills";
					case ZoneID.RTRAK:
						return "Redwood Lights Track";
					case ZoneID.SanAnd:
						return "San Andreas";
					case ZoneID.SANCHIA:
						return "San Chianski Mountain Range";
					case ZoneID.SANDY:
						return "Sandy Shores";
					case ZoneID.SKID:
						return "Mission Row";
					case ZoneID.SLAB:
						return "Stab City";
					case ZoneID.STAD:
						return "Maze Bank Arena";
					case ZoneID.STRAW:
						return "Strawberry";
					case ZoneID.TATAMO:
						return "Tataviam Mountains";
					case ZoneID.TERMINA:
						return "Terminal";
					case ZoneID.TEXTI:
						return "Textile City";
					case ZoneID.TONGVAH:
						return "Tongva Hills";
					case ZoneID.TONGVAV:
						return "Tongva Valley";
					case ZoneID.VCANA:
						return "Vespucci Canals";
					case ZoneID.VESP:
						return "Vespucci";
					case ZoneID.VINE:
						return "Vinewood";
					case ZoneID.WINDF:
						return "RON Alternates Wind Farm";
					case ZoneID.WVINE:
						return "West Vinewood";
					case ZoneID.ZANCUDO:
						return "Zancudo River";
					case ZoneID.ZP_ORT:
						return "Port of South Los Santos";
					case ZoneID.ZQ_UAR:
						return "Davis Quartz";
				}
			}

			return string.Empty;
		}
		public static string GetZoneNameLabel(Vector2 position)
		{
			return GetZoneNameLabel(new Vector3(position.X, position.Y, 0f));
		}
		public static string GetZoneNameLabel(Vector3 position)
		{
			return Function.Call<string>(Hash.GET_NAME_OF_ZONE, position.X, position.Y, position.Z);
		}
		public static string GetStreetName(Vector2 position)
		{
			return GetStreetName(new Vector3(position.X, position.Y, 0f));
		}
		public static string GetStreetName(Vector3 position)
		{
			var streetHash = new OutputArgument();
			var crossingHash = new OutputArgument();
			Function.Call(Hash.GET_STREET_NAME_AT_COORD, position.X, position.Y, position.Z, streetHash, crossingHash);

			return Function.Call<string>(Hash.GET_STREET_NAME_FROM_HASH_KEY, streetHash.GetResult<int>());
		}

		public static Blip CreateBlip(Vector3 position)
		{
			return new Blip(Function.Call<int>(Hash.ADD_BLIP_FOR_COORD, position.X, position.Y, position.Z));
		}
		public static Blip CreateBlip(Vector3 position, float radius)
		{
			return new Blip(Function.Call<int>(Hash.ADD_BLIP_FOR_RADIUS, position.X, position.Y, position.Z, radius));
		}

		public static Camera CreateCamera(Vector3 position, Vector3 rotation, float fov)
		{
			return new Camera(Function.Call<int>(Hash.CREATE_CAM_WITH_PARAMS, "DEFAULT_SCRIPTED_CAMERA", position.X, position.Y, position.Z, rotation.X, rotation.Y, rotation.Z, fov, 1, 2));
		}

		public static Ped CreatePed(Model model, Vector3 position, float heading = 0f, int type = 26)
		{
			if (!model.Request(1000))
			{
				return null;
			}

			return new Ped(Function.Call<int>(Hash.CREATE_PED, type, model.Hash, position.X, position.Y, position.Z, heading, false, false));
		}
		public static Ped CreateRandomPed(Vector3 position)
		{
			return new Ped(Function.Call<int>(Hash.CREATE_RANDOM_PED, position.X, position.Y, position.Z));
		}

		public static Vehicle CreateVehicle(Model model, Vector3 position, float heading = 0f)
		{
			if (!model.Request(1000))
			{
				return null;
			}

			return new Vehicle(Function.Call<int>(Hash.CREATE_VEHICLE, model.Hash, position.X, position.Y, position.Z, heading, false, false));
		}

		public static Vehicle CreateRandomVehicle(Vector3 position, float heading = 0f)
		{
			OutputArgument outModel = new OutputArgument(), outInt = new OutputArgument();
			Function.Call(Hash.GET_RANDOM_VEHICLE_MODEL_IN_MEMORY, 1, outModel, outInt);
			Model model = outModel.GetResult<Model>();
			if (model.IsVehicle && model.IsLoaded)
			{
				return
					new Vehicle(Function.Call<int>(Hash.CREATE_VEHICLE, model.Hash, position.X, position.Y, position.Z, heading, false, false));
			}
			return null;
		}

		public static Prop CreateProp(Model model, Vector3 position, bool dynamic, bool placeOnGround)
		{
			if (!model.Request(1000))
			{
				return null;
			}

			if (placeOnGround)
			{
				position.Z = GetGroundHeight(position);
			}

			return new Prop(Function.Call<int>(Hash.CREATE_OBJECT, model.Hash, position.X, position.Y, position.Z, 1, 1, dynamic));
		}
		public static Prop CreateProp(Model model, Vector3 position, Vector3 rotation, bool dynamic, bool placeOnGround)
		{
			Prop prop = CreateProp(model, position, dynamic, placeOnGround);

			if (prop != null)
			{
				prop.Rotation = rotation;
			}

			return prop;
		}

		public static Prop CreatePropNoOffset(Model model, Vector3 position, bool dynamic)
		{
			if (!model.Request(1000))
			{
				return null;
			}

			return new Prop(Function.Call<int>(Hash.CREATE_OBJECT_NO_OFFSET, model.Hash, position.X, position.Y, position.Z, 1, 1, dynamic));
		}

		public static Prop CreatePropNoOffset(Model model, Vector3 position, Vector3 rotation, bool dynamic)
		{
			Prop prop = CreatePropNoOffset(model, position, dynamic);

			if (prop != null)
			{
				prop.Rotation = rotation;
			}

			return prop;
		}

		public static Pickup CreatePickup(PickupType type, Vector3 position, Model model, int value)
		{
			if (!model.Request(1000))
			{
				return null;
			}

			int handle = Function.Call<int>(Hash.CREATE_PICKUP, type, position.X, position.Y, position.Z, 0, value, true, model.Hash);

			if (handle == 0)
			{
				return null;
			}

			return new Pickup(handle);
		}
		public static Pickup CreatePickup(PickupType type, Vector3 position, Vector3 rotation, Model model, int value)
		{
			if (!model.Request(1000))
			{
				return null;
			}

			int handle = Function.Call<int>(Hash.CREATE_PICKUP_ROTATE, type, position.X, position.Y, position.Z, rotation.X, rotation.Y, rotation.Z, 0, value, 2, true, model.Hash);

			if (handle == 0)
			{
				return null;
			}

			return new Pickup(handle);
		}
		public static Prop CreateAmbientPickup(PickupType type, Vector3 position, Model model, int value)
		{
			if (!model.Request(1000))
			{
				return null;
			}

			int handle = Function.Call<int>(Hash.CREATE_AMBIENT_PICKUP, type, position.X, position.Y, position.Z, 0, value, model.Hash, false, true);

			if (handle == 0)
			{
				return null;
			}

			return new Prop(handle);
		}

		public static Checkpoint CreateCheckpoint(CheckpointIcon icon, Vector3 position, Vector3 pointTo, float radius, System.Drawing.Color color, byte reserved = 0)
		{
			int handle = Function.Call<int>(Hash.CREATE_CHECKPOINT, icon, position.X, position.Y, position.Z, pointTo.X, pointTo.Y, pointTo.Z, radius, color.R, color.G, color.B, color.A, reserved);

			if (handle == 0)
			{
				return null;
			}

			return new Checkpoint(handle);
		}

		public static Rope AddRope(RopeType type, Vector3 position, Vector3 rotation, float length, float minLength, bool breakable)
		{
			Function.Call(Hash.ROPE_LOAD_TEXTURES);

			return new Rope(Function.Call<int>(Hash.ADD_ROPE, position.X, position.Y, position.Z, rotation.X, rotation.Y, rotation.Z, length, type, length, minLength, 0.5f, false, false, true, 1.0f, breakable, 0));
		}


		public static void ShootBullet(Vector3 sourcePosition, Vector3 targetPosition, Ped owner, Model model, int damage, float speed = -1f)
		{
			Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, sourcePosition.X, sourcePosition.Y, sourcePosition.Z, targetPosition.X, targetPosition.Y, targetPosition.Z, damage, 1, model.Hash, owner.Handle, 1, 0, speed);
		}

		public static void AddExplosion(Vector3 position, ExplosionType type, float radius, float cameraShake, bool aubidble = true, bool invisible = false)
		{
			Function.Call(Hash.ADD_EXPLOSION, position.X, position.Y, position.Z, type, radius, aubidble, invisible, cameraShake);
		}
		public static void AddOwnedExplosion(Ped ped, Vector3 position, ExplosionType type, float radius, float cameraShake, bool aubidble = true, bool invisible = false)
		{
			Function.Call(Hash.ADD_OWNED_EXPLOSION, ped.Handle, position.X, position.Y, position.Z, type, radius, aubidble, invisible, cameraShake);
		}

		public static RelationshipGroup AddRelationshipGroup(string name)
		{
			var resultArg = new OutputArgument();
			Function.Call(Hash.ADD_RELATIONSHIP_GROUP, name, resultArg);

			return new RelationshipGroup(resultArg.GetResult<int>());
		}

		public static RaycastResult Raycast(Vector3 source, Vector3 target, IntersectOptions options, Entity ignoreEntity = null)
		{
			return new RaycastResult(Function.Call<int>(Hash._START_SHAPE_TEST_RAY, source.X, source.Y, source.Z, target.X, target.Y, target.Z, options, ignoreEntity == null ? 0 : ignoreEntity.Handle, 7));
		}
		public static RaycastResult Raycast(Vector3 source, Vector3 direction, float maxDistance, IntersectOptions options, Entity ignoreEntity = null)
		{
			Vector3 target = source + direction * maxDistance;

			return new RaycastResult(Function.Call<int>(Hash._START_SHAPE_TEST_RAY, source.X, source.Y, source.Z, target.X, target.Y, target.Z, options, ignoreEntity == null ? 0 : ignoreEntity.Handle, 7));
		}
		public static RaycastResult RaycastCapsule(Vector3 source, Vector3 target, float radius, IntersectOptions options, Entity ignoreEntity = null)
		{
			return new RaycastResult(Function.Call<int>(Hash.START_SHAPE_TEST_CAPSULE, source.X, source.Y, source.Z, target.X, target.Y, target.Z, radius, options, ignoreEntity == null ? 0 : ignoreEntity.Handle, 7));
		}
		public static RaycastResult RaycastCapsule(Vector3 source, Vector3 direction, float maxDistance, float radius, IntersectOptions options, Entity ignoreEntity = null)
		{
			Vector3 target = source + direction * maxDistance;

			return new RaycastResult(Function.Call<int>(Hash.START_SHAPE_TEST_CAPSULE, source.X, source.Y, source.Z, target.X, target.Y, target.Z, radius, options, ignoreEntity == null ? 0 : ignoreEntity.Handle, 7));
		}
		public static RaycastResult GetCrosshairCoordinates()
		{
			return Raycast(GameplayCamera.Position, GameplayCamera.Direction, 1000f, IntersectOptions.Everything, null);
		}

		public static void DrawMarker(MarkerType type, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, Color color,
			bool bobUpAndDown = false, bool faceCamY = false, int unk2 = 2, bool rotateY = false, string textueDict = null, string textureName = null, bool drawOnEnt = false)
		{
			if (!string.IsNullOrEmpty(textueDict) && !string.IsNullOrEmpty(textureName))
			{
				Function.Call(Hash.DRAW_MARKER, type, pos.X, pos.Y, pos.Z, dir.X, dir.Y, dir.Z, rot.X, rot.Y, rot.Z, scale.X,
					scale.Y, scale.Z, color.R, color.G, color.B, color.A, bobUpAndDown, faceCamY, unk2, rotateY, textueDict,
					textureName, drawOnEnt);
			}
			else
			{
				Function.Call(Hash.DRAW_MARKER, type, pos.X, pos.Y, pos.Z, dir.X, dir.Y, dir.Z, rot.X, rot.Y, rot.Z, scale.X,
					scale.Y, scale.Z, color.R, color.G, color.B, color.A, bobUpAndDown, faceCamY, unk2, rotateY, 0, 0, drawOnEnt);
			}
		}

		public static void DrawLightWithRange(Vector3 position, Color color, float range, float intensity)
		{
			Function.Call(Hash.DRAW_LIGHT_WITH_RANGE, position.X, position.Y, position.Z, color.R, color.G, color.B, range,
				intensity);
		}

		public static void DrawSpotLight(Vector3 pos, Vector3 dir, Color color, float distance, float brightness,
			float roundness, float radius, float fadeout)
		{
			Function.Call(Hash.DRAW_SPOT_LIGHT, pos.X, pos.Y, pos.Z, dir.X, dir.Y, dir.Z, color.R, color.G, color.B, distance,
				brightness, roundness, radius, fadeout);
		}

		public static void DrawSpotLightWithShadow(Vector3 pos, Vector3 dir, Color color, float distance, float brightness,
			float roundness, float radius, float fadeout)
		{
			Function.Call(Hash._DRAW_SPOT_LIGHT_WITH_SHADOW, pos.X, pos.Y, pos.Z, dir.X, dir.Y, dir.Z, color.R, color.G, color.B,
				distance, brightness, roundness, radius, fadeout);
		}

		public static void DrawLine(Vector3 start, Vector3 end, Color color)
		{
			Function.Call(Hash.DRAW_LINE, start.X, start.Y, start.Z, end.X, end.Y, end.Z, color.R, color.G, color.B, color.A);
		}

		public static void DrawPoly(Vector3 vertexA, Vector3 vertexB, Vector3 vertexC, Color color)
		{
			Function.Call(Hash.DRAW_POLY, vertexA.X, vertexA.Y, vertexA.Z, vertexB.X, vertexB.Y, vertexB.Z, vertexC.X, vertexC.Y,
				vertexC.Z, color.R, color.G, color.B, color.A);
		}
	}
}
