using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using GTA;
using GTA.Native;
using CherryMPShared;
using Quaternion = GTA.Math.Quaternion;
using Vector3 = GTA.Math.Vector3;
using GTA.Math;

namespace CherryMP.Util
{
    public enum HandleType
    {
        GameHandle,
        LocalHandle,
        NetHandle,
    }

    public struct LocalHandle
    {
        public LocalHandle(int handle)
        {
            _internalId = handle;
            HandleType = HandleType.GameHandle;
        }

        public LocalHandle(int handle, HandleType localId)
        {
            _internalId = handle;
            HandleType = localId;
        }

        private int _internalId;

        public int Raw
        {
            get
            {
                return _internalId;
            }
        }

        public int Value
        {
            get
            {
                if (HandleType == HandleType.LocalHandle)
                {
                    return Main.NetEntityHandler.NetToEntity(Main.NetEntityHandler.NetToStreamedItem(_internalId, true))?.Handle ?? 0;
                }
                else if (HandleType == HandleType.NetHandle)
                {
                    return Main.NetEntityHandler.NetToEntity(_internalId)?.Handle ?? 0;
                }

                return _internalId;
            }
        }

        public T Properties<T>()
        {
            if (HandleType == HandleType.LocalHandle)
                return (T) Main.NetEntityHandler.NetToStreamedItem(_internalId, true);
            else if (HandleType == HandleType.NetHandle)
                return (T) Main.NetEntityHandler.NetToStreamedItem(_internalId);
            else
                return (T) Main.NetEntityHandler.EntityToStreamedItem(_internalId);
        }

        public HandleType HandleType;

        public override bool Equals(object obj)
        {
            return (obj as LocalHandle?)?.Value == Value;
        }

        public static bool operator ==(LocalHandle left, LocalHandle right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(LocalHandle left, LocalHandle right)
        {
            return left.Value != right.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public bool IsNull
        {
            get
            {
                return Value == 0;
            }
        }
    }

    public static class Util
    {

        public static T Clamp<T>(T min, T value, T max) where T : IComparable
        {
            if (value.CompareTo(min) < 0) return min;
            if (value.CompareTo(max) > 0) return max;

            return value;
        }

        public static Point Floor(this PointF point)
        {
            return new Point((int)point.X, (int) point.Y);
        }

        public static bool ModelRequest;
        public static void LoadModel(Model model)
        {
            if (!model.IsValid) return;

            LogManager.DebugLog("REQUESTING MODEL " + model.Hash);
            ModelRequest = true;
            DateTime start = DateTime.Now;
            while (!model.IsLoaded)
            {
                model.Request();
                //Function.Call(Hash.REQUEST_COLLISION_FOR_MODEL, model.Hash);
                Script.Yield();

                if (DateTime.Now.Subtract(start).TotalMilliseconds > 1000) break;
            }
            ModelRequest = false;
            LogManager.DebugLog("MODEL REQUESTED: " + model.IsLoaded);
        }

        public static long TickCount
        {
            get { return DateTime.Now.Ticks / 10000; }
        }

        public static int BuildTyreFlag(Vehicle veh)
        {
            byte tyreFlag = 0;

            for (int i = 0; i < 8; i++)
            {
                if (veh.IsTireBurst(i))
                    tyreFlag |= (byte)(1 << i);
            }

            return tyreFlag;
        }

        public static bool[] BuildTyreArray(Vehicle veh)
        {
            var flag = BuildTyreFlag(veh);
            bool[] arr = new bool[8];

            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = (flag & (1 << i)) != 0;
            }

            return arr;
        }

        public static float Unlerp(double left, double center, double right)
        {
            return (float)((center - left) / (right - left));
        }

        // Dirty & dangerous
        public static dynamic Lerp(dynamic from, float fAlpha, dynamic to)
        {
            return ((to - from)*fAlpha + from);
        }

        public static int GetStationId()
        {
            if (!Game.Player.Character.IsInVehicle()) return -1;
            return Function.Call<int>(Hash.GET_PLAYER_RADIO_STATION_INDEX);
        }

	    public static IEnumerable<Blip> GetAllBlips()
	    {
		    for(int i = 0; i < 600; i++)
		    {
			    int Handle = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, i);
			    while (Function.Call<bool>(Hash.DOES_BLIP_EXIST, Handle))
			    {
				    yield return new Blip(Handle);
					Handle = Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID, i);
			    }
		    }
		}

        public static float Denormalize(this float h)
        {
            return h < 0f ? h + 360f : h;
        }

        public static dynamic Lerp(dynamic from, dynamic to, float fAlpha)
        {
            return ((to - from) * fAlpha + from);
        }

