#define ATTACHSERVER
//#define INTEGRITYCHECK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using CherryMP.GUI;
using CherryMP.Javascript;
using CherryMP.Misc;
using CherryMP.Networking;
using CherryMP.Util;
using CherryMPShared;
using Lidgren.Network;
using Microsoft.Win32;
using NativeUI;
using NativeUI.PauseMenu;
using Newtonsoft.Json;
using ProtoBuf;
using Control = GTA.Control;
using Vector3 = GTA.Math.Vector3;
using WeaponHash = GTA.WeaponHash;
using VehicleHash = GTA.VehicleHash;

namespace CherryMP
{
    internal class MessagePump : Script
    {
        public MessagePump()
        {
            Tick += (sender, args) =>
            {
                if (Main.Client != null)
                {
                    List<NetIncomingMessage> messages = new List<NetIncomingMessage>();
                    int msgsRead = Main.Client.ReadMessages(messages);
                    //LogManager.DebugLog("READING " + msgsRead + " MESSAGES"); disabled for performance gain
                    if (msgsRead > 0)
                        foreach (var message in messages)
                        {
                            if (CrossReference.EntryPoint.IsMessageTypeThreadsafe(message.MessageType))
                            {
                                var message1 = message;
                                var pcMsgThread = new Thread((ThreadStart)delegate
                                {
                                    CrossReference.EntryPoint.ProcessMessages(message1, false);
                                })
                                {
                                    IsBackground = true
                                };
                                pcMsgThread.Start();
                            }
                            else
                            {
                                CrossReference.EntryPoint.ProcessMessages(message, true);
                            }
                        }
                }
            };
        }
    }

    internal static class CrossReference
    {
        public static Main EntryPoint;
    }

    internal partial class Main : Script
    {
        public static PlayerSettings PlayerSettings;

        public static readonly ScriptVersion LocalScriptVersion = ScriptVersion.VERSION_0_9;


        public static bool BlockControls;
        public static bool Multithreading;
        public static bool HTTPFileServer;

        public static bool IsSpectating;
        private static Vector3 _preSpectatorPos;

        internal static Streamer NetEntityHandler;
        internal static CameraManager CameraManager;

        private readonly MenuPool _menuPool;

        private UIResText _versionLabel = new UIResText("Cherry-MP " + CurrentVersion.ToString() + " - REBORN DEV BRANCH", new Point(), 0.50f, Color.FromArgb(100, 200, 200, 200));

        private string _clientIp;
        public static IChat Chat;
        private static ClassicChat _backupChat;

        public static NetClient Client;
        private static NetPeerConfiguration _config;
        public static ParseableVersion CurrentVersion = ParseableVersion.FromAssembly(Assembly.GetExecutingAssembly());

        internal static SynchronizationMode GlobalSyncMode;
        public static bool LerpRotaion = true;
        public static bool VehicleLagCompensation = true;
        public static bool OnFootLagCompensation = true;

        public static bool OnShootingLagCompensation = true;

        //public static int GlobalStreamingRange = 750;
        //public static int PlayerStreamingRange = 200;
        //public static int VehicleStreamingRange = 350;
        public static bool RemoveGameEntities = true;
        public static bool ChatVisible = true;
        public static bool CanOpenChatbox = true;
        public static bool DisplayWastedMessage = true;
        public static bool ScriptChatVisible = true;
        public static bool UIVisible = true;
        public static byte TickCount = 0;
        public static Color UIColor = Color.White;

        public static StringCache StringCache;

        public static int LocalTeam = -1;
        public static int LocalDimension = 0;
        public int SpectatingEntity;

        private readonly Queue<Action> _threadJumping;
        private string _password;
        private string _QCpassword;
        private bool _lastDead;
        private bool _lastKilled;
        private bool _wasTyping;

        public static TabView MainMenu;

        private DebugWindow _debug;
        private SyncEventWatcher Watcher;
        internal static UnoccupiedVehicleSync VehicleSyncManager;
        internal WeaponManager WeaponInventoryManager;

        private Vector3 _vinewoodSign = new Vector3(827.74f, 1295.68f, 364.34f);

        // STATS
        public static int _bytesSent = 0;
        public static int _bytesReceived = 0;

        public static int _messagesSent = 0;
        public static int _messagesReceived = 0;

        public static List<int> _averagePacketSize = new List<int>();

        private TabTextItem _statsItem;

        private static bool EnableDevTool;
        internal static bool EnableMediaStream;
        internal static bool SaveDebugToFile = false;

        public static bool ToggleNametagDraw = false;
        public static bool TogglePosUpdate = false;
        public static bool SlowDownClientForDebug = false;

        public Main()
        {
            Process.GetProcesses().Where(x => x.ProcessName.ToLower().StartsWith("gameoverlay")).ToList().ForEach(x => x.Kill());

            World.DestroyAllCameras();

            CrossReference.EntryPoint = this;

            PlayerSettings = Util.Util.ReadSettings(GTANInstallDir + "\\settings.xml");

            CefUtil.DISABLE_CEF = PlayerSettings.DisableCEF;

            DebugInfo.FPS = PlayerSettings.ShowFPS;

            EnableMediaStream = PlayerSettings.MediaStream;
            EnableDevTool = PlayerSettings.CEFDevtool;

            GameSettings = Misc.GameSettings.LoadGameSettings();
            _threadJumping = new Queue<Action>();

            NetEntityHandler = new Streamer();
            CameraManager = new CameraManager();

            Watcher = new SyncEventWatcher(this);
            VehicleSyncManager = new UnoccupiedVehicleSync();
            WeaponInventoryManager = new WeaponManager();

            Npcs = new Dictionary<string, SyncPed>();
            _tickNatives = new Dictionary<string, NativeData>();
            _dcNatives = new Dictionary<string, NativeData>();

            EntityCleanup = new List<int>();
            BlipCleanup = new List<int>();

            _emptyVehicleMods = new Dictionary<int, int>();
            for (int i = 0; i < 50; i++) _emptyVehicleMods.Add(i, 0);

            Chat = new ClassicChat();
            Chat.OnComplete += ChatOnComplete;

            _backupChat = Chat as ClassicChat;

            Tick += OnTick;
            KeyDown += OnKeyDown;

            KeyUp += (sender, args) =>
            {
                if (args.KeyCode == Keys.Escape && _wasTyping)
                {
                    _wasTyping = false;
                }
            };

            _config = new NetPeerConfiguration("GRANDTHEFTAUTONETWORK")
            {
                Port = 8888
            };
            _config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            _config.ConnectionTimeout = 30f; // 30 second timeout


            #region Menu Set up
            _menuPool = new MenuPool();
            BuildMainMenu();
            #endregion

            _debug = new DebugWindow();

            Function.Call(Hash._USE_FREEMODE_MAP_BEHAVIOR, true); // _ENABLE_MP_DLC_MAPS
            Function.Call(Hash._LOAD_MP_DLC_MAPS); // _LOAD_MP_DLC_MAPS



            MainMenuCamera = World.CreateCamera(new Vector3(743.76f, 1070.7f, 350.24f), new Vector3(),
                GameplayCamera.FieldOfView);
            MainMenuCamera.PointAt(new Vector3(707.86f, 1228.09f, 333.66f));

            RelGroup = World.AddRelationshipGroup("SYNCPED");
            FriendRelGroup = World.AddRelationshipGroup("SYNCPED_TEAMMATES");

            RelGroup.SetRelationshipBetweenGroups(Game.Player.Character.RelationshipGroup, Relationship.Pedestrians, true);
            FriendRelGroup.SetRelationshipBetweenGroups(Game.Player.Character.RelationshipGroup, Relationship.Companion, true);

            //Function.Call(Hash.SHUTDOWN_LOADING_SCREEN);

            SocialClubName = Game.Player.Name;

            GetWelcomeMessage();

            var t = new Thread(UpdateSocialClubAvatar)
            {
                IsBackground = true
            };
            t.Start();

            CEFManager.InitializeCef();
            Audio.SetAudioFlag(AudioFlag.LoadMPData, true);
            Audio.SetAudioFlag(AudioFlag.DisableBarks, true);
            Audio.SetAudioFlag(AudioFlag.PoliceScannerDisabled, true);
            Audio.SetAudioFlag(AudioFlag.DisableFlightMusic, true);
            Function.Call((Hash)0x552369F549563AD5, false); //_FORCE_AMBIENT_SIREN



            GlobalVariable.Get(2576573).Write(1);

            ThreadPool.QueueUserWorkItem(delegate
            {
                NativeWhitelist.Init();
                SoundWhitelist.Init();
            });

        }

        public static void ChatOnComplete(object sender, EventArgs args)
        {
            var message = GUI.Chat.SanitizeString(Chat.CurrentInput);
            if (!string.IsNullOrWhiteSpace(message))
            {
                JavascriptHook.InvokeMessageEvent(message);

                var obj = new ChatData()
                {
                    Message = message,
                };
                var data = SerializeBinary(obj);

                var msg = Client.CreateMessage();
                msg.Write((byte)PacketType.ChatData);
                msg.Write(data.Length);
                msg.Write(data);
                Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.Chat);
            }

            Chat.IsFocused = false;
        }

        public static RelationshipGroup RelGroup;
        public static RelationshipGroup FriendRelGroup;
        public static bool HasFinishedDownloading;
        public static string SocialClubName;

        #region Debug stuff
        private bool display;
        private Ped mainPed;
        private Vehicle mainVehicle;

        private Vector3 oldplayerpos;
        private bool _lastJumping;
        private bool _lastShooting;
        private bool _lastAiming;
        private uint _switch;
        private bool _lastVehicle;
        private bool _oldChat;
        private bool _isGoingToCar;
        #endregion

        public static bool JustJoinedServer { get; set; }
        private int _currentOnlinePlayers;
        private int _currentOnlineServers;

        private Process _serverProcess;

        private int _currentServerPort;
        private string _currentServerIp;
        private bool _debugWindow;

        internal static Dictionary<string, SyncPed> Npcs;
        internal static float Latency;
        private int Port = 4499;

        private GameSettings.Settings GameSettings;

        private string CustomAnimation;
        private int AnimationFlag;

        public static Camera MainMenuCamera;

        public static string GTANInstallDir = ((string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V", "CherryMPInstallDir", null));

        // We want to check whether the player has the latest game version and the DLC content installed.
        public bool VerifyGameIntegrity()
        {
            bool legit = true;

            legit = legit && (Game.Version >= (GameVersion)27);

            CherryMPShared.VehicleHash[] dlcCars = new CherryMPShared.VehicleHash[]
            {
                CherryMPShared.VehicleHash.Trophytruck,CherryMPShared.VehicleHash.Cliffhanger,
                CherryMPShared.VehicleHash.Lynx,CherryMPShared.VehicleHash.Contender,
                CherryMPShared.VehicleHash.Gargoyle,CherryMPShared.VehicleHash.Sheava,
                CherryMPShared.VehicleHash.Brioso,CherryMPShared.VehicleHash.Tropos,
                CherryMPShared.VehicleHash.Tyrus,CherryMPShared.VehicleHash.Rallytruck,
                CherryMPShared.VehicleHash.le7b,CherryMPShared.VehicleHash.Tampa2,
                CherryMPShared.VehicleHash.Omnis,CherryMPShared.VehicleHash.Trophytruck2,
                CherryMPShared.VehicleHash.Avarus,CherryMPShared.VehicleHash.Blazer4,
                CherryMPShared.VehicleHash.Chimera,CherryMPShared.VehicleHash.Daemon2,
                CherryMPShared.VehicleHash.Defiler,CherryMPShared.VehicleHash.Esskey,
                CherryMPShared.VehicleHash.Faggio,CherryMPShared.VehicleHash.Faggio3,
                CherryMPShared.VehicleHash.Hakuchou2,CherryMPShared.VehicleHash.Manchez,
                CherryMPShared.VehicleHash.Nightblade,CherryMPShared.VehicleHash.Raptor,
                CherryMPShared.VehicleHash.Ratbike,CherryMPShared.VehicleHash.Sanctus,
                CherryMPShared.VehicleHash.Shotaro,CherryMPShared.VehicleHash.Tornado6,
                CherryMPShared.VehicleHash.Vortex,CherryMPShared.VehicleHash.Wolfsbane,
                CherryMPShared.VehicleHash.Youga2,CherryMPShared.VehicleHash.Zombiea,
                CherryMPShared.VehicleHash.Zombieb, CherryMPShared.VehicleHash.Voltic2,
                CherryMPShared.VehicleHash.Ruiner2, CherryMPShared.VehicleHash.Dune4,
                CherryMPShared.VehicleHash.Dune5, CherryMPShared.VehicleHash.Phantom2,
                CherryMPShared.VehicleHash.Technical2, CherryMPShared.VehicleHash.Boxville5,
                CherryMPShared.VehicleHash.Blazer5,
                CherryMPShared.VehicleHash.Comet3, CherryMPShared.VehicleHash.Diablous,
                CherryMPShared.VehicleHash.Diablous2, CherryMPShared.VehicleHash.Elegy,
                CherryMPShared.VehicleHash.Fcr, CherryMPShared.VehicleHash.Fcr2,
                CherryMPShared.VehicleHash.Italigtb, CherryMPShared.VehicleHash.Italigtb2,
                CherryMPShared.VehicleHash.Nero, CherryMPShared.VehicleHash.Nero2,
                CherryMPShared.VehicleHash.Penetrator, CherryMPShared.VehicleHash.Specter,
                CherryMPShared.VehicleHash.Specter2, CherryMPShared.VehicleHash.Tempesta
            };


            return dlcCars.Aggregate(legit, (current, car) => current && new Model((int)car).IsValid);
        }

        private void TickSpinner()
        {
            OnTick(null, EventArgs.Empty);
        }

        private void SaveSettings()
        {
            Util.Util.SaveSettings(GTANInstallDir + "\\settings.xml");
        }

        public static IEnumerable<ProcessModule> GetModules()
        {
            var modules = Process.GetCurrentProcess().Modules;

            for (int i = 0; i < modules.Count; i++)
                yield return modules[i];
        }

        private static Dictionary<int, int> _emptyVehicleMods;
        private Dictionary<string, NativeData> _tickNatives;
        private Dictionary<string, NativeData> _dcNatives;

        public static List<int> EntityCleanup;
        public static List<int> BlipCleanup;
        public static Dictionary<int, MarkerProperties> _localMarkers = new Dictionary<int, MarkerProperties>();

        private int _markerCount;

        private static int _modSwitch = 0;
        private static int _pedSwitch = 0;
        private static Dictionary<int, int> _vehMods = new Dictionary<int, int>();
        private static Dictionary<int, int> _pedClothes = new Dictionary<int, int>();


        public static string Weather { get; set; }
        public static TimeSpan? Time { get; set; }

        public static void AddMap(ServerMap map)
        {
            //File.WriteAllText(GTANInstallDir + "\\logs\\map.json", JsonConvert.SerializeObject(map));
            Ped PlayerChar = Game.Player.Character;
            try
            {
                NetEntityHandler.ServerWorld = map.World;

                if (map.World.LoadedIpl != null)
                    foreach (var ipl in map.World.LoadedIpl)
                    {
                        Function.Call(Hash.REQUEST_IPL, ipl);
                    }

                if (map.World.RemovedIpl != null)
                    foreach (var ipl in map.World.RemovedIpl)
                    {
                        Function.Call(Hash.REMOVE_IPL, ipl);
                    }

                if (map.Objects != null)
                    foreach (var pair in map.Objects)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(pair.Key)) continue;
                        NetEntityHandler.CreateObject(pair.Key, pair.Value);
                        //GTA.UI.Screen.ShowSubtitle("Creating object...", 500000);
                    }

                if (map.Vehicles != null)
                    foreach (var pair in map.Vehicles)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(pair.Key)) continue;
                        NetEntityHandler.CreateVehicle(pair.Key, pair.Value);
                        //GTA.UI.Screen.ShowSubtitle("Creating vehicle...", 500000);
                    }

