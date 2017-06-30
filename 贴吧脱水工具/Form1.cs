using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using 贴吧脱水工具.BLL;
using System.Net;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading;
using System.Collections;
using System.Web;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Threading.Tasks;

namespace 贴吧脱水工具
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        public delegate void SetTextDelegate(string strText);
        public void SetTextbox2Text(string text)
        {
            if (this.textBox2.InvokeRequired && this.button1.InvokeRequired)
            {
                SetTextDelegate std = new SetTextDelegate(SetTextbox2Text);
                this.BeginInvoke(std, new object[] { text });
                return;
            }
            this.textBox2.Text = text;
            this.button1.Enabled = true;
            this.comboBox1.Enabled = true;
        }

        delegate void DeleSetProcessBar(object sender, ProcessBarEventArgs e);

        void SetProcessBar(object sender, ProcessBarEventArgs e)
        {
            if (this.progressBar1.InvokeRequired)
            {
                DeleSetProcessBar dsb = new DeleSetProcessBar(SetProcessBar);
                this.progressBar1.Invoke(dsb, new object[] { sender, e });
                return;
            }
            this.progressBar1.Value = e.InCurProcess;
        }


        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                this.button1.Enabled = false;
                this.comboBox1.Enabled = false;
                GetArticle ga = null;

                ArrayList pageInfo;
                switch (this.comboBox1.Text)
                {
                    case "百度":
                        ga = new BaiDuArticle();
                        ga.pageUrl = this.textBox1.Text;
                        pageInfo = ga.GetPagesInfo();
                        ga.LzName = "";
                        ga.pageNum = Convert.ToInt32(pageInfo[0]);
                        ga.ArticleName = pageInfo[1].ToString();
                        ga.SetTextEventHander += SetProcessBar;
                        break;
                    case "天涯":
                        ga = new TianYaArticle();
                        ga.pageUrl = this.textBox1.Text;
                        pageInfo = ga.GetPagesInfo();
                        ga.LzName = pageInfo[2].ToString();
                        ga.pageNum = Convert.ToInt32(pageInfo[0]);
                        ga.ArticleName = pageInfo[1].ToString();
                        ga.SetTextEventHander += SetProcessBar;
                        break;
                    case "百度图解":
                        ga = new BaiDuHtmlArticle();
                        ga.pageUrl = this.textBox1.Text;
                        pageInfo = ga.GetPagesInfo();
                        ga.LzName = "";
                        ga.pageNum = Convert.ToInt32(pageInfo[0]);
                        ga.ArticleName = pageInfo[1].ToString();
                        ga.SetTextEventHander += SetProcessBar;
                        break;
                    default:
                        break;
                }

                this.textBox2.Text = "文章：" + ga.pageNum + "页，标题：" + ga.ArticleName + "\r\n正在开始生成内容。时间正在流逝。。。为什么不夸夸作者，写长长的信，做点有意义的事^_^。下面插播一条广告：作者实在太帅了！";
                string comboxselect = this.comboBox1.Text;
                Thread thdGetAllContent = new Thread(new ThreadStart(delegate
                {
                    string content = ga.GetAllContent();
                    SetTextbox2Text(content);
                    if (comboxselect == "百度图解")
                    {
                        WriteInTxt(content, ga.ArticleName + ".html");

                        // 定义正则表达式用来匹配 img 标签
                        Regex regImg = new Regex(@"<img\b[^<>]*?\bsrc[\s\t\r\n]*=[\s\t\r\n]*[""']?[\s\t\r\n]*(?<imgUrl>[^\s\t\r\n""'<>]*)[^<>]*?/?[\s\t\r\n]*>", RegexOptions.IgnoreCase);

                        // 搜索匹配的图片
                        MatchCollection imgMatches = regImg.Matches(content);

                        foreach (Match match in imgMatches)
                        {
                            content = content.Replace(match.Value, "[#]" + match.Value + "[#]");//把图片和文字隔离
                        }

                        Regex regSplitContent = new Regex(@"(?<=\[#\])[\s\S]+?(?=\[#\])", RegexOptions.IgnoreCase);

                        // 搜索匹配的字符串，处理好之后的文本开始提取内容，保存为内容数组传入PDF生成函数
                        MatchCollection pdfMC = regSplitContent.Matches("[#]" + content + "[#]");

                        string[] pdfArray = new string[pdfMC.Count];

                        for (int i = 0; i < pdfArray.Length; i++)
                        {
                            pdfArray[i] = pdfMC[i].Value;
                        }

                        WritePDF(pdfArray, ga.ArticleName);
                    }
                    else
                    {
                        WriteInTxt(content, ga.ArticleName + ".txt");
                    }

                    //SendError(content);

                }));

                thdGetAllContent.Start();
            }
            catch (Exception ex)
            {
                this.button1.Enabled = true;
                MessageBox.Show("出错鸟呀出错鸟，快快检查下。要是还不行，肯定是度娘更新鸟。坐等程序猿跟进吧。" + ex.Message);
            }
        }

        private void SendError(string content)
        {
            if (content.Length > 5120)
            {
                return;
            }

            content = RemoveHttpStr.Remove(content.Replace(" ", "").Replace("[#]", ""));
            //工作完毕，如果发现抓取的字符数过少，则有可能是百度改版了，弹出是否上报消息的窗口
            if (string.IsNullOrEmpty(content) || content.Length < 10)
            {
                if (DialogResult.OK == MessageBox.Show("这篇帖子的内容小于10字符，怀疑出错，是否上报链接供作者分析处理？", "上报错误", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning))
                {
                    Dictionary<string, string> postParameters = new Dictionary<string, string>();
                    postParameters.Add("errText", "错误：" + this.textBox1.Text);
                    try
                    {
                        HttpWebResponse response =
                       HttpWebResponseUtility
                       .CreatePostHttpResponse("http://www.yinzihao.com.cn/NetArticle/FeedBack", postParameters, 2200, null, Encoding.UTF8, null);

                        System.IO.StreamReader reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8);
                        string srcString = reader.ReadToEnd();

                        MessageBox.Show(srcString);
                    }
                    catch
                    {
                        MessageBox.Show("上报失败，超时错误，只能先这样了。。");
                    }

                }
            }
        }


        private void WriteInTxt(string content, string articleTitle)
        {
            try
            {
                //Pass the filepath and filename to the StreamWriter Constructor
                StreamWriter sw = new StreamWriter(".\\" + articleTitle, false, Encoding.Default);

                //Write a line of text
                sw.Write(content.ToString());

                sw.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception: " + ex.Message);
            }
            finally
            {
                MessageBox.Show("文章已生成为txt.");
            }
        }
        private void WritePDF(string[] content, string articleTitle)
        {
            iTextSharp.text.Document doc = new Document(PageSize.A4);

            //写实例 
            PdfWriter.GetInstance(doc, new FileStream(".\\" + articleTitle + ".pdf", FileMode.Create));
            #region 设置PDF的头信息，一些属性设置，在Document.Open 之前完成
            doc.AddAuthor("作者幻想Zerow");
            doc.AddCreationDate();
            doc.AddCreator("创建人幻想Zerow");
            doc.AddSubject("Dot Net 使用 itextsharp 类库创建PDF文件的例子");
            doc.AddTitle("此PDF由幻想Zerow创建，嘿嘿");
            doc.AddKeywords("ASP.NET,PDF,iTextSharp,幻想Zerow");
            //自定义头 
            doc.AddHeader("Expires", "0");
            #endregion //打开document
            doc.Open();
            //载入字体 
            //要在PDF文档中写入中文必须指定中文字体，否则无法写入中文  
            BaseFont bftitle = BaseFont.CreateFont("C:\\Windows\\Fonts\\SIMHEI.TTF",
                BaseFont.IDENTITY_H, BaseFont.NOT_EMBEDDED);   //用系统中的字体文件SimHei.ttf创建文件字体  
            iTextSharp.text.Font fonttitle = new iTextSharp.text.Font(bftitle, 30);     //标题字体，大小30  
            BaseFont bf1 = BaseFont.CreateFont("C:\\Windows\\Fonts\\SIMSUN.TTC,1",
                BaseFont.IDENTITY_H, BaseFont.NOT_EMBEDDED);     //用系统中的字体文件SimSun.ttc创建文件字体  
            iTextSharp.text.Font CellFont = new iTextSharp.text.Font(bf1, 12);          //单元格中的字体，大小12  
            iTextSharp.text.Font fonttitle2 = new iTextSharp.text.Font(bf1, 15);        //副标题字体，大小15  

            foreach (string str in content)
            {
                Regex regImg = new Regex(@"<img\b[^<>]*?\bsrc[\s\t\r\n]*=[\s\t\r\n]*[""']?[\s\t\r\n]*(?<imgUrl>[^\t\r\n""'<>]*)[^<>]*?/?[\s\t\r\n]*>", RegexOptions.IgnoreCase);
                if (regImg.IsMatch(str))
                {
                    Match mc = regImg.Match(str);
                    string imgPath = mc.Groups["imgUrl"].Value;
                    PDFAddPic(ref doc, imgPath);
                }
                else
                {
                    doc.Add(new Paragraph(BLL.RemoveHttpStr.Remove(str.Replace("<br>", "\r\n")).Replace("[#]", ""), CellFont));
                }

            }

            // doc.Add(new Paragraph("您好， PDF !", CellFont));


            //关闭document 
            doc.Close();
        }
        void PDFAddPic(ref Document doc, string picPath)
        {
            try
            {
                iTextSharp.text.Image jpg = iTextSharp.text.Image.GetInstance(picPath);
                if (jpg.Width > 450f)
                {
                    jpg.ScaleToFit(450f, 450f);

                    jpg.Border = iTextSharp.text.Rectangle.BOX;

                    jpg.BorderColor = BaseColor.BLUE;

                    jpg.BorderWidth = 5f;
                }


                doc.Add(jpg);
            }
            catch
            {

            }
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            this.textBox1.Text = "";
            this.textBox2.AppendText("百度链接示例：http://tieba.baidu.com/p/2945350117 \r\n");
            this.textBox2.AppendText("时间大概在半分钟到5分钟不等，此期间请少安毋躁做些其它的事 \r\n\r\n");
            this.textBox2.AppendText("本人主页 http://www.superpig.win\r\n程序页：http://www.superpig.win/blog/details/566\r\n如项目不能用，请到此鞭打我吧 :)");

            this.comboBox1.Text = "百度";
        }
        private void button2_Click(object sender, EventArgs e)
        {
            Bitmap img = null;
            HttpWebRequest req;
            HttpWebResponse res = null;
            try
            {
                System.Uri httpUrl = new System.Uri("http://hiphotos.baidu.com/%CF%C4%D6%C1%B3%F5%C4%A9_/pic/item/6f15b65a0fc6c7f29d82043d.jpg");
                req = (HttpWebRequest)(WebRequest.Create(httpUrl));
                req.Timeout = 18000; //设置超时值10秒
                //req.UserAgent = "XXXXX";
                //req.Accept = "XXXXXX";
                req.Method = "GET";
                res = (HttpWebResponse)(req.GetResponse());
                img = new Bitmap(res.GetResponseStream());//获取图片流                
                img.Save(@"d:/" + DateTime.Now.ToFileTime().ToString() + ".png");//随机名
            }

            catch (Exception ex)
            {
                string aa = ex.Message;
            }
            finally
            {
                res.Close();
            }

        }


    }

    public abstract class GetArticle
    {
        private string m_lzName;

        public string LzName
        {
            get { return m_lzName; }
            set { m_lzName = value; }
        }

        public string pageUrl;

        public int pageNum;

        private string articleName;

        public string ArticleName
        {
            get { return Helper.CutString(articleName, 64); }
            set { articleName = value; }
        }

        /// <summary>
        /// 获取所有的内容
        /// </summary>
        /// <param name="pageNum">总页码</param>
        /// <param name="pageUrl">帖子的地址</param>
        /// <returns>所有的内容</returns>
        public abstract string GetAllContent();
        /// <summary>
        /// 获取某一页的内容
        /// </summary>
        /// <param name="PageUrl">该页的网址</param>
        /// <returns>该页的内容</returns>
        public abstract string GetPageContent(string curPageUrl);
        /// <summary>
        /// 获取文章的信息
        /// </summary>
        /// <returns>信息列表，比如楼主，页码</returns>
        public abstract ArrayList GetPagesInfo();

        public EventHandler<ProcessBarEventArgs> SetTextEventHander;
        public void OnSetTextEventHander(ProcessBarEventArgs e)
        {
            if (SetTextEventHander != null)
            {
                SetTextEventHander(this, e);
            }
        }
    }

    public class BaiDuArticle : GetArticle
    {
        private int curPageNum = 0;
        public override string GetAllContent()
        {
            pageUrl = pageUrl + "?see_lz=1&pn=";
            StringBuilder content = new StringBuilder();
            for (int i = 1; i <= pageNum; i++)
            {
                content.Append(GetPageContent(pageUrl + (i).ToString()));
            }
            return content.ToString();
        }

        public override string GetPageContent(string curPageUrl)
        {
            try
            {
                string result = "";
                HttpWebResponse response = HttpWebResponseUtility.CreatePostHttpResponse(curPageUrl, null, null, null, Encoding.Default, null);

                System.IO.StreamReader reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8);
                string srcString = reader.ReadToEnd();

                var mc = BaiDuMcSelector.SelectMatchCollection(srcString);

                foreach (Match ma in mc)
                {
                    result += "\r\n" + BLL.RemoveHttpStr.Remove(ma.Value.Replace("<br>", "\r\n")).Trim();
                }
                curPageNum++;
                ProcessBarEventArgs tea = new ProcessBarEventArgs(curPageNum * 100 / pageNum);
                this.OnSetTextEventHander(tea);
                return result;
            }
            catch
            {
                return "";
            }
        }

 
        public override ArrayList GetPagesInfo()
        {
            HttpWebResponse response = HttpWebResponseUtility.CreatePostHttpResponse(pageUrl + "?see_lz=1&pn=1", null, null, null, Encoding.Default, null);

            System.IO.StreamReader reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8);
            string srcString = reader.ReadToEnd();

            string temp = "";
            Regex re = new Regex("<li class=\"l_reply_num\" style=\"margin-left:.*\">.*</li>", RegexOptions.IgnoreCase);
            MatchCollection mc = re.Matches(srcString);
            foreach (Match ma in mc)
            {
                temp += BLL.RemoveHttpStr.Remove(ma.Value);
            }
            string pageNum = temp.Substring(temp.IndexOf("共") + 1, temp.IndexOf("页") - temp.IndexOf("共") - 1);

            temp = "";
            Regex nameTitle = new Regex("<title>.*</title>", RegexOptions.IgnoreCase);

            MatchCollection nc = nameTitle.Matches(srcString);
            foreach (Match na in nc)
            {
                temp += BLL.RemoveHttpStr.Remove(na.Value).Trim();
            }

            //MessageBox.Show(temp);

            ArrayList result = new ArrayList();
            result.Add(pageNum);
            result.Add(temp);
            return result;
        }
    }

    public class BaiDuMcSelector
    {
        public static MatchCollection SelectMatchCollection(string srcString)
        {
            string type1 = "<div id=\"post_content_[0-9]*\" class=\"d_post_content j_d_post_content \">((?!</?div[^>]*>).|\n)*(((?'TAG'<div[^>]*>)((?!</?div[^>]*>).|\n)*)+((?'-TAG'</div>)((?!</?div[^>]*>).|\n)*)+)*(?(TAG)(?!))</div>";
            string type2 = "<div id=\"post_content_[0-9]*\" class=\"d_post_content j_d_post_content\\s*clearfix\">((?!</?div[^>]*>).|\n)*(((?'TAG'<div[^>]*>)((?!</?div[^>]*>).|\n)*)+((?'-TAG'</div>)((?!</?div[^>]*>).|\n)*)+)*(?(TAG)(?!))</div>";

            var list = new List<string>();
            list.Add(type1);
            list.Add(type2);
            MatchCollection mc = null;
            foreach (var item in list)
            {
                Regex re = new Regex(item, RegexOptions.None);
                mc = re.Matches(srcString);
                if (mc.Count > 0)
                {
                    return mc;
                }
            }

            return mc;
        }

    }
    public class TianYaArticle : GetArticle
    {
        public override string GetAllContent()
        {
            //http://bbs.tianya.cn/post-16-990888-1.shtml#ty_vip_look[cszmj2009]
            string firPageUrl = pageUrl.Substring(0, pageUrl.LastIndexOf("-") + 1);
            string endPageUrl = ".shtml";
            //+ "#ty_vip_look[" + HttpUtility.HtmlEncode(lzname) + "]";
            // MessageBox.Show(firPageUrl + "1" + endPageUrl);
            StringBuilder content = new StringBuilder();
            for (int i = 1; i <= pageNum; i++)
            {
                content.Append(GetPageContent(firPageUrl + i.ToString() + endPageUrl));
            }
            return content.ToString();
        }
        int curPageNum = 0;
        public override string GetPageContent(string curPageUrl)
        {
            try
            {
                Thread.Sleep(1000);
                string result = "";
                HttpWebResponse response = HttpWebResponseUtility.CreatePostHttpResponse(curPageUrl, null, null, null, Encoding.UTF8, null);

                System.IO.StreamReader reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8);
                string srcString = reader.ReadToEnd();
                string encodeLzName = HttpUtility.UrlEncode(this.LzName).ToUpper();
                Regex re = new Regex("<div (class=\"atl-item\" _host=\"" + encodeLzName + "\" id=\"[0-9]*?\" (replyid=\"[0-9]*?\")? js_username=\"" + encodeLzName + "\" js_resTime=\"[\\s\\S]*?\"|class=\"atl-item\" _host=\"" + encodeLzName + "\")>((?!</?div[^>]*>).|\n)*(((?'TAG'<div[^>]*>)((?!</?div[^>]*>).|\n)*)+((?'-TAG'</div>)((?!</?div[^>]*>).|\n)*)+)*(?(TAG)(?!))</div>", RegexOptions.IgnoreCase);
                //"<div class=(\"bbs-content clearfix\"|\"bbs-content\")>((?!</?div[^>]*>).|\n)*(((?'TAG'<div[^>]*>)((?!</?div[^>]*>).|\n)*)+((?'-TAG'</div>)((?!</?div[^>]*>).|\n)*)+)*(?(TAG)(?!))</div>

                MatchCollection mc = re.Matches(srcString);
                string temp = "";
                foreach (Match ma in mc)
                {
                    temp = ma.Value;
                    // result += "\r\n" + BLL.RemoveHttpStr.Remove(ma.Value.Replace("<br>", "\r\n")).Trim();
                    Match m = Regex.Match(temp, "<div class=(\"bbs-content clearfix\"|\"bbs-content\")>([\\s\\S](?!<div))*</div>", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        result += "\r\n" + BLL.RemoveHttpStr.Remove(m.Value.Replace("<br>", "\r\n")).Trim();
                    }
                }
                curPageNum++;
                ProcessBarEventArgs tea = new ProcessBarEventArgs(curPageNum * 100 / pageNum);
                this.OnSetTextEventHander(tea);
                return result;
            }
            catch
            {
                return "";
            }
        }

        public override ArrayList GetPagesInfo()
        {
            HttpWebResponse response = HttpWebResponseUtility.CreatePostHttpResponse(this.pageUrl, null, null, null, Encoding.UTF8, null);

            System.IO.StreamReader reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8);
            string srcString = reader.ReadToEnd();

            string temp = "";

            string pageNum = "";
            Regex re = new Regex("<div class=\"atl-pages\">[\\s\\S]+?</div>", RegexOptions.None);
            MatchCollection mc = re.Matches(srcString);

            foreach (Match ma in mc)
            {
                temp += BLL.RemoveHttpStr.Remove(ma.Value);
            }

            Regex getPageNum = new Regex("\n[0-9]+", RegexOptions.None);
            MatchCollection getPageNumMc = getPageNum.Matches(temp);

            foreach (Match ma in getPageNumMc)
            {
                pageNum = ma.Value;
            }

            pageNum = pageNum != "" ? pageNum.Replace("\n", "") : "1";
            //MessageBox.Show(pageNum);
            temp = "";
            Regex nameTitle = new Regex("<span class=\"s_title\"><span style=\"font-weight:400;\">.*?</span></span>", RegexOptions.None);
            MatchCollection nc = nameTitle.Matches(srcString);
            foreach (Match na in nc)
            {
                temp += BLL.RemoveHttpStr.Remove(na.Value).Trim();
            }
            string articleTitle = temp;
            //MessageBox.Show(temp);

            Regex nameLZ = new Regex("<div class=\"atl-info\">[\\s\\S]+?</div>", RegexOptions.None);
            MatchCollection lzmc = nameLZ.Matches(srcString);
            foreach (Match na in lzmc)
            {
                temp += BLL.RemoveHttpStr.Remove(na.Value).Trim();
            }
            string lzname = temp.Substring(temp.IndexOf("楼主：") + 3, temp.IndexOf("时间") - temp.IndexOf("楼主：") - 3)
                .Replace("\r", "").Replace("\t", "").Replace("\n", "");
            //MessageBox.Show(lzname);
            ArrayList result = new ArrayList();
            result.Add(pageNum);
            result.Add(articleTitle);
            result.Add(lzname);
            return result;
        }
    }

    public class BaiDuHtmlArticle : GetArticle
    {
        private int curPageNum = 0;
        public override string GetAllContent()
        {
            pageUrl = pageUrl + "?see_lz=1&pn=";
            StringBuilder content = new StringBuilder();
            for (int i = 1; i <= pageNum; i++)
            {
                content.Append(GetPageContent(pageUrl + (i).ToString()));
            }
            return content.ToString();
        }

        public override string GetPageContent(string curPageUrl)
        {
            try
            {
                string result = "";
                HttpWebResponse response = HttpWebResponseUtility.CreatePostHttpResponse(curPageUrl, null, null, null, Encoding.Default, null);

                System.IO.StreamReader reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8);
                string srcString = reader.ReadToEnd();

                var mc = BaiDuMcSelector.SelectMatchCollection(srcString);

                foreach (Match ma in mc)
                {
                    result += ma.Value + "<br><br>";

                    //   result += "\r\n" + BLL.RemoveHttpStr.Remove(ma.Value.Replace("<br>", "\r\n")).Trim();
                }

                result = GetPageStrContainsPic(result);
                curPageNum++;
                ProcessBarEventArgs tea = new ProcessBarEventArgs(curPageNum * 100 / pageNum);
                this.OnSetTextEventHander(tea);
                return result;
            }
            catch
            {
                return "";
            }
        }

        public string GetPageStrContainsPic(string pageHtml)
        {
            string[] picUrlList = GetHtmlImageUrlList(pageHtml);

            foreach (string str in picUrlList)
            {
                Thread.Sleep(500);
                string picName = str.Substring(str.LastIndexOf("/") + 1);
                //Task.Factory.StartNew(() =>
                //{
                saveimage(str, "./" + ArticleName + "/" + picName);
                //});

                pageHtml = pageHtml.Replace(str, "./" + ArticleName + "/" + picName);
            }
            return pageHtml;
        }

        public void saveimage(string url, string path)
        {
            Bitmap img = null;
            HttpWebRequest req;
            HttpWebResponse res = null;
            try
            {
                System.Uri httpUrl = new System.Uri(url);
                req = (HttpWebRequest)(WebRequest.Create(httpUrl));
                req.Timeout = 180000; //设置超时值10秒
                req.UserAgent = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 5.1; Trident/4.0; .NET CLR 1.1.4322; .NET CLR 2.0.50727; InfoPath.3; .NET CLR 3.0.4506.2152; .NET CLR 3.5.30729; .NET4.0C; .NET4.0E)";
                req.Accept = "*/*";
                req.Method = "GET";
                res = (HttpWebResponse)(req.GetResponse());
                img = new Bitmap(res.GetResponseStream());//获取图片流                
                img.Save(path);//随机名
            }
            catch (Exception ex)
            {
                string aa = ex.Message;
            }
            finally
            {
                res.Close();
            }

        }

        public static string[] GetHtmlImageUrlList(string sHtmlText)
        {
            // 定义正则表达式用来匹配 img 标签
            Regex regImg = new Regex(@"<img\b[^<>]*?\bsrc[\s\t\r\n]*=[\s\t\r\n]*[""']?[\s\t\r\n]*(?<imgUrl>[^\s\t\r\n""'<>]*)[^<>]*?/?[\s\t\r\n]*>", RegexOptions.IgnoreCase);

            // 搜索匹配的字符串
            MatchCollection matches = regImg.Matches(sHtmlText);

            int i = 0;
            string[] sUrlList = new string[matches.Count];

            // 取得匹配项列表
            foreach (Match match in matches)
                sUrlList[i++] = match.Groups["imgUrl"].Value;

            return sUrlList;
        }

        public override ArrayList GetPagesInfo()
        {
            HttpWebResponse response = HttpWebResponseUtility.CreatePostHttpResponse(pageUrl + "?see_lz=1&pn=1", null, null, null, Encoding.Default, null);

            System.IO.StreamReader reader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8);
            string srcString = reader.ReadToEnd();

            string temp = "";
            Regex re = new Regex("<li class=\"l_reply_num\" style=\"margin-left:.*\">.*</li>", RegexOptions.IgnoreCase);
            MatchCollection mc = re.Matches(srcString);
            foreach (Match ma in mc)
            {
                temp += BLL.RemoveHttpStr.Remove(ma.Value);
            }
            string pageNum = temp.Substring(temp.IndexOf("共") + 1, temp.IndexOf("页") - temp.IndexOf("共") - 1);

            temp = "";
            Regex nameTitle = new Regex("<title>.*</title>", RegexOptions.IgnoreCase);
            MatchCollection nc = nameTitle.Matches(srcString);
            foreach (Match na in nc)
            {
                temp += BLL.RemoveHttpStr.Remove(na.Value).Trim();
            }

            //MessageBox.Show(temp);
            //创建一个文件夹
            if (Directory.Exists(".\\" + temp))
            {//do nothing 
            }
            else
            {
                Directory.CreateDirectory(".\\" + temp);
            }
            this.ArticleName = temp;
            this.pageNum = Convert.ToInt32(pageNum);

            ArrayList result = new ArrayList();
            result.Add(pageNum);
            result.Add(temp);
            return result;
        }
    }

    public class ProcessBarEventArgs : EventArgs
    {
        int m_inCurProcess;

        public int InCurProcess
        {
            get { return m_inCurProcess; }
            //  set { m_inCurProcess = value; }
        }

        public ProcessBarEventArgs(int inCurProcess)
        {
            this.m_inCurProcess = inCurProcess;
        }


    }
}
