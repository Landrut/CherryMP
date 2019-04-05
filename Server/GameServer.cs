﻿using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using CherryMPServer.Constant;
using CherryMPServer.Managers;
using CherryMPShared;
using Lidgren.Network;
using Microsoft.CSharp;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using ProtoBuf;

namespace CherryMPServer
{
    internal class StreamingClient
    {
        public StreamingClient(Client c)
        {
            Parent = c;
            ChunkSize = c.NetConnection.Peer.Configuration.MaximumTransmissionUnit - 20;
            Files = new List<StreamedData>();
        }

        public int ChunkSize { get; set; }
        public Client Parent { get; set; }
        public List<StreamedData> Files { get; set; }
    }

    internal class StreamedData
    {
        public StreamedData()
        {
            HasStarted = false;
            BytesSent = 0;
        }

        public bool HasStarted { get; set; }
        public bool Accepted { get; set; }

        public int Id { get; set; }
        public long BytesSent { get; set; }
        public byte[] Data { get; set; }

        public FileType Type { get; set; }
        public string Name { get; set; }
        public string Resource { get; set; }
        public string Hash { get; set; }
    }

    public delegate dynamic ExportedFunctionDelegate(params object[] parameters);
    public delegate void ExportedEvent(params dynamic[] parameters);

    internal class GameServer
    {
        public GameServer(ServerSettings conf)
        {
            Clients = new List<Client>();
            Downloads = new List<StreamingClient>();
            RunningResources = new List<Resource>();
            CommandHandler = new CommandHandler();
            BanManager = new BanManager();
            FileHashes = new Dictionary<string, string>();
            ExportedFunctions = new System.Dynamic.ExpandoObject();
            PickupManager = new PickupManager();
            UnoccupiedVehicleManager = new UnoccupiedVehicleManager();
            NetEntityHandler = new NetEntityHandler();

            Port = conf.Port;
            if (conf.MaxPlayers < 2) MaxPlayers = 2;
            else if (conf.MaxPlayers > 1000) MaxPlayers = 1000;
            else MaxPlayers = conf.MaxPlayers;

            ACLEnabled = conf.UseACL && File.Exists("acl.xml");
            BanManager.Initialize();
            if (ACLEnabled)
            {
                ACL = new AccessControlList("acl.xml");
            }

            ConstantVehicleDataOrganizer.Initialize();

            if (conf.Name != null) Name = conf.Name.Substring(0, Math.Min(58, conf.Name.Length)); // 46 to fill up title + additional 12 chars for colors such as ~g~.. etc..
            Name.Replace("¦", string.Empty);
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            NetPeerConfiguration config = new NetPeerConfiguration("GRANDTHEFTAUTONETWORK");
            var lAdd = IPAddress.Parse(conf.LocalAddress);
            config.LocalAddress = lAdd;
            config.BroadcastAddress = lAdd;
            config.Port = conf.Port;
            config.EnableUPnP = conf.UseUPnP;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            config.MaxPlayers = MaxPlayers;
            config.ConnectionTimeout = 120f; // 30 second timeout
            //config.MaximumConnections = conf.MaxPlayers + 2; // + 2 for discoveries
   

            

            Server = new NetServer(config);
            
            PasswordProtected = !string.IsNullOrWhiteSpace(conf.Password);
            Password = conf.Password;
            AnnounceSelf = conf.Announce;
            MasterServer = conf.MasterServer;
            AnnounceToLAN = conf.AnnounceToLan;
            UseUPnP = conf.UseUPnP;
            MinimumClientVersion = ParseableVersion.Parse(conf.MinimumClientVersion);
            OnFootLagComp = conf.OnFootLagCompensation;
            VehLagComp = conf.VehicleLagCompensation;
            GlobalStreamingRange = conf.GlobalStreamingRange;
            PlayerStreamingRange = conf.PlayerStreamingRange;
            VehicleStreamingRange = conf.VehicleStreamingRange;
            LogLevel = conf.LogLevel;
            UseHTTPFileServer = conf.UseHTTPServer;
            TrustClientProperties = conf.EnableClientsideEntityProperties;
            fqdn = conf.fqdn;
            Conntimeout = conf.Conntimeout;
            AllowCEFDevTool = conf.Allowcefdevtool;

            if (conf.whitelist != null && conf.whitelist != null)
            {
                ModWhitelist = conf.whitelist.Items.Select(item => item.Hash).ToList();
            }

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var index = args.Name.IndexOf(",");
                string actualAssembly = args.Name;

                if (index != -1) actualAssembly = args.Name.Substring(0, index) + ".dll";

                if (AssemblyReferences.ContainsKey(actualAssembly))
                {
                    return Assembly.LoadFrom(AssemblyReferences[actualAssembly]);
                }

                return null;
            };
        }

        public ParseableVersion MinimumClientVersion;
        public NetServer Server;
        public TaskFactory ConcurrentFactory;
        internal List<StreamingClient> Downloads;
        internal SharedAPI PublicAPI = new SharedAPI();
        public int MaxPlayers { get; set; }
        public int Port { get; set; }
        public List<Client> Clients { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public bool PasswordProtected { get; set; }
        public string GamemodeName { get; set; }
        public string fqdn { get; set; }
        public bool Conntimeout { get; set; }
        public bool AllowCEFDevTool { get; set; }
        public Resource Gamemode { get; set; }
        public string MasterServer { get; set; }
        public bool AnnounceSelf { get; set; }
        public int LogLevel { get; set; }
        public bool AnnounceToLAN { get; set; }
        internal AccessControlList ACL { get; set; }
        public bool IsClosing { get; set; }
        public bool ReadyToClose { get; set; }
        public bool ACLEnabled { get; set; }
        public bool UseUPnP { get; set; }
        public bool VehLagComp { get; set; }
        public bool OnFootLagComp { get; set; }
        public int PlayerStreamingRange { get; set; }
        public int GlobalStreamingRange { get; set; }
        public int VehicleStreamingRange { get; set; }
        public List<string> ModWhitelist { get; set; }
        public bool UseHTTPFileServer { get; set; }
        public bool TrustClientProperties { get; set; }
        public string ErrorCmd = "~r~ERROR:~w~ Command not found.";

        public BanManager BanManager;
        public ColShapeManager ColShapeManager;
        public CommandHandler CommandHandler;
        public dynamic ExportedFunctions;

        public List<Resource> RunningResources;
        public PickupManager PickupManager;
        public UnoccupiedVehicleManager UnoccupiedVehicleManager;
        public Thread StreamerThread;
        public FileServer FileServer;

        private Dictionary<string, string> FileHashes { get; set; }
        public Dictionary<NetHandle, Dictionary<string, object>> EntityProperties = 
            new Dictionary<NetHandle, Dictionary<string, object>>();
        public Dictionary<string, object> WorldProperties = new Dictionary<string, object>();

        public Dictionary<int, List<Client>> VehicleOccupants = new Dictionary<int, List<Client>>();

        public NetEntityHandler NetEntityHandler { get; set; }

        public bool AllowDisplayNames { get; set; }

        public List<Resource> AvailableMaps;
        public Resource CurrentMap;

        private object operationKey = new object();

        // Assembly name, Path to assembly.
        public Dictionary<string, string> AssemblyReferences = new Dictionary<string, string>();

        private Dictionary<IPEndPoint, DateTime> queue = new Dictionary<IPEndPoint, DateTime>();
        private List<IPAddress> connBlock = new List<IPAddress>();

        ParseableVersion serverVersion = ParseableVersion.FromAssembly(Assembly.GetExecutingAssembly());

        private DateTime _lastAnnounceDateTime;
        public void Start(string[] filterscripts)
        {
            try
            {
                Server.Start();
            }
            catch (SocketException ex)
            {
                Program.Output("ERROR: Socket Exception when starting server: " + ex.Message);
                Console.Read();
                Program.CloseProgram = true;
                return;
            }

            if (AnnounceSelf)
            {
                _lastAnnounceDateTime = DateTime.Now;
                AnnounceSelfToMaster();
            }

            if (UseUPnP)
            {
                try
                {
                    Server.UPnP.ForwardPort(Port, "Cherry Multiplayer Server");
                }
                catch (Exception ex)
                {
                    Program.Output("UNHANDLED EXCEPTION DURING UPNP PORT FORWARDING. YOUR ROUTER MAY NOT SUPPORT UPNP.");
                    Program.Output(ex.ToString());
                }
            }

            NetEntityHandler.CreateWorld();
            ColShapeManager = new ColShapeManager();

            if (UseHTTPFileServer)
            {
                Program.Output("Starting file server...");

                FileServer = new FileServer();
                FileServer.Start(Port);
            }

            Program.Output("Loading resources...");
            foreach (var path in filterscripts)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                StartResource(path);
            }

            StreamerThread = new Thread(Streamer.MainThread);
            StreamerThread.IsBackground = true;
            StreamerThread.Start();

            //StressTest.Init();
            // Uncomment to start a stress test
        }

