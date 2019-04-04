﻿#define ATTACHSERVER
//#define INTEGRITYCHECK
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
                    LogManager.DebugLog("READING " + msgsRead + " MESSAGES");
                    if (msgsRead > 0)
                        foreach (var message in messages)
                        {
                            if (CrossReference.EntryPoint.IsMessageTypeThreadsafe(message.MessageType))
                            {
                                var message1 = message;
                                var pcMsgThread = new Thread((ThreadStart)delegate
                                {
                                    CrossReference.EntryPoint.ProcessMessages(message1, false);
                                });
                                pcMsgThread.IsBackground = true;
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

    internal class Main : Script
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
        internal UnoccupiedVehicleSync VehicleSyncManager;
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

            _config = new NetPeerConfiguration("GRANDTHEFTAUTONETWORK");
            _config.Port = 8888;
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

            var t = new Thread(UpdateSocialClubAvatar);
            t.IsBackground = true;
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

        private TabInteractiveListItem _Verified;
        private TabInteractiveListItem _serverBrowser;
        private TabInteractiveListItem _lanBrowser;
        private TabInteractiveListItem _favBrowser;
        private TabInteractiveListItem _recentBrowser;

        private TabItemSimpleList _serverPlayers;
        private TabSubmenuItem _serverItem;
        private TabSubmenuItem _connectTab;

        private TabWelcomeMessageItem _welcomePage;

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

        #region Menu
        public void GetWelcomeMessage()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    using (var wc = new ImpatientWebClient())
                    {
                        var rawJson = wc.DownloadString(PlayerSettings.MasterServerAddress.Trim('/') + "/welcome.json");
                        var jsonObj = JsonConvert.DeserializeObject<WelcomeSchema>(rawJson) as WelcomeSchema;
                        if (jsonObj == null) throw new WebException();
                        if (!File.Exists(GTANInstallDir + "images\\" + jsonObj.Picture))
                        {
                            wc.DownloadFile(PlayerSettings.MasterServerAddress.Trim('/') + "/pictures/" + jsonObj.Picture, GTANInstallDir + "\\images\\" + jsonObj.Picture);
                        }

                        _welcomePage.Text = jsonObj.Message;
                        _welcomePage.TextTitle = jsonObj.Title;
                        _welcomePage.PromoPicturePath = GTANInstallDir + "images\\" + jsonObj.Picture;

                        LogManager.RuntimeLog("Set text to " + jsonObj.Message + " and title to " + jsonObj.Title);
                    }
                }
                catch (WebException)
                {
                }
            });
        }

        private bool _hasScAvatar;
        public void UpdateSocialClubAvatar()
        {
            try
            {
                if (string.IsNullOrEmpty(SocialClubName)) return;

                var uri = "https://a.rsg.sc/n/" + SocialClubName.ToLower();

                using (var wc = new ImpatientWebClient())
                {
                    wc.DownloadFile(uri, GTANInstallDir + "images\\scavatar.png");
                }
                _hasScAvatar = true;
            }
            catch (Exception ex)
            {
                LogManager.LogException(ex, "UPDATE SC AVATAR");
            }
        }

        private void AddToFavorites(string server)
        {
            if (string.IsNullOrWhiteSpace(server)) return;
            var split = server.Split(':');
            int port;
            if (split.Length < 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]) || !int.TryParse(split[1], out port)) return;
            PlayerSettings.FavoriteServers.Add(server);
            Util.Util.SaveSettings(GTANInstallDir + "\\settings.xml");
        }

        private void RemoveFromFavorites(string server)
        {
            PlayerSettings.FavoriteServers.Remove(server);
            Util.Util.SaveSettings(GTANInstallDir + "\\settings.xml");
        }

        private void AddServerToRecent(UIMenuItem server)
        {
            if (string.IsNullOrWhiteSpace(server.Description)) return;
            var split = server.Description.Split(':');
            int tmpPort;
            if (split.Length < 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]) || !int.TryParse(split[1], out tmpPort)) return;
            if (!PlayerSettings.RecentServers.Contains(server.Description))
            {
                PlayerSettings.RecentServers.Add(server.Description);
                if (PlayerSettings.RecentServers.Count > 20)
                    PlayerSettings.RecentServers.RemoveAt(0);
                Util.Util.SaveSettings(GTANInstallDir + "\\settings.xml");

                var item = new UIMenuItem(server.Text);
                item.Description = server.Description;
                item.SetRightLabel(server.RightLabel);
                item.SetLeftBadge(server.LeftBadge);
                item.Activated += (sender, selectedItem) =>
                {
                    if (IsOnServer())
                    {
                        Client.Disconnect("Switching servers");

                        if (Npcs != null)
                        {
                            Npcs.ToList().ForEach(pair => pair.Value.Clear());
                            Npcs.Clear();
                        }

                        while (IsOnServer()) Script.Yield();
                    }

                    bool pass = false;
                    if (server.LeftBadge == UIMenuItem.BadgeStyle.Lock)
                    {
                        pass = true;
                    }

                    var splt = server.Description.Split(':');
                    if (splt.Length < 2) return;
                    int port;
                    if (!int.TryParse(splt[1], out port)) return;
                    ConnectToServer(splt[0], port, pass);
                    MainMenu.TemporarilyHidden = true;
                    _connectTab.RefreshIndex();
                };
                _recentBrowser.Items.Add(item);
            }
        }

        private void AddServerToRecent(string server, string password)
        {
            if (string.IsNullOrWhiteSpace(server)) return;
            var split = server.Split(':');
            int tmpPort;
            if (split.Length < 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]) || !int.TryParse(split[1], out tmpPort)) return;
            if (!PlayerSettings.RecentServers.Contains(server))
            {
                PlayerSettings.RecentServers.Add(server);
                if (PlayerSettings.RecentServers.Count > 20)
                    PlayerSettings.RecentServers.RemoveAt(0);
                Util.Util.SaveSettings(GTANInstallDir + "\\settings.xml");

                var item = new UIMenuItem(server);
                item.Description = server;
                item.SetRightLabel(server);
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

                    var splt = server.Split(':');
                    if (splt.Length < 2) return;
                    int port;
                    if (!int.TryParse(splt[1], out port)) return;
                    ConnectToServer(splt[0], port, false, password);
                    MainMenu.TemporarilyHidden = true;
                    _connectTab.RefreshIndex();
                };
                _recentBrowser.Items.Add(item);
            }
        }

        private bool isIPLocal(string ipaddress)
        {
            String[] straryIPAddress = ipaddress.ToString().Split(new String[] { "." }, StringSplitOptions.RemoveEmptyEntries);
            try
            {
                int[] iaryIPAddress = new int[]
                {
                    int.Parse(straryIPAddress[0], CultureInfo.InvariantCulture),
                    int.Parse(straryIPAddress[1], CultureInfo.InvariantCulture),
                    int.Parse(straryIPAddress[2], CultureInfo.InvariantCulture),
                    int.Parse(straryIPAddress[3], CultureInfo.InvariantCulture)
                };
                if (iaryIPAddress[0] == 10 || iaryIPAddress[0] == 127 ||
                    (iaryIPAddress[0] == 192 && iaryIPAddress[1] == 168) ||
                    (iaryIPAddress[0] == 172 && (iaryIPAddress[1] >= 16 && iaryIPAddress[1] <= 31)))
                {
                    return true;
                }
                else
                {
                    // IP Address is "probably" public. This doesn't catch some VPN ranges like OpenVPN and Hamachi.
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        bool finished = true;
        private void RebuildServerBrowser()
        {
            if (!finished) return;
            finished = false;

            _Verified.Items.Clear();
            _serverBrowser.Items.Clear();
            _favBrowser.Items.Clear();
            _lanBrowser.Items.Clear();
            _recentBrowser.Items.Clear();

            _Verified.RefreshIndex();
            _serverBrowser.RefreshIndex();
            _favBrowser.RefreshIndex();
            _lanBrowser.RefreshIndex();
            _recentBrowser.RefreshIndex();

            _currentOnlinePlayers = 0;
            _currentOnlineServers = 0;

            var fetchThread = new Thread((ThreadStart)delegate
            {

                try
                {
                    if (Client == null)
                    {
                        var port = GetOpenUdpPort();
                        if (port == 0)
                        {
                            Util.Util.SafeNotify("No available UDP port was found.");
                            return;
                        }
                        _config.Port = port;
                        Client = new NetClient(_config);
                        Client.Start();
                    }

                    Client.DiscoverLocalPeers(Port);

                    LogManager.RuntimeLog("Contacting " + PlayerSettings.MasterServerAddress);

                    if (string.IsNullOrEmpty(PlayerSettings.MasterServerAddress))
                        return;

                    string response = String.Empty;
                    string responseVerified = String.Empty;
                    try
                    {
                        using (var wc = new ImpatientWebClient())
                        {
                            LogManager.RuntimeLog("Downloading response...");
                            response = wc.DownloadString(PlayerSettings.MasterServerAddress.Trim() + "/servers");
                            responseVerified = wc.DownloadString(PlayerSettings.MasterServerAddress.Trim() + "/verified");
                            LogManager.RuntimeLog("Downloaded " + response);
                        }
                    }
                    catch (Exception e)
                    {
                        Util.Util.SafeNotify("~r~~h~ERROR~h~~w~~n~Could not contact master server. Try again later.");
                        var logOutput = "===== EXCEPTION CONTACTING MASTER SERVER @ " + DateTime.UtcNow + " ======\n";
                        logOutput += "Message: " + e.Message;
                        logOutput += "\nData: " + e.Data;
                        logOutput += "\nStack: " + e.StackTrace;
                        logOutput += "\nSource: " + e.Source;
                        logOutput += "\nTarget: " + e.TargetSite;
                        if (e.InnerException != null)
                            logOutput += "\nInnerException: " + e.InnerException.Message;
                        logOutput += "\n";
                        LogManager.SimpleLog("masterserver", logOutput);
                    }

                    var list = new List<string>();
                    var listVerified = new List<string>();

                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        var dejson = JsonConvert.DeserializeObject<MasterServerList>(response) as MasterServerList;

                        if (dejson != null && dejson.list != null)
                        {
                            list.AddRange(dejson.list);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(responseVerified))
                    {
                        var dejson = JsonConvert.DeserializeObject<MasterServerList>(responseVerified) as MasterServerList;

                        if (dejson != null && dejson.list != null)
                        {
                            listVerified.AddRange(dejson.list);
                        }
                    }

                    foreach (var server in PlayerSettings.FavoriteServers)
                    {
                        if (!list.Contains(server)) list.Add(server);
                    }

                    foreach (var server in PlayerSettings.RecentServers)
                    {
                        if (!list.Contains(server)) list.Add(server);
                    }

                    list = list.Distinct().ToList();
                    listVerified = listVerified.Distinct().ToList();

                    foreach (var server in list)
                    {
                        var split = server.Split(':');
                        if (split.Length != 2) continue;
                        int port;
                        if (!int.TryParse(split[1], out port))
                            continue;

                        var item = new UIMenuItem(split[0] + ":" + split[1]);
                        item.Description = split[0] + ":" + split[1];

                        int lastIndx = 0;

                        try
                        {
                            if (!isIPLocal(Dns.GetHostAddresses(split[0])[0].ToString()))
                            {
                                if (_serverBrowser.Items.Count > 0)
                                    lastIndx = _serverBrowser.Index;

                                _serverBrowser.Items.Add(item);
                                _serverBrowser.Index = lastIndx;
                            }
                            else
                            {
                                if (!_lanBrowser.Items.Any(i => i.Description == item.Description))
                                {
                                    if (_lanBrowser.Items.Count > 0)
                                        lastIndx = _lanBrowser.Index;

                                    _lanBrowser.Items.Add(item);
                                    _lanBrowser.Index = lastIndx;
                                }
                            }

                            if (listVerified.Contains(server))
                            {
                                _Verified.Items.Add(item);
                                _Verified.Index = lastIndx;
                            }

                            if (PlayerSettings.RecentServers.Contains(server))
                            {
                                _recentBrowser.Items.Add(item);
                                _recentBrowser.Index = lastIndx;
                            }

                            if (PlayerSettings.FavoriteServers.Contains(server))
                            {
                                _favBrowser.Items.Add(item);
                                _favBrowser.Index = lastIndx;
                            }
                        }
                        catch (Exception e)
                        {
                            LogManager.LogException(e, "DISCOVERY EXCEPTION");
                        }
                    }

                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i != 0 && i % 10 == 0)
                        {
                            Thread.Sleep(1000);
                        }
                        var spl = list[i].Split(':');
                        if (spl.Length < 2) continue;
                        try
                        {
                            Client.DiscoverKnownPeer(Dns.GetHostAddresses(spl[0])[0].ToString(), int.Parse(spl[1]));
                        }
                        catch (Exception e)
                        {
                            LogManager.LogException(e, "DISCOVERY EXCEPTION");
                        }
                    }
                }
                catch (Exception e)
                {
                    LogManager.LogException(e, "DISCOVERY CRASH");
                }
                finished = true;
            });

            fetchThread.Start();
        }

        private void RebuildPlayersList()
        {
            _serverPlayers.Dictionary.Clear();

            List<SyncPed> list = null;
            lock (NetEntityHandler)
            {
                list = new List<SyncPed>(NetEntityHandler.ClientMap.Where(pair => pair.Value is SyncPed)
                    .Select(pair => pair.Value).Cast<SyncPed>().Take(20));
            }

            _serverPlayers.Dictionary.Add("Total Players", (list.Count + 1).ToString());

            var us =
                NetEntityHandler.ClientMap.Values.FirstOrDefault(p => p is RemotePlayer && ((RemotePlayer)p).LocalHandle == -2) as RemotePlayer;

            if (us == null)
                _serverPlayers.Dictionary.Add(PlayerSettings.DisplayName, ((int)(Latency * 1000)) + "ms");
            else
                _serverPlayers.Dictionary.Add(us.Name, ((int)(Latency * 1000)) + "ms");

            foreach (var ped in list)
            {
                try
                {
                    if (ped != null)
                    {
                        _serverPlayers.Dictionary.Add(ped.Name, ((int)(ped.Latency * 1000)) + "ms");
                    }

                }
                catch (ArgumentException) { }
            }
        }
        #endregion

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

        private TabMapItem _mainMapItem;
        private void BuildMainMenu()
        {
            MainMenu = new TabView("Cherry Multiplayer");
            MainMenu.CanLeave = false;
            MainMenu.MoneySubtitle = "Cherry MP " + CurrentVersion;

            _mainMapItem = new TabMapItem();

            #region Welcome Screen
            {
                _welcomePage = new TabWelcomeMessageItem("Welcome to Cherry Multiplayer", "Development branch.");
                MainMenu.Tabs.Add(_welcomePage);
            }
            #endregion

            #region ServerBrowser
            {
                var dConnect = new TabButtonArrayItem("Quick Connect");

                {
                    var ipButton = new TabButton();
                    ipButton.Text = "IP Address";
                    ipButton.Size = new Size(500, 40);
                    ipButton.Activated += (sender, args) =>
                    {
                        MainMenu.TemporarilyHidden = true;
                        var newIp = InputboxThread.GetUserInput(_clientIp ?? "", 30, TickSpinner);
                        _clientIp = newIp;
                        ipButton.Text = string.IsNullOrWhiteSpace(newIp) ? "IP Address" : newIp;
                        MainMenu.TemporarilyHidden = false;
                    };
                    dConnect.Buttons.Add(ipButton);
                }

                {
                    var ipButton = new TabButton();
                    ipButton.Text = "Port";
                    ipButton.Size = new Size(500, 40);
                    ipButton.Activated += (sender, args) =>
                    {
                        MainMenu.TemporarilyHidden = true;
                        var port = InputboxThread.GetUserInput(Port.ToString(), 30, TickSpinner);

                        if (string.IsNullOrWhiteSpace(port))
                        {
                            port = "4499";
                        }

                        int newPort;
                        if (!int.TryParse(port, out newPort))
                        {
                            Util.Util.SafeNotify("Wrong port format!");
                            MainMenu.TemporarilyHidden = false;
                            return;
                        }
                        Port = newPort;
                        ipButton.Text = Port.ToString();
                        MainMenu.TemporarilyHidden = false;
                    };
                    dConnect.Buttons.Add(ipButton);
                }

                {
                    var ipButton = new TabButton();
                    ipButton.Text = "Password";
                    ipButton.Size = new Size(500, 40);
                    ipButton.Activated += (sender, args) =>
                    {
                        MainMenu.TemporarilyHidden = true;
                        var newIp = InputboxThread.GetUserInput("", 30, TickSpinner);
                        MainMenu.TemporarilyHidden = false;
                        _QCpassword = newIp;
                        ipButton.Text = string.IsNullOrWhiteSpace(newIp) ? "Password" : "*******";
                    };
                    dConnect.Buttons.Add(ipButton);
                }

                {
                    var ipButton = new TabButton();
                    ipButton.Text = "Connect";
                    ipButton.Size = new Size(500, 40);
                    ipButton.Activated += (sender, args) =>
                    {
                        var isPassworded = false;
                        if(!string.IsNullOrWhiteSpace(_QCpassword)) isPassworded = true;
                        
                        AddServerToRecent(_clientIp + ":" + Port, _QCpassword);
                        ConnectToServer(_clientIp, Port, isPassworded, _QCpassword);
                        MainMenu.TemporarilyHidden = true;
                    };
                    dConnect.Buttons.Add(ipButton);
                }

                _Verified = new TabInteractiveListItem("Verified", new List<UIMenuItem>());
                _serverBrowser = new TabInteractiveListItem("Internet", new List<UIMenuItem>());
                _favBrowser = new TabInteractiveListItem("Favorites", new List<UIMenuItem>());
                _lanBrowser = new TabInteractiveListItem("Local Network", new List<UIMenuItem>());
                _recentBrowser = new TabInteractiveListItem("Recent", new List<UIMenuItem>());

                _connectTab = new TabSubmenuItem("connect", new List<TabItem>() { dConnect, _favBrowser, _Verified, _serverBrowser, _lanBrowser, _recentBrowser });

                MainMenu.AddTab(_connectTab);
                _connectTab.DrawInstructionalButtons += (sender, args) =>
                {
                    MainMenu.DrawInstructionalButton(4, Control.Jump, "Refresh");

                    if (Game.IsControlJustPressed(0, Control.Jump))
                    {
                        RebuildServerBrowser();
                    }
                    #region Verified, Iternet, LAN and Recent
                    if (_connectTab.Index == 2 && _connectTab.Items[2].Focused || _connectTab.Index == 3 && _connectTab.Items[3].Focused || _connectTab.Index == 4 && _connectTab.Items[4].Focused || _connectTab.Index == 5 && _connectTab.Items[5].Focused)
                    {
                        MainMenu.DrawInstructionalButton(5, Control.Enter, "Add to Favorites");
                        if (Game.IsControlJustPressed(0, Control.Enter))
                        {
                            var selectedServer = _serverBrowser.Items[_serverBrowser.Index];
                            if (_connectTab.Index == 2 && _connectTab.Items[2].Focused)
                            {
                                selectedServer = _Verified.Items[_Verified.Index];
                            }
                            else if (_connectTab.Index == 4 && _connectTab.Items[4].Focused)
                            {
                                selectedServer = _lanBrowser.Items[_lanBrowser.Index];
                            }
                            else if (_connectTab.Index == 5 && _connectTab.Items[5].Focused)
                            {
                                selectedServer = _recentBrowser.Items[_recentBrowser.Index];
                            }
                            selectedServer.SetRightBadge(UIMenuItem.BadgeStyle.None);
                            if (PlayerSettings.FavoriteServers.Contains(selectedServer.Description))
                            {
                                var favItem = _favBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description);
                                if (favItem != null)
                                {
                                    selectedServer.SetRightBadge(UIMenuItem.BadgeStyle.None);
                                    RemoveFromFavorites(selectedServer.Description);
                                    _favBrowser.Items.Remove(favItem);
                                    _favBrowser.RefreshIndex();
                                }
                            }
                            else
                            {
                                AddToFavorites(selectedServer.Description);
                                selectedServer.SetRightBadge(UIMenuItem.BadgeStyle.Star);
                                var item = new UIMenuItem(selectedServer.Text);
                                item.Description = selectedServer.Description;
                                item.SetRightLabel(selectedServer.RightLabel);
                                item.SetLeftBadge(selectedServer.LeftBadge);
                                item.Activated += (faf, selectedItem) =>
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
                                    if (selectedServer.LeftBadge == UIMenuItem.BadgeStyle.Lock)
                                    {
                                        pass = true;
                                    }

                                    var splt = selectedServer.Description.Split(':');

                                    if (splt.Length < 2) return;
                                    int port;
                                    if (!int.TryParse(splt[1], out port)) return;

                                    ConnectToServer(splt[0], port, pass);
                                    MainMenu.TemporarilyHidden = true;
                                    _connectTab.RefreshIndex();
                                };
                                _favBrowser.Items.Add(item);
                            }
                        }
                    }
                    #endregion
                    #region Favorites
                    if (_connectTab.Index == 1 && _connectTab.Items[1].Focused)
                    {
                        MainMenu.DrawInstructionalButton(6, Control.NextCamera, "Add Server");

                        #region Add server
                        if (Game.IsControlJustPressed(0, Control.NextCamera))
                        {

                            MainMenu.TemporarilyHidden = true;
                            var serverIp = InputboxThread.GetUserInput("Server IP(:Port)", 40, TickSpinner);

                            if (serverIp.Contains("Server IP(:Port)") || string.IsNullOrWhiteSpace(serverIp))
                            {
                                Util.Util.SafeNotify("Incorrect input!");
                                MainMenu.TemporarilyHidden = false;
                                return;
                            }
                            else if (!serverIp.Contains(":"))
                            {
                                serverIp += ":4499";
                            }
                            MainMenu.TemporarilyHidden = false;

                            if (!PlayerSettings.FavoriteServers.Contains(serverIp))
                            {
                                AddToFavorites(serverIp);
                                var item = new UIMenuItem(serverIp);
                                item.Description = serverIp;
                                _favBrowser.Items.Add(item);
                            }
                            else
                            {
                                Util.Util.SafeNotify("Server already on the list");
                                return;
                            }
                        }
                        #endregion

                        MainMenu.DrawInstructionalButton(5, Control.Enter, "Remove");

                        #region Remove
                        if (Game.IsControlJustPressed(0, Control.Enter))
                        {
                            var selectedServer = _favBrowser.Items[_favBrowser.Index];
                            var favItem = _favBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description);

                            if (_Verified.Items.FirstOrDefault(i => i.Description == selectedServer.Description) != null)
                                _Verified.Items.FirstOrDefault(i => i.Description == selectedServer.Description).SetRightBadge(UIMenuItem.BadgeStyle.None);

                            if (_serverBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description) != null)
                                _serverBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description).SetRightBadge(UIMenuItem.BadgeStyle.None);

                            if (_lanBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description) != null)
                                _lanBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description).SetRightBadge(UIMenuItem.BadgeStyle.None);

                            if (_recentBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description) != null)
                                _recentBrowser.Items.FirstOrDefault(i => i.Description == selectedServer.Description).SetRightBadge(UIMenuItem.BadgeStyle.None);

                            if (favItem != null)
                            {
                                Util.Util.SafeNotify("Server removed from Favorites!");
                                RemoveFromFavorites(selectedServer.Description);
                                _favBrowser.Items.Remove(favItem);
                                _favBrowser.RefreshIndex();
                            }
                        }
                        #endregion


                    }
                    #endregion
                };
                CherryDiscord.CherryDiscord.InMenuDiscordInitialize(CurrentVersion.ToString());
                CherryDiscord.CherryDiscord.InMenuDiscordUpdatePresence();
            }
            #endregion

            #region Settings

            {
                #region General Menu
                var GeneralMenu = new TabInteractiveListItem("General", new List<UIMenuItem>());
                {
                    var nameItem = new UIMenuItem("Display Name");
                    nameItem.SetRightLabel(PlayerSettings.DisplayName);
                    nameItem.Activated += (sender, item) =>
                    {
                        if (IsOnServer())
                        {
                            GTA.UI.Screen.ShowNotification("You cannot change your Display Name while connected to a server.");
                            return;
                        }
                        MainMenu.TemporarilyHidden = true;
                        var newName = InputboxThread.GetUserInput(PlayerSettings.DisplayName ?? "Enter a new Display Name", 32, TickSpinner);
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            if (newName.Length > 32)
                                newName = newName.Substring(0, 32);

                            newName = newName.Replace(' ', '_');

                            PlayerSettings.DisplayName = newName;
                            SaveSettings();
                            nameItem.SetRightLabel(newName);
                        }
                        MainMenu.TemporarilyHidden = false;
                    };

                    GeneralMenu.Items.Add(nameItem);
                }
                {
                    var debugItem = new UIMenuCheckboxItem("Show Game FPS Counter", PlayerSettings.ShowFPS);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.ShowFPS = @checked;
                        DebugInfo.FPS = @checked;
                        SaveSettings();
                    };
                    GeneralMenu.Items.Add(debugItem);
                }
                {
                    var debugItem = new UIMenuCheckboxItem("Disable Rockstar Editor", PlayerSettings.DisableRockstarEditor);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.DisableRockstarEditor = @checked;
                        SaveSettings();
                    };
                    GeneralMenu.Items.Add(debugItem);
                }
                {
                    var debugItem = new UIMenuCheckboxItem("Allow Webcam/Microphone Streaming (Requires restart)", PlayerSettings.MediaStream);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.MediaStream = @checked;
                        SaveSettings();
                    };
                    GeneralMenu.Items.Add(debugItem);
                }
                {
                    var nameItem = new UIMenuItem("Update Channel");
                    nameItem.SetRightLabel(PlayerSettings.UpdateChannel);
                    nameItem.Activated += (sender, item) =>
                    {
                        MainMenu.TemporarilyHidden = true;
                        var newName = InputboxThread.GetUserInput(PlayerSettings.UpdateChannel ?? "stable", 40, TickSpinner);
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            PlayerSettings.UpdateChannel = newName;
                            SaveSettings();
                            nameItem.SetRightLabel(newName);
                        }
                        MainMenu.TemporarilyHidden = false;
                    };

                    GeneralMenu.Items.Add(nameItem);
                }
                #endregion
                #region Chat
                var ChatboxMenu = new TabInteractiveListItem("Chat", new List<UIMenuItem>());

                {
                    var chatItem = new UIMenuCheckboxItem("Enable Timestamp", PlayerSettings.Timestamp);
                    chatItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.Timestamp = @checked;
                        SaveSettings();
                    };
                    ChatboxMenu.Items.Add(chatItem);
                }
                {
                    var chatItem = new UIMenuCheckboxItem("Use Military Time", PlayerSettings.Militarytime);
                    chatItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.Militarytime = @checked;
                        SaveSettings();
                    };
                    ChatboxMenu.Items.Add(chatItem);
                }
                {
                    var chatItem = new UIMenuCheckboxItem("Scale Chatbox With Safezone", PlayerSettings.ScaleChatWithSafezone);
                    chatItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.ScaleChatWithSafezone = @checked;
                        SaveSettings();
                    };
                    ChatboxMenu.Items.Add(chatItem);
                }

                {
                    var chatItem = new UIMenuItem("Horizontal Chatbox Offset");
                    chatItem.SetRightLabel(PlayerSettings.ChatboxXOffset.ToString());
                    chatItem.Activated += (sender, item) =>
                    {
                        MainMenu.TemporarilyHidden = true;
                        var strInput = InputboxThread.GetUserInput(PlayerSettings.ChatboxXOffset.ToString(),
                            10, TickSpinner);

                        int newSetting;
                        if (!int.TryParse(strInput, out newSetting))
                        {
                            Util.Util.SafeNotify("Input was not in the correct format.");
                            MainMenu.TemporarilyHidden = false;
                            return;
                        }
                        MainMenu.TemporarilyHidden = false;
                        PlayerSettings.ChatboxXOffset = newSetting;
                        chatItem.SetRightLabel(PlayerSettings.ChatboxXOffset.ToString());
                        SaveSettings();
                    };
                    ChatboxMenu.Items.Add(chatItem);
                }

                {
                    var chatItem = new UIMenuItem("Vertical Chatbox Offset");
                    chatItem.SetRightLabel(PlayerSettings.ChatboxYOffset.ToString());
                    chatItem.Activated += (sender, item) =>
                    {
                        MainMenu.TemporarilyHidden = true;
                        var strInput = InputboxThread.GetUserInput(PlayerSettings.ChatboxYOffset.ToString(),
                            10, TickSpinner);

                        int newSetting;
                        if (!int.TryParse(strInput, out newSetting))
                        {
                            Util.Util.SafeNotify("Input was not in the correct format.");
                            MainMenu.TemporarilyHidden = false;
                            return;
                        }
                        MainMenu.TemporarilyHidden = false;
                        PlayerSettings.ChatboxYOffset = newSetting;
                        chatItem.SetRightLabel(PlayerSettings.ChatboxYOffset.ToString());
                        SaveSettings();
                    };
                    ChatboxMenu.Items.Add(chatItem);
                }

                {
                    var chatItem = new UIMenuCheckboxItem("Use Classic Chatbox", PlayerSettings.UseClassicChat);
                    chatItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.UseClassicChat = @checked;
                        SaveSettings();
                    };
                    ChatboxMenu.Items.Add(chatItem);
                }
                #endregion

                //#region Graphics Menu
                //var GraphicsMenu = new TabInteractiveListItem("Graphics", new List<UIMenuItem>());

                //{
                //    var cityDen = new UIMenuItem("City Density");
                //    cityDen.SetRightLabel(GameSettings.Graphics.CityDensity.Value.ToString());
                //    GraphicsMenu.Items.Add(cityDen);

                //    cityDen.Activated += (sender, item) =>
                //    {
                //        var strInput = InputboxThread.GetUserInput(GameSettings.Graphics.CityDensity.Value.ToString(),
                //            10, TickSpinner);

                //        double newSetting;
                //        if (!double.TryParse(strInput, out newSetting))
                //        {
                //            Util.Util.SafeNotify("Input was not in the correct format.");
                //            return;
                //        }

                //        GameSettings.Graphics.CityDensity.Value = newSetting;
                //        Misc.GameSettings.SaveSettings(GameSettings);
                //    };
                //}

                //{
                //    var cityDen = new UIMenuItem("Depth Of Field");
                //    cityDen.SetRightLabel(GameSettings.Graphics.DoF.Value.ToString());
                //    GraphicsMenu.Items.Add(cityDen);

                //    cityDen.Activated += (sender, item) =>
                //    {
                //        var strInput = InputboxThread.GetUserInput(GameSettings.Graphics.DoF.Value.ToString(),
                //            10, TickSpinner);

                //        bool newSetting;
                //        if (!bool.TryParse(strInput, out newSetting))
                //        {
                //            Util.Util.SafeNotify("Input was not in the correct format.");
                //            return;
                //        }

                //        GameSettings.Graphics.DoF.Value = newSetting;
                //        Misc.GameSettings.SaveSettings(GameSettings);
                //    };
                //}

                //{
                //    var cityDen = new UIMenuItem("Grass Quality");
                //    cityDen.SetRightLabel(GameSettings.Graphics.GrassQuality.Value.ToString());
                //    GraphicsMenu.Items.Add(cityDen);

                //    cityDen.Activated += (sender, item) =>
                //    {
                //        var strInput = InputboxThread.GetUserInput(GameSettings.Graphics.GrassQuality.Value.ToString(),
                //            10, TickSpinner);

                //        int newSetting;
                //        if (!int.TryParse(strInput, out newSetting))
                //        {
                //            Util.Util.SafeNotify("Input was not in the correct format.");
                //            return;
                //        }

                //        GameSettings.Graphics.GrassQuality.Value = newSetting;
                //        Misc.GameSettings.SaveSettings(GameSettings);
                //    };
                //}

                //{
                //    var cityDen = new UIMenuItem("MSAA");
                //    cityDen.SetRightLabel(GameSettings.Graphics.MSAA.Value.ToString());
                //    GraphicsMenu.Items.Add(cityDen);

                //    cityDen.Activated += (sender, item) =>
                //    {
                //        var strInput = InputboxThread.GetUserInput(GameSettings.Graphics.MSAA.Value.ToString(),
                //            10, TickSpinner);

                //        int newSetting;
                //        if (!int.TryParse(strInput, out newSetting))
                //        {
                //            Util.Util.SafeNotify("Input was not in the correct format.");
                //            return;
                //        }

                //        GameSettings.Graphics.MSAA.Value = newSetting;
                //        Misc.GameSettings.SaveSettings(GameSettings);
                //    };
                //}

                //#endregion

                ////#region Display Menu
                //var DisplayMenu = new TabInteractiveListItem("Display", new List<UIMenuItem>());
                //{
                //    var nameItem = new UIMenuCheckboxItem("Start in Borderless Windowed", PlayerSettings.AutosetBorderlessWindowed);

                //    nameItem.CheckboxEvent += (sender, chequed) =>
                //    {
                //        PlayerSettings.AutosetBorderlessWindowed = chequed;
                //        SaveSettings();
                //    };

                //    DisplayMenu.Items.Add(nameItem);
                //}

                //{
                //    var cityDen = new UIMenuItem("City Density");
                //    cityDen.SetRightLabel(GameSettings.Video.Windowed.Value.ToString());
                //    DisplayMenu.Items.Add(cityDen);

                //    cityDen.Activated += (sender, item) =>
                //    {
                //        var strInput = InputboxThread.GetUserInput(GameSettings.Video.Windowed.Value.ToString(),
                //            10, TickSpinner);

                //        int newSetting;
                //        if (!int.TryParse(strInput, out newSetting))
                //        {
                //            Util.Util.SafeNotify("Input was not in the correct format.");
                //            return;
                //        }

                //        GameSettings.Video.Windowed.Value = newSetting;
                //        Misc.GameSettings.SaveSettings(GameSettings);
                //    };
                //}

                //{
                //    var cityDen = new UIMenuItem("Vertical Sync");
                //    cityDen.SetRightLabel(GameSettings.Video.VSync.Value.ToString());
                //    DisplayMenu.Items.Add(cityDen);

                //    cityDen.Activated += (sender, item) =>
                //    {
                //        var strInput = InputboxThread.GetUserInput(GameSettings.Video.VSync.Value.ToString(),
                //            10, TickSpinner);

                //        int newSetting;
                //        if (!int.TryParse(strInput, out newSetting))
                //        {
                //            Util.Util.SafeNotify("Input was not in the correct format.");
                //            return;
                //        }

                //        GameSettings.Video.VSync.Value = newSetting;
                //        Misc.GameSettings.SaveSettings(GameSettings);
                //    };
                //}
                //#endregion

                #region Experimental
                var ExpMenu = new TabInteractiveListItem("Experimental", new List<UIMenuItem>());
                {

                    var expItem = new UIMenuCheckboxItem("Disable Chromium Embedded Framework (Requires restart)", PlayerSettings.DisableCEF);
                    expItem.CheckboxEvent += (sender, @checked) =>
                    {
                        PlayerSettings.DisableCEF = @checked;
                        SaveSettings();
                    };
                    ExpMenu.Items.Add(expItem);
                }
                //{
                //    var expItem = new UIMenuItem("Chromium Embedded Framework Framerate (requires reconnect)");

                //    if(PlayerSettings.CEFfps == 0)
                //    {
                //        expItem.SetRightLabel("Auto (0)");
                //    }
                //    else
                //    {
                //        expItem.SetRightLabel(PlayerSettings.CEFfps.ToString());
                //    }

                //    expItem.Activated += (sender, item) =>
                //    {
                //        MainMenu.TemporarilyHidden = true;
                //        var strInput = InputboxThread.GetUserInput(PlayerSettings.CEFfps.ToString(),
                //            10, TickSpinner);
                //        int newSetting;
                //        if (!int.TryParse(strInput, out newSetting) || newSetting < 0 || newSetting > 120)
                //        {
                //            Util.Util.SafeNotify("Input was not in the correct format.");
                //            MainMenu.TemporarilyHidden = false;
                //            return;
                //        }
                //        expItem.SetRightLabel(PlayerSettings.CEFfps.ToString());
                //        if (newSetting == 0)
                //        {
                //            CEFManager.FPS = (int)Game.FPS;
                //            expItem.SetRightLabel("Auto (0)");
                //        }
                //        else
                //        {
                //            CEFManager.FPS = newSetting;
                //            expItem.SetRightLabel(PlayerSettings.CEFfps.ToString());

                //        }
                //        PlayerSettings.CEFfps = newSetting;
                //        MainMenu.TemporarilyHidden = false;
                //        SaveSettings();
                //    };
                //    ExpMenu.Items.Add(expItem);
                //}

                #endregion

                #region Debug Menu
                var DebugMenu = new TabInteractiveListItem("Debug", new List<UIMenuItem>());
                {
                    {
                        var expItem = new UIMenuCheckboxItem("Enable CEF Development Tools (http://localhost:9222) [Restart required]", PlayerSettings.CEFDevtool);
                        expItem.CheckboxEvent += (sender, @checked) =>
                        {
                            PlayerSettings.CEFDevtool = @checked;
                            SaveSettings();
                        };
                        DebugMenu.Items.Add(expItem);
                    }

                    {
                        var expItem = new UIMenuCheckboxItem("Enable Debug mode (Requires an extra tool)", PlayerSettings.DebugMode);
                        expItem.CheckboxEvent += (sender, @checked) =>
                        {
                            PlayerSettings.DebugMode = @checked;
                            SaveSettings();
                        };
                        DebugMenu.Items.Add(expItem);
                    }

                    {
                        var expItem = new UIMenuCheckboxItem("Save Debug to File (Huge performance impact)", SaveDebugToFile);
                        expItem.CheckboxEvent += (sender, @checked) =>
                        {
                            SaveDebugToFile = @checked;
                            SaveSettings();
                        };
                        DebugMenu.Items.Add(expItem);
                    }

                    {
                        var debugItem = new UIMenuCheckboxItem("Remove Game Entities", RemoveGameEntities);
                        debugItem.CheckboxEvent += (sender, @checked) =>
                        {
                            RemoveGameEntities = @checked;
                        };
                        DebugMenu.Items.Add(debugItem);
                    }

                    {
                        var debugItem = new UIMenuCheckboxItem("Show Streamer Debug Data", DebugInfo.StreamerDebug);
                        debugItem.CheckboxEvent += (sender, @checked) =>
                        {
                            DebugInfo.StreamerDebug = @checked;
                        };
                        DebugMenu.Items.Add(debugItem);
                    }

                    {
                        var debugItem = new UIMenuCheckboxItem("Disable Nametag Draw", ToggleNametagDraw);
                        debugItem.CheckboxEvent += (sender, @checked) =>
                        {
                            ToggleNametagDraw = @checked;
                        };
                        DebugMenu.Items.Add(debugItem);
                    }

                    {
                        var debugItem = new UIMenuCheckboxItem("Disable Position Update", TogglePosUpdate);
                        debugItem.CheckboxEvent += (sender, @checked) =>
                        {
                            TogglePosUpdate = @checked;
                        };
                        DebugMenu.Items.Add(debugItem);
                    }

                }


