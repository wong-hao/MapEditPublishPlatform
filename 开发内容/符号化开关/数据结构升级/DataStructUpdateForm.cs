using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using SMGI.Common;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.DataManagementTools;
using ESRI.ArcGIS.Geoprocessor;
using System.IO;
using ESRI.ArcGIS.Carto;
using System.Xml.Linq;

namespace SMGI.Plugin.BaseFunction
{
    public partial class DataStructUpdateForm : Form
    {
        GApplication _app;

        /// <summary>
        /// 比例尺
        /// </summary>
        public string Mapscale
        {
            get
            {
                return tbMapScale.Text.Trim();
            }
        }

        /// <summary>
        /// 模板风格：一般，水利，影像
        /// </summary>
        public string BaseMapEle
        {
            get
            {
                return cbBaseMapTemplate.Text.Trim();
            }
        }
        /// <summary>
        /// 地图开本：双全开，全开，对开
        /// </summary>
        public string MapSize
        {
            get
            {
                return cmbMapSize.Text.Trim();
            }
        }
        /// <summary>
        /// 模板尺度
        /// </summary>
        public string MapTemplate
        {
            set;
            get;
        }

        /// <summary>
        /// 源数据库
        /// </summary>
        public string SourceGDBFile
        {
            get
            {
                return txtTarget.Text.Trim();
            }
        }
        public bool AttachMap
        {
            get
            {
                return cbAttach.Checked;
            }
        }
        /// <summary>
        /// 输出数据库
        /// </summary>
        public string OutputGDBFile
        {
            get
            {
                return txtExport.Text.Trim();
            }
        }
        List<string> names = new List<string>();
        Dictionary<string, string> EnvironmentDic = new Dictionary<string, string>();

        public DataStructUpdateForm(GApplication app)
        {
            this._app = app;
            this.names = getBaseMapTemplateNames();
            InitializeComponent();

            cbBaseMapTemplate.Items.AddRange(names.ToArray());
            var ele = EnvironmentSettings.getContentElement(_app).Element("AttachArea");
            if (ele != null)
            {
                var attachScale = EnvironmentSettings.getContentElement(_app).Element("AttachArea").Element("AttachMapScale").Value; //附区比例尺
                if (attachScale != null)
                {
                    LoadScaleInfo(double.Parse(attachScale));

                }
            }
            else
            {
                LoadScaleInfo(0);
            }
        }
        public DataStructUpdateForm(GApplication app, string gdbpath, ServerScaleInfo attachDBInfo_ = null)
        {
            this._app = app;
            this.names = getBaseMapTemplateNames();
            InitializeComponent();
            LoadSourceGDB(gdbpath);
            string gdbName = System.IO.Path.GetFileNameWithoutExtension(gdbpath) + "_Ecarto.gdb";
            string savegdb = System.IO.Path.GetDirectoryName(gdbpath) + "\\" + gdbName;

            txtExport.Text = savegdb;

            cbBaseMapTemplate.Items.AddRange(names.ToArray());
            LoadScaleInfo(0);
            if (attachDBInfo_ != null)//设置参数
            {
                if (attachDBInfo_.MapTemplate != null)
                {
                    LoadScaleInfo(attachDBInfo_.MapScale);
                }

            }
        }
        bool shell = false;
        public DataStructUpdateForm(GApplication app, string gdbpath, bool shell_)
        {
            shell = true;
            this._app = app;
            this.names = getBaseMapTemplateNames();
            InitializeComponent();
            string gdbName = System.IO.Path.GetFileNameWithoutExtension(gdbpath) + "_Ecarto.gdb";
            string savegdb = System.IO.Path.GetDirectoryName(gdbpath) + "\\" + gdbName;
            txtTarget.Text = gdbpath;
            txtExport.Text = savegdb;

            cbBaseMapTemplate.Items.AddRange(names.ToArray());


        }

