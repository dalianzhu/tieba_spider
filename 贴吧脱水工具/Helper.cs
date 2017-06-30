using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace 贴吧脱水工具
{
    public class Helper
    {
        public static string CutString(string strInput, int length)
        {
            if (strInput.Length > length)
            {
                return strInput.Substring(0, length);
            }
            return strInput;
        }
    }
}