        public static void SafeNotify(string msg)
        {
            if (!string.IsNullOrWhiteSpace(msg))
            {
                try
                {
                    GTA.UI.Screen.ShowNotification(msg);
                }
                catch (Exception) { }
            }
        }

        public static string GetStationName(int id)
        {
            return Function.Call<string>(Hash.GET_RADIO_STATION_NAME, id);
        }

        public static void WriteMemory(IntPtr pointer, byte value, int length)
        {
            for (int i = 0; i < length; i++)
            {
                MemoryAccess.WriteByte(pointer + i, value);
            }
        }

        public static unsafe IntPtr FindPattern(string bytes, string mask)
        {
            var patternPtr = Marshal.StringToHGlobalAnsi(bytes);
            var maskPtr = Marshal.StringToHGlobalAnsi(bytes);

            IntPtr output = IntPtr.Zero;

            try
            {
                output =
                    new IntPtr(
                        unchecked(
                            (long)
                                MemoryAccess.FindPattern(
                                    (sbyte*) (patternPtr.ToPointer()),
                                    (sbyte*) (patternPtr.ToPointer())
                                    )));
            }
            finally
            {
                Marshal.FreeHGlobal(patternPtr);
                Marshal.FreeHGlobal(maskPtr);
            }

            return output;
        }

        private static int _idX;
        private static int _lastframe;
        public static void DxDrawTexture(int idx, string filename, float xPos, float yPos, float txdWidth, float txdHeight, float rot, int r, int g, int b, int a, bool centered = false)
        {
            int screenw = GTA.UI.Screen.Resolution.Width;
            int screenh = GTA.UI.Screen.Resolution.Height;

            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            float width = height * ratio;

            float reduceX = xPos / width;
            float reduceY = yPos / height;

            float scaleX = txdWidth/width;
            float scaleY = txdHeight/height;

            if (!centered)
            {
                reduceX += scaleX*0.5f;
                reduceY += scaleY*0.5f;
            }

            var cF = Function.Call<int>(Hash.GET_FRAME_COUNT);

            if (cF != _lastframe)
            {
                _idX = 0;
                _lastframe = cF;
            }
            
            GTA.UI.CustomSprite.RawDraw(filename, 70,
                new PointF(reduceX, reduceY),
                new SizeF(scaleX, scaleY / ratio),
                new PointF(0.5f, 0.5f),
                rot, Color.FromArgb(a, r, g, b));
        }

        public static void DrawSprite(string dict, string txtName, double x, double y, double width, double height, double heading,
            int r, int g, int b, int alpha)
        {
            if (!Main.UIVisible || Main.MainMenu.Visible) return;
            if (!Function.Call<bool>(Hash.HAS_STREAMED_TEXTURE_DICT_LOADED, dict))
                Function.Call(Hash.REQUEST_STREAMED_TEXTURE_DICT, dict, true);

            int screenw = GTA.UI.Screen.Resolution.Width;
            int screenh = GTA.UI.Screen.Resolution.Height;
            const float hh = 1080f;
            float ratio = (float)screenw / screenh;
            var ww = hh * ratio;


            float w = (float)(width / ww);
            float h = (float)(height / hh);
            float xx = (float)(x / ww) + w * 0.5f;
            float yy = (float)(y / hh) + h * 0.5f;

            Function.Call(Hash.DRAW_SPRITE, dict, txtName, xx, yy, w, h, heading, r, g, b, alpha);
        }

        public static void DrawRectangle(double xPos, double yPos, double wSize, double hSize, int r, int g, int b, int alpha)
        {
            if (!Main.UIVisible || Main.MainMenu.Visible) return;
            int screenw = GTA.UI.Screen.Resolution.Width;
            int screenh = GTA.UI.Screen.Resolution.Height;
            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            var width = height * ratio;

            float w = (float)wSize / width;
            float h = (float)hSize / height;
            float x = (((float)xPos) / width) + w * 0.5f;
            float y = (((float)yPos) / height) + h * 0.5f;

            Function.Call(Hash.DRAW_RECT, x, y, w, h, r, g, b, alpha);
        }

