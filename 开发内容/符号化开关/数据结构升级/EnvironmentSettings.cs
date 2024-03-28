using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;
using SMGI.Common;
using System.Xml;

namespace SMGI.Plugin.BaseFunction
{
    public class EnvironmentSettings
    {
        /// <summary>
        /// 从环境参数配置文件中获取Content元素
        /// </summary>
        /// <param name="app)"></param>
        /// <returns></returns>
        public static XElement getContentElement(GApplication app)
        {
            //var envFileName = app.Template.Content.Element("EnvironmentSettings").Value;
            var envFileName = "EnvironmentSettings.xml";
            string fileName = app.Template.Root + @"\" + envFileName;
            XDocument doc = XDocument.Load(fileName);
            return doc.Element("Template").Element("Content");
            
        }

        /// <summary>
        /// 更新环境参数配置文件中页面大小参数
        /// </summary>
        /// <param name="app"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public static void updatePageSize(GApplication app, double width, double height)
        {
            //var envFileName = app.Template.Content.Element("EnvironmentSettings").Value;
            var envFileName = "EnvironmentSettings.xml"; 
            string fileName = app.Template.Root + @"\" + envFileName;

            XDocument doc = XDocument.Load(fileName); 
            

            var content = doc.Element("Template").Element("Content");
            //纸张尺寸
            var pageSize = content.Element("PageSize");
            pageSize.SetElementValue("Width", width.ToString());
            pageSize.SetElementValue("Height", height.ToString());
            //成图尺寸
            if (content.Element("MapSize") == null)
            {
                content.Add(new XElement("MapSize"));
            }
            var mapSize = content.Element("MapSize");
            mapSize.SetElementValue("Width", CommonMethods.MapSizeWidth.ToString());
            mapSize.SetElementValue("Height", CommonMethods.MapSizeHeight.ToString());
            //图廓尺寸
            if (content.Element("MapInnerSize") == null)
            {
                content.Add(new XElement("MapInnerSize"));
            }
            var mapInSize = content.Element("MapInnerSize");
            mapInSize.SetElementValue("Width", CommonMethods.InlineWidth.ToString());
            mapInSize.SetElementValue("Height", CommonMethods.InlineHeight.ToString());
            //图廓间距
            if (content.Element("MapInterval") == null)
            {
                content.Add(new XElement("MapInterval"));
            }
            var mapInStep = content.Element("MapInterval");
            mapInStep.SetValue(CommonMethods.InOutLineWidth);
            doc.Save(fileName);
        }

        /// <summary>
        /// 更新环境参数配置文件中与比例尺相关参数
        /// </summary>
        /// <param name="app"></param>
        /// <param name="scale"></param>
        public static void updateMapScale(GApplication app, double scale)
        {
            //var envFileName = app.Template.Content.Element("EnvironmentSettings").Value;
            var envFileName = "EnvironmentSettings.xml"; 
            string fileName = app.Template.Root + @"\" + envFileName;
            FileInfo f = new FileInfo(fileName);
            XDocument doc = XDocument.Load(fileName);
            

            var content = doc.Element("Template").Element("Content");
            content.SetElementValue("MapScale", scale.ToString());

            XElement expertiseContent = ExpertiseDatabase.getContentElement(app);
            var mapScaleRule = expertiseContent.Element("MapScaleRule");
            var scaleItems = mapScaleRule.Elements("Item");
            foreach (XElement ele in scaleItems)
            {
                double min = double.Parse(ele.Element("Min").Value);
                double max = double.Parse(ele.Element("Max").Value);
                double templateScale = double.Parse(ele.Element("Scale").Value);
                if (scale >= min && scale <= max)
                {
                    content.SetElementValue("DatabaseName", ele.Element("DatabaseName").Value);
                    content.SetElementValue("MapTemplate", ele.Element("MapTemplate").Value);
                    if (GApplication.Application.Template.Caption.Contains("四川应急制图"))
                    {
                        #region
                        if (templateScale < 50000)//大比例是天地图
                        {
                            content.SetElementValue("BaseMap", "天地图");
                        }
                        else
                        {
                            content.SetElementValue("BaseMap", "一般");
                        }
                        #endregion
                    }
                    break;
                }
                

        }
            
            doc.Save(fileName);
            
        }
        public static void updateMapSizeStyle(GApplication app, string size)
        {
            //var envFileName = app.Template.Content.Element("EnvironmentSettings").Value;
            var envFileName = "EnvironmentSettings.xml"; 
            string fileName = app.Template.Root + @"\" + envFileName;
            FileInfo f = new FileInfo(fileName);
            XDocument doc = XDocument.Load(fileName);
            var content = doc.Element("Template").Element("Content");
            content.SetElementValue("MapSizeStyle", size);
            doc.Save(fileName);
            
        }
        
