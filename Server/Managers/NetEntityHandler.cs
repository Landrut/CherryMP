using System;
using System.Collections.Generic;
using System.Linq;
using CherryMPServer.Constant;
using CherryMPShared;

namespace CherryMPServer
{
    internal class NetEntityHandler
    {
        private int EntityCounter = 1;
        private Dictionary<int, EntityProperties> ServerEntities;

        public bool ReuseIds = false;

        public NetEntityHandler()
        {
            ServerEntities = new Dictionary<int, EntityProperties>();
        }

        public Dictionary<int, EntityProperties> ToDict()
        {
                return ServerEntities;
        }

        public Dictionary<int, EntityProperties> ToCopy()
        {
            lock (ServerEntities)
            {
                return new Dictionary<int, EntityProperties>(ServerEntities);
            }
        }

        public T NetToProp<T>(int handle) where T : EntityProperties
        {
            if (ServerEntities.ContainsKey(handle) && ServerEntities[handle] is T) return (T)ServerEntities[handle];
            return null;
        }

        public void UpdateMovements()
        {
            var copy = ToCopy();

            // Get all entities who are interpolating
            foreach (var pair in copy.Where(pair => pair.Value.PositionMovement != null || pair.Value.RotationMovement != null))
            {
                var currentTime = Program.GetTicks();

                if (pair.Value.PositionMovement != null)
                {
                    var delta = currentTime - pair.Value.PositionMovement.ServerStartTime;
                    pair.Value.PositionMovement.Start = delta;

                    pair.Value.Position = Vector3.Lerp(pair.Value.PositionMovement.StartVector,
                        pair.Value.PositionMovement.EndVector,
                        Math.Min(((float)delta) / pair.Value.PositionMovement.Duration, 1f));

                    if (delta >= pair.Value.PositionMovement.Duration)
                        pair.Value.PositionMovement = null;
                }

                if (pair.Value.RotationMovement != null)
                {
                    var delta = currentTime - pair.Value.RotationMovement.ServerStartTime;
                    pair.Value.RotationMovement.Start = delta;

                    pair.Value.Rotation = Vector3.Lerp(pair.Value.RotationMovement.StartVector,
                        pair.Value.RotationMovement.EndVector,
                        Math.Min(((float)delta) / pair.Value.RotationMovement.Duration, 1f));

                    if (delta >= pair.Value.RotationMovement.Duration)
                        pair.Value.RotationMovement = null;
                }
            }
        }

        public int GetId()
        {
            if (!ReuseIds)
                return ++EntityCounter;

            lock (ServerEntities)
                for (int i = 0; i < Int32.MaxValue; i++)
                    if (!ServerEntities.ContainsKey(i))
                    {
                        if (i >= Int32.MaxValue - 100)
                            ReuseIds = true;

                        return i;
                    }

            return ++EntityCounter;
        }

        private bool _hasWorldBeenCreated;
        public void CreateWorld()
        {
            if (_hasWorldBeenCreated) return;

            var obj = new WorldProperties
            {
                EntityType = 255,
                Hours = (byte)DateTime.Now.Hour,
                Minutes = (byte)DateTime.Now.Minute,
                Weather = 0,
                LoadedIpl = new List<string>(),
                RemovedIpl = new List<string>()
            };

            lock (ServerEntities) ServerEntities.Add(1, obj);
            _hasWorldBeenCreated = true;
        }

