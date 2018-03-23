using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Reflection;

namespace DocoptNet
{
    public class DocoptAliasAttribute
        : Attribute
    {
        public DocoptAliasAttribute(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("empty string is not allowed.");
            }

            this.Name = name;
        }

        public string Name { get; private set; }
    }

    internal class Binder<T> where T: new()
    {
        // Bind populates the public properties of a given class T with matching option values.
        // Each key in args will be mapped to an public property of class T pointed
        // to by 'container', as follows:
        //
        //   private int Abc {get; set;}  // ignored
        //
        //   public int Abc {get; set;}  // mapped from "--abc", "<abc>", or "abc"
        //                               // (case insensitive)
        //   public int A {get; set;}    // mapped from "-a", "<a>", or "a"
        //                               // (case insensitive)
        //   [DocoptMember(Name="Alias")]
        //   public int A {get; set;}    // mapped from "--alias", "<alias>", or "alias"
        //                               // (case insensitive)
        //
        // If a given class has DataContract attribute, DataMember attribute is used 
        // to specify mapped property. Name of DataMember attribute is used to map.
        //
        // [DataContract]
        // class A {
        //     public int Ingnore {get; set;} // ignored
        //     
        //     [DataMember]
        //     public int Abc {get; set;}     // mapped from "--abc", "<abc>" or "abc"
        //                                    // (case insensitive)
        //
        //     [DataMember(Name="Abc")]
        //     public int PropABC {get; set;} // mapped from "--abc", "<abc>" or "abc"
        //                                    // (case insensitive)
        // }
        // Bind also handles conversion to bool, float, int or string types.


        public Binder()
        { }

        public T Bind(IDictionary<string, ValueObject> args)
        {
            var options = args.ToDictionary(x => x.Key.Trim(new[] { '-', '<', '>' }).ToLower(), x => x.Value);
            var container = new T();
            var t = container.GetType();

            // Listing only public properties.
            var properties = new PropertyInfo[0];
            if (Attribute.IsDefined(t, typeof(DataContractAttribute), true))
            {
                properties = t.GetProperties()
                    .Where(p => Attribute.IsDefined(p, typeof(DataMemberAttribute))).ToArray();
            }
            else
            {
                properties = t.GetProperties();
            }

            foreach (var prop in properties)
            {
                if (!prop.CanWrite) continue;

                ValueObject value = null;

                if (Attribute.IsDefined(prop, typeof(DocoptAliasAttribute)))
                {
                    var attr = prop.GetCustomAttributes(typeof(DocoptAliasAttribute), true)
                    .Cast<DocoptAliasAttribute>().First();

                    options.TryGetValue(attr.Name.ToLower(), out value);
                }

                if (Attribute.IsDefined(prop, typeof(DataMemberAttribute)))
                {
                    var attr = prop.GetCustomAttributes(typeof(DataMemberAttribute), true)
                        .Cast<DataMemberAttribute>().First();

                    if (!string.IsNullOrEmpty(attr.Name))
                    {
                        options.TryGetValue(attr.Name.ToLower(), out value);
                    }
                }

                if (value == null)
                {
                    options.TryGetValue(prop.Name.ToLower(), out value);
                }

                if (value != null)
                {
                    Set(prop, value, container);
                }
            }

            return container;
        }
        
        private static void Set(PropertyInfo prop, ValueObject value, object container)
        {

            var t = prop.PropertyType;
            var indexParams = prop.GetIndexParameters();

            if (t.IsArray)
            {
                var values = value.AsList;
                var vtype = t.GetElementType();
                var array = Array.CreateInstance(vtype, values.Count);

                for (int i = 0; i < array.Length; ++i)
                {
                    array.SetValue(Convert.ChangeType(ConvertValue(vtype, values[i]), vtype), i);
                }

                prop.SetValue(container, array, null);
            }
            else
            {
                prop.SetValue(container, ConvertValue(t, value.Value), null);
            }
        }

        private static object ConvertValue(Type t, object value)
        {
            if (t == typeof(bool))
            {
                if (value is bool)
                {
                    return value;
                }
                return bool.Parse(value.ToString());
            }

            if (t == typeof(int))
            {
                if (value is int)
                {
                    return value;
                }
                return int.Parse(value.ToString());
            }

            if (t == typeof(long))
            {
                if (value is long)
                {
                    return value;
                }
                return long.Parse(value.ToString());
            }

            if (t == typeof(string))
            {
                if (value is string)
                {
                    return value;
                }
                return value.ToString();
            }

            if (t == typeof(double))
            {
                if (value is double)
                {
                    return value;
                }
                return double.Parse(value.ToString());
            }

            if (t == typeof(float))
            {
                if (value is float)
                {
                    return value;
                }
                return float.Parse(value.ToString());
            }

            if (t == typeof(decimal))
            {
                if (value is decimal)
                {
                    return value;
                }
                return decimal.Parse(value.ToString());
            }

            return value;
        }
    }
}
