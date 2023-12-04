using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Display;

using System.Data;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.DataSourcesFile;
using System.IO;
using ESRI.ArcGIS.esriSystem;
using System.Windows.Forms;
using System.Diagnostics;
using ESRI.ArcGIS.Geoprocessor;
using SMGI.Common;
using ESRI.ArcGIS.Controls;
namespace SMGI.Plugin.EmergencyMap
{
    /// <summary>
    /// 辅助类，公共方法
    /// </summary>
    public class DCDHelper
    {
        /// <summary>
        ///  从mdb提取表
        /// </summary>
        /// <param name="mdbFilePath">mdb路径</param>
        /// <param name="tableName">表名称</param>
        /// <returns></returns>
        public static DataTable ReadToDataTable(string mdbFilePath, string tableName)
        {
            DataTable pDataTable = new DataTable();
            if (System.IO.File.Exists(mdbFilePath))
            {
                IWorkspaceFactory pWorkspaceFactory = new AccessWorkspaceFactoryClass();
                IWorkspace pWorkspace = pWorkspaceFactory.OpenFromFile(mdbFilePath, 0);
                IEnumDataset pEnumDataset = pWorkspace.get_Datasets(esriDatasetType.esriDTAny);
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
                 Marshal.ReleaseComObject(pEnumDataset);
                if (pTable != null)
                {
                    ICursor pCursor = pTable.Search(null, false);
                    IRow pRow = null;
                    //添加表的字段信息
                    for (int i = 0; i < pTable.Fields.FieldCount; i++)
                    {
                        pDataTable.Columns.Add(pTable.Fields.Field[i].Name);
                    }
                    //添加数据
                    while ((pRow = pCursor.NextRow()) != null)
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
                    }
                     Marshal.ReleaseComObject(pCursor);
                }
                Marshal.ReleaseComObject(pWorkspace);
                Marshal.ReleaseComObject(pWorkspaceFactory);
            }
            return pDataTable;
        }

        /// <summary>
        /// 获取要素类
        /// </summary>
        /// <param name="pws"></param>
        /// <param name="fclName"></param>
        /// <returns></returns>
        public static IFeatureClass GetFclViaWs(IWorkspace pws, string fclName)
        {
            try
            {
                IFeatureClass fcl = null;
                fcl = (pws as IFeatureWorkspace).OpenFeatureClass(fclName);
                return fcl;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.Source);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                return null; 
            }
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

        /// <summary>
        /// 床架内存工作空间
        /// </summary>
        /// <returns></returns>
        public static IWorkspace CreateInMemoryWorkspace()
        {
            // Create an in-memory workspace factory.
            IWorkspaceFactory pWorkspaceFactory = new InMemoryWorkspaceFactoryClass();

            // Create a new in-memory workspace. This returns a name object.
            IWorkspaceName pWorkspaceName = pWorkspaceFactory.Create(null, "TempWorkspace", null, 0);
            IName pName = (IName)pWorkspaceName;

            // Open the workspace through the name object.
            IWorkspace pWorkspace = (IWorkspace)pName.Open();


            //Type factoryType = Type.GetTypeFromProgID("esriDataSourcesGDB.InMemoryWorkspaceFactory");
            //IWorkspaceFactory pWorkspaceFactory = (IWorkspaceFactory)Activator.CreateInstance(factoryType);

            //IWorkspaceName pWorkspaceName = pWorkspaceFactory.Create("", "TempWorkspace", null, 0);

            //IName pName = (IName)pWorkspaceName;
            //IWorkspace pWorkspace = (IWorkspace)pName.Open();

            return pWorkspace;
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
        /// 从工作空间中获取栅格数据集
        /// </summary>
        /// <returns></returns>
        public static IRasterDataset getRasterDatasetFromWorkspace(IWorkspace ws, string rasterDatasetName)
        {
            IRasterWorkspaceEx rasterWorkspace = ws as IRasterWorkspaceEx;
            if (null == rasterWorkspace)
                return null;

            if (!(ws as IWorkspace2).get_NameExists(esriDatasetType.esriDTRasterDataset, rasterDatasetName))
                return null;

            return rasterWorkspace.OpenRasterDataset(rasterDatasetName);
        }

        /// <summary>
        /// 以要素类org_fc为模板，创建一个空要素类
        /// </summary>
        /// <param name="fWS"></param>
        /// <param name="org_fc"></param>
        /// <param name="FeatureClassName"></param>
        /// <param name="bOverwriteOutput">是否直接覆盖</param>
        /// <returns></returns>
        public static IFeatureClass CreateFeatureClassStructToWorkspace(IFeatureWorkspace fWS, IFeatureClass org_fc, string FeatureClassName, bool bOverwriteOutput = true)
        {
            FeatureClassName = FeatureClassName.Split('.').Last();

            if ((fWS as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, FeatureClassName))
            {
                if (!bOverwriteOutput)
                {
                    if (MessageBox.Show(string.Format("要素类【{0}】已经存在，是否确定直接覆盖,或退出？", FeatureClassName), "警告", MessageBoxButtons.OKCancel) == DialogResult.OK)
                    {
                        bOverwriteOutput = true;
                    }
                }

                if (bOverwriteOutput)
                {
                    IFeatureClass fc = fWS.OpenFeatureClass(FeatureClassName);
                    (fc as IDataset).Delete();
                }
                else
                {
                    return null;
                }
            }

            IFields org_fields;
            org_fields = (org_fc.Fields as IClone).Clone() as IFields;
            for (int i = 0; i < org_fields.FieldCount; i++)
            {
                IField field = org_fields.get_Field(i);
                IFieldEdit fieldEdit = field as IFieldEdit;
                fieldEdit.Name_2 = field.Name.ToUpper();
            }


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

                if (target_fields.FindField(field.Name) != -1)
                {
                    continue;
                }

                IField field_new = (field as ESRI.ArcGIS.esriSystem.IClone).Clone() as IField;
                (target_fields as IFieldsEdit).AddField(field_new);
            }
            return fWS.CreateFeatureClass(FeatureClassName, target_fields, featureDescription.InstanceCLSID,
                featureDescription.ClassExtensionCLSID, esriFeatureType.esriFTSimple,
                (featureDescription as IFeatureClassDescription).ShapeFieldName, string.Empty);
        }

