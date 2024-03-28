using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SMGI.Common;
using ESRI.ArcGIS.Geoprocessor;
using System.Windows.Forms;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Carto;
using System.Data;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.DataSourcesRaster;
using ESRI.ArcGIS.SpatialAnalyst;
using ESRI.ArcGIS.GeoAnalyst;
using ESRI.ArcGIS.Geometry;
using System.Xml.Linq;
using System.IO;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Framework;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.esriSystem;
using System.Data.OleDb;
namespace SMGI.Plugin.BaseFunction
{
    public class CommonMethods
    {
        //new 
        public static ISpatialReference ClipSpatialReference = null;
        //

        public static bool UsingMask = false;
        public static string MaskLayer = "";
        public static string MaskedLayer = "";
        public static string WorkSpacePath = string.Empty;
        public static Dictionary<string, int> DisplayRuleIDs = new Dictionary<string, int>();
        public static string GetMapName()
        {           
            var lyr = GApplication.Application.Workspace.LayerManager.GetLayer(l => (l is IFDOGraphicsLayer) && (l as IFeatureLayer).FeatureClass.AliasName.ToUpper() == "LANNO").FirstOrDefault();
            if (lyr == null)
                return "";
            IFeatureClass   fclanno = (lyr as IFeatureLayer).FeatureClass;
            IQueryFilter qf = new QueryFilterClass();
            IFeature fe;
            IFeatureCursor cursor = null;
            //图名
            qf.WhereClause = "TYPE = '图名'";
            cursor = fclanno.Search(qf, false);
            fe = cursor.NextFeature();
            if (fe != null)
            {
                var namefe = fe as IAnnotationFeature2;
                var txtEle = namefe.Annotation as ITextElement;
                string mapName = txtEle.Text;
                mapName = mapName.Replace(" ", "");
                return txtEle.Text;

            }
            else
            {
                return "";
            }
        }

        public static string GetGdbName()
        { 
            string gdbpath = GApplication.Application.Workspace.EsriWorkspace.PathName;
            string gdbname = System.IO.Path.GetFileNameWithoutExtension(gdbpath);
            gdbname = gdbname.Replace("_Ecarto", "");
            gdbname = gdbname.Replace(" ", "");
            return gdbname;            
        }


        public static int FeatureClassDisplay(string fclName)
        {

            fclName = fclName.ToUpper();
            if (WorkSpacePath != GApplication.Application.Workspace.EsriWorkspace.PathName)
            {
                DisplayRuleIDs.Clear();
                WorkSpacePath = GApplication.Application.Workspace.EsriWorkspace.PathName;
            }
            if (DisplayRuleIDs.ContainsKey(fclName))
            {
                return DisplayRuleIDs[fclName];
            }
            else
            {
                #region
                var lyrs = GApplication.Application.Workspace.LayerManager.GetLayer(new LayerManager.LayerChecker(l =>
                {
                    return (l is IGeoFeatureLayer) && ((l as IGeoFeatureLayer).FeatureClass.AliasName.ToUpper() == fclName.ToUpper());
                })).ToArray();
                ILayer pRepLayer = lyrs.First();//boua

              
                IRepresentationRenderer rp = (pRepLayer as IGeoFeatureLayer).Renderer as IRepresentationRenderer;
                IRepresentationClass m_RepClassTarget = rp.RepresentationClass;

                IRepresentationRules rules = m_RepClassTarget.RepresentationRules;
                rules.Reset();
                IRepresentationRule rule = null;
                int ruleID;
                while (true)
                {
                    rules.Next(out ruleID, out rule);
                    if (rule == null) break;
                    if (rules.get_Name(ruleID) == "不显示要素")
                    {
                        DisplayRuleIDs[fclName.ToUpper()] = ruleID;
                        break;
                    }
                }
                #endregion
                return DisplayRuleIDs[fclName];
            }
             
        }
        /// <summary>
        /// 根据RuleName获取RuleID值
        /// </summary>
        /// <param name="fclName"></param>
        /// <param name="ruleName"></param>
        /// <returns></returns>
        public static int GetRuleIDByRuleName(string fclName,string ruleName)
        {

            fclName = fclName.ToUpper();
            if (WorkSpacePath != GApplication.Application.Workspace.EsriWorkspace.PathName)
            {
                WorkSpacePath = GApplication.Application.Workspace.EsriWorkspace.PathName;
            }
           
                #region
                var lyrs = GApplication.Application.Workspace.LayerManager.GetLayer(new LayerManager.LayerChecker(l =>
                {
                    return (l is IGeoFeatureLayer) && ((l as IGeoFeatureLayer).FeatureClass.AliasName.ToUpper() == fclName.ToUpper());
                })).ToArray();
                if (lyrs.Length == 0)
                    return -1;
                ILayer pRepLayer = lyrs.First();//boua
                IRepresentationRenderer rp = (pRepLayer as IGeoFeatureLayer).Renderer as IRepresentationRenderer;
                IRepresentationClass m_RepClassTarget = rp.RepresentationClass;
                IRepresentationRules rules = m_RepClassTarget.RepresentationRules;
                rules.Reset();
                IRepresentationRule rule = null;
                int ruleID;
                while (true)
                {
                    rules.Next(out ruleID, out rule);
                    if (rule == null) break;
                    if (rules.get_Name(ruleID).Trim().Replace(" ", "") == ruleName.Trim().Replace(" ", ""))//不考虑空白
                    {
                        return  ruleID;
                    }
                }
                #endregion
                return -1;
        }
        /// <summary>
        /// 基于已有字段在工作空间下创建要素类
        /// </summary>
        /// <param name="ws">目标工作空间</param>
        /// <param name="name">要素类名称</param>
        /// <param name="org_fields">字段几何</param>
        /// <returns></returns>
        public static IFeatureClass CreateFeatureClass(IWorkspace ws, string name, IFields org_fields)
        {
            IObjectClassDescription featureDescription = new FeatureClassDescriptionClass();
            IFieldsEdit target_fields = featureDescription.RequiredFields as IFieldsEdit;

            for (int i = 0; i < org_fields.FieldCount; i++)
            {
                IField field = org_fields.get_Field(i);
                if (!(field as IFieldEdit).Editable)
                {
                    continue;
                }
                if (field.Type == esriFieldType.esriFieldTypeGeometry)
                {
                    (target_fields as IFieldsEdit).set_Field(target_fields.FindFieldByAliasName((featureDescription as IFeatureClassDescription).ShapeFieldName),
                        (field as ESRI.ArcGIS.esriSystem.IClone).Clone() as IField);
                    continue;
                }
                if (target_fields.FindField(field.Name) >= 0)
                {
                    continue;
                }
                IField field_new = (field as ESRI.ArcGIS.esriSystem.IClone).Clone() as IField;
                (target_fields as IFieldsEdit).AddField(field_new);
            }

            IFeatureWorkspace fws = ws as IFeatureWorkspace;

            System.String strShapeField = string.Empty;

            return fws.CreateFeatureClass(name, target_fields,
                  featureDescription.InstanceCLSID, featureDescription.ClassExtensionCLSID,
                  esriFeatureType.esriFTSimple,
                  (featureDescription as IFeatureClassDescription).ShapeFieldName,
                  string.Empty);
        }