        private void LoadScaleInfo(double mapscaleAt)
        {
            XElement expertiseContent = ExpertiseDatabase.getContentElement(GApplication.Application);
            var mapScaleRule = expertiseContent.Element("MapScaleRule");
            var scaleItems = mapScaleRule.Elements("Item");
            foreach (XElement ele in scaleItems)
            {
                var info = new ServerScaleInfo();
                info.MapTemplate = ele.Element("MapTemplate").Value;
                info.DataBaseName = ele.Element("DatabaseName").Value;
                info.ScaleItem = "1:" + ele.Element("Scale").Value;
                info.MapScale = double.Parse(ele.Element("Scale").Value);
            }
        }
        public void SetAttachMap(bool flag)
        {
            cbAttach.Checked = flag;
            if (!flag)
                cbAttach.Enabled = false;
        }
        private void DataStructUpdateForm_Load(object sender, EventArgs e)
        {
            init();
            //判断专家库是否存在MapSize.xml
            //cmbMapSize.Items.Add("空");
            //var path = GApplication.Application.Template.Root + @"\专家库\尺寸模板\MapSize.xml";
            //if (File.Exists(path))
            //{
            //    XDocument doc = XDocument.Load(path);
            //    var items = doc.Root.Elements("Item");
            //    foreach (var item in items)
            //    {
            //        cmbMapSize.Items.Add(item.Value);
            //    }
            //}
           // cmbMapSize.SelectedIndex = 0;
        }
        private void LoadSourceGDB(string gdbpath)
        {
            txtTarget.Text = gdbpath;

            //using (var wo = _app.SetBusy())
            {
                //wo.SetText("正在获取源数据库的配置信息......");

                //更新系统的环境配置文件（有什么更好的方法没？？？）
                IWorkspaceFactory pWorkspaceFactory = new FileGDBWorkspaceFactoryClass();
                IWorkspace ws = pWorkspaceFactory.OpenFromFile(gdbpath, 0);
                try
                {
                    var config = Config.Open(ws as IFeatureWorkspace);
                    var envString = config["EMEnvironment"] as Dictionary<string, string>;
                    if (envString == null)
                    {
                        envString = EnvironmentSettings.GetConfigVal(config, "EMEnvironmentXML");
                    }
                    if (envString != null)
                        EnvironmentSettings.updateElementbyKV(GApplication.Application, envString);
                    //更新相关控件的值
                    if (envString.ContainsKey("AttachMap"))
                    {
                        cbAttach.Checked = bool.Parse(envString["AttachMap"]);
                        if (!cbAttach.Checked)
                            cbAttach.Enabled = false;
                    }
                    EnvironmentDic = envString;
                }
                catch
                {

                }
                init();


            }
        }
        private void btnTarget_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.Description = "选择GDB工程文件夹";
            fbd.ShowNewFolderButton = false;

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (!GApplication.GDBFactory.IsWorkspace(fbd.SelectedPath))
                {
                    MessageBox.Show("不是有效地GDB文件");
                    return;
                }

                txtTarget.Text = fbd.SelectedPath;
                //地图符号化功能中这里不需要吧？后续会更新环境设置的xml，这里又将设置的参数重置了
                //LoadSourceGDB(txtTarget.Text);

                string gdbName = System.IO.Path.GetFileNameWithoutExtension(txtTarget.Text) + "_Ecarto.gdb";
                string savegdb = System.IO.Path.GetDirectoryName(txtTarget.Text) + "\\" + gdbName;

                txtExport.Text = savegdb;

