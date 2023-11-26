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

        GDBOperation gdbOperation = new GDBOperation();
        MultiConicCoordinateTransformation multiConicObj = new MultiConicCoordinateTransformation();

        private double mapScale = 0.0;

        private static Envelope mapEnvelope = new EnvelopeClass();

        public override void OnClick()
        {

            mapScale = m_Application.MapControl.Map.ReferenceScale;
            if (mapScale == 0)
            {
                MessageBox.Show("请先设置参考比例尺！");
                return;
            }

            using (var wo = m_Application.SetBusy())
            {
                GDBProject(m_Application.Workspace.EsriWorkspace, wo);
            }

        }

        public void GDBProject(IWorkspace ws, WaitOperation wo)
        {
            #region 创建并初始化投影数据库

            // 创建GP工具对象
            Geoprocessor geoprocessor = new Geoprocessor();
            geoprocessor.OverwriteOutput = true;

            gdbOperation.GDBInit(ref mapEnvelope, geoprocessor, ws, wo);

            #endregion

            #region 在投影数据库中进行投影

            double realMidlL = (mapEnvelope.XMax + mapEnvelope.XMin) / 2;

            double midlL = 150;

            bool bound = true; // 是否需要根据实际中央经线与目标中央经线的差值进行原始数据裁剪,以完善公式限制

            ws = DCDHelper.createTempWorkspace(gdbOperation.fullPath);

            gdbOperation.fws = ws as IFeatureWorkspace;

            Dictionary<string, IFeatureClass> fcName2FC = DCDHelper.GetAllFeatureClassFromWorkspace(gdbOperation.fws);

            // 获取数据库中要素类的数量
            int fcTotalNum = fcName2FC.Count;
            int fcNum = 0;

            foreach (var kv in fcName2FC)
            {
                fcNum++;
                IFeatureClass fc = kv.Value;
                String fcname = kv.Key;

                esriGeometryType geometryType = fc.ShapeType;

                if (Math.Abs(midlL - realMidlL) <= 2)
                {
                    bound = false;
                    FeatureClassProject(kv, gdbOperation.fws, geometryType, fcNum, fcTotalNum, realMidlL, bound, wo);
                }
                else
                {
                    FeatureClassProject(
                        gdbOperation.GDBMultipartToSinglepart(geoprocessor, ws, fcname, fc, fcTotalNum, fcNum, wo),
                        gdbOperation.fws, geometryType, fcNum, fcTotalNum, midlL, bound, wo);
                }
            }

            #endregion
        }

        public void SplitFeatures(IFeatureClass fc, string fcname, WaitOperation wo)
        {
            wo.SetText("正在切割" + "要素类" + fcname + "中经度范围为" + "-180" + "到" + "-30" + "的要素");

            // 创建一个 Polyline
            IPoint startPoint = new PointClass();
            IPoint endPoint = new PointClass();
            startPoint.PutCoords(-30, -1000); // 你的竖线起点坐标
            endPoint.PutCoords(-30, 1000); // 你的竖线终点坐标

            // 构建竖线
            IPolyline verticalLine = new PolylineClass();
            verticalLine.FromPoint = startPoint;
            verticalLine.ToPoint = endPoint;

            // 调用分割要素的方法
            if (fc.ShapeType == esriGeometryType.esriGeometryPolygon)
            {
                splitFeaturePolygon(verticalLine, fc);
            }
            else if (fc.ShapeType == esriGeometryType.esriGeometryPolyline)
            {
                splitFeaturePolyline(verticalLine, fc);
            }
            else
            {
                Console.WriteLine("要素类" + fcname + "为不受支持的几何类型，不进行切割。", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 极有可能报异常，但仍可以得到最后的数据
        private void splitFeaturePolygon(IPolyline splitLine, IFeatureClass fc)
        {
            try
            {
                IGeometry splitGeo = splitLine as IGeometry;

                if (splitGeo == null)
                {
                    MessageBox.Show("切割要素为空！");
                    return;
                }
                else
                {
                    ITopologicalOperator splitGeoTopo = splitGeo as ITopologicalOperator;
                    splitGeoTopo.Simplify();
                }

                IFeatureCursor featureCursor = null;
                featureCursor = fc.Search(null, false);

                IRelationalOperator relOp = splitGeo as IRelationalOperator;

                IFeature fe = null;
                while ((fe = (IFeature)featureCursor.NextFeature()) != null)
                {
                    if (relOp.Disjoint(fe.Shape))
                    {
                        continue; // 如果不相交，则跳过这个要素
                    }

                    IFeatureEdit feEdit = (IFeatureEdit)fe;
                    var feSet = feEdit.Split(splitGeo);
                    if (feSet != null)
                    {
                        feSet.Reset();
                    }
                }
                Marshal.ReleaseComObject(featureCursor);
            }
            catch (Exception ex)
            {
                // 错误处理
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.Source);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void splitFeaturePolyline(IPolyline splitLine, IFeatureClass fc)
        {
            try
            {
                List<int> originalPolylineOID = new List<int>();

                // 遍历要素并分割
                IFeatureCursor featureCursor = fc.Search(null, true);
                double featureTotalCount = fc.FeatureCount(null);
                double featureCount = 0;
                IFeature feature = featureCursor.NextFeature();
                while (feature != null)
                {
                    featureCount++;

                    IGeometry geometry = feature.Shape;

                    IProximityOperator proximityOperator = splitLine as IProximityOperator;
                    if (proximityOperator != null && proximityOperator.ReturnDistance(geometry) == 0)
                    {
                        ITopologicalOperator topoOperator = geometry as ITopologicalOperator;
                        if (topoOperator != null)
                        {
                            IGeometry leGeometry = null;
                            IGeometry riGeometry = null;

                            topoOperator.Cut(splitLine, out leGeometry, out riGeometry);

                            if (leGeometry != null && riGeometry != null)
                            {
                                originalPolylineOID.Add(feature.OID);

                                IFeature newFeaturele = fc.CreateFeature();
                                newFeaturele.Shape = leGeometry;
                                newFeaturele.Store();

                                IFeature newFeatureri = fc.CreateFeature();
                                newFeatureri.Shape = riGeometry;
                                newFeatureri.Store();
                            }
                        }
                    }

                    feature = featureCursor.NextFeature();

                    // 避免分割生成的要素继续被分割
                    if (featureCount >= featureTotalCount)
                    {
                        break;
                    }
                }
                System.Runtime.InteropServices.Marshal.ReleaseComObject(featureCursor);

                // 循环删除指定 OID 的要素
                foreach (int oid in originalPolylineOID)
                {
                    // 根据 OID 获取要素
                    feature = fc.GetFeature(oid);

                    if (feature != null)
                    {
                        // 删除要素
                        feature.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                // 错误处理
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.Source);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void MoveFeatures(IFeatureClass fc, string fcname, WaitOperation wo)
        {
            try
            {
                wo.SetText("正在移动" + "要素类" + fcname + "中经度范围为" + "-180" + "到" + "-30" + "的要素至经度范围180" + "到" + "330");

                // 构建查询以选择满足条件的要素
                ISpatialFilter spatialFilter = new SpatialFilter();
                spatialFilter.GeometryField = fc.ShapeFieldName;
                spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;

                // 创建查询范围的 Envelope
                IEnvelope queryEnvelope = new EnvelopeClass();
                queryEnvelope.PutCoords(-180, -1000, -30, 1000);
                spatialFilter.Geometry = queryEnvelope;

                // 查询要素
                IFeatureCursor featureCursor = fc.Search(spatialFilter, true);
                IFeature feature = featureCursor.NextFeature();

                // 对于每个选定的要素，修改其几何
                while (feature != null)
                {
                    IGeometry geometry = feature.ShapeCopy;
                    if (geometry != null)
                    {
                        ITransform2D transform = geometry as ITransform2D;
                        if (transform != null)
                        {
                            transform.Move(360, 0); // 将几何对象向右平移 360
                            feature.Shape = geometry;
                            feature.Store();
                        }
                    }
                    feature = featureCursor.NextFeature();
                }

                // 释放资源
                System.Runtime.InteropServices.Marshal.ReleaseComObject(featureCursor);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public void FeatureClassProject(KeyValuePair<string, IFeatureClass> fcName2FC, IFeatureWorkspace fws, esriGeometryType geometryType, int fcNum, int fcTotalNum, double midlL, bool bound, WaitOperation wo)
        {
            IFeatureClass fc = fcName2FC.Value;
            String fcname = fcName2FC.Key;

            if (bound)
            {
                SplitFeatures(fc, fcname, wo);
                MoveFeatures(fc, fcname, wo);

                var keyValuePair = gdbOperation.GDBToUnknown(fws, fcname, fc, wo);
                fcname = keyValuePair.Key;
                fc = keyValuePair.Value;

                /*
                keyValuePair = gdbOperation.GDBToDissolved(fws, fcname, fc, wo);
                fcname = keyValuePair.Key;
                fc = keyValuePair.Value;

                keyValuePair = gdbOperation.GDBToUnknown(fws, fcname, fc, wo);
                fcname = keyValuePair.Key;
                fc = keyValuePair.Value;
                 */

                keyValuePair = gdbOperation.GDBRemoveSuffix(fws, fcname, fc, wo);
                fcname = keyValuePair.Key;
                fc = keyValuePair.Value;
            }

            // 获取要素类中要素的数量
            int featureCount = fc.FeatureCount(null); // 如果传入 null，则计算所有的要素数量

            IFeature feature;
            IGeometry pGeo;
            IPoint point;
            IPointCollection pointCollection;
            double pointToTalCount; // 要素类的总点个数
            double pointCount; // 目前已经遍历到的点个数

            double featurecount = 0;

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

                    wo.SetText("正在投影第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname + "(" + (featurecount / featureCount).ToString("P") + ")");
                    Console.WriteLine("正在投影第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname + "的第" + featurecount + "/" + featureCount + "个要素" + "(" + (featurecount / featureCount).ToString("P") + ")");

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

                            Console.WriteLine("正在投影第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname + "的第" + featurecount + "/" + featureCount + "个要素" + "(100%)");

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
                            pointToTalCount = pointCollection.PointCount;
                            pointCount = 0;

                            // 对于每个点，获取其当前的横纵坐标，然后进行移动
                            for (int i = 0; i < pointToTalCount; i++)
                            {
                                point = pointCollection.get_Point(i);

                                // 设置点要素的坐标
                                longitude = point.X;
                                latitude = point.Y;

                                multiConicObj.multiConicProjection(ref xCoordination, ref yCoordination, longitude, latitude, midlL, mapScale);

                                pointCount++;

                                Console.WriteLine("正在投影第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname + "的第" + featurecount + "/" + featureCount + "个要素" + "(" + (pointCount / pointToTalCount).ToString("P") + ")");

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

                            pointToTalCount = 0;
                            pointCount = 0;

                            IRing ring = new RingClass();

                            IGeometryCollection ringCollection = (IGeometryCollection)polygon;
                            for (int i = 0; i < ringCollection.GeometryCount; i++)
                            {
                                ring = (IRing)ringCollection.get_Geometry(i);
                                pointCollection = (IPointCollection)ring;
    
                                // 获取每个环（Ring）中的点数量并累加
                                pointToTalCount += pointCollection.PointCount;
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
                                    
                                    pointCount++;

                                    Console.WriteLine("正在投影第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname + "的第" + featurecount + "/" + featureCount + "个要素" + "(" + (pointCount / pointToTalCount).ToString("P") + ")");

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
                MessageBox.Show(ex.Message);
                MessageBox.Show(ex.Source);
                MessageBox.Show(ex.StackTrace);
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