        //裁切数据的空间参考
        public static string clipSpatialRefFileName;

        /// <summary>
        /// 根据定位方式确定是否裁切附区
        /// </summary>
        public static bool clipEx=true;

        static CommonMethods()
        {
            //初始化空间参考文件
            clipSpatialRefFileName = GApplication.ExePath + @"\..\Projection\China_2000GCS_Albers.prj";
            //var envFileName =GApplication.Application.Template.Content.Element("EnvironmentSettings").Value;
            var envFileName = "EnvironmentSettings.xml";
            string fileName = GApplication.Application.Template.Root + @"\" + envFileName;
            XDocument doc = XDocument.Load(fileName);

            var content = doc.Element("Template").Element("Content");
            XElement sp = content.Element("SpatialReference");
            if (sp != null)
            {
                clipSpatialRefFileName = GApplication.ExePath + @"\..\Projection\"+sp.Value;
            }
        }
        /// <summary>
        /// 自动计算空间参考
        /// </summary>
        /// 
        public static void CalculateSptialRef(IEnvelope env)
        {
            if (env.SpatialReference != GApplication.Application.MapControl.SpatialReference)
            {
                env.Project(GApplication.Application.MapControl.SpatialReference);
            }
            //三度带的中央经线->文件
            if (env.Width > 4)//大于4度 
            {
                Dictionary<string, string> projectDic = new Dictionary<string, string>();
               FileInfo  dirInfo=new FileInfo(GApplication.Application.Template.Root);
               //string fileName =  dirInfo.Directory+@"\公共配置文件\快速制图投影配置\ProjectSet.xml";
               String fileName = GApplication.Application.Template.Root + @"\投影\ProjectSet.xml";
               if(File.Exists(fileName))
               {
                XDocument doc = XDocument.Load(fileName);

                var content = doc.Element("Template").Element("Content");
                var items=content.Elements("Item");
                foreach(var item in items)
                {
                   string name=   item.Attribute("name").Value;
                    string val=item.Value;
                    projectDic[name]=val;
                }
               }
                clipSpatialRefFileName= GApplication.ExePath + @"\..\Projection\China_2000GCS_Albers.prj";
                if(projectDic.ContainsKey("地图投影模板"))
                {
                    clipSpatialRefFileName = GApplication.ExePath + @"\..\Projection\" + projectDic["地图投影模板"];
                }
                return;
            }
            Dictionary<double, string> spatialFilesDic = new Dictionary<double, string>();
            #region 初始化空间参考
            string prjFolderPath = GApplication.ExePath + @"\..\Projection";
            DirectoryInfo dir = new DirectoryInfo(prjFolderPath);
            var files = dir.GetFiles();

            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].Name.Contains("CGCS2000 3 Degree GK CM"))
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(files[i].Name);
                    string cm = fileName.Split(new string[] { "CGCS2000 3 Degree GK CM" }, StringSplitOptions.RemoveEmptyEntries)[0];
                    string center = cm.Split(new string[] { "E" }, StringSplitOptions.RemoveEmptyEntries)[0];
                    double centerm = double.Parse(center);
                    spatialFilesDic[centerm] = files[i].FullName;
                }

            }
            #endregion

            IPoint ct = (env as IArea).LabelPoint;
            //计算最近的中央经线
            double min = 100;
            double centerkey = 0;
            foreach (var kv in spatialFilesDic)
            {
                if (Math.Abs(kv.Key - ct.X) < min)
                {
                    min = Math.Abs(kv.Key - ct.X);
                    centerkey = kv.Key;
                }
            }
            clipSpatialRefFileName = spatialFilesDic[centerkey];
           
        }
        public static ISpatialReference getClipSpatialRef(IPoint centerpoint)
        {
            ISpatialReference clipRef = null;
            Dictionary<double, string> spatialFilesDic = new Dictionary<double, string>();
            #region 初始化空间参考
            string prjFolderPath = GApplication.ExePath + @"\..\Projection";
            DirectoryInfo dir = new DirectoryInfo(prjFolderPath);
            var files = dir.GetFiles();
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].Name.Contains("CGCS2000 3 Degree GK CM"))
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(files[i].Name);
                    string cm = fileName.Split(new string[] { "CGCS2000 3 Degree GK CM" }, StringSplitOptions.RemoveEmptyEntries)[0];
                    string center = cm.Split(new string[] { "E" }, StringSplitOptions.RemoveEmptyEntries)[0];
                    double centerm = double.Parse(center);
                    spatialFilesDic[centerm] = files[i].FullName;
                }

            }
            #endregion
            IPoint ct = centerpoint;
            //计算最近的中央经线
            double min = 100;
            double centerkey = 0;
            foreach (var kv in spatialFilesDic)
            {
                if (Math.Abs(kv.Key - ct.X) < min)
                {
                    min = Math.Abs(kv.Key - ct.X);
                    centerkey = kv.Key;
                }
            }
            string  spatialRefFileName = spatialFilesDic[centerkey];
            ISpatialReferenceFactory pSpatialRefFactory = new SpatialReferenceEnvironmentClass();
            clipRef = pSpatialRefFactory.CreateESRISpatialReferenceFromPRJFile(spatialRefFileName);
            if (null == clipRef)
            {
                MessageBox.Show("无效的投影文件!");
                return null;
            }
            clipSpatialRefFileName = spatialRefFileName;
            return clipRef;
        }
        public static ISpatialReference getClipSpatialRef()
        {
            ISpatialReference clipRef = null;

            ISpatialReferenceFactory pSpatialRefFactory = new SpatialReferenceEnvironmentClass();
            clipRef = pSpatialRefFactory.CreateESRISpatialReferenceFromPRJFile(clipSpatialRefFileName);
            if (null == clipRef)
            {
                MessageBox.Show("无效的投影文件!");
                return null;
            }

            return clipRef;
        }
        public static bool ThemData = false;//是否下载专题
        public static string ThemDataBase = "";//专题库名称
        /// <summary>
        /// 是否生成裁切线
        /// </summary>
        public static bool NeedTline;

        /// <summary>
        /// 纸张尺寸宽
        /// </summary>
        public static double PaperWidth;
        /// <summary>
        /// 纸张尺寸宽
        /// </summary>
        public static double PaperHeight;
        /// <summary>
        /// 成图尺寸宽
        /// </summary>
        public static double MapSizeWidth;
        /// <summary>
        /// 成图尺寸高
        /// </summary>
        public static double MapSizeHeight;
        /// <summary>
        /// 内图廓宽
        /// </summary>
        public static double InlineWidth;
        /// <summary>
        /// 内图廓高
        /// </summary>
        public static double InlineHeight;
        /// <summary>
        /// 内外图廓间距
        /// </summary>
        public static double InOutLineWidth;
        /// <summary>
        /// 上下间距
        /// </summary>
        public static double MapSizeTopInterval;
        public static double MapSizeDownInterval;

        /// <summary>
        /// 地图比例尺
        /// </summary>
        public static double MapScale;
        /// <summary>
        /// 存储地图参数
        /// </summary>
        /// <param name="MapSizeWidth">成图尺寸宽</param>
        /// <param name="MapSizeHeitht">成图尺寸高</param>
        /// <param name="InlineWidth">内图廓宽</param>
        /// <param name="InlineHeight">内图廓高</param>
        /// <param name="InOutWidth">内外图廓间距</param>
        public static void SetMapPar(double _PaperWidth, double _PaperHeight, double _MapSizeWidth, double _MapSizeHeight, double _InlineWidth, double _InlineHeight, double _InOutLineWidth,double _MapScale)
        {
            PaperWidth = _PaperWidth;
            PaperHeight = _PaperHeight;
            MapSizeWidth = _MapSizeWidth;
            MapSizeHeight = _MapSizeHeight;
            InlineWidth = _InlineWidth;
            InlineHeight = _InlineHeight;
            InOutLineWidth = _InOutLineWidth;
            MapScale = _MapScale;
        }
        //打开文件数据库
        public static void OpenGDBFile(GApplication app, string fullFileName)
        {
            if (app.Workspace != null)
            {
                MessageBox.Show("已经打开工作区，请先关闭工作区!");
                return;
            }
            if (!GApplication.GDBFactory.IsWorkspace(fullFileName))
            {
                MessageBox.Show("不是有效地GDB文件");
            }
            IWorkspace ws = GApplication.GDBFactory.OpenFromFile(fullFileName, 0);
            if (GWorkspace.IsWorkspace(ws))
            {
                app.OpenESRIWorkspace(ws);
            }
            else
            {
                app.InitESRIWorkspace(ws);
            }
        }

        //模板匹配
        public static void MatchLayer(GApplication app, ILayer layer, IGroupLayer parent, DataTable dtLayerRule)
        {
            if (parent == null)
            {
                app.Workspace.Map.AddLayer(layer);
            }
            else
            {
                (parent as IGroupLayer).Add(layer);
            }

            if (layer is IGroupLayer)
            {
                var l = (layer as ICompositeLayer);

                List<ILayer> layers = new List<ILayer>();
                for (int i = 0; i < l.Count; i++)
                {
                    layers.Add(l.get_Layer(i));
                }
                (layer as IGroupLayer).Clear();
                foreach (var item in layers)
                {
                    MatchLayer(app, item, layer as IGroupLayer, dtLayerRule);
                }
            }
            else
            {
                string name = ((layer as IDataLayer2).DataSourceName as IDatasetName).Name;
                if (layer is IFeatureLayer)
                {
                    if ((app.Workspace.EsriWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, name))
                    {
                        IFeatureClass fc = (app.Workspace.EsriWorkspace as IFeatureWorkspace).OpenFeatureClass(name);
                        (layer as IFeatureLayer).FeatureClass = fc;


                        DataRow[] drArray = dtLayerRule.Select().Where(i => i["图层"].ToString().Trim() == name).ToArray();
                        if (drArray.Length != 0)
                        {
                            for (int i = 0; i < drArray.Length; i++)
                            {
                                string ruleFieldName = drArray[i]["RuleIDFeildName"].ToString();
                                object ruleID = drArray[i]["RuleID"];
                                object whereClause = drArray[i]["定义查询"];

                                IQueryFilter qf = new QueryFilterClass();
                                qf.WhereClause = whereClause.ToString();

                                try
                                {
                                    IFeatureCursor fCursor = fc.Update(qf, true);
                                    IFeature f = null;
                                    while ((f = fCursor.NextFeature()) != null)
                                    {
                                        f.set_Value(fc.FindField(ruleFieldName), ruleID);
                                        fCursor.UpdateFeature(f);
                                    }
                                    Marshal.ReleaseComObject(fCursor);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Trace.WriteLine(ex.Message);
                                    System.Diagnostics.Trace.WriteLine(ex.Source);
                                    System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                                    //MessageBox.Show(ex.Message);
                                }

                            }
                        }
                    }
                }
                else if (layer is IRasterLayer)
                {
                    if ((app.Workspace.EsriWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTRasterDataset, name))
                    {
                        (layer as IRasterLayer).CreateFromRaster((app.Workspace.EsriWorkspace as IRasterWorkspaceEx).OpenRasterDataset(name).CreateDefaultRaster());
                    }
                }
            }
        }
        public static void MatchLayerXJ(GApplication app, ILayer layer, IGroupLayer parent, DataTable dtLayerRule)
        {
            if (parent == null)
            {
                app.Workspace.Map.AddLayer(layer);
            }
            else
            {
                (parent as IGroupLayer).Add(layer);
            }

            if (layer is IGroupLayer)
            {
                var l = (layer as ICompositeLayer);

                List<ILayer> layers = new List<ILayer>();
                for (int i = 0; i < l.Count; i++)
                {
                    layers.Add(l.get_Layer(i));
                }
                (layer as IGroupLayer).Clear();
                foreach (var item in layers)
                {
                    MatchLayer(app, item, layer as IGroupLayer, dtLayerRule);
                }
            }
            else
            {
                string name = ((layer as IDataLayer2).DataSourceName as IDatasetName).Name;
                if (layer is IFeatureLayer)
                {
                    if ((app.Workspace.EsriWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, name))
                    {
                        IFeatureClass fc = (app.Workspace.EsriWorkspace as IFeatureWorkspace).OpenFeatureClass(name);
                        (layer as IFeatureLayer).FeatureClass = fc;


                        DataRow[] drArray = dtLayerRule.Select().Where(i => i["映射图层"].ToString().Trim() == name).ToArray();
                        if (drArray.Length != 0)
                        {
                            for (int i = 0; i < drArray.Length; i++)
                            {
                                string ruleFieldName = drArray[i]["RuleIDFeildName"].ToString();
                                object ruleID = drArray[i]["RuleID"];
                                object whereClause = drArray[i]["定义查询"];

                                IQueryFilter qf = new QueryFilterClass();
                                qf.WhereClause = whereClause.ToString();

                                try
                                {
                                    IFeatureCursor fCursor = fc.Update(qf, true);
                                    IFeature f = null;
                                    while ((f = fCursor.NextFeature()) != null)
                                    {
                                        f.set_Value(fc.FindField(ruleFieldName), ruleID);
                                        fCursor.UpdateFeature(f);
                                    }
                                    Marshal.ReleaseComObject(fCursor);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Trace.WriteLine(ex.Message);
                                    System.Diagnostics.Trace.WriteLine(ex.Source);
                                    System.Diagnostics.Trace.WriteLine(ex.StackTrace);
                                    //MessageBox.Show(ex.Message);
                                }

                            }
                        }
                    }
                }
                else if (layer is IRasterLayer)
                {
                    if ((app.Workspace.EsriWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTRasterDataset, name))
                    {
                        (layer as IRasterLayer).CreateFromRaster((app.Workspace.EsriWorkspace as IRasterWorkspaceEx).OpenRasterDataset(name).CreateDefaultRaster());
                    }
                }
            }
        }

        //GP调试信息
        public static string ReturnGPMessages(Geoprocessor gp)
        {
            string msg = "";
            if (gp.MessageCount > 0)
            {
                for (int i = 0; i <= gp.MessageCount - 1; i++)
                {
                    msg += gp.GetMessage(i);
                }
            }
            return msg;
        }

        /// <summary>
        /// 裁切栅格数据集
        /// </summary>
        /// <param name="dataset"></param>
        /// <param name="clipGeo"></param>
        /// <returns></returns>
        public static IRaster clipRaterDataset(IRasterDataset dataset, IGeometry clipGeo_,double dis)
        {
            IRaster result = null;
            try
            {
               

                IRaster pRaster = dataset.CreateDefaultRaster();
                IRasterProps pProps = pRaster as IRasterProps;
                object cellSizeProvider = pProps.MeanCellSize().X;
                IGeoDataset pGeoDataset = pRaster as IGeoDataset;

                //对裁切要素进行投影变换
                clipGeo_.Project(pGeoDataset.SpatialReference);
                IGeometry clipGeo=clipGeo_;
                if (dis > 0)
                    clipGeo = (clipGeo_ as ITopologicalOperator).Buffer(dis);
                //裁切
                (clipGeo as ITopologicalOperator).Simplify();
                IGeometryCollection gc = clipGeo as IGeometryCollection;
                if (gc.GeometryCount > 1)
                {
                    #region 去洞处理
                    IRing maxring = null;
                    double area = 0;
                    for (int i = 0; i < gc.GeometryCount; i++)
                    {
                        IRing ring = gc.get_Geometry(i) as IRing;
                        IArea iarea = ring as IArea;
                        if (Math.Abs(iarea.Area) > area)
                        {
                            maxring = ring;
                            area = iarea.Area;
                        }
                    }
                    PolygonClass polygon = new PolygonClass();
                    polygon.AddGeometry(maxring);
                    polygon.Simplify();
                    clipGeo = polygon;
                    #endregion
                }

                IExtractionOp2 pExtractionOp = new RasterExtractionOpClass();
                IRasterAnalysisEnvironment pRasterAnaEnvi = pExtractionOp as IRasterAnalysisEnvironment;
                pRasterAnaEnvi.SetCellSize(esriRasterEnvSettingEnum.esriRasterEnvValue, ref cellSizeProvider);
                object extentProvider = clipGeo.Envelope;
                object snapRasterData = Type.Missing;
                pRasterAnaEnvi.SetExtent(esriRasterEnvSettingEnum.esriRasterEnvValue, ref extentProvider, ref snapRasterData);
                IGeoDataset pOutputDataset = pExtractionOp.Polygon(pGeoDataset, clipGeo as IPolygon, true);

                //属性设置
                result = pOutputDataset as IRaster;
                IRasterProps resRasterProp = result as IRasterProps;
                resRasterProp.NoDataValue = pProps.NoDataValue;
                resRasterProp.PixelType = pProps.PixelType;
            }
            catch(Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.Source);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);
            }
            return result;
        }

        /// <summary>
        /// 从工作空间中获取DEM栅格数据
        /// </summary>
        /// <returns></returns>
        public static IRasterDataset getDEMFromWorkspace(IWorkspace ws, string DEMDatasetName = "DEM")
        {
            IRasterWorkspaceEx rasterWorkspace = ws as IRasterWorkspaceEx;
            if (null == rasterWorkspace)
                return null;

            if (!(ws as IWorkspace2).get_NameExists(esriDatasetType.esriDTRasterDataset, DEMDatasetName))
                return null;

            return rasterWorkspace.OpenRasterDataset(DEMDatasetName);
        }


        /// <summary>
        /// 根据文件对象返回内容节点
        /// </summary>
        /// <param name="f">文件对象</param>
        /// <returns>内容节点</returns>
        public static XElement FromFileInfo(FileInfo f)
        {
           
            {
                XDocument doc = XDocument.Load(f.FullName);
                return doc.Element("Template").Element("Content");
            }
        }

        /// <summary>
        /// 读取mdb数据库表
        /// </summary>
        /// <param name="mdbFilePath"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static DataTable ReadToDataTable(string mdbFilePath, string tableName)
        {
            DataTable pDataTable = new DataTable();
            IWorkspaceFactory pWorkspaceFactory = new AccessWorkspaceFactory();
            if (!File.Exists(mdbFilePath))
                return null;
            IWorkspace pWorkspace = pWorkspaceFactory.OpenFromFile(mdbFilePath, 0);
            IEnumDataset pEnumDataset = pWorkspace.get_Datasets(esriDatasetType.esriDTTable);
            pEnumDataset.Reset();
            IDataset pDataset = pEnumDataset.Next();
            ITable pTable = null;
            while (pDataset != null)
            {
                if (pDataset.Name == tableName)
                {
                    pTable = pDataset as ITable;
                    break;
                }
                pDataset = pEnumDataset.Next();
            }
            System.Runtime.InteropServices.Marshal.ReleaseComObject(pEnumDataset);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(pWorkspace);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(pWorkspaceFactory);
            if (pTable != null)
            {
                ICursor pCursor = pTable.Search(null, false);
                IRow pRow = pCursor.NextRow();
                //添加表的字段信息
                for (int i = 0; i < pRow.Fields.FieldCount; i++)
                {
                    pDataTable.Columns.Add(pRow.Fields.Field[i].Name);
                }
                //添加数据
                while (pRow != null)
                {
                    DataRow dr = pDataTable.NewRow();
                    for (int i = 0; i < pRow.Fields.FieldCount; i++)
                    {
                        object obValue = pRow.get_Value(i);
                        if (obValue != null && !Convert.IsDBNull(obValue))
                        {
                            dr[i] = pRow.get_Value(i);
                        }
                        else
                        {
                            dr[i] = "";
                        }
                    }
                    pDataTable.Rows.Add(dr);
                    pRow = pCursor.NextRow();
                }
                System.Runtime.InteropServices.Marshal.ReleaseComObject(pCursor);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(pTable);
            }
      
            return pDataTable;
        }

        /// <summary>
        /// 返回一个介于min和max之间的随机数
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static double GetRandNum(double min, double max)
        {
            Random r = new Random(Guid.NewGuid().GetHashCode());
            return r.NextDouble() * (max - min) + min;
        }

        //返回附图位置点
        public static IPoint GetFigureMapPoint(IEnvelope pEnvelope, string pos, double size)
        {
            IPoint AnchorPoint = new PointClass();

            size = size / 2.0;
            double xmin = pEnvelope.XMin;
            double xmax = pEnvelope.XMax;
            double ymin = pEnvelope.YMin;
            double ymax = pEnvelope.YMax;
            double x = 0, y = 0;
            switch (pos)
            {
                case "TopLeft":
                    x = xmin + size;
                    y = ymax - size;
                    AnchorPoint.PutCoords(x, y);
                    break;
                case "DownLeft":
                    x = xmin + size;
                    y = ymin + size;
                    AnchorPoint.PutCoords(x, y);
                    break;
                case "TopRight":
                    x = xmax - size;
                    y = ymax - size;
                    AnchorPoint.PutCoords(x, y);
                    break;
                case "DownRight":
                    x = xmax - size;
                    y = ymin + size;
                    AnchorPoint.PutCoords(x, y);
                    break;
            }

            return AnchorPoint;
        }
        public static IPoint GetFigureMapPoint(IEnvelope pEnvelope, string pos)
        {
            IPoint AnchorPoint = new PointClass();
            double size = 0;
            size = size / 2.0;
            double xmin = pEnvelope.XMin;
            double xmax = pEnvelope.XMax;
            double ymin = pEnvelope.YMin;
            double ymax = pEnvelope.YMax;
            double x = 0, y = 0;
            switch (pos)
            {
                case "TopLeft":
                    x = xmin + size;
                    y = ymax - size;
                    AnchorPoint.PutCoords(x, y);
                    break;
                case "DownLeft":
                    x = xmin + size;
                    y = ymin + size;
                    AnchorPoint.PutCoords(x, y);
                    break;
                case "TopRight":
                    x = xmax - size;
                    y = ymax - size;
                    AnchorPoint.PutCoords(x, y);
                    break;
                case "DownRight":
                    x = xmax - size;
                    y = ymin + size;
                    AnchorPoint.PutCoords(x, y);
                    break;
            }

            return AnchorPoint;
        }

        public static IPoint GetfigureMapPointAuto(IEnvelope pEnvelope, IEnvelope Lengend,IGeometry clipGeo, double dx,double dy)
        {
            IPoint AnchorPoint = new PointClass();
            EnvelopeClass envelop = new EnvelopeClass();
          
            dx = dx * 1.0e-3 * GApplication.Application.ActiveView.FocusMap.ReferenceScale;
            dy = dy * 1.0e-3 * GApplication.Application.ActiveView.FocusMap.ReferenceScale;
            envelop.PutCoords(0, 0, dx, dy);
           
            double xmin = pEnvelope.XMin;
            double xmax = pEnvelope.XMax;
            double ymin = pEnvelope.YMin;
            double ymax = pEnvelope.YMax;
            double x = 0;
            double y = 0;
           
            // "TopLeft":
            {
                x = xmin + dx / 2;
                y = ymax - dy / 2;
                AnchorPoint.PutCoords(x, y);
                envelop.CenterAt(AnchorPoint);
                if (Lengend != null)
                {
                    if (envelop.Disjoint(Lengend) && envelop.Disjoint(clipGeo))//不相交
                    {
                        return AnchorPoint;
                    
                    }
                }
            }
           //  "DownLeft":
            {
                x = xmin + dx / 2;
                y = ymin + dy / 2;
                AnchorPoint.PutCoords(x, y);
                envelop.CenterAt(AnchorPoint);
                if (Lengend != null)
                {
                    if (envelop.Disjoint(Lengend) && envelop.Disjoint(clipGeo))//不相交
                    {
                        return AnchorPoint;
                    }
                }
            }
           // "TopRight":
            {
                x = xmax - dx / 2;
                y = ymax - dy / 2;
                AnchorPoint.PutCoords(x, y);
                envelop.CenterAt(AnchorPoint);
                if (Lengend != null)
                {
                    if (envelop.Disjoint(Lengend) && envelop.Disjoint(clipGeo))//不相交
                    {
                        return AnchorPoint;
                    }
                }
            }
           //   "DownRight":
            {
                x = xmax - dx / 2;
                y = ymin + dy / 2;
                AnchorPoint.PutCoords(x, y);
                if (Lengend != null)
                {
                    if (envelop.Disjoint(Lengend) && envelop.Disjoint(clipGeo))//不相交
                    {
                        return AnchorPoint;
                        
                    }
                }
                
            }
            //默认返回右下角
            return AnchorPoint;
        }

        //返回附图位置点
        public static IPoint GetFigureMapPointURe(IEnvelope pEnvelope, string pos, double dx,double dy)
        {
            IPoint AnchorPoint = new PointClass();

            dx =  dx*1.0e-3 * GApplication.Application.ActiveView.FocusMap.ReferenceScale;
            dy =  dy*1.0e-3 * GApplication.Application.ActiveView.FocusMap.ReferenceScale;
           
            double xmin = pEnvelope.XMin;
            double xmax = pEnvelope.XMax;
            double ymin = pEnvelope.YMin;
            double ymax = pEnvelope.YMax;
            double x = 0, y = 0;
            switch (pos)
            {
                case "TopLeft":
                    x = xmin + dx/2;
                    y = ymax - dy/2;
                    AnchorPoint.PutCoords(x, y);
                    break;
                case "DownLeft":
                    x = xmin + dx / 2;
                    y = ymin + dy / 2;
                    AnchorPoint.PutCoords(x, y);
                    break;
                case "TopRight":
                    x = xmax - dx / 2;
                    y = ymax - dy / 2;
                    AnchorPoint.PutCoords(x, y);
                    break;
                case "DownRight":
                    x = xmax - dx / 2;
                    y = ymin + dy / 2;
                    AnchorPoint.PutCoords(x, y);
                    break;
            }

            return AnchorPoint;
        }

        /// <summary>
        /// 设置pMap中所有注记要素类的的参考比例尺
        /// </summary>
        /// <param name="pMap"></param>
        /// <param name="referenceScale"></param>
        public static void UpdateAnnoRefScale(IMap pMap, double referenceScale)
        {
            for (int i = 0; i < pMap.LayerCount; i++)
            {
                var l = pMap.get_Layer(i);

                if (l is IFDOGraphicsLayer)
                {
                    IFeatureClass pfcl = (l as IFeatureLayer).FeatureClass;
                    IAnnoClass pAnno = pfcl.Extension as IAnnoClass;
                    IAnnoClassAdmin3 pAnnoAdmin = pAnno as IAnnoClassAdmin3;
                    if (pAnno.ReferenceScale != referenceScale)
                    {
                        pAnnoAdmin.AllowSymbolOverrides = true;
                        pAnnoAdmin.ReferenceScale = referenceScale;
                        pAnnoAdmin.UpdateProperties();
                    }
                }
            }
        }

        /// <summary>
        /// 获取工作空间中所有要素类的名称集合
        /// </summary>
        /// <param name="pWorkspace"></param>
        /// <returns></returns>
        public static IList<string> getFeatureClassNameList(IWorkspace pWorkspace)
        {
            IList<string> namelist = new List<string>();

            if (null == pWorkspace)
                return namelist;

            IEnumDataset pEnumDataset = pWorkspace.get_Datasets(esriDatasetType.esriDTAny);
            pEnumDataset.Reset();
            IDataset pDataset = pEnumDataset.Next();
            while (pDataset != null)
            {
                if (pDataset is IFeatureDataset)//要素数据集
                {
                    IFeatureWorkspace pFeatureWorkspace = (IFeatureWorkspace)pWorkspace;
                    IFeatureDataset pFeatureDataset = pFeatureWorkspace.OpenFeatureDataset(pDataset.Name);
                    IEnumDataset pEnumDatasetF = pFeatureDataset.Subsets;
                    pEnumDatasetF.Reset();
                    IDataset pDatasetF = pEnumDatasetF.Next();
                    while (pDatasetF != null)
                    {
                        if (pDatasetF is IFeatureClass)//要素类
                        {
                            IFeatureClass fc = pFeatureWorkspace.OpenFeatureClass(pDatasetF.Name);
                            if (fc != null)
                                namelist.Add(fc.AliasName);
                        }

                        pDatasetF = pEnumDatasetF.Next();
                    }

                    System.Runtime.InteropServices.Marshal.ReleaseComObject(pEnumDatasetF);
                }
                else if (pDataset is IFeatureClass)//要素类
                {
                    IFeatureWorkspace pFeatureWorkspace = (IFeatureWorkspace)pWorkspace;

                    IFeatureClass fc = pFeatureWorkspace.OpenFeatureClass(pDataset.Name);
                    if (fc != null)
                        namelist.Add(fc.AliasName);
                }
                else
                {

                }

                pDataset = pEnumDataset.Next();

            }

            System.Runtime.InteropServices.Marshal.ReleaseComObject(pEnumDataset);

            return namelist;
        }

        /// <summary>
        /// 从样式中获取某个指定的标记符号
        /// </summary>
        /// <param name="stylePath"></param>
        /// <param name="symbolName"></param>
        /// <returns></returns>
        public static IMarkerSymbol GetMarkerSymbolFromStyleFile(string stylePath, string symbolName)
        {
            IMarkerSymbol markerSymbol = GetSymbolFromStyleFile(stylePath, "Marker Symbols", symbolName) as IMarkerSymbol;
            if(markerSymbol == null)
            {
                markerSymbol = GetSymbolFromStyleFile(stylePath, "标记符号", symbolName) as IMarkerSymbol;
            }

            return markerSymbol;
        }

        /// <summary>
        /// 从样式中获取某个指定的类型的符号
        /// </summary>
        /// <param name="stylePath">符号样式文件路径</param>
        /// <param name="className">样式类名称</param>
        /// <param name="symbolName">样式名称</param>
        /// <returns></returns>
        public static object GetSymbolFromStyleFile(string stylePath, string className, string symbolName)
        {
            object result = null;

            try
            {
                //获取现有的styles
                IStyleGallery styleGallery = new StyleGalleryClass();
                IStyleGalleryStorage styleGalleryStorage = styleGallery as IStyleGalleryStorage;
                int styleCount = styleGalleryStorage.FileCount;

                //判断所选择的文件是否已经装载，若尚未加载，则加载
                bool styleExit = false;
                for (int i = 0; i < styleCount; ++i)
                {
                    if (styleGalleryStorage.get_File(i) == stylePath)
                    {
                        styleExit = true;
                        break;
                    }
                }
                if (!styleExit)
                    styleGalleryStorage.AddFile(stylePath);

                IEnumStyleGalleryItem enumStyleItem = styleGallery.get_Items(className, stylePath, "");
                enumStyleItem.Reset();
                IStyleGalleryItem styleItem = null;
                while( (styleItem = enumStyleItem.Next()) != null)
                {
                    if(styleItem.Name == symbolName)
                    {
                        result = styleItem.Item;
                        break;
                    }
                }
                Marshal.ReleaseComObject(enumStyleItem);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);
                System.Diagnostics.Trace.WriteLine(ex.Source);
            }

            return result;
        }

        /// <summary>
        /// 增加一个文本字段
        /// </summary>
        /// <param name="fc"></param>
        /// <param name="newFieldName"></param>
        /// <param name="fieldLen"></param>
        public static void AddField(IFeatureClass fc, string newFieldName, int fieldLen = 1)
        {
            IField pField = new FieldClass();
            IFieldEdit pFieldEdit = pField as IFieldEdit;
            pFieldEdit.Name_2 = newFieldName;
            pFieldEdit.AliasName_2 = newFieldName;
            pFieldEdit.Length_2 = fieldLen;
            pFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;

            IClass pTable = fc as IClass;
            pTable.AddField(pField);
        }
        /// <summary>  
        /// IGeometry转成JSON字符串  
        /// </summary> 
        public static string GeometryToJsonString(ESRI.ArcGIS.Geometry.IGeometry geometry)
        {
            ESRI.ArcGIS.esriSystem.IJSONWriter jsonWriter = new ESRI.ArcGIS.esriSystem.JSONWriterClass();
            jsonWriter.WriteToString();

            ESRI.ArcGIS.Geometry.JSONConverterGeometryClass jsonCon = new ESRI.ArcGIS.Geometry.JSONConverterGeometryClass();
            jsonCon.WriteGeometry(jsonWriter, null, geometry, false);

            return Encoding.UTF8.GetString(jsonWriter.GetStringBuffer());
        }

        /// <summary>  
        /// JSON字符串转成IGeometry  
        /// </summary>  
        public static ESRI.ArcGIS.Geometry.IGeometry GeometryFromJsonString(string strJson, ESRI.ArcGIS.Geometry.esriGeometryType type)
        {
            return GeometryFromJsonString(strJson, type, false, false);
        }

        /// <summary>  
        /// JSON字符串转成IGeometry  
        /// </summary>  
        public static ESRI.ArcGIS.Geometry.IGeometry GeometryFromJsonString(string strJson, ESRI.ArcGIS.Geometry.esriGeometryType type, bool bHasZ, bool bHasM)
        {
            try
            {
                if (strJson == "")
                    return null;
                if (strJson.Contains("paths"))
                    type = esriGeometryType.esriGeometryPolyline;
                if (strJson.Contains("rings"))
                    type = esriGeometryType.esriGeometryPolygon;
                ESRI.ArcGIS.esriSystem.IJSONReader jsonReader = new ESRI.ArcGIS.esriSystem.JSONReaderClass();
                jsonReader.ReadFromString(strJson);

                ESRI.ArcGIS.Geometry.JSONConverterGeometryClass jsonCon = new ESRI.ArcGIS.Geometry.JSONConverterGeometryClass();
                return jsonCon.ReadGeometry(jsonReader, type, bHasZ, bHasM);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>  
        /// JSON字符串转成IGeometry  
        /// </summary>  
        public static ESRI.ArcGIS.Geometry.IGeometry GeometryFromJsonString(string strJson)
        {
            ESRI.ArcGIS.Geometry.esriGeometryType type = ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPoint;
            if (strJson == "")
            {
                return null;
            }
            if (strJson.Contains("paths"))
            {
                type = ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolyline;
            }
            if (strJson.Contains("rings"))
            {
                type = ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolygon;
            }
            return GeometryFromJsonString(strJson, type, false, false);
        }

        /// <summary>
        /// 将所有图层的Attach字段值为非空的要素赋值为空，且裁切面的几何赋值为纸张页面
        /// </summary>
        /// <param name="ws"></param>
        /// <param name="fcNames"></param>
        /// <param name="fieldName"></param>
        public static void NoMainRegionAndAdjRegion(IFeatureWorkspace ws, List<string> fcNames, string fieldName = "ATTACH")
        {
            foreach (var fcName in fcNames)
            {
                if(!(ws as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, fcName))
                    continue;

                var fc = ws.OpenFeatureClass(fcName);
                int index = fc.FindField(fieldName);
                if( index == -1)
                    continue;
                
                IQueryFilter qf = new QueryFilterClass();
                qf.WhereClause = string.Format("{0} is not null", fieldName);
                IFeatureCursor feCursor = fc.Update(qf, true);
                IFeature f = null;
                while ((f = feCursor.NextFeature()) != null)
                {
                    f.set_Value(index, DBNull.Value);

                    feCursor.UpdateFeature(f);
                }
                Marshal.ReleaseComObject(feCursor);

                //if (fcName.ToUpper() == "CLIPBOUNDARY")//将裁切面几何替换为纸张页面几何
                //{
                //    //获取纸张页面几何
                //    qf.WhereClause = "TYPE = '页面'";
                //    feCursor = fc.Search(qf, true);
                //    f = feCursor.NextFeature();
                //    if (f == null)
                //        continue;
                //    IGeometry pageGeo = f.ShapeCopy;
                //    Marshal.ReleaseComObject(feCursor);

                //    //替换几何
                //    qf.WhereClause = "TYPE ='裁切面'";
                //    feCursor = fc.Update(qf, true);
                //    f = feCursor.NextFeature();
                //    if (f != null)
                //    {
                //        f.Shape = pageGeo;

                //        feCursor.UpdateFeature(f);
                //    }
                //    Marshal.ReleaseComObject(feCursor);
                //}
            }
            
        }

        /// <summary>
        /// 获取应用程序默认路径
        /// </summary>
        public static string GetAppDataPath()
        {
            if (System.Environment.OSVersion.Version.Major <= 5)
            {
                return System.IO.Path.GetFullPath(
                    System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) + @"\..");
            }

            var dp = System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var di = new System.IO.DirectoryInfo(dp);
            var ds = di.GetDirectories("SMGI");
            if (ds == null || ds.Length == 0)
            {
                var sdi = di.CreateSubdirectory("SMGI");
                return sdi.FullName;
            }
            else
            {
                return ds[0].FullName;
            }
        }

        /// <summary>
        /// 打开shp文件
        /// </summary>
        /// <param name="shpFileName"></param>
        /// <returns></returns>
        public static IFeatureClass OpenSHPFile(string shpFileName)
        {
            IWorkspaceFactory wsFactory = new ShapefileWorkspaceFactory();
            IFeatureWorkspace featureWS = wsFactory.OpenFromFile(System.IO.Path.GetDirectoryName(shpFileName), 0) as IFeatureWorkspace;

            IFeatureClass fc = featureWS.OpenFeatureClass(System.IO.Path.GetFileNameWithoutExtension(shpFileName));
            Marshal.ReleaseComObject(featureWS);
            Marshal.ReleaseComObject(wsFactory);
            return fc;
        }


        public static void ExportFeatureClassToShapefile(IFeatureClass fc, string shpFullPath)
        {
            string filePath = null;
            string fileName = null;
            filePath = System.IO.Path.GetDirectoryName(shpFullPath);
            fileName = System.IO.Path.GetFileNameWithoutExtension(shpFullPath);
            IWorkspaceFactory wsf = new ShapefileWorkspaceFactoryClass();
            IWorkspace outWorkspace = wsf.OpenFromFile(filePath, 0);

            IDataset inDataSet = fc as IDataset;
            IFeatureClassName inFCName = inDataSet.FullName as IFeatureClassName;
            IWorkspace inWorkspace = inDataSet.Workspace;


            IDataset outDataSet = outWorkspace as IDataset;
            IWorkspaceName outWorkspaceName = outDataSet.FullName as IWorkspaceName;
            IFeatureClassName outFCName = new FeatureClassNameClass();
            IDatasetName outDataSetName = outFCName as IDatasetName;
            outDataSetName.WorkspaceName = outWorkspaceName;
            outDataSetName.Name = fileName;

            IFieldChecker fieldChecker = new FieldCheckerClass();
            fieldChecker.InputWorkspace = inWorkspace;
            fieldChecker.ValidateWorkspace = outWorkspace;
            IFields fields = fc.Fields;
            IFields outFields = null;
            IEnumFieldError enumFieldError = null;
            fieldChecker.Validate(fields, out enumFieldError, out outFields);

            IFeatureDataConverter featureDataConverter = new FeatureDataConverterClass();
            featureDataConverter.ConvertFeatureClass(inFCName, null, null, outFCName, null, outFields, "", 100, 0);
        }


        public static bool DeleteShapeFile(string fileName)
        {
            if (File.Exists(fileName))
            {
                try
                {
                    IWorkspaceFactory workspaceFactory = new ShapefileWorkspaceFactory();//文件夹
                    IFeatureWorkspace featureWorkspace = workspaceFactory.OpenFromFile(System.IO.Path.GetDirectoryName(fileName), 0) as IFeatureWorkspace;
                    IFeatureClass fc = featureWorkspace.OpenFeatureClass(System.IO.Path.GetFileName(fileName));
                    (fc as IDataset).Delete();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine(ex.Message);
                    System.Diagnostics.Trace.WriteLine(ex.Source);
                    System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                    MessageBox.Show(string.Format("删除文件失败【{0}】!", fileName));

                    throw ex;
                }
            }

            return true;
        }

        /// <summary>
        /// 创建临时工作空间
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns></returns>
        public static IWorkspace createTempWorkspace(string fullPath)
        {
            IWorkspace pWorkspace = null;
            IWorkspaceFactory2 wsFactory = new FileGDBWorkspaceFactoryClass();

            if (!Directory.Exists(fullPath))
            {

                IWorkspaceName pWorkspaceName = wsFactory.Create(System.IO.Path.GetDirectoryName(fullPath),
                    System.IO.Path.GetFileName(fullPath), null, 0);
                IName pName = (IName)pWorkspaceName;
                pWorkspace = (IWorkspace)pName.Open();
            }
            else
            {
                pWorkspace = wsFactory.OpenFromFile(fullPath, 0);
            }



            return pWorkspace;
        }

        public static DataTable ReadAccesstoDataTable(string mdbPath, string tableName)
        {
            DataTable dt = new DataTable();
            try
            {
                DataRow dr;
                //1、树立衔接C#操作Access之读取mdb 
                string strConn = "Provider=Microsoft.Jet.OLEDB.4.0; Data Source=" + mdbPath + ";jet oledb:database password=123;";
                //string strConn = @"Provider=Microsoft.Jet.OLEDB.4.0;DataSource=" + mdbPath + ";Jet OLEDB:Database Password=123";
                OleDbConnection odcConnection = new OleDbConnection(strConn);
                //2、翻开衔接C#操作Access之读取mdb 
                odcConnection.Open();
                //树立SQL查询 
                OleDbCommand odCommand = odcConnection.CreateCommand();
                //3、输入查询句子C#操作Access之读取mdb 
                odCommand.CommandText = "select * from " + tableName;
                //树立读取 
                OleDbDataReader odrReader = odCommand.ExecuteReader();
                //查询并显现数据 
                int size = odrReader.FieldCount;
                for (int i = 0; i < size; i++)
                {
                    DataColumn dc;
                    dc = new DataColumn(odrReader.GetName(i));
                    dt.Columns.Add(dc);
                }
                while (odrReader.Read())
                {
                    dr = dt.NewRow();
                    for (int i = 0; i < size; i++)
                    {
                        dr[odrReader.GetName(i)] =
                        odrReader[odrReader.GetName(i)].ToString();
                    }
                    dt.Rows.Add(dr);
                }
                //封闭衔接C#操作Access之读取mdb 
                odrReader.Close();
                odcConnection.Close();
                //success = true;
                return dt;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                //success = false;
                return dt;
            }
        }
    
    }
}