                updateTemplateAndRule2();
            }



        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            SaveFileDialog pDialog = new SaveFileDialog();
            pDialog.AddExtension = true;
            pDialog.DefaultExt = "gdb";
            pDialog.Filter = "文件地理数据库|*.gdb";
            pDialog.FilterIndex = 0;
            if (pDialog.ShowDialog() == DialogResult.OK)
            {
                txtExport.Text = pDialog.FileName;
            }
        }
        public ServerScaleInfo AttachInfo = null;
        private void btnOK_Click(object sender, EventArgs e)
        {
            AttachInfo = new ServerScaleInfo();
            if (txtExport.Text.Trim() == "" || txtTarget.Text.Trim() == "" || tbMapScale.Text.Trim() == "" || cbBaseMapTemplate.Text.Trim() == "")
            {
                MessageBox.Show("输入不能为空！");
                return;
            }
            if (!shell)
            {
                if (!Directory.Exists(txtTarget.Text.Trim()))
                {
                    MessageBox.Show("升级数据文件不存在！");
                    return;
                }
            }
            if (Directory.Exists(txtExport.Text.Trim()))
            {
                MessageBox.Show("导出数据文件已经存在！");
                //return;
            }

            int mapscale = 1;
            int.TryParse(tbMapScale.Text.Trim(), out mapscale);
            if (mapscale < 1)
            {
                MessageBox.Show("输入的比例尺不合法!");
                return;
            }

            //更新配置表
            EnvironmentSettings.updateMapScale(_app, mapscale);
            EnvironmentSettings.updateMapSizeStyle(_app, MapSize);
            EnvironmentSettings.updateBaseMap(_app, cbBaseMapTemplate.Text);
            EnvironmentSettings.updateMapTemplate(_app, cmbMapSize.Text);
            if (EnvironmentDic != null)
            {
                if (EnvironmentDic.ContainsKey("ThemDataBase"))
                {
                    CommonMethods.ThemDataBase = EnvironmentDic["ThemDataBase"];

                }
                if (EnvironmentDic.ContainsKey("ThemExist"))
                {
                    CommonMethods.ThemData = bool.Parse(EnvironmentDic["ThemExist"]);

                }
            }
            var paramContent = EnvironmentSettings.getContentElement(_app);
            MapTemplate = paramContent.Element("MapTemplate").Value;//模板尺度
            DialogResult = DialogResult.OK;
        }

        private void init()
        {
            var paramContent = EnvironmentSettings.getContentElement(_app);
            var mapScale = paramContent.Element("MapScale");//比例尺
            var baseMapEle = paramContent.Element("BaseMap");//模板风格
            var mapSizeStyle = paramContent.Element("MapSizeStyle");//开本
            double attachScale = 0;
            if (paramContent.Element("AttachArea") != null)
            {
                var val = paramContent.Element("AttachArea").Element("AttachMapScale").Value; //附区比例尺
                attachScale = double.Parse(val);
            }



            //主区比例尺小于5万  模板为天地图
            //if (Int32.Parse(mapScale.Value) > 50000)
            //{
            //    cbBaseMapTemplate.Items.Remove("天地图");
            //}
            //else
            //{
            //    cbBaseMapTemplate.Items.Clear();
            //    cbBaseMapTemplate.Items.Add("天地图");
            //    cbBaseMapTemplate.SelectedIndex = 0;
            //}

            if (Int32.Parse(mapScale.Value) <= 35000)
            {
                if (baseMapEle.Value != "天地图" && cbBaseMapTemplate.Items.Contains("天地图"))
                    baseMapEle.Value = "天地图";
            }
            else
            {
                if (baseMapEle.Value == "天地图")
                    baseMapEle.Value = "";
            }
            //设置BaseMap
            if (cbBaseMapTemplate.Items.Contains(baseMapEle.Value))
            {
                cbBaseMapTemplate.SelectedIndex = cbBaseMapTemplate.Items.IndexOf(baseMapEle.Value);
            }
            else
            {
                cbBaseMapTemplate.SelectedIndex = -1;
            }
            //设置MapTemplate
            if (cmbMapSize.Items.Contains(mapSizeStyle.Value))
            {
                cmbMapSize.SelectedIndex = cmbMapSize.Items.IndexOf(mapSizeStyle.Value);
            }
            else
            {
                cmbMapSize.SelectedIndex = -1;
            }

            //附区比例尺 小于5万
            //if (attachScale > 50000)
            //{
            //    cmbAttachTemplate.Items.Remove("天地图");

            //}
            tbMapScale.Text = mapScale.Value;
        }


        private List<string> getBaseMapTemplateNames()
        {
            List<string> names = new List<string>();

            const string TemplatesFileName = "MapStyle.xml";
            string thematicPath = _app.Template.Root + @"/底图";
            DirectoryInfo dir = new DirectoryInfo(thematicPath);
            var dirs = dir.GetDirectories();

            foreach (var d in dirs)
            {
                var fs = d.GetFiles(TemplatesFileName);
                if (fs.Length != 1)
                {
                    continue;
                }
                var f = fs[0];
                try
                {
                    XElement xmlContent = FromFileInfo(f);
                    names.Add(xmlContent.Element("Name").Value);
                }
                catch
                {
                    continue;
                }

            }
            return names;
        }

        /// <summary>
        /// 根据文件对象返回内容节点
        /// </summary>
        /// <param name="f">文件对象</param>
        /// <returns>内容节点</returns>
        public XElement FromFileInfo(FileInfo f)
        {

            {
                XDocument doc = XDocument.Load(f.FullName);
                return doc.Element("Template").Element("Content");
            }
        }

        private void tbMapScale_Leave(object sender, EventArgs e)
        {
            updateTemplateAndRule();
        }

        private void tbMapScale_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                updateTemplateAndRule();
            }
        }

        private void cbBaseMapTemplate_SelectedIndexChanged(object sender, EventArgs e)
        {
            cmbMapSize.Items.Clear();
            string thematiPath = _app.Template.Root + "\\底图\\" + cbBaseMapTemplate.SelectedItem.ToString();
            DirectoryInfo dir = new DirectoryInfo(thematiPath);
            var dirs = dir.GetDirectories();
            foreach (var d in dirs)
            {
                cmbMapSize.Items.Add(d.Name);
            }
            cmbMapSize.SelectedIndex = 0;
            updateTemplateAndRule2();
        }

        private void cmbMapSize_SelectedIndexChanged(object sender, EventArgs e)
        {
            string strFileName = _app.Template.Root + "\\底图\\" + cbBaseMapTemplate.SelectedItem.ToString() + "\\" + cmbMapSize.Text + "\\";
            tbMxdFile.Text = strFileName + cmbMapSize.Text + ".mxd";
            tbLayerRuleFile.Text = strFileName + "规则对照.mdb";

            txtTarget.Text = _app.Workspace.EsriWorkspace.PathName;
            string gdbName = System.IO.Path.GetFileNameWithoutExtension(txtTarget.Text) + "_Ecarto.gdb";
            string savegdb = System.IO.Path.GetDirectoryName(txtTarget.Text) + "\\" + gdbName;
            txtExport.Text = savegdb;

            updateTemplateAndRule2();
        }

        private void updateTemplateAndRule()
        {
            try
            {
                tbMxdFile.Text = "";
                tbLayerRuleFile.Text = "";

                if (cbBaseMapTemplate.Text.Trim() == "")
                    return;

                int mapscale = 1;
                int.TryParse(tbMapScale.Text.Trim(), out mapscale);
                if (mapscale < 1)
                    return;

                //更新配置表
                EnvironmentSettings.updateMapScale(_app, mapscale);
                EnvironmentSettings.updateMapSizeStyle(_app, MapSize);
                EnvironmentSettings.updateBaseMap(_app, cbBaseMapTemplate.Text);
                if (EnvironmentDic != null)
                {
                    if (EnvironmentDic.ContainsKey("ThemDataBase"))
                    {
                        CommonMethods.ThemDataBase = EnvironmentDic["ThemDataBase"];

                    }
                    if (EnvironmentDic.ContainsKey("ThemExist"))
                    {
                        CommonMethods.ThemData = bool.Parse(EnvironmentDic["ThemExist"]);
                    }
                }

                tbMxdFile.Text = EnvironmentSettings.getMxdFullFileName(_app);
                tbLayerRuleFile.Text = EnvironmentSettings.getLayerRuleDBFileName(_app);

            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.Source);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                MessageBox.Show(ex.Message);
            }
        }

        private void updateTemplateAndRule2()
        {
            try
            {

                if (cbBaseMapTemplate.Text.Trim() == "")
                    return;

                int mapscale = 1;
                int.TryParse(tbMapScale.Text.Trim(), out mapscale);
                if (mapscale < 1)
                    return;

                //更新配置表
                EnvironmentSettings.updateMapScale(_app, mapscale);
                EnvironmentSettings.updateMapSizeStyle(_app, MapSize);
                EnvironmentSettings.updateBaseMap(_app, cbBaseMapTemplate.Text);
                if (EnvironmentDic != null)
                {
                    if (EnvironmentDic.ContainsKey("ThemDataBase"))
                    {
                        CommonMethods.ThemDataBase = EnvironmentDic["ThemDataBase"];

                    }
                    if (EnvironmentDic.ContainsKey("ThemExist"))
                    {
                        CommonMethods.ThemData = bool.Parse(EnvironmentDic["ThemExist"]);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.Source);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                MessageBox.Show(ex.Message);
            }
        }

        private void btnGDB_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "地图模板选择";
            ofd.Filter = "地图文档|*.mxd";
            ofd.Multiselect = false;
            ofd.RestoreDirectory = true;
            ofd.InitialDirectory = _app.Template.Root + @"\底图\一般";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                tbMxdFile.Text = ofd.FileName;
            }
        }

        private void btnMDB_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = _app.Template.Root + @"\底图\一般";
            ofd.Filter = "地图对照规则库|*.mdb";
            ofd.Title = "图层对照规则选择";
            ofd.RestoreDirectory = true;
            ofd.Multiselect = false;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                tbLayerRuleFile.Text = ofd.FileName;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

    }

    public class ServerScaleInfo
    {
        public string ScaleItem;
        public double MapScale;//基本比例尺
        public string DataBaseName;//数据库名称
        public string MapTemplate;//5w
        public string MapStyle;//地图风格
        public override string ToString()
        {
            return ScaleItem.ToString();
        }
    }
}
