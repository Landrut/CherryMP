using GTA.Math;
using System;

namespace CherryMP.Util
{
    public static class Vector3Extentions
    {
        public static GTA.Math.Quaternion ToQuaternion(this CherryMPShared.Quaternion q)
        {
            return new GTA.Math.Quaternion(q.X, q.Y, q.Z, q.W);
        }

        public static GTA.Math.Vector3 ToVector(this CherryMPShared.Vector3 v)
        {
            if ((object)v == null) return new Vector3();
            return new GTA.Math.Vector3(v.X, v.Y, v.Z);
        }

        public static CherryMPShared.Vector3 ToLVector(this GTA.Math.Vector3 vec)
        {
            return new CherryMPShared.Vector3()
            {
                X = vec.X,
                Y = vec.Y,
                Z = vec.Z,
            };
        }

        public static CherryMPShared.Quaternion ToLQuaternion(this GTA.Math.Quaternion vec)
        {
            return new CherryMPShared.Quaternion()
            {
                X = vec.X,
                Y = vec.Y,
                Z = vec.Z,
                W = vec.W,
            };
        }

        public static float LengthSquared(this CherryMPShared.Vector3 left)
        {
            return left.X * left.X + left.Y * left.Y + left.Z + left.Z;
        }

        public static float Length(this CherryMPShared.Vector3 left)
        {
            return (float)Math.Sqrt(left.LengthSquared());
        }

        public static CherryMPShared.Vector3 Sub(this CherryMPShared.Vector3 left, CherryMPShared.Vector3 right)
        {
            if ((object)left == null && (object)right == null) return new CherryMPShared.Vector3();
            if ((object)left == null) return right;
            if ((object)right == null) return left;
            return new CherryMPShared.Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static CherryMPShared.Vector3 Add(this CherryMPShared.Vector3 left, CherryMPShared.Vector3 right)
        {
            if ((object)left == null && (object)right == null) return new CherryMPShared.Vector3();
            if ((object)left == null) return right;
            if ((object)right == null) return left;
            return new CherryMPShared.Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static Quaternion ToQuaternion(this Vector3 vect)
        {
            vect = new Vector3()
            {
                X = vect.X.Denormalize() * -1,
                Y = vect.Y.Denormalize() - 180f,
                Z = vect.Z.Denormalize() - 180f,
            };

            vect = vect.ToRadians();

            float rollOver2 = vect.Z * 0.5f;
            float sinRollOver2 = (float)Math.Sin((double)rollOver2);
            float cosRollOver2 = (float)Math.Cos((double)rollOver2);
            float pitchOver2 = vect.Y * 0.5f;
            float sinPitchOver2 = (float)Math.Sin((double)pitchOver2);
            float cosPitchOver2 = (float)Math.Cos((double)pitchOver2);
            float yawOver2 = vect.X * 0.5f; // pitch
            float sinYawOver2 = (float)Math.Sin((double)yawOver2);
            float cosYawOver2 = (float)Math.Cos((double)yawOver2);
            Quaternion result = new Quaternion();
            result.X = cosYawOver2 * cosPitchOver2 * cosRollOver2 + sinYawOver2 * sinPitchOver2 * sinRollOver2;
            result.Y = cosYawOver2 * cosPitchOver2 * sinRollOver2 - sinYawOver2 * sinPitchOver2 * cosRollOver2;
            result.Z = cosYawOver2 * sinPitchOver2 * cosRollOver2 + sinYawOver2 * cosPitchOver2 * sinRollOver2;
            result.W = sinYawOver2 * cosPitchOver2 * cosRollOver2 - cosYawOver2 * sinPitchOver2 * sinRollOver2;
            return result;
        }

        public static float ToRadians(this float val)
        {
            return (float)(Math.PI / 180) * val;
        }

        public static Vector3 ToRadians(this Vector3 i)
        {
            return new Vector3()
            {
                X = ToRadians(i.X),
                Y = ToRadians(i.Y),
                Z = ToRadians(i.Z),
            };
        }

        public static Vector3 Denormalize(this Vector3 v)
        {
            return new Vector3(v.X.Denormalize(), v.Y.Denormalize(), v.Z.Denormalize());
        }
    }
}