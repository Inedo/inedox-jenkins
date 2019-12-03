using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inedo.Extensions.Jenkins
{
    public static class Message
    {
        public static string IfHasValue(string value, string msg)
        {
            if (String.IsNullOrEmpty(value))
                return String.Empty;

            return msg;
        }

        public static object IfHasValue(string value, object msg)
        {
            if (String.IsNullOrEmpty(value))
                return null;

            return msg;
        }

        public static object[] IfHasValue(string value, params object[] msg)
        {
            if (String.IsNullOrEmpty(value))
                return null;

            return msg;
        }
    }
}
