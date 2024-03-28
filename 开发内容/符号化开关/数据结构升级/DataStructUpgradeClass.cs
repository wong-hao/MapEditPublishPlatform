using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SMGI.Common;
using System.Xml.Linq;
using System.IO;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.Geoprocessor;
using System.Windows.Forms;
using ESRI.ArcGIS.DataManagementTools;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.DataSourcesRaster;
using System.Data;
using System.Data.OleDb;
using ESRI.ArcGIS.ConversionTools;
namespace SMGI.Plugin.BaseFunction
{
    /// <summary>
    /// 2017-9-5 将源数据库中的要素类拷贝到升级后的数据库，将所有的栅格数据集拷贝到升级后的数据库
    /// </summary>
    public class DataStructUpgradeClass
    {
        private string a;
        public string templateFullFileName
        {
            get
            { return a; }
            set
            { a = value; }
        }
        private GApplication _app;//应用程序
        private string _sourceFileGDB;//源数据库
        private string _upgradeFileGDB;//升级后的数据库
        public Dictionary<string, string> _themTemplateFCName2themName = new Dictionary<string, string>();//图层名-专题名称
        public Dictionary<string, IFeatureClass> _themTemplateFCName2FC;//图层名-模板要素类（空）
        public Dictionary<string, DataTable> _themName2DT;//专题名称->图层规则表

        public DataStructUpgradeClass(GApplication app, string sourceFileGDB, string upgradeFileGDB)
        {
            _app = app;
            _sourceFileGDB = sourceFileGDB;
            _upgradeFileGDB = upgradeFileGDB;
        }

