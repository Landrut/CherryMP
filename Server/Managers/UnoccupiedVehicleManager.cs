using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CherryMPShared;
using Lidgren.Network;

namespace CherryMPServer.Managers
{
    internal class UnoccupiedVehicleManager
    {
        private const int UPDATE_RATE = 500;
        private const float SYNC_RANGE = 130;
        private const float SYNC_RANGE_SQUARED = SYNC_RANGE*SYNC_RANGE;
        private const float DROPOFF = 30;
        private const float DROPOFF_SQUARED = DROPOFF*DROPOFF;

        private long _lastUpdate;

        private Dictionary<int, Client> Syncers = new Dictionary<int, Client>();

        public void Pulse()
        {
            if (Program.GetTicks() - _lastUpdate <= UPDATE_RATE) return;
            _lastUpdate = Program.GetTicks();
            Task.Run((Action)Update);
        }

        public Client GetSyncer(int handle)
        {
            return Syncers.Get(handle);
        }

        public void UnsyncAllFrom(Client player)
        {
            for (var i = Syncers.Count - 1; i >= 0; i--)
            {
                var el = Syncers.ElementAt(i);

                if (el.Value == player)
                {
                    StopSync(el.Value, el.Key);
                    Syncers.Remove(el.Key);
                }
            }
        }

        public static bool IsVehicleUnoccupied(NetHandle vehicle)
        {
            var players = Program.ServerInstance.PublicAPI.getAllPlayers();
            var vehicles = Program.ServerInstance.NetEntityHandler.ToCopy().Select(pair => pair.Value).Where(p => p is VehicleProperties).Cast<VehicleProperties>();
            var prop = Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value);

            return players.TrueForAll(c => c.CurrentVehicle != vehicle) && vehicles.All(v => v.Trailer != vehicle.Value) && prop.AttachedTo == null;
        }

        private void Update()
        {
            for (var index = Program.ServerInstance.PublicAPI.getAllVehicles().Count - 1; index >= 0; index--)
            {
                var vehicle = Program.ServerInstance.PublicAPI.getAllVehicles()[index];
                UpdateVehicle(vehicle.Value, Program.ServerInstance.NetEntityHandler.NetToProp<VehicleProperties>(vehicle.Value));
            }
        }

        public void UpdateVehicle(int handle, EntityProperties prop)
        {
            if (handle == 0 || prop == null) return;

            if (!IsVehicleUnoccupied(new NetHandle(handle))) //OCCUPIED
            {
                if (Syncers.ContainsKey(handle))
                {
                    StopSync(Syncers[handle], handle);
                }
                return;
            }

            if (prop.Position == null) return;

            var players = Program.ServerInstance.PublicAPI.getAllPlayers().Where(c => (c.Properties.Dimension == prop.Dimension || prop.Dimension == 0) && c.Position != null).OrderBy(c => c.Position.DistanceToSquared(prop.Position)).Take(1).ToArray();
            if (players[0] == null) return;

            if (players[0].Position.DistanceToSquared(prop.Position) < SYNC_RANGE_SQUARED / 2 && (players[0].Properties.Dimension == prop.Dimension || prop.Dimension == 0))
            {
                if (Syncers.ContainsKey(handle))
                {
                    if (Syncers[handle] != players[0])
                    {
                        StopSync(Syncers[handle], handle);
                        StartSync(players[0], handle);
                    }
                }
                else
                {
                    StartSync(players[0], handle);
                }
            }
            else
            {
                if (Syncers.ContainsKey(handle))
                {
                    StopSync(players[0], handle);
                }
            }
        }

        public void OverrideSyncer(int vehicleHandle, Client newSyncer)
        {
            if (Syncers.ContainsKey(vehicleHandle)) // We are currently syncing this vehicle
            {
                if (Syncers[vehicleHandle] == newSyncer) return;

                StopSync(Syncers[vehicleHandle], vehicleHandle);
                Syncers[vehicleHandle] = newSyncer;
            }
            else
            {
                Syncers.Add(vehicleHandle, newSyncer);
            }

            StartSync(newSyncer, vehicleHandle);
        }

        public void FindSyncer(int handle, VehicleProperties prop)
        {
            if (prop.Position == null) return;

            var players =
                Program.ServerInstance.PublicAPI.getAllPlayers()
                    .Where(c => (c.Properties.Dimension == prop.Dimension || prop.Dimension == 0) && c.Position != null)
                    .OrderBy(c => c.Position.DistanceToSquared(prop.Position));

            Client targetPlayer;

            if ((targetPlayer = players.FirstOrDefault()) != null && targetPlayer.Position.DistanceToSquared(prop.Position) < SYNC_RANGE_SQUARED - DROPOFF_SQUARED)
            {
                StartSync(targetPlayer, handle);
            }
        }

        public void StartSync(Client player, int vehicle)
        {
            var packet = Program.ServerInstance.Server.CreateMessage();
            packet.Write((byte)PacketType.UnoccupiedVehStartStopSync);
            packet.Write(vehicle);
            packet.Write(true);

            Program.ServerInstance.Server.SendMessage(packet, player.NetConnection, NetDeliveryMethod.ReliableUnordered, (int)ConnectionChannel.SyncEvent);
            //Console.WriteLine("[DEBUG MESSAGE] [+] Starting sync for: " + player.Name + " | Vehicle: " + vehicle);

            Syncers.Set(vehicle, player);
        }

        public void StopSync(Client player, int vehicle)
        {
            var packet = Program.ServerInstance.Server.CreateMessage();
            packet.Write((byte)PacketType.UnoccupiedVehStartStopSync);
            packet.Write(vehicle);
            packet.Write(false);

            Program.ServerInstance.Server.SendMessage(packet, player.NetConnection, NetDeliveryMethod.ReliableUnordered, (int)ConnectionChannel.SyncEvent);
            //Console.WriteLine("[DEBUG MESSAGE] [-] Stopping sync for: " + player.Name + " | Vehicle: " + vehicle);

            Syncers.Remove(vehicle);
        }
    }
}