                if (map.Blips != null)
                {
                    foreach (var blip in map.Blips)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(blip.Key)) continue;
                        NetEntityHandler.CreateBlip(blip.Key, blip.Value);
                    }
                }

                if (map.Markers != null)
                {
                    foreach (var marker in map.Markers)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(marker.Key)) continue;
                        NetEntityHandler.CreateMarker(marker.Key, marker.Value);
                    }
                }

                if (map.Pickups != null)
                {
                    foreach (var pickup in map.Pickups)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(pickup.Key)) continue;
                        NetEntityHandler.CreatePickup(pickup.Key, pickup.Value);
                    }
                }

                if (map.TextLabels != null)
                {
                    //map.TextLabels.GroupBy(x => x.Key).Select(y => y.First()); //Remove duplicates before procceeding

                    foreach (var label in map.TextLabels)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(label.Key)) continue;
                        NetEntityHandler.CreateTextLabel(label.Key, label.Value);
                    }
                }

                if (map.Peds != null)
                {
                    foreach (var ped in map.Peds)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(ped.Key)) continue;
                        NetEntityHandler.CreatePed(ped.Key, ped.Value);
                    }
                }

                if (map.Particles != null)
                {
                    foreach (var ped in map.Particles)
                    {
                        if (NetEntityHandler.ClientMap.ContainsKey(ped.Key)) continue;
                        NetEntityHandler.CreateParticle(ped.Key, ped.Value);
                    }
                }

                if (map.Players != null)
                {
                    LogManager.DebugLog("STARTING PLAYER MAP");

                    foreach (var pair in map.Players)
                    {
                        if (NetEntityHandler.NetToEntity(pair.Key)?.Handle == PlayerChar.Handle)
                        {
                            // It's us!
                            var remPl = NetEntityHandler.NetToStreamedItem(pair.Key) as RemotePlayer;
                            remPl.Name = pair.Value.Name;
                        }
                        else
                        {
                            var ourSyncPed = NetEntityHandler.GetPlayer(pair.Key);
                            NetEntityHandler.UpdatePlayer(pair.Key, pair.Value);
                            if (ourSyncPed.Character != null)
                            {
                                ourSyncPed.Character.RelationshipGroup = (pair.Value.Team == LocalTeam &&
                                                                            pair.Value.Team != -1)
                                    ? Main.FriendRelGroup
                                    : Main.RelGroup;

                                for (int i = 0; i < 15; i++) //NEEDS A CHECK
                                {
                                    Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ourSyncPed.Character, i,
                                        pair.Value.Props.Get((byte)i),
                                        pair.Value.Textures.Get((byte)i), 2);
                                }

                                lock (NetEntityHandler.HandleMap)
                                    NetEntityHandler.HandleMap.Set(pair.Key, ourSyncPed.Character.Handle);

                                ourSyncPed.Character.Opacity = pair.Value.Alpha;
                                /*
                                if (ourSyncPed.Character.AttachedBlip != null)
                                {
                                    ourSyncPed.Character.AttachedBlip.Sprite = (BlipSprite)pair.Value.BlipSprite;
                                    ourSyncPed.Character.AttachedBlip.Color = (BlipColor)pair.Value.BlipColor;
                                    ourSyncPed.Character.AttachedBlip.Alpha = pair.Value.BlipAlpha;
                                }
                                */
                                NetEntityHandler.ReattachAllEntities(ourSyncPed, false);
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                GTA.UI.Screen.ShowNotification("FATAL ERROR WHEN PARSING MAP");
                GTA.UI.Screen.ShowNotification(ex.Message);
                Client.Disconnect("Map Parse Error");

                LogManager.LogException(ex, "MAP PARSE");

                return;
            }

            World.CurrentDayTime = new TimeSpan(map.World.Hours, map.World.Minutes, 00);

            Time = new TimeSpan(map.World.Hours, map.World.Minutes, 00);
            if (map.World.Weather >= 0 && map.World.Weather < _weather.Length)
            {
                Weather = _weather[map.World.Weather];
                Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, _weather[map.World.Weather]);
            }

            Function.Call(Hash.PAUSE_CLOCK, true);
        }

        public static void StartClientsideScripts(ScriptCollection scripts)
        {
            if (scripts.ClientsideScripts == null) return;
            JavascriptHook.StartScripts(scripts);
        }

        public static Dictionary<int, int> CheckPlayerVehicleMods()
        {
            if (!Game.Player.Character.IsInVehicle()) return null;

            if (_modSwitch % 30 == 0)
            {
                var id = _modSwitch / 30;
                var mod = Game.Player.Character.CurrentVehicle.Mods[(VehicleModType)id].Index;
                if (mod != -1)
                {
                    lock (_vehMods)
                    {
                        if (!_vehMods.ContainsKey(id)) _vehMods.Add(id, mod);

                        _vehMods[id] = mod;
                    }
                }
            }

            _modSwitch++;

            if (_modSwitch >= 1500) _modSwitch = 0;

            return _vehMods;
        }

        public static Dictionary<int, int> CheckPlayerProps()
        {
            if (_pedSwitch % 30 == 0)
            {
                var id = _pedSwitch / 30;
                var mod = Function.Call<int>(Hash.GET_PED_DRAWABLE_VARIATION, Game.Player.Character.Handle, id);
                if (mod != -1)
                {
                    lock (_pedClothes)
                    {
                        if (!_pedClothes.ContainsKey(id)) _pedClothes.Add(id, mod);

                        _pedClothes[id] = mod;
                    }
                }
            }

            _pedSwitch++;

            if (_pedSwitch >= 450) _pedSwitch = 0;

            return _pedClothes;
        }

        public const NetDeliveryMethod SYNC_MESSAGE_TYPE = NetDeliveryMethod.UnreliableSequenced; // unreliable_sequenced
        private static bool _sendData = true;

        private static bool _lastPedData;
        private static int _lastLightSync;
        private static int LIGHT_SYNC_RATE = 1500;

        /*
        public static void SendPlayerData()
        {
            if (IsSpectating || !_sendData ) return; //|| !HasFinishedDownloading
            var player = Game.Player.Character;
            
            if (player.IsInVehicle())
            {
                var veh = player.CurrentVehicle;
                
                var horn = Game.Player.IsPressingHorn;
                var siren = veh.SirenActive;
                var vehdead = veh.IsDead;

                var obj = new VehicleData();
                obj.Position = veh.Position.ToLVector();
                obj.VehicleHandle = NetEntityHandler.EntityToNet(player.CurrentVehicle.Handle);
                obj.Quaternion = veh.Rotation.ToLVector();
                obj.PedModelHash = player.Model.Hash;
                obj.PlayerHealth = (byte)(100 * ((player.Health < 0 ? 0 : player.Health) / (float)player.MaxHealth));
                obj.VehicleHealth = veh.EngineHealth;
                obj.Velocity = veh.Velocity.ToLVector();
                obj.PedArmor = (byte)player.Armor;
                obj.RPM = veh.CurrentRPM;
                obj.VehicleSeat = (short)Util.GetPedSeat(player); 
                obj.Flag = 0;
	            obj.Steering = veh.SteeringAngle;

                if (horn)
                    obj.Flag |= (byte) VehicleDataFlags.PressingHorn;
                if (siren)
                    obj.Flag |= (byte)VehicleDataFlags.SirenActive;
                if (vehdead)
                    obj.Flag |= (byte)VehicleDataFlags.VehicleDead;

                if (Util.GetResponsiblePed(veh).Handle == player.Handle)
                    obj.Flag |= (byte) VehicleDataFlags.Driver;


                if (!WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.GetPedSeat(Game.Player.Character)) && WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash)veh.Model.Hash))
                {
                    obj.Flag |= (byte) VehicleDataFlags.MountedWeapon;
                    obj.AimCoords = new CherryMPShared.Vector3(0, 0, 0);
                    obj.WeaponHash = GetCurrentVehicleWeaponHash(Game.Player.Character);
                    if (Game.IsEnabledControlPressed(0, Control.VehicleFlyAttack))
                        obj.Flag |= (byte) VehicleDataFlags.Shooting;
                }
                else if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.GetPedSeat(Game.Player.Character)))
                {
                    obj.Flag |= (byte)VehicleDataFlags.MountedWeapon;

                    obj.AimCoords = RaycastEverything(new Vector2(0, 0)).ToLVector();
                    if (Game.IsEnabledControlPressed(0, Control.VehicleAttack))
                        obj.Flag |= (byte) VehicleDataFlags.Shooting;
                }
                else
                {
                    if (player.IsSubtaskActive(200) && 
                        Game.IsEnabledControlPressed(0, Control.Attack) &&
                        Game.Player.Character.Weapons.Current?.AmmoInClip != 0)
                        obj.Flag |= (byte) VehicleDataFlags.Shooting;
                    if (player.IsSubtaskActive(200) && // or 290
                        Game.Player.Character.Weapons.Current?.AmmoInClip != 0)
                        obj.Flag |= (byte)VehicleDataFlags.Aiming;
                    //obj.IsShooting = Game.Player.Character.IsShooting;
                    obj.AimCoords = RaycastEverything(new Vector2(0, 0)).ToLVector();

                    var outputArg = new OutputArgument();
                    Function.Call(Hash.GET_CURRENT_PED_WEAPON, Game.Player.Character, outputArg, true);
                    obj.WeaponHash = outputArg.GetResult<int>();
                }

                Vehicle trailer;

                if ((VehicleHash)veh.Model.Hash == VehicleHash.TowTruck ||
                    (VehicleHash)veh.Model.Hash == VehicleHash.TowTruck2)
                    trailer = veh.TowedVehicle;
                else if ((VehicleHash)veh.Model.Hash == VehicleHash.Cargobob ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob2 ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob3 ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob4)
                    trailer = SyncEventWatcher.GetVehicleCargobobVehicle(veh);
                else trailer = SyncEventWatcher.GetVehicleTrailerVehicle(veh);

                if (trailer != null && trailer.Exists())
                {
                    obj.Trailer = trailer.Position.ToLVector();
                }

                //var bin = SerializeBinary(DeltaCompressor.CompressData(obj));
                var bin = PacketOptimization.WritePureSync(obj);

                var msg = Client.CreateMessage();
                msg.Write((int)PacketType.VehiclePureSync);
                msg.Write(bin.Length);
                msg.Write(bin);
                try
                {
                    Client.SendMessage(msg, SYNC_MESSAGE_TYPE, (int) ConnectionChannel.PureSync);
                }
                catch (Exception ex)
                {
                    Util.SafeNotify("FAILED TO SEND DATA: " + ex.Message);
                    LogManager.LogException(ex, "SENDPLAYERDATA");
                }

                if (_lastPedData || Environment.TickCount - _lastLightSync > LIGHT_SYNC_RATE)
                {
                    _lastLightSync = Environment.TickCount;

                    LogManager.DebugLog("SENDING LIGHT VEHICLE SYNC");

                    var lightBin = PacketOptimization.WriteLightSync(obj);

                    var lightMsg = Client.CreateMessage();
                    lightMsg.Write((int)PacketType.VehicleLightSync);
                    lightMsg.Write(lightBin.Length);
                    lightMsg.Write(lightBin);
                    try
                    {
                        Client.SendMessage(lightMsg, NetDeliveryMethod.ReliableSequenced, (int)ConnectionChannel.LightSync);
                    }
                    catch (Exception ex)
                    {
                        Util.SafeNotify("FAILED TO SEND LIGHT DATA: " + ex.Message);
                        LogManager.LogException(ex, "SENDPLAYERDATA");
                    }

                    _bytesSent += lightBin.Length;
                    _messagesSent++;
                }

                _lastPedData = false;

                _averagePacketSize.Add(bin.Length);
                if (_averagePacketSize.Count > 10)
                    _averagePacketSize.RemoveAt(0);

                _bytesSent += bin.Length;
                _messagesSent++;
            }
            else
            {
                bool aiming = player.IsSubtaskActive(ESubtask.AIMED_SHOOTING_ON_FOOT) || player.IsSubtaskActive(ESubtask.AIMING_THROWABLE); // Game.IsControlPressed(0, GTA.Control.Aim);
                bool shooting = Function.Call<bool>(Hash.IS_PED_SHOOTING, player.Handle);
                
                Vector3 aimCoord = new Vector3();
                if (aiming || shooting)
                {
                    aimCoord = RaycastEverything(new Vector2(0, 0));
                }

                var obj = new PedData();
                obj.AimCoords = aimCoord.ToLVector();
                obj.Position = player.Position.ToLVector();
                obj.Quaternion = player.Rotation.ToLVector();
                obj.PedArmor = (byte)player.Armor;
                obj.PedModelHash = player.Model.Hash;
                obj.WeaponHash = (int)player.Weapons.Current.Hash;
                obj.PlayerHealth = (byte)(100 * ((player.Health < 0 ? 0 : player.Health) / (float)player.MaxHealth));
                obj.Velocity = player.Velocity.ToLVector();

                obj.Flag = 0;

                if (player.IsRagdoll)
                    obj.Flag |= (int)PedDataFlags.Ragdoll;
                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 0 &&
                    Game.Player.Character.IsInAir)
                    obj.Flag |= (int) PedDataFlags.InFreefall;
                if (player.IsInMeleeCombat)
                    obj.Flag |= (int)PedDataFlags.InMeleeCombat;
                if (aiming)
                    obj.Flag |= (int)PedDataFlags.Aiming;
                if ((shooting && !player.IsSubtaskActive(ESubtask.AIMING_PREVENTED_BY_OBSTACLE) && !player.IsSubtaskActive(ESubtask.MELEE_COMBAT)) || (player.IsInMeleeCombat && Game.IsControlJustPressed(0, Control.Attack)))
                    obj.Flag |= (int)PedDataFlags.Shooting;
                if (Function.Call<bool>(Hash.IS_PED_JUMPING, player.Handle))
                    obj.Flag |= (int)PedDataFlags.Jumping;
                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 2)
                    obj.Flag |= (int)PedDataFlags.ParachuteOpen;
                if (player.IsInCover())
                    obj.Flag |= (int) PedDataFlags.IsInCover;
                if (!Function.Call<bool>((Hash) 0x6A03BF943D767C93, player))
                    obj.Flag |= (int) PedDataFlags.IsInLowerCover;
                if (player.IsInCoverFacingLeft)
                    obj.Flag |= (int) PedDataFlags.IsInCoverFacingLeft;
                if (player.IsReloading)
                    obj.Flag |= (int)PedDataFlags.IsReloading;

                obj.Speed = GetPedWalkingSpeed(player);

                //var bin = SerializeBinary(DeltaCompressor.CompressData(obj));
                var bin = PacketOptimization.WritePureSync(obj);

                var msg = Client.CreateMessage();

                msg.Write((int)PacketType.PedPureSync);
                msg.Write(bin.Length);
                msg.Write(bin);

                try
                {
                    Client.SendMessage(msg, SYNC_MESSAGE_TYPE, (int)ConnectionChannel.PureSync);
                }
                catch (Exception ex)
                {
                    Util.SafeNotify("FAILED TO SEND DATA: " + ex.Message);
                    LogManager.LogException(ex, "SENDPLAYERDATAPED");
                }

                LogManager.DebugLog("TIME SINCE LAST LIGHTSYNC: " + (Environment.TickCount - _lastLightSync));
                if (!_lastPedData || Environment.TickCount - _lastLightSync > LIGHT_SYNC_RATE)
                {
                    _lastLightSync = Environment.TickCount;

                    LogManager.DebugLog("SENDING LIGHT PED SYNC");

                    var lightBin = PacketOptimization.WriteLightSync(obj);

                    var lightMsg = Client.CreateMessage();
                    lightMsg.Write((int)PacketType.PedLightSync);
                    lightMsg.Write(lightBin.Length);
                    lightMsg.Write(lightBin);
                    try
                    {
                        var result = Client.SendMessage(lightMsg, NetDeliveryMethod.ReliableSequenced, (int)ConnectionChannel.LightSync);
                        LogManager.DebugLog("LIGHT PED SYNC RESULT :" + result);
                    }
                    catch (Exception ex)
                    {
                        Util.SafeNotify("FAILED TO SEND LIGHT DATA: " + ex.Message);
                        LogManager.LogException(ex, "SENDPLAYERDATA");
                    }

                    _bytesSent += lightBin.Length;
                    _messagesSent++;
                }

                _lastPedData = true;

                _averagePacketSize.Add(bin.Length);
                if (_averagePacketSize.Count > 10)
                    _averagePacketSize.RemoveAt(0);
                _bytesSent += bin.Length;
                _messagesSent++;
            }
        }
        */
        ///*

        /// <summary>
        /// Debug use only
        /// </summary>
        /// <returns></returns>
        public PedData PackagePedData()
        {
            var player = Game.Player.Character;

            if (player.IsInVehicle())
            {
                return null;
            }
            else
            {
                bool aiming = player.IsSubtaskActive(ESubtask.AIMED_SHOOTING_ON_FOOT); // Game.IsControlPressed(0, GTA.Control.Aim);
                bool shooting = Function.Call<bool>(Hash.IS_PED_SHOOTING, player.Handle);

                Vector3 aimCoord = new Vector3();
                if (aiming || shooting)
                {
                    aimCoord = RaycastEverything(new Vector2(0, 0));
                }

                var obj = new PedData
                {
                    AimCoords = aimCoord.ToLVector(),
                    Position = (player.Position + new Vector3(10, 0, 0)).ToLVector(),
                    Quaternion = player.Rotation.ToLVector(),
                    PedArmor = (byte)player.Armor,
                    PedModelHash = player.Model.Hash,
                    WeaponHash = (int)player.Weapons.Current.Hash,
                    WeaponAmmo = (int)player.Weapons.Current.Ammo,
                    PlayerHealth = (byte)Util.Util.Clamp(0, player.Health, 255),

                    Velocity = player.Velocity.ToLVector(),
                    Flag = 0,
                    Speed = (byte)GetPedWalkingSpeed(player),
                    Latency = _debugInterval / 1000f
                };

                if (player.IsRagdoll)
                    obj.Flag |= (int)PedDataFlags.Ragdoll;
                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 0 &&
                    Game.Player.Character.IsInAir)
                    obj.Flag |= (int)PedDataFlags.InFreefall;
                if (player.IsInMeleeCombat)
                    obj.Flag |= (int)PedDataFlags.InMeleeCombat;
                if (aiming)
                    obj.Flag |= (int)PedDataFlags.Aiming;
                if ((shooting && !player.IsSubtaskActive(ESubtask.AIMING_PREVENTED_BY_OBSTACLE) && !player.IsSubtaskActive(ESubtask.MELEE_COMBAT)) || (player.IsInMeleeCombat && Game.IsControlJustPressed(0, Control.Attack)))
                    obj.Flag |= (int)PedDataFlags.Shooting;
                if (Function.Call<bool>(Hash.IS_PED_JUMPING, player.Handle))
                    obj.Flag |= (int)PedDataFlags.Jumping;
                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character.Handle) == 2)
                    obj.Flag |= (int)PedDataFlags.ParachuteOpen;
                if (player.IsInCover())
                    obj.Flag |= (int)PedDataFlags.IsInCover;
                if (!Function.Call<bool>((Hash)0x6A03BF943D767C93, player))
                    obj.Flag |= (int)PedDataFlags.IsInLowerCover;
                if (player.IsInCoverFacingLeft)
                    obj.Flag |= (int)PedDataFlags.IsInCoverFacingLeft;
                if (player.IsSubtaskActive(ESubtask.USING_LADDER))
                    obj.Flag |= (int)PedDataFlags.IsOnLadder;
                if (Function.Call<bool>(Hash.IS_PED_CLIMBING, player))
                    obj.Flag |= (int)PedDataFlags.IsVaulting;
                if (player.IsReloading)
                    obj.Flag |= (int)PedDataFlags.IsReloading;
                if (player.IsSubtaskActive(161) || player.IsSubtaskActive(162) || player.IsSubtaskActive(163) ||
                    player.IsSubtaskActive(164))
                {
                    obj.Flag |= (int)PedDataFlags.EnteringVehicle;
                    obj.VehicleTryingToEnter =
                        NetEntityHandler.EntityToNet(Function.Call<int>(Hash.GET_VEHICLE_PED_IS_TRYING_TO_ENTER,
                            Game.Player.Character));
                    obj.SeatTryingToEnter = (sbyte)
                        Function.Call<int>(Hash.GET_SEAT_PED_IS_TRYING_TO_ENTER,
                            Game.Player.Character);
                }

                if (player.IsSubtaskActive(168))
                {
                    obj.Flag |= (int)PedDataFlags.ClosingVehicleDoor;
                }

                if (player.IsSubtaskActive(161) || player.IsSubtaskActive(162) || player.IsSubtaskActive(163) ||
                    player.IsSubtaskActive(164))
                {
                    obj.Flag |= (int)PedDataFlags.EnteringVehicle;

                    obj.VehicleTryingToEnter =
                        Main.NetEntityHandler.EntityToNet(Function.Call<int>(Hash.GET_VEHICLE_PED_IS_TRYING_TO_ENTER,
                            Game.Player.Character));

                    obj.SeatTryingToEnter = (sbyte)
                        Function.Call<int>(Hash.GET_SEAT_PED_IS_TRYING_TO_ENTER,
                            Game.Player.Character);
                }

                obj.Speed = GetPedWalkingSpeed(player);
                return obj;
            }
        }

        /// <summary>
        /// Debug use only
        /// </summary>
        /// <returns></returns>
        public VehicleData PackageVehicleData()
        {
            var player = Game.Player.Character;

            if (player.IsInVehicle())
            {
                var veh = player.CurrentVehicle;

                var horn = Game.Player.IsPressingHorn;
                var siren = veh.SirenActive;
                var vehdead = veh.IsDead;

                var obj = new VehicleData
                {
                    Position = veh.Position.ToLVector(),
                    VehicleHandle = Main.NetEntityHandler.EntityToNet(player.CurrentVehicle.Handle),
                    Quaternion = veh.Rotation.ToLVector(),
                    PedModelHash = player.Model.Hash,
                    PlayerHealth = (byte)Util.Util.Clamp(0, player.Health, 255),
                    VehicleHealth = veh.EngineHealth,
                    Velocity = veh.Velocity.ToLVector(),
                    PedArmor = (byte)player.Armor,
                    RPM = veh.CurrentRPM,
                    VehicleSeat = (short)player.GetPedSeat(),
                    Flag = 0,
                    Steering = veh.SteeringAngle,
                };

                if (player.IsSubtaskActive(167) || player.IsSubtaskActive(168))
                {
                    obj.Flag |= (short)VehicleDataFlags.ExitingVehicle;
                }

                if (horn)
                    obj.Flag |= (byte)VehicleDataFlags.PressingHorn;
                if (siren)
                    obj.Flag |= (byte)VehicleDataFlags.SirenActive;
                if (vehdead)
                    obj.Flag |= (byte)VehicleDataFlags.VehicleDead;

                if (veh.IsInBurnout)
                    obj.Flag |= (byte)VehicleDataFlags.BurnOut;

                // DUBSTEP
                if (!WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, player.GetPedSeat()) &&
                WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash)veh.Model.Hash) &&
                player.GetPedSeat() == -1)
                {
                    obj.Flag |= (byte)VehicleDataFlags.HasAimData;
                    obj.AimCoords = new CherryMPShared.Vector3(0, 0, 0);
                    obj.WeaponHash = Main.GetCurrentVehicleWeaponHash(player);
                    if (Game.IsEnabledControlPressed(0, Control.VehicleFlyAttack))
                        obj.Flag |= (byte)VehicleDataFlags.Shooting;
                }
                else if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, player.GetPedSeat()))
                {
                    obj.Flag |= (byte)VehicleDataFlags.HasAimData;
                    obj.WeaponHash = 0;
                    obj.AimCoords = Main.RaycastEverything(new Vector2(0, 0)).ToLVector();
                    if (Game.IsEnabledControlPressed(0, Control.VehicleAttack))
                        obj.Flag |= (byte)VehicleDataFlags.Shooting;
                }
                else
                {
                    bool usingVehicleWeapon = player.IsSubtaskActive(200) || player.IsSubtaskActive(190);

                    if (usingVehicleWeapon &&
                        Game.IsEnabledControlPressed(0, Control.Attack) &&
                        player.Weapons.Current?.AmmoInClip != 0)
                    {
                        obj.Flag |= (byte)VehicleDataFlags.Shooting;
                        obj.Flag |= (byte)VehicleDataFlags.HasAimData;
                    }

                    if ((usingVehicleWeapon &&
                         player.Weapons.Current?.AmmoInClip != 0) ||
                        (player.Weapons.Current?.Hash == WeaponHash.Unarmed &&
                         player.IsSubtaskActive(200)))
                    {
                        obj.Flag |= (byte)VehicleDataFlags.Aiming;
                        obj.Flag |= (byte)VehicleDataFlags.HasAimData;
                    }

                    var outputArg = new OutputArgument();
                    Function.Call(Hash.GET_CURRENT_PED_WEAPON, player, outputArg, true);
                    obj.WeaponHash = outputArg.GetResult<int>();

                    lock (SyncCollector.Lock)
                    {
                        if (SyncCollector.LastSyncPacket != null && SyncCollector.LastSyncPacket is VehicleData &&
                            WeaponDataProvider.NeedsFakeBullets(obj.WeaponHash.Value) &&
                            (((VehicleData)SyncCollector.LastSyncPacket).Flag & (byte)VehicleDataFlags.Shooting) != 0)
                        {
                            obj.Flag |= (byte)VehicleDataFlags.Shooting;
                            obj.Flag |= (byte)VehicleDataFlags.HasAimData;
                        }
                    }

                    obj.AimCoords = Main.RaycastEverything(new Vector2(0, 0)).ToLVector();
                }

                Vehicle trailer;

                if ((VehicleHash)veh.Model.Hash == VehicleHash.TowTruck ||
                    (VehicleHash)veh.Model.Hash == VehicleHash.TowTruck2)
                    trailer = veh.TowedVehicle;
                else if ((VehicleHash)veh.Model.Hash == VehicleHash.Cargobob ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob2 ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob3 ||
                         (VehicleHash)veh.Model.Hash == VehicleHash.Cargobob4)
                    trailer = SyncEventWatcher.GetVehicleCargobobVehicle(veh);
                else trailer = SyncEventWatcher.GetVehicleTrailerVehicle(veh);

                if (trailer != null && trailer.Exists())
                {
                    obj.Trailer = trailer.Position.ToLVector();
                }

                return obj;
            }
            else
            {
                return null;
            }
        }
        //*/

        public static SyncPed GetPedDamagedByPlayer()
        {
            foreach (SyncPed StreamedInPlayers in StreamerThread.StreamedInPlayers)
            {
                if (Function.Call<bool>(Hash.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY, StreamedInPlayers.LocalHandle, Game.Player.Character, true))
                {
                    if (Function.Call<bool>(Hash.HAS_PED_BEEN_DAMAGED_BY_WEAPON, StreamedInPlayers.LocalHandle, Game.Player.Character.Weapons.Current.Model.Hash, 0))
                    {
                        if (Function.Call<int>(Hash.GET_WEAPON_DAMAGE_TYPE, Game.Player.Character.Weapons.Current.Model.Hash) == 3)
                        {
                            Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, StreamedInPlayers.LocalHandle);
                            return StreamedInPlayers;
                        }
                    }
                }
            }
            return null;
        }

        public static SyncPed GetPedWeHaveDamaged()
        {
            var us = Game.Player.Character;

            SyncPed[] myArray;

            lock (StreamerThread.StreamedInPlayers) myArray = StreamerThread.StreamedInPlayers.ToArray();

            foreach (var index in myArray)
            {
                if (index == null) continue;

                var them = new Ped(index.LocalHandle);
                if (!them.HasBeenDamagedBy(us)) continue;

                Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, them);
                Function.Call(Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, us);
                //Util.Util.SafeNotify("Shot at" + index.Name + " " + DateTime.Now.Millisecond);
                return index;
            }
            return null;
        }

        public static byte GetPedWalkingSpeed(Ped ped)
        {
            byte output = 0;
            string animd;

            if ((animd = SyncPed.GetAnimalAnimationDictionary(ped.Model.Hash)) != null)
            {
                // Player has an animal skin
                var hash = (PedHash)ped.Model.Hash;

                if (hash == PedHash.ChickenHawk || hash == PedHash.Cormorant || hash == PedHash.Crow ||
                    hash == PedHash.Seagull || hash == PedHash.Pigeon)
                {
                    if (ped.Velocity.Length() > 0.1) output = 1;
                    if (ped.IsInAir || ped.Velocity.Length() > 0.5) output = 3;
                }
                else if (hash == PedHash.Dolphin || hash == PedHash.Fish || hash == PedHash.Humpback ||
                         hash == PedHash.KillerWhale || hash == PedHash.Stingray || hash == PedHash.HammerShark ||
                         hash == PedHash.TigerShark)
                {
                    if (ped.Velocity.Length() > 0.1) output = 1;
                    if (ped.Velocity.Length() > 0.5) output = 2;
                }
            }
            if (Function.Call<bool>(Hash.IS_PED_WALKING, ped))
                output = 1;
            if (Function.Call<bool>(Hash.IS_PED_RUNNING, ped))
                output = 2;
            if (Function.Call<bool>(Hash.IS_PED_SPRINTING, ped) || (ped.IsPlayer && Game.IsControlPressed(0, Control.Sprint)))
                output = 3;
            //if (Function.Call<bool>(Hash.IS_PED_STRAFING, ped)) ;

            /*if (ped.IsSubtaskActive(ESubtask.AIMING_GUN))
            {
                if (ped.Velocity.LengthSquared() > 0.1f*0.1f)
                    output = 1;
            }
            */

            return output;
        }

        public static void InvokeFinishedDownload(List<string> resources)
        {
            var confirmObj = Client.CreateMessage();
            confirmObj.Write((byte)PacketType.ConnectionConfirmed);
            confirmObj.Write(true);
            confirmObj.Write(resources.Count);

            for (int i = 0; i < resources.Count; i++)
            {
                confirmObj.Write(resources[i]);
            }

            Client.SendMessage(confirmObj, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);

            HasFinishedDownloading = true;
            Function.Call((Hash)0x10D373323E5B9C0D); //_REMOVE_LOADING_PROMPT
            Function.Call(Hash.DISPLAY_RADAR, true);
        }

        public static int GetCurrentVehicleWeaponHash(Ped ped)
        {
            if (ped.IsInVehicle())
            {
                var outputArg = new OutputArgument();
                var success = Function.Call<bool>(Hash.GET_CURRENT_PED_VEHICLE_WEAPON, ped, outputArg);
                if (success)
                {
                    return outputArg.GetResult<int>();
                }
                else
                {
                    return 0;
                }
            }
            return 0;
        }

        private Vehicle _lastPlayerCar;
        private int _lastModel;
        private bool _whoseturnisitanyways;

        private Vector3 offset;
        private DateTime _start;

        private bool _hasInitialized;
        private bool _hasPlayerSpawned;

        private static int _debugStep;

        private int _debugPickup = 0;
        private int _debugmask;
        private Vehicle _debugVehicle;
        private bool _lastSpectating;
        private int _currentSpectatingPlayerIndex = 100000;
        internal SyncPed CurrentSpectatingPlayer;
        private Vector3 _lastWaveReset;
        public static DateTime LastCarEnter;
        private float _debugPed;
        private Dictionary<int, int> _debugSettings = new Dictionary<int, int>();
        private bool _minimapSet;
        private object freedebug;
        private int _lastPlayerHealth = 100;
        private int _lastPlayerArmor = 0;
        private bool _lastVehicleSiren;
        private WeaponHash _lastPlayerWeapon = WeaponHash.Unarmed;
        private PedHash _lastPlayerModel = PedHash.Clown01SMY;

        private int _lastBytesSent;
        private int _lastBytesReceived;
        private int _lastCheck;

        internal static int _bytesSentPerSecond;
        internal static int _bytesReceivedPerSecond;

        internal static Warning _mainWarning;
        internal static string _threadsafeSubtitle;

        internal static bool _playerGodMode;

        private long _lastEntityRemoval;

        public static bool IsOnServer()
        {
            return Client != null && Client.ConnectionStatus == NetConnectionStatus.Connected;
        }

        #region Download stuff
        private Thread _httpDownloadThread;
        private bool _cancelDownload;
        private void StartFileDownload(string address)
        {
            _cancelDownload = false;

            _httpDownloadThread?.Abort();
            _httpDownloadThread = new Thread((ThreadStart)delegate
            {
                try
                {
                    using (var wc = new WebClient())
                    {
                        var manifestJson = wc.DownloadString(address + "/manifest.json");

                        var obj = JsonConvert.DeserializeObject<FileManifest>(manifestJson);

                        wc.DownloadProgressChanged += (sender, args) =>
                        {
                            _threadsafeSubtitle = "Downloading " + args.ProgressPercentage;
                        };

                        foreach (var resource in obj.exportedFiles)
                        {
                            if (!Directory.Exists(FileTransferId._DOWNLOADFOLDER_ + resource.Key))
                                Directory.CreateDirectory(FileTransferId._DOWNLOADFOLDER_ + resource.Key);

                            for (var index = resource.Value.Count - 1; index >= 0; index--)
                            {
                                var file = resource.Value[index];
                                if (file.type == FileType.Script) continue;

                                var target = Path.Combine(FileTransferId._DOWNLOADFOLDER_, resource.Key, file.path);

                                if (File.Exists(target))
                                {
                                    var newHash = DownloadManager.HashFile(target);

                                    if (newHash == file.hash) continue;
                                }

                                wc.DownloadFileAsync(
                                    new Uri($"{address}/{resource.Key}/{file.path}"), target);

                                while (wc.IsBusy)
                                {
                                    Thread.Yield();
                                    if (!_cancelDownload) continue;
                                    wc.CancelAsync();
                                    return;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogException(ex, "HTTP FILE DOWNLOAD");
                }
            });
        }
        #endregion

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            Chat.OnKeyDown(e.KeyCode);

            if (e.KeyCode == Keys.Escape && Client != null &&
                Client.ConnectionStatus == NetConnectionStatus.Disconnected)
            {
                Client.Disconnect("Connection canceled.");
            }

            if (e.KeyCode == Keys.P && IsOnServer() && !MainMenu.Visible && !Chat.IsFocused && !CefController.ShowCursor)
            {
                _mainWarning = new Warning("Disabled feature", "Game settings menu has been disabled while connected.\nDisconnect from the server first.")
                {
                    OnAccept = () => { _mainWarning.Visible = false; }
                };
            }

            if (e.KeyCode == Keys.F10 && !Chat.IsFocused)
            {
                MainMenu.Visible = !MainMenu.Visible;

                if (!IsOnServer())
                {
                    if (MainMenu.Visible)
                        World.RenderingCamera = MainMenuCamera;
                    else
                        World.RenderingCamera = null;
                }
                else if (MainMenu.Visible)
                {
                    RebuildPlayersList();
                }

                MainMenu.RefreshIndex();
            }

            if (e.KeyCode == Keys.F7 && IsOnServer())
            {
                ChatVisible = !ChatVisible;
                UIVisible = !UIVisible;
                Function.Call(Hash.DISPLAY_RADAR, UIVisible);
                Function.Call(Hash.DISPLAY_HUD, UIVisible);
            }

            if (e.KeyCode == PlayerSettings.ScreenshotKey && IsOnServer())
            {
                Screenshot.TakeScreenshot();
            }

            if (e.KeyCode == Keys.T && IsOnServer() && UIVisible && ChatVisible && ScriptChatVisible && CanOpenChatbox)
            {
                if (!_oldChat)
                {
                    Chat.IsFocused = true;
                    _wasTyping = true;
                }
                else
                {
                    var message = Game.GetUserInput(255);
                    if (!string.IsNullOrEmpty(message))
                    {
                        var obj = new ChatData()
                        {
                            Message = message,
                        };
                        var data = SerializeBinary(obj);

                        var msg = Client.CreateMessage();
                        msg.Write((byte)PacketType.ChatData);
                        msg.Write(data.Length);
                        msg.Write(data);
                        Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);
                    }
                }
            }
        }

        public void ConnectToServer(string ip, int port = 0, bool passProtected = false, string myPass = "")
        {
            if (IsOnServer())
            {
                Client.Disconnect("Switching servers");
                Wait(1000);
            }

            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            if (!_minimapSet)
            {
                var scal = new Scaleform("minimap");
                scal.CallFunction("MULTIPLAYER_IS_ACTIVE", true, false);

                Function.Call(Hash._SET_RADAR_BIGMAP_ENABLED, true, false);
                Function.Call(Hash._SET_RADAR_BIGMAP_ENABLED, false, false);

                _minimapSet = true;
            }

            Chat.Init();

            if (Client == null)
            {
                var cport = GetOpenUdpPort();
                if (cport == 0)
                {
                    Util.Util.SafeNotify("No available UDP port was found.");
                    return;
                }
                _config.Port = cport;
                Client = new NetClient(_config);
                Client.Start();
            }

            lock (Npcs) Npcs = new Dictionary<string, SyncPed>();
            lock (_tickNatives) _tickNatives = new Dictionary<string, NativeData>();

            var msg = Client.CreateMessage();

            var obj = new ConnectionRequest
            {
                SocialClubName = string.IsNullOrWhiteSpace(Game.Player.Name) ? "Unknown" : Game.Player.Name, // To be used as identifiers in server files
                DisplayName = string.IsNullOrWhiteSpace(PlayerSettings.DisplayName) ? SocialClubName : PlayerSettings.DisplayName.Trim(),
                ScriptVersion = CurrentVersion.ToString(),
                CEF = !CefUtil.DISABLE_CEF,
                CEFDevtool = EnableDevTool,
                GameVersion = (byte)Game.Version,
                MediaStream = EnableMediaStream
            };

            if (passProtected)
            {
                if (!string.IsNullOrWhiteSpace(myPass))
                {
                    obj.Password = myPass;
                }
                else
                {
                    MainMenu.TemporarilyHidden = true;
                    obj.Password = Game.GetUserInput(256);
                    MainMenu.TemporarilyHidden = false;
                }
            }

            var bin = SerializeBinary(obj);

            msg.Write((byte)PacketType.ConnectionRequest);
            msg.Write(bin.Length);
            msg.Write(bin);

            try
            {
                Client.Connect(ip, port == 0 ? Port : port, msg);
            }
            catch (NetException ex)
            {
                GTA.UI.Screen.ShowNotification("~b~~h~Cherry Multiplayer~h~~w~~n~" + ex.Message);
                OnLocalDisconnect();
                return;
            }

            var pos = Game.Player.Character.Position;
            Function.Call(Hash.CLEAR_AREA_OF_PEDS, pos.X, pos.Y, pos.Z, 100f, 0);
            Function.Call(Hash.CLEAR_AREA_OF_VEHICLES, pos.X, pos.Y, pos.Z, 100f, 0);

            Function.Call(Hash.SET_GARBAGE_TRUCKS, 0);
            Function.Call(Hash.SET_RANDOM_BOATS, 0);
            Function.Call(Hash.SET_RANDOM_TRAINS, 0);

            Function.Call(Hash.CLEAR_ALL_BROKEN_GLASS);

            TerminateGameScripts();

            _currentServerIp = ip;
            _currentServerPort = port == 0 ? Port : port;
            CherryDiscord.CherryDiscord.InMenuDiscordDeinitializePresence();
            CherryDiscord.CherryDiscord.OnServerDiscordInitialize(PlayerSettings.DisplayName.Replace("_", " "), "Cherry Roleplay");
            CherryDiscord.CherryDiscord.OnServerDiscordUpdatePresence();
        }

        private void OnLocalDisconnect()
        {
            DEBUG_STEP = 42;
            if (NetEntityHandler.ServerWorld?.LoadedIpl != null)
            {
                foreach (var ipl in NetEntityHandler.ServerWorld.LoadedIpl)
                    Function.Call(Hash.REMOVE_IPL, ipl);
            }

            DEBUG_STEP = 43;
            if (NetEntityHandler.ServerWorld?.RemovedIpl != null)
                foreach (var ipl in NetEntityHandler.ServerWorld.RemovedIpl)
                {
                    Function.Call(Hash.REQUEST_IPL, ipl);
                }


            DEBUG_STEP = 44;

            ClearLocalEntities();

            DEBUG_STEP = 45;

            ClearLocalBlips();

            DEBUG_STEP = 49;
            CameraManager.Reset();
            NetEntityHandler.ClearAll();
            DEBUG_STEP = 50;
            JavascriptHook.StopAllScripts();
            JavascriptHook.TextElements.Clear();
            SyncCollector.ForceAimData = false;
            StringCache.Dispose();
            StringCache = null;
            _threadsafeSubtitle = null;
            _cancelDownload = true;
            _httpDownloadThread?.Abort();
            CefController.ShowCursor = false;
            DEBUG_STEP = 51;
            DownloadManager.Cancel();
            DownloadManager.FileIntegrity.Clear();
            Chat = _backupChat;
            Chat.Clear();
            WeaponInventoryManager.Clear();
            VehicleSyncManager.StopAll();
            HasFinishedDownloading = false;
            ScriptChatVisible = true;
            CanOpenChatbox = true;
            DisplayWastedMessage = true;
            _password = string.Empty;

            UIColor = Color.White;

            DEBUG_STEP = 52;

            lock (CEFManager.Browsers)
            {
                foreach (var browser in CEFManager.Browsers)
                {
                    browser.Close();
                    browser.Dispose();
                }

                CEFManager.Browsers.Clear();
            }

            CEFManager.Dispose();
            ClearStats();

            RestoreMainMenu();

            DEBUG_STEP = 56;

            ResetWorld();

            DEBUG_STEP = 57;

            ResetPlayer();

            DEBUG_STEP = 58;

            if (_serverProcess != null)
            {
                GTA.UI.Screen.ShowNotification("~b~~h~Cherry Multiplayer~h~~w~~n~Shutting down server...");
                _serverProcess.Kill();
                _serverProcess.Dispose();
                _serverProcess = null;
            }

            CherryDiscord.CherryDiscord.OnServerDiscordDeinitializePresence();
            CherryDiscord.CherryDiscord.InMenuDiscordInitialize(CurrentVersion.ToString());
            CherryDiscord.CherryDiscord.InMenuDiscordUpdatePresence();
        }

        public bool IsMessageTypeThreadsafe(NetIncomingMessageType msgType)
        {
            return false;
            //if (msgType == NetIncomingMessageType.Data || msgType == NetIncomingMessageType.StatusChanged) return false;
            //return true;
        }

        private bool IsPacketTypeThreadsafe(PacketType type)
        {
            return false;

            //if (type == PacketType.CreateEntity ||
            //    type == PacketType.DeleteEntity ||
            //    type == PacketType.FileTransferTick || // TODO: Make this threadsafe (remove GTA.UI.Screen.ShowSubtitle)
            //    type == PacketType.FileTransferComplete || 
            //    type == PacketType.ServerEvent ||
            //    type == PacketType.SyncEvent ||
            //    type == PacketType.NativeCall ||
            //    type == PacketType.BasicUnoccupiedVehSync ||
            //    type == PacketType.UnoccupiedVehSync ||
            //    type == PacketType.UnoccupiedVehStartStopSync ||
            //    type == PacketType.NativeResponse)
            //    return false;
            //return true;
        }

        private void ProcessDataMessage(NetIncomingMessage msg, PacketType type)
        {
            #region Data
            LogManager.DebugLog("RECEIVED DATATYPE " + type);
            switch (type)
            {
                case PacketType.RedownloadManifest:
                    {
                        StartFileDownload(string.Format("http://{0}:{1}", _currentServerIp, _currentServerPort));
                    }
                    break;
                case PacketType.VehiclePureSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        var packet = PacketOptimization.ReadPureVehicleSync(data);
                        HandleVehiclePacket(packet, true);
                    }
                    break;
                case PacketType.VehicleLightSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        var packet = PacketOptimization.ReadLightVehicleSync(data);
                        //LogManager.DebugLog("RECEIVED LIGHT VEHICLE PACKET");
                        HandleVehiclePacket(packet, false);
                    }
                    break;
                case PacketType.PedPureSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        var packet = PacketOptimization.ReadPurePedSync(data);
                        HandlePedPacket(packet, true);
                    }
                    break;
                case PacketType.PedLightSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        var packet = PacketOptimization.ReadLightPedSync(data);
                        HandlePedPacket(packet, false);
                    }
                    break;
                case PacketType.BasicSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);

                        LogManager.DebugLog("BASICSYNC - " + data + " | " + len);
                        foreach (var value in data)
                            LogManager.DebugLog("BASICSYNC FOR - " + value);

                        PacketOptimization.ReadBasicSync(data, out int nethandle, out CherryMPShared.Vector3 position);

                        HandleBasicPacket(nethandle, position.ToVector());
                    }
                    break;
                case PacketType.BulletSync:
                    {
                        //Util.Util.SafeNotify("Bullet Packet" + DateTime.Now.Millisecond);
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);

                        var shooting = PacketOptimization.ReadBulletSync(data, out int nethandle, out CherryMPShared.Vector3 position);

                        HandleBulletPacket(nethandle, shooting, position.ToVector());
                    }
                    break;
                case PacketType.BulletPlayerSync:
                    {
                        //Util.Util.SafeNotify("Bullet Player Packet" + DateTime.Now.Millisecond);
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);

                        var shooting = PacketOptimization.ReadBulletSync(data, out int nethandle, out int nethandleTarget);
                        HandleBulletPacket(nethandle, shooting, nethandleTarget);
                    }
                    break;
                case PacketType.UnoccupiedVehStartStopSync:
                    {
                        var veh = msg.ReadInt32();
                        var startSyncing = msg.ReadBoolean();

                        if (startSyncing)
                        {
                            VehicleSyncManager.StartSyncing(veh);
                        }
                        else
                        {
                            VehicleSyncManager.StopSyncing(veh);
                        }
                    }
                    break;
                case PacketType.UnoccupiedVehSync:
                    {
                        var len = msg.ReadInt32();
                        var bin = msg.ReadBytes(len);
                        var data = PacketOptimization.ReadUnoccupiedVehicleSync(bin);

                        if (data != null)
                        {
                            HandleUnoccupiedVehicleSync(data);
                        }
                    }
                    break;
                case PacketType.BasicUnoccupiedVehSync:
                    {
                        var len = msg.ReadInt32();
                        var bin = msg.ReadBytes(len);
                        var data = PacketOptimization.ReadBasicUnoccupiedVehicleSync(bin);

                        if (data != null)
                        {
                            HandleUnoccupiedVehicleSync(data);
                        }
                    }
                    break;
                case PacketType.NpcVehPositionData:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<VehicleData>(msg.ReadBytes(len)) as VehicleData;
                        if (data == null) return;
                        /*
                        lock (Npcs)
                        {
                            if (!Npcs.ContainsKey(data.Name))
                            {
                                var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                    //data.Quaternion.ToQuaternion(), false);
                                    data.Quaternion.ToVector(), false);
                                Npcs.Add(data.Name, repr);
                                Npcs[data.Name].Name = "";
                                Npcs[data.Name].Host = data.Id;
                            }
                            if (Npcs[data.Name].Character != null)
                                NetEntityHandler.SetEntity(data.NetHandle, Npcs[data.Name].Character.Handle);

                            Npcs[data.Name].LastUpdateReceived = DateTime.Now;
                            Npcs[data.Name].VehiclePosition =
                                data.Position.ToVector();
                            Npcs[data.Name].ModelHash = data.PedModelHash;
                            Npcs[data.Name].VehicleHash =
                                data.VehicleModelHash;
                            Npcs[data.Name].VehicleRotation =
                                data.Quaternion.ToVector();
                            //data.Quaternion.ToQuaternion();
                            Npcs[data.Name].PedHealth = data.PlayerHealth;
                            Npcs[data.Name].VehicleHealth = data.VehicleHealth;
                            //Npcs[data.Name].VehiclePrimaryColor = data.PrimaryColor;
                            //Npcs[data.Name].VehicleSecondaryColor = data.SecondaryColor;
                            Npcs[data.Name].VehicleSeat = data.VehicleSeat;
                            Npcs[data.Name].IsInVehicle = true;

                            Npcs[data.Name].IsHornPressed = data.IsPressingHorn;
                            Npcs[data.Name].Speed = data.Speed;
                            Npcs[data.Name].Siren = data.IsSirenActive;
                        }*/
                    }
                    break;
                case PacketType.ConnectionPacket:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                        if (data == null) return;
                        /*
                        lock (Npcs)
                        {
                            if (!Npcs.ContainsKey(data.Name))
                            {
                                var repr = new SyncPed(data.PedModelHash, data.Position.ToVector(),
                                    //data.Quaternion.ToQuaternion(), false);
                                    data.Quaternion.ToVector(), false);
                                Npcs.Add(data.Name, repr);
                                Npcs[data.Name].Name = "";
                                Npcs[data.Name].Host = data.Id;
                            }
                            if (Npcs[data.Name].Character != null)
                                NetEntityHandler.SetEntity(data.NetHandle, Npcs[data.Name].Character.Handle);

                            Npcs[data.Name].LastUpdateReceived = DateTime.Now;
                            Npcs[data.Name].Position = data.Position.ToVector();
                            Npcs[data.Name].ModelHash = data.PedModelHash;
                            //Npcs[data.Name].Rotation = data.Quaternion.ToVector();
                            Npcs[data.Name].Rotation = data.Quaternion.ToVector();
                            Npcs[data.Name].PedHealth = data.PlayerHealth;
                            Npcs[data.Name].IsInVehicle = false;
                            Npcs[data.Name].AimCoords = data.AimCoords.ToVector();
                            Npcs[data.Name].CurrentWeapon = data.WeaponHash;
                            Npcs[data.Name].IsAiming = data.IsAiming;
                            Npcs[data.Name].IsJumping = data.IsJumping;
                            Npcs[data.Name].IsShooting = data.IsShooting;
                            Npcs[data.Name].IsParachuteOpen = data.IsParachuteOpen;
                        }*/
                    }
                    break;
                case PacketType.CreateEntity:
                    {
                        var len = msg.ReadInt32();
                        //LogManager.DebugLog("Received CreateEntity");
                        if (DeserializeBinary<CreateEntity>(msg.ReadBytes(len)) is CreateEntity data && data.Properties != null)
                        {
                            switch (data.EntityType)
                            {
                                case (byte)EntityType.Vehicle:
                                    {
                                        NetEntityHandler.CreateVehicle(data.NetHandle, (VehicleProperties)data.Properties);
                                        //if (NetEntityHandler.Count(typeof(RemoteVehicle)) < StreamerThread.MAX_VEHICLES)
                                        //    NetEntityHandler.StreamIn(veh);
                                    }
                                    break;
                                case (byte)EntityType.Prop:
                                    {
                                        NetEntityHandler.CreateObject(data.NetHandle, data.Properties);
                                        //if (NetEntityHandler.Count(typeof(RemoteProp)) < StreamerThread.MAX_OBJECTS)
                                        //    NetEntityHandler.StreamIn(prop);
                                    }
                                    break;
                                case (byte)EntityType.Blip:
                                    {
                                        NetEntityHandler.CreateBlip(data.NetHandle, (BlipProperties)data.Properties);
                                        //if (NetEntityHandler.Count(typeof(RemoteBlip)) < StreamerThread.MAX_BLIPS)
                                        //    NetEntityHandler.StreamIn(blip);
                                    }
                                    break;
                                case (byte)EntityType.Marker:
                                    {
                                        NetEntityHandler.CreateMarker(data.NetHandle, (MarkerProperties)data.Properties);
                                        //if (NetEntityHandler.Count(typeof(RemoteMarker)) < StreamerThread.MAX_MARKERS)
                                        //    NetEntityHandler.StreamIn(mark);
                                    }
                                    break;
                                case (byte)EntityType.Pickup:
                                    {
                                        NetEntityHandler.CreatePickup(data.NetHandle, (PickupProperties)data.Properties);
                                        //if (NetEntityHandler.Count(typeof(RemotePickup)) < StreamerThread.MAX_PICKUPS)
                                        //    NetEntityHandler.StreamIn(pickup);
                                    }
                                    break;
                                case (byte)EntityType.TextLabel:
                                    {
                                        NetEntityHandler.CreateTextLabel(data.NetHandle, (TextLabelProperties)data.Properties);
                                        //if (NetEntityHandler.Count(typeof(RemoteTextLabel)) < StreamerThread.MAX_LABELS)
                                        //    NetEntityHandler.StreamIn(label);
                                    }
                                    break;
                                case (byte)EntityType.Ped:
                                    {
                                        NetEntityHandler.CreatePed(data.NetHandle, data.Properties as PedProperties);
                                        //if (NetEntityHandler.Count(typeof(RemotePed)) < StreamerThread.MAX_PEDS)
                                        //    NetEntityHandler.StreamIn(ped);
                                    }
                                    break;
                                case (byte)EntityType.Particle:
                                    {
                                        var ped = NetEntityHandler.CreateParticle(data.NetHandle, data.Properties as ParticleProperties);
                                        if (NetEntityHandler.Count(typeof(RemoteParticle)) < StreamerThread.MAX_PARTICLES) NetEntityHandler.StreamIn(ped);
                                    }
                                    break;
                            }
                        }
                    }
                    break;
                case PacketType.UpdateEntityProperties:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<UpdateEntity>(msg.ReadBytes(len)) as UpdateEntity;
                        if (data != null && data.Properties != null)
                        {
                            switch ((EntityType)data.EntityType)
                            {
                                case EntityType.Blip:
                                    NetEntityHandler.UpdateBlip(data.NetHandle, data.Properties as Delta_BlipProperties);
                                    break;
                                case EntityType.Marker:
                                    NetEntityHandler.UpdateMarker(data.NetHandle, data.Properties as Delta_MarkerProperties);
                                    break;
                                case EntityType.Player:
                                    NetEntityHandler.UpdatePlayer(data.NetHandle, data.Properties as Delta_PlayerProperties);
                                    break;
                                case EntityType.Pickup:
                                    NetEntityHandler.UpdatePickup(data.NetHandle, data.Properties as Delta_PickupProperties);
                                    break;
                                case EntityType.Prop:
                                    NetEntityHandler.UpdateProp(data.NetHandle, data.Properties as Delta_EntityProperties);
                                    break;
                                case EntityType.Vehicle:
                                    NetEntityHandler.UpdateVehicle(data.NetHandle, data.Properties as Delta_VehicleProperties);
                                    break;
                                case EntityType.Ped:
                                    NetEntityHandler.UpdatePed(data.NetHandle, data.Properties as Delta_PedProperties);
                                    break;
                                case EntityType.TextLabel:
                                    NetEntityHandler.UpdateTextLabel(data.NetHandle, data.Properties as Delta_TextLabelProperties);
                                    break;
                                case EntityType.Particle:
                                    NetEntityHandler.UpdateParticle(data.NetHandle, data.Properties as Delta_ParticleProperties);
                                    break;
                                case EntityType.World:
                                    NetEntityHandler.UpdateWorld(data.Properties);
                                    break;
                            }
                        }
                    }
                    break;
                case PacketType.DeleteEntity:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<DeleteEntity>(msg.ReadBytes(len)) as DeleteEntity;
                        if (data != null)
                        {
                            LogManager.DebugLog("RECEIVED DELETE ENTITY " + data.NetHandle);

                            var streamItem = NetEntityHandler.NetToStreamedItem(data.NetHandle);
                            if (streamItem != null)
                            {
                                VehicleSyncManager.StopSyncing(data.NetHandle);
                                NetEntityHandler.Remove(streamItem);
                                NetEntityHandler.StreamOut(streamItem);
                            }
                        }
                    }
                    break;
                case PacketType.StopResource:
                    {
                        var resourceName = msg.ReadString();
                        JavascriptHook.StopScript(resourceName);
                    }
                    break;
                case PacketType.FileTransferRequest:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<DataDownloadStart>(msg.ReadBytes(len)) as DataDownloadStart;
                        if (data != null)
                        {
                            var acceptDownload = DownloadManager.StartDownload(data.Id,
                                data.ResourceParent + Path.DirectorySeparatorChar + data.FileName,
                                (FileType)data.FileType, data.Length, data.Md5Hash, data.ResourceParent);
                            LogManager.DebugLog("FILE TYPE: " + (FileType)data.FileType);
                            LogManager.DebugLog("DOWNLOAD ACCEPTED: " + acceptDownload);
                            var newMsg = Client.CreateMessage();
                            newMsg.Write((byte)PacketType.FileAcceptDeny);
                            newMsg.Write(data.Id);
                            newMsg.Write(acceptDownload);
                            Client.SendMessage(newMsg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);
                        }
                        else
                        {
                            LogManager.DebugLog("DATA WAS NULL ON REQUEST");
                        }
                    }
                    break;
                case PacketType.FileTransferTick:
                    {
                        var channel = msg.ReadInt32();
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);
                        DownloadManager.DownloadPart(channel, data);
                    }
                    break;
                case PacketType.FileTransferComplete:
                    {
                        var id = msg.ReadInt32();
                        DownloadManager.End(id);
                    }
                    break;
                case PacketType.ChatData:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<ChatData>(msg.ReadBytes(len)) as ChatData;
                        if (data != null && !string.IsNullOrEmpty(data.Message))
                        {
                            Chat.AddMessage(data.Sender, data.Message);
                        }
                    }
                    break;
                case PacketType.ServerEvent:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<SyncEvent>(msg.ReadBytes(len)) as SyncEvent;
                        if (data != null)
                        {
                            var args = DecodeArgumentListPure(data.Arguments?.ToArray() ?? new NativeArgument[0]).ToList();
                            switch ((ServerEventType)data.EventType)
                            {
                                case ServerEventType.PlayerSpectatorChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var spectating = (bool)args[1];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != Game.Player.Character.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null)
                                            {
                                                pair.IsSpectating = spectating;
                                                if (spectating)
                                                    pair.Clear();
                                            }
                                        }
                                        else if (lclHndl != null && lclHndl.Handle == Game.Player.Character.Handle)
                                        {
                                            IsSpectating = spectating;
                                            if (spectating)
                                                _preSpectatorPos = Game.Player.Character.Position;
                                            if (spectating && args.Count >= 3)
                                            {
                                                var target = (int)args[2];
                                                SpectatingEntity = target;
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerBlipColorChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var newColor = (int)args[1];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != Game.Player.Character.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null)
                                            {
                                                pair.BlipColor = newColor;
                                                if (pair.Character != null &&
                                                    pair.Character.AttachedBlip != null)
                                                {
                                                    pair.Character.AttachedBlip.Color = (BlipColor)newColor;
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerBlipSpriteChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var newSprite = (int)args[1];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != Game.Player.Character.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null)
                                            {
                                                pair.BlipSprite = newSprite;
                                                if (pair.Character != null && pair.Character.AttachedBlip != null)
                                                    pair.Character.AttachedBlip.Sprite =
                                                        (BlipSprite)newSprite;
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerBlipAlphaChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var newAlpha = (int)args[1];
                                        var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                        if (pair != null)
                                        {
                                            pair.BlipAlpha = (byte)newAlpha;
                                            if (pair.Character != null &&
                                                pair.Character.AttachedBlip != null)
                                                pair.Character.AttachedBlip.Alpha = newAlpha;
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerTeamChange:
                                    {
                                        var netHandle = (int)args[0];
                                        var newTeam = (int)args[1];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != Game.Player.Character.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null)
                                            {
                                                pair.Team = newTeam;
                                                if (pair.Character != null)
                                                    pair.Character.RelationshipGroup = (newTeam == LocalTeam &&
                                                                                                newTeam != -1)
                                                        ? Main.FriendRelGroup
                                                        : Main.RelGroup;
                                            }
                                        }
                                        else if (lclHndl != null && lclHndl.Handle == Game.Player.Character.Handle)
                                        {
                                            LocalTeam = newTeam;
                                            foreach (var opponent in NetEntityHandler.ClientMap.Values.Where(item => item is SyncPed && ((SyncPed)item).LocalHandle != -2).Cast<SyncPed>())
                                            {
                                                if (opponent.Character != null &&
                                                    (opponent.Team == newTeam && newTeam != -1))
                                                {
                                                    opponent.Character.RelationshipGroup =
                                                        Main.FriendRelGroup;
                                                }
                                                else if (opponent.Character != null)
                                                {
                                                    opponent.Character.RelationshipGroup =
                                                        Main.RelGroup;
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerAnimationStart:
                                    {
                                        var netHandle = (int)args[0];
                                        var animFlag = (int)args[1];
                                        var animDict = (string)args[2];
                                        var animName = (string)args[3];

                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != Game.Player.Character.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null && pair.Character != null && pair.Character.Exists())
                                            {
                                                pair.IsCustomAnimationPlaying = true;
                                                pair.CustomAnimationName = animName;
                                                pair.CustomAnimationDictionary = animDict;
                                                pair.CustomAnimationFlag = animFlag;
                                                pair.CustomAnimationStartTime = Util.Util.TickCount;

                                                if (!string.IsNullOrEmpty(animName) &&
                                                    string.IsNullOrEmpty(animDict))
                                                {
                                                    pair.IsCustomScenarioPlaying = true;
                                                    pair.HasCustomScenarioStarted = false;
                                                }
                                            }
                                        }
                                        else if (lclHndl != null && lclHndl.Handle == Game.Player.Character.Handle)
                                        {
                                            AnimationFlag = 0;
                                            CustomAnimation = null;

                                            if (string.IsNullOrEmpty(animDict))
                                            {
                                                Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, Game.Player.Character, animName, 0, 0);
                                            }
                                            else
                                            {
                                                Function.Call(Hash.TASK_PLAY_ANIM, Game.Player.Character,
                                                    Util.Util.LoadDict(animDict), animName, 8f, 10f, -1, animFlag, -8f, 1, 1, 1);
                                                if ((animFlag & 1) != 0)
                                                {
                                                    CustomAnimation = animDict + " " + animName;
                                                    AnimationFlag = animFlag;
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case ServerEventType.PlayerAnimationStop:
                                    {
                                        var netHandle = (int)args[0];
                                        var lclHndl = NetEntityHandler.NetToEntity(netHandle);
                                        if (lclHndl != null && lclHndl.Handle != Game.Player.Character.Handle)
                                        {
                                            var pair = NetEntityHandler.NetToStreamedItem(netHandle) as SyncPed;
                                            if (pair != null && pair.Character != null && pair.Character.Exists() && pair.IsCustomAnimationPlaying)
                                            {
                                                pair.Character.Task.ClearAll();
                                                pair.IsCustomAnimationPlaying = false;
                                                pair.CustomAnimationName = null;
                                                pair.CustomAnimationDictionary = null;
                                                pair.CustomAnimationFlag = 0;
                                                pair.IsCustomScenarioPlaying = false;
                                                pair.HasCustomScenarioStarted = false;

                                            }
                                        }
                                        else if (lclHndl != null && lclHndl.Handle == Game.Player.Character.Handle)
                                        {
                                            Game.Player.Character.Task.ClearAll();
                                            AnimationFlag = 0;
                                            CustomAnimation = null;
                                        }
                                    }
                                    break;
                                case ServerEventType.EntityDetachment:
                                    {
                                        var netHandle = (int)args[0];
                                        bool col = (bool)args[1];
                                        NetEntityHandler.DetachEntity(NetEntityHandler.NetToStreamedItem(netHandle), col);
                                    }
                                    break;
                                case ServerEventType.WeaponPermissionChange:
                                    {
                                        var isSingleWeaponChange = (bool)args[0];

                                        if (isSingleWeaponChange)
                                        {
                                            var hash = (int)args[1];
                                            var hasPermission = (bool)args[2];

                                            if (hasPermission) WeaponInventoryManager.Allow((CherryMPShared.WeaponHash)hash);
                                            else WeaponInventoryManager.Deny((CherryMPShared.WeaponHash)hash);
                                        }
                                        else
                                        {
                                            WeaponInventoryManager.Clear();
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                    break;
                case PacketType.SyncEvent:
                    {
                        var len = msg.ReadInt32();
                        var data = DeserializeBinary<SyncEvent>(msg.ReadBytes(len)) as SyncEvent;
                        if (data != null)
                        {
                            var args = DecodeArgumentList(data.Arguments.ToArray()).ToList();
                            if (args.Count > 0)
                                LogManager.DebugLog("RECEIVED SYNC EVENT " + ((SyncEventType)data.EventType) + ": " + args.Aggregate((f, s) => f.ToString() + ", " + s.ToString()));
                            switch ((SyncEventType)data.EventType)
                            {
                                case SyncEventType.LandingGearChange:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var newState = (int)args[1];
                                        if (veh == null) return;
                                        Function.Call(Hash.CONTROL_LANDING_GEAR, veh, newState);
                                    }
                                    break;
                                case SyncEventType.DoorStateChange:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var doorId = (int)args[1];
                                        var newFloat = (bool)args[2];
                                        if (veh == null) return;
                                        if (newFloat)
                                            new Vehicle(veh.Handle).Doors[(VehicleDoorIndex)doorId].Open(false, true);
                                        else
                                            new Vehicle(veh.Handle).Doors[(VehicleDoorIndex)doorId].Close(true);

                                        var item = NetEntityHandler.NetToStreamedItem((int)args[0]) as RemoteVehicle;
                                        if (item != null)
                                        {
                                            if (newFloat)
                                                item.Tires |= (byte)(1 << doorId);
                                            else
                                                item.Tires &= (byte)~(1 << doorId);
                                        }
                                    }
                                    break;
                                case SyncEventType.BooleanLights:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var lightId = (Lights)(int)args[1];
                                        var state = (bool)args[2];
                                        if (veh == null) return;
                                        if (lightId == Lights.NormalLights)
                                            new Vehicle(veh.Handle).LightsOn = state;
                                        else if (lightId == Lights.Highbeams)
                                            Function.Call(Hash.SET_VEHICLE_FULLBEAM, veh.Handle, state);
                                    }
                                    break;
                                case SyncEventType.TrailerDeTach:
                                    {
                                        var newState = (bool)args[0];
                                        if (!newState)
                                        {
                                            var vObj =
                                                NetEntityHandler.NetToStreamedItem((int)args[1]) as RemoteVehicle;
                                            var tObj = NetEntityHandler.NetToStreamedItem(vObj.Trailer) as RemoteVehicle;

                                            vObj.Trailer = 0;
                                            if (tObj != null) tObj.TraileredBy = 0;

                                            var car = NetEntityHandler.NetToEntity((int)args[1]);
                                            if (car != null)
                                            {
                                                if ((VehicleHash)car.Model.Hash == VehicleHash.TowTruck ||
                                                    (VehicleHash)car.Model.Hash == VehicleHash.TowTruck2)
                                                {
                                                    var trailer = Function.Call<Vehicle>(Hash.GET_ENTITY_ATTACHED_TO_TOW_TRUCK, car);
                                                    Function.Call(Hash.DETACH_VEHICLE_FROM_ANY_TOW_TRUCK, trailer);
                                                }
                                                else if ((VehicleHash)car.Model.Hash == VehicleHash.Cargobob ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob2 ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob3 ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob4)
                                                {
                                                    var trailer =
                                                        Function.Call<Vehicle>(Hash.GET_VEHICLE_ATTACHED_TO_CARGOBOB,
                                                            car);
                                                    Function.Call(Hash.DETACH_VEHICLE_FROM_ANY_CARGOBOB, trailer);
                                                }
                                                else
                                                {
                                                    Function.Call(Hash.DETACH_VEHICLE_FROM_TRAILER, car.Handle);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            var vObj =
                                                NetEntityHandler.NetToStreamedItem((int)args[1]) as RemoteVehicle;
                                            var tObj = NetEntityHandler.NetToStreamedItem((int)args[2]) as RemoteVehicle;

                                            vObj.Trailer = (int)args[2];
                                            if (tObj != null) tObj.TraileredBy = (int)args[1];

                                            var car = NetEntityHandler.NetToEntity((int)args[1]);
                                            var trailer = NetEntityHandler.NetToEntity((int)args[2]);
                                            if (car != null && trailer != null)
                                            {
                                                if ((VehicleHash)car.Model.Hash == VehicleHash.TowTruck ||
                                                    (VehicleHash)car.Model.Hash == VehicleHash.TowTruck2)
                                                {
                                                    Function.Call(Hash.ATTACH_VEHICLE_TO_TOW_TRUCK, car, trailer, true, 0, 0, 0);
                                                }
                                                else if ((VehicleHash)car.Model.Hash == VehicleHash.Cargobob ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob2 ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob3 ||
                                                         (VehicleHash)car.Model.Hash == VehicleHash.Cargobob4)
                                                {
                                                    new Vehicle(car.Handle).DropCargobobHook(CargobobHook.Hook);
                                                    Function.Call(Hash.ATTACH_VEHICLE_TO_CARGOBOB, trailer, car, 0, 0, 0, 0);
                                                }
                                                else
                                                {
                                                    Function.Call(Hash.ATTACH_VEHICLE_TO_TRAILER, car, trailer, 4f);
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case SyncEventType.TireBurst:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var tireId = (int)args[1];
                                        var isBursted = (bool)args[2];
                                        if (veh == null) return;
                                        if (isBursted)
                                            new Vehicle(veh.Handle).Wheels[tireId].Burst();
                                        else
                                            new Vehicle(veh.Handle).Wheels[tireId].Fix();

                                        var item = NetEntityHandler.NetToStreamedItem((int)args[0]) as RemoteVehicle;
                                        if (item != null)
                                        {
                                            if (isBursted)
                                                item.Tires |= (byte)(1 << tireId);
                                            else
                                                item.Tires &= (byte)~(1 << tireId);
                                        }
                                    }
                                    break;
                                case SyncEventType.RadioChange:
                                    {
                                        var veh = NetEntityHandler.NetToEntity((int)args[0]);
                                        var newRadio = (int)args[1];
                                        if (veh != null)
                                        {
                                            var rad = (RadioStation)newRadio;
                                            string radioName = "OFF";
                                            if (rad != RadioStation.RadioOff)
                                            {
                                                radioName = Function.Call<string>(Hash.GET_RADIO_STATION_NAME,
                                                    newRadio);
                                            }
                                            Function.Call(Hash.SET_VEH_RADIO_STATION, veh, radioName);
                                        }
                                    }
                                    break;
                                case SyncEventType.PickupPickedUp:
                                    {
                                        var pickupItem = NetEntityHandler.NetToStreamedItem((int)args[0]);
                                        if (pickupItem != null)
                                        {
                                            NetEntityHandler.StreamOut(pickupItem);
                                            NetEntityHandler.Remove(pickupItem);
                                        }
                                    }
                                    break;
                                case SyncEventType.StickyBombDetonation:
                                    {
                                        var playerId = (int)args[0];
                                        var syncP = NetEntityHandler.NetToStreamedItem(playerId) as SyncPed;

                                        if (syncP != null && syncP.StreamedIn && syncP.Character != null)
                                        {
                                            Function.Call(Hash.EXPLODE_PROJECTILES, syncP.Character, (int)WeaponHash.StickyBomb, true);
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                    break;
                case PacketType.PlayerDisconnect:
                    {
                        var len = msg.ReadInt32();

                        var data = DeserializeBinary<PlayerDisconnect>(msg.ReadBytes(len)) as PlayerDisconnect;
                        SyncPed target = null;
                        if (data != null && (target = NetEntityHandler.NetToStreamedItem(data.Id) as SyncPed) != null)
                        {
                            NetEntityHandler.StreamOut(target);
                            target.Clear();
                            lock (Npcs)
                            {
                                foreach (var pair in new Dictionary<string, SyncPed>(Npcs).Where(p => p.Value.Host == data.Id))
                                {
                                    Npcs.Remove(pair.Key);
                                    pair.Value.Clear();
                                }
                            }
                        }
                        if (data != null) NetEntityHandler.RemoveByNetHandle(data.Id);
                    }
                    break;
                case PacketType.ScriptEventTrigger:
                    {
                        var len = msg.ReadInt32();
                        var data =
                            DeserializeBinary<ScriptEventTrigger>(msg.ReadBytes(len)) as ScriptEventTrigger;
                        if (data != null)
                        {
                            if (data.Arguments != null && data.Arguments.Count > 0)
                                JavascriptHook.InvokeServerEvent(data.EventName, data.Resource,
                                    DecodeArgumentListPure(data.Arguments?.ToArray()).ToArray());
                            else
                                JavascriptHook.InvokeServerEvent(data.EventName, data.Resource, new object[0]);
                        }
                    }
                    break;
                case PacketType.NativeCall:
                    {
                        var len = msg.ReadInt32();
                        var data = (NativeData)DeserializeBinary<NativeData>(msg.ReadBytes(len));
                        if (data == null) return;
                        LogManager.DebugLog("RECEIVED NATIVE CALL " + data.Hash);
                        DecodeNativeCall(data);
                    }
                    break;
                case PacketType.DeleteObject:
                    {
                        var len = msg.ReadInt32();
                        var data = (ObjectData)DeserializeBinary<ObjectData>(msg.ReadBytes(len));
                        if (data == null) return;
                        DeleteObject(data.Position, data.Radius, data.modelHash);
                    }
                    break;
            }
            #endregion
        }

        public void ProcessMessages(NetIncomingMessage msg, bool safeThreaded)
        {
            PacketType type = PacketType.WorldSharingStop;
            LogManager.DebugLog("RECEIVED MESSAGE " + msg.MessageType);
            try
            {
                _messagesReceived++;
                _bytesReceived += msg.LengthBytes;
                if (msg.MessageType == NetIncomingMessageType.Data)
                {
                    type = (PacketType)msg.ReadByte();
                    if (IsPacketTypeThreadsafe(type))
                    {
                        var pcmsgThread = new Thread((ThreadStart)delegate
                        {
                            ProcessDataMessage(msg, type);
                        })
                        {
                            IsBackground = true
                        };
                        pcmsgThread.Start();
                    }
                    else
                    {
                        ProcessDataMessage(msg, type);
                    }
                }
                else if (msg.MessageType == NetIncomingMessageType.ConnectionLatencyUpdated)
                {
                    Latency = msg.ReadFloat();
                }
                else if (msg.MessageType == NetIncomingMessageType.StatusChanged)
                {
                    #region StatusChanged
                    var newStatus = (NetConnectionStatus)msg.ReadByte();
                    LogManager.DebugLog("NEW STATUS: " + newStatus);
                    switch (newStatus)
                    {
                        case NetConnectionStatus.InitiatedConnect:
                            Util.Util.SafeNotify("Connecting...");
                            /*World.RenderingCamera = null;*/
                            LocalTeam = -1;
                            LocalDimension = 0;
                            ResetPlayer();
                            CEFManager.Initialize(GTA.UI.Screen.Resolution);

                            if (StringCache != null) StringCache.Dispose();

                            StringCache = new StringCache();
                            break;
                        case NetConnectionStatus.Connected:
                            AddServerToRecent(_currentServerIp, "");
                            Util.Util.SafeNotify("Connection established!");
                            var respLen = msg.SenderConnection.RemoteHailMessage.ReadInt32();
                            var respObj =
                                DeserializeBinary<ConnectionResponse>(
                                    msg.SenderConnection.RemoteHailMessage.ReadBytes(respLen)) as ConnectionResponse;
                            if (respObj == null)
                            {
                                Util.Util.SafeNotify("ERROR WHILE READING REMOTE HAIL MESSAGE");
                                return;
                            }

                            NetEntityHandler.AddLocalCharacter(respObj.CharacterHandle);

                            var confirmObj = Client.CreateMessage();
                            confirmObj.Write((byte)PacketType.ConnectionConfirmed);
                            confirmObj.Write(false);
                            Client.SendMessage(confirmObj, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);
                            JustJoinedServer = true;

                            MainMenu.Tabs.Remove(_welcomePage);

                            if (!MainMenu.Tabs.Contains(_serverItem))
                                MainMenu.Tabs.Insert(0, _serverItem);
                            if (!MainMenu.Tabs.Contains(_mainMapItem))
                                MainMenu.Tabs.Insert(0, _mainMapItem);

                            MainMenu.RefreshIndex();

                            if (respObj.Settings != null)
                            {
                                OnFootLagCompensation = respObj.Settings.OnFootLagCompensation;
                                VehicleLagCompensation = respObj.Settings.VehicleLagCompensation;

                                //try
                                //{
                                //    if (respObj.Settings.GlobalStreamingRange != 0)
                                //        GlobalStreamingRange = respObj.Settings.GlobalStreamingRange;

                                //    if (respObj.Settings.PlayerStreamingRange != 0)
                                //        PlayerStreamingRange = respObj.Settings.PlayerStreamingRange;

                                //    if (respObj.Settings.VehicleStreamingRange != 0)
                                //        VehicleStreamingRange = respObj.Settings.VehicleStreamingRange;
                                //}
                                //catch
                                //{
                                //    // Client.Disconnect("The server need to be update!");
                                //}


                                HTTPFileServer = respObj.Settings.UseHttpServer;

                                if (respObj.Settings.ModWhitelist != null)
                                {
                                    if (!DownloadManager.ValidateExternalMods(respObj.Settings.ModWhitelist))
                                        Client.Disconnect("Some mods are not whitelisted");
                                }
                            }

                            if (ParseableVersion.Parse(respObj.ServerVersion) < VersionCompatibility.LastCompatibleServerVersion)
                            {
                                Client.Disconnect("Server is outdated.");
                            }


                            if (HTTPFileServer)
                            {
                                StartFileDownload(string.Format("http://{0}:{1}", _currentServerIp, _currentServerPort));

                                if (Main.JustJoinedServer)
                                {
                                    World.RenderingCamera = null;
                                    Main.MainMenu.TemporarilyHidden = false;
                                    Main.MainMenu.Visible = false;
                                    Main.JustJoinedServer = false;
                                }
                            }
                            break;
                        case NetConnectionStatus.Disconnected:
                            var reason = msg.ReadString();
                            Util.Util.SafeNotify("You have been disconnected" +
                                        (string.IsNullOrEmpty(reason) ? " from the server." : ": " + reason));

                            OnLocalDisconnect();
                            break;
                    }
                    #endregion
                }
                else if (msg.MessageType == NetIncomingMessageType.DiscoveryResponse)
                {

                    #region DiscoveryResponse
                    var discType = msg.ReadByte();
                    var len = msg.ReadInt32();
                    var bin = msg.ReadBytes(len);
                    var data = DeserializeBinary<DiscoveryResponse>(bin) as DiscoveryResponse;
                    if (data == null) return;

                    var itemText = msg.SenderEndPoint.Address.ToString() + ":" + data.Port;
                    var matchedItems = new List<UIMenuItem>
                    {
                        _serverBrowser.Items.FirstOrDefault(i => Dns.GetHostAddresses(i.Description.Split(':')[0])[0].ToString() + ":" + i.Description.Split(':')[1] == itemText),
                        _recentBrowser.Items.FirstOrDefault(i => i.Description == itemText),
                        _favBrowser.Items.FirstOrDefault(i => i.Description == itemText),
                        _lanBrowser.Items.FirstOrDefault(i => i.Description == itemText)
                    };
                    matchedItems = matchedItems.Distinct().ToList();

                    _currentOnlinePlayers += data.PlayerCount;

                    MainMenu.Money = "Servers Online: " + ++_currentOnlineServers + " | Players Online: " + _currentOnlinePlayers;
                    #region LAN
                    if (data.LAN) //  && matchedItems.Count == 0
                    {
                        var item = new UIMenuItem(data.ServerName);
                        var gamemode = data.Gamemode == null ? "Unknown" : data.Gamemode;

                        item.Text = data.ServerName;
                        item.Description = itemText;
                        item.SetRightLabel(gamemode + " | " + data.PlayerCount + "/" + data.MaxPlayers);

                        if (data.PasswordProtected)
                            item.SetLeftBadge(UIMenuItem.BadgeStyle.Lock);

                        int lastIndx = 0;
                        if (_serverBrowser.Items.Count > 0)
                            lastIndx = _serverBrowser.Index;

                        var gMsg = msg;
                        item.Activated += (sender, selectedItem) =>
                        {
                            if (IsOnServer())
                            {
                                Client.Disconnect("Switching servers");
                                NetEntityHandler.ClearAll();

                                if (Npcs != null)
                                {
                                    Npcs.ToList().ForEach(pair => pair.Value.Clear());
                                    Npcs.Clear();
                                }

                                while (IsOnServer()) Script.Yield();
                            }
                            bool pass = false;
                            if (data.PasswordProtected)
                            {
                                pass = true;
                            }
                            _connectTab.RefreshIndex();
                            ConnectToServer(gMsg.SenderEndPoint.Address.ToString(), data.Port, pass);
                            MainMenu.TemporarilyHidden = true;
                        };

                        _lanBrowser.Items.Add(item);
                    }
                    #endregion

                    foreach (var ourItem in matchedItems.Where(k => k != null))
                    {
                        var gamemode = data.Gamemode == null ? "Unknown" : data.Gamemode;

                        ourItem.Text = data.ServerName;
                        ourItem.SetRightLabel(gamemode + " | " + data.PlayerCount + "/" + data.MaxPlayers);

                        if (PlayerSettings.FavoriteServers.Contains(ourItem.Description))
                            ourItem.SetRightBadge(UIMenuItem.BadgeStyle.Star);

                        if (data.PasswordProtected)
                            ourItem.SetLeftBadge(UIMenuItem.BadgeStyle.Lock);

                        int lastIndx = 0;
                        if (_serverBrowser.Items.Count > 0)
                            lastIndx = _serverBrowser.Index;

                        var gMsg = msg;
                        ourItem.Activated += (sender, selectedItem) =>
                        {
                            if (IsOnServer())
                            {
                                Client.Disconnect("Switching servers");

                                NetEntityHandler.ClearAll();

                                if (Npcs != null)
                                {
                                    Npcs.ToList().ForEach(pair => pair.Value.Clear());
                                    Npcs.Clear();
                                }

                                while (IsOnServer()) Script.Yield();
                            }
                            bool pass = false;
                            if (data.PasswordProtected)
                            {
                                pass = true;
                            }
                            ConnectToServer(gMsg.SenderEndPoint.Address.ToString(), data.Port, pass);
                            MainMenu.TemporarilyHidden = true;
                            _connectTab.RefreshIndex();
                        };

                        if (_serverBrowser.Items.Contains(ourItem))
                        {
                            _serverBrowser.Items.Remove(ourItem);
                            _serverBrowser.Items.Insert(0, ourItem);
                            if (_serverBrowser.Focused)
                                _serverBrowser.MoveDown();
                            else
                                _serverBrowser.RefreshIndex();
                        }
                        else if (_Verified.Items.Contains(ourItem))
                        {
                            _Verified.Items.Remove(ourItem);
                            _Verified.Items.Insert(0, ourItem);
                            if (_Verified.Focused)
                                _Verified.MoveDown();
                            else
                                _Verified.RefreshIndex();
                        }
                        else if (_lanBrowser.Items.Contains(ourItem))
                        {
                            _lanBrowser.Items.Remove(ourItem);
                            _lanBrowser.Items.Insert(0, ourItem);
                            if (_lanBrowser.Focused)
                                _lanBrowser.MoveDown();
                            else
                                _lanBrowser.RefreshIndex();
                        }
                        else if (_favBrowser.Items.Contains(ourItem))
                        {
                            _favBrowser.Items.Remove(ourItem);
                            _favBrowser.Items.Insert(0, ourItem);
                            if (_favBrowser.Focused)
                                _favBrowser.MoveDown();
                            else
                                _favBrowser.RefreshIndex();
                        }
                        else if (_recentBrowser.Items.Contains(ourItem))
                        {
                            _recentBrowser.Items.Remove(ourItem);
                            _recentBrowser.Items.Insert(0, ourItem);
                            if (_recentBrowser.Focused)
                                _recentBrowser.MoveDown();
                            else
                                _recentBrowser.RefreshIndex();
                        }
                    }
                    #endregion
                }
            }
            catch (Exception e)
            {
                if (safeThreaded)
                {
                    Util.Util.SafeNotify("Unhandled Exception ocurred in Process Messages");
                    Util.Util.SafeNotify("Message Type: " + msg.MessageType);
                    Util.Util.SafeNotify("Data Type: " + type);
                    Util.Util.SafeNotify(e.Message);
                }
                LogManager.LogException(e, "PROCESS MESSAGES (TYPE: " + msg.MessageType + " DATATYPE: " + type + ")");
            }

            //Client.Recycle(msg);
        }

        #region Bullets stuff

        private void HandleBasicPacket(int nethandle, Vector3 position)
        {
            var syncPed = NetEntityHandler.GetPlayer(nethandle);

            syncPed.Position = position;

            syncPed.LastUpdateReceived = Util.Util.TickCount;

            if (syncPed.VehicleNetHandle != 0)
            {
                var car = NetEntityHandler.NetToStreamedItem(syncPed.VehicleNetHandle) as RemoteVehicle;
                if (car != null)
                {
                    car.Position = position.ToLVector();
                    if (car.StreamedIn)
                    {
                        NetEntityHandler.NetToEntity(car).PositionNoOffset = position;
                    }
                }
            }
        }

        private void HandleVehiclePacket(VehicleData fullData, bool purePacket)
        {
            if (fullData.NetHandle == null) return;
            var syncPed = NetEntityHandler.GetPlayer(fullData.NetHandle.Value);

            syncPed.IsInVehicle = true;

            if (fullData.VehicleHandle != null) LogManager.DebugLog("RECEIVED LIGHT VEHICLE PACKET " + fullData.VehicleHandle);

            if (fullData.Position != null)
            {
                syncPed.Position = fullData.Position.ToVector();
            }

            if (fullData.VehicleHandle != null) syncPed.VehicleNetHandle = fullData.VehicleHandle.Value;
            if (fullData.Velocity != null) syncPed.VehicleVelocity = fullData.Velocity.ToVector();
            if (fullData.PedModelHash != null) syncPed.ModelHash = fullData.PedModelHash.Value;
            if (fullData.PedArmor != null) syncPed.PedArmor = fullData.PedArmor.Value;
            if (fullData.RPM != null) syncPed.VehicleRPM = fullData.RPM.Value;
            if (fullData.Quaternion != null) syncPed.VehicleRotation = fullData.Quaternion.ToVector();
            if (fullData.PlayerHealth != null) syncPed.PedHealth = fullData.PlayerHealth.Value;
            if (fullData.VehicleHealth != null) syncPed.VehicleHealth = fullData.VehicleHealth.Value;
            if (fullData.VehicleSeat != null) syncPed.VehicleSeat = fullData.VehicleSeat.Value;
            if (fullData.Latency != null) syncPed.Latency = fullData.Latency.Value;
            if (fullData.Steering != null) syncPed.SteeringScale = fullData.Steering.Value;
            if (fullData.Velocity != null) syncPed.Speed = fullData.Velocity.ToVector().Length();
            if (fullData.DamageModel != null && syncPed.MainVehicle != null) syncPed.MainVehicle.SetVehicleDamageModel(fullData.DamageModel);

            if (fullData.Flag != null)
            {
                syncPed.IsVehDead = (fullData.Flag.Value & (short)VehicleDataFlags.VehicleDead) > 0;
                syncPed.IsHornPressed = (fullData.Flag.Value & (short)VehicleDataFlags.PressingHorn) > 0;
                syncPed.Siren = (fullData.Flag.Value & (short)VehicleDataFlags.SirenActive) > 0;
                syncPed.IsShooting = (fullData.Flag.Value & (short)VehicleDataFlags.Shooting) > 0;
                syncPed.IsAiming = (fullData.Flag.Value & (short)VehicleDataFlags.Aiming) > 0;
                syncPed.IsInBurnout = (fullData.Flag.Value & (short)VehicleDataFlags.BurnOut) > 0;
                syncPed.ExitingVehicle = (fullData.Flag.Value & (short)VehicleDataFlags.ExitingVehicle) != 0;
                syncPed.IsPlayerDead = (fullData.Flag.Value & (int)VehicleDataFlags.PlayerDead) != 0;
                syncPed.Braking = (fullData.Flag.Value & (short)VehicleDataFlags.Braking) != 0;
            }

            if (fullData.WeaponHash != null)
            {
                syncPed.CurrentWeapon = fullData.WeaponHash.Value;
            }

            if (fullData.AimCoords != null) syncPed.AimCoords = fullData.AimCoords.ToVector();

            if (syncPed.VehicleNetHandle != 0 && fullData.Position != null)
            {
                var car = NetEntityHandler.NetToStreamedItem(syncPed.VehicleNetHandle) as RemoteVehicle;
                if (car != null)
                {
                    car.Position = fullData.Position;
                    car.Rotation = fullData.Quaternion;
                }

            }
            else if (syncPed.VehicleNetHandle != 00 && fullData.Position == null && fullData.Flag != null && !PacketOptimization.CheckBit(fullData.Flag.Value, VehicleDataFlags.Driver))
            {
                var car = NetEntityHandler.NetToStreamedItem(syncPed.VehicleNetHandle) as RemoteVehicle;
                if (car != null)
                {
                    syncPed.Position = car.Position.ToVector();
                    syncPed.VehicleRotation = car.Rotation.ToVector();
                }
            }

            if (purePacket)
            {
                syncPed.LastUpdateReceived = Util.Util.TickCount;
                syncPed.StartInterpolation();
            }
        }

        private void HandleBulletPacket(int netHandle, bool shooting, Vector3 aim)
        {
            //Util.Util.SafeNotify("Handling Bullet - " + DateTime.Now.Millisecond);
            var syncPed = NetEntityHandler.GetPlayer(netHandle);

            syncPed.IsShooting = shooting;
            syncPed.AimedAtPlayer = false;

            if (shooting) syncPed.AimCoords = aim;
        }

        private void HandleBulletPacket(int netHandle, bool shooting, int netHandleTarget)
        {
            //Util.Util.SafeNotify("Handling PlayerBullet - " + DateTime.Now.Millisecond);
            var syncPed = NetEntityHandler.GetPlayer(netHandle);
            var syncPedTarget = NetEntityHandler.NetToEntity(netHandleTarget);
            if (syncPed.StreamedIn && syncPedTarget != null)
            {
                syncPed.IsShooting = shooting;
                syncPed.AimedAtPlayer = true;

                if (shooting) syncPed.AimPlayer = new Ped(syncPedTarget.Handle);
            }
        }
        #endregion

        private void HandlePedPacket(PedData fullPacket, bool pure)
        {
            if (fullPacket.NetHandle == null) return;
            var syncPed = NetEntityHandler.GetPlayer(fullPacket.NetHandle.Value);


            syncPed.IsInVehicle = false;
            syncPed.VehicleNetHandle = 0;

            if (fullPacket.Position != null) syncPed.Position = fullPacket.Position.ToVector();
            if (fullPacket.Speed != null) syncPed.OnFootSpeed = fullPacket.Speed.Value;
            if (fullPacket.PedArmor != null) syncPed.PedArmor = fullPacket.PedArmor.Value;
            if (fullPacket.PedModelHash != null) syncPed.ModelHash = fullPacket.PedModelHash.Value;
            if (fullPacket.Quaternion != null) syncPed.Rotation = fullPacket.Quaternion.ToVector();
            if (fullPacket.PlayerHealth != null) syncPed.PedHealth = fullPacket.PlayerHealth.Value;
            if (fullPacket.AimCoords != null) syncPed.AimCoords = fullPacket.AimCoords.ToVector();
            if (fullPacket.WeaponHash != null) syncPed.CurrentWeapon = fullPacket.WeaponHash.Value;
            if (fullPacket.Latency != null) syncPed.Latency = fullPacket.Latency.Value;
            if (fullPacket.Velocity != null) syncPed.PedVelocity = fullPacket.Velocity.ToVector();
            if (fullPacket.WeaponAmmo != null) syncPed.Ammo = fullPacket.WeaponAmmo.Value;

            if (fullPacket.Flag != null)
            {
                syncPed.IsFreefallingWithParachute = (fullPacket.Flag.Value & (int)PedDataFlags.InFreefall) >
                                                     0;
                syncPed.IsInMeleeCombat = (fullPacket.Flag.Value & (int)PedDataFlags.InMeleeCombat) > 0;
                syncPed.IsRagdoll = (fullPacket.Flag.Value & (int)PedDataFlags.Ragdoll) > 0;
                syncPed.IsAiming = (fullPacket.Flag.Value & (int)PedDataFlags.Aiming) > 0;
                syncPed.IsJumping = (fullPacket.Flag.Value & (int)PedDataFlags.Jumping) > 0;
                syncPed.IsParachuteOpen = (fullPacket.Flag.Value & (int)PedDataFlags.ParachuteOpen) > 0;
                syncPed.IsInCover = (fullPacket.Flag.Value & (int)PedDataFlags.IsInCover) > 0;
                syncPed.IsInLowCover = (fullPacket.Flag.Value & (int)PedDataFlags.IsInLowerCover) > 0;
                syncPed.IsCoveringToLeft = (fullPacket.Flag.Value & (int)PedDataFlags.IsInCoverFacingLeft) > 0;
                syncPed.IsOnLadder = (fullPacket.Flag.Value & (int)PedDataFlags.IsOnLadder) > 0;
                syncPed.IsReloading = (fullPacket.Flag.Value & (int)PedDataFlags.IsReloading) > 0;
                syncPed.IsVaulting = (fullPacket.Flag.Value & (int)PedDataFlags.IsVaulting) > 0;
                syncPed.IsOnFire = (fullPacket.Flag.Value & (int)PedDataFlags.OnFire) != 0;
                syncPed.IsPlayerDead = (fullPacket.Flag.Value & (int)PedDataFlags.PlayerDead) != 0;

                syncPed.EnteringVehicle = (fullPacket.Flag.Value & (int)PedDataFlags.EnteringVehicle) != 0;

                if ((fullPacket.Flag.Value & (int)PedDataFlags.ClosingVehicleDoor) != 0 && syncPed.MainVehicle != null && syncPed.MainVehicle.Model.Hash != (int)VehicleHash.CargoPlane)
                {
                    syncPed.MainVehicle.Doors[(VehicleDoorIndex)syncPed.VehicleSeat + 1].Close(true);
                }

                if (syncPed.EnteringVehicle)
                {
                    syncPed.VehicleNetHandle = fullPacket.VehicleTryingToEnter.Value;
                    syncPed.VehicleSeat = fullPacket.SeatTryingToEnter.Value;
                }
            }

            if (pure)
            {
                syncPed.LastUpdateReceived = Util.Util.TickCount;
                syncPed.StartInterpolation();
            }
        }

        public void HandleUnoccupiedVehicleSync(VehicleData data)
        {
            var car = NetEntityHandler.NetToStreamedItem(data.VehicleHandle.Value) as RemoteVehicle;

            if (car != null)
            {
                car.Health = data.VehicleHealth.Value;
                car.IsDead = (data.Flag & (int)VehicleDataFlags.VehicleDead) != 0;

                if (car.DamageModel == null)
                {
                    car.DamageModel = new VehicleDamageModel();
                }

                car.DamageModel.BrokenWindows = data.DamageModel.BrokenWindows;
                car.DamageModel.BrokenDoors = data.DamageModel.BrokenDoors;

                car.Tires = data.PlayerHealth.Value;

                if (car.StreamedIn)
                {
                    var ent = NetEntityHandler.NetToEntity(data.VehicleHandle.Value);

                    if (ent != null)
                    {
                        if (data.Velocity != null)
                        {
                            VehicleSyncManager.Interpolate(data.VehicleHandle.Value, ent.Handle, data.Position.ToVector(), data.Velocity, data.Quaternion.ToVector());
                        }
                        else
                        {
                            car.Position = data.Position;
                            car.Rotation = data.Quaternion;
                        }

                        var veh = new Vehicle(ent.Handle);

                        veh.SetVehicleDamageModel(car.DamageModel);

                        veh.EngineHealth = car.Health;
                        if (!ent.IsDead && car.IsDead)
                        {
                            ent.IsInvincible = false;
                            veh.Explode();
                        }

                        for (int i = 0; i < 8; i++)
                        {
                            bool busted = (data.PlayerHealth.Value & (byte)(1 << i)) != 0;
                            if (busted && !veh.IsTireBurst(i))
                            {
                                veh.Wheels[i].Burst();
                            }
                            else if (!busted && veh.IsTireBurst(i))
                            {
                                veh.Wheels[i].Fix();
                            }
                        }
                    }
                }
                else
                {
                    car.Position = data.Position;
                    car.Rotation = data.Quaternion;
                }
            }
        }

        private void ClearLocalEntities()
        {
            lock (EntityCleanup)
            {
                EntityCleanup.ForEach(ent =>
                {
                    var prop = new Prop(ent);
                    if (prop.Exists()) prop.Delete();
                });
                EntityCleanup.Clear();
            }
        }

        private void ClearLocalBlips()
        {
            lock (BlipCleanup)
            {
                BlipCleanup.ForEach(blip =>
                {
                    var b = new Blip(blip);
                    if (b.Exists()) b.Remove();
                });
                BlipCleanup.Clear();
            }
        }

        private void RestoreMainMenu()
        {
            MainMenu.TemporarilyHidden = false;
            JustJoinedServer = false;

            MainMenu.Tabs.Remove(_serverItem);
            MainMenu.Tabs.Remove(_mainMapItem);

            if (!MainMenu.Tabs.Contains(_welcomePage))
                MainMenu.Tabs.Insert(0, _welcomePage);

            MainMenu.RefreshIndex();
            _localMarkers.Clear();

        }

        private void ResetPlayer()
        {
            Game.Player.Character.Position = _vinewoodSign;
            Game.Player.Character.IsPositionFrozen = false;

            CustomAnimation = null;
            AnimationFlag = 0;

            Game.Player.Character.SetPlayerSkin(PedHash.Jesus01);

            Game.Player.Character.MaxHealth = 200;
            Game.Player.Character.Health = 200;
            Game.Player.Character.SetDefaultClothes();

            Game.Player.Character.IsPositionFrozen = false;
            Game.Player.IsInvincible = false;
            Game.Player.Character.IsCollisionEnabled = true;
            Game.Player.Character.Opacity = 255;
            Game.Player.Character.IsInvincible = false;
            Game.Player.Character.Weapons.RemoveAll();
            Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, Game.Player.Handle, 1f);
            Function.Call(Hash.SET_SWIM_MULTIPLIER_FOR_PLAYER, Game.Player.Handle, 1f);

            Function.Call(Hash.SET_FAKE_WANTED_LEVEL, 0);
            Function.Call(Hash.DETACH_ENTITY, Game.Player.Character.Handle, true, true);
        }

        private void ResetWorld()
        {
            World.RenderingCamera = MainMenuCamera;
            MainMenu.Visible = true;
            MainMenu.TemporarilyHidden = false;
            IsSpectating = false;
            Weather = null;
            Time = null;
            LocalTeam = -1;
            LocalDimension = 0;

            //Script.Wait(500);
            //Game.Player.Character.SetDefaultClothes();
        }

        void ClearStats()
        {
            _bytesReceived = 0;
            _bytesSent = 0;
            _messagesReceived = 0;
            _messagesSent = 0;
        }

        #region Debug stuff

        private DateTime _artificialLagCounter = DateTime.MinValue;
        private bool _debugStarted;
        private SyncPed _debugSyncPed;
        private int _debugPing = 150;
        private DateTime _lastPingTime;
        private int _debugSyncrate = 100;
        private long _debugLastSync;


        public static int _debugInterval = 60;
        private int _debugFluctuation = 0;
        private Camera _debugCamera;
        private Random _r = new Random();
        private List<Tuple<long, object>> _lastData = new List<Tuple<long, object>>();
        private void Debug()
        {
            var player = Game.Player.Character;

            if (_debugSyncPed == null)
            {
                _debugSyncPed = new SyncPed(player.Model.Hash, player.Position, player.Rotation, false)
                {
                    Debug = true,
                    StreamedIn = true,
                    Name = "DEBUG",
                    Alpha = 255
                };
            }

            if (Game.IsKeyPressed(Keys.NumPad1) && _debugInterval > 0)
            {
                _debugInterval--;
                GTA.UI.Screen.ShowSubtitle("SIMULATED PING: " + _debugInterval, 5000);
            }
            else if (Game.IsKeyPressed(Keys.NumPad2))
            {
                _debugInterval++;
                GTA.UI.Screen.ShowSubtitle("SIMULATED PING: " + _debugInterval, 5000);
            }

            if (Util.Util.TickCount - _debugLastSync > _debugSyncrate)
            {
                _debugLastSync = Util.Util.TickCount;


                _lastData.Add(new Tuple<long, object>(Util.Util.TickCount,
                    player.IsInVehicle() ? (object)PackageVehicleData() : (object)PackagePedData()));

                if (Util.Util.TickCount - _lastData[0].Item1 >= (_debugInterval))
                {
                    //_artificialLagCounter = DateTime.Now;
                    //_debugFluctuation = _r.Next(10) - 5;

                    var ourData = _lastData[0].Item2;
                    _lastData.RemoveAt(0);

                    _debugSyncPed.Snapshot = ourData;

                    if (ourData is VehicleData)
                    {
                        if (player.IsInVehicle())
                            player.CurrentVehicle.Opacity = 50;

                        var data = (VehicleData)ourData;
                        _debugSyncPed.LastUpdateReceived = Util.Util.TickCount;

                        _debugSyncPed.VehicleNetHandle = data.VehicleHandle.Value;
                        _debugSyncPed.Position = data.Position.ToVector();
                        _debugSyncPed.VehicleVelocity = data.Velocity.ToVector();
                        _debugSyncPed.ModelHash = data.PedModelHash.Value;
                        if (Game.Player.Character.IsInVehicle())
                            _debugSyncPed._debugVehicleHash = Game.Player.Character.CurrentVehicle.Model.Hash;
                        _debugSyncPed.PedArmor = data.PedArmor.Value;
                        _debugSyncPed.VehicleRPM = data.RPM.Value;
                        _debugSyncPed.VehicleRotation =
                            data.Quaternion.ToVector();
                        _debugSyncPed.PedHealth = data.PlayerHealth.Value;
                        _debugSyncPed.VehicleHealth = data.VehicleHealth.Value;
                        _debugSyncPed.VehicleSeat = data.VehicleSeat.Value;
                        _debugSyncPed.IsInVehicle = true;
                        _debugSyncPed.Latency = data.Latency.Value;
                        _debugSyncPed.SteeringScale = data.Steering.Value;
                        _debugSyncPed.IsVehDead = (data.Flag & (short)VehicleDataFlags.VehicleDead) > 0;
                        _debugSyncPed.IsHornPressed = (data.Flag & (short)VehicleDataFlags.PressingHorn) > 0;
                        _debugSyncPed.Speed = data.Velocity.ToVector().Length();
                        _debugSyncPed.Siren = (data.Flag & (short)VehicleDataFlags.SirenActive) > 0;
                        _debugSyncPed.IsShooting = (data.Flag & (short)VehicleDataFlags.Shooting) > 0;
                        _debugSyncPed.IsAiming = (data.Flag & (short)VehicleDataFlags.Aiming) > 0;
                        _debugSyncPed.IsInBurnout = (data.Flag & (short)VehicleDataFlags.BurnOut) > 0;
                        _debugSyncPed.ExitingVehicle = (data.Flag.Value & (short)VehicleDataFlags.ExitingVehicle) != 0;
                        _debugSyncPed.CurrentWeapon = data.WeaponHash.Value;
                        if (data.AimCoords != null)
                            _debugSyncPed.AimCoords = data.AimCoords.ToVector();

                        _debugSyncPed.StartInterpolation();

                        //if (_debugCamera == null)
                        //_debugCamera = World.CreateCamera(player.Position + new Vector3(0, 0, 10f), new Vector3(), 60f);
                        //_debugCamera.PointAt(player);
                        //_debugCamera.Position = player.GetOffsetInWorldCoords(new Vector3(0, -10f, 20f));
                        //World.RenderingCamera = _debugCamera;
                    }
                    else
                    {
                        var data = (PedData)ourData;

                        _debugSyncPed.IsRagdoll = player.IsRagdoll;
                        _debugSyncPed.OnFootSpeed = data.Speed.Value;
                        _debugSyncPed.PedArmor = data.PedArmor.Value;
                        _debugSyncPed.LastUpdateReceived = Util.Util.TickCount;
                        _debugSyncPed.Position = data.Position.ToVector();
                        _debugSyncPed.ModelHash = data.PedModelHash.Value;
                        _debugSyncPed.Rotation = data.Quaternion.ToVector();
                        _debugSyncPed.PedHealth = data.PlayerHealth.Value;
                        _debugSyncPed.IsInVehicle = false;
                        _debugSyncPed.AimCoords = data.AimCoords.ToVector();
                        _debugSyncPed.CurrentWeapon = data.WeaponHash.Value;
                        _debugSyncPed.Ammo = data.WeaponAmmo.Value;
                        _debugSyncPed.Latency = data.Latency.Value;
                        _debugSyncPed.PedVelocity = data.Velocity.ToVector();
                        _debugSyncPed.IsFreefallingWithParachute = (data.Flag & (int)PedDataFlags.InFreefall) > 0;
                        _debugSyncPed.IsInMeleeCombat = (data.Flag & (int)PedDataFlags.InMeleeCombat) > 0;
                        _debugSyncPed.IsRagdoll = (data.Flag & (int)PedDataFlags.Ragdoll) > 0;
                        _debugSyncPed.IsAiming = (data.Flag & (int)PedDataFlags.Aiming) > 0;
                        _debugSyncPed.IsJumping = (data.Flag & (int)PedDataFlags.Jumping) > 0;
                        _debugSyncPed.IsShooting = (data.Flag & (int)PedDataFlags.Shooting) > 0;
                        _debugSyncPed.IsParachuteOpen = (data.Flag & (int)PedDataFlags.ParachuteOpen) > 0;
                        _debugSyncPed.IsInCover = (data.Flag & (int)PedDataFlags.IsInCover) > 0;
                        _debugSyncPed.IsInLowCover = (data.Flag & (int)PedDataFlags.IsInLowerCover) > 0;
                        _debugSyncPed.IsCoveringToLeft = (data.Flag & (int)PedDataFlags.IsInCoverFacingLeft) > 0;
                        _debugSyncPed.IsReloading = (data.Flag & (int)PedDataFlags.IsReloading) > 0;
                        _debugSyncPed.IsOnLadder = (data.Flag & (int)PedDataFlags.IsOnLadder) > 0;
                        _debugSyncPed.IsVaulting = (data.Flag & (int)PedDataFlags.IsVaulting) > 0;
                        _debugSyncPed.EnteringVehicle = (data.Flag & (int)PedDataFlags.EnteringVehicle) != 0;

                        if ((data.Flag.Value & (int)PedDataFlags.ClosingVehicleDoor) != 0 && _debugSyncPed.MainVehicle != null && _debugSyncPed.MainVehicle.Model.Hash != (int)VehicleHash.CargoPlane)
                        {
                            _debugSyncPed.MainVehicle.Doors[(VehicleDoorIndex)_debugSyncPed.VehicleSeat + 1].Close(true);
                        }

                        if (_debugSyncPed.EnteringVehicle)
                        {
                            _debugSyncPed.VehicleNetHandle =
                                Function.Call<int>(Hash.GET_VEHICLE_PED_IS_TRYING_TO_ENTER,
                                    Game.Player.Character);

                            _debugSyncPed.VehicleSeat = (sbyte)
                                Function.Call<int>(Hash.GET_SEAT_PED_IS_TRYING_TO_ENTER,
                                    Game.Player.Character);
                        }

                        _debugSyncPed.StartInterpolation();
                    }
                }
            }

            _debugSyncPed.DisplayLocally();

            if (_debugSyncPed.Character != null)
            {
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, _debugSyncPed.Character.Handle, player.Handle, false);
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, player.Handle, _debugSyncPed.Character.Handle, false);
            }


            if (_debugSyncPed.MainVehicle != null && player.IsInVehicle())
            {
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, _debugSyncPed.MainVehicle.Handle, player.CurrentVehicle.Handle, false);
                Function.Call(Hash.SET_ENTITY_NO_COLLISION_ENTITY, player.CurrentVehicle.Handle, _debugSyncPed.MainVehicle.Handle, false);
            }

        }

        #endregion

        public static void SendToServer(object newData, PacketType packetType, bool important, ConnectionChannel channel)
        {
            var data = SerializeBinary(newData);
            NetOutgoingMessage msg = Client.CreateMessage();
            msg.Write((byte)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Client.SendMessage(msg, important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced, (int)channel);
        }

        public static void TriggerServerEvent(string eventName, string resource, params object[] args)
        {
            if (!IsOnServer()) return;
            var packet = new ScriptEventTrigger
            {
                EventName = eventName,
                Resource = resource,
                Arguments = ParseNativeArguments(args)
            };
            var bin = SerializeBinary(packet);

            var msg = Client.CreateMessage();
            msg.Write((byte)PacketType.ScriptEventTrigger);
            msg.Write(bin.Length);
            msg.Write(bin);

            Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered);
        }

        internal static readonly string[] _weather = new string[]
        {
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
            "XMAS "
        };

        #region Raycast
        public static Vector3 RaycastEverything(Vector2 screenCoord)
        {
            Vector3 camPos, camRot;

            if (World.RenderingCamera.Handle == -1)
            {
                camPos = GameplayCamera.Position;
                camRot = GameplayCamera.Rotation;
            }
            else
            {
                camPos = World.RenderingCamera.Position;
                camRot = World.RenderingCamera.Rotation;
            }

            const float raycastToDist = 100.0f;
            const float raycastFromDist = 1f;

            var target3D = ScreenRelToWorld(camPos, camRot, screenCoord);
            var source3D = camPos;

            Entity ignoreEntity = Game.Player.Character;
            if (Game.Player.Character.IsInVehicle())
            {
                ignoreEntity = Game.Player.Character.CurrentVehicle;
            }

            var dir = (target3D - source3D);
            dir.Normalize();
            var raycastResults = World.Raycast(source3D + dir * raycastFromDist,
                source3D + dir * raycastToDist,
                (IntersectOptions)(1 | 16 | 256 | 2 | 4 | 8)// | peds + vehicles
                , ignoreEntity);

            if (raycastResults.DitHit)
            {
                return raycastResults.HitPosition;
            }

            return camPos + dir * raycastToDist;
        }

        public static Vector3 RaycastEverything(Vector2 screenCoord, Vector3 camPos, Vector3 camRot)
        {
            const float raycastToDist = 100.0f;
            const float raycastFromDist = 1f;

            var target3D = ScreenRelToWorld(camPos, camRot, screenCoord);
            var source3D = camPos;

            Entity ignoreEntity = Game.Player.Character;
            if (Game.Player.Character.IsInVehicle())
            {
                ignoreEntity = Game.Player.Character.CurrentVehicle;
            }

            var dir = (target3D - source3D);
            dir.Normalize();
            var raycastResults = World.Raycast(source3D + dir * raycastFromDist,
                source3D + dir * raycastToDist,
                (IntersectOptions)(1 | 16 | 256 | 2 | 4 | 8)// | peds + vehicles
                , ignoreEntity);

            if (raycastResults.DitHit)
            {
                return raycastResults.HitPosition;
            }

            return camPos + dir * raycastToDist;
        }
        #endregion

        #region Serialization
        public static object DeserializeBinary<T>(byte[] data)
        {
            object output;
            using (var stream = new MemoryStream(data))
            {
                try
                {
                    output = Serializer.Deserialize<T>(stream);
                }
                catch (ProtoException)
                {
                    return null;
                }
            }
            return output;
        }
        public static byte[] SerializeBinary(object data)
        {
            using (var stream = new MemoryStream())
            {
                stream.SetLength(0);
                Serializer.Serialize(stream, data);
                return stream.ToArray();
            }
        }
        #endregion

        #region Math & Conversion
        public static int GetPedSpeed(Vector3 firstVector, Vector3 secondVector)
        {
            float speed = (firstVector - secondVector).Length();
            if (speed < 0.02f)
            {
                return 0;
            }
            else if (speed >= 0.02f && speed < 0.05f)
            {
                return 1;
            }
            else if (speed >= 0.05f && speed < 0.12f)
            {
                return 2;
            }
            else if (speed >= 0.12f)
                return 3;
            return 0;
        }

        public static bool WorldToScreenRel(Vector3 worldCoords, out Vector2 screenCoords)
        {
            var num1 = new OutputArgument();
            var num2 = new OutputArgument();

            if (!Function.Call<bool>(Hash.GET_SCREEN_COORD_FROM_WORLD_COORD, worldCoords.X, worldCoords.Y, worldCoords.Z, num1, num2))
            {
                screenCoords = new Vector2();
                return false;
            }
            screenCoords = new Vector2((num1.GetResult<float>() - 0.5f) * 2, (num2.GetResult<float>() - 0.5f) * 2);
            return true;
        }

        public static PointF WorldToScreen(Vector3 worldCoords)
        {
            var num1 = new OutputArgument();
            var num2 = new OutputArgument();

            if (!Function.Call<bool>(Hash.GET_SCREEN_COORD_FROM_WORLD_COORD, worldCoords.X, worldCoords.Y, worldCoords.Z, num1, num2))
            {
                return new PointF();
            }
            return new PointF(num1.GetResult<float>(), num2.GetResult<float>());
        }

        public static Vector3 ScreenRelToWorld(Vector3 camPos, Vector3 camRot, Vector2 coord)
        {
            var camForward = RotationToDirection(camRot);
            var rotUp = camRot + new Vector3(10, 0, 0);
            var rotDown = camRot + new Vector3(-10, 0, 0);
            var rotLeft = camRot + new Vector3(0, 0, -10);
            var rotRight = camRot + new Vector3(0, 0, 10);

            var camRight = RotationToDirection(rotRight) - RotationToDirection(rotLeft);
            var camUp = RotationToDirection(rotUp) - RotationToDirection(rotDown);

            var rollRad = -DegToRad(camRot.Y);

            var camRightRoll = camRight * (float)Math.Cos(rollRad) - camUp * (float)Math.Sin(rollRad);
            var camUpRoll = camRight * (float)Math.Sin(rollRad) + camUp * (float)Math.Cos(rollRad);

            var point3D = camPos + camForward * 10.0f + camRightRoll + camUpRoll;
            if (!WorldToScreenRel(point3D, out Vector2 point2D)) return camPos + camForward * 10.0f;
            var point3DZero = camPos + camForward * 10.0f;
            if (!WorldToScreenRel(point3DZero, out Vector2 point2DZero)) return camPos + camForward * 10.0f;

            const double eps = 0.001;
            if (Math.Abs(point2D.X - point2DZero.X) < eps || Math.Abs(point2D.Y - point2DZero.Y) < eps) return camPos + camForward * 10.0f;
            var scaleX = (coord.X - point2DZero.X) / (point2D.X - point2DZero.X);
            var scaleY = (coord.Y - point2DZero.Y) / (point2D.Y - point2DZero.Y);
            var point3Dret = camPos + camForward * 10.0f + camRightRoll * scaleX + camUpRoll * scaleY;
            return point3Dret;
        }

        public static Vector3 RotationToDirection(Vector3 rotation)
        {
            var z = DegToRad(rotation.Z);
            var x = DegToRad(rotation.X);
            var num = Math.Abs(Math.Cos(x));
            return new Vector3
            {
                X = (float)(-Math.Sin(z) * num),
                Y = (float)(Math.Cos(z) * num),
                Z = (float)Math.Sin(x)
            };
        }

        public static Vector3 DirectionToRotation(Vector3 direction)
        {
            direction.Normalize();

            var x = Math.Atan2(direction.Z, direction.Y);
            var y = 0;
            var z = -Math.Atan2(direction.X, direction.Y);

            return new Vector3
            {
                X = (float)RadToDeg(x),
                Y = (float)RadToDeg(y),
                Z = (float)RadToDeg(z)
            };
        }

        public static double DegToRad(double deg)
        {
            return deg * Math.PI / 180.0;
        }

        public static double RadToDeg(double deg)
        {
            return deg * 180.0 / Math.PI;
        }

        public static double BoundRotationDeg(double angleDeg)
        {
            var twoPi = (int)(angleDeg / 360);
            var res = angleDeg - twoPi * 360;
            if (res < 0) res += 360;
            return res;
        }
        #endregion

        public void TerminateGameScripts()
        {
            GameScript.DisableAll(PlayerSettings.DisableRockstarEditor);
        }

        public void DeleteObject(CherryMPShared.Vector3 pos, float radius, int modelHash)
        {
            Prop returnedProp = Function.Call<Prop>(Hash.GET_CLOSEST_OBJECT_OF_TYPE, pos.X, pos.Y, pos.Z, radius, modelHash, 0);
            if (returnedProp != null && returnedProp.Handle != 0) returnedProp.Delete();
        }

        public static int DEBUG_STEP
        {
            get { return _debugStep; }
            set
            {
                _debugStep = value;
                LogManager.DebugLog("LAST STEP: " + value.ToString());

                if (SlowDownClientForDebug)
                    GTA.UI.Screen.ShowSubtitle(value.ToString());
            }
        }

        public int GetOpenUdpPort()
        {
            var startingAtPort = 6000;
            var maxNumberOfPortsToCheck = 500;
            var range = Enumerable.Range(startingAtPort, maxNumberOfPortsToCheck);
            var portsInUse =
                from p in range
                join used in System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
            on p equals used.Port
                select p;

            return range.Except(portsInUse).FirstOrDefault();
        }
    }


    public class MasterServerList
    {
        public List<string> list { get; set; }
        public List<string> listVerified { get; set; }
    }

    public class WelcomeSchema
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public string Picture { get; set; }
    }
}