﻿using CherryMPShared;

namespace CherryMPServer
{
    public abstract class Entity
    {
        internal Entity(ServerAPI father, NetHandle handle)
        {
            Base = father;
            this.handle = handle;
        }

        public NetHandle handle { get; protected set; }

        public int Value
        {
            get { return handle.Value; }
        }

        protected ServerAPI Base { get; set; }

        public static implicit operator NetHandle(Entity c)
        {
            return c.handle;
        }

        public override int GetHashCode()
        {
            return handle.Value;
        }

        public override bool Equals(object obj)
        {
            return (obj as NetHandle?)?.Value == handle.Value;
        }

        public static bool operator ==(Entity left, Entity right)
        {
            if ((object) left == null && (object) right == null) return true;
            if ((object) left == null || (object) right == null) return false;

            return left.handle == right.handle;
        }

        public static bool operator !=(Entity left, Entity right)
        {
            if ((object)left == null && (object)right == null) return false;
            if ((object)left == null || (object)right == null) return true;

            return left.handle != right.handle;
        }

        #region Properties

        public bool freezePosition
        {
            set
            {
                Base.setEntityPositionFrozen(this, value);
            }
        }

        public virtual Vector3 position
        {
            set
            {
                Base.setEntityPosition(this, value);
            }
            get
            {
                return Base.getEntityPosition(this);
            }
        }

        public virtual Vector3 rotation
        {
            set
            {
                Base.setEntityRotation(this, value);
            }
            get
            {
                return Base.getEntityRotation(this);
            }
        }

        public bool IsNull
        {
            get { return handle.IsNull; }
        }

        public bool exists
        {
            get { return Base.doesEntityExist(this); }
        }

        public EntityType type
        {
            get { return Base.getEntityType(this); }
        }

        public virtual int transparency
        {
            set
            {
                Base.setEntityTransparency(this, value);
            }
            get { return Base.getEntityTransparency(this); }
        }

        public int dimension
        {
            set
            {
                Base.setEntityDimension(this, value);
            }
            get { return Base.getEntityDimension(this); }
        }

        public bool invincible
        {
            set
            {
                Base.setEntityInvincible(this, value);
            }
            get { return Base.getEntityInvincible(this); }
        }

        public bool collisionless
        {
            set
            {
                Base.setEntityCollisionless(this, value);
            }
            get { return Base.getEntityCollisionless(this); }
        }

        public int model
        {
            get { return Base.getEntityModel(this); }
        }
        
        #endregion

        #region Methods

        public void delete()
        {
            Base.deleteEntity(this);
        }

        public void movePosition(Vector3 target, int duration)
        {
            Base.moveEntityPosition(this, target, duration);
        }

        public void moveRotation(Vector3 target, int duration)
        {
            Base.moveEntityRotation(this, target, duration);
        }

        public void attachTo(NetHandle entity, string bone, Vector3 offset, Vector3 rotation)
        {
            Base.attachEntityToEntity(this, entity, bone, offset, rotation);
        }

        public void detach()
        {
            Base.detachEntity(this);
        }

        public void detach(bool resetCollision)
        {
            Base.detachEntity(this, resetCollision);
        }

        public void createParticleEffect(string ptfxLib, string ptfxName, Vector3 offset, Vector3 rotation, float scale, int bone = -1)
        {
            Base.createParticleEffectOnEntity(ptfxLib, ptfxName, this, offset, rotation, scale, bone, dimension);
        }

        public void setSyncedData(string key, object value)
        {
            Base.setEntitySyncedData(this, key, value);
        }

        public dynamic getSyncedData(string key)
        {
            return Base.getEntitySyncedData(this, key);
        }

        public void resetSyncedData(string key)
        {
            Base.resetEntitySyncedData(this, key);
        }

        public bool hasSyncedData(string key)
        {
            return Base.hasEntitySyncedData(this, key);
        }

        public void setData(string key, object value)
        {
            Base.setEntityData(this, key, value);
        }

        public dynamic getData(string key)
        {
            return Base.getEntityData(this, key);
        }

        public void resetData(string key)
        {
            Base.resetEntityData(this, key);
        }

        public bool hasData(string key)
        {
            return Base.hasEntityData(this, key);
        }

        #endregion
    }
}