        /// <summary>
        /// 将要素类inFeatureClss中指定的要素复制到目标要素类中targetFeatureClss
        /// </summary>
        /// <param name="inFeatureClss"></param>
        /// <param name="qf"></param>
        /// <param name="targetFeatureClss"></param>
        /// <param name="bRemoveCollaValue"></param>
        public static void CopyFeaturesToFeatureClass(IFeatureClass inFeatureClss, IQueryFilter qf, IFeatureClass targetFeatureClss, bool bRemoveCollaValue = true)
        {
            if (inFeatureClss.ShapeType != targetFeatureClss.ShapeType)
            {
                return;
            }

            bool bProjection = false;
            ISpatialReference in_sr = (inFeatureClss as IGeoDataset).SpatialReference;
            ISpatialReference target_sr = (targetFeatureClss as IGeoDataset).SpatialReference;
            if (in_sr.Name != target_sr.Name)
            {
                bProjection = true;
            }

            IFeatureClassLoad pFCLoad = targetFeatureClss as IFeatureClassLoad;
            pFCLoad.LoadOnlyMode = true;

            IFeatureCursor pTargetFeatureCursor = targetFeatureClss.Insert(true);
            

            IFeatureCursor pInFeatureCursor = inFeatureClss.Search(qf, true);
            IFeature pInFeature = null;

            try
            {
                while ((pInFeature = pInFeatureCursor.NextFeature()) != null)
                {
                    IFeatureBuffer pFeatureBuffer = targetFeatureClss.CreateFeatureBuffer();

                    IGeometry shape = pInFeature.ShapeCopy;
                    if (bProjection)
                        shape.Project(target_sr);//投影变换

                    pFeatureBuffer.Shape = shape;
                    for (int i = 0; i < pFeatureBuffer.Fields.FieldCount; i++)
                    {
                        IField pfield = pFeatureBuffer.Fields.get_Field(i);
                        if (pfield.Type == esriFieldType.esriFieldTypeGeometry || pfield.Type == esriFieldType.esriFieldTypeOID)
                            continue;

                        if (pfield.Name.ToUpper() == "SHAPE_LENGTH" || pfield.Name.ToUpper() == "SHAPE_AREA")
                            continue;

                        if (bRemoveCollaValue)
                        {
                            if (pfield.Name.ToUpper() == cmdUpdateRecord.CollabGUID || pfield.Name.ToUpper() == cmdUpdateRecord.CollabVERSION || pfield.Name.ToUpper() == "SMGIUSER")
                                continue;
                        }

                        //复制属性值
                        int index = pInFeature.Fields.FindField(pfield.Name);
                        if (index != -1 && pfield.Editable)
                        {
                            pFeatureBuffer.set_Value(i, pInFeature.get_Value(index));
                        }

                    }
                    pTargetFeatureCursor.InsertFeature(pFeatureBuffer);

                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.Source);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                MessageBox.Show(ex.Message);
            }
            pTargetFeatureCursor.Flush();

            System.Runtime.InteropServices.Marshal.ReleaseComObject(pTargetFeatureCursor);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(pInFeatureCursor);

            pFCLoad.LoadOnlyMode = false;
        }
        /// 截取字符串到Dictionary中
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static Dictionary<string, string> mdbreaderF(string str)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            if (str.Contains('|'))
            {
                string[] temp11 = str.Split('|');
                foreach (var item in temp11)
                {

                    if (item.Contains('['))
                    {
                        string[] temp1 = item.Split('[');//LRDL[gb:410100,410100]
                        string key = temp1[0];//LRDL
                        string temp2 = temp1[1].Substring(0, temp1[1].Length - 1);//gb:410100,410100

                        result.Add(key, temp2);
                    }
                    else { result.Add(item, ""); }

                }
            }
            else
            {

                if (str.Contains('['))
                {
                    string[] temp1 = str.Split('[');//LRDL[,410100,410100]
                    string key = temp1[0];
                    string temp2 = temp1[1].Substring(0, temp1[1].Length - 1);//410100,410100                   
                    result.Add(key, temp2);
                }
                else { result.Add(str, ""); }
            }
            return result;
        }

        /// <summary>
        /// 根据几何几何，增加一个新要素类
        /// </summary>
        /// <param name="fWS"></param>
        /// <param name="fdt"></param>
        /// <param name="newFCName"></param>
        /// <param name="geoType"></param>
        /// <param name="spatialReference"></param>
        /// <param name="geoList"></param>
        /// <param name="bOverwriteOutput"></param>
        /// <returns></returns>
        public static IFeatureClass CreateFeatureClassStructToWorkspace(IFeatureWorkspace fWS, IFeatureDataset fdt, string newFCName, 
            esriGeometryType geoType, ISpatialReference spatialReference, List<IGeometry> geoList, bool bOverwriteOutput = true)
        {
            IFeatureClass result = null;

            if ((fWS as IWorkspace2).get_NameExists(esriDatasetType.esriDTFeatureClass, newFCName))
            {
                if (!bOverwriteOutput)
                {
                    if (MessageBox.Show(string.Format("要素类【{0}】已经存在，是否确定直接覆盖,或退出？", newFCName), "警告", MessageBoxButtons.OKCancel) == DialogResult.OK)
                    {
                        bOverwriteOutput = true;
                    }
                }

                if (bOverwriteOutput)
                {
                    IFeatureClass fc = fWS.OpenFeatureClass(newFCName);
                    (fc as IDataset).Delete();
                }
                else
                {
                    return null;
                }
            }

            #region 创建临时点要素类
            IFeatureClassDescription fcDescription = new FeatureClassDescriptionClass();
            IObjectClassDescription ocDescription = (IObjectClassDescription)fcDescription;
            IFields fields = ocDescription.RequiredFields;
            IFieldsEdit pFieldsEdit = (IFieldsEdit)fields;

            IFieldChecker fieldChecker = new FieldCheckerClass();
            IEnumFieldError enumFieldError = null;
            IFields validatedFields = null;
            fieldChecker.ValidateWorkspace = (IWorkspace)fWS;
            fieldChecker.Validate(fields, out enumFieldError, out validatedFields);

            int shapeFieldIndex = fields.FindField(fcDescription.ShapeFieldName);
            IField Shapefield = fields.get_Field(shapeFieldIndex);
            IGeometryDef geometryDef = Shapefield.GeometryDef;
            IGeometryDefEdit geometryDefEdit = (IGeometryDefEdit)geometryDef;
            geometryDefEdit.GeometryType_2 = geoType;
            geometryDefEdit.SpatialReference_2 = spatialReference;

            if (fdt == null)
            {
                result = fWS.CreateFeatureClass(newFCName, fields, ocDescription.InstanceCLSID,
                    ocDescription.ClassExtensionCLSID, esriFeatureType.esriFTSimple, fcDescription.ShapeFieldName, "");
            }
            else
            {
                result = fdt.CreateFeatureClass(newFCName, fields, ocDescription.InstanceCLSID, 
                    ocDescription.ClassExtensionCLSID, esriFeatureType.esriFTSimple, fcDescription.ShapeFieldName, "");
            }
            #endregion

            #region 复制几何，插入新要素
            IFeatureCursor feCursor = result.Insert(true);
            foreach (var geo in geoList)
            {
                IFeatureBuffer featureBuf = result.CreateFeatureBuffer();
                featureBuf.Shape = geo;

                feCursor.InsertFeature(featureBuf);
            }
            feCursor.Flush();

            System.Runtime.InteropServices.Marshal.ReleaseComObject(feCursor);
            #endregion

            return result;

        }

        /// <summary>
        /// 增加一个整数字段，并将字段值根据某一整数字段值进行初始化
        /// </summary>
        /// <param name="fCls"></param>
        /// <param name="newFieldName"></param>
        /// <param name="valFieldName"></param>
        public static void AddIntegerField(IFeatureClass fCls, string newFieldName, string valFieldName)
        {
            //新增字段
            IFields pFields = fCls.Fields;
            IFieldsEdit pFieldsEdit = pFields as IFieldsEdit;
            IField pField = new FieldClass();
            IFieldEdit pFieldEdit = pField as IFieldEdit;
            pFieldEdit.Name_2 = newFieldName;
            pFieldEdit.AliasName_2 = newFieldName;
            pFieldEdit.Length_2 = 1;
            pFieldEdit.Type_2 = esriFieldType.esriFieldTypeInteger;
            IClass pTable = fCls as IClass;
            pTable.AddField(pField);
            pFieldsEdit = null;
            pField = null;

            //赋值
            int index = fCls.FindField(newFieldName);
            int valIndex = fCls.FindField(valFieldName);
            if (index != -1 && valIndex != -1)
            {
                IFeatureCursor fCursor = fCls.Update(null, true);
                IFeature f = null;
                while ((f = fCursor.NextFeature()) != null)
                {
                    try
                    {
                        int val = int.Parse(f.get_Value(valIndex).ToString());

                        f.set_Value(index, val);
                        fCursor.UpdateFeature(f);
                    }
                    catch
                    {
                        continue;
                    }
                    
                }
                System.Runtime.InteropServices.Marshal.ReleaseComObject(fCursor);
            }
        }

        /// <summary>
        /// 通过命令行方式调用py文件(需安装python 32位)
        /// </summary>
        /// <param name="pyFilePath">py文件全路径</param>
        /// <param name="paramList">参数列表</param>
        /// <returns></returns>
        public static bool RunPython(string pyFilePath, params string[] paramList)
        {
            try
            {
                string arguments = "/c " + pyFilePath;
                foreach (var item in paramList)
                {
                    arguments += " " + item;
                }

                ProcessStartInfo si = new ProcessStartInfo("cmd", arguments);
                si.RedirectStandardOutput = true;
                si.RedirectStandardError = true;
                si.RedirectStandardInput = true;
                si.UseShellExecute = false;
                si.CreateNoWindow = true;


                string outStr = "";
                string errStr = "";
                using (System.Diagnostics.Process p = new System.Diagnostics.Process())
                {
                    p.StartInfo = si;
                    p.Start();

                    outStr = p.StandardOutput.ReadToEnd();
                    errStr = p.StandardError.ReadToEnd();

                    p.WaitForExit();
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();

                bool res = outStr.Length == 0 && errStr.Length == 0;
                if(!res)
                {
                    MessageBox.Show(errStr + "\n" + outStr);
                }

                return res;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 返回与直线成degree夹角的角度指
        /// </summary>
        /// <param name="pPolyline"></param>
        /// <param name="degree"></param>
        /// <returns></returns>
        public static double GetAngle(ILine pPolyline, double degree = 0)
        {
            Double radian = pPolyline.Angle;
            Double angle = radian * 180 / Math.PI;
            angle = angle + degree;
            while (angle < 0)
            {
                angle = angle + 360;
            }

            angle = angle % 360;

            return angle;
        }


        /// <summary>
        /// 相交分析
        /// </summary>
        /// <param name="fc1"></param>
        /// <param name="fc2"></param>
        /// <param name="outputType">INPUT、LINE、POINT</param>
        /// <returns></returns>
        public static IFeatureClass IntersectAnalysis(IFeatureClass fc1, IQueryFilter qf1, IFeatureClass fc2, IQueryFilter qf2, string outputType = "INPUT")
        {
            IFeatureClass result = null;

            if (fc1 == null || fc2 == null)
                return null;

            try
            {
                //临时数据库
                string fullPath = DCDHelper.GetAppDataPath() + "\\MyWorkspace.gdb";
                IWorkspace ws = DCDHelper.createTempWorkspace(fullPath);
                IFeatureWorkspace fws = ws as IFeatureWorkspace;

                Geoprocessor gp = new Geoprocessor();
                gp.OverwriteOutput = true;
                gp.SetEnvironmentValue("workspace", ws.PathName);


                ESRI.ArcGIS.DataManagementTools.MakeFeatureLayer makeFeatureLayer = new ESRI.ArcGIS.DataManagementTools.MakeFeatureLayer();
                ESRI.ArcGIS.DataManagementTools.SelectLayerByAttribute selectLayerByAttribute = new ESRI.ArcGIS.DataManagementTools.SelectLayerByAttribute();

                makeFeatureLayer.in_features = fc1;
                makeFeatureLayer.out_layer = fc1.AliasName + "_Layer";
                gp.Execute(makeFeatureLayer, null);
                selectLayerByAttribute.in_layer_or_view = fc1.AliasName + "_Layer";
                selectLayerByAttribute.where_clause = qf1.WhereClause;
                gp.Execute(selectLayerByAttribute, null);

                makeFeatureLayer.in_features = fc2;
                makeFeatureLayer.out_layer = fc2.AliasName + "_Layer";
                gp.Execute(makeFeatureLayer, null);
                selectLayerByAttribute.in_layer_or_view = fc2.AliasName + "_Layer";
                selectLayerByAttribute.where_clause = qf2.WhereClause;
                gp.Execute(selectLayerByAttribute, null);


                ESRI.ArcGIS.AnalysisTools.Intersect intersect = new ESRI.ArcGIS.AnalysisTools.Intersect();
                intersect.in_features = fc1.AliasName + "_Layer;" + fc2.AliasName + "_Layer";
                intersect.out_feature_class = fc1.AliasName + "_" + fc2.AliasName + "_intersect";
                intersect.join_attributes = "ONLY_FID";
                intersect.output_type = outputType;

                gp.Execute(intersect, null);

                result = fws.OpenFeatureClass(fc1.AliasName + "_" + fc2.AliasName + "_intersect");
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return result;

        }

        /// <summary>
        /// 读取MDB表
        /// </summary>
        /// <param name="mdbFilePath"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static DataTable ReadMDBTable(string mdbFilePath, string tableName)
        {
            DataTable dt = null;
            if (!System.IO.File.Exists(mdbFilePath))
                return dt;

            IWorkspaceFactory wsFactory = new AccessWorkspaceFactoryClass();
            IWorkspace ws = wsFactory.OpenFromFile(mdbFilePath, 0);
            IEnumDataset enumDataset = ws.get_Datasets(esriDatasetType.esriDTAny);
            enumDataset.Reset();
            IDataset dataset = null;
            ITable objTable = null;
            while ((dataset = enumDataset.Next()) != null)
            {
                if (dataset.Name == tableName)
                {
                    objTable = dataset as ITable;
                    break;
                }
            }
            Marshal.ReleaseComObject(enumDataset);
            if (objTable == null)
            {
                return dt;
            }

            //添加表的字段信息
            dt = new DataTable();
            for (int i = 0; i < objTable.Fields.FieldCount; i++)
            {
                dt.Columns.Add(objTable.Fields.Field[i].Name);
            }

            //添加数据
            ICursor rowCursor = objTable.Search(null, false);
            IRow row = null;
            while ((row = rowCursor.NextRow()) != null)
            {
                DataRow dr = dt.NewRow();
                for (int i = 0; i < row.Fields.FieldCount; i++)
                {
                    object obValue = row.get_Value(i);
                    if (obValue != null && !Convert.IsDBNull(obValue))
                    {
                        dr[i] = row.get_Value(i);
                    }
                    else
                    {
                        dr[i] = "";
                    }
                }
                dt.Rows.Add(dr);
            }
            Marshal.ReleaseComObject(rowCursor);

            Marshal.ReleaseComObject(ws);
            Marshal.ReleaseComObject(wsFactory);

            return dt;
        }

        #region 追踪相关函数
        /// <summary>
        /// 根据点选取需要追踪的要素和追踪线
        /// </summary>
        /// <param name="pt"></param>
        /// <returns></returns>
        public static IPolyline SelectTraceLineAndPolygonByPoint(IPoint pt, out IFeature fe)
        {
            IPolyline result = null;
            IGeometry bufferGeo = null;
            fe = null;
            var topoOp = (pt as ITopologicalOperator);
            if (GApplication.Application.Workspace.Map.SpatialReference is IGeographicCoordinateSystem)
            {
                bufferGeo = topoOp.Buffer(5 * 0.000009).Envelope;
            }
            else
            {
                double dis = GApplication.Application.ActiveView.ScreenDisplay.DisplayTransformation.FromPoints(3);
                bufferGeo = topoOp.Buffer(dis).Envelope;
            }


            ISpatialFilter sf = new SpatialFilter();
            sf.Geometry = bufferGeo;
            sf.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
            var lyrs = GApplication.Application.Workspace.LayerManager.GetLayer(new LayerManager.LayerChecker(l =>
            {
                return l is IGeoFeatureLayer && (l.Visible) && ((l as IGeoFeatureLayer).FeatureClass.ShapeType == esriGeometryType.esriGeometryPolyline || (l as IGeoFeatureLayer).FeatureClass.ShapeType == esriGeometryType.esriGeometryPolygon);

            })).ToArray();

            try
            {
                IEngineEditLayers editLayer = GApplication.Application.EngineEditor as IEngineEditLayers;
                foreach (var item in lyrs)
                {
                    if (!editLayer.IsEditable(item as IFeatureLayer))
                        continue;

                    IFeatureClass fc = (item as IGeoFeatureLayer).FeatureClass;
                    IFeatureCursor feCursor = fc.Search(sf, true);
                    //IFeature fe = null;
                    while ((fe = feCursor.NextFeature()) != null)
                    {
                        if (fe.Shape == null || fe.Shape.IsEmpty)
                            continue;

                        if (fe.Shape.GeometryType == esriGeometryType.esriGeometryPolygon)
                        {
                            result = (fe.ShapeCopy as ITopologicalOperator).Boundary as IPolyline;
                        }
                        else
                        {
                            result = fe.ShapeCopy as IPolyline;
                        }

                        break;
                    }
                    Marshal.ReleaseComObject(feCursor);

                    if (result != null)
                    {
                        return result;
                    }
                }
                
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.Source);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);
            }

            return null;
        }

        /// <summary>
        /// 根据点选取需要追踪的要素的追踪线
        /// </summary>
        /// <param name="pt"></param>
        /// <returns></returns>
        public static IPolyline SelectTraceLineByPoint(IPoint pt)
        {

            IFeature fe = null;
            return SelectTraceLineAndPolygonByPoint(pt, out fe);

        }
        /// <summary>
        /// 清除追踪线元素
        /// </summary>
        public static void DeleteTraceLineElement()
        {
            IGraphicsContainer gc = GApplication.Application.ActiveView.GraphicsContainer;
            gc.Reset();
            IElement ele = null;
            while ((ele = gc.Next()) != null)
            {
                IElementProperties ep1 = ele as IElementProperties;
                if (ep1.Name == "TraceSelLine")
                    GApplication.Application.ActiveView.GraphicsContainer.DeleteElement(ele);
            }
            GApplication.Application.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewBackground, null, GApplication.Application.ActiveView.Extent);
        }

        /// <summary>
        /// 将追踪线元素画出来
        /// </summary>
        /// <param name="tracePolyline"></param>
        public static void DrawTraceLineElement(IPolyline tracePolyline)
        {
            //清除原先的追踪线元素
            DeleteTraceLineElement();

            IElement element = null;
            ILineElement lineElement = null;
            ISimpleLineSymbol lineSymbol = null;
            IElementProperties ep = null;

            //绘制选中的线
            if (tracePolyline != null)
            {
                lineElement = new LineElementClass();
                lineSymbol = new SimpleLineSymbolClass();
                lineSymbol.Style = esriSimpleLineStyle.esriSLSSolid;
                lineSymbol.Color = new RgbColorClass { Red = 255 };
                lineSymbol.Width = 2;
                lineElement.Symbol = lineSymbol;

                //
                element = lineElement as IElement;
                element.Geometry = tracePolyline;
                ep = element as IElementProperties;
                ep.Name = "TraceSelLine";
                GApplication.Application.ActiveView.GraphicsContainer.AddElement(element, 0);
            }

            GApplication.Application.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewBackground, null, GApplication.Application.ActiveView.Extent);
        }

        public static void DrawPolygonElement(IPolygon polygon)
        {
            //清除原先的追踪线元素
            //DeleteTraceLineElement();

            IElement element = null;
            IPolygonElement polygonElement = null;
            IElementProperties ep = null;

            //绘制
            if (polygon != null)
            {
                polygonElement = new PolygonElementClass();
                element = polygonElement as IElement;
                element.Geometry = polygon;
                ep = element as IElementProperties;
                ep.Name = "TraceSelLine";
                GApplication.Application.ActiveView.GraphicsContainer.AddElement(element, 0);
            }

            GApplication.Application.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewBackground, null, GApplication.Application.ActiveView.Extent);
        }

        /// <summary>
        /// 获取节点组
        /// </summary>
        /// <param name="startPt"></param>
        /// <param name="endPt"></param>
        /// <param name="pLine"></param>
        /// <returns></returns>
        public static IPointCollection getColFromTwoPoint(IPolyline polyline, IPoint startPt, IPoint endPt)
        {
            IPointCollection ptColl = new PolylineClass();

            bool isSpilt;
            int startPart1Index, seg1Index;
            int startPart2Index, seg2Index;
            //起始点不在顶点上时，seg1Index为与起始点相邻的两个顶点中索引较大者
            polyline.SplitAtPoint(startPt, true, false, out isSpilt, out startPart1Index, out seg1Index);
            polyline.SplitAtPoint(endPt, true, false, out isSpilt, out startPart2Index, out seg2Index);
            List<int> oid_list_1 = new List<int>();
            List<int> oid_list_2 = new List<int>();
            if (startPart1Index != startPart2Index)//跨部件时，不添加中间节点
            {
                ptColl.AddPoint(startPt);
                ptColl.AddPoint(endPt);

                return ptColl;
            }

            IGeometryCollection gc = polyline as IGeometryCollection;
            if (polyline.IsClosed)//取较短线
            {
                int ptCount = (gc.Geometry[startPart1Index] as IPointCollection).PointCount;

                //起始点到结束点为索引顺序
                if (seg1Index < seg2Index)
                {
                    Console.WriteLine("起始点到结束点为索引顺序({0},{1})", seg1Index, seg2Index);
                    //线段1（线段结点索引顺序）
                    IPointCollection ptColl_1 = new PolylineClass();
                    for (int i = seg1Index; i <= seg2Index; i++)
                    {
                        ptColl_1.AddPoint((gc.Geometry[startPart1Index] as IPointCollection).get_Point(i));
                        oid_list_1.Add(i);
                    }

                    //线段2（线段结点索引逆序）
                    IPointCollection ptColl_2 = new PolylineClass();
                    for (int i = seg1Index; i >= 0; i--)
                    {
                        ptColl_2.AddPoint((gc.Geometry[startPart1Index] as IPointCollection).get_Point(i));
                        oid_list_2.Add(i);
                    }
                    for (int i = ptCount - 2; i >= seg2Index; i--)
                    {
                        ptColl_2.AddPoint((gc.Geometry[startPart1Index] as IPointCollection).get_Point(i));
                        oid_list_2.Add(i);
                    }

                    ptColl = (ptColl_1 as IPolyline).Length < (ptColl_2 as IPolyline).Length ? ptColl_1 : ptColl_2;
                    if ((ptColl_1 as IPolyline).Length < (ptColl_2 as IPolyline).Length)
                    {
                        Console.WriteLine("ptColl_1:{0}", string.Join(" ", oid_list_1));
                    }
                    else {
                        Console.WriteLine("ptColl_2:{0}", string.Join(" ", oid_list_2));
                    }
                }
                else
                //起始点到结束点为索引逆序
                {
                    Console.WriteLine("起始点到结束点为索引逆序({0},{1})", seg1Index, seg2Index);
                    //线段1（线段结点索引逆序）
                    IPointCollection ptColl_1 = new PolylineClass();
                    ptColl_1.AddPoint((gc.Geometry[startPart1Index] as IPointCollection).get_Point(seg1Index));
                    for (int i = seg1Index; i >= seg2Index; i--)
                    {
                        ptColl_1.AddPoint((gc.Geometry[startPart1Index] as IPointCollection).get_Point(i));
                        oid_list_1.Add(i);
                    }

                    //线段2（线段结点索引顺序）
                    IPointCollection ptColl_2 = new PolylineClass();
                    for (int i = seg1Index; i < ptCount - 1; i++)
                    {
                        ptColl_2.AddPoint((gc.Geometry[startPart1Index] as IPointCollection).get_Point(i));
                        oid_list_2.Add(i);
                    }
                    for (int i = 0; i <= seg2Index; i++)
                    {
                        ptColl_2.AddPoint((gc.Geometry[startPart1Index] as IPointCollection).get_Point(i));
                        oid_list_2.Add(i);
                    }

                    ptColl = (ptColl_1 as IPolyline).Length < (ptColl_2 as IPolyline).Length ? ptColl_1 : ptColl_2;
                    if ((ptColl_1 as IPolyline).Length < (ptColl_2 as IPolyline).Length)
                    {
                        Console.WriteLine("ptColl_1:{0}", string.Join(" ", oid_list_1));
                        for (int i = 0; i < ptColl_1.PointCount; i++) {
                            Console.WriteLine("返回节点坐标({0},{1})", ptColl_1.get_Point(i).X, ptColl_1.get_Point(i).Y);
                        }
                    }
                    else
                    {
                        Console.WriteLine("ptColl_2:{0}", string.Join(" ", oid_list_2));
                        for (int i = 0; i < ptColl_2.PointCount; i++)
                        {
                            Console.WriteLine("返回节点坐标({0},{1})", ptColl_2.get_Point(i).X, ptColl_2.get_Point(i).Y);
                        }
                    }
                }
            }
            else
            {
                if (seg1Index < seg2Index)
                {
                    for (int i = seg1Index; i <= seg2Index; i++)
                    {
                        ptColl.AddPoint((gc.Geometry[startPart1Index] as IPointCollection).get_Point(i));
                    }
                }
                else
                {
                    for (int i = seg1Index; i >= seg2Index; i--)
                    {
                        ptColl.AddPoint((gc.Geometry[startPart1Index] as IPointCollection).get_Point(i));
                    }
                }
            }

            return ptColl;
        }


        /// <summary>
        /// 获取最近节点
        /// </summary>
        /// <param name="startPt"></param>
        /// <param name="endPt"></param>
        /// <param name="pLine"></param>
        /// <returns></returns>
        public static IPoint getVertexFromPolyline(IPolyline polyline, IPoint mousePt)
        {
            IPoint vertex = new Point();
            IPointCollection ptColl = new PolylineClass();
            bool isSpilt;
            int startPartIndex, segIndex;
            polyline.SplitAtPoint(mousePt, true, false, out isSpilt, out startPartIndex, out segIndex);
            IGeometryCollection gc = polyline as IGeometryCollection;
            vertex = (gc.Geometry[startPartIndex] as IPointCollection).get_Point(segIndex);

            return vertex;
        }

        #endregion

        /// <summary>
        /// 添加字段
        /// </summary>
        /// <param name="fCls"></param>
        /// <param name="newFieldName"></param>
        /// <param name="aliasName"></param>
        /// <param name="len"></param>
        /// <param name="fieldType"></param>
        public static void AddField(IFeatureClass fCls, string newFieldName, string aliasName, int len, esriFieldType fieldType)
        {
            if (fCls.Fields.FindField(newFieldName) != -1)
            {//删除字段
                ITable dTable = fCls as ITable;
                IField dField = dTable.Fields.get_Field(fCls.Fields.FindField(newFieldName));
                dTable.DeleteField(dField);
            }
            //新增字段
            IFields pFields = fCls.Fields;
            IFieldsEdit pFieldsEdit = pFields as IFieldsEdit;
            IField pField = new FieldClass();
            IFieldEdit pFieldEdit = pField as IFieldEdit;
            pFieldEdit.Name_2 = newFieldName;
            pFieldEdit.AliasName_2 = aliasName;
            pFieldEdit.Length_2 = len;
            pFieldEdit.Type_2 = fieldType;
            IClass pTable = fCls as IClass;
            pTable.AddField(pField);
            pFieldsEdit = null;
            pField = null;
        }
        /// <summary>
        /// 添加字段-如果已存在则跳过
        /// </summary>
        /// <param name="fCls"></param>
        /// <param name="newFieldName"></param>
        /// <param name="aliasName"></param>
        /// <param name="len"></param>
        /// <param name="fieldType"></param>
        public static void AddFieldSkip(IFeatureClass fCls, string newFieldName, string aliasName, int len, esriFieldType fieldType)
        {
            if (fCls.Fields.FindField(newFieldName) != -1)
            {//
                return;
            }
            //新增字段
            IFields pFields = fCls.Fields;
            IFieldsEdit pFieldsEdit = pFields as IFieldsEdit;
            IField pField = new FieldClass();
            IFieldEdit pFieldEdit = pField as IFieldEdit;
            pFieldEdit.Name_2 = newFieldName;
            pFieldEdit.AliasName_2 = aliasName;
            pFieldEdit.Length_2 = len;
            pFieldEdit.Type_2 = fieldType;
            IClass pTable = fCls as IClass;
            pTable.AddField(pField);
            pFieldsEdit = null;
            pField = null;
        }

        public static Dictionary<string, IFeatureClass> GetAllFeatureClassFromWorkspace(IFeatureWorkspace ws)
        {
            Dictionary<string, IFeatureClass> result = new Dictionary<string, IFeatureClass>();

            if (null == ws)
                return result;

            IEnumDataset enumDataset = (ws as IWorkspace).get_Datasets(esriDatasetType.esriDTAny);
            enumDataset.Reset();
            IDataset dataset = null;
            while ((dataset = enumDataset.Next()) != null)
            {
                if (dataset is IFeatureDataset)//要素数据集
                {
                    IFeatureDataset feDataset = dataset as IFeatureDataset;
                    IEnumDataset subEnumDataset = feDataset.Subsets;
                    subEnumDataset.Reset();
                    IDataset subDataset = null;
                    while ((subDataset = subEnumDataset.Next()) != null)
                    {
                        if (subDataset is IFeatureClass)//要素类
                        {
                            IFeatureClass fc = subDataset as IFeatureClass;
                            if (fc != null)
                                result.Add(subDataset.Name, fc);
                        }
                    }
                    Marshal.ReleaseComObject(subEnumDataset);
                }
                else if (dataset is IFeatureClass)//要素类
                {
                    IFeatureClass fc = dataset as IFeatureClass;
                    if (fc != null)
                        result.Add(dataset.Name, fc);
                }
                else
                {

                }

            }
            Marshal.ReleaseComObject(enumDataset);


            return result;
        }
    }
}
