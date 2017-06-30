using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace 贴吧脱水工具.BLL
{
    public class RemoveHttpStr
    {
        public static string Remove(string strContent)
        {
            strContent = System.Text.RegularExpressions.Regex.Replace(strContent, "<[^>]+>", "");
            strContent = System.Text.RegularExpressions.Regex.Replace(strContent, "&[^;]+;", "");
            return strContent;
        }
    }
}
