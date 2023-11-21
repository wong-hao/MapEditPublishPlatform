using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using ESRI.ArcGIS.Carto;
using SMGI.Common;
using System.Windows.Forms;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using System.Runtime.InteropServices;
using System.Data;
using ESRI.ArcGIS.AnalysisTools;
using ESRI.ArcGIS.ConversionTools;
using ESRI.ArcGIS.DataManagementTools;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.GeoAnalyst;
using ESRI.ArcGIS.Geoprocessor;
using SMGI.Plugin.DCDProcess;

namespace SMGI.Plugin.CollaborativeWorkWithAccount
{

    public class GDBOperation
    {
        static string appAath = DCDHelper.GetAppDataPath();
        static string projectedGDB = "投影数据库.gdb";
        public string fullPath = appAath + "\\" + projectedGDB;
        public IFeatureWorkspace fws = null;

        public string suffixToRemove = "_MultipartToSinglep";
        
        // 将 ArcObjects 的几何类型转换为字符串表示形式
        static string GetGeometryType(esriGeometryType shapeType)
        {
            switch (shapeType)
            {
                case esriGeometryType.esriGeometryPoint:
                    return "POINT";
                case esriGeometryType.esriGeometryPolyline:
                    return "POLYLINE";
                case esriGeometryType.esriGeometryPolygon:
                    return "POLYGON";
                case esriGeometryType.esriGeometryMultipoint:
                    return "MULTIPOINT";
                default:
                    return "";
            }
        }