        /// <summary>
        ///  2017-9-5:实现将裁切的原始库数据(主区+专题数据) 导出到模板（含制图表达）GDB中
        /// </summary>
        public bool UpgradeBaseData(WaitOperation wo = null)
        {
            try
            {

                if (!Directory.Exists(_sourceFileGDB))
                {
                    MessageBox.Show("升级数据文件不存在！");
                    return false;
                }

                if (Directory.Exists(_upgradeFileGDB))
                {
                    MessageBox.Show("导出数据文件已经存在！");
                    return false;
                }

                //获取模板文件名             

                if (!Directory.Exists(templateFullFileName))
                {
                    MessageBox.Show("模板数据文件不存在！");
                    return false;
                }

                if (wo != null)
                    wo.SetText("正在读取源数据库...");

                //读取欲升级的文件数据库
                List<string> sourceFeatureClassName = new List<string>();
                List<string> sourceFeatureDatasetName = new List<string>();
                List<string> sourceRasterDatasetName = new List<string>();
                IWorkspaceFactory sourceWSFactory = new FileGDBWorkspaceFactoryClass();
                IWorkspace sourceWorkspace = sourceWSFactory.OpenFromFile(_sourceFileGDB, 0);
                IFeatureWorkspace sourceFeatureWorkspace = (IFeatureWorkspace)sourceWorkspace;
                _app.GetDatasetNames(sourceWorkspace, ref sourceFeatureClassName, ref sourceFeatureDatasetName);
                getRasterDatasetNames(sourceWorkspace, ref sourceRasterDatasetName);

                if (wo != null)
                    wo.SetText("正在读取数据结构升级的数据模板...");

                IFeatureClass sourceSPFC = sourceFeatureWorkspace.OpenFeatureClass(sourceFeatureClassName[0]);
                IGeoDataset geodt = sourceSPFC as IGeoDataset;
                ISpatialReference _spatialReference = geodt.SpatialReference;//数据的投影信息

                //读取数据模板
                IWorkspaceFactory tempWSFactory = new FileGDBWorkspaceFactoryClass();
                IWorkspace tempWorkspace = tempWSFactory.OpenFromFile(templateFullFileName, 0);

                DeifineSpatialRef(tempWorkspace, geodt.SpatialReference as IClone, geodt.SpatialReference);

                if (wo != null)
                    wo.SetText("正在拷贝数据...");

                //拷贝模板
                string expFilePath = _upgradeFileGDB.Substring(0, _upgradeFileGDB.LastIndexOf("\\"));
                string expFileName = _upgradeFileGDB.Substring(_upgradeFileGDB.LastIndexOf('\\') + 1);
                //IWorkspace expWorkspace = CopyTemplateDatabase(tempWorkspace, expFilePath, expFileName);

                //TEST
                Dictionary<string, IFeatureClass> inFCName2FC = new Dictionary<string, IFeatureClass>();
                Dictionary<string, IFeatureClass> outFCName2FC = new Dictionary<string, IFeatureClass>();
                IWorkspace expWorkspace = CopyTemplateDatabaseZT(_app, _upgradeFileGDB, sourceWorkspace, tempWorkspace,
                    _themTemplateFCName2FC, false, ref inFCName2FC, ref outFCName2FC, _themTemplateFCName2themName);

                List<string> newFNList = new List<string>();
                newFNList.Add("ATTACH");//邻区标识字段
                newFNList.Add("SELECTSTATE");//选取状态标识字段

                //为输出数据库要素类添加额外字段
                foreach (var item in outFCName2FC.Values)
                {
                    //增加文本字段
                    foreach (var fn in newFNList)
                    {
                        AddStringField(item, fn);
                    }
                }
                //TEST

                DeifineSpatialRef(expWorkspace, geodt.SpatialReference as IClone, geodt.SpatialReference);


                IntilThematicGDB();
                //拷贝要素数据到导出的文件数据库
                string errInfo = CopyFeatureClassFromOriginToTargetTh(sourceWorkspace, expWorkspace, sourceFeatureClassName);
                if (errInfo != "")
                {
                    MessageBox.Show(errInfo);
                    return false;
                }

                //非四川模板【新疆、江苏、吉林等）模板单独处理
                string template = GApplication.Application.Template.Root;
                if (GApplication.Application.Template.Caption != "四川应急制图模板")
                {
                    // bSuccess = CopyLFCLFromOriginToTarget(sourceWorkspace, expWorkspace);
                    // if (!bSuccess)
                    //{
                    //    MessageBox.Show("拷贝LFCL要素数据到导出的文件数据库失败！");
                    //    return false;
                    //}
                }

                //拷贝栅格数据集到导出的文件数据库
                errInfo = CopyRasterDatasetFromOriginToTarget(sourceWorkspace, expWorkspace, sourceRasterDatasetName);
                if (errInfo != "")
                {
                    MessageBox.Show(errInfo);
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.Source);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                MessageBox.Show(ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        ///  2023-2-14:宁夏：实现将裁切的原始库数据(主区+专题数据) 导出到自选模板（含制图表达）GDB中
        /// </summary>
        public bool UpgradeBaseDataNX( string formMXDFilePath,WaitOperation wo = null)
        {
            try
            {

                if (!Directory.Exists(_sourceFileGDB))
                {
                    MessageBox.Show("升级数据文件不存在！");
                    return false;
                }

                if (Directory.Exists(_upgradeFileGDB))
                {
                    MessageBox.Show("导出数据文件已经存在！");
                    return false;
                }

                //获取模板文件名
                string templateFullFileName = System.IO.Path.ChangeExtension(formMXDFilePath, ".gdb");

                if (!Directory.Exists(templateFullFileName))
                {
                    MessageBox.Show("模板数据文件不存在！");
                    return false;
                }

                if (wo != null)
                    wo.SetText("正在读取源数据库...");

                //读取欲升级的文件数据库
                List<string> sourceFeatureClassName = new List<string>();
                List<string> sourceFeatureDatasetName = new List<string>();
                List<string> sourceRasterDatasetName = new List<string>();
                IWorkspaceFactory sourceWSFactory = new FileGDBWorkspaceFactoryClass();
                IWorkspace sourceWorkspace = sourceWSFactory.OpenFromFile(_sourceFileGDB, 0);
                IFeatureWorkspace sourceFeatureWorkspace = (IFeatureWorkspace)sourceWorkspace;
                _app.GetDatasetNames(sourceWorkspace, ref sourceFeatureClassName, ref sourceFeatureDatasetName);
                getRasterDatasetNames(sourceWorkspace, ref sourceRasterDatasetName);

                if (wo != null)
                    wo.SetText("正在读取数据结构升级的数据模板...");

                //读取数据模板
                IWorkspaceFactory tempWSFactory = new FileGDBWorkspaceFactoryClass();
                IWorkspace tempWorkspace = tempWSFactory.OpenFromFile(templateFullFileName, 0);

                if (wo != null)
                    wo.SetText("正在拷贝数据...");

                //拷贝模板
                string expFilePath = _upgradeFileGDB.Substring(0, _upgradeFileGDB.LastIndexOf("\\"));
                string expFileName = _upgradeFileGDB.Substring(_upgradeFileGDB.LastIndexOf('\\') + 1);
                IWorkspace expWorkspace = CopyTemplateDatabase(tempWorkspace, expFilePath, expFileName);

                //定义投影
                IFeatureClass sourceSPFC = sourceFeatureWorkspace.OpenFeatureClass(sourceFeatureClassName[0]);
                IGeoDataset geodt = sourceSPFC as IGeoDataset;

                DeifineSpatialRef(expWorkspace, geodt.SpatialReference as IClone, geodt.SpatialReference);


                IntilThematicGDB();
                //拷贝要素数据到导出的文件数据库
                string errInfo = CopyFeatureClassFromOriginToTargetTh(sourceWorkspace, expWorkspace, sourceFeatureClassName);
                if (errInfo != "")
                {
                    MessageBox.Show(errInfo);
                    return false;
                }

                //非四川模板【新疆、江苏、吉林等）模板单独处理
                string template = GApplication.Application.Template.Root;
                if (GApplication.Application.Template.Caption != "四川应急制图模板")
                {
                    // bSuccess = CopyLFCLFromOriginToTarget(sourceWorkspace, expWorkspace);
                    // if (!bSuccess)
                    //{
                    //    MessageBox.Show("拷贝LFCL要素数据到导出的文件数据库失败！");
                    //    return false;
                    //}
                }

                //拷贝栅格数据集到导出的文件数据库
                errInfo = CopyRasterDatasetFromOriginToTarget(sourceWorkspace, expWorkspace, sourceRasterDatasetName);
                if (errInfo != "")
                {
                    MessageBox.Show(errInfo);
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.Source);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                MessageBox.Show(ex.Message);
                return false;
            }

            return true;
        }

        public DataTable RuleMDB
        {
            get;
            set;
        }
        public DataTable ReadToDataTable(string mdbFilePath, string tableName)
        {

            if (!File.Exists(mdbFilePath))
                return null;
            DataTable pDataTable = new DataTable();
            try
            {
                #region GIS 方式读取MDB
                IWorkspaceFactory pWorkspaceFactory = new AccessWorkspaceFactory();
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
                }
                #endregion
            }
            catch
            {
                GC.Collect();
                return MDBHelper.GetTable(mdbFilePath, tableName);
            }
            return pDataTable;
        }

        //特殊处理：LFCL->将原数据库中的要素类的数据对应复制到targetWorkspace中对应的要素类中，若targetWorkspace中没有该要素类，则先创建一个要素类再复制数据
        private bool CopyLFCLFromOriginToTarget(IWorkspace originWorkspace, IWorkspace targetWorkspace)
        {
            string sqltest = "";
            try
            {
                //原始数据库中不存在 LFCL！
                var ws2 = originWorkspace as IWorkspace2;
                if (!ws2.get_NameExists(esriDatasetType.esriDTFeatureClass, "LFCL"))
                    return true;
                //查找对照关系。原始图层 对应新的图层：LFCL->LRDL
                if (RuleMDB == null)
                {
                    string ruleMatchFileName = EnvironmentSettings.getLayerRuleDBFileName(GApplication.Application);
                    RuleMDB = ReadToDataTable(ruleMatchFileName, "图层对照规则");
                }
                DataTable dtLayerRule = RuleMDB;
                //LFCL  映射->LRDL和LRRL
                var drsLFCL = dtLayerRule.Select("图层='LFCL'");
                //sql-lyer
                IFeatureWorkspace originFeatureWorkspace = originWorkspace as IFeatureWorkspace;
                IFeatureWorkspace targetFeatureWorkspace = targetWorkspace as IFeatureWorkspace;


                IQueryFilter qf = new QueryFilterClass();
                for (int i = 0; i < drsLFCL.Length; i++)
                {
                    DataRow dr = drsLFCL[i];
                    string sourceFcl = dr["图层"].ToString();
                    string ruleName = dr["RuleName"].ToString();
                    if (ruleName == "不显示要素")
                        continue;
                    string targetFcl = dr["映射图层"].ToString();
                    string sql = dr["定义查询"].ToString();
                    qf.WhereClause = sql;
                    IFeatureClass originFeatureclass = originFeatureWorkspace.OpenFeatureClass(sourceFcl);
                    IFeatureClass targetFeatureclass = null;


                    if (!ExistLayerOrNot(targetWorkspace, targetFcl))
                    {
                        //创建新的要素类
                        IFields fields = createFeatureClassFields(originFeatureclass);
                        if (null == targetFeatureWorkspace.CreateFeatureClass(targetFcl, fields, null, null, esriFeatureType.esriFTSimple, originFeatureclass.ShapeFieldName, ""))
                        {
                            return false;
                        }
                    }
                    targetFeatureclass = targetFeatureWorkspace.OpenFeatureClass(targetFcl);

                    IFeatureClassLoad pFCLoad = targetFeatureclass as IFeatureClassLoad;
                    pFCLoad.LoadOnlyMode = true;
                    #region
                    //获取不一样属性字段的索引
                    int indexDate_orgin = -1;
                    int indexDate_target = -1;

                    if (originFeatureclass.FindField("DATE_") > 0)
                    {
                        indexDate_orgin = originFeatureclass.FindField("DATE_");
                        indexDate_target = targetFeatureclass.FindField("DATE");
                    }

                    int indexNames_origin = -1;
                    int indexNames_target = -1;
                    if (originFeatureclass.FindField("NAMES_") > 0)
                    {
                        indexNames_origin = originFeatureclass.FindField("NAMES_");
                        indexNames_target = targetFeatureclass.FindField("NAMES");
                    }

                    int indexTime_origin = -1;
                    int indexTime_target = -1;
                    if (originFeatureclass.FindField("TIME_") > 0)
                    {
                        indexTime_origin = originFeatureclass.FindField("TIME_");
                        indexTime_target = targetFeatureclass.FindField("TIME");
                    }
                    #endregion

                    //遍历赋值 :
                    else if (originFeatureclass.FeatureCount(qf) > 0)
                    {
                        IList<string> fieldNameList = new List<string>();
                        fieldNameList = GetAttributeList(targetWorkspace, targetFcl, true);
                        sqltest = qf.WhereClause;
                        IFeatureCursor targetCursor = targetFeatureclass.Insert(true);
                        IFeatureCursor pFeatureCursor = originFeatureclass.Search(qf, false);
                        IFeature pFeature = null;

                        while ((pFeature = pFeatureCursor.NextFeature()) != null)
                        {
                            IFeatureBuffer newFeatureBuf = targetFeatureclass.CreateFeatureBuffer();
                            IGeometry geo = pFeature.Shape as IGeometry;
                            newFeatureBuf.Shape = geo;

                            //属性赋值
                            for (int j = 0; j < fieldNameList.Count; j++)
                            {
                                if (fieldNameList[j] == "DATE" && indexDate_orgin > 0)
                                {
                                    IField feild = targetFeatureclass.Fields.get_Field(indexDate_orgin);
                                    if (feild.Editable)
                                    {
                                        object value = pFeature.get_Value(indexDate_orgin);
                                        if (value.ToString() != "" && value != null)
                                        {
                                            newFeatureBuf.set_Value(indexDate_target, value);
                                        }
                                    }
                                }
                                else if (fieldNameList[j] == "NAMES" && indexNames_origin > 0)
                                {
                                    IField feild = targetFeatureclass.Fields.get_Field(indexNames_origin);
                                    if (feild.Editable)
                                    {
                                        object value = pFeature.get_Value(indexNames_origin);
                                        if (value.ToString() != "" && value != null)
                                        {
                                            newFeatureBuf.set_Value(indexNames_target, value);
                                        }
                                    }
                                }
                                else if (fieldNameList[j] == "TIME" && indexTime_origin > 0)
                                {
                                    IField feild = targetFeatureclass.Fields.get_Field(indexTime_origin);
                                    if (feild.Editable)
                                    {
                                        object value = pFeature.get_Value(indexTime_origin);
                                        if (value.ToString() != "" && value != null)
                                        {
                                            newFeatureBuf.set_Value(indexTime_target, value);
                                        }
                                    }
                                }
                                else
                                {
                                    int indexOrigin = originFeatureclass.FindField(fieldNameList[j]);
                                    int indexTarget = targetFeatureclass.FindField(fieldNameList[j]);
                                    if (indexTarget != -1 && indexOrigin != -1)
                                    {
                                        IField feild = targetFeatureclass.Fields.get_Field(indexTarget);
                                        if (feild.Editable)
                                        {
                                            object value = pFeature.get_Value(indexOrigin);
                                            if (value.ToString() != "" && value != null)
                                            {
                                                newFeatureBuf.set_Value(indexTarget, value);
                                            }
                                        }
                                    }
                                }
                            }
                            targetCursor.InsertFeature(newFeatureBuf);
                            System.Runtime.InteropServices.Marshal.ReleaseComObject(pFeature);
                        }
                        targetCursor.Flush();

                        System.Runtime.InteropServices.Marshal.ReleaseComObject(pFeatureCursor);
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(targetCursor);
                    }
                    pFCLoad.LoadOnlyMode = false;

                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.Source);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                MessageBox.Show(ex.Message + ":" + sqltest);
                MessageBox.Show(ex.StackTrace);
                return false;
            }

            return true;
        }

        //复制模板数据库
        private IWorkspace CopyTemplateDatabase(IWorkspace originWorkspace, string targetFolderPath, string name)
        {
            string msg = "";
            Copy pCopy = new Copy();
            pCopy.in_data = originWorkspace.PathName;
            pCopy.out_data = targetFolderPath + "\\" + name;

            try
            {
                _app.GPTool.Execute(pCopy, null);
            }
            catch (Exception err)
            {
                System.Diagnostics.Trace.WriteLine(err.Message);
                System.Diagnostics.Trace.WriteLine(err.Source);
                System.Diagnostics.Trace.WriteLine(err.StackTrace);

                msg += err.Message;
                msg += CommonMethods.ReturnGPMessages(_app.GPTool);
                MessageBox.Show(msg);
            }
            IWorkspaceFactory wsFactory = new FileGDBWorkspaceFactoryClass();
            IWorkspace ws = wsFactory.OpenFromFile(targetFolderPath + "\\" + name, 0);

            return ws;
        }

        public IWorkspace CopyTemplateDatabaseZT(GApplication app,
            string outputGDB, IWorkspace sourceWorkspace, IWorkspace templateWorkspace,
            Dictionary<string, IFeatureClass> themTemplateFCName2FC, bool needAddNewFC,
            ref Dictionary<string, IFeatureClass> inFCName2FC,
            ref Dictionary<string, IFeatureClass> outFCName2FC, Dictionary<string, string> themTemplateFCName2themName) 
        {
            try
            {
                bool hasThematic = false;
                IFeatureWorkspace templateFWS = templateWorkspace as IFeatureWorkspace;
                IFeatureWorkspace sourceFWS = sourceWorkspace as IFeatureWorkspace;

                //以底图模板数据库为基础，创建输出数据库
                if (System.IO.Directory.Exists(outputGDB))
                {
                    System.IO.Directory.Delete(outputGDB, true);
                }
                IWorkspace outputWorkspace = CopyTemplateDatabase(app, templateWorkspace, outputGDB);
                IFeatureWorkspace outputFWS = outputWorkspace as IFeatureWorkspace;
                bool bRedefineSRF = false;
                ISpatialReference sr = null;

                //获取当前输出数据库中的要素类信息
                outFCName2FC = getFeatureClassList(outputWorkspace);

                //遍历源数据库（将底图模板数据库中不存在的要素类视情况添加至输出数据库中）
                IEnumDataset sourceEnumDataset = sourceWorkspace.get_Datasets(esriDatasetType.esriDTAny);
                sourceEnumDataset.Reset();
                IDataset sourceDataset = null;
                while ((sourceDataset = sourceEnumDataset.Next()) != null)
                {
                    if (sourceDataset is IFeatureDataset)//要素数据集
                    {
                        if (sr == null)
                        {
                            sr = (sourceDataset as IGeoDataset).SpatialReference;
                        }

                        if (!bRedefineSRF && (templateWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureDataset, sourceDataset.Name))
                        {
                            //重定义基于底图模板数据库复制而来的输出数据库的空间参考
                            ReDefineWorkspaceSpatialReference(outputWorkspace, (sourceDataset as IGeoDataset).SpatialReference);
                            bRedefineSRF = true;
                        }

                        IFeatureDataset outputFeatureDataset = null;

                        //输出数据库（模板数据库）中是否存在该数据集，则存在则获取该数据集，否则在输出数据库中也不创建该数据集
                        if ((outputWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureDataset, sourceDataset.Name))
                        {
                            outputFeatureDataset = outputFWS.OpenFeatureDataset(sourceDataset.Name);
                        }


                        //遍历子要素类
                        IFeatureDataset sourceFeatureDataset = sourceDataset as IFeatureDataset;
                        IEnumDataset sourceEnumDatasetF = sourceFeatureDataset.Subsets;
                        sourceEnumDatasetF.Reset();
                        IDataset sourceDatasetF = null;
                        while ((sourceDatasetF = sourceEnumDatasetF.Next()) != null)
                        {
                            if (sourceDatasetF is IFeatureClass)//要素类
                            {
                                IFeatureClass outputFC = null;

                                if (!(outputWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, sourceDatasetF.Name))//底图模板数据库中不存在该要素类
                                {
                                    if (themTemplateFCName2FC.ContainsKey(sourceDatasetF.Name))//判断是否为专题数据库中的要素类
                                    {
                                        #region 追加专题要素类（仅结构）
                                        IFeatureClass fc = themTemplateFCName2FC[sourceDatasetF.Name];//专题模板数据库中的要素类（空要素类）
                                        #region 方法1：CreateFeatureClass方法（制图表达丢失？？）
                                        //IFields fields = getFeatureClassFields(fc, (sourceDatasetF as IGeoDataset).SpatialReference);
                                        //esriFeatureType featureType = fc.FeatureType;
                                        //string shapeFieldName = fc.ShapeFieldName;

                                        ////创建新的要素类
                                        //if (outputFeatureDataset != null)
                                        //{
                                        //    outputFC = outputFeatureDataset.CreateFeatureClass(sourceDatasetF.Name, fields, null, null, featureType, shapeFieldName, "");
                                        //}
                                        //else
                                        //{
                                        //    outputFC = outputFWS.CreateFeatureClass(sourceDatasetF.Name, fields, null, null, featureType, shapeFieldName, "");
                                        //}
                                        #endregion

                                        #region 方法2：GP工具
                                        string featureDatasetName = "";
                                        if (outputFeatureDataset != null)
                                        {
                                            featureDatasetName = outputFeatureDataset.Name;
                                        }
                                        outputFC = CopyFCStruct2Database(app, fc, outputWorkspace, featureDatasetName, (sourceDatasetF as IGeoDataset).SpatialReference);
                                        hasThematic = true;
                                        #endregion

                                        #endregion


                                    }
                                    else//非模板数据库中的要素类
                                    {
                                        if (needAddNewFC)
                                        {
                                            #region 直接全部复制（包括数据）
                                            IFeatureClass fc = sourceDatasetF as IFeatureClass;

                                            string featureDatasetName = "";
                                            if (outputFeatureDataset != null)
                                            {
                                                featureDatasetName = outputFeatureDataset.Name;
                                            }

                                            outputFC = CopyFC2Database(app, fc, outputWorkspace, featureDatasetName, (sourceDatasetF as IGeoDataset).SpatialReference);
                                            #endregion
                                        }
                                    }
                                }

                                //添加输出数据库中的要素类
                                if (outputFC != null)
                                    outFCName2FC.Add(sourceDatasetF.Name.ToUpper(), outputFC);
                                //添加输入数据库中的要素类
                                inFCName2FC.Add(sourceDatasetF.Name.ToUpper(), sourceDatasetF as IFeatureClass);
                            }
                        }

                        System.Runtime.InteropServices.Marshal.ReleaseComObject(sourceEnumDatasetF);
                    }
                    else if (sourceDataset is IFeatureClass)//要素类
                    {
                        if (sr == null)
                        {
                            sr = (sourceDataset as IGeoDataset).SpatialReference;
                        }

                        if (!bRedefineSRF && (templateWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, sourceDataset.Name))
                        {
                            //重定义基于底图模板数据库复制而来的输出数据库的空间参考
                            ReDefineWorkspaceSpatialReference(outputWorkspace, (sourceDataset as IGeoDataset).SpatialReference);
                            bRedefineSRF = true;
                        }

                        IFeatureClass outputFC = null;

                        if (!(outputWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, sourceDataset.Name))//底图模板数据库中不存在该要素类
                        {
                            if (themTemplateFCName2FC.ContainsKey(sourceDataset.Name))//判断是否为专题数据库中的要素类
                            {
                                #region 追加专题要素类（仅结构）
                                IFeatureClass fc = themTemplateFCName2FC[sourceDataset.Name];//专题模板数据库中的要素类

                                #region 方法1：CreateFeatureClass方法（制图表达丢失？？）
                                //IFields fields = getFeatureClassFields(fc, (sourceDataset as IGeoDataset).SpatialReference);
                                //esriFeatureType featureType = fc.FeatureType;
                                //string shapeFieldName = fc.ShapeFieldName;

                                ////创建新的要素类
                                //outputFC = outputFWS.CreateFeatureClass(sourceDataset.Name, fields, null, null, featureType, shapeFieldName, "");
                                #endregion

                                #region 方法2：GP工具
                                outputFC = CopyFCStruct2Database(app, fc, outputWorkspace, "", (sourceDataset as IGeoDataset).SpatialReference);
                                hasThematic = true;
                                #endregion

                                #endregion
                            }
                            else//非模板数据库中的要素类
                            {
                                if (needAddNewFC)
                                {
                                    #region 直接全部复制（包括数据）
                                    IFeatureClass fc = sourceDataset as IFeatureClass;

                                    outputFC = CopyFC2Database(app, fc, outputWorkspace, "", (sourceDataset as IGeoDataset).SpatialReference);
                                    #endregion
                                }
                            }

                        }

                        //添加输出数据库中的要素类
                        if (outputFC != null)
                            outFCName2FC.Add(sourceDataset.Name.ToUpper(), outputFC);
                        //添加输入数据库中的要素类
                        inFCName2FC.Add(sourceDataset.Name.ToUpper(), sourceDataset as IFeatureClass);
                    }
                    else if (sourceDataset is IRasterDataset)//栅格数据集
                    {
                        IRasterDataset rasterDataset = (sourceWorkspace as IRasterWorkspaceEx).OpenRasterDataset(sourceDataset.Name);

                        //复制栅格数据集到输出数据库
                        IRasterValue rasterValue = new RasterValueClass();
                        rasterValue.RasterDataset = rasterDataset;
                        (outputWorkspace as IRasterWorkspaceEx).SaveAsRasterDataset(sourceDataset.Name, rasterDataset.CreateDefaultRaster(), rasterValue.RasterStorageDef, "", null, null);
                    }
                    else if (sourceDataset is IRasterCatalog)
                    {
                        //暂不支持
                    }
                    else if (sourceDataset is IMosaicDataset)
                    {
                        //暂不支持
                    }
                    else
                    {
                        //暂不支持
                    }
                }
                System.Runtime.InteropServices.Marshal.ReleaseComObject(sourceEnumDataset);

                //复制专题中的注记层（模板中有而输入数据库中没有）
                foreach (var kv in themTemplateFCName2FC)
                {
                    if (hasThematic)
                    {
                        //if ((outputWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, kv.Key))//有对应的专题图层
                        {
                            string themName = kv.Key;
                            if (!(outputWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, themName))//若输出库中没有此专题注记层，则添加
                            {
                                IFeatureClass annoFC = themTemplateFCName2FC[themName];
                                var outputFC = CopyFCStruct2Database(app, annoFC, outputWorkspace, "", sr);//空间参考
                                outFCName2FC.Add(themName, outputFC);
                            }
                        }
                    }

                }
                return outputWorkspace;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        //复制模板要素类
        private void CopyTemplateFcl(IFeatureClass fcl, IWorkspace targetWorkspace, ISpatialReference targetSpatialRef)
        {
            string msg = "";
            Copy pCopy = new Copy();
            pCopy.in_data = (fcl as IDataset).Workspace.PathName + "\\" + fcl.AliasName;
            pCopy.out_data = targetWorkspace.PathName + "\\" + fcl.AliasName;

            try
            {
                _app.GPTool.Execute(pCopy, null);
                //
                var targetFcl = (targetWorkspace as IFeatureWorkspace).OpenFeatureClass(fcl.AliasName);
                IGeoDataset geoDataset = targetFcl as IGeoDataset;
                ISpatialReference spatialRefInDataset = geoDataset.SpatialReference;
                IClone sfCloneInDataset = spatialRefInDataset as IClone;
                var sfClone = targetSpatialRef as IClone;
                if (!sfCloneInDataset.IsEqual(sfClone))
                {
                    IGeoDatasetSchemaEdit pGeoDatasetSchemaEdit = geoDataset as IGeoDatasetSchemaEdit;
                    if (pGeoDatasetSchemaEdit.CanAlterSpatialReference == true)
                    {
                        pGeoDatasetSchemaEdit.AlterSpatialReference(targetSpatialRef);
                    }
                }
            }
            catch (Exception err)
            {
                msg += err.Message;
                msg += CommonMethods.ReturnGPMessages(_app.GPTool);

                throw new Exception(msg);
            }
            return;

        }



        //定义投影
        private ISpatialReference DeifineSpatialRef(IWorkspace ws, IClone sfClone, ISpatialReference targetSpatialRef)
        {
            ISpatialReference tempSf = null;
            IEnumDataset enumFeatureclass = ws.get_Datasets(esriDatasetType.esriDTAny);
            IDataset dataset = null;
            while ((dataset = enumFeatureclass.Next()) != null)
            {
                if (dataset is IFeatureDataset)
                {
                    if (dataset.Name == "位置图")
                    {
                        continue;
                    }
                    IGeoDataset geoDataset = dataset as IGeoDataset;
                    ISpatialReference spatialRefInDataset = geoDataset.SpatialReference;
                    if (tempSf == null)
                        tempSf = spatialRefInDataset;
                    IClone sfCloneInDataset = spatialRefInDataset as IClone;
                    if (!sfCloneInDataset.IsEqual(sfClone))
                    {
                        IGeoDatasetSchemaEdit pGeoDatasetSchemaEdit = geoDataset as IGeoDatasetSchemaEdit;
                        if (pGeoDatasetSchemaEdit.CanAlterSpatialReference == true)
                        {
                            pGeoDatasetSchemaEdit.AlterSpatialReference(targetSpatialRef);
                        }
                    }

                    IEnumDataset enumSubset = dataset.Subsets;
                    enumSubset.Reset();
                    IDataset subsetDataset = enumSubset.Next();
                    while (subsetDataset != null)
                    {
                        if (subsetDataset is IFeatureClass)
                        {
                            IGeoDataset geoSubDataset = subsetDataset as IGeoDataset;
                            ISpatialReference spatialRefInSubDataset = geoSubDataset.SpatialReference;
                            IClone sfCloneInSubDataset = spatialRefInSubDataset as IClone;
                            if (!sfCloneInSubDataset.IsEqual(sfClone))
                            {
                                IGeoDatasetSchemaEdit pGeoDatasetSchemaEdit = geoSubDataset as IGeoDatasetSchemaEdit;
                                if (pGeoDatasetSchemaEdit.CanAlterSpatialReference == true)
                                {
                                    pGeoDatasetSchemaEdit.AlterSpatialReference(targetSpatialRef);
                                }
                            }
                        }
                        subsetDataset = enumSubset.Next();
                    }
                }

                if (dataset is IFeatureClass)
                {
                    IGeoDataset geoDataset = dataset as IGeoDataset;
                    ISpatialReference spatialRefInDataset = geoDataset.SpatialReference;
                    if (tempSf == null)
                        tempSf = spatialRefInDataset;
                    IClone sfCloneInDataset = spatialRefInDataset as IClone;
                    if (!sfCloneInDataset.IsEqual(sfClone))
                    {
                        IGeoDatasetSchemaEdit pGeoDatasetSchemaEdit = geoDataset as IGeoDatasetSchemaEdit;
                        if (pGeoDatasetSchemaEdit.CanAlterSpatialReference == true)
                        {
                            pGeoDatasetSchemaEdit.AlterSpatialReference(targetSpatialRef);
                        }
                    }
                }
                System.Runtime.InteropServices.Marshal.ReleaseComObject(dataset);
            }
            System.Runtime.InteropServices.Marshal.ReleaseComObject(enumFeatureclass);
            return tempSf;
        }


        //判断一个数据库中是否存在某一图层
        private bool ExistLayerOrNot(IWorkspace workspace, string name)
        {
            return (workspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, name);
        }

        //得到一个要素类的属性信息列表(可以判断是否加上系统属性字段)
        private IList<string> GetAttributeList(IWorkspace ws, string fcName, bool bIgnoreSystemField = true)
        {
            IFeatureWorkspace feaWS = ws as IFeatureWorkspace;
            IFeatureClass fc = feaWS.OpenFeatureClass(fcName);

            IList<string> fieldList = new List<string>();

            for (int i = 0; i < fc.Fields.FieldCount; i++)
            {
                IField pField = fc.Fields.get_Field(i);
                string fieldName = pField.Name;

                //判断是否过滤系统属性字段
                if (bIgnoreSystemField)
                {
                    if (fieldName != fc.OIDFieldName && fieldName != fc.ShapeFieldName && pField != fc.LengthField && pField != fc.AreaField)
                    {
                        if (fieldName.StartsWith("RULEID") || fieldName.StartsWith("OVERRIDE"))
                        {
                            continue;
                        }
                        fieldList.Add(fieldName);
                    }
                }
                else
                {
                    fieldList.Add(fieldName);
                }
            }
            return fieldList;
        }


        //初始化专题要素类信息
        private List<ThematicTemplate> ThematicFcls = null;
        public Dictionary<string, string> ThematicLyrNames = new Dictionary<string, string>();//lyr-专题名称
        private void IntilThematicGDB()
        {
            ThematicFcls = new List<ThematicTemplate>();
            string dirpath = GApplication.Application.Template.Root + "\\专题\\";
            DirectoryInfo dirs = new DirectoryInfo(dirpath);
            Type factoryType = Type.GetTypeFromProgID("esriDataSourcesGDB.FileGDBWorkspaceFactory");
            IWorkspaceFactory workspaceFactory = (IWorkspaceFactory)Activator.CreateInstance(factoryType);
            //foreach (var dir in dirs.GetDirectories())
            if (!CommonMethods.ThemData)
                return;
            string dirName = CommonMethods.ThemDataBase;
            {
                string gdb = dirpath + dirName + "\\Template.gdb";
                if (Directory.Exists(gdb))
                {
                    ThematicTemplate thematicgdb = new ThematicTemplate();
                    Dictionary<string, IFeatureClass> dic = new Dictionary<string, IFeatureClass>();
                    IWorkspace ws = workspaceFactory.OpenFromFile(gdb, 0);
                    IEnumDataset pEnumDataset = ws.get_Datasets(esriDatasetType.esriDTAny);
                    pEnumDataset.Reset();
                    IDataset pDataset = null;
                    while ((pDataset = pEnumDataset.Next()) != null)
                    {
                        #region
                        if (pDataset is IFeatureDataset)//数据集
                        {
                            var enumds = (pDataset as IFeatureDataset).Subsets;
                            enumds.Reset();
                            IDataset ds = null;
                            while ((ds = enumds.Next()) != null)
                            {
                                if (ds is IFeatureClass)
                                {
                                    dic[ds.Name] = ds as IFeatureClass;
                                }
                            }
                            System.Runtime.InteropServices.Marshal.ReleaseComObject(enumds);
                        }
                        if (pDataset is IFeatureClass)
                        {
                            dic[pDataset.Name] = pDataset as IFeatureClass;
                        }
                        #endregion

                    }

                    System.Runtime.InteropServices.Marshal.ReleaseComObject(pEnumDataset);

                    thematicgdb.FclInfos = dic;
                    thematicgdb.Name = dirName;
                    ThematicFcls.Add(thematicgdb);
                }
            }
        }

        void AddField(IFeatureClass fCls, string fieldName)
        {
            if (fCls.FindField(fieldName) != -1)
                return;
            IFields pFields = fCls.Fields;
            IFieldsEdit pFieldsEdit = pFields as IFieldsEdit;
            IField pField = new FieldClass();
            IFieldEdit pFieldEdit = pField as IFieldEdit;
            pFieldEdit.Name_2 = fieldName;
            pFieldEdit.AliasName_2 = fieldName;
            // pFieldEdit.Length_2 = 2147483647;
            pFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            IClass pTable = fCls as IClass;
            pTable.AddField(pField);
            pFieldsEdit = null;
            pField = null;
        }

        /// <summary>
        /// 支持专题数据
        /// </summary>
        private string CopyFeatureClassFromOriginToTargetTh(IWorkspace originWorkspace, IWorkspace targetWorkspace, List<string> sourceFCNames)
        {
            IFeatureWorkspace originFeatureWorkspace = originWorkspace as IFeatureWorkspace;
            IFeatureWorkspace targetFeatureWorkspace = targetWorkspace as IFeatureWorkspace;
            string lyrName = "";
            try
            {
                for (int i = 0; i < sourceFCNames.Count; i++)
                {
                    if (i == 20)
                    {
                    }
                    #region
                    lyrName = sourceFCNames[i];
                    IFeatureClass originFeatureclass = originFeatureWorkspace.OpenFeatureClass(sourceFCNames[i]);
                    IFeatureClass targetFeatureclass = null;
                    //如何不存在,判断是否为专题要素。创建新的要素类
                    if (!ExistLayerOrNot(targetWorkspace, sourceFCNames[i]))
                    {
                        #region
                        var list = ThematicFcls.Where(t => t.FclInfos.ContainsKey(sourceFCNames[i])).FirstOrDefault();
                        if (list != null) //专题要素类
                        {
                            ThematicLyrNames.Add(sourceFCNames[i], list.Name);

                            IFeatureClass thematicfcl = list.FclInfos[sourceFCNames[i]];
                            CopyTemplateFcl(thematicfcl, targetFeatureWorkspace as IWorkspace, (originFeatureclass as IGeoDataset).SpatialReference);
                        }
                        else //新的要素类,创建
                        {
                            IFields fields = createFeatureClassFields(originFeatureclass);
                            if (null == targetFeatureWorkspace.CreateFeatureClass(sourceFCNames[i], fields, null, null, esriFeatureType.esriFTSimple, originFeatureclass.ShapeFieldName, ""))
                            {
                                throw new Exception(string.Format("在输出数据库中创建要素类【{0}】失败!", sourceFCNames[i]));
                            }
                        }
                        #endregion
                    }
                    //新疆模板单独处理
                    string template = GApplication.Application.Template.Root;
                    targetFeatureclass = targetFeatureWorkspace.OpenFeatureClass(sourceFCNames[i]);
                    //attach 字段
                    AddField(targetFeatureclass, "ATTACH");

                    IFeatureClassLoad pFCLoad = targetFeatureclass as IFeatureClassLoad;
                    pFCLoad.LoadOnlyMode = true;

                    //获取不一样属性字段的索引
                    int indexDate_orgin = -1;
                    int indexDate_target = -1;

                    if (originFeatureclass.FindField("DATE_") > 0)
                    {
                        indexDate_orgin = originFeatureclass.FindField("DATE_");
                        indexDate_target = targetFeatureclass.FindField("DATE");
                    }

                    int indexNames_origin = -1;
                    int indexNames_target = -1;
                    if (originFeatureclass.FindField("NAMES_") > 0)
                    {
                        indexNames_origin = originFeatureclass.FindField("NAMES_");
                        indexNames_target = targetFeatureclass.FindField("NAMES");
                    }

                    int indexTime_origin = -1;
                    int indexTime_target = -1;
                    if (originFeatureclass.FindField("TIME_") > 0)
                    {
                        indexTime_origin = originFeatureclass.FindField("TIME_");
                        indexTime_target = targetFeatureclass.FindField("TIME");
                    }

                    //遍历赋值
                    if (originFeatureclass.FeatureCount(null) > 0)
                    {
                        IList<string> fieldNameList = new List<string>();
                        fieldNameList = GetAttributeList(targetWorkspace, sourceFCNames[i], true);

                        IFeatureCursor targetCursor = targetFeatureclass.Insert(true);
                        IFeatureCursor pFeatureCursor = originFeatureclass.Search(null, false);
                        IFeature pFeature = null;

                        while ((pFeature = pFeatureCursor.NextFeature()) != null)
                        {
                            IFeatureBuffer newFeatureBuf = targetFeatureclass.CreateFeatureBuffer();
                            IGeometry geo = pFeature.Shape as IGeometry;
                            newFeatureBuf.Shape = geo;

                            //属性赋值
                            int num = 0;
                            for (int j = 0; j < fieldNameList.Count; j++)
                            {
                                if (fieldNameList[j] == "DATE" && indexDate_orgin > 0)
                                {
                                    IField feild = targetFeatureclass.Fields.get_Field(indexDate_orgin);
                                    if (feild.Editable)
                                    {
                                        object value = pFeature.get_Value(indexDate_orgin);
                                        if (value.ToString() != "" && value != null)
                                        {
                                            newFeatureBuf.set_Value(indexDate_target, value);
                                        }
                                    }
                                }
                                else if (fieldNameList[j] == "NAMES" && indexNames_origin > 0)
                                {
                                    IField feild = targetFeatureclass.Fields.get_Field(indexNames_origin);
                                    if (feild.Editable)
                                    {
                                        object value = pFeature.get_Value(indexNames_origin);
                                        if (value.ToString() != "" && value != null)
                                        {
                                            newFeatureBuf.set_Value(indexNames_target, value);
                                        }
                                    }
                                }
                                else if (fieldNameList[j] == "TIME" && indexTime_origin > 0)
                                {
                                    IField feild = targetFeatureclass.Fields.get_Field(indexTime_origin);
                                    if (feild.Editable)
                                    {
                                        object value = pFeature.get_Value(indexTime_origin);
                                        if (value.ToString() != "" && value != null)
                                        {
                                            newFeatureBuf.set_Value(indexTime_target, value);
                                        }
                                    }
                                }
                                else
                                {
                                    int indexOrigin = originFeatureclass.FindField(fieldNameList[j]);
                                    int indexTarget = targetFeatureclass.FindField(fieldNameList[j]);
                                    if (indexTarget != -1 && indexOrigin != -1)
                                    {
                                        IField feild = targetFeatureclass.Fields.get_Field(indexTarget);
                                        if (feild.Editable)
                                        {
                                            object value = pFeature.get_Value(indexOrigin);
                                            if (value.ToString() != "" && value != null)
                                            {
                                                newFeatureBuf.set_Value(indexTarget, value);
                                            }
                                        }
                                    }
                                }
                            }
                            targetCursor.InsertFeature(newFeatureBuf);
                            System.Runtime.InteropServices.Marshal.ReleaseComObject(newFeatureBuf);
                            System.Runtime.InteropServices.Marshal.ReleaseComObject(pFeature);

                            num++;
                            if (num > 1000)
                            {
                                num = 0;
                                targetCursor.Flush();
                            }
                        }
                        targetCursor.Flush();

                        System.Runtime.InteropServices.Marshal.ReleaseComObject(pFeatureCursor);
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(targetCursor);
                    }
                    pFCLoad.LoadOnlyMode = false;
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(originFeatureclass);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(targetFeatureclass);
                    #endregion
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.Source);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                return string.Format("拷贝图层【{0}】的要素数据到导出的文件数据库失败:{1}", lyrName, ex.Message);
            }

            return "";
        }


        //将原数据库中的栅格数据集复制到targetWorkspace，若targetWorkspace中已存在该栅格数据集，则跳过
        private string CopyRasterDatasetFromOriginToTarget(IWorkspace originWorkspace, IWorkspace targetWorkspace, List<string> sourceRasterDatasetNames)
        {
            IRasterWorkspaceEx originRasterWorkspace = originWorkspace as IRasterWorkspaceEx;
            IRasterWorkspaceEx targetRasterWorkspace = targetWorkspace as IRasterWorkspaceEx;

            string lyrName = "";
            try
            {
                for (int i = 0; i < sourceRasterDatasetNames.Count; i++)
                {
                    lyrName = sourceRasterDatasetNames[i];

                    if ((targetWorkspace as IWorkspace2).get_NameExists(esriDatasetType.esriDTRasterDataset, sourceRasterDatasetNames[i]))
                    {
                        continue;
                    }

                    IRasterDataset rasterDataset = originRasterWorkspace.OpenRasterDataset(sourceRasterDatasetNames[i]);

                    //复制
                    IRasterValue rasterValue = new RasterValueClass();
                    rasterValue.RasterDataset = rasterDataset;
                    targetRasterWorkspace.SaveAsRasterDataset(sourceRasterDatasetNames[i], rasterDataset.CreateDefaultRaster(), rasterValue.RasterStorageDef, "", null, null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.Source);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                return string.Format("拷贝图层【{0}】的数据到导出的文件数据库失败:{1}", lyrName, ex.Message);
            }


            return "";
        }

        /// <summary>
        /// 获取要素类的字段结构信息
        /// </summary>
        /// <param name="pSourceFeatureClass"></param>
        /// <returns></returns>
        private IFields createFeatureClassFields(IFeatureClass pSourceFeatureClass)
        {
            //获取源要素类的字段结构信息
            IFields targetFields = null;
            IObjectClassDescription featureDescription = new FeatureClassDescriptionClass();
            targetFields = featureDescription.RequiredFields; //要素类自带字段
            for (int i = 0; i < pSourceFeatureClass.Fields.FieldCount; ++i)
            {
                IField field = pSourceFeatureClass.Fields.get_Field(i);

                if (field.Type == esriFieldType.esriFieldTypeGeometry)
                {
                    (targetFields as IFieldsEdit).set_Field(targetFields.FindFieldByAliasName((featureDescription as IFeatureClassDescription).ShapeFieldName),
                        (field as ESRI.ArcGIS.esriSystem.IClone).Clone() as IField);

                    continue;
                }

                if (targetFields.FindField(field.Name) != -1)//已包含该字段（要素类自带字段）
                {
                    continue;
                }

                //剔除sde数据中的"st_area_shape_"、"st_length_shape_"
                if ("st_area_shape_" == field.Name.ToLower() || "st_length_shape_" == field.Name.ToLower())
                {
                    continue;
                }

                IField newField = (field as ESRI.ArcGIS.esriSystem.IClone).Clone() as IField;
                (targetFields as IFieldsEdit).AddField(newField);
            }

            IGeometryDef pGeometryDef = new GeometryDefClass();
            IGeometryDefEdit pGeometryDefEdit = pGeometryDef as IGeometryDefEdit;
            pGeometryDefEdit.SpatialReference_2 = (pSourceFeatureClass as IGeoDataset).SpatialReference;
            for (int i = 0; i < targetFields.FieldCount; i++)
            {
                IField pfield = targetFields.get_Field(i);
                if (pfield.Type == esriFieldType.esriFieldTypeOID)
                {
                    IFieldEdit pFieldEdit = (IFieldEdit)pfield;
                    pFieldEdit.Name_2 = pfield.AliasName;
                }

                if (pfield.Type == esriFieldType.esriFieldTypeGeometry)
                {
                    pGeometryDefEdit.GeometryType_2 = pfield.GeometryDef.GeometryType;
                    IFieldEdit pFieldEdit = (IFieldEdit)pfield;
                    pFieldEdit.Name_2 = pfield.AliasName;
                    pFieldEdit.GeometryDef_2 = pGeometryDef;
                    break;
                }

            }

            return targetFields;
        }

        /// <summary>
        /// 获取数据库的栅格数据集名称
        /// </summary>
        /// <param name="ws"></param>
        /// <param name="rasterDatasetNames"></param>
        private void getRasterDatasetNames(IWorkspace ws, ref List<string> rasterDatasetNames)
        {
            if (null == ws)
                return;

            IEnumDataset pEnumDataset = ws.get_Datasets(esriDatasetType.esriDTAny);
            pEnumDataset.Reset();
            IDataset pDataset = pEnumDataset.Next();
            while (pDataset != null)
            {
                if (pDataset is IRasterDataset)//栅格数据集
                {
                    rasterDatasetNames.Add(pDataset.Name);
                }
                else if (pDataset is IRasterCatalog)
                {

                }
                else if (pDataset is IMosaicDataset)
                {

                }
                else
                {

                }

                pDataset = pEnumDataset.Next();

            }

            System.Runtime.InteropServices.Marshal.ReleaseComObject(pEnumDataset);

        }
        //专题模板信息
        class ThematicTemplate
        {
            public string Name;//专题名称
            public Dictionary<string, IFeatureClass> FclInfos;//名称->要素类
        }

        private static IWorkspace CopyTemplateDatabase(GApplication app, IWorkspace templateWorkspace, string outputGDB)
        {
            string msg = "";

            Copy pCopy = new Copy();
            pCopy.in_data = templateWorkspace.PathName;
            pCopy.out_data = outputGDB;

            IWorkspace ws = null;
            try
            {
                app.GPTool.Execute(pCopy, null);

                IWorkspaceFactory wsFactory = new FileGDBWorkspaceFactoryClass();
                ws = wsFactory.OpenFromFile(outputGDB, 0);

            }
            catch (Exception err)
            {
                msg += err.Message;
                msg += CommonMethods.ReturnGPMessages(app.GPTool);
                MessageBox.Show(msg);
            }

            return ws;
        }

        private static Dictionary<string, IFeatureClass> getFeatureClassList(IWorkspace ws)
        {
            Dictionary<string, IFeatureClass> fcName2FC = new Dictionary<string, IFeatureClass>();

            if (null == ws)
                return fcName2FC;

            IEnumDataset enumDataset = ws.get_Datasets(esriDatasetType.esriDTAny);
            enumDataset.Reset();
            IDataset dataset = null;
            while ((dataset = enumDataset.Next()) != null)
            {
                if (dataset is IFeatureDataset)//要素数据集
                {
                    IFeatureDataset featureDataset = dataset as IFeatureDataset;
                    IEnumDataset subEnumDataset = featureDataset.Subsets;
                    subEnumDataset.Reset();
                    IDataset subDataset = null;
                    while ((subDataset = subEnumDataset.Next()) != null)
                    {
                        if (subDataset is IFeatureClass)//要素类
                        {
                            IFeatureClass fc = subDataset as IFeatureClass;
                            if (fc != null)
                                fcName2FC.Add(subDataset.Name.ToUpper(), fc);
                        }
                    }

                    System.Runtime.InteropServices.Marshal.ReleaseComObject(subEnumDataset);
                }
                else if (dataset is IFeatureClass)//要素类
                {
                    IFeatureClass fc = dataset as IFeatureClass;
                    if (fc != null)
                        fcName2FC.Add(dataset.Name.ToUpper(), fc);
                }
                else
                {

                }

            }

            System.Runtime.InteropServices.Marshal.ReleaseComObject(enumDataset);

            return fcName2FC;
        }

        private static void ReDefineWorkspaceSpatialReference(IWorkspace ws, ISpatialReference targetSpatialRef)
        {
            IClone targetSpatialRefClone = targetSpatialRef as IClone;

            IEnumDataset enumDataset = ws.get_Datasets(esriDatasetType.esriDTAny);
            IDataset dataset = null;
            while ((dataset = enumDataset.Next()) != null)
            {
                if (dataset is IFeatureDataset)
                {
                    if (dataset.Name == "位置图")
                    {
                        continue;
                    }

                    ISpatialReference spatialRef = (dataset as IGeoDataset).SpatialReference;
                    IClone spatialRefClone = spatialRef as IClone;
                    if (!targetSpatialRefClone.IsEqual(spatialRefClone))
                    {
                        IGeoDatasetSchemaEdit pGeoDatasetSchemaEdit = dataset as IGeoDatasetSchemaEdit;
                        if (pGeoDatasetSchemaEdit.CanAlterSpatialReference == true)
                        {
                            pGeoDatasetSchemaEdit.AlterSpatialReference(targetSpatialRef);
                        }
                    }

                    IEnumDataset enumSubset = dataset.Subsets;
                    enumSubset.Reset();
                    IDataset subsetDataset = enumSubset.Next();
                    while (subsetDataset != null)
                    {
                        if (subsetDataset is IFeatureClass)
                        {
                            if (!targetSpatialRefClone.IsEqual(spatialRefClone))
                            {
                                IGeoDatasetSchemaEdit pGeoDatasetSchemaEdit = subsetDataset as IGeoDatasetSchemaEdit;
                                if (pGeoDatasetSchemaEdit.CanAlterSpatialReference == true)
                                {
                                    pGeoDatasetSchemaEdit.AlterSpatialReference(targetSpatialRef);
                                }
                            }
                        }
                        subsetDataset = enumSubset.Next();
                    }
                }
                else if (dataset is IFeatureClass)
                {
                    ISpatialReference spatialRef = (dataset as IGeoDataset).SpatialReference;
                    IClone spatialRefClone = spatialRef as IClone;
                    if (!targetSpatialRefClone.IsEqual(spatialRefClone))
                    {
                        IGeoDatasetSchemaEdit pGeoDatasetSchemaEdit = dataset as IGeoDatasetSchemaEdit;
                        if (pGeoDatasetSchemaEdit.CanAlterSpatialReference == true)
                        {
                            pGeoDatasetSchemaEdit.AlterSpatialReference(targetSpatialRef);
                        }
                    }
                }
            }
            System.Runtime.InteropServices.Marshal.ReleaseComObject(enumDataset);
        }

        private static IFeatureClass CopyFCStruct2Database(GApplication app, IFeatureClass fc, IWorkspace targetWorkspace, string featureDatasetName, ISpatialReference targetSpatialRef)
        {
            string msg = "";
            FeatureClassToFeatureClass gpTool = new FeatureClassToFeatureClass();
            gpTool.in_features = (fc as IDataset).Workspace.PathName + "\\" + (fc as IDataset).Name;//fc.AliasName;
            gpTool.where_clause = "1<>1";
            gpTool.out_path = targetWorkspace.PathName;
            if (featureDatasetName != "")
            {
                gpTool.out_path += "\\" + featureDatasetName;
            }
            gpTool.out_name = (fc as IDataset).Name;//fc.AliasName;

            IFeatureClass result = null;
            try
            {
                app.GPTool.Execute(gpTool, null);

                result = (targetWorkspace as IFeatureWorkspace).OpenFeatureClass((fc as IDataset).Name);//fc.AliasName;

                if (targetSpatialRef != null)
                {
                    #region 重定义空间参考
                    ISpatialReference spatialRef = (result as IGeoDataset).SpatialReference;
                    var targetSpatialRefClone = targetSpatialRef as IClone;
                    if (spatialRef == null || !targetSpatialRefClone.IsEqual(spatialRef as IClone))
                    {
                        IGeoDatasetSchemaEdit pGeoDatasetSchemaEdit = result as IGeoDatasetSchemaEdit;
                        if (pGeoDatasetSchemaEdit.CanAlterSpatialReference == true)
                        {
                            pGeoDatasetSchemaEdit.AlterSpatialReference(targetSpatialRef);
                        }
                    }
                    #endregion
                }
            }
            catch (Exception err)
            {
                msg += err.Message;
                msg += CommonMethods.ReturnGPMessages(app.GPTool);
                MessageBox.Show(msg);
            }

            return result;
        }

        private static IFeatureClass CopyFC2Database(GApplication app, IFeatureClass fc, IWorkspace targetWorkspace, string featureDatasetName, ISpatialReference targetSpatialRef)
        {
            string msg = "";

            Copy gpTool = new Copy();
            gpTool.in_data = (fc as IDataset).Workspace.PathName + "\\" + fc.AliasName;
            string out_data = targetWorkspace.PathName + "\\";
            if (featureDatasetName != "")
            {
                out_data += featureDatasetName + "\\";
            }
            gpTool.out_data = out_data + fc.AliasName;

            IFeatureClass result = null;
            try
            {
                app.GPTool.Execute(gpTool, null);

                result = (targetWorkspace as IFeatureWorkspace).OpenFeatureClass(fc.AliasName);

                if (targetSpatialRef != null)
                {
                    #region 重定义空间参考
                    ISpatialReference spatialRef = (result as IGeoDataset).SpatialReference;
                    var targetSpatialRefClone = targetSpatialRef as IClone;
                    if (spatialRef == null || !targetSpatialRefClone.IsEqual(spatialRef as IClone))
                    {
                        IGeoDatasetSchemaEdit pGeoDatasetSchemaEdit = result as IGeoDatasetSchemaEdit;
                        if (pGeoDatasetSchemaEdit.CanAlterSpatialReference == true)
                        {
                            pGeoDatasetSchemaEdit.AlterSpatialReference(targetSpatialRef);
                        }
                    }
                    #endregion
                }
            }
            catch (Exception err)
            {
                msg += err.Message;
                msg += CommonMethods.ReturnGPMessages(app.GPTool);
                MessageBox.Show(msg);
            }

            return result;

        }

        public static void AddStringField(IFeatureClass fc, string newFieldName)
        {
            if (fc.FindField(newFieldName) != -1)
                return;

            IField newField = new FieldClass();
            IFieldEdit newFieldEdit = (IFieldEdit)newField;
            newFieldEdit.Name_2 = newFieldName;
            newFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;

            IClass pTable = fc as IClass;
            pTable.AddField(newField);
        }
    }

    public class MDBHelper
    {

        public MDBHelper()
        {

        }
        public static DataTable GetTable(string fileName, string TableName)
        {
            string connectStr = @"Provider=Microsoft.jet.OLEDB.4.0;Data Source=" + fileName;
            DataTable dt = new DataTable();
            using (OleDbConnection conn = new OleDbConnection(connectStr))
            {
                string sql = "Select * from " + TableName;
                OleDbDataAdapter adapter = new OleDbDataAdapter(sql, conn);
                adapter.SelectCommand.CommandType = CommandType.Text;
                adapter.Fill(dt);
            }
            return dt;
        }
    }

}