#if DEBUG

               
                    var debugItem = new UIMenuCheckboxItem("Debug", false);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        display = @checked;
                        if (!display)
                        {
                            if (mainPed != null) mainPed.Delete();
                            if (mainVehicle != null) mainVehicle.Delete();
                            if (_debugSyncPed != null)
                            {
                                _debugSyncPed.Clear();
                                _debugSyncPed = null;
                            }
                        }
                    };
                    DebugMenu.Items.Add(debugItem);
                }

                {
                    var debugItem = new UIMenuCheckboxItem("Debug Window", false);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        _debugWindow = @checked;
                    };
                    DebugMenu.Items.Add(debugItem);
                }



                {
                    var debugItem = new UIMenuCheckboxItem("Write Debug Info To File", false);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        WriteDebugLog = @checked;
                    };
                    DebugMenu.Items.Add(debugItem);
                }

                {
                    var debugItem = new UIMenuCheckboxItem("Subtitle Debug", false);
                    debugItem.CheckboxEvent += (sender, @checked) =>
                    {
                        SlowDownClientForDebug = @checked;
                    };
                    DebugMenu.Items.Add(debugItem);
                }

                
#endif
                    #endregion

                    var welcomeItem = new TabSubmenuItem("settings", new List<TabItem>()
                {
                    GeneralMenu,
                    ChatboxMenu,
                    //DisplayMenu,
                    //GraphicsMenu,
                    ExpMenu,
                    DebugMenu
                });
                MainMenu.AddTab(welcomeItem);
            }

            #endregion

            #region Host
            {
#if ATTACHSERVER
                var settingsPath = GTANInstallDir + "\\server\\settings.xml";

                if (File.Exists(settingsPath) && Directory.Exists(GTANInstallDir + "\\server\\resources"))
                {
                    var settingsFile = ServerSettings.ReadSettings(settingsPath);

                    var hostStart = new TabTextItem("Start Server", "Host a Session",
                        "Press [ENTER] to start your own server!");
                    hostStart.CanBeFocused = false;

                    hostStart.Activated += (sender, args) =>
                    {
                        if (IsOnServer() || _serverProcess != null)
                        {
                            GTA.UI.Screen.ShowNotification("~b~~h~Cherry Multiplayer~h~~w~~n~Leave the current server first!");
                            return;
                        }

                        GTA.UI.Screen.ShowNotification("~b~~h~Cherry Multiplayer~h~~w~~n~Starting server...");
                        var startSettings = new ProcessStartInfo(GTANInstallDir + "\\server\\CherryMPServer.exe");
                        startSettings.CreateNoWindow = true;
                        startSettings.RedirectStandardOutput = true;
                        startSettings.UseShellExecute = false;
                        startSettings.WorkingDirectory = GTANInstallDir + "\\server";

                        _serverProcess = Process.Start(startSettings);

                        Script.Wait(5000);
                        ConnectToServer("127.0.0.1", settingsFile.Port);
                    };

                    var settingsList = new List<UIMenuItem>();

                    {
                        var serverName = new UIMenuItem("Server Name");
                        settingsList.Add(serverName);
                        serverName.SetRightLabel(settingsFile.Name);
                        serverName.Activated += (sender, item) =>
                        {
                            MainMenu.TemporarilyHidden = true;
                            var newName = InputboxThread.GetUserInput(settingsFile.Name, 40, TickSpinner);
                            if (string.IsNullOrWhiteSpace(newName))
                            {
                                GTA.UI.Screen.ShowNotification(
                                    "~b~~h~Cherry Multiplayer~h~~w~~n~Server name must not be empty!");
                                MainMenu.TemporarilyHidden = false;
                                return;
                            }
                            serverName.SetRightLabel(newName);
                            settingsFile.Name = newName;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                            MainMenu.TemporarilyHidden = false;
                        };
                    }

                    {
                        var serverName = new UIMenuItem("Password");
                        settingsList.Add(serverName);
                        serverName.SetRightLabel(settingsFile.Password);
                        serverName.Activated += (sender, item) =>
                        {
                            MainMenu.TemporarilyHidden = true;
                            var newName = InputboxThread.GetUserInput(settingsFile.Password, 40, TickSpinner);
                            serverName.SetRightLabel(newName);
                            settingsFile.Password = newName;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                            MainMenu.TemporarilyHidden = false;
                        };
                    }

                    {
                        var serverName = new UIMenuItem("Player Limit");
                        settingsList.Add(serverName);
                        serverName.SetRightLabel(settingsFile.MaxPlayers.ToString());
                        serverName.Activated += (sender, item) =>
                        {
                            MainMenu.TemporarilyHidden = true;
                            var newName = InputboxThread.GetUserInput(settingsFile.MaxPlayers.ToString(), 40,
                                TickSpinner);
                            int newLimit;
                            if (string.IsNullOrWhiteSpace(newName) ||
                                !int.TryParse(newName, NumberStyles.Integer, CultureInfo.InvariantCulture, out newLimit))
                            {
                                GTA.UI.Screen.ShowNotification(
                                    "~b~~h~Cherry Multiplayer~h~~w~~n~Invalid input for player limit!");
                                MainMenu.TemporarilyHidden = false;
                                return;
                            }

                            serverName.SetRightLabel(newName);
                            settingsFile.MaxPlayers = newLimit;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                            MainMenu.TemporarilyHidden = false;
                        };
                    }

                    {
                        var serverName = new UIMenuItem("Port");
                        settingsList.Add(serverName);
                        serverName.SetRightLabel(settingsFile.Port.ToString());
                        serverName.Activated += (sender, item) =>
                        {
                            MainMenu.TemporarilyHidden = true;
                            var newName = InputboxThread.GetUserInput(settingsFile.Port.ToString(), 40, TickSpinner);
                            int newLimit;
                            if (string.IsNullOrWhiteSpace(newName) ||
                                !int.TryParse(newName, NumberStyles.Integer, CultureInfo.InvariantCulture, out newLimit) ||
                                newLimit < 1024)
                            {
                                GTA.UI.Screen.ShowNotification(
                                    "~b~~h~Cherry Multiplayer~h~~w~~n~Invalid input for server port!");
                                MainMenu.TemporarilyHidden = false;
                                return;
                            }

                            serverName.SetRightLabel(newName);
                            settingsFile.Port = newLimit;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                            MainMenu.TemporarilyHidden = false;
                        };
                    }

                    {
                        var serverName = new UIMenuCheckboxItem("Announce to Master Server", settingsFile.Announce);
                        settingsList.Add(serverName);
                        serverName.CheckboxEvent += (sender, item) =>
                        {
                            settingsFile.Announce = item;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                        };
                    }

                    {
                        var serverName = new UIMenuCheckboxItem("Auto Portforward (UPnP)", settingsFile.UseUPnP);
                        settingsList.Add(serverName);
                        serverName.CheckboxEvent += (sender, item) =>
                        {
                            settingsFile.UseUPnP = item;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                        };
                    }

                    {
                        var serverName = new UIMenuCheckboxItem("Use Access Control List", settingsFile.UseACL);
                        settingsList.Add(serverName);
                        serverName.CheckboxEvent += (sender, item) =>
                        {
                            settingsFile.UseACL = item;
                            ServerSettings.WriteSettings(settingsPath, settingsFile);
                        };
                    }

                    var serverSettings = new TabInteractiveListItem("Server Settings", settingsList);

                    var resourcesList = new List<UIMenuItem>();
                    {
                        var resourceRoot = GTANInstallDir + "\\server\\resources";
                        var folders = Directory.GetDirectories(resourceRoot);

                        foreach (var folder in folders)
                        {
                            var resourceName = Path.GetFileName(folder);

                            var item = new UIMenuCheckboxItem(resourceName,
                                settingsFile.Resources.Any(res => res.Path == resourceName));
                            resourcesList.Add(item);
                            item.CheckboxEvent += (sender, @checked) =>
                            {
                                if (@checked)
                                {
                                    settingsFile.Resources.Add(new ServerSettings.SettingsResFilepath()
                                    {
                                        Path = resourceName
                                    });
                                }
                                else
                                {
                                    settingsFile.Resources.Remove(
                                        settingsFile.Resources.FirstOrDefault(r => r.Path == resourceName));
                                }
                                ServerSettings.WriteSettings(settingsPath, settingsFile);
                            };
                        }
                    }

                    var resources = new TabInteractiveListItem("Resources", resourcesList);


                    var welcomeItem = new TabSubmenuItem("host",
                        new List<TabItem> { hostStart, serverSettings, resources });
                    MainMenu.AddTab(welcomeItem);
                }
#endif
            }
            #endregion

            #region Quit
            {
                var welcomeItem = new TabTextItem("Quit", "Quit Cherry Multiplayer", "Are you sure you want to quit Cherry Multiplayer and return to desktop?");
                welcomeItem.CanBeFocused = false;
                welcomeItem.Activated += (sender, args) =>
                {
                    if (Client != null && IsOnServer()) Client.Disconnect("Quit");
                    CEFManager.Dispose();
                    CEFManager.DisposeCef();
                    Script.Wait(500);
                    //Environment.Exit(0);
                    Process.GetProcessesByName("GTA5")[0].Kill();
                    Process.GetCurrentProcess().Kill();
                };
                MainMenu.Tabs.Add(welcomeItem);
            }
            #endregion

            #region Current Server Tab

            #region Players
            _serverPlayers = new TabItemSimpleList("Players", new Dictionary<string, string>());
            #endregion

            var favTab = new TabTextItem("Favorite", "Add to Favorites", "Add the current server to favorites.");
            favTab.CanBeFocused = false;
            favTab.Activated += (sender, args) =>
            {
                var serb = _currentServerIp + ":" + _currentServerPort;
                AddToFavorites(_currentServerIp + ":" + _currentServerPort);
                var item = new UIMenuItem(serb);
                item.Description = serb;
                Util.Util.SafeNotify("Server added to favorites!");
                item.Activated += (faf, selectedItem) =>
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

                    var splt = serb.Split(':');

                    if (splt.Length < 2) return;
                    int port;
                    if (!int.TryParse(splt[1], out port)) return;

                    ConnectToServer(splt[0], port);
                    MainMenu.TemporarilyHidden = true;
                    _connectTab.RefreshIndex();
                    AddServerToRecent(serb, "");
                };
                _favBrowser.Items.Add(item);
            };

            var dcItem = new TabTextItem("Disconnect", "Disconnect", "Disconnect from the current server.");
            dcItem.CanBeFocused = false;
            dcItem.Activated += (sender, args) =>
            {
                if (Client != null) Client.Disconnect("Quit");
            };

            _statsItem = new TabTextItem("Statistics", "Network Statistics", "");
            _statsItem.CanBeFocused = false;

            _serverItem = new TabSubmenuItem("server", new List<TabItem>() { _serverPlayers, favTab, _statsItem, dcItem });
            _serverItem.Parent = MainMenu;
            #endregion

            MainMenu.RefreshIndex();
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

            GTA.UI.Screen.ShowSubtitle("Downloading Map...", 500000);

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
                        if (NetEntityHandler.NetToEntity(pair.Key)?.Handle == Game.Player.Character.Handle)
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
            if (scripts.ClientsideScripts != null)
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

                var obj = new PedData();
                obj.AimCoords = aimCoord.ToLVector();
                obj.Position = (player.Position + new Vector3(10, 0, 0)).ToLVector();
                obj.Quaternion = player.Rotation.ToLVector();
                obj.PedArmor = (byte)player.Armor;
                obj.PedModelHash = player.Model.Hash;
                obj.WeaponHash = (int)player.Weapons.Current.Hash;
                obj.WeaponAmmo = (int)player.Weapons.Current.Ammo;
                obj.PlayerHealth = (byte)Util.Util.Clamp(0, player.Health, 255);

                obj.Velocity = player.Velocity.ToLVector();
                obj.Flag = 0;
                obj.Speed = (byte)GetPedWalkingSpeed(player);
                obj.Latency = _debugInterval / 1000f;

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

                var obj = new VehicleData();
                obj.Position = (veh.Position + new Vector3(10, 0, 0)).ToLVector();


                obj.VehicleHandle = NetEntityHandler.EntityToNet(player.CurrentVehicle.Handle);
                obj.Quaternion = veh.Rotation.ToLVector();
                obj.PedModelHash = player.Model.Hash;
                obj.PlayerHealth = (byte)Util.Util.Clamp(0, player.Health, 255);
                obj.VehicleHealth = veh.EngineHealth;
                obj.Velocity = veh.Velocity.ToLVector();
                obj.PedArmor = (byte)player.Armor;
                obj.RPM = veh.CurrentRPM;
                obj.VehicleSeat = (short)Util.Util.GetPedSeat(player);
                obj.Flag = 0;
                obj.Steering = veh.SteeringAngle;
                obj.Latency = _debugInterval / 1000f;

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

                if (!WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.Util.GetPedSeat(Game.Player.Character)) && WeaponDataProvider.DoesVehicleSeatHaveMountedGuns((VehicleHash)veh.Model.Hash))
                {
                    obj.WeaponHash = GetCurrentVehicleWeaponHash(Game.Player.Character);
                    if (Game.IsEnabledControlPressed(0, Control.VehicleFlyAttack))
                        obj.Flag |= (byte)VehicleDataFlags.Shooting;
                }
                else if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)veh.Model.Hash, Util.Util.GetPedSeat(Game.Player.Character)))
                {
                    obj.AimCoords = RaycastEverything(new Vector2(0, 0)).ToLVector();
                    obj.WeaponHash = 0;
                    if (Game.IsEnabledControlPressed(0, Control.VehicleAttack))
                        obj.Flag |= (byte)VehicleDataFlags.Shooting;
                }
                else
                {
                    if (player.IsSubtaskActive(200) &&
                        Game.IsEnabledControlPressed(0, Control.Attack) &&
                        Game.Player.Character.Weapons.Current?.AmmoInClip != 0)
                        obj.Flag |= (byte)VehicleDataFlags.Shooting;
                    if ((player.IsSubtaskActive(200) && // or 290
                        Game.Player.Character.Weapons.Current?.AmmoInClip != 0) || (Game.Player.Character.Weapons.Current.Hash == WeaponHash.Unarmed && player.IsSubtaskActive(200)))
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
            foreach (var resource in resources)
            {
                confirmObj.Write(resource);
            }
            Client.SendMessage(confirmObj, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);

            HasFinishedDownloading = true;
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

        public void OnTick(object sender, EventArgs e)
        {
            Main.TickCount++;
            try
            {
                Ped player = Game.Player.Character;
                var res = UIMenu.GetScreenResolutionMantainRatio();

                if (Environment.TickCount - _lastCheck > 1000)
                {
                    _bytesSentPerSecond = _bytesSent - _lastBytesSent;
                    _bytesReceivedPerSecond = _bytesReceived - _lastBytesReceived;

                    _lastBytesReceived = _bytesReceived;
                    _lastBytesSent = _bytesSent;


                    _lastCheck = Environment.TickCount;
                }

                if (!string.IsNullOrWhiteSpace(_threadsafeSubtitle))
                {
                    GTA.UI.Screen.ShowSubtitle(_threadsafeSubtitle, 500);
                }

                DEBUG_STEP = 1;

                if (!_hasInitialized)
                {
                    RebuildServerBrowser();
#if INTEGRITYCHECK
                    if (!VerifyGameIntegrity())
                    {
                        _mainWarning = new Warning("alert", "Could not verify game integrity.\nPlease restart your game, or update Grand Theft Auto V.");
                        _mainWarning.OnAccept = () =>
                        {
                            if (Client != null && IsOnServer()) Client.Disconnect("Quit");
                            CEFManager.Dispose();
                            CEFManager.DisposeCef();
                            Script.Wait(500);
                            //Environment.Exit(0);
                            Process.GetProcessesByName("GTA5")[0].Kill();
                            Process.GetCurrentProcess().Kill();
                        };
                    }
#endif
                    _hasInitialized = true;
                }
                DEBUG_STEP = 2;

                if (!_hasPlayerSpawned && player != null && player.Handle != 0 && !Game.IsLoading)
                {
                    GTA.UI.Screen.FadeOut(1);
                    ResetPlayer();
                    MainMenu.RefreshIndex();
                    _hasPlayerSpawned = true;
                    MainMenu.Visible = true;
                    World.RenderingCamera = MainMenuCamera;

                    var address = Util.Util.FindPattern("\x32\xc0\xf3\x0f\x11\x09", "xxxxxx"); // Weapon/radio slowdown

                    DEBUG_STEP = 3;

                    if (address != IntPtr.Zero)
                    {
                        Util.Util.WriteMemory(address, 0x90, 6);
                    }

                    address = Util.Util.FindPattern("\x48\x85\xC0\x0F\x84\x00\x00\x00\x00\x8B\x48\x50", "xxxxx????xxx");
                    // unlock objects; credit goes to the GTA-MP team

                    DEBUG_STEP = 4;

                    if (address != IntPtr.Zero)
                    {
                        Util.Util.WriteMemory(address, 0x90, 24);
                    }

                    //TerminateGameScripts();

                    GTA.UI.Screen.FadeIn(1000);
                }

                DEBUG_STEP = 5;

                Game.DisableControlThisFrame(0, Control.EnterCheatCode);
                Game.DisableControlThisFrame(0, Control.FrontendPause);
                Game.DisableControlThisFrame(0, Control.FrontendPauseAlternate);
                Game.DisableControlThisFrame(0, Control.FrontendSocialClub);
                Game.DisableControlThisFrame(0, Control.FrontendSocialClubSecondary);

                if (!player.IsJumping)
                {
                    //Game.DisableControlThisFrame(0, Control.MeleeAttack1);
                    Game.DisableControlThisFrame(0, Control.MeleeAttackLight);
                }

                Function.Call(Hash.HIDE_HELP_TEXT_THIS_FRAME);

                if (Game.Player.Character.IsRagdoll)
                {
                    Game.DisableControlThisFrame(0, Control.Attack);
                    Game.DisableControlThisFrame(0, Control.Attack2);
                }

                if (_mainWarning != null)
                {
                    _mainWarning.Draw();
                    return;
                }

                if ((Game.IsControlJustPressed(0, Control.FrontendPauseAlternate) || Game.IsControlJustPressed(0, Control.FrontendPause))
                    && !MainMenu.Visible && !_wasTyping)
                {
                    MainMenu.Visible = true;

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
                else
                {
                    if (!BlockControls)
                        MainMenu.ProcessControls();
                    MainMenu.Update();
                    MainMenu.CanLeave = IsOnServer();
                    if (MainMenu.Visible && !MainMenu.TemporarilyHidden && !_mainMapItem.Focused && _hasScAvatar && File.Exists(GTANInstallDir + "\\images\\scavatar.png"))
                    {
                        var safe = new Point(300, 180);
                        Util.Util.DxDrawTexture(0, GTANInstallDir + "images\\scavatar.png", res.Width - safe.X - 64, safe.Y - 80, 64, 64, 0, 255, 255, 255, 255, false);
                    }

                    if (!IsOnServer()) Game.EnableControlThisFrame(0, Control.FrontendPause);
                }
                DEBUG_STEP = 6;

                if (_isGoingToCar && Game.IsControlJustPressed(0, Control.PhoneCancel))
                {
                    Game.Player.Character.Task.ClearAll();
                    _isGoingToCar = false;
                }

#region DISABLED
                /*
                if (Game.Player.Character.IsInVehicle())
                {
                    var pos = Game.Player.Character.CurrentVehicle.GetOffsetInWorldCoords(offset);
                    GTA.UI.Screen.ShowSubtitle(offset.ToString());
                    World.DrawMarker(MarkerType.DebugSphere, pos, new Vector3(), new Vector3(), new Vector3(0.2f, 0.2f, 0.2f), Color.Red);
                }*/
                /*
                if (Game.IsKeyPressed(Keys.NumPad7))
                {
                    offset = new Vector3(offset.X, offset.Y, offset.Z + 0.005f);
                }

                if (Game.IsKeyPressed(Keys.NumPad1))
                {
                    offset = new Vector3(offset.X, offset.Y, offset.Z - 0.005f);
                }

                if (Game.IsKeyPressed(Keys.NumPad4))
                {
                    offset = new Vector3(offset.X, offset.Y - 0.005f, offset.Z);
                }

                if (Game.IsKeyPressed(Keys.NumPad6))
                {
                    offset = new Vector3(offset.X, offset.Y + 0.005f, offset.Z);
                }

                if (Game.IsKeyPressed(Keys.NumPad2))
                {
                    offset = new Vector3(offset.X - 0.005f, offset.Y, offset.Z);
                }

                if (Game.IsKeyPressed(Keys.NumPad8))
                {
                    offset = new Vector3(offset.X + 0.005f, offset.Y, offset.Z);
                }
                */
                /*
                if (Game.IsControlJustPressed(0, Control.Context))
                {
                    var p = Game.Player.Character.GetOffsetInWorldCoords(new Vector3(0, 10f, 0f));
                    //_debugPickup = Function.Call<int>(Hash.CREATE_PICKUP, 1295434569, p.X, p.Y, p.Z, 0, 1, 1, 0);
                    int mask = 0;
                    mask |= 1 << _debugmask;
                    //mask |= 1 << 4;
                    //mask |= 1 << 8;
                    //mask |= 1 << 1;
                    _debugPickup = Function.Call<int>(Hash.CREATE_PICKUP_ROTATE, 1295434569, p.X, p.Y, p.Z, 0, 0, 0, mask, 1, 2, true, 0);
                }

                if (_debugPickup != 0)
                {
                    var obj = Function.Call<int>(Hash.GET_PICKUP_OBJECT, _debugPickup);
                    new Prop(obj).FreezePosition = true;
                    var exist = Function.Call<bool>(Hash.DOES_PICKUP_EXIST, _debugPickup);
                    GTA.UI.Screen.ShowSubtitle(_debugPickup + " (exists? " + exist + ") picked up obj (" + obj + "): " + Function.Call<bool>(Hash.HAS_PICKUP_BEEN_COLLECTED, obj));
                }

                if (Game.IsControlJustPressed(0, Control.LookBehind) && _debugPickup != 0)
                {
                    Function.Call(Hash.REMOVE_PICKUP, _debugPickup);
                }

                if (Game.IsControlJustPressed(0, Control.VehicleDuck))
                {
                    _debugmask++;
                    GTA.UI.Screen.ShowNotification("new bit pos: " + _debugmask);
                }

                GTA.UI.Screen.ShowSubtitle(Game.Player.Character.Weapons.Current.Hash.ToString());
                new UIResText(Game.Player.Character.Health + "/" + Game.Player.Character.MaxHealth, new Point(), 0.5f).Draw();


                for (int i = 0; i < 2000; i++)
                {
                    var val = Function.Call<int>(Hash.GET_PROFILE_SETTING, i);
                    if (_debugSettings.ContainsKey(i))
                    {
                        if (_debugSettings[i] != val)
                        {
                            GTA.UI.Screen.ShowNotification("SETTINGS ID " + i + " CHANGED TO " + val);
                        }
                        _debugSettings[i] = val;
                    }
                    else
                    {
                        _debugSettings.Add(i, val);
                    }
                }


                */

#if DEBUG
            /*
            var outArg = new OutputArgument();
            if (Game.IsControlJustPressed(0, Control.Context))
            {
                var pos = Game.Player.Character.Position;
                Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, "scr_rcbarry2");
                Function.Call(Hash._SET_PTFX_ASSET_NEXT_CALL, "scr_rcbarry2");
                Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_AT_COORD, "scr_clown_appears", pos.X, pos.Y, pos.Z, 0, 0, 0, 2f, 0, 0, 0);
            }
            
            if (player.IsInVehicle()) GTA.UI.Screen.ShowSubtitle(""+ player.CurrentVehicle.Velocity);
            else GTA.UI.Screen.ShowSubtitle(""+ player.Velocity);

            if (player.IsInVehicle()) GTA.UI.Screen.ShowSubtitle("" + player.CurrentVehicle.IsInBurnout());

            if (false)
            {
                Game.Player.Character.Alpha = 150;
                var rattleMeBones = Enum.GetValues(typeof (Bone)).Cast<Bone>().ToList();
                int count = 0;
                //37
                for (var i = _debugPed; i < Math.Min(_debugPed+37, rattleMeBones.Count); i++)
                {
                    var bone = rattleMeBones[(int)i];

                    var pos = Game.Player.Character.GetBoneCoord(bone);

                    World.DrawMarker(MarkerType.DebugSphere, pos, new Vector3(), new Vector3(),
                        new Vector3(0.05f, 0.05f, 0.05f), Color.FromArgb(100, 255, 255, 255));

                    new UIResText(bone.ToString(), new Point(10, count * 30), 0.35f, Color.White).Draw();
                    var lineSt = new Point(10, count*30) +
                                 new Size(StringMeasurer.MeasureString(bone.ToString()) + 10, 10);

                    var denorm = new Vector2((2 * lineSt.X / res.Width) - 1f, (2 * lineSt.Y / res.Height) - 1f);

                    var start = RaycastEverything(denorm);

                    Function.Call(Hash.DRAW_LINE, start.X, start.Y, start.Z, pos.X, pos.Y, pos.Z, 255 ,255 ,255 ,255);

                    count ++;
                }

                if (Game.IsKeyPressed(Keys.NumPad8)) _debugPed++;
                if (Game.IsKeyPressed(Keys.NumPad5) && _debugPed > 0) _debugPed--;
            }

            var cveh = World.GetClosestVehicle(Game.Player.Character.Position, 100f);

            if (cveh != null)
            {
                GTA.UI.Screen.ShowSubtitle(cveh.GetOffsetFromWorldCoords(Game.Player.Character.Position)+"");
            }


            public void setPlayerSeatbelt(Client player, bool seatbelt)
        {
            Program.ServerInstance.SendNativeCallToPlayer(player, 0x1913FE4CBF41C463,
                new EntityArgument(player.CharacterHandle.Value), 32, !seatbelt);
        }

        public bool getPlayerSeatbelt(Client player)
        {
            return fetchNativeFromPlayer<bool>(player, 0x1913FE4CBF41C463, new EntityArgument(player.CharacterHandle.Value), 32, true);
        }

            if (Game.IsControlJustPressed(0, Control.Context))
            {
                //Function.Call(Hash.SET_VEHICLE_ENGINE_ON, Game.Player.Character.CurrentVehicle, false, true, true);
                //Function.Call(Hash.SET_VEHICLE_UNDRIVEABLE, Game.Player.Character.CurrentVehicle, true);
                //SET_VEHICLE_UNDRIVEABLE

                Function.Call((Hash)0x1913FE4CBF41C463, Game.Player.Character, 32, false);
            }

            GTA.UI.Screen.ShowSubtitle(""+ Function.Call<bool>((Hash)0x7EE53118C892B513, Game.Player.Character, 32, true));

            if (Game.IsControlJustPressed(0, Control.LookBehind))
            {
                Function.Call((Hash)0x1913FE4CBF41C463, Game.Player.Character, 32, true);
                //Function.Call(Hash.SET_VEHICLE_ENGINE_ON, Game.Player.Character.CurrentVehicle, true, true, true);
                //Function.Call(Hash.SET_VEHICLE_UNDRIVEABLE, Game.Player.Character.CurrentVehicle, false);
                //SET_VEHICLE_UNDRIVEABLE
            }
            GTA.UI.Screen.ShowSubtitle(Function.Call<int>(Hash.GET_PED_TYPE, Game.Player.Character)+"");
            */
            /*
            var iActive = Function.Call<bool>(Hash.IS_PED_WEAPON_COMPONENT_ACTIVE, Game.Player.Character, (int)WeaponHash.Pistol, 899381934);
            GTA.UI.Screen.ShowSubtitle("" + iActive);

            if (Game.IsControlJustPressed(0, Control.Context))
            {
                var p = World.CreateRandomPed(
                        Game.Player.Character.GetOffsetInWorldCoords(
                            new Vector3(0, 5f, 0f)));
                //p.IsPersistent = false;
                p.BlockPermanentEvents = true;


                var hasGot = Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON_COMPONENT, p, (int)WeaponHash.PumpShotgun, -435637410);

                GTA.UI.Screen.ShowNotification("1st Got: " + hasGot);
                
                //p.Weapons.Give(WeaponHash.PumpShotgun, 99, true, true);
                //Script.Wait(2000);
                //Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_PED, p,
                    //(int)WeaponHash.PumpShotgun,
                    //-435637410); // AtSrSupp

                var wObj = Function.Call<int>(Hash.CREATE_WEAPON_OBJECT, (int) WeaponHash.PumpShotgun, 999, p.Position.X,
                    p.Position.Y, p.Position.Z, false, 0f, 0);
                Function.Call(Hash.GIVE_WEAPON_COMPONENT_TO_WEAPON_OBJECT, wObj, -435637410);
                Function.Call(Hash.GIVE_WEAPON_OBJECT_TO_PED, wObj, p);

                Script.Wait(1000);

                hasGot = Function.Call<bool>(Hash.HAS_PED_GOT_WEAPON_COMPONENT, p, (int) WeaponHash.PumpShotgun, -435637410);

                GTA.UI.Screen.ShowNotification("2nd Got: " + hasGot);
            }
        */

            /*            
                    if (freedebug != null)
                    {
                        if (Game.Player.Character.IsInVehicle() && !((Ped)freedebug).IsInVehicle())
                        {
                            ((Ped)freedebug).SetIntoVehicle(Game.Player.Character.CurrentVehicle, VehicleSeat.Passenger);
                        }

                        if (Game.IsControlJustPressed(0, Control.VehicleDuck) || Game.IsKeyPressed(Keys.Z))
                        {
                            Function.Call(Hash.SET_PED_CONFIG_FLAG, ((Ped) freedebug), ++_debugPickup, true);

                            GTA.UI.Screen.ShowSubtitle("Flag: " + _debugPickup);
                        }
                    }

                    if (Game.IsControlJustReleased(0, Control.Context))
                    {
                        Function.Call(Hash.SET_CREATE_RANDOM_COPS, false);
                        var ourPed = World.CreatePed(new Model(PedHash.Swat01SMY),
                            Game.Player.Character.GetOffsetInWorldCoords(new Vector3(0, 5f, 0)), 0, 20);

                        ourPed.BlockPermanentEvents = true;

                        Function.Call(Hash.TASK_SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ourPed, true);
                        ourPed.IsInvincible = true;
                        ourPed.CanRagdoll = false;

                        ourPed.RelationshipGroup = Main.RelGroup;
                        Function.Call(Hash.SET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH, ourPed, Main.RelGroup);

                        ourPed.FiringPattern = FiringPattern.FullAuto;

                        Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, ourPed);

                        Function.Call(Hash.SET_PED_CAN_EVASIVE_DIVE, ourPed, false);
                        Function.Call(Hash.SET_PED_DROPS_WEAPONS_WHEN_DEAD, ourPed, false);

                        Function.Call(Hash.SET_PED_CAN_BE_TARGETTED, ourPed, true);
                        Function.Call(Hash.SET_PED_CAN_BE_TARGETTED_BY_PLAYER, ourPed, Game.Player, true);
                        Function.Call(Hash.SET_PED_GET_OUT_UPSIDE_DOWN_VEHICLE, ourPed, false);
                        Function.Call(Hash.SET_PED_AS_ENEMY, ourPed, false);
                        Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, ourPed, true, true);

                        //ourPed.Weapons.Give(WeaponHash.Pistol, 9999, true, true);

                        freedebug = ourPed;
                    }
                    */

            if (display)
            {
                Debug();
                //unsafe
                //{
                //GTA.UI.Screen.ShowSubtitle(new IntPtr(Game.Player.Character.MemoryAddress).ToInt64().ToString("X"));
                //}
                //Game.Player.Character.Task.AimAt(Game.Player.Character.GetOffsetInWorldCoords(new Vector3(0, 5f, 0)), -1);
            }
            /*
            var wList = Enum.GetValues(typeof (WeaponHash)).Cast<WeaponHash>();
            Dictionary<int, List<string>> slots = new Dictionary<int, List<string>>();

            foreach (var hash in wList)
            {
                var slot = Function.Call<int>(Hash.GET_WEAPONTYPE_GROUP, unchecked((int) hash));

                if (!slots.ContainsKey(slot)) slots.Add(slot, new List<string>());

                slots[slot].Add(hash.ToString());
            }

            int counter = 0;
            foreach(var pair in slots)
            {
                var str = pair.Key + ": " + pair.Value.Aggregate((prev, next) => prev + ", " + next);
                if (str.Length > 90) str = str.Substring(0, 90);
                new UIResText(str,new Point(10, 40 * counter++), 0.3f).Draw();
            }

            GTA.UI.Screen.ShowSubtitle(slots.Count.ToString());
                */

            
            DEBUG_STEP = 4;
            if (_debugWindow)
            {
                _debug.Visible = true;
                _debug.Draw();
            }
            /*
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < 500; i++)
            {
                if (Game.Player.Character.IsSubtaskActive(i))
                {
                    sb.Append(i+",");
                }
            }

            new UIResText(sb.ToString(), new Point(10, 10), 0.3f).Draw();
            */
            /*
            GTA.UI.Screen.ShowSubtitle(Game.Player.Character.RelationshipGroup.ToString());
            if (Game.Player.Character.LastVehicle != null)
            {
                unsafe
                {
                    var address = new IntPtr(Game.Player.Character.LastVehicle.MemoryAddress);
                    GTA.UI.Screen.ShowSubtitle(address + " (" + address.ToInt64() + ")\n" + Game.Player.Character.LastVehicle.SteeringScale);
                }
            }

            if (Game.IsControlPressed(0, Control.LookBehind))
            {
                Game.Player.Character.LastVehicle.SteeringAngle = -0.69f;
            }

			var gunEnt = Function.Call<Entity>(Hash._0x3B390A939AF0B5FC, Game.Player.Character);

	        if (gunEnt != null)
	        {
				var start = gunEnt.GetOffsetInWorldCoords(offset);
				World.DrawMarker(MarkerType.DebugSphere, start, new Vector3(), new Vector3(), new Vector3(0.01f, 0.01f, 0.01f), Color.Red);
				GTA.UI.Screen.ShowSubtitle(offset.ToString());
				if (Game.IsKeyPressed(Keys.NumPad3))
		        {
			        var end = Game.Player.Character.GetOffsetInWorldCoords(new Vector3(0, 5f, 0f));

			        Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, start.X, start.Y, start.Z,
				        end.X,
				        end.Y, end.Z, 100, true, (int) WeaponHash.APPistol, Game.Player.Character, true, false, 100);
					Function.Call(Hash.DRAW_LINE, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 255, 255, 255, 255);
			        GTA.UI.Screen.ShowSubtitle("Bullet!");
		        }
	        }*/


            /*
            if (Game.IsControlJustPressed(0, Control.Context))
            {
                //Game.Player.Character.Task.ShootAt(World.GetCrosshairCoordinates().HitCoords, -1);
                var k = World.GetCrosshairCoordinates().HitCoords;
                //Function.Call(Hash.ADD_VEHICLE_SUBTASK_ATTACK_COORD, Game.Player.Character, k.X, k.Y, k.Z);
                _lastModel =
                    World.CreateRandomPed(Game.Player.Character.GetOffsetInWorldCoords(new Vector3(0, 10f, 0))).Handle;
                new Ped(_lastModel).Weapons.Give(WeaponHash.MicroSMG, 500, true, true);
                _tmpCar = World.CreateVehicle(new Model(VehicleHash.Adder),
                    Game.Player.Character.GetOffsetInWorldCoords(new Vector3(0, 10f, 0)), 0f);
                new Ped(_lastModel).SetIntoVehicle(_tmpCar, VehicleSeat.Passenger);
                Function.Call(Hash.TASK_DRIVE_BY, _lastModel, 0, 0, k.X, k.Y, k.Z, 0, 0, 0, unchecked ((int) FiringPattern.FullAuto));
            }

            if (Game.IsKeyPressed(Keys.D5))
            {
                new Ped(_lastModel).Task.ClearAll();
            }

            if (Game.IsKeyPressed(Keys.D6))
            {
                var k = World.GetCrosshairCoordinates().HitCoords;
                Function.Call(Hash.TASK_DRIVE_BY, _lastModel, 0, 0, k.X, k.Y, k.Z, 0, 0, 0, unchecked((int)FiringPattern.FullAuto));
            }

            if (Game.IsControlJustPressed(0, Control.LookBehind))
            {
                Game.Player.Character.Task.ClearAll();
                new Ped(_lastModel).Delete();
                _tmpCar.Delete();
            }

            if (Game.IsControlJustPressed(0, Control.LookBehind))
            {
                var mod = new Model(PedHash.Zombie01);
                mod.Request(10000);
                Function.Call(Hash.SET_PLAYER_MODEL, Game.Player, mod.Hash);
            }
            */
            /*
            if (Game.IsControlPressed(0, Control.LookBehind) && Game.Player.Character.IsInVehicle())
            {
                Game.Player.Character.CurrentVehicle.CurrentRPM = 1f;
                Game.Player.Character.CurrentVehicle.Acceleration = 1f;
            }

            if (Game.Player.Character.IsInVehicle())
            {
                GTA.UI.Screen.ShowSubtitle("RPM: " + Game.Player.Character.CurrentVehicle.CurrentRPM + " AC: " + Game.Player.Character.CurrentVehicle.Acceleration);
            }*/
#endif
#endregion


                if (Client == null || Client.ConnectionStatus == NetConnectionStatus.Disconnected || Client.ConnectionStatus == NetConnectionStatus.None) return;
                // BELOW ONLY ON SERVER

                _versionLabel.Position = new Point((int)(res.Width / 2), 0);
                _versionLabel.TextAlignment = UIResText.Alignment.Centered;
                _versionLabel.Draw();

                DEBUG_STEP = 7;

                if (_wasTyping)
                    Game.DisableControlThisFrame(0, Control.FrontendPauseAlternate);

                var playerCar = Game.Player.Character.CurrentVehicle;

                DEBUG_STEP = 8;

                Watcher.Tick();

                DEBUG_STEP = 9;

                _playerGodMode = Game.Player.IsInvincible;

                //invokeonLocalPlayerShoot
                if (player != null && player.IsShooting)
                {
                    JavascriptHook.InvokeCustomEvent(
                        api =>
                            api?.invokeonLocalPlayerShoot((int)(player.Weapons.Current?.Hash ?? 0),
                                RaycastEverything(new Vector2(0, 0)).ToLVector()));
                }


                DEBUG_STEP = 10;

                int netPlayerCar = 0;
                RemoteVehicle cc = null;
                if (playerCar != null && (netPlayerCar = NetEntityHandler.EntityToNet(playerCar.Handle)) != 0)
                {
                    var item = NetEntityHandler.NetToStreamedItem(netPlayerCar) as RemoteVehicle;

                    if (item != null)
                    {
                        var lastHealth = item.Health;
                        var lastDamageModel = item.DamageModel;

                        item.Position = playerCar.Position.ToLVector();
                        item.Rotation = playerCar.Rotation.ToLVector();
                        item.Health = playerCar.EngineHealth;
                        item.IsDead = playerCar.IsDead;
                        item.DamageModel = playerCar.GetVehicleDamageModel();

                        if (lastHealth != playerCar.EngineHealth)
                        {
                            JavascriptHook.InvokeCustomEvent(api => api?.invokeonVehicleHealthChange((int)lastHealth));
                        }

                        if (playerCar.IsEngineRunning ^ !PacketOptimization.CheckBit(item.Flag, EntityFlag.EngineOff))
                        {
                            playerCar.IsEngineRunning = !PacketOptimization.CheckBit(item.Flag, EntityFlag.EngineOff);
                        }

                        if (lastDamageModel != null)
                        {
                            if (lastDamageModel.BrokenWindows != item.DamageModel.BrokenWindows)
                            {
                                for (int i = 0; i < 8; i++)
                                {
                                    if (((lastDamageModel.BrokenWindows ^ item.DamageModel.BrokenWindows) & 1 << i) != 0)
                                    {
                                        var i1 = i;
                                        JavascriptHook.InvokeCustomEvent(api => api?.invokeonVehicleWindowSmash(i1));
                                    }
                                }
                            }

                            if (lastDamageModel.BrokenDoors != item.DamageModel.BrokenDoors)
                            {
                                for (int i = 0; i < 8; i++)
                                {
                                    if (((lastDamageModel.BrokenDoors ^ item.DamageModel.BrokenDoors) & 1 << i) != 0)
                                    {
                                        var i1 = i;
                                        JavascriptHook.InvokeCustomEvent(api => api?.invokeonVehicleDoorBreak(i1));
                                    }
                                }
                            }
                        }
                    }

                    cc = item;
                }

                DEBUG_STEP = 11;

                if (playerCar != _lastPlayerCar)
                {
                    if (_lastPlayerCar != null)
                    {
                        var c = NetEntityHandler.EntityToStreamedItem(_lastPlayerCar.Handle) as
                                    RemoteVehicle;

                        if (VehicleSyncManager.IsSyncing(c))
                        {
                            _lastPlayerCar.IsInvincible = c?.IsInvincible ?? false;
                        }
                        else
                        {
                            _lastPlayerCar.IsInvincible = true;
                        }

                        if (c != null)
                        {
                            Function.Call(Hash.SET_VEHICLE_ENGINE_ON, _lastPlayerCar,
                                !PacketOptimization.CheckBit(c.Flag, EntityFlag.EngineOff), true, true);
                        }

                        int h = _lastPlayerCar.Handle;
                        var lh = new LocalHandle(h);
                        JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerExitVehicle(lh));
                        _lastVehicleSiren = false;
                    }

                    if (playerCar != null)
                    {
                        if (!NetEntityHandler.ContainsLocalHandle(playerCar.Handle))
                        {
                            playerCar.Delete();
                            playerCar = null;
                        }
                        else
                        {
                            playerCar.IsInvincible = cc?.IsInvincible ?? false;

                            LocalHandle handle = new LocalHandle(playerCar.Handle);
                            JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerEnterVehicle(handle));
                        }

                    }

                    LastCarEnter = DateTime.Now;
                }

                DEBUG_STEP = 12;

                if (playerCar != null)
                {
                    if (Util.Util.GetResponsiblePed(playerCar).Handle == player.Handle)
                    {
                        playerCar.IsInvincible = cc?.IsInvincible ?? false;
                    }
                    else
                    {
                        playerCar.IsInvincible = true;
                    }

                    var siren = playerCar.SirenActive;

                    if (siren != _lastVehicleSiren)
                    {
                        JavascriptHook.InvokeCustomEvent(api => api?.invokeonVehicleSirenToggle());
                    }

                    _lastVehicleSiren = siren;
                }

                Game.Player.Character.MaxHealth = 200;

                var playerObj = NetEntityHandler.EntityToStreamedItem(Game.Player.Character.Handle) as RemotePlayer;

                if (playerObj != null)
                {
                    Game.Player.IsInvincible = playerObj.IsInvincible;
                }

                if (!string.IsNullOrWhiteSpace(CustomAnimation))
                {
                    var sp = CustomAnimation.Split();

                    if (
                        !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, Game.Player.Character,
                            sp[0], sp[1],
                            3))
                    {
                        Game.Player.Character.Task.ClearSecondary();

                        Function.Call(Hash.TASK_PLAY_ANIM, Game.Player.Character,
                            Util.Util.LoadDict(sp[0]), sp[1],
                            8f, 10f, -1, AnimationFlag, -8f, 1, 1, 1);
                    }
                }

                DEBUG_STEP = 13;
                _lastPlayerCar = playerCar;


                if (Game.IsControlJustPressed(0, Control.ThrowGrenade) && !Game.Player.Character.IsInVehicle() && IsOnServer() && !Chat.IsFocused)
                {
                    var vehs = World.GetAllVehicles().OrderBy(v => v.Position.DistanceToSquared(Game.Player.Character.Position)).Take(1).ToList();
                    if (vehs.Any() && Game.Player.Character.IsInRangeOfEx(vehs[0].Position, 6f))
                    {
                        var relPos = vehs[0].GetOffsetFromWorldCoords(Game.Player.Character.Position);
                        VehicleSeat seat = VehicleSeat.Any;

                        if (relPos.X < 0 && relPos.Y > 0)
                        {
                            seat = VehicleSeat.LeftRear;
                        }
                        else if (relPos.X >= 0 && relPos.Y > 0)
                        {
                            seat = VehicleSeat.RightFront;
                        }
                        else if (relPos.X < 0 && relPos.Y <= 0)
                        {
                            seat = VehicleSeat.LeftRear;
                        }
                        else if (relPos.X >= 0 && relPos.Y <= 0)
                        {
                            seat = VehicleSeat.RightRear;
                        }

                        if (vehs[0].PassengerCapacity == 1) seat = VehicleSeat.Passenger;

                        if (vehs[0].PassengerCapacity > 3 && vehs[0].GetPedOnSeat(seat).Handle != 0)
                        {
                            if (seat == VehicleSeat.LeftRear)
                            {
                                for (int i = 3; i < vehs[0].PassengerCapacity; i += 2)
                                {
                                    if (vehs[0].GetPedOnSeat((VehicleSeat)i).Handle == 0)
                                    {
                                        seat = (VehicleSeat)i;
                                        break;
                                    }
                                }
                            }
                            else if (seat == VehicleSeat.RightRear)
                            {
                                for (int i = 4; i < vehs[0].PassengerCapacity; i += 2)
                                {
                                    if (vehs[0].GetPedOnSeat((VehicleSeat)i).Handle == 0)
                                    {
                                        seat = (VehicleSeat)i;
                                        break;
                                    }
                                }
                            }
                        }

                        if (WeaponDataProvider.DoesVehicleSeatHaveGunPosition((VehicleHash)vehs[0].Model.Hash, 0, true) && Game.Player.Character.IsIdle && !Game.Player.IsAiming)
                            Game.Player.Character.SetIntoVehicle(vehs[0], seat);
                        else
                            Game.Player.Character.Task.EnterVehicle(vehs[0], seat, -1, 2f);
                        _isGoingToCar = true;
                    }
                }

                DEBUG_STEP = 14;

                Game.DisableControlThisFrame(0, Control.SpecialAbility);
                Game.DisableControlThisFrame(0, Control.SpecialAbilityPC);
                Game.DisableControlThisFrame(0, Control.SpecialAbilitySecondary);
                Game.DisableControlThisFrame(0, Control.CharacterWheel);
                Game.DisableControlThisFrame(0, Control.Phone);
                Game.DisableControlThisFrame(0, Control.Duck);

                DEBUG_STEP = 15;

                VehicleSyncManager?.Pulse();

                DEBUG_STEP = 16;

                if (StringCache != null)
                {
                    StringCache.Pulse();
                }

                DEBUG_STEP = 17;

                double aver = 0;
                lock (_averagePacketSize)
                    aver = _averagePacketSize.Count > 0 ? _averagePacketSize.Average() : 0;

                _statsItem.Text =
                    string.Format(
                        "~h~Bytes Sent~h~: {0}~n~~h~Bytes Received~h~: {1}~n~~h~Bytes Sent / Second~h~: {5}~n~~h~Bytes Received / Second~h~: {6}~n~~h~Average Packet Size~h~: {4}~n~~n~~h~Messages Sent~h~: {2}~n~~h~Messages Received~h~: {3}",
                        _bytesSent, _bytesReceived, _messagesSent, _messagesReceived,
                        aver, _bytesSentPerSecond,
                        _bytesReceivedPerSecond);



                DEBUG_STEP = 18;
                if (Game.IsControlPressed(0, Control.Aim) && !Game.Player.Character.IsInVehicle() && Game.Player.Character.Weapons.Current.Hash != WeaponHash.Unarmed)
                {
                    Game.DisableControlThisFrame(0, Control.Jump);
                }

                //CRASH WORKAROUND: DISABLE PARACHUTE RUINER2
                if (Game.Player.Character.IsInVehicle())
                {
                    if (Game.Player.Character.CurrentVehicle.IsInAir && Game.Player.Character.CurrentVehicle.Model.Hash == 941494461)
                    {
                        Game.DisableAllControlsThisFrame(0);
                    }
                }
                

                DEBUG_STEP = 19;
                Function.Call((Hash)0x5DB660B38DD98A31, Game.Player, 0f);
                DEBUG_STEP = 20;
                Game.MaxWantedLevel = 0;
                Game.Player.WantedLevel = 0;
                DEBUG_STEP = 21;
                lock (_localMarkers)
                {
                    foreach (var marker in _localMarkers)
                    {
                        World.DrawMarker((MarkerType)marker.Value.MarkerType, marker.Value.Position.ToVector(),
                            marker.Value.Direction.ToVector(), marker.Value.Rotation.ToVector(),
                            marker.Value.Scale.ToVector(),
                            Color.FromArgb(marker.Value.Alpha, marker.Value.Red, marker.Value.Green, marker.Value.Blue));
                    }
                }

                DEBUG_STEP = 22;
                var hasRespawned = (Function.Call<int>(Hash.GET_TIME_SINCE_LAST_DEATH) < 8000 &&
                                    Function.Call<int>(Hash.GET_TIME_SINCE_LAST_DEATH) != -1 &&
                                    Game.Player.CanControlCharacter);

                if (hasRespawned && !_lastDead)
                {
                    _lastDead = true;
                    var msg = Client.CreateMessage();
                    msg.Write((byte)PacketType.PlayerRespawned);
                    Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);

                    JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerRespawn());

                    if (Weather != null) Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST, Weather);
                    if (Time.HasValue)
                    {
                        World.CurrentDayTime = new TimeSpan(Time.Value.Hours, Time.Value.Minutes, 00);
                    }

                    Function.Call(Hash.PAUSE_CLOCK, true);

                    var us = NetEntityHandler.EntityToStreamedItem(Game.Player.Character.Handle);

                    NetEntityHandler.ReattachAllEntities(us, true);
                    foreach (
                        var source in
                            Main.NetEntityHandler.ClientMap.Values.Where(
                                item => item is RemoteParticle && ((RemoteParticle)item).EntityAttached == us.RemoteHandle)
                                .Cast<RemoteParticle>())
                    {
                        Main.NetEntityHandler.StreamOut(source);
                        Main.NetEntityHandler.StreamIn(source);
                    }
                }

                var pHealth = player.Health;
                var pArmor = player.Armor;
                var pGun = player.Weapons.Current?.Hash ?? WeaponHash.Unarmed;
                var pModel = player.Model.Hash;

                if (pHealth != _lastPlayerHealth)
                {
                    int test = _lastPlayerHealth;
                    JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerHealthChange(test));
                }

                if (pArmor != _lastPlayerArmor)
                {
                    int test = _lastPlayerArmor;
                    JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerArmorChange(test));
                }

                if (pGun != _lastPlayerWeapon)
                {
                    WeaponHash test = _lastPlayerWeapon;
                    JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerWeaponSwitch((int)test));
                }

                if (pModel != (int)_lastPlayerModel)
                {
                    PedHash test = _lastPlayerModel;
                    JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerModelChange((int)test));
                }

                _lastPlayerHealth = pHealth;
                _lastPlayerArmor = pArmor;
                _lastPlayerWeapon = pGun;
                _lastPlayerModel = (PedHash)pModel;

                DEBUG_STEP = 23;
                _lastDead = hasRespawned;
                DEBUG_STEP = 24;
                var killed = Game.Player.Character.IsDead;
                DEBUG_STEP = 25;
                if (killed && !_lastKilled)
                {
                    var msg = Client.CreateMessage();
                    msg.Write((byte)PacketType.PlayerKilled);
                    var killer = Function.Call<int>(Hash.GET_PED_SOURCE_OF_DEATH, Game.Player.Character);
                    var weapon = Function.Call<int>(Hash.GET_PED_CAUSE_OF_DEATH, Game.Player.Character);


                    var killerEnt = NetEntityHandler.EntityToNet(killer);
                    msg.Write(killerEnt);
                    msg.Write(weapon);
                    /*
                    var playerMod = (PedHash)Game.Player.Character.Model.Hash;
                    if (playerMod != PedHash.Michael && playerMod != PedHash.Franklin && playerMod != PedHash.Trevor)
                    {
                        _lastModel = Game.Player.Character.Model.Hash;
                        var lastMod = new Model(PedHash.Michael);
                        lastMod.Request(10000);
                        Function.Call(Hash.SET_PLAYER_MODEL, Game.Player, lastMod);
                        Game.Player.Character.Kill();
                    }
                    else
                    {
                        _lastModel = 0;
                    }
                    */
                    Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.SyncEvent);

                    JavascriptHook.InvokeCustomEvent(api => api?.invokeonPlayerDeath(new LocalHandle(killer), weapon));

                    NativeUI.BigMessageThread.MessageInstance.ShowColoredShard("WASTED", "", HudColor.HUD_COLOUR_BLACK, HudColor.HUD_COLOUR_RED, 7000);
                    Function.Call(Hash.REQUEST_SCRIPT_AUDIO_BANK, "HUD_MINI_GAME_SOUNDSET", true);
                    Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
                }

                /*
                if (Function.Call<int>(Hash.GET_TIME_SINCE_LAST_DEATH) < 8000 &&
                    Function.Call<int>(Hash.GET_TIME_SINCE_LAST_DEATH) != -1)
                {
                    if (_lastModel != 0 && Game.Player.Character.Model.Hash != _lastModel)
                    {
                        var lastMod = new Model(_lastModel);
                        lastMod.Request(10000);
                        Function.Call(Hash.SET_PLAYER_MODEL, new InputArgument(Game.Player), lastMod.Hash);
                    }
                }
                */


                DEBUG_STEP = 28;


                if (!IsSpectating && _lastSpectating)
                {
                    Game.Player.Character.Opacity = 255;
                    Game.Player.Character.IsPositionFrozen = false;
                    Game.Player.IsInvincible = false;
                    Game.Player.Character.IsCollisionEnabled = true;
                    SpectatingEntity = 0;
                    CurrentSpectatingPlayer = null;
                    _currentSpectatingPlayerIndex = 100000;
                    Game.Player.Character.PositionNoOffset = _preSpectatorPos;
                }
                if (IsSpectating)
                {
                    if (SpectatingEntity != 0)
                    {
                        Game.Player.Character.Opacity = 0;
                        Game.Player.Character.IsPositionFrozen = true;
                        Game.Player.IsInvincible = true;
                        Game.Player.Character.IsCollisionEnabled = false;

                        Control[] exceptions = new[]
                        {
                            Control.LookLeftRight,
                            Control.LookUpDown,
                            Control.LookLeft,
                            Control.LookRight,
                            Control.LookUp,
                            Control.LookDown,
                        };

                        Game.DisableAllControlsThisFrame(0);
                        foreach (var c in exceptions)
                            Game.EnableControlThisFrame(0, c);

                        var ent = NetEntityHandler.NetToEntity(SpectatingEntity);

                        if (ent != null)
                        {
                            if (Function.Call<bool>(Hash.IS_ENTITY_A_PED, ent) && new Ped(ent.Handle).IsInVehicle())
                                Game.Player.Character.PositionNoOffset = ent.Position + new Vector3(0, 0, 1.3f);
                            else
                                Game.Player.Character.PositionNoOffset = ent.Position;
                        }
                    }
                    else if (SpectatingEntity == 0 && CurrentSpectatingPlayer == null &&
                             NetEntityHandler.ClientMap.Values.Count(op => op is SyncPed && !((SyncPed)op).IsSpectating &&
                                    (((SyncPed)op).Team == 0 || ((SyncPed)op).Team == Main.LocalTeam) &&
                                    (((SyncPed)op).Dimension == 0 || ((SyncPed)op).Dimension == Main.LocalDimension)) > 0)
                    {
                        CurrentSpectatingPlayer =
                            NetEntityHandler.ClientMap.Values.Where(
                                op =>
                                    op is SyncPed && !((SyncPed)op).IsSpectating &&
                                    (((SyncPed)op).Team == 0 || ((SyncPed)op).Team == Main.LocalTeam) &&
                                    (((SyncPed)op).Dimension == 0 || ((SyncPed)op).Dimension == Main.LocalDimension))
                                .ElementAt(_currentSpectatingPlayerIndex %
                                           NetEntityHandler.ClientMap.Values.Count(
                                               op =>
                                                   op is SyncPed && !((SyncPed)op).IsSpectating &&
                                                   (((SyncPed)op).Team == 0 || ((SyncPed)op).Team == Main.LocalTeam) &&
                                                   (((SyncPed)op).Dimension == 0 ||
                                                    ((SyncPed)op).Dimension == Main.LocalDimension))) as SyncPed;
                    }
                    else if (SpectatingEntity == 0 && CurrentSpectatingPlayer != null)
                    {
                        Game.Player.Character.Opacity = 0;
                        Game.Player.Character.IsPositionFrozen = true;
                        Game.Player.IsInvincible = true;
                        Game.Player.Character.IsCollisionEnabled = false;
                        Game.DisableAllControlsThisFrame(0);

                        if (CurrentSpectatingPlayer.Character == null)
                            Game.Player.Character.PositionNoOffset = CurrentSpectatingPlayer.Position;
                        else if (CurrentSpectatingPlayer.IsInVehicle)
                            Game.Player.Character.PositionNoOffset = CurrentSpectatingPlayer.Character.Position + new Vector3(0, 0, 1.3f);
                        else
                            Game.Player.Character.PositionNoOffset = CurrentSpectatingPlayer.Character.Position;

                        if (Game.IsControlJustPressed(0, Control.PhoneLeft))
                        {
                            _currentSpectatingPlayerIndex--;
                            CurrentSpectatingPlayer = null;
                        }
                        else if (Game.IsControlJustPressed(0, Control.PhoneRight))
                        {
                            _currentSpectatingPlayerIndex++;
                            CurrentSpectatingPlayer = null;
                        }

                        if (CurrentSpectatingPlayer != null)
                        {
                            var center = new Point((int)(res.Width / 2), (int)(res.Height / 2));

                            new UIResText("Now spectating:~n~" + CurrentSpectatingPlayer.Name,
                                new Point(center.X, (int)(res.Height - 200)), 0.4f, Color.White, GTA.UI.Font.ChaletLondon,
                                UIResText.Alignment.Centered)
                            {
                                Outline = true,
                            }.Draw();

                            new Sprite("mparrow", "mp_arrowxlarge", new Point(center.X - 264, (int)(res.Height - 232)), new Size(64, 128), 180f, Color.White).Draw();
                            new Sprite("mparrow", "mp_arrowxlarge", new Point(center.X + 200, (int)(res.Height - 232)), new Size(64, 128)).Draw();
                        }
                    }
                }

                _lastSpectating = IsSpectating;

                _lastKilled = killed;

                Function.Call(Hash.SET_RANDOM_TRAINS, 0);
                Function.Call(Hash.CAN_CREATE_RANDOM_COPS, false);
                DEBUG_STEP = 29;
                Function.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);

                Function.Call(Hash.SET_VEHICLE_POPULATION_BUDGET, 0);
                Function.Call(Hash.SET_PED_POPULATION_BUDGET, 0);

                Function.Call(Hash.SUPPRESS_SHOCKING_EVENTS_NEXT_FRAME);
                Function.Call(Hash.SUPPRESS_AGITATION_EVENTS_NEXT_FRAME);



                Function.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_NUMBER_OF_PARKED_VEHICLES, -1);
                Function.Call(Hash.SET_ALL_LOW_PRIORITY_VEHICLE_GENERATORS_ACTIVE, false);
                Function.Call(Hash.SET_FAR_DRAW_VEHICLES, false);
                Function.Call(Hash.DESTROY_MOBILE_PHONE);
                Function.Call((Hash)0x015C49A93E3E086E, true);
                Function.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                Function.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f, 0f);
                Function.Call(Hash.SET_CAN_ATTACK_FRIENDLY, Game.Player.Character, true, true);
                Function.Call(Hash.SET_PED_CAN_BE_TARGETTED, Game.Player.Character, true);
                Function.Call((Hash)0xF796359A959DF65D, false); // Display distant vehicles
                Function.Call(Hash.SET_AUTO_GIVE_PARACHUTE_WHEN_ENTER_PLANE, Game.Player, false);
                Function.Call((Hash)0xD2B315B6689D537D, Game.Player, false);
                Function.Call(Hash.DISPLAY_CASH, false);
                DEBUG_STEP = 30;


                GameScript.Pulse();

                if ((Game.Player.Character.Position - _lastWaveReset).LengthSquared() > 10000f) // 100f * 100f
                {
                    Function.Call((Hash)0x5E5E99285AE812DB);
                    Function.Call((Hash)0xB96B00E976BE977F, 0f);

                    _lastWaveReset = Game.Player.Character.Position;
                }

                Function.Call((Hash)0x2F9A292AD0A3BD89);
                Function.Call((Hash)0x5F3B7749C112D552);

                if (Function.Call<bool>(Hash.IS_STUNT_JUMP_IN_PROGRESS))
                    Function.Call(Hash.CANCEL_STUNT_JUMP);

                DEBUG_STEP = 31;
                if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, Game.Player.Character) == 2)
                {
                    Game.DisableControlThisFrame(0, Control.Aim);
                    Game.DisableControlThisFrame(0, Control.Attack);
                }
                DEBUG_STEP = 32;


                if (RemoveGameEntities && Util.Util.TickCount - _lastEntityRemoval > 500) // Save ressource
                {
                    _lastEntityRemoval = Util.Util.TickCount;
                    foreach (var entity in World.GetAllPeds())
                    {
                        if (!NetEntityHandler.ContainsLocalHandle(entity.Handle) && entity != Game.Player.Character)
                        {
                            entity.Kill(); //"Some special peds like Epsilon guys or seashark minigame will refuse to despawn if you don't kill them first." - Guad
                            entity.Delete();
                        }
                    }

                    foreach (var entity in World.GetAllVehicles())
                    {
                        if (entity == null) continue;
                        var veh = NetEntityHandler.NetToStreamedItem(entity.Handle, useGameHandle: true) as RemoteVehicle;
                        if (veh == null)
                        {
                            entity.Delete();
                            continue;
                        }
                        //TO CHECK
                        if (Util.Util.IsVehicleEmpty(entity) && !VehicleSyncManager.IsInterpolating(entity.Handle) && veh.TraileredBy == 0 && !VehicleSyncManager.IsSyncing(veh) && ((entity.Handle == Game.Player.LastVehicle?.Handle && DateTime.Now.Subtract(LastCarEnter).TotalMilliseconds > 3000) || entity.Handle != Game.Player.LastVehicle?.Handle))
                        {
                            if (entity.Position.DistanceToSquared(veh.Position.ToVector()) > 2f)
                            {
                                entity.PositionNoOffset = veh.Position.ToVector();
                                entity.Quaternion = veh.Rotation.ToVector().ToQuaternion();
                            }
                        }

                        //veh.Position = entity.Position.ToLVector();
                        //veh.Rotation = entity.Rotation.ToLVector();
                    }
                }
                _whoseturnisitanyways = !_whoseturnisitanyways;

                DEBUG_STEP = 34;
                NetEntityHandler.UpdateAttachments();
                DEBUG_STEP = 35;
                NetEntityHandler.DrawMarkers();
                DEBUG_STEP = 36;
                NetEntityHandler.DrawLabels();
                DEBUG_STEP = 37;
                NetEntityHandler.UpdateMisc();
                DEBUG_STEP = 38;
                NetEntityHandler.UpdateInterpolations();
                DEBUG_STEP = 39;
                WeaponInventoryManager.Update();
                DEBUG_STEP = 40;

                /*string stats = string.Format("{0}Kb (D)/{1}Kb (U), {2}Msg (D)/{3}Msg (U)", _bytesReceived / 1000,
                    _bytesSent / 1000, _messagesReceived, _messagesSent);
                    */
                //GTA.UI.Screen.ShowSubtitle(stats);


                PedThread.OnTick("thisaintnullnigga", e);

                DebugInfo.Draw();

                //Thread calcucationThread = new Thread(Work);
                //calcucationThread.IsBackground = true;
                //calcucationThread.Start();

                lock (_threadJumping)
                {
                    if (_threadJumping.Any())
                    {
                        Action action = _threadJumping.Dequeue();
                        if (action != null) action.Invoke();
                    }
                }
                DEBUG_STEP = 41;
            }
            catch (Exception ex) // Catch any other exception. (can prevent crash)
            {
                LogManager.LogException(ex, "MAIN OnTick: STEP : " + DEBUG_STEP);
            }
        }

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
                    using (WebClient wc = new WebClient())
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

                            foreach (var file in resource.Value)
                            {
                                if (file.type == FileType.Script) continue;

                                string target = Path.Combine(FileTransferId._DOWNLOADFOLDER_, resource.Key, file.path);

                                if (File.Exists(target))
                                {
                                    var newHash = DownloadManager.HashFile(target);

                                    if (newHash == file.hash) continue;
                                }

                                wc.DownloadFileAsync(
                                    new Uri(string.Format("{0}/{1}/{2}", address, resource.Key, file.path)), target);

                                while (wc.IsBusy)
                                {
                                    Thread.Yield();
                                    if (_cancelDownload)
                                    {
                                        wc.CancelAsync();
                                        return;
                                    }
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

            var obj = new ConnectionRequest();
            obj.SocialClubName = string.IsNullOrWhiteSpace(Game.Player.Name) ? "Unknown" : Game.Player.Name; // To be used as identifiers in server files
            obj.DisplayName = string.IsNullOrWhiteSpace(PlayerSettings.DisplayName) ? obj.SocialClubName : PlayerSettings.DisplayName.Trim();
            obj.ScriptVersion = CurrentVersion.ToString();
            obj.CEF = !CefUtil.DISABLE_CEF;
            obj.CEFDevtool = EnableDevTool;
            obj.GameVersion = (byte)Game.Version;
            obj.MediaStream = EnableMediaStream;

            if(passProtected)
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
                        LogManager.DebugLog("RECEIVED LIGHT VEHICLE PACKET");
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

                        int nethandle;
                        CherryMPShared.Vector3 position;
                        PacketOptimization.ReadBasicSync(data, out nethandle, out position);

                        HandleBasicPacket(nethandle, position.ToVector());
                    }
                    break;
                case PacketType.BulletSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);

                        int nethandle;
                        CherryMPShared.Vector3 position;
                        bool shooting = PacketOptimization.ReadBulletSync(data, out nethandle, out position);

                        HandleBulletPacket(nethandle, shooting, position.ToVector());
                    }
                    break;
                case PacketType.BulletPlayerSync:
                    {
                        var len = msg.ReadInt32();
                        var data = msg.ReadBytes(len);

                        int nethandle;
                        int nethandleTarget;
                        bool shooting = PacketOptimization.ReadBulletSync(data, out nethandle, out nethandleTarget);

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
                       });
                        pcmsgThread.IsBackground = true;
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
                    var matchedItems = new List<UIMenuItem>();

                    matchedItems.Add(_serverBrowser.Items.FirstOrDefault(i => Dns.GetHostAddresses(i.Description.Split(':')[0])[0].ToString() + ":" + i.Description.Split(':')[1] == itemText));
                    matchedItems.Add(_recentBrowser.Items.FirstOrDefault(i => i.Description == itemText));
                    matchedItems.Add(_favBrowser.Items.FirstOrDefault(i => i.Description == itemText));
                    matchedItems.Add(_lanBrowser.Items.FirstOrDefault(i => i.Description == itemText));
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
            if (fullData.Quaternion != null)
            {
                syncPed.VehicleRotation = fullData.Quaternion.ToVector();
            }
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
            var syncPed = NetEntityHandler.GetPlayer(netHandle);

            syncPed.IsShooting = shooting;

            if (shooting) syncPed.AimCoords = aim;
        }

        private void HandleBulletPacket(int netHandle, bool shooting, int netHandleTarget)
        {
            var syncPed = NetEntityHandler.GetPlayer(netHandle);
            var syncPedTarget = NetEntityHandler.GetPlayer(netHandleTarget);

            syncPed.IsShooting = shooting;

            if (shooting) syncPed.AimPlayer = syncPedTarget;
        }
