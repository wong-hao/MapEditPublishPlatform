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

    public class CoordinateTransformationCmd : SMGI.Common.SMGICommand
    {
        public override bool Enabled
        {
            get
            {
                return m_Application != null && m_Application.Workspace != null;
            }
        }

        MultiConicCoordinateTransformation multiConicObj = new MultiConicCoordinateTransformation();

        private double mapScale = 0.0;
        static string appAath = DCDHelper.GetAppDataPath();
        static string projectedGDB = "投影数据库.gdb";
        static string fullPath = appAath + "\\" + projectedGDB;

        private static Envelope mapEnvelope = new EnvelopeClass();

        public override void OnClick()
        {

            mapScale = m_Application.MapControl.Map.ReferenceScale;
            if (mapScale == 0)
            {
                MessageBox.Show("请先设置参考比例尺！");
                return;
            }

            /*
            double longitude = 0;
            double latitude = 0;
            double xCoordination = 0;
            double yCoordination = 0;
            multiConicProjection(ref xCoordination, ref yCoordination, longitude, latitude, midlL, mapScale);
            MessageBox.Show("longitude: " + longitude + " latitude: " + latitude +
                              " xCoordination: " + xCoordination + " yCoordination: " + yCoordination);
             */

            using (var wo = m_Application.SetBusy())
            {
                GeoDBDataTransfer(m_Application.Workspace.EsriWorkspace, wo);
            }

        }

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

        public void GeoDBDataTransfer(IWorkspace ws, WaitOperation wo)
        {
            #region 创建并初始化投影数据库

            IFeatureWorkspace fws = ws as IFeatureWorkspace;

            mapEnvelope.XMin = 0;
            mapEnvelope.XMax = 0;

            wo.SetText("正在创建新投影数据库:" + projectedGDB);

            // 创建GP工具对象
            Geoprocessor geoprocessor = new Geoprocessor();
            geoprocessor.OverwriteOutput = true;

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

            double midlL = 0;

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

            #endregion

            #region 在投影数据库中进行投影

            midlL = (mapEnvelope.XMax + mapEnvelope.XMin) / 2;

            ws = DCDHelper.createTempWorkspace(fullPath);
            fws = ws as IFeatureWorkspace;

            fcName2FC = DCDHelper.GetAllFeatureClassFromWorkspace(fws);
            // 获取数据库中要素类的数量
            fcTotalNum = fcName2FC.Count;
            fcNum = 0;
            foreach (var kv in fcName2FC)
            {
                fcNum++;
                IFeatureClass fc = kv.Value;
                String fcname = kv.Key;

                esriGeometryType geometryType = fc.ShapeType;

                FeatureClassTransfer(kv, fws, geometryType, fcNum, fcTotalNum, midlL, wo);
            }

            #endregion
        }

        public void FeatureClassTransfer(KeyValuePair<string, IFeatureClass> fcName2FC, IFeatureWorkspace fws, esriGeometryType geometryType, int fcNum, int fcTotalNum, double midlL, WaitOperation wo)
        {
            IFeatureClass fc = fcName2FC.Value;
            String fcname = fcName2FC.Key;

            // 获取要素类中要素的数量
            int featureCount = fc.FeatureCount(null); // 如果传入 null，则计算所有的要素数量
            IFeatureClassManage featureClassManage = (IFeatureClassManage)fc;

            IFeature feature;
            IGeometry pGeo;
            IPoint point;
            IPointCollection pointCollection;
            double pointCount;

            int featurecount = 0;

            double longitude;
            double latitude;
            double xCoordination = 0; //x（自南向北）-B
            double yCoordination = 0; //y（自西向东）-L

            IFeatureCursor featureCursor = null;
            try
            {
                featureCursor = fc.Search(null, false);

                while ((feature = (IFeature)featureCursor.NextFeature()) != null)
                {
                    pGeo = feature.Shape;

                    // 不考虑几何为空的要素
                    if (pGeo == null || pGeo.IsEmpty)
                    {
                        continue;
                    }

                    featurecount++;

                    Console.WriteLine("正在投影第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname + "的第" + featurecount + "/" + featureCount + "个要素");

                    // 根据几何类型输出相应信息
                    switch (geometryType)
                    {
                        case esriGeometryType.esriGeometryPoint:

                            // 获取点要素的几何对象
                            point = pGeo as IPoint;

                            // 设置点要素的坐标
                            longitude = point.X;
                            latitude = point.Y;

                            multiConicObj.multiConicProjection(ref xCoordination, ref yCoordination, longitude, latitude, midlL, mapScale);

                            wo.SetText("正在投影第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname + "的第" + featurecount + "/" + featureCount + "个要素" + "(100%)");

                            Console.WriteLine("longitude: " + longitude + " latitude: " + latitude +
                                              " xCoordination: " + xCoordination + " yCoordination: " + yCoordination);

                            if (double.IsNaN(xCoordination) || double.IsNaN(yCoordination))
                            {
                                MessageBox.Show("对于要素类" + fcname + "，OID为" + feature.OID + "的要素坐标转换得到空值，请检查是否源数据是否均为地理坐标系或中央经线及比例尺设置是否正确！", "错误", MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                                return;
                            }

                            point.PutCoords(yCoordination, xCoordination);

                            //pGeo.Project(ISR);

                            // 更新要素
                            feature.Shape = point;
                            feature.Store();
                            break;
                        case esriGeometryType.esriGeometryPolyline:
                            // 获取要素的所有点
                            pointCollection = pGeo as IPointCollection;
                            pointCount = pointCollection.PointCount;

                            // 对于每个点，获取其当前的横纵坐标，然后进行移动
                            for (int i = 0; i < pointCount; i++)
                            {
                                point = pointCollection.get_Point(i);

                                // 设置点要素的坐标
                                longitude = point.X;
                                latitude = point.Y;

                                multiConicObj.multiConicProjection(ref xCoordination, ref yCoordination, longitude, latitude, midlL, mapScale);

                                wo.SetText("正在投影第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname + "的第" + featurecount + "/" + featureCount + "个要素" + "(" + ((i + 1) / pointCount).ToString("P") + ")");

                                Console.WriteLine("longitude: " + longitude + " latitude: " + latitude +
                                                  " xCoordination: " + xCoordination + " yCoordination: " + yCoordination);

                                if (double.IsNaN(xCoordination) || double.IsNaN(yCoordination))
                                {
                                    MessageBox.Show("对于要素类" + fcname + "，OID为" + feature.OID + "的要素坐标转换得到空值，请检查是否源数据是否均为地理坐标系或中央经线及比例尺设置是否正确！", "错误", MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                                    return;
                                }

                                point.PutCoords(yCoordination, xCoordination);

                                // 将移动后的点重新设置到要素中
                                pointCollection.UpdatePoint(i, point);
                            }

                            // 更新要素
                            feature.Shape = pointCollection as IGeometry;
                            feature.Store();
                            break;
                        case esriGeometryType.esriGeometryPolygon:
                            IPolygon polygon = (IPolygon)pGeo;

                            pointCount = 0;

                            IRing ring = new RingClass();

                            IGeometryCollection ringCollection = (IGeometryCollection)polygon;
                            for (int i = 0; i < ringCollection.GeometryCount; i++)
                            {
                                ring = (IRing)ringCollection.get_Geometry(i);
                                pointCollection = (IPointCollection)ring;
    
                                // 获取每个环（Ring）中的点数量并累加
                                pointCount += pointCollection.PointCount;
                            }

                            for (int i = 0; i < ringCollection.GeometryCount; i++)
                            {
                                ring = (IRing)ringCollection.get_Geometry(i);
                                pointCollection = (IPointCollection)ring;

                                IPoint[] originalPoints = new IPoint[pointCollection.PointCount];

                                // 记录原始点的位置，保证面不发生形变
                                for (int j = 0; j < pointCollection.PointCount; j++)
                                {
                                    originalPoints[j] = pointCollection.get_Point(j);
                                }

                                // 移动所有点
                                for (int j = 0; j < pointCollection.PointCount; j++)
                                {
                                    point = pointCollection.get_Point(j);

                                    // 设置点要素的坐标
                                    longitude = originalPoints[j].X;
                                    latitude = originalPoints[j].Y;

                                    multiConicObj.multiConicProjection(ref xCoordination, ref yCoordination, longitude, latitude, midlL, mapScale);

                                    wo.SetText("正在投影第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname + "的第" + featurecount + "/" + featureCount + "个要素" + "(" + ((i + 1) / pointCount).ToString("P") + ")");

                                    Console.WriteLine("longitude: " + longitude + " latitude: " + latitude +
                                                      " xCoordination: " + xCoordination + " yCoordination: " + yCoordination);

                                    if (double.IsNaN(xCoordination) || double.IsNaN(yCoordination))
                                    {
                                        MessageBox.Show("对于要素类" + fcname + "，OID为" + feature.OID + "的要素坐标转换得到空值，请检查是否源数据是否均为地理坐标系或中央经线及比例尺设置是否正确！", "错误", MessageBoxButtons.OK,
                                            MessageBoxIcon.Error);
                                        return;
                                    }

                                    point.PutCoords(yCoordination, xCoordination);

                                    // 将移动后的点重新设置到环中
                                    pointCollection.UpdatePoint(j, point);
                                }
                            }

                            // 更新要素的几何对象
                            feature.Shape = (IGeometry)polygon;
                            feature.Store();
                            break;
                        default:
                            MessageBox.Show("对于要素类" + fcname + "，OID为" + feature.OID + "的要素，其几何类型不受支持！", "错误", MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.Source);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);
                throw;
            }
            finally
            {
                // 在 finally 块中释放 COM 对象
                if (featureCursor != null)
                {
                    Marshal.ReleaseComObject(featureCursor);
                }
            }
        }
    }
}