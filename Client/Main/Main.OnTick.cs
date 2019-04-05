using CherryMP.Javascript;
using CherryMP.Misc;
using CherryMP.Networking;
using CherryMP.Util;
using CherryMPShared;
using GTA;
using GTA.Math;
using GTA.Native;
using Lidgren.Network;
using NativeUI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VehicleHash = GTA.VehicleHash;
using WeaponHash = GTA.WeaponHash;

namespace CherryMP
{
    internal partial class Main
    {
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
                    if (playerCar.GetResponsiblePed().Handle == player.Handle)
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
                    StringCache?.Pulse();
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
                                Game.Player.Character.PositionNoOffset = ent.Position + new GTA.Math.Vector3(0, 0, 1.3f);
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
                            Game.Player.Character.PositionNoOffset = CurrentSpectatingPlayer.Character.Position + new GTA.Math.Vector3(0, 0, 1.3f);
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

                var collection = new CallCollection();

                collection.Call(Hash.SET_RANDOM_TRAINS, 0);
                collection.Call(Hash.CAN_CREATE_RANDOM_COPS, false);
                DEBUG_STEP = 29;
                collection.Call(Hash.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                collection.Call(Hash.SET_VEHICLE_POPULATION_BUDGET, 0);
                collection.Call(Hash.SET_PED_POPULATION_BUDGET, 0);
                collection.Call(Hash.SUPPRESS_SHOCKING_EVENTS_NEXT_FRAME);
                collection.Call(Hash.SUPPRESS_AGITATION_EVENTS_NEXT_FRAME);
                collection.Call(Hash.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                collection.Call(Hash.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                collection.Call(Hash.SET_NUMBER_OF_PARKED_VEHICLES, -1);
                collection.Call(Hash.SET_ALL_LOW_PRIORITY_VEHICLE_GENERATORS_ACTIVE, false);
                collection.Call(Hash.SET_FAR_DRAW_VEHICLES, false);
                collection.Call(Hash.DESTROY_MOBILE_PHONE);
                collection.Call((Hash)0x015C49A93E3E086E, true);
                collection.Call(Hash.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f);
                collection.Call(Hash.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME, 0f, 0f);
                collection.Call(Hash.SET_CAN_ATTACK_FRIENDLY, Game.Player.Character, true, true);
                collection.Call(Hash.SET_PED_CAN_BE_TARGETTED, Game.Player.Character, true);
                collection.Call((Hash)0xF796359A959DF65D, false); // Display distant vehicles
                collection.Call(Hash.SET_AUTO_GIVE_PARACHUTE_WHEN_ENTER_PLANE, Game.Player, false);
                collection.Call((Hash)0xD2B315B6689D537D, Game.Player, false);
                collection.Call(Hash.DISPLAY_CASH, false);
                collection.Execute();
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
                        if (entity.IsVehicleEmpty() && !VehicleSyncManager.IsInterpolating(entity.Handle) && veh.TraileredBy == 0 && !VehicleSyncManager.IsSyncing(veh) && ((entity.Handle == Game.Player.LastVehicle?.Handle && DateTime.Now.Subtract(LastCarEnter).TotalMilliseconds > 3000) || entity.Handle != Game.Player.LastVehicle?.Handle))
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
    }
}