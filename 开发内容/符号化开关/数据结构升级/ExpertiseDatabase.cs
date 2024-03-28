using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using SMGI.Common;
using System.IO;

namespace SMGI.Plugin.BaseFunction
{
    public class ExpertiseDatabase
    {
        /// <summary>
        /// 从专家数据库中获取Content元素
        /// </summary>
        /// <returns></returns>
        public static XElement getContentElement(GApplication app)
        {
            //string envFileName = app.Template.Content.Element("Expertise").Value;
            string envFileName = "Expertise.xml";
            string fileName = app.Template.Root + @"\专家库\" + envFileName;
            XDocument doc = XDocument.Load(fileName);
            
            {
                return doc.Element("Expertise").Element("Content");
            }
        }
    }
}
