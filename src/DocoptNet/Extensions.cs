using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DocoptNet
{
    internal static class Extensions
    {
        public static T Bind<T>(this IDictionary<string, ValueObject>args) where T: new()
        {
            return new Binder<T>().Bind(args);
        }
    }
}