        public static void DrawText(string caption, double xPos, double yPos, double scale, int r, int g, int b, int alpha, int font,
            int justify, bool shadow, bool outline, int wordWrap)
        {
            if (!Main.UIVisible || Main.MainMenu.Visible) return;
            int screenw = GTA.UI.Screen.Resolution.Width;
            int screenh = GTA.UI.Screen.Resolution.Height;
            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            var width = height * ratio;

            float x = (float)(xPos) / width;
            float y = (float)(yPos) / height;

            Function.Call(Hash.SET_TEXT_FONT, font);
            Function.Call(Hash.SET_TEXT_SCALE, 1.0f, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, r, g, b, alpha);
            if (shadow)
                Function.Call(Hash.SET_TEXT_DROP_SHADOW);
            if (outline)
                Function.Call(Hash.SET_TEXT_OUTLINE);
            switch (justify)
            {
                case 1:
                    Function.Call(Hash.SET_TEXT_CENTRE, true);
                    break;
                case 2:
                    Function.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, true);
                    Function.Call(Hash.SET_TEXT_WRAP, 0, x);
                    break;
            }

            if (wordWrap != 0)
            {
                float xsize = (float)(xPos + wordWrap) / width;
                Function.Call(Hash.SET_TEXT_WRAP, x, xsize);
            }

            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "CELL_EMAIL_BCON");

            const int maxStringLength = 99;

            for (int i = 0; i < caption.Length; i += maxStringLength)
            {
                Function.Call((Hash)0x6C188BE134E074AA,
                    caption.Substring(i,
                            System.Math.Min(maxStringLength, caption.Length - i)));
                //Function.Call((Hash)0x6C188BE134E074AA, caption.Substring(i, System.Math.Min(maxStringLength, caption.Length - i)));
            }

            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y);
        }

        public static float GetOffsetDegrees(float a, float b)
        {
            float c = (b > a) ? b - a : 0 - (a - b);
            if (c > 180f)
                c = 0 - (360 - c);
            else if (c <= -180)
                c = 360 + c;
            return c;
        }

        public static Vector3 ToEuler(this Quaternion q)
        {
            var pitchYawRoll = new Vector3();

            double sqw = q.W * q.W;
            double sqx = q.X * q.X;
            double sqy = q.Y * q.Y;
            double sqz = q.Z * q.Z;

            pitchYawRoll.Y = (float)Math.Atan2(2f * q.X * q.W + 2f * q.Y * q.Z, 1 - 2f * (sqz + sqw));     // Yaw 
            pitchYawRoll.X = (float)Math.Asin(2f * (q.X * q.Z - q.W * q.Y));                             // Pitch 
            pitchYawRoll.Z = (float)Math.Atan2(2f * q.X * q.Y + 2f * q.Z * q.W, 1 - 2f * (sqy + sqz));

            return pitchYawRoll;
        }

        public static int FromArgb(byte a, byte r, byte g, byte b)
        {
            return b | g << 8 | r << 16 | a << 24;
        }

        public static void ToArgb(int argb, out byte a, out byte r, out byte g, out byte b)
        {
            b = (byte)(argb & 0xFF);
            g = (byte)((argb & 0xFF00) >> 8);
            r = (byte)((argb & 0xFF0000) >> 16);
            a = (byte)((argb & 0xFF000000) >> 24);
        }

        public static int GetTrackId()
        {
            if (!Game.Player.Character.IsInVehicle()) return -1;
            return Function.Call<int>(Hash.GET_AUDIBLE_MUSIC_TRACK_TEXT_ID);
        }

        public static string LoadDict(string dict)
        {
            LogManager.DebugLog("REQUESTING DICTIONARY " + dict);
            Function.Call(Hash.REQUEST_ANIM_DICT, dict);

            DateTime endtime = DateTime.UtcNow + new TimeSpan(0, 0, 0, 0, 1000);

            while (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dict))
            {
                LogManager.DebugLog("DICTIONARY HAS NOT BEEN LOADED. YIELDING...");
                Script.Yield();
                Function.Call(Hash.REQUEST_ANIM_DICT, dict);
                if (DateTime.UtcNow >= endtime)
                {
                    break;
                }
            }

            LogManager.DebugLog("DICTIONARY LOAD COMPLETE.");

            return dict;
        }

        public static string LoadPtfxAsset(string dict)
        {
            LogManager.DebugLog("REQUESTING PTFX " + dict);
            Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, dict);

            DateTime endtime = DateTime.UtcNow + new TimeSpan(0, 0, 0, 0, 5000);

            bool wasLoading = false;

            while (!Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, dict))
            {
                wasLoading = true;
                LogManager.DebugLog("DICTIONARY HAS NOT BEEN LOADED. YIELDING...");
                Script.Yield();
                Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, dict);
                if (DateTime.UtcNow >= endtime)
                {
                    break;
                }
            }

            //if (wasLoading) Script.Wait(100);

            LogManager.DebugLog("DICTIONARY LOAD COMPLETE.");

            return dict;
        }

        public static string LoadAnimDictStreamer(string dict)
        {
            LogManager.DebugLog("REQUESTING DICTIONARY " + dict);
            Function.Call(Hash.REQUEST_ANIM_DICT, dict);

            DateTime endtime = DateTime.UtcNow + new TimeSpan(0, 0, 0, 0, 1000);

            ModelRequest = true;

            while (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dict))
            {
                LogManager.DebugLog("DICTIONARY HAS NOT BEEN LOADED. YIELDING...");
                Script.Yield();
                Function.Call(Hash.REQUEST_ANIM_DICT, dict);
                if (DateTime.UtcNow >= endtime)
                {
                    break;
                }
            }

            ModelRequest = false;

            LogManager.DebugLog("DICTIONARY LOAD COMPLETE.");

            return dict;
        }

        public static Vector3 LinearVectorLerp(Vector3 start, Vector3 end, long currentTime, long duration)
        {
            return new Vector3()
            {
                X = LinearFloatLerp(start.X, end.X, currentTime, duration),
                Y = LinearFloatLerp(start.Y, end.Y, currentTime, duration),
                Z = LinearFloatLerp(start.Z, end.Z, currentTime, duration),
            };
        }

        public static float LinearFloatLerp(float start, float end, long currentTime, long duration)
        {
            float change = end - start;
            return change * currentTime / duration + start;
        }
        
        public static PlayerSettings ReadSettings(string path)
        {
            var ser = new XmlSerializer(typeof(PlayerSettings));

            PlayerSettings settings = null;

            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path)) settings = (PlayerSettings)ser.Deserialize(stream);

                if (string.IsNullOrWhiteSpace(settings.DisplayName))
                {
                    settings.DisplayName = string.IsNullOrWhiteSpace(GTA.Game.Player.Name) ? "Player" : GTA.Game.Player.Name;
                }

                if (settings.DisplayName.Length > 32)
                {
                    settings.DisplayName = settings.DisplayName.Substring(0, 32);
                }

                settings.DisplayName = settings.DisplayName.Replace(' ', '_');

                using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, settings);
            }
            else
            {
                using (var stream = File.OpenWrite(path))
                {
                    ser.Serialize(stream, settings = new PlayerSettings());
                }
            }

            return settings;
        }

        public static void SaveSettings(string path)
        {
            var ser = new XmlSerializer(typeof(PlayerSettings));
            using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Truncate : FileMode.Create, FileAccess.ReadWrite)) ser.Serialize(stream, Main.PlayerSettings);
        }

        public static Vector3 GetLastWeaponImpact(Ped ped)
        {
            var coord = new OutputArgument();
            if (!Function.Call<bool>(Hash.GET_PED_LAST_WEAPON_IMPACT_COORD, ped.Handle, coord))
            {
                return new Vector3();
            }
            return coord.GetResult<Vector3>();
        }

        public static Quaternion LerpQuaternion(Quaternion start, Quaternion end, float speed)
        {
            return new Quaternion()
            {
                X = start.X + (end.X - start.X) * speed,
                Y = start.Y + (end.Y - start.Y) * speed,
                Z = start.Z + (end.Z - start.Z) * speed,
                W = start.W + (end.W - start.W) * speed,
            };
        }

        public static Vector3 LerpVector(Vector3 start, Vector3 end, float speed)
        {
            return new Vector3()
            {
                X = start.X + (end.X - start.X) * speed,
                Y = start.Y + (end.Y - start.Y) * speed,
                Z = start.Z + (end.Z - start.Z) * speed,
            };
        }

        public static Vector3 QuaternionToEuler(Quaternion quat)
        {
            //heading = atan2(2*qy*qw-2*qx*qz , 1 - 2*qy2 - 2*qz2) (yaw)
            //attitude = asin(2 * qx * qy + 2 * qz * qw) (pitch)
            //bank = atan2(2 * qx * qw - 2 * qy * qz, 1 - 2 * qx2 - 2 * qz2) (roll)

            return new Vector3()
            {
                X = (float)Math.Asin(2 * quat.X * quat.Y + 2 *quat.Z * quat.W),
                Y = (float)Math.Atan2(2 * quat.X * quat.W - 2 * quat.Y * quat.Z, 1 -  2 * quat.X*quat.X - 2 * quat.Z * quat.Z),
                Z = (float)Math.Atan2(2*quat.Y*quat.W - 2*quat.X*quat.Z, 1 - 2*quat.Y*quat.Y - 2*quat.Z * quat.Z),
            };

            /*except when qx*qy + qz*qw = 0.5 (north pole)
            which gives:
            heading = 2 * atan2(x,w)
            bank = 0

            and when qx*qy + qz*qw = -0.5 (south pole)
            which gives:
            heading = -2 * atan2(x,w)
            bank = 0 */
        }
    }
}