#endregion

        private void HandlePedPacket(PedData fullPacket, bool pure)
        {
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

                if (car.DamageModel == null) car.DamageModel = new VehicleDamageModel();
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
                            if (busted && !veh.IsTireBurst(i)) veh.Wheels[i].Burst();
                            else if (!busted && veh.IsTireBurst(i)) veh.Wheels[i].Fix();
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

            Util.Util.SetPlayerSkin(PedHash.Clown01SMY);

            Game.Player.Character.MaxHealth = 200;
            Game.Player.Character.Health = 200;
            Game.Player.Character.SetDefaultClothes();

            Game.Player.Character.IsPositionFrozen = false;
            Game.Player.IsInvincible = false;
            Game.Player.Character.IsCollisionEnabled = true;
            Game.Player.Character.Opacity = 255;
            Game.Player.Character.IsInvincible = false;
            Game.Player.Character.Weapons.RemoveAll();
            Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, Game.Player, 1f);
            Function.Call(Hash.SET_SWIM_MULTIPLIER_FOR_PLAYER, Game.Player, 1f);

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
                _debugSyncPed = new SyncPed(player.Model.Hash, player.Position, player.Rotation, false);
                _debugSyncPed.Debug = true;
                _debugSyncPed.StreamedIn = true;
                _debugSyncPed.Name = "DEBUG";
                _debugSyncPed.Alpha = 255;
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
            var packet = new ScriptEventTrigger();
            packet.EventName = eventName;
            packet.Resource = resource;
            packet.Arguments = ParseNativeArguments(args);
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