        public static void updateAttachInfo(GApplication app,double scale,string database,string mapTemplate)
        {
            //var envFileName = app.Template.Content.Element("EnvironmentSettings").Value;
            var envFileName = "EnvironmentSettings.xml"; 
            string fileName = app.Template.Root + @"\" + envFileName;
          
            XDocument doc = XDocument.Load(fileName);
           

            var content = doc.Element("Template").Element("Content").Element("AttachArea");

            content.SetElementValue("AttachMapScale", scale);
            content.SetElementValue("AttachDatabaseName", database);
            content.SetElementValue("AttachMapTemplate", mapTemplate);

            
            doc.Save(fileName);
        }

        /// <summary>
        /// 更新地图风格
        /// </summary>
        /// <param name="app"></param>
        /// <param name="baseMap"></param>
        public static void updateBaseMap(GApplication app, string baseMap)
        {
            //var envFileName = app.Template.Content.Element("EnvironmentSettings").Value;
            var envFileName = "EnvironmentSettings.xml"; 
            string fileName = app.Template.Root + @"\" + envFileName;

            XDocument doc = XDocument.Load(fileName);
           

                var content = doc.Element("Template").Element("Content");

                content.SetElementValue("BaseMap", baseMap);

             
            doc.Save(fileName);
        }
        public static void updateMapTemplate(GApplication app, string mapTemplate)
        {
            //var envFileName = app.Template.Content.Element("EnvironmentSettings").Value;
            var envFileName = "EnvironmentSettings.xml"; 
            string fileName = app.Template.Root + @"\" + envFileName;

            XDocument doc = XDocument.Load(fileName);


            var content = doc.Element("Template").Element("Content");

            content.SetElementValue("MapTemplate", mapTemplate);


            doc.Save(fileName);
        }

        /// <summary>
        /// 根据输入的键值对更新配置表
        /// </summary>
        /// <param name="kv"></param>
        public static void updateElementbyKV(GApplication app, Dictionary<string, string> kv)
        {

            //var envFileName = app.Template.Content.Element("EnvironmentSettings").Value;
            var envFileName = "EnvironmentSettings.xml"; 
            string fileName = app.Template.Root + @"\" + envFileName;

            XDocument doc = XDocument.Load(fileName); 
         

                var content = doc.Element("Template").Element("Content");

                foreach (var item in kv)
                {
                    string key = item.Key;
                    if (key == "SpatialReference")
                        continue;
                    if(key=="UserName")
                     {
                         continue;
                     }
                    if(key=="IPAddress")
                    {
                        continue;
                     }
                    if(key=="Password")
                    {
                        continue;
                     }
                    string val = item.Value;

                    var ele = getSubElement(content, key);
                    if(ele != null)
                        ele.SetValue(val);
                }

             
            doc.Save(fileName);
        }

        /// <summary>
        /// 返回element元素下第一个出现的
        /// </summary>
        /// <param name="element"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static XElement getSubElement(XElement element, string name)
        {
            XElement ele = element.Element(name);
            if (ele != null)
                return ele;
            if (name == "Width" || name == "Height")
            {
                //特指纸张
               return  element.Element("PageSize").Element(name);
            }
            var subElements = element.Elements();
            foreach (var item in subElements)
            {
                ele = getSubElement(item, name);
                if (ele != null)
                    return ele;
            }

            return ele;
        }