        public int CreateVehicle(int model, Vector3 pos, Vector3 rot, int color1, int color2, int dimension)
        {
            var obj = new VehicleProperties
            {
                Position = pos,
                Rotation = rot,
                ModelHash = model,
                IsDead = false,
                Health = 1000,
                Alpha = 255,
                Livery = 0,
                NumberPlate = "Cherry",
                EntityType = (byte)EntityType.Vehicle,
                PrimaryColor = color1,
                SecondaryColor = color2,
                Dimension = dimension
            };

            if (model == (int)VehicleHash.Taxi)
                obj.VehicleComponents = 1 << 5;
            else if (model == (int) VehicleHash.Police)
                obj.VehicleComponents = 1 << 2;
            else if (model == (int) VehicleHash.Skylift)
                obj.VehicleComponents = -1537;
            else
                obj.VehicleComponents = ~0;


            int localEntityHash;
            lock (ServerEntities)
            {
                localEntityHash = GetId();
                ServerEntities.Add(localEntityHash, obj);
            }

            var packet = new CreateEntity
            {
                EntityType = (byte)EntityType.Vehicle,
                NetHandle = localEntityHash,
                Properties = obj
            };
            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateProp(int model, Vector3 pos, Vector3 rot, int dimension)
        {
            var obj = new EntityProperties
            {
                Position = pos,
                Rotation = rot,
                ModelHash = model,
                Dimension = dimension,
                Alpha = 255,
                EntityType = (byte)EntityType.Prop
            };
            int localEntityHash;

            lock (ServerEntities)
            {
                localEntityHash = GetId();
                ServerEntities.Add(localEntityHash, obj);
            }

            var packet = new CreateEntity
            {
                EntityType = (byte)EntityType.Prop,
                Properties = obj,
                NetHandle = localEntityHash
            };

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateProp(int model, Vector3 pos, Quaternion rot, int dimension)
        {
            var obj = new EntityProperties
            {
                Position = pos,
                Rotation = rot,
                ModelHash = model,
                Dimension = dimension,
                Alpha = 255,
                EntityType = (byte)EntityType.Prop
            };

            int localEntityHash;
            lock (ServerEntities)
            {
                localEntityHash = GetId();
                ServerEntities.Add(localEntityHash, obj);
            }

            var packet = new CreateEntity
            {
                EntityType = (byte)EntityType.Prop,
                Properties = obj,
                NetHandle = localEntityHash
            };

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreatePickup(int model, Vector3 pos, Vector3 rot, int amount, uint respawnTime, int dimension, int customModel = 0)
        {
            int localEntityHash;
            var obj = new PickupProperties
            {
                Position = pos,
                Rotation = rot,
                ModelHash = model,
                RespawnTime = respawnTime,
                Amount = amount,
                Dimension = dimension,
                Alpha = 255,
                Flag = 0,
                CustomModel = customModel,
                EntityType = (byte)EntityType.Pickup
            };

            lock (ServerEntities)
            {
                localEntityHash = GetId();
                ServerEntities.Add(localEntityHash, obj);
            }

            var packet = new CreateEntity
            {
                EntityType = (byte)EntityType.Pickup,
                Properties = obj,
                NetHandle = localEntityHash
            };

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateBlip(NetHandle ent)
        {
            if (ent.IsNull || !ent.Exists()) return 0;

            int localEntityHash;
            var obj = new BlipProperties
            {
                EntityType = (byte)EntityType.Blip,
                AttachedNetEntity = ent.Value,
                Dimension = ServerEntities[ent.Value].Dimension,
                Position = ServerEntities[ent.Value].Position,
                Sprite = 0,
                Alpha = 255,
                Scale = 1f
            };
            lock (ServerEntities)
            {
                localEntityHash = GetId();
                ServerEntities.Add(localEntityHash, obj);
            }

            var packet = new CreateEntity
            {
                EntityType = (byte)EntityType.Blip,
                Properties = obj,
                NetHandle = localEntityHash
            };

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateBlip(Vector3 pos, int dimension)
        {
            int localEntityHash;
            var obj = new BlipProperties
            {
                EntityType = (byte)EntityType.Blip,
                Position = pos,
                Dimension = dimension,
                Sprite = 0,
                Scale = 1f,
                Alpha = 255,
                AttachedNetEntity = 0
            };
            lock (ServerEntities)
            {
                localEntityHash = GetId();
                ServerEntities.Add(localEntityHash, obj);
            }

            var packet = new CreateEntity
            {
                EntityType = (byte)EntityType.Blip,
                Properties = obj,
                NetHandle = localEntityHash
            };

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateBlip(Vector3 pos, float range, int dimension)
        {
            int localEntityHash;
            var obj = new BlipProperties
            {
                EntityType = (byte)EntityType.Blip,
                Position = pos,
                Dimension = dimension,
                Sprite = 0,
                Scale = 1f,
                RangedBlip = range,
                Alpha = 255,
                AttachedNetEntity = 0
            };
            lock (ServerEntities)
            {
                localEntityHash = GetId();
                ServerEntities.Add(localEntityHash, obj);
            }

            var packet = new CreateEntity
            {
                EntityType = (byte)EntityType.Blip,
                Properties = obj,
                NetHandle = localEntityHash
            };

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateMarker(int markerType, Vector3 pos, Vector3 dir, Vector3 rot, Vector3 scale, int alpha, int r, int g, int b, int dimension)
        {
            int localEntityHash;
            
            var obj = new MarkerProperties()
            {
                MarkerType = markerType,
                Position = pos,
                Direction = dir,
                Rotation = rot,
                Scale = scale,
                Alpha = (byte) alpha,
                Red = r,
                Green = g,
                Blue = b,
                Dimension = dimension,
                EntityType = (byte) EntityType.Marker,
            };

            lock (ServerEntities)
            {
                localEntityHash = GetId();
                ServerEntities.Add(localEntityHash, obj);
            }

            var packet = new CreateEntity
            {
                EntityType = (byte)EntityType.Marker,
                Properties = obj,
                NetHandle = localEntityHash
            };

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateTextLabel(string text, float size, float range, int r, int g, int b, Vector3 pos, bool entitySeethrough, int dimension)
        {
            int localEntityHash;
            var obj = new TextLabelProperties
            {
                EntityType = (byte)EntityType.TextLabel,
                Position = pos,
                Size = size,
                Blue = b,
                Green = g,
                Range = range,
                Red = r,
                Text = text,
                Alpha = 255,
                EntitySeethrough = entitySeethrough,
                Dimension = dimension
            };
            lock (ServerEntities)
            {
                localEntityHash = GetId();
                ServerEntities.Add(localEntityHash, obj);
            }

            var packet = new CreateEntity
            {
                EntityType = (byte)EntityType.TextLabel,
                Properties = obj,
                NetHandle = localEntityHash
            };

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateStaticPed(int model, Vector3 pos, float heading, int dimension = 0)
        {
            int localEntityHash;
            var obj = new PedProperties
            {
                EntityType = (byte)EntityType.Ped,
                Position = pos,
                Alpha = 255,
                ModelHash = model,
                Rotation = new Vector3(0, 0, heading),
                Dimension = dimension
            };
            lock (ServerEntities)
            {
                localEntityHash = GetId();
                ServerEntities.Add(localEntityHash, obj);
            }

            var packet = new CreateEntity
            {
                EntityType = (byte)EntityType.Ped,
                Properties = obj,
                NetHandle = localEntityHash
            };

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public int CreateParticleEffect(string lib, string name, Vector3 pos, Vector3 rot, float scale, int attachedEntity = 0, int boneId = 0, int dimension = 0)
        {
            int localEntityHash;
            var obj = new ParticleProperties
            {
                EntityType = (byte)EntityType.Particle,
                Position = pos,
                Rotation = rot,
                Alpha = 255,
                Library = lib,
                Name = name,
                Scale = scale,
                EntityAttached = attachedEntity,
                BoneAttached = boneId,
                Dimension = dimension
            };

            lock (ServerEntities)
            {
                localEntityHash = GetId();
                ServerEntities.Add(localEntityHash, obj);
            }

            var packet = new CreateEntity
            {
                EntityType = (byte)EntityType.Particle,
                Properties = obj,
                NetHandle = localEntityHash
            };

            Program.ServerInstance.SendToAll(packet, PacketType.CreateEntity, true, ConnectionChannel.NativeCall);

            return localEntityHash;
        }

        public void DeleteEntity(int netId)
        {
            if (!ServerEntities.ContainsKey(netId)) return;

            var packet = new DeleteEntity { NetHandle = netId };
            Program.ServerInstance.SendToAll(packet, PacketType.DeleteEntity, true, ConnectionChannel.EntityBackend);

            lock (ServerEntities) ServerEntities.Remove(netId);
        }

        public void DeleteEntityQuiet(int netId)
        {
            lock (ServerEntities) ServerEntities.Remove(netId);
        }

        public int GeneratePedHandle()
        {
            int localHan;

            lock (ServerEntities)
            {
                localHan = GetId();

                ServerEntities.Add(localHan, new PlayerProperties()
                {
                    EntityType = (byte) EntityType.Player,
                    BlipSprite = 1,
                    ModelHash = (int) PedHash.Clown01SMY,
                    BlipAlpha = 255,
                    Alpha = 255,
                });
            }

            return localHan;
        }
    }

    internal static class NetHandleExtension
    {
        internal static bool Exists(this NetHandle ent)
        {
            return !ent.IsNull && Program.ServerInstance.NetEntityHandler.ToDict().ContainsKey(ent.Value);
        }
    }
}