#region Natives
        public static IEnumerable<object> DecodeArgumentList(params NativeArgument[] args)
        {
            var list = new List<object>();

            foreach (var arg in args)
            {
                if (arg is IntArgument)
                {
                    list.Add(((IntArgument)arg).Data);
                }
                else if (arg is UIntArgument)
                {
                    list.Add(((UIntArgument)arg).Data);
                }
                else if (arg is StringArgument)
                {
                    list.Add(((StringArgument)arg).Data);
                }
                else if (arg is FloatArgument)
                {
                    list.Add(((FloatArgument)arg).Data);
                }
                else if (arg is BooleanArgument)
                {
                    list.Add(((BooleanArgument)arg).Data);
                }
                else if (arg is LocalPlayerArgument)
                {
                    list.Add(Game.Player.Character.Handle);
                }
                else if (arg is Vector3Argument)
                {
                    var tmp = (Vector3Argument)arg;
                    list.Add(tmp.X);
                    list.Add(tmp.Y);
                    list.Add(tmp.Z);
                }
                else if (arg is LocalGamePlayerArgument)
                {
                    list.Add(Game.Player.Handle);
                }
                else if (arg is EntityArgument)
                {
                    list.Add(NetEntityHandler.NetToEntity(((EntityArgument)arg).NetHandle)?.Handle);
                }
                else if (arg is EntityPointerArgument)
                {
                    list.Add(new OutputArgument(NetEntityHandler.NetToEntity(((EntityPointerArgument)arg).NetHandle)));
                }
                else if (args == null)
                {
                    list.Add(null);
                }
            }

            return list;
        }

        public static IEnumerable<object> DecodeArgumentListPure(params NativeArgument[] args)
        {
            var list = new List<object>();

            foreach (var arg in args)
            {
                if (arg is IntArgument)
                {
                    list.Add(((IntArgument)arg).Data);
                }
                else if (arg is UIntArgument)
                {
                    list.Add(((UIntArgument)arg).Data);
                }
                else if (arg is StringArgument)
                {
                    list.Add(((StringArgument)arg).Data);
                }
                else if (arg is FloatArgument)
                {
                    list.Add(((FloatArgument)arg).Data);
                }
                else if (arg is BooleanArgument)
                {
                    list.Add(((BooleanArgument)arg).Data);
                }
                else if (arg is LocalPlayerArgument)
                {
                    list.Add(new LocalHandle(Game.Player.Character.Handle));
                }
                else if (arg is Vector3Argument)
                {
                    var tmp = (Vector3Argument)arg;
                    list.Add(new CherryMPShared.Vector3(tmp.X, tmp.Y, tmp.Z));
                }
                else if (arg is LocalGamePlayerArgument)
                {
                    list.Add(new LocalHandle(Game.Player.Handle));
                }
                else if (arg is EntityArgument)
                {
                    var ent = NetEntityHandler.NetToStreamedItem(((EntityArgument)arg).NetHandle);

                    if (ent == null)
                    {
                        list.Add(new LocalHandle(0));
                    }
                    else if (ent is ILocalHandleable)
                    {
                        list.Add(new LocalHandle(NetEntityHandler.NetToEntity(ent)?.Handle ?? 0));
                    }
                    else
                    {
                        list.Add(new LocalHandle(ent.RemoteHandle, HandleType.NetHandle));
                    }
                }
                else if (arg is EntityPointerArgument)
                {
                    list.Add(new OutputArgument(NetEntityHandler.NetToEntity(((EntityPointerArgument)arg).NetHandle)));
                }
                else if (arg is ListArgument)
                {
                    List<object> output = new List<object>();
                    var larg = (ListArgument)arg;
                    if (larg.Data != null && larg.Data.Count > 0)
                        output.AddRange(DecodeArgumentListPure(larg.Data.ToArray()));
                    list.Add(output);
                }
                else
                {
                    list.Add(null);
                }
            }

            return list;
        }

        public static List<NativeArgument> ParseNativeArguments(params object[] args)
        {
            var list = new List<NativeArgument>();
            foreach (var o in args)
            {
                if (o is int)
                {
                    list.Add(new IntArgument() { Data = ((int)o) });
                }
                else if (o is uint)
                {
                    list.Add(new UIntArgument() { Data = ((uint)o) });
                }
                else if (o is string)
                {
                    list.Add(new StringArgument() { Data = ((string)o) });
                }
                else if (o is float)
                {
                    list.Add(new FloatArgument() { Data = ((float)o) });
                }
                else if (o is double)
                {
                    list.Add(new FloatArgument() { Data = ((float)(double)o) });
                }
                else if (o is bool)
                {
                    list.Add(new BooleanArgument() { Data = ((bool)o) });
                }
                else if (o is CherryMPShared.Vector3)
                {
                    var tmp = (CherryMPShared.Vector3)o;
                    list.Add(new Vector3Argument()
                    {
                        X = tmp.X,
                        Y = tmp.Y,
                        Z = tmp.Z,
                    });
                }
                else if (o is Vector3)
                {
                    var tmp = (Vector3)o;
                    list.Add(new Vector3Argument()
                    {
                        X = tmp.X,
                        Y = tmp.Y,
                        Z = tmp.Z,
                    });
                }
                else if (o is LocalPlayerArgument)
                {
                    list.Add((LocalPlayerArgument)o);
                }
                else if (o is OpponentPedHandleArgument)
                {
                    list.Add((OpponentPedHandleArgument)o);
                }
                else if (o is LocalGamePlayerArgument)
                {
                    list.Add((LocalGamePlayerArgument)o);
                }
                else if (o is EntityArgument)
                {
                    list.Add((EntityArgument)o);
                }
                else if (o is EntityPointerArgument)
                {
                    list.Add((EntityPointerArgument)o);
                }
                else if (o is NetHandle)
                {
                    list.Add(new EntityArgument(((NetHandle)o).Value));
                }
                else if (o is LocalHandle)
                {
                    list.Add(new EntityArgument(NetEntityHandler.EntityToNet(((LocalHandle)o).Value)));
                }
                else if (o is IList)
                {
                    var larg = new ListArgument();
                    var l = ((IList)o);
                    object[] array = new object[l.Count];
                    l.CopyTo(array, 0);
                    larg.Data = new List<NativeArgument>(ParseNativeArguments(array));
                    list.Add(larg);
                }
                else
                {
                    list.Add(null);
                }
            }

            return list;
        }

        public void DecodeNativeCall(NativeData obj)
        {
            if (!NativeWhitelist.IsAllowed(obj.Hash) && obj.Internal == false)
            {
                throw new ArgumentException("Hash \"" + obj.Hash.ToString("X") + "\" is not allowed!");
            }
            else if (obj.Hash == (ulong)Hash.REQUEST_SCRIPT_AUDIO_BANK)
            {
                if (!SoundWhitelist.IsAllowed(((StringArgument)obj.Arguments[0]).Data))
                {
                    throw new ArgumentException("Such SoundSet is not allowed!");
                }
            }
            else if (obj.Hash == (ulong)Hash.PLAY_SOUND_FRONTEND)
            {
                if (!SoundWhitelist.IsAllowed(((StringArgument)obj.Arguments[1]).Data) || !SoundWhitelist.IsAllowed(((StringArgument)obj.Arguments[2]).Data))
                {
                    throw new ArgumentException("SoundSet/Name is not allowed!");
                }

            }


            var list = new List<InputArgument>();

            var nativeType = CheckNativeHash(obj.Hash);
            LogManager.DebugLog("NATIVE TYPE IS " + nativeType);
            int playerHealth = Game.Player.Character.Health;

            if (((int)nativeType & (int)NativeType.VehicleWarp) > 0)
            {
                int veh = ((EntityArgument)obj.Arguments[1]).NetHandle;
                var item = NetEntityHandler.NetToStreamedItem(veh);
                if (item != null && !item.StreamedIn) NetEntityHandler.StreamIn(item);
            }

            if (((int)nativeType & (int)NativeType.EntityWarp) > 0)
            {
                float x = ((FloatArgument)obj.Arguments[1]).Data;
                float y = ((FloatArgument)obj.Arguments[2]).Data;
                float z = ((FloatArgument)obj.Arguments[3]).Data;

                int interior;
                if ((interior = Function.Call<int>(Hash.GET_INTERIOR_AT_COORDS, x, y, z)) != 0)
                {
                    Function.Call((Hash)0x2CA429C029CCF247, interior); // LOAD_INTERIOR
                    Function.Call(Hash.SET_INTERIOR_ACTIVE, interior, true);
                    Function.Call(Hash.DISABLE_INTERIOR, interior, false);
                    if (Function.Call<bool>(Hash.IS_INTERIOR_CAPPED, interior))
                        Function.Call(Hash.CAP_INTERIOR, interior, false);
                }
            }

            var objectList = DecodeArgumentList(obj.Arguments.ToArray());

            list.AddRange(objectList.Select(ob => ob is OutputArgument ? (OutputArgument)ob : new InputArgument(ob)));

            if (objectList.Count() > 0)
                LogManager.DebugLog("NATIVE CALL ARGUMENTS: " + objectList.Aggregate((f, s) => f + ", " + s) + ", RETURN TYPE: " + obj.ReturnType);
            Model model = null;
            if (((int)nativeType & (int)NativeType.NeedsModel) > 0)
            {
                LogManager.DebugLog("REQUIRES MODEL");
                int position = 0;
                if (((int)nativeType & (int)NativeType.NeedsModel1) > 0)
                    position = 0;
                if (((int)nativeType & (int)NativeType.NeedsModel2) > 0)
                    position = 1;
                if (((int)nativeType & (int)NativeType.NeedsModel3) > 0)
                    position = 2;
                LogManager.DebugLog("POSITION IS " + position);
                var modelObj = obj.Arguments[position];
                int modelHash = 0;
                if (modelObj is UIntArgument)
                {
                    modelHash = unchecked((int)((UIntArgument)modelObj).Data);
                }
                else if (modelObj is IntArgument)
                {
                    modelHash = ((IntArgument)modelObj).Data;
                }
                LogManager.DebugLog("MODEL HASH IS " + modelHash);
                model = new Model(modelHash);

                if (model.IsValid)
                {
                    LogManager.DebugLog("MODEL IS VALID, REQUESTING");
                    model.Request(10000);
                }
            }

            if (((int)nativeType & (int)NativeType.NeedsAnimDict) > 0)
            {
                var animDict = ((StringArgument)obj.Arguments[1]).Data;
                Util.Util.LoadDict(animDict);
            }

            if (((int)nativeType & (int)NativeType.PtfxAssetRequest) != 0)
            {
                var animDict = ((StringArgument)obj.Arguments[0]).Data;

                Util.Util.LoadPtfxAsset(animDict);
                Function.Call(Hash.USE_PARTICLE_FX_ASSET, animDict);

                list.RemoveAt(0);
            }

            if (((int)nativeType & (int)NativeType.ReturnsEntity) > 0)
            {
                var entId = Function.Call<int>((Hash)obj.Hash, list.ToArray());
                lock (EntityCleanup) EntityCleanup.Add(entId);
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, entId);
                }

                if (model != null)
                    model.MarkAsNoLongerNeeded();
                return;
            }

            if (nativeType == NativeType.ReturnsBlip)
            {
                var blipId = Function.Call<int>((Hash)obj.Hash, list.ToArray());
                lock (BlipCleanup) BlipCleanup.Add(blipId);
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, blipId);
                }
                return;
            }

            if (((int)nativeType & (int)NativeType.TimeSet) > 0)
            {
                var newHours = ((IntArgument)obj.Arguments[0]).Data;
                var newMinutes = ((IntArgument)obj.Arguments[1]).Data;
                Time = new TimeSpan(newHours, newMinutes, 0);
            }

            if (((int)nativeType & (int)NativeType.WeatherSet) > 0)
            {
                var newWeather = ((IntArgument)obj.Arguments[0]).Data;
                if (newWeather >= 0 && newWeather < _weather.Length)
                {
                    Weather = _weather[newWeather];
                    Function.Call((Hash)obj.Hash, _weather[newWeather]);
                    return;
                }
            }

            var tmpArgs = obj.Arguments;

            if (!ReplacePointerNatives(obj.Hash, ref list, ref tmpArgs))
                return;

            if (obj.ReturnType == null)
            {
                Function.Call((Hash)obj.Hash, list.ToArray());
            }
            else
            {
                if (obj.ReturnType is IntArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<int>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is UIntArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<uint>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is StringArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<string>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is FloatArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<float>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is BooleanArgument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<bool>((Hash)obj.Hash, list.ToArray()));
                }
                else if (obj.ReturnType is Vector3Argument)
                {
                    SendNativeCallResponse(obj.Id, Function.Call<Vector3>((Hash)obj.Hash, list.ToArray()));
                }
            }

            if (((int)nativeType & (int)NativeType.PlayerSkinChange) > 0)
            {
                Game.Player.Character.SetDefaultClothes();
                Game.Player.Character.MaxHealth = 200;
                Game.Player.Character.Health = playerHealth;
            }
        }

        public void SendNativeCallResponse(uint id, object response)
        {
            var obj = new NativeResponse();
            obj.Id = id;

            if (response is int)
            {
                obj.Response = new IntArgument() { Data = ((int)response) };
            }
            else if (response is uint)
            {
                obj.Response = new UIntArgument() { Data = ((uint)response) };
            }
            else if (response is string)
            {
                obj.Response = new StringArgument() { Data = ((string)response) };
            }
            else if (response is float)
            {
                obj.Response = new FloatArgument() { Data = ((float)response) };
            }
            else if (response is bool)
            {
                obj.Response = new BooleanArgument() { Data = ((bool)response) };
            }
            else if (response is Vector3)
            {
                var tmp = (Vector3)response;
                obj.Response = new Vector3Argument()
                {
                    X = tmp.X,
                    Y = tmp.Y,
                    Z = tmp.Z,
                };
            }

            var msg = Client.CreateMessage();
            var bin = SerializeBinary(obj);
            msg.Write((byte)PacketType.NativeResponse);
            msg.Write(bin.Length);
            msg.Write(bin);
            Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        private bool ReplacePointerNatives(ulong hash, ref List<InputArgument> list, ref List<NativeArgument> args)
        {
            if (hash == 0x202709F4C58A0424) // _SET_NOTIFICATION_TEXT_ENTRY
            {
                list[0] = new InputArgument("STRING");
                return true;
            }

            if (hash == 0x6C188BE134E074AA && ((StringArgument)args[0]).Data.Length > 99) // ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME
            {
                list[0] = ((StringArgument)args[0]).Data.Substring(0, 99);
            }

            return true;
        }

        private enum NativeType
        {
            Unknown = 0,
            ReturnsBlip = 1 << 1,
            ReturnsEntity = 1 << 2,
            NeedsModel = 1 << 3,
            NeedsModel1 = 1 << 4,
            NeedsModel2 = 1 << 5,
            NeedsModel3 = 1 << 6,
            TimeSet = 1 << 7,
            WeatherSet = 1 << 8,
            VehicleWarp = 1 << 9,
            EntityWarp = 1 << 10,
            NeedsAnimDict = 1 << 11,
            PtfxAssetRequest = 1 << 12,
            PlayerSkinChange = 1 << 13,
        }

        private NativeType CheckNativeHash(ulong hash)
        {
            switch (hash)
            {
                default:
                    return NativeType.Unknown;
                case 0x00A1CADD00108836:
                    return NativeType.NeedsModel2 | NativeType.Unknown | NativeType.NeedsModel | NativeType.PlayerSkinChange;
                case 0xD49F9B0955C367DE:
                    return NativeType.NeedsModel2 | NativeType.NeedsModel | NativeType.ReturnsEntity;
                case 0x7DD959874C1FD534:
                    return NativeType.NeedsModel3 | NativeType.NeedsModel | NativeType.ReturnsEntity;
                case 0xAF35D0D2583051B0:
                case 0x509D5878EB39E842:
                case 0x9A294B2138ABB884:
                    return NativeType.NeedsModel1 | NativeType.NeedsModel | NativeType.ReturnsEntity;
                case 0xEF29A16337FACADB:
                case 0xB4AC7D0CF06BFE8F:
                case 0x9B62392B474F44A0:
                case 0x63C6CCA8E68AE8C8:
                    return NativeType.ReturnsEntity;
                case 0x46818D79B1F7499A:
                case 0x5CDE92C702A8FCE7:
                case 0xBE339365C863BD36:
                case 0x5A039BB0BCA604B6:
                    return NativeType.ReturnsBlip;
                case 0x47C3B5848C3E45D8:
                    return NativeType.TimeSet;
                case 0xED712CA327900C8A:
                    return NativeType.WeatherSet;
                case 0xF75B0D629E1C063D:
                    return NativeType.VehicleWarp;
                case 0x239A3351AC1DA385:
                    return NativeType.EntityWarp;
                case 0xEA47FE3719165B94:
                    return NativeType.NeedsAnimDict;
                case 0x25129531F77B9ED3:
                case 0x0E7E72961BA18619:
                case 0xF56B8137DF10135D:
                case 0xA41B6A43642AC2CF:
                case 0x0D53A3B8DA0809D2:
                case 0xC95EB1DB6E92113D:
                    return NativeType.PtfxAssetRequest;
            }
        }
#endregion

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

#region Properties
        public static void UpdateEntityInfo(int netId, EntityType entity, Delta_EntityProperties newInfo)
        {
            var packet = new UpdateEntity();
            packet.EntityType = (byte)entity;
            packet.Properties = newInfo;
            packet.NetHandle = netId;
            SendToServer(packet, PacketType.UpdateEntityProperties, true, ConnectionChannel.NativeCall);
        }

        public static bool SetEntityProperty(LocalHandle entity, string key, object value)
        {
            var handle = NetEntityHandler.EntityToNet(entity.Value);
            var item = NetEntityHandler.NetToStreamedItem(handle);
            var prop = item as EntityProperties;

            if (prop == null || string.IsNullOrEmpty(key)) return false;

            if (prop.SyncedProperties == null) prop.SyncedProperties = new Dictionary<string, NativeArgument>();

            var nativeArg = ParseNativeArguments(value).Single();

            NativeArgument oldValue = prop.SyncedProperties.Get(key);

            prop.SyncedProperties.Set(key, nativeArg);


            if (!item.LocalOnly)
            {
                var delta = new Delta_EntityProperties();
                delta.SyncedProperties = new Dictionary<string, NativeArgument>();
                delta.SyncedProperties.Add(key, nativeArg);
                UpdateEntityInfo(handle, EntityType.Prop, delta);
            }

            JavascriptHook.InvokeDataChangeEvent(entity, key, DecodeArgumentListPure(oldValue).FirstOrDefault());
            return true;
        }

        public static void ResetEntityProperty(LocalHandle entity, string key)
        {
            var handle = NetEntityHandler.EntityToNet(entity.Value);
            var item = NetEntityHandler.NetToStreamedItem(handle);
            var prop = item as EntityProperties;

            if (prop == null || string.IsNullOrEmpty(key)) return;

            if (prop.SyncedProperties == null || !prop.SyncedProperties.ContainsKey(key)) return;

            prop.SyncedProperties.Remove(key);

            if (!item.LocalOnly)
            {
                var delta = new Delta_EntityProperties();
                delta.SyncedProperties = new Dictionary<string, NativeArgument>();
                delta.SyncedProperties.Add(key, new LocalGamePlayerArgument());
                UpdateEntityInfo(handle, EntityType.Prop, delta);
            }
        }

        public static bool HasEntityProperty(LocalHandle entity, string key)
        {
            var handle = NetEntityHandler.EntityToNet(entity.Value);
            var prop = NetEntityHandler.NetToStreamedItem(handle) as EntityProperties;

            if (prop == null || string.IsNullOrEmpty(key) || prop.SyncedProperties == null) return false;

            return prop.SyncedProperties.ContainsKey(key);
        }

        public static object GetEntityProperty(LocalHandle entity, string key)
        {
            var handle = NetEntityHandler.EntityToNet(entity.Value);
            var prop = NetEntityHandler.NetToStreamedItem(handle) as EntityProperties;

            if (prop == null || string.IsNullOrEmpty(key)) return null;

            if (prop.SyncedProperties == null || !prop.SyncedProperties.ContainsKey(key)) return null;

            var natArg = prop.SyncedProperties[key];

            return DecodeArgumentListPure(natArg).Single();
        }

        public static string[] GetEntityAllProperties(LocalHandle entity)
        {
            var handle = NetEntityHandler.EntityToNet(entity.Value);
            var prop = NetEntityHandler.NetToStreamedItem(handle) as EntityProperties;

            if (prop == null) return new string[0];

            if (prop.SyncedProperties == null || prop.SyncedProperties.Any(pair => string.IsNullOrEmpty(pair.Key))) return new string[0];

            //return prop.SyncedProperties.Select(pair => DecodeArgumentListPure(pair.Value).Single().ToString()).ToArray(); //Returns all the values
            return prop.SyncedProperties.Select(pair => pair.Key).ToArray();
        }

        public static bool SetWorldData(string key, object value)
        {
            if (NetEntityHandler.ServerWorld.SyncedProperties == null) NetEntityHandler.ServerWorld.SyncedProperties = new Dictionary<string, NativeArgument>();

            var nativeArg = ParseNativeArguments(value).Single();

            NativeArgument oldValue = NetEntityHandler.ServerWorld.SyncedProperties.Get(key);

            NetEntityHandler.ServerWorld.SyncedProperties.Set(key, nativeArg);


            var delta = new Delta_EntityProperties();
            delta.SyncedProperties = new Dictionary<string, NativeArgument>();
            delta.SyncedProperties.Add(key, nativeArg);
            UpdateEntityInfo(1, EntityType.Prop, delta);

            JavascriptHook.InvokeDataChangeEvent(new LocalHandle(0), key, DecodeArgumentListPure(oldValue).FirstOrDefault());
            return true;
        }

        public static void ResetWorldData(string key)
        {
            if (NetEntityHandler.ServerWorld.SyncedProperties == null || !NetEntityHandler.ServerWorld.SyncedProperties.ContainsKey(key)) return;

            NetEntityHandler.ServerWorld.SyncedProperties.Remove(key);

            var delta = new Delta_EntityProperties();
            delta.SyncedProperties = new Dictionary<string, NativeArgument>();
            delta.SyncedProperties.Add(key, new LocalGamePlayerArgument());
            UpdateEntityInfo(1, EntityType.Prop, delta);
        }

        public static bool HasWorldData(string key)
        {
            if (NetEntityHandler.ServerWorld == null || string.IsNullOrEmpty(key) || NetEntityHandler.ServerWorld.SyncedProperties == null) return false;

            return NetEntityHandler.ServerWorld.SyncedProperties.ContainsKey(key);
        }

        public static object GetWorldData(string key)
        {
            if (NetEntityHandler.ServerWorld == null || string.IsNullOrEmpty(key)) return null;

            if (NetEntityHandler.ServerWorld.SyncedProperties == null || !NetEntityHandler.ServerWorld.SyncedProperties.ContainsKey(key)) return null;

            var natArg = NetEntityHandler.ServerWorld.SyncedProperties[key];

            return DecodeArgumentListPure(natArg).Single();
        }

        public static string[] GetAllWorldData()
        {
            if (NetEntityHandler.ServerWorld == null) return new string[0];

            if (NetEntityHandler.ServerWorld.SyncedProperties == null || NetEntityHandler.ServerWorld.SyncedProperties.Any(pair => string.IsNullOrEmpty(pair.Key))) return new string[0];

            return NetEntityHandler.ServerWorld.SyncedProperties.Select(pair => pair.Key).ToArray();
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
            Vector2 point2D;
            if (!WorldToScreenRel(point3D, out point2D)) return camPos + camForward * 10.0f;
            var point3DZero = camPos + camForward * 10.0f;
            Vector2 point2DZero;
            if (!WorldToScreenRel(point3DZero, out point2DZero)) return camPos + camForward * 10.0f;

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