        /// <summary>
        /// 获取EnvironmentSettings中Content元素包含的所有的叶子元素
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static Dictionary<string, string> getEnvironmentSettingsElemets(GApplication app)
        {
            Dictionary<string, string> kv = new Dictionary<string, string>();

            XElement content = EnvironmentSettings.getContentElement(app);

            EnvironmentSettings.getSubElements(content, kv);

            return kv;
        }


        /// <summary>
        /// 将数据库环境信息写入当前工程的config表
        /// </summary>
        public static void UpdateEnvironmentToConfig(bool attachMap=false)
        {
            var config = GApplication.Application.Workspace.MapConfig;
            Dictionary<string, string> envString = EnvironmentSettings.getEnvironmentSettingsElemets(GApplication.Application);
            envString["AttachMap"] = attachMap.ToString();
            envString["ThemDataBase"] = CommonMethods.ThemDataBase;
            envString["ThemExist"] = CommonMethods.ThemData.ToString();
            XDocument doc = new System.Xml.Linq.XDocument();
            XElement root = new XElement("Arg");
            foreach (var kv in envString)
            {
                root.Add(new XElement(kv.Key) { Value = kv.Value });
            }
            doc.Add(root);
            config.SetOriginValue("EMEnvironmentXML",doc.ToString());
        }
        public static void UpdateEnvironmentToConfig(Dictionary<string, string> envString)
        {
            var config = GApplication.Application.Workspace.MapConfig;
            XDocument doc = new System.Xml.Linq.XDocument();
            XElement root = new XElement("Arg");
            foreach (var kv in envString)
            {
                root.Add(new XElement(kv.Key) { Value = kv.Value });
            }
            doc.Add(root);
            config.SetOriginValue("EMEnvironmentXML", doc.ToString());
        }
        public static void UpdateEnvironmentToConfig(Dictionary<string, string> envString,Config config)
        {
 
            XDocument doc = new System.Xml.Linq.XDocument();
            XElement root = new XElement("Arg");
            foreach (var kv in envString)
            {
                root.Add(new XElement(kv.Key) { Value = kv.Value });
            }
            doc.Add(root);
            config.SetOriginValue("EMEnvironmentXML", doc.ToString());
        }
        public static Dictionary<string, string> GetConfigVal(IConfig config, string key)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            string val = config.GetOriginValue(key);
            XDocument doc = XDocument.Parse(val);
            foreach (var ele in doc.Root.Elements())
            {
                dic[ele.Name.ToString()] = ele.Value;
            }
            return dic;
        }
        public static Dictionary<string,string> GetConfigVal(string key)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            var config = GApplication.Application.Workspace.MapConfig;
            string val= config.GetOriginValue(key);
            if (val != null)
            {
                XDocument doc = XDocument.Parse(val);
                foreach (var ele in doc.Root.Elements())
                {
                    dic[ele.Name.ToString()] = ele.Value;
                }
            }
            return dic;
        }
        public static void UpdateEnvironmentToConfig(Config config, bool attachMap = false)
        {
            Dictionary<string, string> envString = EnvironmentSettings.getEnvironmentSettingsElemets(GApplication.Application);
            envString["AttachMap"] = attachMap.ToString();
            envString["ThemDataBase"] = CommonMethods.ThemDataBase;
            envString["ThemExist"] = CommonMethods.ThemData.ToString();

            envString["MapSizeWidth"] = CommonMethods.MapSizeWidth.ToString();
            envString["MapSizeHeight"] = CommonMethods.MapSizeHeight.ToString();
            envString["InlineWidth"] = CommonMethods.InlineWidth.ToString();
            envString["InlineHeight"] = CommonMethods.InlineHeight.ToString();
            envString["InOutLineWidth"] = CommonMethods.InOutLineWidth.ToString();

            XDocument doc = new System.Xml.Linq.XDocument();
            XElement root = new XElement("Arg");
            foreach (var kv in envString)
            {
                root.Add(new XElement(kv.Key) { Value = kv.Value });
            }
            doc.Add(root);
            config.SetOriginValue("EMEnvironmentXML", doc.ToString());
        }
        /// <summary>
        /// 递归获取element所有子元素
        /// </summary>
        /// <param name="element"></param>
        /// <param name="kv"></param>
        public static void getSubElements(XElement element, Dictionary<string, string> kv)
        {
            if (!element.HasElements)
            {
                //防止pageSize覆盖
                if (!kv.ContainsKey(element.Name.LocalName))
                {
                    kv[element.Name.LocalName] = element.Value;
                }
                return;
            }
            var subElements = element.Elements();
            foreach (var ele in subElements)
            {
                getSubElements(ele, kv);
            }
        }


        /// <summary>
        /// 读取配置表,返回图廓要素的模板名称
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static string getTemplateFclName()
        {
            GApplication app = GApplication.Application;
            var content = getContentElement(app);
            var mapTemplate = content.Element("MapTemplate");
            string fclname = "";
            switch (mapTemplate.Value)
            {
                case"5万":
                    fclname = "LPOINT5W";
                    break;
                case "10万":
                    fclname = "LPOINT10W";
                    break;
                case "25万":
                    fclname = "LPOINT25W";
                    break;
                case "50万":
                    fclname = "LPOINT50W";
                    break;
                case "100万":
                    fclname = "LPOINT100W";
                    break;
                default:
                    fclname = "LPOINT5W";
                    break;
            }
            return fclname;
            
 
        }

        /// <summary>
        /// 获取专题名称
        /// </summary>
        /// <returns></returns>
        public static string getMapthemeName()
        {
            GApplication app = GApplication.Application;
            var content = getContentElement(app);
            var mapTemplate = content.Element("ThematicMap");
            string name = mapTemplate.Value;
            return name;
        }

        
        /// <summary>
        /// 读取配置表，返回附图文件全路径
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static string getLocationMxdFullFileName(GApplication app)
        {
            var content = getContentElement(app);
            var mapTemplate = content.Element("MapTemplate");

            return app.Template.Root + "\\位置图\\位置图.mxd";
        }

        /// <summary>
        /// 读取配置表，返回模板数据库全路径
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static string getTemplateFullFileName(GApplication app)
        {
            var content = getContentElement(app);
            var baseMap = content.Element("BaseMap");
            var mapTemplate = content.Element("MapTemplate");
            #region
            if (content.Element("MapSizeStyle") != null)
            {
                string mapsizeStlye = content.Element("MapSizeStyle").Value;
                var path = GApplication.Application.Template.Root + @"\专家库\尺寸模板\MapSize.xml";
                Dictionary<string, XElement> dic = new Dictionary<string, XElement>();//全开模板->25万
                if (File.Exists(path))
                {
                    XDocument doc = XDocument.Load(path);
                    var items = doc.Root.Elements("Item");
                    foreach (var item in items)
                    {
                        dic[item.Value] = item;
                    }
                    if (dic.ContainsKey(mapsizeStlye))
                    {
                        string temp = dic[mapsizeStlye].Attribute("Scale").Value;
                        string bm = baseMap.Value;
                        return app.Template.Root + "\\底图\\" + bm + "\\" + temp + "\\" + temp + ".gdb";
                    }
                }
            }
            #endregion
            return app.Template.Root + "\\底图\\" + baseMap.Value + "\\" + mapTemplate.Value + "\\" + mapTemplate.Value + ".gdb";
        }

        /// <summary>
        /// 读取配置表，返回地图模板全路径
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static string getMxdFullFileName(GApplication app)
        {
            var content = getContentElement(app);
            //Dictionary<string, string> envString = GApplication.Application.Workspace.MapConfig["EMEnvironment"] as Dictionary<string, string>;
            //if (envString == null)
            //{
            //    envString = EnvironmentSettings.GetConfigVal("EMEnvironmentXML");
            //}
            var baseMap = content.Element("BaseMap");
           
            var mapTemplate = content.Element("MapTemplate");
            #region
            if (content.Element("MapSizeStyle") != null)
            {
                string mapsizeStlye = content.Element("MapSizeStyle").Value;
                var path = GApplication.Application.Template.Root + @"\专家库\尺寸模板\MapSize.xml";
                Dictionary<string, XElement> dic = new Dictionary<string, XElement>();//全开模板->25万
                if (File.Exists(path))
                {
                    XDocument doc = XDocument.Load(path);
                    var items = doc.Root.Elements("Item");
                    foreach (var item in items)
                    {
                        dic[item.Value] = item;
                    }
                    if (dic.ContainsKey(mapsizeStlye))
                    {
                        string temp = dic[mapsizeStlye].Attribute("Scale").Value;
                        string bm = baseMap.Value;
                        return app.Template.Root + "\\底图\\" + bm + "\\" + temp + "\\" + temp + ".mxd";
                    }
                }
               
            }
            #endregion
            return app.Template.Root + "\\底图\\" + baseMap.Value + "\\" + mapTemplate.Value + "\\" + mapTemplate.Value + ".mxd";
        }

        /// <summary>
        /// 读取配置表，返回图层对照规则库全路径
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static string getLayerRuleDBFileName(GApplication app)
        {
            var ruleDataBaseFileName = app.Template.Content.Element("RuleDataBase").Value;
            if (GApplication.Application.Workspace != null)
            {
                Dictionary<string, ParameterInfo> dic = new Dictionary<string, ParameterInfo>();
                Dictionary<string, string> envString = GApplication.Application.Workspace.MapConfig["EMEnvironment"] as Dictionary<string, string>;
                if (envString == null)
                {
                    envString = EnvironmentSettings.GetConfigVal("EMEnvironmentXML");
                }
                if (envString != null)
                {
                    if (envString.ContainsKey("BaseMap") && envString.ContainsKey("MapTemplate"))
                    {
                        string db = GApplication.Application.Template.Root + "\\底图\\" + envString["BaseMap"] + "\\" + envString["MapTemplate"] + "\\" + ruleDataBaseFileName;
                        return db;
                    }
                }
            }
            var content = getContentElement(app);
            var baseMap = content.Element("BaseMap");
            var mapTemplate = content.Element("MapTemplate");
            return app.Template.Root + "\\底图\\" + baseMap.Value + "\\" + mapTemplate.Value + "\\" + ruleDataBaseFileName;
        }

        /// <summary>
        /// 读取配置表，返回对照规则全路径
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static string getAnnoRuleDBFileName(GApplication app)
        {
            var ruleDataBaseFileName = app.Template.Content.Element("RuleDataBase").Value;

            var content = getContentElement(app);
            var baseMap = content.Element("BaseMap");
            var mapTemplate = content.Element("MapTemplate");
            #region
            if (content.Element("MapSizeStyle") != null)
            {
                string mapsizeStlye = content.Element("MapSizeStyle").Value;
                var path = GApplication.Application.Template.Root + @"\专家库\尺寸模板\MapSize.xml";
                Dictionary<string, XElement> dic = new Dictionary<string, XElement>();//全开模板->25万
                if (File.Exists(path))
                {
                    XDocument doc = XDocument.Load(path);
                    var items = doc.Root.Elements("Item");
                    foreach (var item in items)
                    {
                        dic[item.Value] = item;
                    }
                    if (dic.ContainsKey(mapsizeStlye))
                    {
                        string temp = dic[mapsizeStlye].Attribute("Scale").Value;
                        string bm = baseMap.Value;
                        return app.Template.Root + "\\底图\\" + bm + "\\" + temp + "\\" + ruleDataBaseFileName;
                    }
                }
            }
            #endregion
            return app.Template.Root + "\\底图\\" + baseMap.Value + "\\" + mapTemplate.Value + "\\" + ruleDataBaseFileName;
        }
        
    }
}
