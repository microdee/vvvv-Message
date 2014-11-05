using System;
using System.Collections.Generic;
using System.IO;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;
using VVVV.Packs.Time;


namespace VVVV.Packs.Message.Core
{
    public class TypeIdentity : Dictionary<Type, string>
    {
        private static TypeIdentity _instance;
        public static TypeIdentity Instance
        {
            get { 
                if (_instance == null) _instance = new TypeIdentity();
                return _instance;
            }
            private set { throw new NotImplementedException(); }
        }

        public TypeIdentity()
	    {
            // This is the only place where you need to add new datatypes.

            Add(typeof(bool), "bool");
            Add(typeof(int), "int");
            Add(typeof(double), "double");
            Add(typeof(float), "float");
            Add(typeof(string), "string");

            Add(typeof(RGBAColor), "Color");
            Add(typeof(Matrix4x4), "Transform");
            Add(typeof(Vector2D), "Vector2d");
            Add(typeof(Vector3D), "Vector3d");
            Add(typeof(Vector4D), "Vector4d");

            Add(typeof(Stream), "Raw");
            Add(typeof(Time.Time), "Time");
            
            Add(typeof(Message), "Message");	        
	    }

        public string FindAlias(Type t)
        {
            foreach (Type key in Keys)
            {
                if (key == t) return this[key];
            }
            return null;
        }

        public Type FindBaseType(Type t)
        {
            foreach (Type key in Keys)
            {
                if (key == t) return key;
                if (t.IsSubclassOf(key)) return key;
            }
            return null;
        }


        public string FindBaseAlias(Type t)
        {
            foreach (Type key in Keys)
            {
                if (key == t) return this[key];
                if (t.IsSubclassOf(key)) return this[key];
            }
            return null;
        }

        public Type FindType(string alias)
        {
            Type type = typeof(string);
            foreach (Type key in this.Keys)
            {
                if (this[key].ToLower() == alias.ToLower())
                {
                    type = key;
                }
            }
            return type;
        }
    }
}