        public void AnnounceSelfToMaster()
        {
            if (LogLevel > 0)
                Program.Output("Announcing self to master server...", LogCat.Debug);

            var annThread = new Thread((ThreadStart) delegate
            {
                using (var wb = new WebClient())
                {
                    try
                    {
                        var annObject = new MasterServerAnnounce();

                        annObject.ServerName = Name;
                        annObject.CurrentPlayers = Clients.Count;
                        annObject.MaxPlayers = MaxPlayers;
                        annObject.Map = CurrentMap?.DirectoryName;
                        annObject.Gamemode = string.IsNullOrEmpty(GamemodeName)
                                        ? Gamemode?
                                            .DirectoryName ?? "Cherry Multiplayer"
                                        : GamemodeName;
                        annObject.Port = Port;
                        annObject.Passworded = PasswordProtected;
                        annObject.fqdn = fqdn;
                        annObject.ServerVersion = serverVersion.ToString();

                        wb.UploadData(MasterServer.Trim('/') + "/addserver",
                            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(annObject)));
                    }
                    catch (WebException)
                    {
                        Program.Output("Failed to announce self: master server is not available at this time.", LogCat.Error);
                        //if (LogLevel >= 2)
                        //{
                        //    Program.Output("\n====\n" + ex.ToString() + "\n====\n");
                        //}
                    }
                }
            });
            annThread.IsBackground = true;
            annThread.Start();
        }

        private bool isIPLocal(string ipaddress)
        {
            String[] straryIPAddress = ipaddress.ToString().Split(new String[] { "." }, StringSplitOptions.RemoveEmptyEntries);
            int[] iaryIPAddress = new int[] { int.Parse(straryIPAddress[0]), int.Parse(straryIPAddress[1]), int.Parse(straryIPAddress[2]), int.Parse(straryIPAddress[3]) };
            if (iaryIPAddress[0] == 10 || (iaryIPAddress[0] == 192 && iaryIPAddress[1] == 168) || (iaryIPAddress[0] == 172 && (iaryIPAddress[1] >= 16 && iaryIPAddress[1] <= 31)))
            {
                return true;
            }
            else
            {
                // IP Address is "probably" public. This doesn't catch some VPN ranges like OpenVPN and Hamachi.
                return false;
            }
        }

        public List<ClientsideScript> GetAllClientsideScripts()
        {
            List<ClientsideScript> allScripts = new List<ClientsideScript>();

            lock (RunningResources)
            {
                foreach (var resource in RunningResources)
                {
                    allScripts.AddRange(resource.ClientsideScripts);
                }
            }

            return allScripts;
        }

        public static ResourceInfo GetStoppedResourceInfo(string resourceName)
        {
            if (!Directory.Exists("resources" + Path.DirectorySeparatorChar + resourceName))
                throw new FileNotFoundException("Resource does not exist.");

            var baseDir = "resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar;

            if (!File.Exists(baseDir + "meta.xml"))
                throw new FileNotFoundException("meta.xml has not been found.");

            var xmlSer = new XmlSerializer(typeof(ResourceInfo));
            ResourceInfo currentResInfo;
            using (var str = File.OpenRead(baseDir + "meta.xml"))
                currentResInfo = (ResourceInfo)xmlSer.Deserialize(str);

            return currentResInfo;
        }

        public ResourceInfo GetResourceInfo(string resourceName)
        {
            lock (RunningResources)
            {
                Resource runningResource;

                if ((runningResource = RunningResources.FirstOrDefault(r => r.DirectoryName == resourceName)) != null)
                {
                    return runningResource.Info;
                }
                else
                {
                    return GetStoppedResourceInfo(resourceName);
                }
            }
        }

        public Dictionary<string, CustomSetting> LoadSettings(List<MetaSetting> sets)
        {
            var dict = new Dictionary<string, CustomSetting>();

            if (sets != null)
            foreach (var setting in sets)
            {
                dict.Set(setting.Name, new CustomSetting()
                {
                    Value = setting.Value,
                    DefaultValue = setting.DefaultValue,
                    Description = setting.Description,
                });
            }

            return dict;
        }

        public bool StartResource(string resourceName, string father = null)
        {
            try
            {
                if (RunningResources.Any(res => res.DirectoryName == resourceName)) return false;

                Program.Output("Starting " + resourceName);

                if (!Directory.Exists("resources" + Path.DirectorySeparatorChar + resourceName))
                    throw new FileNotFoundException("Resource does not exist.");

                var baseDir = "resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar;

                if (!File.Exists(baseDir + "meta.xml")) 
                    throw new FileNotFoundException("meta.xml has not been found.");

                var xmlSer = new XmlSerializer(typeof(ResourceInfo));
                ResourceInfo currentResInfo;
                using (var str = File.OpenRead(baseDir + "meta.xml"))
                    currentResInfo = (ResourceInfo)xmlSer.Deserialize(str);
                
                var ourResource = new Resource();
                ourResource.Info = currentResInfo;
                ourResource.DirectoryName = resourceName;
                ourResource.Engines = new List<ScriptingEngine>();
                ourResource.ClientsideScripts = new List<ClientsideScript>();

                if (ourResource.Info.Info != null && ourResource.Info.Info.Type == ResourceType.gamemode)
                {
                    if (Gamemode != null)
                        StopResource(Gamemode.DirectoryName);
                    Gamemode = ourResource;
                }

                if (currentResInfo.ResourceACL != null && ACLEnabled)
                {
                    var aclHead = AccessControlList.ParseXml("resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar + currentResInfo.ResourceACL.Path);
                    ACL.MergeACL(aclHead);
                }
                
                if (currentResInfo.Includes != null)
                    foreach (var resource in currentResInfo.Includes)
                    {
                        if (string.IsNullOrWhiteSpace(resource.Resource) || resource.Resource == father) continue;
                        StartResource(resource.Resource, resourceName);
                    }

                FileModule.ExportedFiles.Set(resourceName, new List<FileDeclaration>());

                foreach (var filePath in currentResInfo.Files)
                {
                    using (var md5 = MD5.Create())
                    using (var stream = File.OpenRead("resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar + filePath.Path))
                    {
                        var myData = md5.ComputeHash(stream);

                        var keyName = ourResource.DirectoryName + "_" + filePath.Path;

                        string hash = myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right);

                        if (FileHashes.ContainsKey(keyName))
                            FileHashes[keyName] = hash;
                        else
                            FileHashes.Add(keyName, hash);

                        FileModule.ExportedFiles[resourceName].Add(new FileDeclaration(filePath.Path, hash, FileType.Normal));
                    }
                }

                if (currentResInfo.ConfigFiles != null)
                foreach (var filePath in currentResInfo.ConfigFiles.Where(cfg => cfg.Type == ScriptType.client))
                {
                    using (var md5 = MD5.Create())
                    using (var stream = File.OpenRead("resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar + filePath.Path))
                    {
                        var myData = md5.ComputeHash(stream);

                        var keyName = ourResource.DirectoryName + "_" + filePath.Path;

                        string hash = myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right);

                        if (FileHashes.ContainsKey(keyName))
                            FileHashes[keyName] = hash;
                        else
                            FileHashes.Add(keyName, hash);

                        FileModule.ExportedFiles[resourceName].Add(new FileDeclaration(filePath.Path, hash, FileType.Normal));
                    }
                }

                if (currentResInfo.settings != null)
                {
                    if (string.IsNullOrEmpty(currentResInfo.settings.Path))
                    {
                        ourResource.Settings = LoadSettings(currentResInfo.settings.Settings);
                    }
                    else
                    {
                        var ser2 = new XmlSerializer(typeof(ResourceSettingsFile));

                        ResourceSettingsFile file;

                        using (var stream = File.Open(currentResInfo.settings.Path, FileMode.Open))
                            file = ser2.Deserialize(stream) as ResourceSettingsFile;

                        if (file != null)
                        {
                            ourResource.Settings = LoadSettings(file.Settings);
                        }
                    }
                }

                // Load assembly references
                if (currentResInfo.References != null)
                    foreach (var ass in currentResInfo.References)
                    {
                        AssemblyReferences.Set(ass.Name,
                            "resources" + Path.DirectorySeparatorChar + resourceName + Path.DirectorySeparatorChar + ass.Name);
                    }
                

                var csScripts = new List<ClientsideScript>();

                var cSharp = new List<string>();
                var vBasic = new List<string>();

                bool multithreaded = false;

                if (ourResource.Info.Info != null)
                {
                    multithreaded = ourResource.Info.Info.Multithreaded;
                }

                foreach (var script in currentResInfo.Scripts)
                {
                    if (script.Language == ScriptingEngineLanguage.javascript)
                    {
                        var scrTxt = File.ReadAllText(baseDir + script.Path);
                        if (script.Type == ScriptType.client)
                        {
                            var csScript = new ClientsideScript()
                            {
                                ResourceParent = resourceName,
                                Script = scrTxt,
                                //Filename = Path.GetFileNameWithoutExtension(script.Path)?.Replace('.', '_'),
                                Filename = script.Path,
                            };

                            string hash;
                            
                            using (var md5 = MD5.Create())
                            { 
                                var myData = md5.ComputeHash(Encoding.UTF8.GetBytes(scrTxt));
                                hash = myData.Select(byt => byt.ToString("x2")).Aggregate((left, right) => left + right);
                                csScript.MD5Hash = hash;

                                if (FileHashes.ContainsKey(ourResource.DirectoryName + "_" + script.Path))
                                    FileHashes[ourResource.DirectoryName + "_" + script.Path] = hash;
                                else
                                    FileHashes.Add(ourResource.DirectoryName + "_" + script.Path, hash);
                            }

                            FileModule.ExportedFiles[resourceName].Add(new FileDeclaration(script.Path, hash, FileType.Script));

                            ourResource.ClientsideScripts.Add(csScript);
                            csScripts.Add(csScript);
                            continue;
                        }
                    }
                    else if (script.Language == ScriptingEngineLanguage.compiled)
                    {
                        try
                        {
                            Program.DeleteFile(baseDir + script.Path + ":Zone.Identifier");
                        }
                        catch
                        {
                        }
                        Assembly ass;

                        if (ourResource.Info.Info.Shadowcopy)
                        {
                            byte[] bytes = File.ReadAllBytes(baseDir + script.Path);
                            ass = Assembly.Load(bytes);
                        }
                        else
                        {
                            ass = Assembly.LoadFrom(baseDir + script.Path);
                        }

                        var instances = InstantiateScripts(ass);
                        ourResource.Engines.AddRange(instances.Select(sss => new ScriptingEngine(sss, sss.GetType().Name, ourResource, multithreaded)));
                    }
                    else if (script.Language == ScriptingEngineLanguage.csharp)
                    {
                        cSharp.Add(baseDir + script.Path);
                    }
                    else if (script.Language == ScriptingEngineLanguage.vbasic)
                    {
                        var scrTxt = File.ReadAllText(baseDir + script.Path);
                        vBasic.Add(scrTxt);
                    }
                }



                if (cSharp.Count > 0)
                {
                    var csharpAss = CompileScript(cSharp.ToArray(), currentResInfo.References.Select(r => r.Name).ToArray(), false);
                    if (csharpAss != null)
                        ourResource.Engines.AddRange(csharpAss.Select(sss => new ScriptingEngine(sss, sss.GetType().Name, ourResource, multithreaded)));
                }

                if (vBasic.Count > 0)
                {
                    var vbasicAss = CompileScript(vBasic.ToArray(), currentResInfo.References.Select(r => r.Name).ToArray(), true);
                    if (vbasicAss != null)
                        ourResource.Engines.AddRange(vbasicAss.Select(sss => new ScriptingEngine(sss, sss.GetType().Name, ourResource, multithreaded)));
                }

                CommandHandler.Register(ourResource);
                
                var randGen = new Random();
                
                if (ourResource.ClientsideScripts.Count > 0 || currentResInfo.Files.Count > 0)
                foreach (var client in Clients)
                {
                    var downloader = new StreamingClient(client);

                    if (!UseHTTPFileServer)
                    {
                        foreach (var file in currentResInfo.Files)
                        {
                            var fileData = new StreamedData();
                            fileData.Id = randGen.Next(int.MaxValue);
                            fileData.Type = FileType.Normal;
                            fileData.Data =
                                File.ReadAllBytes("resources" + Path.DirectorySeparatorChar +
                                                  ourResource.DirectoryName +
                                                  Path.DirectorySeparatorChar +
                                                  file.Path);
                            fileData.Name = file.Path;
                            fileData.Resource = ourResource.DirectoryName;
                            fileData.Hash = FileHashes.ContainsKey(ourResource.DirectoryName + "_" + file.Path)
                                ? FileHashes[ourResource.DirectoryName + "_" + file.Path]
                                : null;

                            downloader.Files.Add(fileData);
                        }
                    }
                    else
                    {
                        var msg = Server.CreateMessage();
                        msg.Write((byte)PacketType.RedownloadManifest);
                        client.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.FileTransfer);
                    }

                    foreach (var script in ourResource.ClientsideScripts)
                    {
                        var scriptData = new StreamedData();
                        scriptData.Id = randGen.Next(int.MaxValue);
                        scriptData.Data = Encoding.UTF8.GetBytes(script.Script);
                        scriptData.Type = FileType.Script;
                        scriptData.Resource = script.ResourceParent;
                        scriptData.Hash = script.MD5Hash;
                        scriptData.Name = script.Filename;
                        downloader.Files.Add(scriptData);
                    }

                    var endStream = new StreamedData();
                    endStream.Id = randGen.Next(int.MaxValue);
                    endStream.Data = new byte[] { 0xDE, 0xAD, 0xF0, 0x0D };
                    endStream.Type = FileType.EndOfTransfer;
                    downloader.Files.Add(endStream);

                    Downloads.Add(downloader);
                }

                if (ourResource.Info.Map != null && !string.IsNullOrWhiteSpace(ourResource.Info.Map.Path))
                {
                    ourResource.Map = new XmlGroup();
                    ourResource.Map.Load("resources\\" + ourResource.DirectoryName +"\\" + ourResource.Info.Map.Path);

                    LoadMap(ourResource, ourResource.Map, ourResource.Info.Map.Dimension);

                    if (ourResource.Info.Info.Type == ResourceType.gamemode)
                    {
                        if (CurrentMap != null) StopResource(CurrentMap.DirectoryName);
                        ourResource.Engines.ForEach(cs => cs.InvokeMapChange(ourResource.DirectoryName, ourResource.Map));
                    }
                    else if (ourResource.Info.Info.Type == ResourceType.map)
                    {
                        if (string.IsNullOrWhiteSpace(ourResource.Info.Info.Gamemodes))
                        {}
                        else if (ourResource.Info.Info.Gamemodes?.Split(',').Length != 1 && Gamemode == null)
                        {}
                        else if (ourResource.Info.Info.Gamemodes?.Split(',').Length == 1 && (Gamemode == null || !ourResource.Info.Info.Gamemodes.Split(',').Contains(Gamemode.DirectoryName)))
                        {
                            if (CurrentMap != null) StopResource(CurrentMap.DirectoryName);
                            StartResource(ourResource.Info.Info.Gamemodes?.Split(',')[0]);

                            CurrentMap = ourResource;
                            Gamemode.Engines.ForEach(cs => cs.InvokeMapChange(ourResource.DirectoryName, ourResource.Map));
                        }
                        else if (Gamemode != null && ourResource.Info.Info.Gamemodes.Split(',').Contains(Gamemode.DirectoryName))
                        {
                            Program.Output("Starting map " + ourResource.DirectoryName + "!");
                            if (CurrentMap != null) StopResource(CurrentMap.DirectoryName);
                            CurrentMap = ourResource;
                            Gamemode.Engines.ForEach(cs => cs.InvokeMapChange(ourResource.DirectoryName, ourResource.Map));
                        }
                    }
                }

                if (ourResource.Info.ExportedFunctions != null)
                {
                    var gPool = ExportedFunctions as IDictionary<string, object>;
                    dynamic resPool = new System.Dynamic.ExpandoObject();
                    var resPoolDict = resPool as IDictionary<string, object>;

                    foreach (var func in ourResource.Info.ExportedFunctions)
                    {
                        ScriptingEngine engine;
                        if (string.IsNullOrEmpty(func.Path))
                            engine = ourResource.Engines.SingleOrDefault();
                        else
                            engine = ourResource.Engines.FirstOrDefault(en => en.Filename == func.Path);

                        if (engine == null) continue;

                        if (string.IsNullOrWhiteSpace(func.EventName))
                        {
                            ExportedFunctionDelegate punchthrough = new ExportedFunctionDelegate((ExportedFunctionDelegate)
                                delegate (object[] parameters)
                                {
                                    return engine.InvokeMethod(func.Name, parameters);
                                });
                            resPoolDict.Add(func.Name, punchthrough);
                        }
                        else
                        {
                            var eventInfo = engine._compiledScript.GetType().GetEvent(func.EventName);

                            if (eventInfo == null)
                            {
                                Program.Output("WARN: Exported event " + func.EventName + " has not been found!");
                                if (LogLevel > 0)
                                {
                                    Program.Output("Available events:");
                                    Program.Output(string.Join(", ", engine._compiledScript.GetType().GetEvents().Select(ev => ev.Name)));
                                }
                            }
                            else
                            {

                                resPoolDict.Add(func.EventName, null);

                                ExportedEvent punchthrough = new ExportedEvent((ExportedEvent)
                                    delegate(dynamic[] parameters)
                                    {
                                        ExportedEvent e = resPoolDict[func.EventName] as ExportedEvent;

                                        if (e != null)
                                        {
                                            e.Invoke(parameters);
                                        }
                                    });

                                eventInfo.AddEventHandler(engine._compiledScript, punchthrough);
                            }
                        }
                    }

                    gPool.Add(ourResource.DirectoryName, resPool);
                }
                
                foreach (var engine in ourResource.Engines)
                {
                    engine.InvokeResourceStart();
                }

                var oldRes = new List<Resource>(RunningResources);
                lock (RunningResources) RunningResources.Add(ourResource);

                foreach (var resource in oldRes)
                {
                    resource.Engines.ForEach(en => en.InvokeServerResourceStart(ourResource.DirectoryName));
                }

                Program.Output("Resource " + ourResource.DirectoryName + " started!");
                return true;
            }
            catch (Exception ex)
            {
                Program.Output("ERROR STARTING RESOURCE " + resourceName);
                Program.Output(ex.ToString());
                return false;
            }
        }

        public bool StopResource(string resourceName, Resource[] resourceParent = null)
        {
            Resource ourRes;
            lock (RunningResources)
            {
                ourRes = RunningResources.FirstOrDefault(r => r.DirectoryName == resourceName);
                if (ourRes == null) return false;

                Program.Output("Stopping " + resourceName);
                
                RunningResources.Remove(ourRes);
            }

            ourRes.Engines.ForEach(en => en.InvokeResourceStop());

            var msg = Server.CreateMessage();
            msg.Write((byte) PacketType.StopResource);
            msg.Write(resourceName);
            Server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);

            if (Gamemode == ourRes)
            {
                if (CurrentMap != null && CurrentMap != ourRes)
                {
                    StopResource(CurrentMap.DirectoryName);
                    CurrentMap = null;
                }
                    
                Gamemode = null;
            }

            if (ourRes.MapEntities != null)
            foreach (var entity in ourRes.MapEntities)
            {
                PublicAPI.deleteEntity(entity);
            }

            if (CurrentMap == ourRes) CurrentMap = null;

            var gPool = ExportedFunctions as IDictionary<string, object>;
            if (gPool.ContainsKey(ourRes.DirectoryName)) gPool.Remove(ourRes.DirectoryName);
            CommandHandler.Unregister(ourRes.DirectoryName);
            FileModule.ExportedFiles.Remove(resourceName);
            lock (RunningResources)
            {
                foreach (var resource in RunningResources)
                {
                    resource.Engines.ForEach(en => en.InvokeServerResourceStop(ourRes.DirectoryName));
                }
            }

            Program.Output("Stopped " + resourceName + "!");
            return true;
        }

        public void LoadMap(Resource res, XmlGroup map, int dimension)
        {
            res.MapEntities = new List<NetHandle>();

            var world = map.getElementByType("world");
            if (world != null)
            {
                if (world.hasElementData("time"))
                {
                    var time = world.getElementData<TimeSpan>("time");
                    PublicAPI.setTime(time.Hours, time.Minutes);
                }

                if (world.hasElementData("weather")) // TODO: Change to integer w/ an array of possible weathers
                {
                    PublicAPI.setWeather(world.getElementData<int>("weather"));
                }
            }

            var props = map.getElementsByType("prop");
            foreach (var prop in props)
            {
                if (prop.hasElementData("quatX"))
                {
                    var ent = PublicAPI.createObject(prop.getElementData<int>("model"),
                        new Vector3(prop.getElementData<float>("posX"), prop.getElementData<float>("posY"),
                            prop.getElementData<float>("posZ")),
                        new Quaternion(prop.getElementData<float>("quatX"), prop.getElementData<float>("quatY"),
                            prop.getElementData<float>("quatZ"), prop.getElementData<float>("quatW")), dimension);
                    res.MapEntities.Add(ent);
                }
                else
                {
                    var ent = PublicAPI.createObject(prop.getElementData<int>("model"),
                        new Vector3(prop.getElementData<float>("posX"), prop.getElementData<float>("posY"),
                            prop.getElementData<float>("posZ")),
                        new Vector3(prop.getElementData<float>("rotX"), prop.getElementData<float>("rotY"),
                            prop.getElementData<float>("rotZ")), dimension);
                    res.MapEntities.Add(ent);
                }
            }

            var vehicles = map.getElementsByType("vehicle");
            foreach (var vehicle in vehicles)
            {
                var ent = PublicAPI.createVehicle((VehicleHash)vehicle.getElementData<int>("model"),
                    new Vector3(vehicle.getElementData<float>("posX"), vehicle.getElementData<float>("posY"),
                        vehicle.getElementData<float>("posZ")),
                    new Vector3(vehicle.getElementData<float>("rotX"), vehicle.getElementData<float>("rotY"),
                        vehicle.getElementData<float>("rotZ")), vehicle.getElementData<int>("color1"),
                    vehicle.getElementData<int>("color2"), dimension);
                res.MapEntities.Add(ent);
            }

            var pickups = map.getElementsByType("pickup");
            foreach (var vehicle in pickups)
            {
                var ent = PublicAPI.createPickup((PickupHash)vehicle.getElementData<int>("model"),
                    new Vector3(vehicle.getElementData<float>("posX"), vehicle.getElementData<float>("posY"),
                        vehicle.getElementData<float>("posZ")),
                    new Vector3(vehicle.getElementData<float>("rotX"), vehicle.getElementData<float>("rotY"),
                        vehicle.getElementData<float>("rotZ")), vehicle.getElementData<int>("amount"), vehicle.getElementData<uint>("respawn"), dimension);
                res.MapEntities.Add(ent);
            }

            var markers = map.getElementsByType("marker");
            foreach (var vehicle in markers)
            {
                var ent = PublicAPI.createMarker(vehicle.getElementData<int>("model"),
                    new Vector3(vehicle.getElementData<float>("posX"), vehicle.getElementData<float>("posY"),
                        vehicle.getElementData<float>("posZ")),
                    new Vector3(vehicle.getElementData<float>("dirX"), vehicle.getElementData<float>("dirY"),
                        vehicle.getElementData<float>("dirZ")),
                    new Vector3(vehicle.getElementData<float>("rotX"), vehicle.getElementData<float>("rotY"),
                        vehicle.getElementData<float>("rotZ")),
                    new Vector3(vehicle.getElementData<float>("scaleX"), vehicle.getElementData<float>("scaleY"),
                        vehicle.getElementData<float>("scaleZ")), vehicle.getElementData<int>("alpha"),
                    vehicle.getElementData<int>("red"), vehicle.getElementData<int>("green"), vehicle.getElementData<int>("blue"), dimension);
                res.MapEntities.Add(ent);
            }

            var blips = map.getElementsByType("blip");
            foreach (var vehicle in blips)
            {
                var ent = PublicAPI.createBlip(
                    new Vector3(vehicle.getElementData<float>("posX"), vehicle.getElementData<float>("posY"),
                        vehicle.getElementData<float>("posZ")), dimension);

                if (vehicle.hasElementData("sprite"))
                    PublicAPI.setBlipSprite(ent, vehicle.getElementData<int>("sprite"));
                if (vehicle.hasElementData("color"))
                    PublicAPI.setBlipColor(ent, vehicle.getElementData<int>("color"));
                if (vehicle.hasElementData("scale"))
                    PublicAPI.setBlipScale(ent, vehicle.getElementData<float>("scale"));
                if (vehicle.hasElementData("shortRange"))
                    PublicAPI.setBlipShortRange(ent, vehicle.getElementData<bool>("shortRange"));

                res.MapEntities.Add(ent);
            }

            var peds = map.getElementsByType("ped");
            foreach (var vehicle in peds)
            {
                var ent = PublicAPI.createPed((PedHash)vehicle.getElementData<int>("model"),
                    new Vector3(vehicle.getElementData<float>("posX"), vehicle.getElementData<float>("posY"),
                        vehicle.getElementData<float>("posZ")),vehicle.getElementData<float>("heading"), dimension);
                res.MapEntities.Add(ent);
            }

            var labels = map.getElementsByType("textlabel");
            foreach (var vehicle in labels)
            {
                var ent = PublicAPI.createTextLabel(vehicle.getElementData<string>("text"),
                    new Vector3(vehicle.getElementData<float>("posX"), vehicle.getElementData<float>("posY"),
                        vehicle.getElementData<float>("posZ")), vehicle.getElementData<float>("range"), vehicle.getElementData<float>("size"), dimension: dimension);
                res.MapEntities.Add(ent);
            }

            var neededInteriors = map.getElementsByType("ipl");
            foreach (var point in neededInteriors)
            {
                PublicAPI.requestIpl(point.getElementData<string>("name"));
            }

            var removedInteriors = map.getElementsByType("removeipl");
            foreach (var point in removedInteriors)
            {
                PublicAPI.removeIpl(point.getElementData<string>("name"));
            }
        }
        
        private IEnumerable<Script> InstantiateScripts(Assembly targetAssembly)
        {
            //var types = targetAssembly.GetExportedTypes();
            var types = targetAssembly.GetTypes();
            var validTypes = types.Where(t =>
                !t.IsInterface &&
                !t.IsAbstract)
                .Where(t => typeof(Script).IsAssignableFrom(t));
            if (!validTypes.Any())
            {
                yield break;
            }
            foreach (var type in validTypes)
            {
                var obj = Activator.CreateInstance(type) as Script;
                if (obj != null)
                    yield return obj;
            }
        }

        private IEnumerable<Script> CompileScript(string[] script, string[] references, bool vbBasic = false)
        {
            var provide = new CSharpCodeProvider();
            var vBasicProvider = new VBCodeProvider();

            var compParams = new CompilerParameters();

            compParams.ReferencedAssemblies.Add("System.Drawing.dll");
            compParams.ReferencedAssemblies.Add("System.Windows.Forms.dll");
            compParams.ReferencedAssemblies.Add("System.IO.dll");
            compParams.ReferencedAssemblies.Add("System.Linq.dll");
            compParams.ReferencedAssemblies.Add("System.Core.dll");
            compParams.ReferencedAssemblies.Add("System.dll");
            compParams.ReferencedAssemblies.Add("Microsoft.CSharp.dll");
            compParams.ReferencedAssemblies.Add("CherryMP.Server.exe");
            compParams.ReferencedAssemblies.Add("CherryMP.Shared.dll");

            foreach (var s in references)
            {
                if (File.Exists(AssemblyReferences[s]))
                    compParams.ReferencedAssemblies.Add(AssemblyReferences[s]);
                else compParams.ReferencedAssemblies.Add(s);
            }
            
            compParams.GenerateInMemory = true;
            compParams.GenerateExecutable = false;

            for (int s = 0; s < script.Length; s++)
            {
                if (!vbBasic && script[s].TrimStart().StartsWith("public Constructor"))
                {
                    script[s] = string.Format(@"
using System;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CherryMPServer;
using CherryMPShared;

namespace GTANResource
{{
    public class Constructor{1} : Script
    {{
        {0}
    }}
}}", script[s].Replace("Constructor(", "Constructor" + s + "("), s);
                }
            }
            
            try
            {
                CompilerResults results;
                results = !vbBasic
                    ? provide.CompileAssemblyFromFile(compParams, script)
                    : vBasicProvider.CompileAssemblyFromSource(compParams, script);

                if (results.Errors.HasErrors)
                {
                    var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                    bool allWarns = true;
                    Program.Output("Error/warning while compiling script!", LogCat.Warn);
                    foreach (CompilerError error in results.Errors)
                    {
                        Program.Output(
                            String.Format("{3} ({0}) at {4}:{2}: {1}",
                            error.ErrorNumber,
                            error.ErrorText,
                            error.Line,
                            error.IsWarning ? "Warning" : "Error",
                            error.FileName.Substring(basePath.Length + 1)), error.IsWarning ? LogCat.Warn : LogCat.Error);

                        allWarns = allWarns && error.IsWarning;
                    }

                    
                    if (!allWarns)
                        return null;
                }

                var asm = results.CompiledAssembly;
                return InstantiateScripts(asm);
            }
            catch (Exception ex)
            {
                Program.Output("Error while compiling assembly!", LogCat.Error);
                Program.Output(ex.Message, LogCat.Error);
                Program.Output(ex.StackTrace);

                Program.Output(ex.Source);
                return null;
            }
        }

        public void UpdateEntityInfo(int netId, EntityType entity, Delta_EntityProperties newInfo, Client exclude = null)
        {
            var packet = new UpdateEntity();
            packet.EntityType = (byte)entity;
            packet.Properties = newInfo;
            packet.NetHandle = netId;
            if (exclude == null)
                Program.ServerInstance.SendToAll(packet, PacketType.UpdateEntityProperties, true, ConnectionChannel.NativeCall);
            else
                Program.ServerInstance.SendToAll(packet, PacketType.UpdateEntityProperties, true, exclude, ConnectionChannel.NativeCall);
        }

        internal void ResendPacket(PedData fullPacket, Client exception, bool pure)
        {
            byte[] full = new byte[0];
            byte[] basic = new byte[0];

            if (pure)
            {
                full = PacketOptimization.WritePureSync(fullPacket);
                basic = PacketOptimization.WriteBasicSync(fullPacket.NetHandle.Value, fullPacket.Position);
            }
            else
            {
                full = PacketOptimization.WriteLightSync(fullPacket);
            }

            foreach(var client in exception.Streamer.GetNearClients())
            {
                if (client.Fake) continue;
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client == exception) continue;

                NetOutgoingMessage msg = Server.CreateMessage();
                if (pure)
                {
                    if (client.Position == null) continue;
                    if (client.Position.DistanceToSquared(fullPacket.Position) > GlobalStreamingRange * GlobalStreamingRange) // 1km
                    {
                        var lastUpdateReceived = client.LastPacketReceived.Get(exception.handle.Value);

                        if (lastUpdateReceived == 0 || Program.GetTicks() - lastUpdateReceived > 1000)
                        { 
                            msg.Write((byte) PacketType.BasicSync);
                            msg.Write(basic.Length);
                            msg.Write(basic);
                            Server.SendMessage(msg, client.NetConnection,
                                NetDeliveryMethod.UnreliableSequenced,
                                (int) ConnectionChannel.BasicSync);

                            client.LastPacketReceived.Set(exception.handle.Value, Program.GetTicks());
                        }
                    }
                    else
                    {
                        msg.Write((byte)PacketType.PedPureSync);
                        msg.Write(full.Length);
                        msg.Write(full);
                        Server.SendMessage(msg, client.NetConnection,
                            NetDeliveryMethod.UnreliableSequenced,
                            (int)ConnectionChannel.PureSync);
                    }
                }
                else
                {
                    msg.Write((byte)PacketType.PedLightSync);
                    msg.Write(full.Length);
                    msg.Write(full);
                    Server.SendMessage(msg, client.NetConnection,
                        NetDeliveryMethod.ReliableSequenced,
                        (int)ConnectionChannel.LightSync);
                }
            }

            foreach (var client in exception.Streamer.GetFarClients())
            {
                if (client.Fake) continue;
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client == exception) continue;

                NetOutgoingMessage msg = Server.CreateMessage();
                if (pure)
                {
                    var lastUpdateReceived = client.LastPacketReceived.Get(exception.handle.Value);

                    if (lastUpdateReceived == 0 || Program.GetTicks() - lastUpdateReceived > 1000)
                    {
                        msg.Write((byte)PacketType.BasicSync);
                        msg.Write(basic.Length);
                        msg.Write(basic);
                        Server.SendMessage(msg, client.NetConnection,
                            NetDeliveryMethod.UnreliableSequenced,
                            (int)ConnectionChannel.BasicSync);

                        client.LastPacketReceived.Set(exception.handle.Value, Program.GetTicks());
                    }
                }
            }
        }

        internal void ResendBulletPacket(int netHandle, Vector3 aim, bool shooting, Client exception)
        {
            byte[] full = new byte[0];

            full = PacketOptimization.WriteBulletSync(netHandle, shooting, aim);

            foreach (var client in exception.Streamer.GetNearClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;
                if (client.Position.DistanceToSquared(exception.Position) > GlobalStreamingRange * GlobalStreamingRange) continue; // 1km

                NetOutgoingMessage msg = Server.CreateMessage();
                msg.Write((byte)PacketType.BulletSync);
                msg.Write(full.Length);
                msg.Write(full);
                Server.SendMessage(msg, client.NetConnection,
                    NetDeliveryMethod.ReliableSequenced,
                    (int)ConnectionChannel.BulletSync);
            }
        }

        internal void ResendBulletPacket(int netHandle, int netHandleTarget, bool shooting, Client exception)
        {
            byte[] full = new byte[0];

            full = PacketOptimization.WriteBulletSync(netHandle, shooting, netHandleTarget);

            foreach (var client in exception.Streamer.GetNearClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;
                if (client.Position.DistanceToSquared(exception.Position) > GlobalStreamingRange * GlobalStreamingRange) continue; // 1km

                NetOutgoingMessage msg = Server.CreateMessage();
                msg.Write((byte)PacketType.BulletPlayerSync);
                msg.Write(full.Length);
                msg.Write(full);
                Server.SendMessage(msg, client.NetConnection,
                    NetDeliveryMethod.ReliableSequenced,
                    (int)ConnectionChannel.BulletSync);
            }
        }

        internal void ResendPacket(VehicleData fullPacket, Client exception, bool pure)
        {
            byte[] full = new byte[0];
            byte[] basic = new byte[0];

            if (pure)
            {
                full = PacketOptimization.WritePureSync(fullPacket);
                if (PacketOptimization.CheckBit(fullPacket.Flag.Value, VehicleDataFlags.Driver))
                {
                    basic = PacketOptimization.WriteBasicSync(fullPacket.NetHandle.Value, fullPacket.Position);
                }
                else if (!exception.CurrentVehicle.IsNull)
                {
                    var carPos = NetEntityHandler.ToDict()[exception.CurrentVehicle.Value].Position;
                    basic = PacketOptimization.WriteBasicSync(fullPacket.NetHandle.Value, carPos);
                }
            }
            else
            {
                full = PacketOptimization.WriteLightSync(fullPacket);
            }

            foreach (var client in exception.Streamer.GetNearClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;

                NetOutgoingMessage msg = Server.CreateMessage();
                if (pure)
                {
                    if (client.Position == null) continue;
                    if (client.Position.DistanceToSquared(fullPacket.Position) > GlobalStreamingRange * GlobalStreamingRange) // 1 km
                    {
                        var lastUpdateReceived = client.LastPacketReceived.Get(exception.handle.Value);

                        if (lastUpdateReceived == 0 || Program.GetTicks() - lastUpdateReceived > 1000)
                        {
                            msg.Write((byte) PacketType.BasicSync);
                            msg.Write(basic.Length);
                            msg.Write(basic);
                            Server.SendMessage(msg, client.NetConnection,
                                NetDeliveryMethod.UnreliableSequenced,
                                (int) ConnectionChannel.BasicSync);

                            client.LastPacketReceived.Set(exception.handle.Value, Program.GetTicks());
                        }
                    }
                    else
                    {
                        msg.Write((byte)PacketType.VehiclePureSync);
                        msg.Write(full.Length);
                        msg.Write(full);
                        Server.SendMessage(msg, client.NetConnection,
                            NetDeliveryMethod.UnreliableSequenced,
                            (int)ConnectionChannel.PureSync);
                    }
                }
                else
                {
                    msg.Write((byte)PacketType.VehicleLightSync);
                    msg.Write(full.Length);
                    msg.Write(full);
                    Server.SendMessage(msg, client.NetConnection,
                        NetDeliveryMethod.ReliableSequenced,
                        (int)ConnectionChannel.LightSync);
                }
            }

            foreach (var client in exception.Streamer.GetFarClients())
            {
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;

                NetOutgoingMessage msg = Server.CreateMessage();
                if (pure)
                {
                    var lastUpdateReceived = client.LastPacketReceived.Get(exception.handle.Value);

                    if (lastUpdateReceived == 0 || Program.GetTicks() - lastUpdateReceived > 1000)
                    {
                        msg.Write((byte)PacketType.BasicSync);
                        msg.Write(basic.Length);
                        msg.Write(basic);
                        Server.SendMessage(msg, client.NetConnection,
                            NetDeliveryMethod.UnreliableSequenced,
                            (int)ConnectionChannel.BasicSync);

                        client.LastPacketReceived.Set(exception.handle.Value, Program.GetTicks());
                    }
                }
            }
        }

        internal void ResendUnoccupiedPacket(VehicleData fullPacket, Client exception)
        {
            if (fullPacket.NetHandle == null) return;

            var vehicleEntity = new NetHandle(fullPacket.NetHandle.Value);
            var full = PacketOptimization.WriteUnOccupiedVehicleSync(fullPacket);
            var basic = PacketOptimization.WriteBasicUnOccupiedVehicleSync(fullPacket);

            var msgNear = Server.CreateMessage();
            msgNear.Write((byte)PacketType.UnoccupiedVehSync);
            msgNear.Write(full.Length);
            msgNear.Write(full);

            var msgFar = Server.CreateMessage();
            msgFar.Write((byte)PacketType.BasicUnoccupiedVehSync);
            msgFar.Write(basic.Length);
            msgFar.Write(basic);

            List<NetConnection> connectionsNear = new List<NetConnection>();
            List<NetConnection> connectionsFar = new List<NetConnection>();

            foreach (var client in exception.Streamer.GetNearClients())
            {
                // skip sending a sync packet for a trailer to it's owner.
                if (CheckUnoccupiedTrailerDriver(client, vehicleEntity)) continue;
                if (client.NetConnection.Status == NetConnectionStatus.Disconnected) continue;
                if (client.NetConnection.RemoteUniqueIdentifier == exception.NetConnection.RemoteUniqueIdentifier) continue;

                if (client.Position == null) continue;
                if (client.Position.DistanceToSquared(fullPacket.Position) < 20000)
                {
                    connectionsNear.Add(client.NetConnection);
                }
                else
                {
                    connectionsFar.Add(client.NetConnection);
                }
            }

            Server.SendMessage(msgNear, connectionsNear,
                NetDeliveryMethod.UnreliableSequenced,
                (int)ConnectionChannel.UnoccupiedVeh);

            foreach (var client in exception.Streamer.GetFarClients())
            {
                connectionsFar.Add(client.NetConnection);
            }

            Server.SendMessage(msgFar, connectionsFar,
                NetDeliveryMethod.UnreliableSequenced,
                (int)ConnectionChannel.UnoccupiedVeh);
        }

        internal bool CheckUnoccupiedTrailerDriver(Client player, NetHandle vehicle)
        {
            if (vehicle.IsNull)
            {
                Program.Output("NULL CATCH");
                return false;
            }


            NetHandle traileredBy = Program.ServerInstance.PublicAPI.getVehicleTraileredBy(vehicle);

            if (traileredBy != null)
            {
                Program.Output("TRAILERED");
                return Program.ServerInstance.PublicAPI.getVehicleDriver(traileredBy) == player;
            }

            Program.Output("NOT TRAILERED");
            return false;
        }

        private void LogException(Exception ex, string resourceName)
        {
            Program.Output("RESOURCE EXCEPTION FROM " + resourceName + ": " + ex.Message);
            Program.Output(ex.StackTrace);
        }

        public void ProcessMessages()
        {
            List<NetIncomingMessage> messages = new List<NetIncomingMessage>();
            int msgsRead = Server.ReadMessages(messages);
            if (msgsRead > 0)
                foreach (var msg in messages)
                {
                    Client client = null;
                    lock (Clients)
                    {
                        foreach (Client c in Clients)
                        {
                            if (c != null && c.NetConnection != null &&
                                c.NetConnection.RemoteUniqueIdentifier != 0 &&
                                msg.SenderConnection != null &&
                                c.NetConnection.RemoteUniqueIdentifier == msg.SenderConnection.RemoteUniqueIdentifier)
                            {
                                client = c;
                                break;
                            }
                        }
                    }
                    if (client == null) client = new Client(msg.SenderConnection);
                    PacketType packetType = PacketType.ConnectionPacket;

                    try
                    {
                        switch (msg.MessageType)
                        {
                            case NetIncomingMessageType.UnconnectedData:
                                try
                                {
                                    var isPing = msg.ReadString();
                                    if (isPing == "ping")
                                    {
                                        //if (LogLevel > 0) Program.Output("INFO: Received a ping, responding..");

                                        var pong = Server.CreateMessage();
                                        pong.Write("pong");
                                        //Server.SendUnconnectedMessage(pong, msg.SenderEndPoint.Address.ToString(), msg.SenderEndPoint.Port);
                                        Server.SendMessage(pong, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
                                    }
                                    if (isPing == "query")
                                    {
                                        //Program.Output("INFO: query received from " + msg.SenderEndPoint.Address.ToString());
                                        var pong = Server.CreateMessage();
                                        pong.Write(Name + "%" + PasswordProtected + "%" + Clients.Count + "%" + MaxPlayers + "%" + GamemodeName);
                                        Server.SendMessage(pong, client.NetConnection, NetDeliveryMethod.ReliableOrdered);
                                    }
                                }
                                catch (Exception) { }
                                break;
                            case NetIncomingMessageType.DiscoveryResponse:
                                break;
                            case NetIncomingMessageType.VerboseDebugMessage:
                                if (LogLevel > 2)
                                    Program.Output("[VERBOSE] " + msg.ReadString());
                                break;
                            case NetIncomingMessageType.DebugMessage:
                                if (LogLevel > 1)
                                    Program.Output("[DEBUG] " + msg.ReadString());
                                break;
                            case NetIncomingMessageType.WarningMessage:
                                Program.ToFile("attack.log", msg.ReadString());
                                break;
                            case NetIncomingMessageType.ErrorMessage:
                                if (LogLevel > 0)
                                    Program.Output("[ERROR] " + msg.ReadString());
                                break;
                            case NetIncomingMessageType.ConnectionLatencyUpdated:
                                client.Latency = msg.ReadFloat();
                                break;

                            case NetIncomingMessageType.ConnectionApproval:
                        {
                                    /*if (Conntimeout)
                                    {
                                        lock (queue)
                                        {
                                            if (queue.ContainsKey(client.NetConnection.RemoteEndPoint))
                                            {
                                                client.NetConnection.Deny("Wait atleast 60 seconds before reconnecting..");
                                                continue;
                                            }
                                            else
                                            {
                                                queue.Add(client.NetConnection.RemoteEndPoint, DateTime.Now);
                                            }
                                        }
                                    }*/
                                    Program.Output("Initiating connection: [" + client.NetConnection.RemoteEndPoint.Address + ":" + client.NetConnection.RemoteEndPoint.Port + "]");
                                    msg.ReadByte();
                                    var leng = msg.ReadInt32();
                                    ConnectionRequest connReq;
                                    try
                                    {
                                        connReq = DeserializeBinary<ConnectionRequest>(msg.ReadBytes(leng)) as ConnectionRequest;
                                    }
                                    catch (EndOfStreamException)
                                    {
                                        continue;
                                    }

                                    if (string.IsNullOrWhiteSpace(connReq?.DisplayName) ||
                                        string.IsNullOrWhiteSpace(connReq.SocialClubName) ||
                                        string.IsNullOrWhiteSpace(connReq.ScriptVersion))
                                    {
                                        client.NetConnection.Deny("Outdated version!\nPlease update your client.");
                                        continue;
                                    }

                                    var cVersion = ParseableVersion.Parse(connReq.ScriptVersion);
                                    if (cVersion < MinimumClientVersion ||
                                        cVersion < VersionCompatibility.LastCompatibleClientVersion)
                                    {
                                        client.NetConnection.Deny("Outdated version!\nPlease update your client.");
                                        continue;
                                    }

                                    if (BanManager.IsClientBanned(client))
                                    {
                                        client.NetConnection.Deny("You are banned from the server.");
                                        continue;
                                    }

                                    if (PasswordProtected && !string.IsNullOrWhiteSpace(Password))
                                    {
                                        if (Password != connReq.Password)
                                        {
                                            client.NetConnection.Deny("Wrong password!");
                                            Program.Output("Player connection refused: wrong password. (" + client.NetConnection.RemoteEndPoint.Address + ")");
                                            continue;
                                        }
                                    }

                                    lock (Clients)
                                    {
                                        var duplicate = 0;
                                        var displayname = connReq.DisplayName;

                                        while (AllowDisplayNames && Clients.Any(c => c.Name == connReq.DisplayName))
                                        {
                                            duplicate++;
                                            connReq.DisplayName = displayname + " (" + duplicate + ")";
                                        }
                                    }

                                    client.CommitConnection(); //Create PedHandle
                                    client.SocialClubName = connReq.SocialClubName;
                                    client.CEF = connReq.CEF;
                                    client.MediaStream = connReq.MediaStream;
                                    client.Name = AllowDisplayNames ? connReq.DisplayName : connReq.SocialClubName;
                                    client.RemoteScriptVersion = ParseableVersion.Parse(connReq.ScriptVersion);
                                    client.GameVersion = connReq.GameVersion;
                                    client.ConnectionConfirmed = false;
                                    ((PlayerProperties)NetEntityHandler.ToDict()[client.handle.Value]).Name = client.Name;

                                    if (!AllowCEFDevTool && connReq.CEFDevtool)
                                    {
                                        client.NetConnection.Deny("CEF DevTool is not allowed.");
                                        Program.Output("Player connection refused: CEF Devtool enabled. (" + client.NetConnection.RemoteEndPoint.Address + ")");
                                        continue;
                                    }

                                    var respObj = new ConnectionResponse
                                    {
                                        ServerVersion = serverVersion.ToString(),
                                        CharacterHandle = client.handle.Value,
                                        Settings = new SharedSettings
                                        {
                                            ModWhitelist = ModWhitelist,
                                            UseHttpServer = UseHTTPFileServer,
                                        }
                                    };

                                    var channelHail = Server.CreateMessage();
                                    var respBin = SerializeBinary(respObj);

                                    channelHail.Write(respBin.Length);
                                    channelHail.Write(respBin);

                                    var cancelArgs = new CancelEventArgs();

                                    lock (RunningResources)
                                    {
                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en => en.InvokePlayerBeginConnect(client, cancelArgs)));
                                    }

                                    Clients.Add(client);
                                    Server.Configuration.CurrentPlayers = Clients.Count;
                                    client.NetConnection.Approve(channelHail);

                                    Program.Output("Processing connection: " + client.SocialClubName + " (" + client.Name + ") [" + client.NetConnection.RemoteEndPoint.Address + "]");
                                }
                                break;

                            case NetIncomingMessageType.StatusChanged:
                                {

                                    var newStatus = (NetConnectionStatus)msg.ReadByte();

                                    switch (newStatus)
                                    {
                                        case NetConnectionStatus.Connected:

                                            break;

                                        case NetConnectionStatus.Disconnected:
                                            {
                                                var reason = msg.ReadString();
                                                if (Clients.Contains(client))
                                                {
                                                    lock (RunningResources)
                                                    {
                                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerDisconnected(client, reason);
                                                        }));
                                                    }

                                                    UnoccupiedVehicleManager.UnsyncAllFrom(client);

                                                    lock (Clients)
                                                    {
                                                        var dcObj = new PlayerDisconnect() { Id = client.handle.Value };

                                                        SendToAll(dcObj, PacketType.PlayerDisconnect, true, ConnectionChannel.SyncEvent);

                                                        Program.Output("Player disconnected: " + client.SocialClubName + " (" +
                                                                       client.Name + ") [" +
                                                                       client.NetConnection.RemoteEndPoint.Address + "], reason: " +
                                                                       reason);

                                                        int vehValue = client.CurrentVehicle.Value;

                                                        if (vehValue != 0 &&
                                                            VehicleOccupants.ContainsKey(vehValue) &&
                                                            VehicleOccupants[vehValue].Contains(client))
                                                            VehicleOccupants[vehValue].Remove(client);

                                                        Clients.Remove(client);
                                                        Server.Configuration.CurrentPlayers = Clients.Count;
                                                        NetEntityHandler.DeleteEntityQuiet(client.handle.Value);
                                                        if (ACLEnabled) ACL.LogOutClient(client);

                                                        Downloads.RemoveAll(d => d.Parent == client);
                                                    }
                                                }
                                                break;
                                            }
                                    }
                                }
                                break;

                            case NetIncomingMessageType.DiscoveryRequest:
                                {
                                    //Program.Output("Discovery Request from: [" + msg.SenderEndPoint.Address.ToString() + ":" + msg.SenderEndPoint.Port + "] " + msg.SenderConnection.RemoteUniqueIdentifier);
                                    var response = Server.CreateMessage();
                                    var obj = new DiscoveryResponse
                                    {
                                        ServerName = Name,
                                        MaxPlayers = (short)MaxPlayers,
                                        PasswordProtected = PasswordProtected,
                                        Gamemode = string.IsNullOrEmpty(GamemodeName)
                                            ? Gamemode?
                                                  .DirectoryName ?? "GTA Network"
                                            : GamemodeName,
                                        PlayerCount = (short)
                                            Clients.Count,
                                        Port = Port,
                                        LAN = isIPLocal(msg.SenderEndPoint.Address.ToString())
                                    };

                                    if (obj.LAN && AnnounceToLAN || !obj.LAN)
                                    {
                                        var bin = SerializeBinary(obj);

                                        response.Write((byte)PacketType.DiscoveryResponse);
                                        response.Write(bin.Length);
                                        response.Write(bin);

                                        Server.SendDiscoveryResponse(response, msg.SenderEndPoint);
                                    }
                                }
                                break;

                            case NetIncomingMessageType.Data:

                                packetType = (PacketType)msg.ReadByte();
                                //Console.WriteLine("Called... " + packetType);
                                switch (packetType)
                                {
                                    case PacketType.ConnectionConfirmed:
                                        {
                                            var state = msg.ReadBoolean();
                                            if (!state)
                                            {
                                                var delta = new Delta_PlayerProperties { Name = client.Name };
                                                UpdateEntityInfo(client.handle.Value, EntityType.Player, delta, client);

                                                var mapObj = new ServerMap
                                                {
                                                    World =
                                                        Program.ServerInstance.NetEntityHandler.NetToProp<WorldProperties>(1)
                                                };

                                                foreach (var pair in NetEntityHandler.ToCopy())
                                                {
                                                    if (pair.Value.EntityType == (byte)EntityType.Vehicle)
                                                    {
                                                        mapObj.Vehicles.Add(pair.Key, (VehicleProperties)pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte)EntityType.Prop)
                                                    {
                                                        mapObj.Objects.Add(pair.Key, pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte)EntityType.Blip)
                                                    {
                                                        mapObj.Blips.Add(pair.Key, (BlipProperties)pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte)EntityType.Marker)
                                                    {
                                                        mapObj.Markers.Add(pair.Key, (MarkerProperties)pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte)EntityType.Pickup)
                                                    {
                                                        if (!((PickupProperties)pair.Value).PickedUp)
                                                            mapObj.Pickups.Add(pair.Key, (PickupProperties)pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte)EntityType.Player)
                                                    {
                                                        mapObj.Players.Add(pair.Key, (PlayerProperties)pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte)EntityType.TextLabel)
                                                    {
                                                        mapObj.TextLabels.Add(pair.Key, (TextLabelProperties)pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte)EntityType.Ped)
                                                    {
                                                        mapObj.Peds.Add(pair.Key, (PedProperties)pair.Value);
                                                    }
                                                    else if (pair.Value.EntityType == (byte)EntityType.Particle)
                                                    {
                                                        mapObj.Particles.Add(pair.Key, (ParticleProperties)pair.Value);
                                                    }
                                                }

                                                // TODO: replace this filth
                                                var r = new Random();

                                                var mapData = new StreamedData
                                                {
                                                    Id = r.Next(int.MaxValue),
                                                    Data = SerializeBinary(mapObj),
                                                    Type = FileType.Map
                                                };

                                                var downloader = new StreamingClient(client);
                                                downloader.Files.Add(mapData);

                                                if (!UseHTTPFileServer)
                                                {
                                                    lock (RunningResources)
                                                    {
                                                        foreach (var resource in RunningResources)
                                                        {
                                                            foreach (var file in resource.Info.Files)
                                                            {
                                                                var fileData = new StreamedData
                                                                {
                                                                    Id = r.Next(int.MaxValue),
                                                                    Type = FileType.Normal,
                                                                    Data = File.ReadAllBytes("resources" +
                                                                                             Path.DirectorySeparatorChar +
                                                                                             resource.DirectoryName +
                                                                                             Path.DirectorySeparatorChar +
                                                                                             file.Path),
                                                                    Name = file.Path,
                                                                    Resource = resource.DirectoryName,
                                                                    Hash = FileHashes.ContainsKey(resource.DirectoryName + "_" +
                                                                                                  file.Path)
                                                                        ? FileHashes[
                                                                            resource.DirectoryName + "_" + file.Path]
                                                                        : null
                                                                };
                                                                downloader.Files.Add(fileData);
                                                            }
                                                        }
                                                    }
                                                }

                                                foreach (var script in GetAllClientsideScripts())
                                                {
                                                    var scriptData = new StreamedData
                                                    {
                                                        Id = r.Next(int.MaxValue),
                                                        Data = Encoding.UTF8.GetBytes(script.Script),
                                                        Type = FileType.Script,
                                                        Resource = script.ResourceParent,
                                                        Hash = script.MD5Hash,
                                                        Name = script.Filename
                                                    };
                                                    downloader.Files.Add(scriptData);
                                                }

                                                var endStream = new StreamedData
                                                {
                                                    Id = r.Next(int.MaxValue),
                                                    Data = new byte[] { 0xDE, 0xAD, 0xF0, 0x0D },
                                                    Type = FileType.EndOfTransfer
                                                };
                                                downloader.Files.Add(endStream);


                                                Downloads.Add(downloader);

                                                lock (RunningResources)
                                                    RunningResources.ForEach(
                                                        fs => fs.Engines.ForEach(en => { en.InvokePlayerConnected(client); }));

                                                client.ConnectionConfirmed = true;
                                                Program.Output("Connection established: " + client.SocialClubName + " (" +
                                                               client.Name + ") [" +
                                                               client.NetConnection.RemoteEndPoint.Address + "]");
                                            }
                                            else
                                            {
                                                var length = msg.ReadInt32();
                                                string[] resources = new string[length];

                                                for (int i = 0; i < length; i++)
                                                {
                                                    resources[i] = msg.ReadString();
                                                }

                                                lock (RunningResources)
                                                {
                                                    RunningResources.ForEach(fs =>
                                                    {
                                                        if (Array.IndexOf(resources, fs.DirectoryName) == -1) return;
                                                        fs.Engines.ForEach(en => { en.InvokePlayerDownloadFinished(client); });
                                                    });
                                                }
                                                StressTest.HasPlayers = true;
                                            }
                                        }
                                        break;

                                    case PacketType.ChatData:
                                        {
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var data = DeserializeBinary<ChatData>(msg.ReadBytes(len)) as ChatData;
                                                if (data != null)
                                                {
                                                    var pass = true;
                                                    var command = data.Message.StartsWith("/");

                                                    if (command)
                                                    {
                                                        if (ACLEnabled)
                                                        {
                                                            pass =
                                                                ACL.DoesUserHaveAccessToCommand(client,
                                                                    data.Message.Split()[0].TrimStart('/'));
                                                        }

                                                        if (pass)
                                                        {
                                                            ThreadPool.QueueUserWorkItem(delegate
                                                            {
                                                                var cancelArg = new CancelEventArgs();

                                                                lock (RunningResources)
                                                                {
                                                                    RunningResources.ForEach(
                                                                        fs =>
                                                                            fs.Engines.ForEach(
                                                                                en =>
                                                                                    en.InvokeChatCommand(client,
                                                                                        data.Message, cancelArg)));
                                                                }

                                                                if (!cancelArg.Cancel)
                                                                {
                                                                    if (!CommandHandler.Parse(client, data.Message))
                                                                        PublicAPI.sendChatMessageToPlayer(client, ErrorCmd);
                                                                }
                                                            });
                                                        }
                                                        else
                                                        {
                                                            var chatObj = new ChatData()
                                                            {
                                                                Sender = "",
                                                                Message = "You don't have access to this command!",
                                                            };

                                                            var binData = SerializeBinary(chatObj);

                                                            var respMsg = Program.ServerInstance.Server.CreateMessage();
                                                            respMsg.Write((byte)PacketType.ChatData);
                                                            respMsg.Write(binData.Length);
                                                            respMsg.Write(binData);
                                                            client.NetConnection.SendMessage(respMsg,
                                                                NetDeliveryMethod.ReliableOrdered, 0);
                                                        }

                                                        continue;
                                                    }

                                                    ThreadPool.QueueUserWorkItem(delegate
                                                    {
                                                        lock (RunningResources)
                                                            RunningResources.ForEach(
                                                                fs =>
                                                                    fs.Engines.ForEach(
                                                                        en =>
                                                                            pass =
                                                                                pass &&
                                                                                en.InvokeChatMessage(client,
                                                                                    data.Message)));

                                                        if (pass)
                                                        {
                                                            data.Id = client.NetConnection.RemoteUniqueIdentifier;
                                                            data.Sender = client.Name;
                                                            SendToAll(data, PacketType.ChatData, true,
                                                                ConnectionChannel.Chat);
                                                            Program.Output(data.Sender + ": " + data.Message);
                                                        }
                                                    });
                                                }
                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                            }
                                        }
                                        break;
                                    case PacketType.VehiclePureSync:
                                        {
                                            if (!client.ConnectionConfirmed) continue;
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var bin = msg.ReadBytes(len);

                                                var fullPacket = PacketOptimization.ReadPureVehicleSync(bin);

                                                fullPacket.NetHandle = client.handle.Value;

                                                if (fullPacket.PlayerHealth.Value != client.Health)
                                                {
                                                    lock (RunningResources)
                                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerHealthChange(client, client.Health);
                                                        }));
                                                }

                                                if (fullPacket.PedArmor.Value != client.Armor)
                                                {
                                                    lock (RunningResources)
                                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerArmorChange(client, client.Armor);
                                                        }));
                                                }

                                                if (fullPacket.WeaponHash != null &&
                                                    fullPacket.WeaponHash.Value != (int)client.CurrentWeapon)
                                                {
                                                    lock (RunningResources)
                                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerWeaponChange(client, (int)client.CurrentWeapon);
                                                        }));
                                                }

                                                client.Health = fullPacket.PlayerHealth.Value;
                                                client.Armor = fullPacket.PedArmor.Value;
                                                client.LastVehicleFlag = fullPacket.Flag.Value;
                                                client.LastUpdate = DateTime.Now;

                                                if (fullPacket.WeaponHash != null)
                                                    client.CurrentWeapon = (WeaponHash)fullPacket.WeaponHash.Value;

                                                if (PacketOptimization.CheckBit(fullPacket.Flag.Value,
                                                        VehicleDataFlags.HasAimData) && fullPacket.AimCoords != null)
                                                {
                                                    client.LastAimPos = fullPacket.AimCoords;
                                                }
                                                else
                                                {
                                                    client.LastAimPos = null;
                                                }

                                                if (PacketOptimization.CheckBit(fullPacket.Flag.Value,
                                                    VehicleDataFlags.Driver))
                                                {
                                                    client.Position = fullPacket.Position;
                                                    client.Rotation = fullPacket.Quaternion;
                                                    client.Velocity = fullPacket.Velocity;

                                                    var handlerDict = NetEntityHandler.ToDict();
                                                    int vehValue = client.CurrentVehicle.Value;

                                                    if (!client.CurrentVehicle.IsNull &&
                                                        handlerDict
                                                            .ContainsKey(vehValue))
                                                    {
                                                        var props = handlerDict[vehValue];

                                                        props.Position
                                                            = fullPacket.Position;
                                                        props.Rotation
                                                            = fullPacket.Quaternion;
                                                        props.Velocity
                                                            = fullPacket.Velocity;
                                                        if (fullPacket.Flag.HasValue)
                                                        {
                                                            var newDead = (fullPacket.Flag &
                                                                           (byte)VehicleDataFlags.VehicleDead) > 0;
                                                            if (!((VehicleProperties)
                                                                        props)
                                                                    .IsDead && newDead)
                                                            {
                                                                lock (RunningResources)
                                                                    RunningResources.ForEach(
                                                                        fs => fs.Engines.ForEach(en =>
                                                                        {
                                                                            en.InvokeVehicleDeath(client.CurrentVehicle);
                                                                        }));
                                                            }

                                                            ((VehicleProperties)
                                                                    props)
                                                                .IsDead = newDead;
                                                        }

                                                        if (fullPacket.VehicleHealth.HasValue)
                                                        {
                                                            if (fullPacket.VehicleHealth.Value != ((VehicleProperties)props).Health)
                                                            {
                                                                lock (RunningResources)
                                                                    RunningResources.ForEach(
                                                                        fs => fs.Engines.ForEach(
                                                                            en =>
                                                                            {
                                                                                en.InvokeVehicleHealthChange(client, ((VehicleProperties)props).Health);
                                                                            }
                                                                            )
                                                                        )
                                                                    ;
                                                            }

                                                            ((VehicleProperties)props).Health = fullPacket.VehicleHealth.Value;
                                                        }

                                                        if (fullPacket.Flag.HasValue)
                                                        {
                                                            if ((fullPacket.Flag & (byte)VehicleDataFlags.SirenActive) != 0 ^ ((VehicleProperties)props).Siren)
                                                            {
                                                                lock (RunningResources)
                                                                    RunningResources.ForEach(
                                                                        fs => fs.Engines.ForEach(
                                                                            en =>
                                                                            {
                                                                                en.InvokeVehicleSirenToggle(client, ((VehicleProperties)NetEntityHandler.ToDict()[client.CurrentVehicle.Value]).Siren);
                                                                            }
                                                                            )
                                                                        )
                                                                    ;
                                                            }
                                                            ((VehicleProperties)props).Siren = (fullPacket.Flag & (byte)VehicleDataFlags.SirenActive) > 0;
                                                        }
                                                    }

                                                    int netHandleValue = fullPacket.NetHandle.Value;

                                                    if (handlerDict.ContainsKey(netHandleValue))
                                                    {
                                                        var props = handlerDict[netHandleValue];

                                                        props.Position = fullPacket.Position;
                                                        props.Rotation = fullPacket.Quaternion;
                                                        props.Velocity = fullPacket.Velocity;
                                                    }
                                                }
                                                else if (!client.CurrentVehicle.IsNull && NetEntityHandler.ToDict().ContainsKey(client.CurrentVehicle.Value))
                                                {
                                                    var props = NetEntityHandler.ToDict()[client.CurrentVehicle.Value];

                                                    var carPos = props.Position;
                                                    var carRot = props.Rotation;
                                                    var carVel = props.Velocity;

                                                    client.Position = carPos;
                                                    client.Rotation = carRot;
                                                    client.Velocity = carVel;

                                                    if (NetEntityHandler.ToDict().ContainsKey(fullPacket.NetHandle.Value))
                                                    {
                                                        var playerProps = NetEntityHandler.ToDict()[fullPacket.NetHandle.Value];

                                                        playerProps.Position = carPos;
                                                        playerProps.Rotation = carRot;
                                                        playerProps.Velocity = carVel;
                                                    }
                                                }
                                                client.IsInVehicle = true;

                                                ResendPacket(fullPacket, client, true);

                                                UpdateAttachables(client.handle.Value);
                                                UpdateAttachables(client.CurrentVehicle.Value);
                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                            }
                                        }
                                        break;
                                    case PacketType.VehicleLightSync:
                                        {
                                            if (!client.ConnectionConfirmed) continue;
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var bin = msg.ReadBytes(len);

                                                var fullPacket = PacketOptimization.ReadLightVehicleSync(bin);

                                                fullPacket.NetHandle = client.handle.Value;
                                                fullPacket.Latency = client.Latency;

                                                client.IsInVehicle = true;
                                                client.VehicleSeat = fullPacket.VehicleSeat.Value;

                                                var car = new NetHandle(fullPacket.VehicleHandle.Value);

                                                if (!client.IsInVehicleInternal || client.VehicleHandleInternal != car.Value)
                                                {
                                                    if (!VehicleOccupants.ContainsKey(car.Value))
                                                    {
                                                        VehicleOccupants.Add(car.Value, new List<Client>());
                                                    }

                                                    if (!VehicleOccupants[car.Value].Contains(client))
                                                        VehicleOccupants[car.Value].Add(client);

                                                    lock (RunningResources)
                                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerEnterVehicle(client, car);
                                                        }));
                                                }

                                                client.IsInVehicleInternal = true;
                                                client.VehicleHandleInternal = car.Value;
                                                client.CurrentVehicle = car;


                                                if (NetEntityHandler.ToDict().ContainsKey(fullPacket.NetHandle.Value))
                                                {
                                                    NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].ModelHash =
                                                        fullPacket.PedModelHash.Value;
                                                }

                                                if (fullPacket.Trailer != null)
                                                {
                                                    var trailer =
                                                        ((VehicleProperties)
                                                            NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                        .Trailer;
                                                    if (NetEntityHandler.ToDict().ContainsKey(trailer))
                                                    {
                                                        NetEntityHandler.ToDict()[trailer].Position = fullPacket.Trailer;
                                                    }
                                                }


                                                if (fullPacket.DamageModel != null)
                                                {
                                                    if (((VehicleProperties)
                                                            NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                        .DamageModel ==
                                                        null)
                                                    {
                                                        ((VehicleProperties)
                                                                NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                            .DamageModel = new VehicleDamageModel();
                                                    }

                                                    var oldDoors = ((VehicleProperties)
                                                            NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                        .DamageModel.BrokenDoors;

                                                    var oldWindows = ((VehicleProperties)
                                                            NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                        .DamageModel.BrokenWindows;

                                                    ((VehicleProperties)
                                                            NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                        .DamageModel = fullPacket.DamageModel;

                                                    if ((oldDoors ^ fullPacket.DamageModel.BrokenDoors) != 0)
                                                    {
                                                        lock (RunningResources)
                                                        {
                                                            for (int k = 0; k < 8; k++)
                                                            {
                                                                if (((oldDoors ^ fullPacket.DamageModel.BrokenDoors) &
                                                                     1 << k) == 0) continue;
                                                                var localCopy = fullPacket.VehicleHandle.Value;
                                                                RunningResources.ForEach(
                                                                    fs => fs.Engines.ForEach(en =>
                                                                    {
                                                                        en.InvokeVehicleDoorBreak(
                                                                            new NetHandle(localCopy),
                                                                            k);
                                                                    }));
                                                            }
                                                        }
                                                    }


                                                    if ((oldWindows ^ fullPacket.DamageModel.BrokenWindows) != 0)
                                                    {
                                                        lock (RunningResources)
                                                        {
                                                            for (int k = 0; k < 8; k++)
                                                            {
                                                                if (((oldDoors ^ fullPacket.DamageModel.BrokenWindows) &
                                                                     1 << k) == 0) continue;
                                                                var localCopy = fullPacket.VehicleHandle.Value;

                                                                RunningResources.ForEach(
                                                                    fs => fs.Engines.ForEach(en =>
                                                                    {
                                                                        en.InvokeVehicleWindowBreak(
                                                                            new NetHandle(localCopy),
                                                                            k);
                                                                    }));
                                                            }
                                                        }
                                                    }
                                                }

                                                ResendPacket(fullPacket, client, false);
                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                            }
                                            catch (KeyNotFoundException)
                                            {
                                            } //Proper fix is needed but this isn't very problematic
                                        }
                                        break;
                                    case PacketType.PedPureSync:
                                        {
                                            if (!client.ConnectionConfirmed) continue;
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var bin = msg.ReadBytes(len);

                                                var fullPacket = PacketOptimization.ReadPurePedSync(bin);

                                                fullPacket.NetHandle = client.handle.Value;

                                                var oldHealth = client.Health;
                                                var oldArmor = client.Armor;
                                                var oldWeap = client.CurrentWeapon;
                                                var oldAmmo = client.Ammo;

                                                client.Health = fullPacket.PlayerHealth.Value;
                                                client.Armor = fullPacket.PedArmor.Value;
                                                client.Position = fullPacket.Position;
                                                client.LastUpdate = DateTime.Now;
                                                client.Rotation = fullPacket.Quaternion;
                                                client.Velocity = fullPacket.Velocity;
                                                client.CurrentWeapon = (WeaponHash)fullPacket.WeaponHash.Value;
                                                client.Ammo = fullPacket.WeaponAmmo.Value;
                                                client.Weapons[client.CurrentWeapon] = client.Ammo;
                                                if (fullPacket.Flag != null) client.LastPedFlag = fullPacket.Flag.Value;

                                                if (fullPacket.PlayerHealth.Value != oldHealth)
                                                {
                                                    lock (RunningResources)
                                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerHealthChange(client, oldHealth);
                                                        }));
                                                }

                                                if (fullPacket.PedArmor.Value != oldArmor)
                                                {
                                                    lock (RunningResources)
                                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerArmorChange(client, oldArmor);
                                                        }));
                                                }

                                                if (fullPacket.WeaponHash.Value != (int)oldWeap)
                                                {
                                                    lock (RunningResources)
                                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerWeaponChange(client, (int)oldWeap);
                                                        }));
                                                }

                                                if (fullPacket.WeaponAmmo.Value != oldAmmo &&
                                                    fullPacket.WeaponHash.Value == (int)oldWeap)
                                                {
                                                    lock (RunningResources)
                                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerWeaponAmmoChange(client, (int)oldWeap, oldAmmo);
                                                        }));
                                                }

                                                if (client.IsInVehicleInternal && !client.CurrentVehicle.IsNull)
                                                {
                                                    if (client.CurrentVehicle.Value != 0 &&
                                                        VehicleOccupants.ContainsKey(client.CurrentVehicle.Value) &&
                                                        VehicleOccupants[client.CurrentVehicle.Value].Contains(client))
                                                        VehicleOccupants[client.CurrentVehicle.Value].Remove(client);


                                                    lock (RunningResources)
                                                        RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                        {
                                                            en.InvokePlayerExitVehicle(client, client.CurrentVehicle);
                                                        }));
                                                }

                                                client.IsInVehicleInternal = false;
                                                client.IsInVehicle = false;
                                                client.CurrentVehicle = new NetHandle(0);
                                                client.VehicleHandleInternal = 0;

                                                if (NetEntityHandler.ToDict().ContainsKey(fullPacket.NetHandle.Value))
                                                {
                                                    NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].Position =
                                                        fullPacket.Position;
                                                    NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].Rotation =
                                                        fullPacket.Quaternion;
                                                }

                                                ResendPacket(fullPacket, client, true);
                                                UpdateAttachables(client.handle.Value);
                                                //SendToAll(data, PacketType.PedPositionData, false, client, ConnectionChannel.PositionData);
                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                            }
                                        }
                                        break;
                                    case PacketType.PedLightSync:
                                        {
                                            if (!client.ConnectionConfirmed) continue;
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var bin = msg.ReadBytes(len);

                                                var fullPacket = PacketOptimization.ReadLightPedSync(bin);

                                                fullPacket.NetHandle = client.handle.Value;
                                                fullPacket.Latency = client.Latency;

                                                if (NetEntityHandler.ToDict().ContainsKey(fullPacket.NetHandle.Value))
                                                {
                                                    if (client.ModelHash == 0) client.ModelHash = fullPacket.PedModelHash.Value;

                                                    var oldValue = client.ModelHash;
                                                    if (oldValue != fullPacket.PedModelHash.Value)
                                                    {
                                                        client.ModelHash = fullPacket.PedModelHash.Value;
                                                        NetEntityHandler.ToDict()[fullPacket.NetHandle.Value].ModelHash =
                                                            fullPacket.PedModelHash.Value;
                                                        lock (RunningResources)
                                                            RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                            {
                                                                en.InvokePlayerModelChange(client, oldValue);
                                                            }));
                                                    }
                                                }
                                                ResendPacket(fullPacket, client, false);
                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                                // ignored
                                            }
                                        }
                                        break;
                                        case PacketType.BulletSync:
                                        {
                                            if (!client.ConnectionConfirmed) continue;
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var bin = msg.ReadBytes(len);

                                                int netHandle;
                                                Vector3 aimPoint;

                                                var shooting =
                                                    PacketOptimization.ReadBulletSync(bin, out netHandle, out aimPoint);

                                                netHandle = client.handle.Value;

                                                ResendBulletPacket(netHandle, aimPoint, shooting, client);
                                            }
                                            catch
                                            {
                                                // ignored
                                            }
                                        }
                                        break;
                                    case PacketType.BulletPlayerSync:
                                        {
                                            if (!client.ConnectionConfirmed) continue;
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var bin = msg.ReadBytes(len);

                                                int netHandle;
                                                int netHandleTarget;

                                                var shooting =
                                                    PacketOptimization.ReadBulletSync(bin, out netHandle, out netHandleTarget);

                                                netHandle = client.handle.Value;

                                                ResendBulletPacket(netHandle, netHandleTarget, shooting, client);
                                            }
                                            catch
                                            {
                                                // ignored
                                            }
                                        }
                                        break;
                                    case PacketType.UnoccupiedVehSync:
                                        {
                                            if (!client.ConnectionConfirmed) continue;
                                            try
                                            {
                                                var len = msg.ReadInt32();
                                                var bin = msg.ReadBytes(len);

                                                for (int i = 0; i < bin[0]; i++)
                                                {
                                                    var cVehBin = bin.Skip(1 + 46 * i).Take(46).ToArray();

                                                    var fullPacket = PacketOptimization.ReadUnoccupiedVehicleSync(cVehBin);

                                                    if (NetEntityHandler.ToDict()
                                                        .ContainsKey(fullPacket.VehicleHandle.Value))
                                                    {
                                                        NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value].Position
                                                            = fullPacket.Position;
                                                        NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value].Rotation
                                                            = fullPacket.Quaternion;
                                                        NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value].Velocity
                                                            = fullPacket.Velocity;

                                                        ((VehicleProperties)
                                                                NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                            .Tires = fullPacket.PlayerHealth.Value;

                                                        if (((VehicleProperties)
                                                                NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                            .DamageModel == null)
                                                            ((VehicleProperties)
                                                                    NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                                .DamageModel = new VehicleDamageModel();

                                                        var oldDoors = ((VehicleProperties)
                                                                NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                            .DamageModel.BrokenWindows;
                                                        var oldWindows = ((VehicleProperties)
                                                                NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                            .DamageModel.BrokenDoors;

                                                        ((VehicleProperties)
                                                                NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                            .DamageModel.BrokenWindows =
                                                            fullPacket.DamageModel.BrokenWindows;
                                                        ((VehicleProperties)
                                                                NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                            .DamageModel.BrokenDoors =
                                                            fullPacket.DamageModel.BrokenDoors;

                                                        if ((oldDoors ^ fullPacket.DamageModel.BrokenDoors) != 0)
                                                        {
                                                            lock (RunningResources)
                                                            {
                                                                for (int k = 0; k < 8; k++)
                                                                {
                                                                    if (((oldDoors ^ fullPacket.DamageModel.BrokenDoors) &
                                                                         1 << k) == 0) continue;

                                                                    RunningResources.ForEach(
                                                                        fs => fs.Engines.ForEach(en =>
                                                                        {
                                                                            if (fullPacket.VehicleHandle != null)
                                                                                en.InvokeVehicleDoorBreak(
                                                                                    new NetHandle(
                                                                                        fullPacket.VehicleHandle.Value), k);
                                                                        }));
                                                                }
                                                            }
                                                        }


                                                        if ((oldWindows ^ fullPacket.DamageModel.BrokenWindows) != 0)
                                                        {
                                                            lock (RunningResources)
                                                            {
                                                                for (int k = 0; k < 8; k++)
                                                                {
                                                                    if (((oldDoors ^ fullPacket.DamageModel.BrokenWindows) &
                                                                         1 << k) == 0) continue;

                                                                    RunningResources.ForEach(
                                                                        fs => fs.Engines.ForEach(en =>
                                                                        {
                                                                            if (fullPacket.VehicleHandle != null)
                                                                                en.InvokeVehicleWindowBreak(
                                                                                    new NetHandle(
                                                                                        fullPacket.VehicleHandle.Value), k);
                                                                        }));
                                                                }
                                                            }
                                                        }

                                                        if (fullPacket.Flag.HasValue)
                                                        {
                                                            var newDead = (fullPacket.Flag &
                                                                           (byte)VehicleDataFlags.VehicleDead) > 0;
                                                            var oldDead = ((VehicleProperties)
                                                                    NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value
                                                                    ])
                                                                .IsDead;

                                                            ((VehicleProperties)
                                                                    NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value])
                                                                .IsDead = newDead;

                                                            if (!oldDead && newDead)
                                                            {
                                                                lock (RunningResources)
                                                                    RunningResources.ForEach(
                                                                        fs => fs.Engines.ForEach(en =>
                                                                        {
                                                                            if (fullPacket.VehicleHandle != null)
                                                                                en.InvokeVehicleDeath(new NetHandle(fullPacket
                                                                                    .VehicleHandle.Value));
                                                                        }));
                                                            }
                                                        }

                                                        if (fullPacket.VehicleHealth.HasValue)
                                                        {
                                                            var oldValue = ((VehicleProperties)
                                                                    NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value
                                                                    ])
                                                                .Health;

                                                            ((VehicleProperties)
                                                                    NetEntityHandler.ToDict()[fullPacket.VehicleHandle.Value
                                                                    ])
                                                                .Health = fullPacket.VehicleHealth.Value;

                                                            if (fullPacket.VehicleHealth.Value != oldValue)
                                                            {
                                                                lock (RunningResources)
                                                                    RunningResources.ForEach(
                                                                        fs => fs.Engines.ForEach(en =>
                                                                        {
                                                                            en.InvokeVehicleHealthChange(client,
                                                                                oldValue);
                                                                        }));
                                                            }
                                                        }
                                                    }

                                                    ResendUnoccupiedPacket(fullPacket, client);

                                                    UpdateAttachables(fullPacket.VehicleHandle.Value);
                                                }
                                            }
                                            catch (IndexOutOfRangeException ex)
                                            {
                                                if (LogLevel > 0) Program.Output(ex.ToString());
                                            }
                                        }
                                        break;
                                    case PacketType.ConnectionPacket:
                                        {
                                            /*try
                                            {
                                                var len = msg.ReadInt32();
                                                var data =
                                                    DeserializeBinary<PedData>(msg.ReadBytes(len)) as PedData;
                                                if (data != null)
                                                {
                                                    SendToAll(data, PacketType.NpcPedPositionData, false, client, ConnectionChannel.PositionData);
                                                }
                                            }
                                            catch (IndexOutOfRangeException)
                                            {
                                            }*/
                                        }
                                        break;
                                    case PacketType.SyncEvent:
                                        {
                                            if (!client.ConnectionConfirmed) continue;
                                            var len = msg.ReadInt32();
                                            var data = DeserializeBinary<SyncEvent>(msg.ReadBytes(len)) as SyncEvent;
                                            if (data != null)
                                            {
                                                SendToAll(data, PacketType.SyncEvent, true, client, ConnectionChannel.SyncEvent);
                                                HandleSyncEvent(client, data);
                                            }
                                        }
                                        break;
                                    case PacketType.ScriptEventTrigger:
                                        {
                                            if (!client.ConnectionConfirmed) continue;
                                            var len = msg.ReadInt32();
                                            var data =
                                                DeserializeBinary<ScriptEventTrigger>(msg.ReadBytes(len)) as ScriptEventTrigger;
                                            if (data != null)
                                            {
                                                lock (RunningResources)
                                                    RunningResources.ForEach(
                                                        en =>
                                                        {
                                                            if (en.DirectoryName != data.Resource) return;

                                                            en.Engines.ForEach(fs =>
                                                            {
                                                                fs.InvokeClientEvent(client, data.EventName,
                                                                    DecodeArgumentListPure(data.Arguments?.ToArray() ??
                                                                                           new NativeArgument[0])
                                                                        .ToArray());
                                                            });
                                                        });
                                            }
                                        }
                                        break;
                                    case PacketType.NativeResponse:
                                        {
                                            if (!client.ConnectionConfirmed) continue;
                                            var len = msg.ReadInt32();
                                            var data = DeserializeBinary<NativeResponse>(msg.ReadBytes(len)) as NativeResponse;

                                            if (data == null || !_callbacks.ContainsKey(data.Id)) continue;
                                            object resp = null;
                                            var argument = data.Response as IntArgument;
                                            if (argument != null)
                                            {
                                                resp = argument.Data;
                                            }
                                            else if (data.Response is UIntArgument)
                                            {
                                                resp = ((UIntArgument)data.Response).Data;
                                            }
                                            else if (data.Response is StringArgument)
                                            {
                                                resp = ((StringArgument)data.Response).Data;
                                            }
                                            else if (data.Response is FloatArgument)
                                            {
                                                resp = ((FloatArgument)data.Response).Data;
                                            }
                                            else if (data.Response is BooleanArgument)
                                            {
                                                resp = ((BooleanArgument)data.Response).Data;
                                            }
                                            else if (data.Response is Vector3Argument)
                                            {
                                                var tmp = (Vector3Argument)data.Response;
                                                resp = new Vector3()
                                                {
                                                    X = tmp.X,
                                                    Y = tmp.Y,
                                                    Z = tmp.Z,
                                                };
                                            }
                                            if (_callbacks.ContainsKey(data.Id))
                                                _callbacks[data.Id].Invoke(resp);
                                            _callbacks.Remove(data.Id);
                                        }
                                        break;
                                    case PacketType.FileAcceptDeny:
                                        {
                                            var fileId = msg.ReadInt32();
                                            var hasBeenAccepted = msg.ReadBoolean();
                                            var ourD = Downloads.FirstOrDefault(d => d.Parent == client);
                                            if (ourD != null && ourD.Files.Count > 0 && ourD.Files[0].Id == fileId &&
                                                !ourD.Files[0].Accepted)
                                            {
                                                if (!hasBeenAccepted)
                                                {
                                                    ourD.Files.RemoveAt(0);
                                                }
                                                else
                                                {
                                                    ourD.Files[0].Accepted = true;
                                                }
                                            }
                                        }
                                        break;
                                    case PacketType.PlayerKilled:
                                        {
                                            var reason = msg.ReadInt32();
                                            var weapon = msg.ReadInt32();

                                            lock (RunningResources)
                                            {
                                                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                {
                                                    en.InvokePlayerDeath(client, reason, weapon);
                                                }));
                                            }

                                            PublicAPI.setEntityData(client, "__LAST_PLAYER_DEATH", PublicAPI.TickCount);
                                        }
                                        break;
                                    case PacketType.PlayerRespawned:
                                        {
                                            PublicAPI.removeAllPlayerWeapons(client);
                                            PublicAPI.stopPlayerAnimation(client);

                                            lock (RunningResources)
                                                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                                                {
                                                    en.InvokePlayerRespawn(client);
                                                }));

                                            PublicAPI.setEntityData(client, "__LAST_PLAYER_RESPAWN", PublicAPI.TickCount);
                                        }
                                        break;
                                    case PacketType.UpdateEntityProperties:
                                        {
                                            if (TrustClientProperties)
                                            {
                                                var len = msg.ReadInt32();
                                                var data =
                                                    DeserializeBinary<UpdateEntity>(msg.ReadBytes(len)) as UpdateEntity;
                                                if (data != null && data.Properties != null)
                                                {
                                                    var item =
                                                        NetEntityHandler.NetToProp<EntityProperties>(data.NetHandle);

                                                    if (item != null)
                                                    {
                                                        if (data.Properties.SyncedProperties != null)
                                                        {
                                                            if (item.SyncedProperties == null)
                                                                item.SyncedProperties =
                                                                    new Dictionary<string, NativeArgument>();
                                                            foreach (var pair in data.Properties.SyncedProperties)
                                                            {
                                                                if (pair.Value is LocalGamePlayerArgument)
                                                                    item.SyncedProperties.Remove(pair.Key);
                                                                else
                                                                {
                                                                    object oldValue =
                                                                        DecodeArgumentListPure(
                                                                            item.SyncedProperties.Get(pair.Key));
                                                                    item.SyncedProperties.Set(pair.Key, pair.Value);
                                                                    NetHandle ent = new NetHandle(data.NetHandle);
                                                                    lock (RunningResources)
                                                                        RunningResources.ForEach(
                                                                            fs => fs.Engines.ForEach(en =>
                                                                            {
                                                                                en.InvokeEntityDataChange(ent, pair.Key,
                                                                                    oldValue);
                                                                            }));
                                                                }
                                                            }
                                                        }
                                                    }

                                                    UpdateEntityInfo(data.NetHandle, (EntityType)data.EntityType,
                                                        data.Properties, client);
                                                }
                                            }

                                        }
                                        break;
                                }
                                break;
                            default:
                                if (LogLevel > 0) Program.Output("WARN: Unhandled type: " + msg.MessageType);
                                break;
                        }
                    }
                    catch (InvalidCastException)
                    {
                        Program.ToFile("attack.log", "Suspected connection exploit [" + client.NetConnection.RemoteEndPoint.Address.ToString() + "], Message type: " + msg.MessageType + " |" + " Packet type: " + packetType + " |" + " Exception: InvalidCastException");
                        if (!connBlock.Contains(client.NetConnection.RemoteEndPoint.Address)) connBlock.Add(client.NetConnection.RemoteEndPoint.Address);
                    }
                    catch (Exception ex)
                    {
                        // Program.Output("EXCEPTION IN MESSAGEPUMP, MSG TYPE: " + msg.MessageType + " DATA TYPE: " + packetType);
                        // Program.Output(ex.ToString());
                        if (LogLevel > 0)
                        {
                            Program.Output("--> Exception in the Netcode.");
                            Program.Output("--> Message type: " + msg.MessageType + " |" + " Packet type: " + packetType);
                            Program.Output("\n===\n" + ex.ToString() + "\n===");
                        }
                    }
                    finally
                    {
                        Server.Recycle(msg);
                    }
                }
        }

        private void UpdateAttachables(int root)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(root);

            if (prop == null || prop.Attachables == null) return;

            foreach (var attachable in prop.Attachables)
            {
                // TODO: Proper position with offsets
                var attachableProp = NetEntityHandler.NetToProp<EntityProperties>(attachable);

                if (attachableProp == null) continue;

                attachableProp.Position = prop.Position;

                UpdateAttachables(attachable);
            }
        }

        public Client GetClientFromName(string name)
        {
            //return Clients.FirstOrDefault(c => c.Name.ToLower() == name.ToLower());
            return Clients.FirstOrDefault(c => c.Name.ToLower().StartsWith(name.ToLower()));
        }

        public void Tick()
        {
            if (IsClosing)
            {
                Streamer.Stop = true;

                try
                {
                    lock (RunningResources)
                    {
                        for (int i = RunningResources.Count - 1; i >= 0; i--)
                        {
                            StopResource(RunningResources[i].DirectoryName);
                        }
                    }

                    for (int i = Clients.Count - 1; i >= 0; i--)
                    {
                        if (!Clients[i].Fake) Clients[i].NetConnection.Disconnect("Server shutdown.");
                    }

                    ColShapeManager.Shutdown();
                    //FileServer.Dispose(); //Causes nullref on server termination
                    if (UseUPnP) Server.UPnP?.DeleteForwardingRule(Port);
                }
                catch (Exception e) { Program.Output(e.ToString()); }

                ReadyToClose = true;
                return;
            }

            if (Downloads.Count > 0)
            {
                for (int i = Downloads.Count - 1; i >= 0; i--)
                {
                    if (Downloads[i].Files.Count > 0)
                    {
                        if (!Downloads[i].Parent.NetConnection.CanSendImmediately(NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.FileTransfer)) continue;
                        if (!Downloads[i].Files[0].HasStarted)
                        {
                            var notifyObj = new DataDownloadStart
                            {
                                FileType = (byte)Downloads[i].Files[0].Type,
                                ResourceParent = Downloads[i].Files[0].Resource,
                                FileName = Downloads[i].Files[0].Name,
                                Id = Downloads[i].Files[0].Id,
                                Length = Downloads[i].Files[0].Data.Length,
                                Md5Hash = Downloads[i].Files[0].Hash
                            };
                            SendToClient(Downloads[i].Parent, notifyObj, PacketType.FileTransferRequest, true, ConnectionChannel.FileTransfer);
                            Downloads[i].Files[0].HasStarted = true;
                        }

                        if (!Downloads[i].Files[0].Accepted) continue;

                        var remaining = Downloads[i].Files[0].Data.Length - Downloads[i].Files[0].BytesSent;
                        var sendBytes = (remaining > Downloads[i].ChunkSize
                            ? Downloads[i].ChunkSize
                            : (int)remaining);

                        var updateObj = Server.CreateMessage();
                        updateObj.Write((byte)PacketType.FileTransferTick);
                        updateObj.Write(Downloads[i].Files[0].Id);
                        updateObj.Write(sendBytes);
                        updateObj.Write(Downloads[i].Files[0].Data, (int)Downloads[i].Files[0].BytesSent, sendBytes);
                        Downloads[i].Files[0].BytesSent += sendBytes;

                        Server.SendMessage(updateObj, Downloads[i].Parent.NetConnection, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.FileTransfer);

                        if (remaining - sendBytes > 0) continue;

                        var endObject = Server.CreateMessage();
                        endObject.Write((byte)PacketType.FileTransferComplete);
                        endObject.Write(Downloads[i].Files[0].Id);

                        Server.SendMessage(endObject, Downloads[i].Parent.NetConnection, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.FileTransfer);
                        Downloads[i].Files.RemoveAt(0);
                    }
                    else
                    {
                        Downloads.RemoveAt(i);
                    }
                }
            }

            ProcessMessages();

            NetEntityHandler.UpdateMovements();

            PickupManager.Pulse();

            UnoccupiedVehicleManager.Pulse();

            lock (RunningResources)
            {
                //RunningResources.ForEach(fs => fs.Engines.ForEach(en => { en.InvokeUpdate(); }));

                for (int i = RunningResources.Count - 1; i >= 0; i--)
                {
                    for (int j = RunningResources[i].Engines.Count - 1; j >= 0; j--)
                    {
                        RunningResources[i].Engines[j].InvokeUpdate();
                    }

                    if (!RunningResources[i].Engines.Any(en => en.HasTerminated)) continue;
                    Program.Output("TERMINATING RESOURCE " + RunningResources[i].DirectoryName + " BECAUSE AN ENGINE HAS BEEN TERMINATED.");
                    RunningResources.RemoveAt(i);
                }
            }

            /*if (AnnounceSelf && DateTime.Now.Subtract(_lastAnnounceDateTime).TotalMinutes >= 2)
            {
                _lastAnnounceDateTime = DateTime.Now;
                AnnounceSelfToMaster();
            }*/

            lock (queue)
            {
                for (int i = queue.Count - 1; i >= 0; i--)
                {
                    if (DateTime.Now.Subtract(queue.ElementAt(i).Value).TotalSeconds >= 60)
                    {
                        queue.Remove(queue.ElementAt(i).Key);
                    }
                }
            }

            lock (Clients)
            {
                for (var i = Clients.Count - 1; i >= 0; i--) // Kick AFK players
                {
                    var time = Clients[i].LastUpdate != default(DateTime) ? DateTime.Now.Subtract(Clients[i].LastUpdate).TotalSeconds : 0;

                    if (time > 70)
                    {
                        Clients.Remove(Clients[i]);
                    }
                    else if (time > 10)
                    {
                        Clients[i].NetConnection.Disconnect("Timed out.");
                        //DisconnectClient(Clients[i], "Timeout");
                    }

                }
            }
        }

        private void HandleSyncEvent(Client sender, SyncEvent data)
        {
            var args = DecodeArgumentList(data.Arguments?.ToArray()).ToList();

            switch ((SyncEventType)data.EventType)
            {
                case SyncEventType.DoorStateChange:
                    {
                        var doorId = (int)args[1];
                        var newFloat = (bool)args[2];
                        if (NetEntityHandler.ToDict().ContainsKey((int)args[0]))
                        {
                            if (newFloat)
                                ((VehicleProperties)NetEntityHandler.ToDict()[(int)args[0]]).Doors |= (byte)(1 << doorId);
                            else
                                ((VehicleProperties)NetEntityHandler.ToDict()[(int)args[0]]).Doors &= (byte)(~(1 << doorId));
                        }
                    }
                    break;
                case SyncEventType.TrailerDeTach:
                    {
                        var newState = (bool)args[0];
                        if (!newState)
                        {
                            if (NetEntityHandler.ToDict().ContainsKey((int)args[1]))
                            {
                                if (
                                    NetEntityHandler.ToDict()
                                        .ContainsKey((NetEntityHandler.NetToProp<VehicleProperties>((int)args[1])).Trailer))
                                    ((VehicleProperties)
                                        NetEntityHandler.ToDict()[
                                            NetEntityHandler.NetToProp<VehicleProperties>((int)args[1]).Trailer])
                                        .TraileredBy = 0;

                                ((VehicleProperties)NetEntityHandler.ToDict()[(int)args[1]]).Trailer = 0;
                            }
                        }
                        else
                        {
                            if (NetEntityHandler.ToDict().ContainsKey((int)args[1]))
                                ((VehicleProperties)NetEntityHandler.ToDict()[(int)args[1]]).Trailer = (int)args[2];

                            if (NetEntityHandler.ToDict().ContainsKey((int)args[2]))
                                ((VehicleProperties)NetEntityHandler.ToDict()[(int)args[2]]).TraileredBy = (int)args[1];
                        }

                        lock (RunningResources)
                            RunningResources.ForEach(
                                fs => fs.Engines.ForEach(en =>
                                {
                                    en.InvokeVehicleTrailerChange(new NetHandle((int)args[1]), (bool)args[0] ? new NetHandle((int)args[2]) : new NetHandle());
                                }));
                        break;
                    }
                case SyncEventType.TireBurst:
                    {
                        var veh = (int)args[0];
                        var tireId = (int)args[1];
                        var isBursted = (bool)args[2];
                        if (NetEntityHandler.ToDict().ContainsKey(veh))
                        {
                            var oldValue = (((VehicleProperties)NetEntityHandler.ToDict()[(int)args[0]]).Tires &
                                            (byte)(1 << tireId)) != 0;

                            if (isBursted)
                                ((VehicleProperties)NetEntityHandler.ToDict()[(int)args[0]]).Tires |= (byte)(1 << tireId);
                            else
                                ((VehicleProperties)NetEntityHandler.ToDict()[(int)args[0]]).Tires &= (byte)(~(1 << tireId));

                            if (oldValue ^ isBursted)
                            {
                                lock (RunningResources)
                                    RunningResources.ForEach(
                                        fs => fs.Engines.ForEach(en =>
                                        {
                                            en.InvokeVehicleTyreBurst(new NetHandle((int)args[0]), tireId);
                                        }));
                            }
                        }
                        break;
                    }
                case SyncEventType.PickupPickedUp:
                    {
                        var pickupId = (int)args[0];

                        if (NetEntityHandler.ToDict().ContainsKey(pickupId))
                        {
                            if (!((PickupProperties)NetEntityHandler.ToDict()[pickupId]).PickedUp)
                            {
                                ((PickupProperties)NetEntityHandler.ToDict()[pickupId]).PickedUp = true;
                                lock (RunningResources)
                                {
                                    RunningResources.ForEach(res => res.Engines.ForEach(en => en.InvokePlayerPickup(sender, new NetHandle(pickupId))));
                                }
                                if (((PickupProperties)NetEntityHandler.ToDict()[pickupId]).RespawnTime > 0)
                                    PickupManager.Add(pickupId);

                                if (
                                    PickupToWeapon.Translate(
                                        ((PickupProperties)NetEntityHandler.ToDict()[pickupId]).ModelHash) != 0)
                                {
                                    var wh = (WeaponHash)PickupToWeapon.Translate(((PickupProperties)NetEntityHandler.ToDict()[pickupId]).ModelHash);
                                    if (!sender.Weapons.ContainsKey(wh))
                                    {
                                        sender.Weapons.Add(wh, ((PickupProperties)NetEntityHandler.ToDict()[pickupId]).Amount);
                                    }
                                    else
                                    {
                                        PublicAPI.setPlayerWeaponAmmo(sender, wh, sender.getWeaponAmmo(wh) + ((PickupProperties)NetEntityHandler.ToDict()[pickupId]).Amount);
                                    }
                                }
                            }
                        }
                        break;
                    }
                case SyncEventType.StickyBombDetonation:
                    {
                        var playerId = (int)args[0];
                        var c = PublicAPI.getPlayerFromHandle(new NetHandle(playerId));

                        lock (RunningResources)
                            RunningResources.ForEach(
                                fs => fs.Engines.ForEach(en =>
                                {
                                    en.InvokePlayerDetonateStickies(c);
                                }));
                    }
                    break;
            }
        }

        public IEnumerable<object> DecodeArgumentList(params NativeArgument[] args)
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
                else if (arg is Vector3Argument)
                {
                    var tmp = (Vector3Argument)arg;
                    list.Add(new Vector3(tmp.X, tmp.Y, tmp.Z));
                }
                else if (arg == null)
                {
                    list.Add(null);
                }
            }

            return list;
        }

        public IEnumerable<object> DecodeArgumentListPure(params NativeArgument[] args)
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
                else if (arg is Vector3Argument)
                {
                    var tmp = (Vector3Argument)arg;
                    list.Add(new CherryMPShared.Vector3(tmp.X, tmp.Y, tmp.Z));
                }
                else if (arg is EntityArgument)
                {
                    list.Add(new NetHandle(((EntityArgument) arg).NetHandle));
                }
                else if (arg is ListArgument)
                {
                    List<object> output = new List<object>();
                    var larg = (ListArgument)arg;
                    output.AddRange(DecodeArgumentListPure(larg.Data.ToArray()));
                    list.Add(output);
                }
                else if (args == null)
                {
                    list.Add(null);
                }
            }

            return list;
        }

        public void SendToClient(Client c, object newData, PacketType packetType, bool important, ConnectionChannel channel)
        {
            var data = SerializeBinary(newData);
            var msg = Server.CreateMessage();
            msg.Write((byte)packetType);
            msg.Write(data.Length);
            msg.Write(data);
            Server.SendMessage(msg, c.NetConnection, important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.UnreliableSequenced, (int)channel);
        }

        public void SendToAll(object newData, PacketType packetType, bool important, ConnectionChannel channel)
        {
            var data = SerializeBinary(newData);
            var msg = Server.CreateMessage();
            msg.Write((byte)packetType);
            msg.Write(data.Length);
            msg.Write(data);

            Server.SendToAll(msg, null, important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced, (int)channel);
        }

        public void SendToAll(object newData, PacketType packetType, bool important, Client exclude, ConnectionChannel channel)
        {
            var data = SerializeBinary(newData);
            var msg = Server.CreateMessage();
            msg.Write((byte)packetType);
            msg.Write(data.Length);
            msg.Write(data);

            Server.SendToAll(msg, exclude.NetConnection, important ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.ReliableSequenced, (int)channel);
        }

        public object DeserializeBinary<T>(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                try
                {
                    return Serializer.Deserialize<T>(stream);
                }
                catch (ProtoException e)
                {
                    if(LogLevel > 0) Program.Output("WARN: Deserialization failed: " + e.Message);
                    return null;
                }
            }
        }

        public byte[] SerializeBinary(object data)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, data);
                return stream.ToArray();
            }
        }

        //public byte GetChannelIdForConnection(Client conn)
        //{
            //lock (Clients) return (byte)(((Clients.IndexOf(conn)) % 31) + 1);
        //}

        public NativeArgument ParseReturnType(Type t)
        {
            if (t == typeof(int))
            {
                return new IntArgument();
            }
            else if (t == typeof(uint))
            {
                return new UIntArgument();
            }
            else if (t == typeof(string))
            {
                return new StringArgument();
            }
            else if (t == typeof(float))
            {
                return new FloatArgument();
            }
            else if (t == typeof(double))
            {
                return new FloatArgument();
            }
            else if (t == typeof(bool))
            {
                return new BooleanArgument();
            }
            else if (t == typeof(Vector3))
            {
                return new Vector3Argument();
            }
            else
            {
                return null;
            }
        }

        public List<NativeArgument> ParseNativeArguments(params object[] args)
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
                    list.Add((EntityPointerArgument) o);
                }
                else if (o is NetHandle)
                {
                    list.Add(new EntityArgument(((NetHandle) o).Value));
                }
                else if (o is Entity)
                {
                    list.Add(new EntityArgument(((Entity)o).Value));
                }
                else if (o is Client)
                {
                    list.Add(new EntityArgument(((Client) o).handle.Value));
                }
                else if (o is IList)
                {
                    var larg = new ListArgument();
                    var l = ((IList) o);
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

        public void SendDeleteObject(Client player, Vector3 pos, float radius, int modelHash)
        {
            var obj = new ObjectData();
            obj.Position = pos;
            obj.Radius = radius;
            obj.modelHash = modelHash;
            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();
            msg.Write((byte)PacketType.DeleteObject);
            msg.Write(bin.Length);
            msg.Write(bin);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.Default);
        }

        public void SendNativeCallToPlayer(Client player, bool safe, ulong hash, params object[] arguments)
        {
            var obj = new NativeData
            {
                Hash = hash,
                Internal = safe,
                Arguments = ParseNativeArguments(arguments)
            };
            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();
            msg.Write((byte)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.NativeCall);
        }

        public void SendNativeCallToAllPlayers(bool safe, ulong hash, params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;
            obj.Internal = safe;
            obj.Arguments = ParseNativeArguments(arguments);
            obj.ReturnType = null;
            obj.Id = 0;

            var bin = SerializeBinary(obj);

            foreach (var c in Clients)
            {
                var msg = Server.CreateMessage();

                msg.Write((byte) PacketType.NativeCall);
                msg.Write(bin.Length);
                msg.Write(bin);

                Server.SendMessage(msg, c.NetConnection, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.NativeCall);
            }
        }

        private uint _nativeCount = 0;
        public object ReturnNativeCallFromPlayer(Client player, bool safe, ulong hash, NativeArgument returnType, params object[] args)
        {
            _nativeCount++;
            object output = null;
            GetNativeCallFromPlayer(player, safe, _nativeCount, hash, returnType, (o) =>
            {
                output = o;
            }, args);

            DateTime start = DateTime.Now;
            while (output == null && DateTime.Now.Subtract(start).Milliseconds < 10000)
            {}
            
            return output;
        }

        private Dictionary<uint, Action<object>> _callbacks = new Dictionary<uint, Action<object>>();
        public void GetNativeCallFromPlayer(Client player, bool safe, uint salt, ulong hash, NativeArgument returnType, Action<object> callback,
            params object[] arguments)
        {
            var obj = new NativeData();
            obj.Hash = hash;
            obj.ReturnType = returnType;
            obj.Id = salt;
            obj.Arguments = ParseNativeArguments(arguments);
            obj.Internal = safe;

            var bin = SerializeBinary(obj);

            var msg = Server.CreateMessage();

            msg.Write((byte)PacketType.NativeCall);
            msg.Write(bin.Length);
            msg.Write(bin);

            _callbacks.Add(salt, callback);
            player.NetConnection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, (int)ConnectionChannel.NativeCall);
        }

        public void TransferLargeString(Client target, string data, string resourceSource)
        {
            if (target == null || string.IsNullOrEmpty(data)) return;
            var bytes = Encoding.UTF8.GetBytes(data);

            var r = new Random();

            var mapData = new StreamedData();
            mapData.Id = r.Next(int.MaxValue);
            mapData.Data = bytes;
            mapData.Type = FileType.CustomData;
            mapData.Resource = resourceSource;
            mapData.Name = "data";

            lock (Downloads)
            {
                StreamingClient cl;
                if ((cl = Downloads.FirstOrDefault(c => c.Parent == target)) != null)
                {
                    cl.Files.Add(mapData);
                }
                else
                {
                    var downloader = new StreamingClient(target);
                    downloader.Files.Add(mapData);

                    Downloads.Add(downloader);
                }
            }
        }

        public void CreatePositionInterpolation(int entity, Vector3 target, int duration)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null) return;

            var mov = new Movement();
            mov.ServerStartTime = Program.GetTicks();
            mov.Duration = duration;
            mov.StartVector = prop.Position;
            mov.EndVector = target;
            mov.Start = 0;
            prop.PositionMovement = mov;

            var delta = new Delta_EntityProperties();
            delta.PositionMovement = mov;
            UpdateEntityInfo(entity, EntityType.Prop, delta);
        }

        public void CreateRotationInterpolation(int entity, Vector3 target, int duration)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null) return;

            var mov = new Movement();
            mov.ServerStartTime = Program.GetTicks();
            mov.Duration = duration;
            mov.StartVector = prop.Position;
            mov.EndVector = target;
            mov.Start = 0;
            prop.RotationMovement = mov;

            var delta = new Delta_EntityProperties();
            delta.RotationMovement = mov;
            UpdateEntityInfo(entity, EntityType.Prop, delta);
        }

        public bool SetEntityProperty(int entity, string key, object value, bool world = false)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null || string.IsNullOrEmpty(key)) return false;

            if (prop.SyncedProperties == null) prop.SyncedProperties = new Dictionary<string, NativeArgument>();

            var nativeArg = ParseNativeArguments(value).Single();

            object oldValue = DecodeArgumentListPure(prop.SyncedProperties.Get(key)).FirstOrDefault();

            prop.SyncedProperties.Set(key, nativeArg);

            var delta = new Delta_EntityProperties();
            delta.SyncedProperties = new Dictionary<string, NativeArgument>();
            delta.SyncedProperties.Add(key, nativeArg);
            UpdateEntityInfo(entity, world ? EntityType.World : EntityType.Prop, delta);

            var ent = new NetHandle(entity);

            lock (RunningResources)
                RunningResources.ForEach(fs => fs.Engines.ForEach(en =>
                {
                    en.InvokeEntityDataChange(ent, key, oldValue);
                }));

            return true;
        }

        public void ResetEntityProperty(int entity, string key, bool world = false)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null || string.IsNullOrEmpty(key)) return;

            if (prop.SyncedProperties == null || !prop.SyncedProperties.ContainsKey(key)) return;

            prop.SyncedProperties.Remove(key);

            var delta = new Delta_EntityProperties();
            delta.SyncedProperties = new Dictionary<string, NativeArgument>();
            delta.SyncedProperties.Add(key, new LocalGamePlayerArgument());
            UpdateEntityInfo(entity, world ? EntityType.World : EntityType.Prop, delta);
        }

        public bool HasEntityProperty(int entity, string key)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null || string.IsNullOrEmpty(key) || prop.SyncedProperties == null) return false;

            return prop.SyncedProperties.ContainsKey(key);
        }

        public dynamic GetEntityProperty(int entity, string key)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null || string.IsNullOrEmpty(key)) return null;

            if (prop.SyncedProperties == null || !prop.SyncedProperties.ContainsKey(key)) return null;

            var natArg = prop.SyncedProperties[key];

            return DecodeArgumentListPure(natArg).Single();
        }

        public string[] GetEntityAllProperties(int entity)
        {
            var prop = NetEntityHandler.NetToProp<EntityProperties>(entity);

            if (prop == null) return new string[0];

            if (prop.SyncedProperties == null || prop.SyncedProperties.Any(pair => string.IsNullOrEmpty(pair.Key))) return new string[0];

            //return prop.SyncedProperties.Select(pair => DecodeArgumentListPure(pair.Value).Single().ToString()).ToArray(); //Returns all the values
            return prop.SyncedProperties.Select(pair => pair.Key).ToArray();
        }

        public void ChangePlayerTeam(Client target, int newTeam)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.handle.Value))
            {
                ((PlayerProperties) NetEntityHandler.ToDict()[target.handle.Value]).Team = newTeam;
            }

            var obj = new SyncEvent();
            obj.EventType = (byte) ServerEventType.PlayerTeamChange;
            obj.Arguments = ParseNativeArguments(target.handle.Value, newTeam);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipColor(Client target, int newColor)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.handle.Value))
            {
                ((PlayerProperties)NetEntityHandler.ToDict()[target.handle.Value]).BlipColor = newColor;
            }

            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipColorChange;
            obj.Arguments = ParseNativeArguments(target.handle.Value, newColor);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipColorForPlayer(Client target, int newColor, Client forPlayer)
        {
            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipColorChange;
            obj.Arguments = ParseNativeArguments(target.handle.Value, newColor);

            SendToClient(forPlayer, obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipSprite(Client target, int newSprite)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.handle.Value))
            {
                ((PlayerProperties)NetEntityHandler.ToDict()[target.handle.Value]).BlipSprite = newSprite;
            }

            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipSpriteChange;
            obj.Arguments = ParseNativeArguments(target.handle.Value, newSprite);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipSpriteForPlayer(Client target, int newSprite, Client forPlayer)
        {
            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipSpriteChange;
            obj.Arguments = ParseNativeArguments(target.handle.Value, newSprite);

            SendToClient(forPlayer, obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipAlpha(Client target, int newAlpha)
        {
            if (NetEntityHandler.ToDict().ContainsKey(target.handle.Value))
            {
                ((PlayerProperties)NetEntityHandler.ToDict()[target.handle.Value]).BlipAlpha = (byte)newAlpha;
            }

            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipAlphaChange;
            obj.Arguments = ParseNativeArguments(target.handle.Value, newAlpha);

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void ChangePlayerBlipAlphaForPlayer(Client target, int newAlpha, Client forPlayer)
        {
            var obj = new SyncEvent();
            obj.EventType = (byte)ServerEventType.PlayerBlipAlphaChange;
            obj.Arguments = ParseNativeArguments(target.handle.Value, newAlpha);

            SendToClient(forPlayer, obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void SendServerEvent(ServerEventType type, params object[] arg)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)type,
                Arguments = ParseNativeArguments(arg)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void SendServerEventToPlayer(Client target, ServerEventType type, params object[] arg)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)type,
                Arguments = ParseNativeArguments(arg)
            };

            SendToClient(target, obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void DetachEntity(int nethandle, bool collision)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.EntityDetachment,
                Arguments = ParseNativeArguments(nethandle, collision)
            };
            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void SetPlayerOnSpectate(Client target, bool spectating)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerSpectatorChange,
                Arguments = ParseNativeArguments(target.handle.Value, spectating)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void SetPlayerOnSpectatePlayer(Client spectator, Client target)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerSpectatorChange,
                Arguments = ParseNativeArguments(spectator.handle.Value, true, target.handle.Value)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void PlayCustomPlayerAnimation(Client target, int flag, string animDict, string animName)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerAnimationStart,
                Arguments = ParseNativeArguments(target.handle.Value, flag, animDict, animName)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void PlayCustomPlayerAnimationStop(Client target)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerAnimationStop,
                Arguments = ParseNativeArguments(target.handle.Value)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }

        public void PlayCustomPlayerAnimationStop(Client target, string animDict, string animName)
        {
            var obj = new SyncEvent
            {
                EventType = (byte)ServerEventType.PlayerAnimationStop,
                Arguments = ParseNativeArguments(target.handle.Value, animDict, animName)
            };

            SendToAll(obj, PacketType.ServerEvent, true, ConnectionChannel.EntityBackend);
        }
    }
}