        public void GDBInit(ref Envelope mapEnvelope, Geoprocessor geoprocessor, IWorkspace ws, WaitOperation wo)
        {
            fws = ws as IFeatureWorkspace;

            mapEnvelope.XMin = 0;
            mapEnvelope.XMax = 0;

            wo.SetText("正在创建新投影数据库:" + projectedGDB);

            // 使用createFileGdb工具
            CreateFileGDB createFileGdb = new CreateFileGDB();
            createFileGdb.out_name = projectedGDB;
            createFileGdb.out_folder_path = appAath;
            Helper.ExecuteGPTool(geoprocessor, createFileGdb, null);

            // 使用CreateFeatureclass工具
            CreateFeatureclass createFeatureclass = new CreateFeatureclass();

            // 设置为未知坐标系统
            ISpatialReference unknownSpatialReference = new UnknownCoordinateSystem() as ISpatialReference;

            // 使用Append工具
            Append append = new Append();

            Dictionary<string, IFeatureClass> fcName2FC = DCDHelper.GetAllFeatureClassFromWorkspace(fws);

            // 获取数据库中要素类的数量
            int fcTotalNum = fcName2FC.Count;
            int fcNum = 0;

            foreach (var kv in fcName2FC)
            {
                fcNum++;

                IFeatureClass fc = kv.Value;
                String fcname = kv.Key;

                wo.SetText("正在创建投影数据库的第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname);

                // 获取要素类的范围
                IEnvelope fcEnvelope = ((IGeoDataset)fc).Extent;

                if (fcEnvelope.XMin < mapEnvelope.XMin)
                {
                    mapEnvelope.XMin = fcEnvelope.XMin;
                }

                if (fcEnvelope.XMax > mapEnvelope.XMax)
                {
                    mapEnvelope.XMax = fcEnvelope.XMax;
                }

                esriGeometryType geometryType = fc.ShapeType;

                createFeatureclass.spatial_reference = unknownSpatialReference;
                createFeatureclass.out_name = fcname;
                createFeatureclass.out_path = fullPath;
                createFeatureclass.geometry_type = GetGeometryType(geometryType);

                string geoType = GetGeometryType(geometryType);

                if (string.IsNullOrEmpty(geoType))
                {
                    MessageBox.Show("要素类" + fcname + "的几何类型不受支持，无法创建", "Error", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                Helper.ExecuteGPTool(geoprocessor, createFeatureclass, null);

                wo.SetText("正在拷贝投影数据库的第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname);

                append.inputs = fcname;
                append.schema_type = "NO_TEST";
                append.target = fullPath + "\\" + fcname;
                Helper.ExecuteGPTool(geoprocessor, append, null);
            }
        }

        public KeyValuePair<string, IFeatureClass> GDBMultipartToSinglepart(Geoprocessor geoprocessor, IWorkspace ws, String fcname, IFeatureClass fc, int fcTotalNum, int fcNum, WaitOperation wo)
        {
            fws = ws as IFeatureWorkspace;

            wo.SetText("正在多部件处理投影数据库的第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname);

            MultipartToSinglepart multipartToSinglepart = new MultipartToSinglepart();
            multipartToSinglepart.in_features = fcname;
            String fcname_MultipartToSinglep = fcname + "_MultipartToSinglep";
            multipartToSinglepart.out_feature_class = fullPath + "\\" + fcname_MultipartToSinglep;

            Helper.ExecuteGPTool(geoprocessor, multipartToSinglepart, null);

            IFeatureClass fc_MultipartToSinglep = fws.OpenFeatureClass(fcname_MultipartToSinglep);
            var kv_MultipartToSinglep =
                new KeyValuePair<string, IFeatureClass>(fcname_MultipartToSinglep, fc_MultipartToSinglep);

            ((IDataset)fc).Delete(); // 删除原始要素类

            return kv_MultipartToSinglep;
        }

        public string RemoveSuffix(string input, string suffix)
        {
            if (input.EndsWith(suffix))
            {
                return input.Substring(0, input.Length - suffix.Length);
            }

            return input;
        }

        public void PerformUnion(IFeatureClass fc, string fcname, WaitOperation wo)
        {
            wo.SetText("正在结合" + "要素类" + fcname + "的要素");

            if (fc.ShapeType == esriGeometryType.esriGeometryPolygon)
            {
                // 创建GP工具对象
                Geoprocessor geoprocessor = new Geoprocessor();
                geoprocessor.OverwriteOutput = true;

                // 创建一个 Union 工具实例
                Union unionTool = new Union();

                // 设置输入要素类
                unionTool.in_features = fullPath + "\\" + fcname;

                fcname = RemoveSuffix(fcname, suffixToRemove);

                // 设置输出要素类
                unionTool.out_feature_class = fullPath + "\\" + fcname; // 替换为输出要素类的路径

                // 执行 Union 工具
                Helper.ExecuteGPTool(geoprocessor, unionTool, null);
            }
            else if (fc.ShapeType == esriGeometryType.esriGeometryPolyline)
            {
                // 创建GP工具对象
                Geoprocessor geoprocessor = new Geoprocessor();
                geoprocessor.OverwriteOutput = true;

                // 创建一个 FeatureToPolygon 工具实例
                FeatureToPolygon featureToPolygonTool = new FeatureToPolygon();

                // 设置输入线要素类
                featureToPolygonTool.in_features = fullPath + "\\" + fcname;

                // 设置输出面要素类
                featureToPolygonTool.out_feature_class = fullPath + "\\" + fcname + "_Polygon"; // 临时存储转换后的面要素

                // 执行 FeatureToPolygon 工具
                Helper.ExecuteGPTool(geoprocessor, featureToPolygonTool, null);

                // 创建一个 Union 工具实例
                Union unionTool = new Union();

                // 设置输入要素类为转换后的面要素类
                unionTool.in_features = fullPath + "\\" + fcname + "_Polygon";

                fcname = RemoveSuffix(fcname, suffixToRemove + "_Polygon");

                // 设置输出要素类
                unionTool.out_feature_class = fullPath + "\\" + fcname; // 存储 Union 结果的路径

                // 执行 Union 工具
                Helper.ExecuteGPTool(geoprocessor, unionTool, null);
            }

            //((IDataset)fc).Delete(); // 删除多部件要素类
        }

        public void PerformDissolve(IFeatureClass fc, string fcname, WaitOperation wo)
        {
            wo.SetText("正在结合" + "要素类" + "的要素");

            // 创建一个 Dissolve 工具实例
            Dissolve dissolveTool = new Dissolve();

            // 设置输入要素类
            dissolveTool.in_features = fullPath + "\\" + fcname;

            string suffixToRemove = "_MultipartToSinglep";
            fcname = RemoveSuffix(fcname, suffixToRemove);

            // 设置输出要素类
            dissolveTool.out_feature_class = fullPath + "\\" + fcname; // 替换为输出要素类的路径

            // 设置要素融合的字段
            dissolveTool.dissolve_field = "ORIG_FID"; // 替换为用于融合的字段名

            // 创建一个 Geoprocessor 实例并执行 Dissolve 工具
            Geoprocessor geoprocessor = new Geoprocessor();
            geoprocessor.Execute(dissolveTool, null);

            ((IDataset)fc).Delete(); // 删除多部件要素类
        }
    }
}