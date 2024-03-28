using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Display;
using SMGI.Common;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Linq;
using ESRI.ArcGIS.DataManagementTools;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geoprocessor;
using ESRI.ArcGIS.Maplex;
using SMGI.DxForm;
using Helper = SMGI.Common.Helper;

namespace SMGI.Plugin.BaseFunction
{
    public class DataCartoSwitch : SMGICommand
    {
        public DataTable ChinEnlishLayerMappingTable;//中英文图层名映射表
        public Dictionary<string, string> ChinEnlishLayerDic = new Dictionary<string, string>();//英文-中文图层名字典
        Dictionary<string, string> templateGdbPath2themName = new Dictionary<string, string>();//模板GDB路径-专题名称


        public DataCartoSwitch()
        {
            m_caption = "符号化开关";
        }

        public override bool Enabled
        {
            get { return m_Application != null && m_Application.Workspace != null; }
        }


        public override void OnClick()
        {
            using (WaitOperation wo = m_Application.SetBusy())//利用using确保wo在执行完后会自动关闭
            {
                var lys = m_Application.Workspace.LayerManager.GetLayer(l => (l is IGeoFeatureLayer)).Select(i => (IFeatureLayer)i).ToList();
                var cartomxd = m_Application.Template.Root + @"\" + m_Application.Template.Content.Element("CartoMxd").Value;
                IMapDocument mapDoc = new MapDocumentClass();
                mapDoc.Open(cartomxd);
                var rs = mapDoc.Map[0].ReferenceScale;
                double referenceScale = m_Application.MapControl.Map.ReferenceScale;
                bool flag = false;
                var gdbmxd = m_Application.Template.Root + @"\" + m_Application.Template.Content.Element("GdbMxd").Value;//符号化对应的模板
                mapDoc = null;
                if (File.Exists(gdbmxd))
                {
                    mapDoc = new MapDocumentClass();
                    if (mapDoc.IsPresent[gdbmxd]) mapDoc.Open(gdbmxd);
                }

                if (mapDoc == null || mapDoc.MapCount == 0)  //没有模板
                {
                    var rand = new Random();
                    foreach (var ly in lys)
                    {
                        if (ly.FeatureClass.AliasName.StartsWith("VEGA_") || ly.FeatureClass.AliasName.EndsWith("普色")
                            || ly.FeatureClass.AliasName.EndsWith("注记") || ly.FeatureClass.AliasName.EndsWith("底色")) continue;

                        var gfl = (IGeoFeatureLayer)ly;
                        if (gfl.Renderer is IRepresentationRenderer)
                        {
                            wo.SetText(string.Format("正在开启符号化"));
                            SimpleRenderer src = new SimpleRendererClass();
                            ISymbol sym;
                            if (gfl.FeatureClass.ShapeType == esriGeometryType.esriGeometryPoint)
                            {
                                sym = new SimpleMarkerSymbolClass();
                                ((ISimpleMarkerSymbol)sym).Color = new RgbColorClass { Red = rand.Next(255), Green = rand.Next(255), Blue = rand.Next(255) };
                            }
                            else if (gfl.FeatureClass.ShapeType == esriGeometryType.esriGeometryPolyline)
                            {
                                sym = new SimpleLineSymbolClass();
                                ((ISimpleLineSymbol)sym).Color = new RgbColorClass { Red = rand.Next(255), Green = rand.Next(255), Blue = rand.Next(255) };
                            }
                            else
                            {
                                sym = new SimpleFillSymbolClass();
                                ((ISimpleFillSymbol)sym).Color = new RgbColorClass { Red = rand.Next(255), Green = rand.Next(255), Blue = rand.Next(255) };
                            }
                            src.Symbol = sym;
                            gfl.Renderer = src as IFeatureRenderer;
                        }
                        else if (!(gfl.Renderer is IRepresentationRenderer) && !m_Application.Workspace.EsriWorkspace.PathName.EndsWith("_Ecarto.gdb"))
                        {
                            DataStructUpdateForm dataUpdateFrom = new DataStructUpdateForm(m_Application);
                            dataUpdateFrom.Text = "地图符号化设置";
                            if (DialogResult.OK == dataUpdateFrom.ShowDialog())
                            {
                                //初始化专题数据信息

                                string sourceFileGDB = dataUpdateFrom.SourceGDBFile;
                                string outputFileGDB = dataUpdateFrom.OutputGDBFile;
                                int mapScale = int.Parse(dataUpdateFrom.Mapscale);
                                //获取配置信息
                                string mxdFullFileName = dataUpdateFrom.tbMxdFile.Text;
                                string ruleMatchFileName = dataUpdateFrom.tbLayerRuleFile.Text;
                                string str = mxdFullFileName;  //文件名称中设计多个特定符号；
                                str = str.Substring(0, str.LastIndexOf("."));
                                string templateFileName = str + ".gdb";
                                //string mxdFullFileName = EnvironmentSettings.getMxdFullFileName(m_Application);
                                //string ruleMatchFileName = EnvironmentSettings.getLayerRuleDBFileName(m_Application);

                                DataTable ruleMDB = Helper.ReadToDataTable(ruleMatchFileName, "图层对照规则");

                                //TEST************************************************
                                //获取专题模板配置信息
                                Dictionary<string, string> themTemplateFCName2themName = new Dictionary<string, string>();//图层名-专题名称
                                Dictionary<string, IFeatureClass> themTemplateFCName2FC = new Dictionary<string, IFeatureClass>();//图层名-模板要素类（空）
                                Dictionary<string, DataTable> themName2DT = new Dictionary<string, DataTable>();//专题名称-图层规则表
                                #region 获取当前模板中各专题图层以及专题规则
                                List<ThemathicInfo> thematicList = InitialThematicList(GApplication.Application.Template.Root + @"\专题\ThematicRulePath.xml");//读取专题模板路径
                                //遍历所有专题要素类，获取待符号化数据中存在的要素类
                                Type factoryType = Type.GetTypeFromProgID("esriDataSourcesGDB.FileGDBWorkspaceFactory");
                                IWorkspaceFactory workspaceFactory = (IWorkspaceFactory)Activator.CreateInstance(factoryType);
                                IWorkspace wsSource = workspaceFactory.OpenFromFile(sourceFileGDB, 0);
                                foreach (ThemathicInfo info in thematicList)
                                {
                                    IWorkspace wsthematic = null;
                                    foreach (string fcname in info.name2AliasName.Keys)
                                    {
                                        if ((wsSource as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, fcname))
                                        {
                                            //下载了该专题图层
                                            //记录信息
                                            themTemplateFCName2themName.Add(fcname, info.name);
                                            if (wsthematic == null) wsthematic = workspaceFactory.OpenFromFile(info.RuleDirc + "\\" + "template.gdb", 0);
                                            if ((wsthematic as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, fcname))
                                            {
                                                themTemplateFCName2FC.Add(fcname, (wsthematic as IFeatureWorkspace).OpenFeatureClass(fcname));
                                                if (!themTemplateFCName2FC.ContainsKey(info.annoLayer.Key))
                                                {
                                                    //添加该专题的注记图层
                                                    if ((wsthematic as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, info.annoLayer.Key))
                                                    {
                                                        themTemplateFCName2FC.Add(info.annoLayer.Key, (wsthematic as IFeatureWorkspace).OpenFeatureClass(info.annoLayer.Key));
                                                        themTemplateFCName2themName.Add(info.annoLayer.Key, info.name);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                //加载专题对照规则
                                foreach (var info in thematicList)
                                {
                                    if (!themName2DT.ContainsKey(info.name) && themTemplateFCName2themName.ContainsValue(info.name))
                                    {
                                        string rulepath = info.RuleDirc + "//规则对照.mdb";//  string rulepath = GApplication.Application.Template.Root + "\\专题\\" + "社会经济发展专题库\\经济建设" + "\\规则对照.mdb";
                                        DataTable dtLayerRule = CommonMethods.ReadAccesstoDataTable(rulepath, "图层对照规则");
                                        themName2DT[info.name] = dtLayerRule;
                                    }
                                }

                                if (themTemplateFCName2FC.Count != 0)//专题模板不为空，标记ThemData为true
                                {
                                    CommonMethods.ThemData = true;
                                }
                                #endregion
                                //TEST************************************************

                                wo.SetText("正在进行数据结构升级...");
                                DataStructUpgradeClass dsUpgrade = new DataStructUpgradeClass(m_Application, sourceFileGDB, outputFileGDB);
                                dsUpgrade.templateFullFileName = templateFileName;
                                dsUpgrade.RuleMDB = ruleMDB;

                                //TEST***********************************************
                                dsUpgrade._themTemplateFCName2themName = themTemplateFCName2themName;
                                dsUpgrade._themTemplateFCName2FC = themTemplateFCName2FC;
                                dsUpgrade._themName2DT = themName2DT;
                                //TEST************************************************

                                if (!dsUpgrade.UpgradeBaseData(wo))
                                {
                                    return;
                                }

                                if (!dataUpdateFrom.AttachMap)//如果符号化后不区分主邻区，则将所有图层的Attach字段值为非空的要素赋值为空，且裁切面的几何赋值为纸张页面
                                {
                                    IWorkspaceFactory sourceWSFactory = new FileGDBWorkspaceFactoryClass();
                                    IWorkspace ws = sourceWSFactory.OpenFromFile(outputFileGDB, 0);

                                    List<string> fcNames = new List<string>();
                                    List<string> fdtNames = new List<string>();

                                    m_Application.GetDatasetNames(ws, ref fcNames, ref fdtNames);
                                    CommonMethods.NoMainRegionAndAdjRegion(ws as IFeatureWorkspace, fcNames);
                                }

                                //wo.SetText("正在加载升级后的文件数据库...");
                                //CommonMethods.OpenGDBFile(m_Application, outputFileGDB);


                                //IMapDocument pMapDoc = new MapDocumentClass();
                                //pMapDoc.Open(mxdFullFileName, "");
                                //if (pMapDoc.MapCount == 0)//如果地图模板为空
                                //{
                                //    MessageBox.Show("地图模板不能为空！");
                                //    return;
                                //}
                                ////底图规则
                                //DataTable dtLayerRule = ruleMDB;

                                ////专题规则
                                //InitThematicLyrRules(dsUpgrade.ThematicLyrNames);
                                //if (dtLayerRule.Rows.Count == 0)
                                //{
                                //    MessageBox.Show("没有找到图层对照规则！");
                                //    return;
                                //}

                                ////获取模板中的地图
                                //IActiveView view = m_Application.ActiveView;
                                //IMap map = view.FocusMap;
                                //IMap templateMap = pMapDoc.get_Map(0);

                                //string engineName = templateMap.AnnotationEngine.Name;
                                //if (engineName.Contains("Maplex"))
                                //{
                                //    IAnnotateMap sm = new MaplexAnnotateMapClass();
                                //    map.AnnotationEngine = sm;
                                //}
                                //else
                                //{
                                //    map.AnnotationEngine = templateMap.AnnotationEngine;
                                //}
                                //map.ReferenceScale = Convert.ToInt32(mapScale);
                                //templateMap.SpatialReference = map.SpatialReference;

                                //wo.SetText("正在匹配制图表达......");
                                //m_Application.Workspace.LayerManager.Map.ClearLayers();
                                //List<ILayer> layers = new List<ILayer>();
                                ////加载底图模板图层
                                //for (int i = templateMap.LayerCount - 1; i >= 0; i--)
                                //{
                                //    var l = templateMap.get_Layer(i);
                                //    layers.Add(l);
                                //}
                                //IGroupLayer comlyr = new GroupLayerClass();
                                //comlyr.Name = "底图";
                                //layers.Reverse();
                                //foreach (var item in layers)
                                //{
                                //    comlyr.Add(item);
                                //}
                                //MatchLayer(m_Application, comlyr, null, dtLayerRule, dsUpgrade.ThematicLyrNames);

                                ////加载专题图层
                                //layers.Clear();
                                //foreach (var kv in dsUpgrade.ThematicLyrNames)
                                //{
                                //    var l = new FeatureLayerClass();
                                //    l.Name = kv.Key;
                                //    IFeatureClass fc = (m_Application.Workspace.EsriWorkspace as IFeatureWorkspace).OpenFeatureClass(l.Name);
                                //    (l as IFeatureLayer).FeatureClass = fc;
                                //    layers.Add(l);
                                //}
                                //templateMap.ClearLayers();
                                //if (layers.Count > 0)
                                //{
                                //    IGroupLayer thlyr = new GroupLayerClass();
                                //    thlyr.Name = "专题";
                                //    layers.Reverse();
                                //    foreach (var item in layers)
                                //    {
                                //        thlyr.Add(item);
                                //    }
                                //    MatchLayer(m_Application, thlyr, null, dtLayerRule, dsUpgrade.ThematicLyrNames);

                                //}


                                //wo.SetText("正在更新视图......");
                                ////var lyrs = m_Application.Workspace.LayerManager.GetLayer(new LayerManager.LayerChecker(l =>
                                ////{
                                ////    return l is IGeoFeatureLayer;
                                ////})).ToArray();
                                ////获取挡白的范围（可换成其他任意图层，但需要有数据）
                                //var lyrs=m_Application.Workspace.LayerManager.GetLayer(new LayerManager.LayerChecker(l=>
                                //    {
                                //        return l is IGeoFeatureLayer && (l as IGeoFeatureLayer).FeatureClass.AliasName == "LPOLY";
                                //    })).FirstOrDefault();

                                //IEnvelope env = null;
                                //IFeatureClass fc_LPOLY = (lyrs as IFeatureLayer).FeatureClass;
                                //IEnvelope e = (fc_LPOLY as IGeoDataset).Extent;
                                //if (!e.IsEmpty)
                                //{
                                //    env = e;
                                //}

                                //if (env != null && !env.IsEmpty)
                                //{
                                //    env.Expand(1.2, 1.2, true);
                                //    double xcenter = (env.XMax + env.XMin) / 2;
                                //    double ycenter = (env.YMax + env.YMin) / 2;
                                //    IPoint centerpoint = new PointClass();
                                //    centerpoint.PutCoords(xcenter, ycenter);
                                //    m_Application.MapControl.CenterAt(centerpoint);

                                //    m_Application.MapControl.Extent = env;
                                //    m_Application.Workspace.Map.AreaOfInterest = env;

                                //    (lyrs as ILayer2).AreaOfInterest = env;
                                //}
                                ////for (int i = 0; i <1; i++)
                                ////{
                                ////    IFeatureClass fc = (lyrs[i] as IFeatureLayer).FeatureClass;
                                ////IFeatureClassManage fcMgr = fc as IFeatureClassManage;
                                ////fcMgr.UpdateExtent();

                                ////    IEnvelope e = (fc as IGeoDataset).Extent;
                                ////    if (!e.IsEmpty)
                                ////    {
                                ////        if (null == env)
                                ////        {
                                ////            env = e;
                                ////        }
                                ////        else
                                ////        {
                                ////            env.Union(e);
                                ////        }
                                ////    }

                                ////    #region Li
                                ////    if (fc.AliasName.ToUpper() == "CLIPBOUNDARY")
                                ////    {
                                ////        IQueryFilter qf = new QueryFilterClass();
                                ////        qf.WhereClause = "TYPE = '页面'";
                                ////        var feCursor = fc.Search(qf, true);
                                ////        var f = feCursor.NextFeature();
                                ////        if (f != null)
                                ////        {
                                ////            IGeometry pageGeo = f.ShapeCopy;
                                ////            GApplication.Application.Workspace.Map.ClipGeometry = pageGeo;
                                ////        }
                                ////        Marshal.ReleaseComObject(feCursor);
                                ////    }
                                ////    #endregion
                                ////}

                                ////if (env != null && !env.IsEmpty)
                                ////{
                                ////    env.Expand(1.2, 1.2, true);
                                ////    double xcenter = (env.XMax + env.XMin) / 2;
                                ////    double ycenter = (env.YMax + env.YMin) / 2;
                                ////    IPoint centerpoint = new PointClass();
                                ////    centerpoint.PutCoords(xcenter, ycenter);
                                ////    m_Application.MapControl.CenterAt(centerpoint);

                                ////    m_Application.MapControl.Extent = env;
                                ////    m_Application.Workspace.Map.AreaOfInterest = env;

                                ////    for (int i = 0; i < lyrs.Length; i++)
                                ////    {
                                ////        (lyrs[i] as ILayer2).AreaOfInterest = env;
                                ////    }
                                ////}
                                ////修改注记地图比例尺
                                //#region
                                //var lyrsAnno = m_Application.Workspace.LayerManager.GetLayer(new LayerManager.LayerChecker(l =>
                                //{
                                //    return (l is IFeatureLayer);
                                //})).ToArray();

                                //foreach (var l in lyrsAnno)
                                //{
                                //    IFeatureClass pfcl = (l as IFeatureLayer).FeatureClass;
                                //    if (pfcl.Extension is IAnnoClass)
                                //    {
                                //        IAnnoClass pAnno = pfcl.Extension as IAnnoClass;
                                //        IAnnoClassAdmin3 pAnnoAdmin = pAnno as IAnnoClassAdmin3;
                                //        if (pAnno.ReferenceScale != map.ReferenceScale)
                                //        {
                                //            pAnnoAdmin.AllowSymbolOverrides = true;
                                //            pAnnoAdmin.ReferenceScale = map.ReferenceScale;
                                //            pAnnoAdmin.UpdateProperties();
                                //        }
                                //    }
                                //}
                                //#endregion
                                //wo.SetText("正在保存工程......");
                                //m_Application.Workspace.Save();
                                //GC.Collect();
                                ////将环境配置信息写入econfig
                                //EnvironmentSettings.UpdateEnvironmentToConfig(dataUpdateFrom.AttachMap);
                                //m_Application.MapControl.ActiveView.Refresh();

                                //TEST**************************************
                                wo.SetText("正在加载升级后的文件数据库...");
                                #region 关闭当前工程

                                if (m_Application.Workspace == null)
                                {
                                    System.Windows.Forms.MessageBox.Show("未打开地图工程");
                                    return;
                                }
                                try
                                {
                                    //关闭资源锁定
                                    IWorkspaceFactoryLockControl ipWsFactoryLock;
                                    ipWsFactoryLock = (IWorkspaceFactoryLockControl)(SMGI.Common.GApplication.GDBFactory as IWorkspaceFactory2);
                                    if (ipWsFactoryLock.SchemaLockingEnabled)
                                    {
                                        ipWsFactoryLock.DisableSchemaLocking();
                                    }
                                }
                                catch { }
                                if (m_Application.EngineEditor.EditState == ESRI.ArcGIS.Controls.esriEngineEditState.esriEngineStateEditing)
                                {
                                    System.Windows.Forms.DialogResult r = System.Windows.Forms.MessageBox.Show("正在开启编辑，是否保存编辑？", "提示", System.Windows.Forms.MessageBoxButtons.YesNoCancel);
                                    if (r == System.Windows.Forms.DialogResult.Cancel)
                                    {
                                        return;
                                    }
                                    else
                                    {
                                        m_Application.EngineEditor.StopEditing(r == System.Windows.Forms.DialogResult.Yes);
                                    }
                                }

                                //此处无法判断用户是否保存工程，问是否保存容易引发用户误解，因此删除，详见福建协同项目20230222提交的问题清单
                                //System.Windows.Forms.DialogResult r1 = System.Windows.Forms.MessageBox.Show("工作区尚未保存，是否保存？", "提示", System.Windows.Forms.MessageBoxButtons.YesNoCancel);
                                //if (r1 == System.Windows.Forms.DialogResult.Cancel)
                                //{
                                //    return;
                                //}
                                //else
                                //{
                                //    if (r1 == System.Windows.Forms.DialogResult.Yes)
                                //        m_Application.Workspace.Save();
                                //}

                                m_Application.CloseWorkspace();
                                if (m_Application.Template.Caption == "含权限控制的地理信息协同作业")
                                {
                                    m_Application.MainForm.Title = GlobalClass.strname + "-已登录 " + m_Application.Template.Caption;
                                }

                                #endregion

                                #region 打开地理数据库，获取地图模板
                                CommonMethods.OpenGDBFile(m_Application, outputFileGDB);

                                //test
                                EnvironmentSettings.updateMapScale(m_Application, mapScale);
                                //

                                IMapDocument pMapDoc = new MapDocumentClass();
                                pMapDoc.Open(mxdFullFileName, "");
                                if (pMapDoc.MapCount == 0)//如果地图模板为空
                                {
                                    MessageBox.Show("地图模板不能为空！");
                                    return;
                                }

                                //获取模板中的地图
                                IActiveView view = m_Application.ActiveView;
                                IMap map = view.FocusMap;
                                IMap templateMap = pMapDoc.get_Map(0);

                                string engineName = templateMap.AnnotationEngine.Name;
                                if (engineName.Contains("Maplex"))
                                {
                                    IAnnotateMap sm = new MaplexAnnotateMapClass();
                                    map.AnnotationEngine = sm;
                                }
                                else
                                {
                                    map.AnnotationEngine = templateMap.AnnotationEngine;
                                }
                                map.ReferenceScale = Convert.ToInt32(mapScale);
                                templateMap.SpatialReference = map.SpatialReference;
                                #endregion

                                wo.SetText("正在匹配制图表达......");
                                #region 重新匹配制图表达
                                m_Application.Workspace.LayerManager.Map.ClearLayers();
                                List<ILayer> layers = new List<ILayer>();
                                //加载底图模板图层
                                for (int i = templateMap.LayerCount - 1; i >= 0; i--)
                                {
                                    var l = templateMap.get_Layer(i);
                                    layers.Add(l);
                                }
                                IGroupLayer comlyr = new GroupLayerClass();
                                comlyr.Name = "底图";
                                layers.Reverse();
                                foreach (var item in layers)
                                {
                                    comlyr.Add(item);
                                }
                                // MatchLayer(m_Application, comlyr, null, ruleMDB, dsUpgrade.ThematicLyrNames);
                                MatchLayer(m_Application, comlyr, null, ruleMDB, themTemplateFCName2themName, themName2DT);
                                #endregion

                                #region 重新匹配专题图层数据的制图表达

                                #region 读取中英文映射表 2023.8.9
                                ChinEnlishLayerMappingTable = CommonMethods.ReadAccesstoDataTable(GApplication.Application.Template.Root + @"\专题\专题图层中英文映射.mdb", "中英文图层映射");
                                for (int j = 0; j < ChinEnlishLayerMappingTable.Rows.Count; j++)
                                {
                                    DataRow row = ChinEnlishLayerMappingTable.Rows[j];
                                    ChinEnlishLayerDic[row["英文图层名"].ToString()] = row["中文图层名"].ToString();
                                }
                                #endregion
                                // layers.Clear();
                                #region 收集输出数据库中存在的专题要素，并分类折叠
                                Dictionary<ILayer, string> ILayer2themName = new Dictionary<ILayer, string>();//专题图层ILayer-中文专题名称
                                foreach (var kv in themTemplateFCName2themName)
                                {
                                    if (!(GApplication.Application.Workspace.EsriWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, kv.Key)) continue;
                                    ILayer layer = null;
                                    IFeatureClass fc = (GApplication.Application.Workspace.EsriWorkspace as IFeatureWorkspace).OpenFeatureClass(kv.Key);
                                    if (fc.FeatureType == esriFeatureType.esriFTAnnotation)
                                    {
                                        layer = new FDOGraphicsLayerClass();
                                    }
                                    else
                                    {
                                        layer = new FeatureLayerClass();
                                    }
                                    //设置图层的别名
                                    if (ChinEnlishLayerDic.ContainsKey(kv.Key))//图层名转中文.2023.8.9
                                    {
                                        layer.Name = ChinEnlishLayerDic[kv.Key];
                                    }
                                    else
                                    {

                                    }

                                    (layer as IFeatureLayer).FeatureClass = fc;

                                    if (fc.FindField("SELECTSTATE") != -1)
                                    {
                                        var fd = layer as ESRI.ArcGIS.Carto.IFeatureLayerDefinition;
                                        string finitionExpression = fd.DefinitionExpression;
                                        if (!finitionExpression.ToLower().Contains("SELECTSTATE"))
                                        {
                                            if (finitionExpression != "")
                                            {
                                                fd.DefinitionExpression = string.Format("({0}) and (SELECTSTATE IS NULL)", finitionExpression);
                                            }
                                            else
                                            {
                                                fd.DefinitionExpression = "SELECTSTATE IS NULL";
                                            }
                                        }
                                    }

                                    ILegendInfo legendInfo = layer as ILegendInfo;
                                    if (legendInfo != null)
                                    {
                                        ILegendGroup lGroup;
                                        for (int i = 0; i < legendInfo.LegendGroupCount; ++i)
                                        {
                                            lGroup = legendInfo.get_LegendGroup(i);
                                            lGroup.Visible = false;//折叠
                                        }
                                    }

                                    if (ChinEnlishLayerDic.ContainsKey(kv.Value))//专题名转中文.2023.8.11
                                    {
                                        ILayer2themName.Add(layer, ChinEnlishLayerDic[kv.Value]); //layers.Add(layer);
                                    }
                                }
                                //分类存放
                                Dictionary<string, List<ILayer>> ZhuanTiFenZu = new Dictionary<string, List<ILayer>>();
                                foreach (var Lay2TheName in ILayer2themName)
                                {
                                    List<ILayer> layerList = new List<ILayer>();
                                    foreach (var Lay2TheName2 in ILayer2themName)
                                    {
                                        if (Lay2TheName.Key == Lay2TheName2.Key)
                                        {
                                            continue;
                                        }
                                        if (Lay2TheName.Value == Lay2TheName2.Value)
                                        {
                                            layerList.Add(Lay2TheName2.Key);
                                        }
                                    }
                                    layerList.Add(Lay2TheName.Key);
                                    if (!ZhuanTiFenZu.ContainsKey(Lay2TheName.Value))
                                    {
                                        ZhuanTiFenZu.Add(Lay2TheName.Value, layerList);
                                    }
                                }
                                #endregion
                                GApplication.Application.TOCControl.Update();

                                //遍历专题组，重新匹配专题图层数据的制图表达
                                foreach (var Name2Layerlist in ZhuanTiFenZu)
                                {
                                    if (Name2Layerlist.Value.Count > 0)
                                    {
                                        IGroupLayer thlyr = new GroupLayerClass();
                                        thlyr.Name = Name2Layerlist.Key;
                                        // layers.Reverse();
                                        //thlyr.Expanded = false;
                                        foreach (var item in Name2Layerlist.Value)
                                        {
                                            thlyr.Add(item);
                                        }
                                        MatchLayer(GApplication.Application, thlyr, null, ruleMDB, themTemplateFCName2themName, themName2DT);
                                    }
                                }

                                #endregion

                                #region  修改注记地图比例尺
                                var lyrsAnno = m_Application.Workspace.LayerManager.GetLayer(new LayerManager.LayerChecker(l =>
                                {
                                    return (l is IFeatureLayer);
                                })).ToArray();

                                foreach (var l in lyrsAnno)
                                {
                                    IFeatureClass pfcl = (l as IFeatureLayer).FeatureClass;
                                    if (pfcl.Extension is IAnnoClass)
                                    {
                                        IAnnoClass pAnno = pfcl.Extension as IAnnoClass;
                                        IAnnoClassAdmin3 pAnnoAdmin = pAnno as IAnnoClassAdmin3;
                                        if (pAnno.ReferenceScale != map.ReferenceScale)
                                        {
                                            pAnnoAdmin.AllowSymbolOverrides = true;
                                            pAnnoAdmin.ReferenceScale = map.ReferenceScale;
                                            pAnnoAdmin.UpdateProperties();
                                        }
                                    }
                                }
                                #endregion

                                wo.SetText("正在更新视图......");
                                #region 更新视图
                                var lyrs = m_Application.Workspace.LayerManager.GetLayer(new LayerManager.LayerChecker(l =>
                                {
                                    return l is IGeoFeatureLayer;
                                })).ToArray();

                                IEnvelope env = null;
                                for (int i = 0; i < lyrs.Length; i++)
                                {
                                    IFeatureClass fc = (lyrs[i] as IFeatureLayer).FeatureClass;
                                    IFeatureClassManage fcMgr = fc as IFeatureClassManage;
                                    fcMgr.UpdateExtent();

                                    IEnvelope e = (fc as IGeoDataset).Extent;
                                    if (!e.IsEmpty)
                                    {
                                        if (null == env)
                                        {
                                            env = e;
                                        }
                                        else
                                        {
                                            env.Union(e);
                                        }
                                    }

                                    #region Li
                                    if (fc.AliasName.ToUpper() == "CLIPBOUNDARY")
                                    {
                                        IQueryFilter qf = new QueryFilterClass();
                                        qf.WhereClause = "TYPE = '页面'";
                                        var feCursor = fc.Search(qf, true);
                                        var f = feCursor.NextFeature();
                                        if (f != null)
                                        {
                                            IGeometry pageGeo = f.ShapeCopy;
                                            GApplication.Application.Workspace.Map.ClipGeometry = pageGeo;
                                        }
                                        Marshal.ReleaseComObject(feCursor);
                                    }
                                    #endregion
                                }

                                if (env != null && !env.IsEmpty)
                                {
                                    env.Expand(1.2, 1.2, true);
                                    m_Application.MapControl.Extent = env;
                                    m_Application.Workspace.Map.AreaOfInterest = env;

                                    for (int i = 0; i < lyrs.Length; i++)
                                    {
                                        (lyrs[i] as ILayer2).AreaOfInterest = env;
                                    }
                                }
                                #endregion

                                //#region 写入裁切面的PAC
                                //string path = GApplication.Application.Template.Root + @"\专家库\LocatoinLastSetting.xml";
                                //XDocument doc = XDocument.Load(path);
                                //var content = doc.Element("LocationParams").Element("Content");
                                //try
                                //{
                                //    var mapPacItem = content.Element("mapPac");
                                //    string pac = mapPacItem.Attribute("PAC").Value.ToString();
                                //    if (pac != "")
                                //    {
                                //        if ((m_Application.Workspace.EsriWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, "ClipBoundary"))
                                //        {
                                //            IFeatureClass fc = (m_Application.Workspace.EsriWorkspace as IFeatureWorkspace).OpenFeatureClass("ClipBoundary");
                                //            IQueryFilter qf = new QueryFilterClass();
                                //            qf.WhereClause = "type='裁切面'";
                                //            IFeatureCursor fecursor = fc.Search(qf, false);
                                //            IFeature fe = fecursor.NextFeature();
                                //            if (fe != null)
                                //            {
                                //                fe.set_Value(fe.Fields.FindField("BZ"), "PAC=" + pac);
                                //                fe.Store();
                                //            }
                                //            Marshal.ReleaseComObject(fecursor);
                                //        }
                                //    }
                                //}
                                //catch (Exception ex)
                                //{
                                //    System.Diagnostics.Trace.WriteLine(ex.Message);
                                //    System.Diagnostics.Trace.WriteLine(ex.Source);
                                //    System.Diagnostics.Trace.WriteLine(ex.StackTrace);
                                //    MessageBox.Show(ex.Message);
                                //}
                                //#endregion

                                wo.SetText("正在保存工程......");
                                m_Application.Workspace.Save();
                                GC.Collect();
                                //将环境配置信息写入econfig
                                EnvironmentSettings.UpdateEnvironmentToConfig(dataUpdateFrom.AttachMap);
                                //TEST****************************************

                                #region 关闭当前工程

                                if (m_Application.Workspace == null)
                                {
                                    System.Windows.Forms.MessageBox.Show("未打开地图工程");
                                    return;
                                }
                                try
                                {
                                    //关闭资源锁定
                                    IWorkspaceFactoryLockControl ipWsFactoryLock;
                                    ipWsFactoryLock = (IWorkspaceFactoryLockControl)(SMGI.Common.GApplication.GDBFactory as IWorkspaceFactory2);
                                    if (ipWsFactoryLock.SchemaLockingEnabled)
                                    {
                                        ipWsFactoryLock.DisableSchemaLocking();
                                    }
                                }
                                catch { }
                                if (m_Application.EngineEditor.EditState == ESRI.ArcGIS.Controls.esriEngineEditState.esriEngineStateEditing)
                                {
                                    System.Windows.Forms.DialogResult r = System.Windows.Forms.MessageBox.Show("正在开启编辑，是否保存编辑？", "提示", System.Windows.Forms.MessageBoxButtons.YesNoCancel);
                                    if (r == System.Windows.Forms.DialogResult.Cancel)
                                    {
                                        return;
                                    }
                                    else
                                    {
                                        m_Application.EngineEditor.StopEditing(r == System.Windows.Forms.DialogResult.Yes);
                                    }
                                }

                                //此处无法判断用户是否保存工程，问是否保存容易引发用户误解，因此删除，详见福建协同项目20230222提交的问题清单
                                //System.Windows.Forms.DialogResult r1 = System.Windows.Forms.MessageBox.Show("工作区尚未保存，是否保存？", "提示", System.Windows.Forms.MessageBoxButtons.YesNoCancel);
                                //if (r1 == System.Windows.Forms.DialogResult.Cancel)
                                //{
                                //    return;
                                //}
                                //else
                                //{
                                //    if (r1 == System.Windows.Forms.DialogResult.Yes)
                                //        m_Application.Workspace.Save();
                                //}

                                m_Application.CloseWorkspace();
                                if (m_Application.Template.Caption == "含权限控制的地理信息协同作业")
                                {
                                    m_Application.MainForm.Title = GlobalClass.strname + "-已登录 " + m_Application.Template.Caption;
                                }

                                #endregion

                                #region 替换工程

                                GC.Collect();
                                GC.WaitForFullGCComplete();
                                GC.WaitForPendingFinalizers();

                                CopyGDBContent(sourceFileGDB, outputFileGDB);

                                #endregion

                                #region 打开GDB

                                CommonMethods.OpenGDBFile(m_Application, sourceFileGDB);

                                #endregion

                                break;
                            }
                            else
                            {
                                return;
                            }
                        }
                        else
                        {
                            wo.SetText(string.Format("正在关闭符号化"));
                            ResetToRep(gfl);
                            flag = true;
                        }
                    }
                }
                else  //有模板
                {
                    var tempMap = mapDoc.Map[0];
                    var olys = new Dictionary<string, ILayer>();
                    for (var i = tempMap.LayerCount - 1; i >= 0; i--)
                        olys.Add(tempMap.Layer[i].Name, tempMap.Layer[i]);

                    //原本是ly.name，但其获取的是图层名（中文），从而导致即使有模板也无法匹配
                    //有模板的情况下，如果出现当前地图存在某些图层是模板没有的情况，就会导致无模版的图层不发生变化，而有模版对应的图层会进行符号化开关
                    //可以考虑增加判断（注释），如果出现上述情况则进行随机赋符号
                    foreach (var ly in lys)
                    {
                        if (ly.FeatureClass.AliasName.StartsWith("VEGA_") || ly.FeatureClass.AliasName.EndsWith("普色")
                            || ly.FeatureClass.AliasName.EndsWith("注记") || ly.FeatureClass.AliasName.EndsWith("底色")) continue;
                        var gfl = (IGeoFeatureLayer)ly;
                        if (gfl.Renderer is IRepresentationRenderer)
                        {
                            wo.SetText(string.Format("正在开启符号化"));
                            if (olys.ContainsKey(ly.FeatureClass.AliasName))
                            {
                                var render = ((IGeoFeatureLayer)olys[ly.FeatureClass.AliasName]).Renderer;
                                gfl.Renderer = render;
                            }
                            //else
                            //{
                            //    SimpleRenderer src = new SimpleRendererClass();
                            //    ISymbol sym;
                            //    if (gfl.FeatureClass.ShapeType == esriGeometryType.esriGeometryPoint)
                            //    {
                            //        sym = new SimpleMarkerSymbolClass();
                            //        ((ISimpleMarkerSymbol)sym).Color = new RgbColorClass { Red = rand.Next(255), Green = rand.Next(255), Blue = rand.Next(255) };
                            //    }
                            //    else if (gfl.FeatureClass.ShapeType == esriGeometryType.esriGeometryPolyline)
                            //    {
                            //        sym = new SimpleLineSymbolClass();
                            //        ((ISimpleLineSymbol)sym).Color = new RgbColorClass { Red = rand.Next(255), Green = rand.Next(255), Blue = rand.Next(255) };
                            //    }
                            //    else
                            //    {
                            //        sym = new SimpleFillSymbolClass();
                            //        ((ISimpleFillSymbol)sym).Color = new RgbColorClass { Red = rand.Next(255), Green = rand.Next(255), Blue = rand.Next(255) };
                            //    }
                            //    src.Symbol = sym;
                            //    gfl.Renderer = src as IFeatureRenderer;
                            //}
                        }
                        else
                        {
                            wo.SetText(string.Format("正在关闭符号化"));
                            ResetToRep(gfl);
                            flag = true;
                        }
                    }
                }

                m_Application.MapControl.Map.ReferenceScale = referenceScale;
                m_Application.MapControl.Refresh();
            }
        }

        //还原成制图表达
        private void ResetToRep(IGeoFeatureLayer gfl)
        {
            var fc = gfl.FeatureClass;
            IRepresentationRenderer rrc = new RepresentationRendererClass();
            var rwe = m_Application.Workspace.RepersentationWorkspaceExtension;
            if (rwe.FeatureClassHasRepresentations[fc])
            {
                var edn = rwe.FeatureClassRepresentationNames[fc];
                edn.Reset();
                var dn = edn.Next();
                if (dn != null) rrc.RepresentationClass = rwe.OpenRepresentationClass(dn.Name);
            }
            gfl.Renderer = rrc as IFeatureRenderer;
        }

        public class ThemathicInfo
        {
            public ThemathicInfo()
            {
                name2AliasName = new Dictionary<string, string>();
            }
            public string name
            {
                get;
                set;
            }
            /// <summary>
            /// 图层名称-中文名称
            /// </summary>
            public Dictionary<string, string> name2AliasName
            {
                get;
                set;
            }
            public string AnnoRulePath
            {
                get;
                set;
            }
            public string RuleDirc
            {
                get;
                set;
            }
            public KeyValuePair<string, string> annoLayer
            {
                get;
                set;
            }
        }

        private List<ThemathicInfo> InitialThematicList(string path)
        {
            XElement content = null;
            XDocument doc = XDocument.Load(path);
            content = doc.Element("Template").Element("Content");
            var ThematicItems = content.Elements("Thematic");
            List<ThemathicInfo> infos = new List<ThemathicInfo>();
            foreach (XElement ele in ThematicItems)
            {
                ThemathicInfo info = new ThemathicInfo();
                string rulePath = GApplication.Application.Template.Root + "\\" + ele.Attribute("Path").Value;
                info.RuleDirc = rulePath.TrimEnd("普通注记规则.mdb".ToArray());
                info.name = ele.Element("Name").Value;
                foreach (XElement item in ele.Elements("Layer"))
                {
                    if (item.Value.Contains("ANNO"))
                    {
                        info.annoLayer = new KeyValuePair<string, string>(item.Value.Trim(), item.Attribute("AliasName").Value.Trim());
                    }
                    else
                    {
                        info.name2AliasName[item.Value.Trim()] = item.Attribute("AliasName").Value.Trim();
                    }
                }
                infos.Add(info);
            }
            return infos;
        }

        private static void MatchLayer(GApplication app, ILayer layer, IGroupLayer parent, DataTable dtLayerRule,
           Dictionary<string, string> lyrName2themName, Dictionary<string, DataTable> themName2DT)
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
                    MatchLayer(app, item, layer as IGroupLayer, dtLayerRule, lyrName2themName, themName2DT);
                }
            }
            else
            {
                #region
                try
                {
                    string name = ((layer as IDataLayer2).DataSourceName as IDatasetName).Name;
                    if (layer is IFeatureLayer)
                    {
                        if ((app.Workspace.EsriWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, name))
                        {
                            try
                            {
                                //指定图层数据源
                                IFeatureClass fc = (app.Workspace.EsriWorkspace as IFeatureWorkspace).OpenFeatureClass(name);
                                (layer as IFeatureLayer).FeatureClass = fc;
                                if (fc.Extension is IAnnoClass)
                                    return;
                                DataRow[] drArray = dtLayerRule.Select().Where(i => i["映射图层"].ToString().Trim() == name).ToArray();
                                if (drArray.Length != 0)
                                {
                                    int selectStateIndex = fc.FindField("SELECTSTATE");

                                    int ruleIDIndex = -1;
                                    int invisibleRuleID = 0;
                                    #region 获取图层的制图表达字段索引值
                                    for (int i = 0; i < drArray.Length; i++)
                                    {
                                        //获取该图层的制图表达规则字段索引
                                        if (ruleIDIndex == -1)
                                        {
                                            ruleIDIndex = fc.FindField(drArray[i]["RuleIDFeildName"].ToString().Trim());
                                        }

                                        //获取该图层的不显示规则ID值
                                        if (invisibleRuleID == 0 && drArray[i]["RuleName"].ToString() == "不显示要素")
                                        {
                                            invisibleRuleID = CommonMethods.GetRuleIDByRuleName(name, "不显示要素");
                                        }

                                    }

                                    if (ruleIDIndex == -1 && (layer as IGeoFeatureLayer).Renderer is IRepresentationRenderer)
                                    {
                                        IRepresentationRenderer reprenderer = (layer as IGeoFeatureLayer).Renderer as IRepresentationRenderer;
                                        if (reprenderer != null)
                                        {
                                            IRepresentationClass repClass = reprenderer.RepresentationClass;
                                            if (repClass != null)
                                                ruleIDIndex = repClass.RuleIDFieldIndex;
                                        }
                                    }
                                    #endregion

                                    #region 更新制图表达规则
                                    for (int i = 0; i < drArray.Length; i++)
                                    {
                                        string whereClause = drArray[i]["定义查询"].ToString().Trim();
                                        string ruleName = drArray[i]["RuleName"].ToString().Trim();
                                        bool isSelect = (drArray[i]["选取状态"].ToString().Trim() != "否");

                                        int ruleID = CommonMethods.GetRuleIDByRuleName(name, ruleName);
                                        if (ruleID == -1)
                                        {
                                            int.TryParse(drArray[i]["RuleID"].ToString().Trim(), out ruleID);
                                        }
                                        if (ruleID == -1 || ruleID == 0)
                                            continue;//无效的规则ID属性值

                                        IQueryFilter qf = new QueryFilterClass();
                                        qf.WhereClause = whereClause.ToString();
                                        IFeatureCursor fCursor = fc.Update(qf, true);
                                        IFeature f = null;
                                        while ((f = fCursor.NextFeature()) != null)
                                        {
                                            f.set_Value(ruleIDIndex, ruleID);
                                            if (selectStateIndex != -1)
                                            {
                                                if (!isSelect)
                                                    f.set_Value(selectStateIndex, "不选取");
                                            }
                                            fCursor.UpdateFeature(f);

                                            Marshal.ReleaseComObject(f);
                                        }
                                        Marshal.ReleaseComObject(fCursor);
                                    }

                                    #region 将图层剩余要素设置为不显示要素
                                    if (invisibleRuleID != 0)
                                    {
                                        IQueryFilter qf = new QueryFilterClass();
                                        qf.WhereClause = string.Format("{0} is null", fc.Fields.get_Field(ruleIDIndex).Name);
                                        IFeatureCursor feCursor = fc.Update(qf, true);
                                        IFeature fe = null;
                                        while ((fe = feCursor.NextFeature()) != null)
                                        {
                                            fe.set_Value(ruleIDIndex, invisibleRuleID);
                                            feCursor.UpdateFeature(fe);

                                            Marshal.ReleaseComObject(fe);
                                        }
                                        Marshal.ReleaseComObject(feCursor);
                                    }
                                    #endregion
                                    #endregion

                                }
                                else
                                {
                                    if (lyrName2themName.ContainsKey(name)) //判断是否是专题图层
                                    {
                                        var thematicName = lyrName2themName[name];
                                        var dt = themName2DT[thematicName];
                                        drArray = dt.Select().Where(i => i["映射图层"].ToString().Trim() == name).ToArray();
                                        if (drArray.Length != 0)
                                        {
                                            int selectStateIndex = fc.FindField("SELECTSTATE");

                                            int ruleIDIndex = -1;
                                            int invisibleRuleID = 0;

                                            #region 获取图层的制图表达字段索引值
                                            for (int i = 0; i < drArray.Length; i++)
                                            {
                                                //获取该图层的制图表达规则字段索引
                                                if (ruleIDIndex == -1)
                                                {
                                                    ruleIDIndex = fc.FindField(drArray[i]["RuleIDFeildName"].ToString().Trim());
                                                    break;
                                                }

                                                //获取该图层的不显示规则ID值
                                                if (invisibleRuleID == 0 && drArray[i]["RuleName"].ToString() == "不显示要素")
                                                {
                                                    invisibleRuleID = CommonMethods.GetRuleIDByRuleName(name, "不显示要素");
                                                }

                                            }
                                            if (ruleIDIndex == -1 && (layer as IGeoFeatureLayer).Renderer is IRepresentationRenderer)
                                            {
                                                IRepresentationRenderer reprenderer = (layer as IGeoFeatureLayer).Renderer as IRepresentationRenderer;
                                                if (reprenderer != null)
                                                {
                                                    IRepresentationClass repClass = reprenderer.RepresentationClass;
                                                    if (repClass != null)
                                                        ruleIDIndex = repClass.RuleIDFieldIndex;
                                                }

                                            }
                                            #endregion

                                            #region 更新制图表达规则属性值
                                            for (int i = 0; i < drArray.Length; i++)
                                            {
                                                string whereClause = drArray[i]["定义查询"].ToString().Trim();
                                                string ruleName = drArray[i]["RuleName"].ToString().Trim();
                                                bool isSelect = (drArray[i]["选取状态"].ToString().Trim() != "否");

                                                int ruleID = CommonMethods.GetRuleIDByRuleName(name, ruleName);
                                                if (ruleID == -1)
                                                {
                                                    int.TryParse(drArray[i]["RuleID"].ToString().Trim(), out ruleID);
                                                }
                                                if (ruleID == -1 || ruleID == 0)
                                                    continue;//无效的规则ID属性值

                                                IQueryFilter qf = new QueryFilterClass();
                                                qf.WhereClause = whereClause.ToString();
                                                IFeatureCursor fCursor = fc.Update(qf, true);
                                                IFeature f = null;
                                                while ((f = fCursor.NextFeature()) != null)
                                                {
                                                    f.set_Value(ruleIDIndex, ruleID);
                                                    if (selectStateIndex != -1)
                                                    {
                                                        if (!isSelect)
                                                            f.set_Value(selectStateIndex, "不选取");
                                                    }
                                                    fCursor.UpdateFeature(f);

                                                    Marshal.ReleaseComObject(f);
                                                }
                                                Marshal.ReleaseComObject(fCursor);
                                            }

                                            #region 将图层剩余要素设置为不显示要素
                                            if (invisibleRuleID != 0)
                                            {
                                                IQueryFilter qf = new QueryFilterClass();
                                                qf.WhereClause = string.Format("{0} is null", fc.Fields.get_Field(ruleIDIndex).Name);
                                                IFeatureCursor feCursor = fc.Update(qf, true);
                                                IFeature fe = null;
                                                while ((fe = feCursor.NextFeature()) != null)
                                                {
                                                    fe.set_Value(ruleIDIndex, invisibleRuleID);
                                                    feCursor.UpdateFeature(fe);

                                                    Marshal.ReleaseComObject(fe);
                                                }
                                                Marshal.ReleaseComObject(feCursor);
                                            }
                                            #endregion
                                            #endregion
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                throw ex;
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
                catch (Exception ex)
                {
                    throw ex;
                }
                #endregion
            }
        }

        void CopyGDBContent(string sourceGDBPath, string targetGDBPath)
        {
            // 创建GP工具对象
            Geoprocessor geoprocessor = new Geoprocessor();
            geoprocessor.OverwriteOutput = true;

            // 初始化工作空间工厂
            IWorkspaceFactory workspaceFactory = new FileGDBWorkspaceFactoryClass();

            // 打开源和目标地理数据库
            IWorkspace sourceGDB = workspaceFactory.OpenFromFile(sourceGDBPath, 0);
            IWorkspace targetGDB = workspaceFactory.OpenFromFile(targetGDBPath, 0);

            // 获取源地理数据库的所有数据集，并逐个删除
            IEnumDataset sourceDatasets = sourceGDB.get_Datasets(esriDatasetType.esriDTAny);
            IDataset sourceDataset;
            while ((sourceDataset = sourceDatasets.Next()) != null)
            {
                sourceDataset.Delete();
            }

            Marshal.ReleaseComObject(sourceDatasets);

            // 获取目标地理数据库的要素数据集
            IEnumDataset outputDatasets = targetGDB.get_Datasets(esriDatasetType.esriDTFeatureDataset);

            // 遍历要素数据集并对每一个进行复制操作
            IDataset ouputDataset;
            while ((ouputDataset = outputDatasets.Next()) != null)
            {
                // 使用Copy工具复制每一个要素类
                Copy copyTool = new Copy();
                copyTool.in_data = ouputDataset.FullName;
                copyTool.out_data = System.IO.Path.Combine(sourceGDBPath, ouputDataset.Name);
                Helper.ExecuteGPTool(geoprocessor, copyTool, null);
                Marshal.ReleaseComObject(ouputDataset);
            }

            // 释放 COM 对象
            Marshal.ReleaseComObject(outputDatasets);
        }


        bool IsBOUA(ILayer info)
        {
            return (info is IFeatureLayer)
                   && ((info as IFeatureLayer).FeatureClass as IDataset).Name.ToUpper() == "BOUA";
        }
    }
}