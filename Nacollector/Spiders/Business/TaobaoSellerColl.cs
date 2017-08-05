﻿using CsQuery;
using Nacollector.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace Nacollector.Spiders.Business
{
    /// <summary>
    /// 淘宝店铺搜索卖家ID名采集
    /// </summary>
    public class TaobaoSellerColl : Spider
    {
        // 参数
        string PageUrl = "";
        int CollBeginPage = 0;
        int CollEndPage = 0;
        // 卖家名池
        List<string> sellerNames = new List<string>();

        public override void BeginWork()
        {
            base.BeginWork();
            // 参数设定
            PageUrl = GetParm("PageUrl").Trim();
            bool CollBeginPageIsInt = Int32.TryParse(GetParm("CollBeginPage"), out CollBeginPage); // out 到 this.CollBeginPage 里 2333
            if (!CollBeginPageIsInt) throw new Exception("参数 CollBeginPage 不是数字");
            bool CollEndPageIsInt = Int32.TryParse(GetParm("CollEndPage"), out CollEndPage);
            if (!CollEndPageIsInt) throw new Exception("参数 CollEndPage 不是数字");
            
            for (int i = CollBeginPage; i <= CollEndPage; i++)
            {
                WorkOnPage(i); 
            }

            string txtFilePath = Path.Combine(GetTempDirPath("ForUserSave"), $"TaobaoSellerColl_{DateTime.Now.ToString("yyyyMMddhhmmss")}.txt");
            string txtFileStr = "";
            foreach (var item in sellerNames) txtFileStr += $"{item}{Environment.NewLine}";
            txtFileStr = txtFileStr.Trim();
            File.WriteAllText(txtFilePath, txtFileStr);
            Log("\n");
            LogInfo($"<a href=\"{txtFilePath}\" onclick=\"downloadFile($(this).attr('href'));return false;\">点击保存卖家ID名文本文件</a>");
        }
        
        /// <summary>
        /// 收集指定某一页的全部卖家名
        /// </summary>
        /// <param name="page"></param>
        private void WorkOnPage(int page)
        {
            Log("\n");
            Log($"&gt;&gt; 开始采集第 {page} 页卖家名");
            string reqUrl = GetHandledUrl(page);
            if (string.IsNullOrEmpty(reqUrl)) { LogError("请求地址有误"); return; }
            string refererUrl = (page > 1) ? (GetHandledUrl(page - 1)) : "https://www.taobao.com";
            Log($"请求地址：{reqUrl}\n伪造来源：{refererUrl}");
            HttpResult downloadPage = Utils.GetPageByUrl(reqUrl, new Dictionary<string, string>{ { "referer", refererUrl } });
            if (downloadPage.StatusCode != System.Net.HttpStatusCode.OK) { LogError("下载失败 [" + downloadPage.StatusCode + "] " + downloadPage.StatusDescription); }
            string pageContent = downloadPage.Html;
            LogSuccess("下载完毕");
            JObject jsonConf;
            try
            {
                string jsonStr = new Regex("(?s)(?i)g_page_config = {(.*?)};").Match(pageContent).Groups[1].Value.Trim();
                jsonConf = JObject.Parse("{"+jsonStr+"}");
            }
            catch { LogError("解析 JSON 失败"); return; }
            string itemsJsonStr = jsonConf["mods"]["shoplist"]["data"]["shopItems"].ToString();
            if (string.IsNullOrEmpty(itemsJsonStr)) { LogError("内容无法正常获取"); return; }
            JArray items = JArray.Parse(jsonConf["mods"]["shoplist"]["data"]["shopItems"].ToString());
            int addedCount = 0;
            foreach (var item in items)
            {
                var seller = item["nick"].ToString().Trim();
                if (!sellerNames.Contains(seller))
                    sellerNames.Add(seller);
                addedCount++;
            }
            LogSuccess($"采集 {addedCount} 个卖家ID");
        }

        /// <summary>
        /// 根据页码生成 URL
        /// </summary>
        /// <param name="page"></param>
        private string GetHandledUrl(int page)
        {
            if (page <= 0)
                page = 0;
            else
                page = page * 20 - 20;
            
            if (string.IsNullOrEmpty(PageUrl)) return null;
            if (PageUrl.IndexOf("shopsearch.taobao.com") <= -1) return null;
            string pattern = @"(?<=&s=)\d+(?=.*?)";
            if (Regex.IsMatch(PageUrl, pattern))
                return Regex.Replace(PageUrl, pattern, page.ToString());
            else
                return PageUrl + "&s=" + page;
        }
    }
}