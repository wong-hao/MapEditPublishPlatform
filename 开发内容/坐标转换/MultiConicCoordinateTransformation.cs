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

    public class MultiConicCoordinateTransformation
    {
        private static double R = 6371116;
        private static double mu0 = 1 / 26000000;
        private static double x0;
        private static double xn;
        private static double yn;
        private static double q;
        private static double sinan;
        private static double an;
        private static double a;
        private static double l;
        private static double ln = 201.6 * Math.PI / 180;
        private static double huL;
        private static double huB;

        public static double getx0()
        {
            return (huB + 0.06683225 * Math.Pow(huB, 4)) * R / 140000;
        }

        static double getxn()
        {
            return x0 + 0.20984 * huB * R / 140000;
        }

        static double getyn()
        {
            return Math.Sqrt(Math.Pow(112, 2) - Math.Pow(xn, 2)) + 20;
        }

        static double getq()
        {
            return (Math.Pow(yn, 2) + Math.Pow(xn - x0, 2)) / (2 * (xn - x0));
        }

        static double getsinan()
        {
            return yn / q;
        }

        static double getan()
        {
            return Math.Asin(sinan);
        }

        // 由于公式限制，若不对原始地理坐标系数据进行额外处理，则需要保证L - midlL的范围为[-180, 180]
        static double getl(double L, double midlL)
        {
            return (L - midlL) * Math.PI / 180;
        }

        static double geta()
        {
            return an * l / ln;
        }

        // L：经度（坐标）；B：纬度（坐标）；mapScale：比例尺（真实值）；midlL：中央经线（坐标）
        public static void multiConicProjection(ref double x, ref double y, double L, double B, double midlL, double mapScale)
        {
            mapScale = mapScale / 10000; //比例尺（以万为单位）
            huL = L * Math.PI / 180; //经度（弧度）
            huB = B * Math.PI / 180; //纬度（弧度）

            if (huB < 0)
            {
                huB = -1 * huB;

                x0 = getx0();
                xn = getxn();
                yn = getyn();

                q = getq();
                sinan = getsinan();
                an = getan();
                l = getl(L, midlL);
                a = geta();

                //MessageBox.Show("L: " + L + " B: " + B + " huL: " + huL + " huB: " + huB + " x0: " + x0 + " xn: " + xn + " yn: " + yn, "中间结果1");
                //MessageBox.Show(" q: " + q + " sinan: " + sinan + " an: " + an + " l: " + l + " a: " + a, "中间结果2");
                //MessageBox.Show((q * Math.Sin(a) * 14000).ToString(), "中间结果3");

                x = (x0 + q * (1 - Math.Cos(a))) * (-0.888428) * 14000 / mapScale;
                y = q * Math.Sin(a) * 14000 / mapScale;
            }
            else if (huB == 0)
            {
                x0 = getx0();
                xn = getxn();
                yn = getyn();

                //MessageBox.Show("L: " + L + " B: " + B + " huL: " + huL + " huB: " + huB + " x0: " + x0 + " xn: " + xn + " yn: " + yn, "中间结果1");
                //MessageBox.Show(" q: " + q + " sinan: " + sinan + " an: " + an + " l: " + l + " a: " + a, "中间结果2");
                //MessageBox.Show((q * Math.Sin(a) * 14000).ToString(), "中间结果3");

                x = 0;
                y = yn * l / ln * 14000 / mapScale;
            }
            else
            {
                x0 = getx0();
                xn = getxn();
                yn = getyn();

                q = getq();
                sinan = getsinan();
                an = getan();
                l = getl(L, midlL);
                a = geta();

                //MessageBox.Show("L: " + L + " B: " + B + " huL: " + huL + " huB: " + huB + " x0: " + x0 + " xn: " + xn + " yn: " + yn, "中间结果1");
                //MessageBox.Show(" q: " + q + " sinan: " + sinan + " an: " + an + " l: " + l + " a: " + a, "中间结果2");
                //MessageBox.Show((q * Math.Sin(a) * 14000).ToString(), "中间结果3");

                x = (x0 + q * (1 - Math.Cos(a))) * (0.888428) * 14000 / mapScale;
                y = q * Math.Sin(a) * 14000 / mapScale;
            }
        }
    }

    public class GDBOperation
    {
        private static Envelope mapEnvelope = new EnvelopeClass();

        static string appAath = DCDHelper.GetAppDataPath();
        static string projectedGDB = "投影数据库.gdb";
        public static string fullPath = appAath + "\\" + projectedGDB;
        public static IFeatureWorkspace fws = null;

        public static string MultipartToSinglepsuffix = "_MultipartToSinglep";
        public static string Unknownsuffix = "_Unknown";
        public static string Dissolvedsuffix = "_Dissolved";

        public static string suffixToRemove = MultipartToSinglepsuffix + Unknownsuffix;

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

        private static void MoveFeatures(IFeatureClass fc, string fcname, WaitOperation wo)
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

        private static void splitFeaturePolyline(IPolyline splitLine, IFeatureClass fc)
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

        // 极有可能报异常，但仍可以得到最后的数据
        private static void splitFeaturePolygon(IPolyline splitLine, IFeatureClass fc)
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

        public static void SplitFeatures(IFeatureClass fc, string fcname, WaitOperation wo)
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

        public static void FeatureClassProject(KeyValuePair<string, IFeatureClass> fcName2FC, IFeatureWorkspace fws, esriGeometryType geometryType, int fcNum, int fcTotalNum, double midlL, bool bound, double mapScale, WaitOperation wo)
        {
            IFeatureClass fc = fcName2FC.Value;
            String fcname = fcName2FC.Key;

            if (bound)
            {
                SplitFeatures(fc, fcname, wo);
                MoveFeatures(fc, fcname, wo);

                var keyValuePair = FCToUnknown(fws, fcname, fc, wo);
                fcname = keyValuePair.Key;
                fc = keyValuePair.Value;

                /*
                keyValuePair = FCToDissolved(fws, fcname, fc, wo);
                fcname = keyValuePair.Key;
                fc = keyValuePair.Value;

                keyValuePair = FCToUnknown(fws, fcname, fc, wo);
                fcname = keyValuePair.Key;
                fc = keyValuePair.Value;
                 */

                keyValuePair = FCRemoveSuffix(fws, fcname, fc, wo);
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

                            MultiConicCoordinateTransformation.multiConicProjection(ref xCoordination, ref yCoordination, longitude, latitude, midlL, mapScale);

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

                                MultiConicCoordinateTransformation.multiConicProjection(ref xCoordination, ref yCoordination, longitude, latitude, midlL, mapScale);

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

                                    MultiConicCoordinateTransformation.multiConicProjection(ref xCoordination, ref yCoordination, longitude, latitude, midlL, mapScale);

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


        public static void GDBProject(IWorkspace ws, double mapScale, WaitOperation wo)
        {
            #region 创建并初始化投影数据库

            // 创建GP工具对象
            Geoprocessor geoprocessor = new Geoprocessor();
            geoprocessor.OverwriteOutput = true;

            GDBInit(ref mapEnvelope, geoprocessor, ws, wo);

            #endregion

            #region 在投影数据库中进行投影

            double realMidlL = (mapEnvelope.XMax + mapEnvelope.XMin) / 2;

            double midlL = 150;

            bool bound = true; // 是否需要根据实际中央经线与目标中央经线的差值进行原始数据裁剪,以完善公式限制

            ws = DCDHelper.createTempWorkspace(fullPath);

            fws = ws as IFeatureWorkspace;

            Dictionary<string, IFeatureClass> fcName2FC = DCDHelper.GetAllFeatureClassFromWorkspace(fws);

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
                    FeatureClassProject(kv, fws, geometryType, fcNum, fcTotalNum, realMidlL, bound, mapScale, wo);
                }
                else
                {
                    FeatureClassProject(
                        FCMultipartToSinglepart(geoprocessor, ws, fcname, fc, fcTotalNum, fcNum, wo),
                        fws, geometryType, fcNum, fcTotalNum, midlL, bound, mapScale, wo);
                }
            }

            #endregion

            #region 在投影数据库中进行定义投影

            fcName2FC = DCDHelper.GetAllFeatureClassFromWorkspace(fws);

            foreach (var kv in fcName2FC)
            {
                IFeatureClass fc = kv.Value;
                String fcname = kv.Key;

                wo.SetText("将投影数据库中的要素类" + fcname + "定义为Web墨卡托投影");

                // 使用空间参考工厂创建 WGS_1984_Web_Mercator 空间参考对象
                ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass();
                ISpatialReferenceFactory3 spatialReferenceFactory3 = (ISpatialReferenceFactory3)spatialReferenceFactory;
                ISpatialReference wgsWebMercator = spatialReferenceFactory3.CreateSpatialReference(3785);

                DefineProjection defineProjection = new DefineProjection();
                defineProjection.in_dataset = fullPath + "\\" + fcname;
                defineProjection.coor_system = wgsWebMercator;

                Helper.ExecuteGPTool(geoprocessor, defineProjection, null);
            }

            #endregion
        }

        // 这个函数使用 GP 工具将要素类投影到 GCS_WGS_1984
        public static void FeatureClassReverseProject(string fcname, IFeatureClass fc, WaitOperation wo)
        {
            wo.SetText("正在将原始投影数据库中的要素类" + fcname + "反投影为地理坐标系");

            // 创建 GP 工具
            Geoprocessor geoprocessor = new Geoprocessor();
            geoprocessor.OverwriteOutput = true;

            Project project = new Project();

            project.in_dataset = fcname;
            IGeoDataset geoDataset = (IGeoDataset)fc;
            project.in_coor_system = geoDataset.SpatialReference;

            ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass();
            IGeographicCoordinateSystem wgs1984 = spatialReferenceFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984);
            project.out_coor_system = wgs1984;
            project.out_dataset = fcname + "WGS1984";

            Helper.ExecuteGPTool(geoprocessor, project, null);
        }

        public static void GDBInit(ref Envelope mapEnvelope, Geoprocessor geoprocessor, IWorkspace ws, WaitOperation wo)
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

            /*
               // 创建一个 SpatialReferenceFactory
               Type factoryType = Type.GetTypeFromProgID("esriGeometry.SpatialReferenceEnvironment");
               ISpatialReferenceFactory spatialReferenceFactory = Activator.CreateInstance(factoryType) as ISpatialReferenceFactory;
               
               // 设置为指定投影坐标系统
               ISpatialReference projectedSpatialReference = spatialReferenceFactory.CreateProjectedCoordinateSystem((int)esriSRProjCSType.esriSRProjCS_WGS1984UTM_10N);
               
               // 可以设置其他投影坐标系的参数，例如投影方式、单位等
               // 比如，要设置投影单位为米：
               IProjectedCoordinateSystem projectedCoordSys = projectedSpatialReference as IProjectedCoordinateSystem;
               projectedCoordSys.CoordinateUnit(1);
             */

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

                // 获取要素类中要素的数量
                int featureCount = fc.FeatureCount(null); // 如果传入 null，则计算所有的要素数量
                if (featureCount == 0)
                {
                    continue;
                }

                IGeoDataset geoDataset = (IGeoDataset)fc;

                if (geoDataset.SpatialReference is IProjectedCoordinateSystem)
                {
                    FeatureClassReverseProject(fcname, fc, wo);

                    esriGeometryType geometryType = fc.ShapeType;
                    string geoType = GetGeometryType(geometryType);
                    if (string.IsNullOrEmpty(geoType))
                    {
                        MessageBox.Show("要素类" + fcname + "的几何类型不受支持，无法创建", "Error", MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        continue;
                    }

                    if (fc.FeatureType == esriFeatureType.esriFTAnnotation)
                    {
                        Console.WriteLine("要素类" + fcname + "为注记类，无法投影");
                        continue;
                    }

                    wo.SetText("正在创建投影数据库的第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname + "WGS1984");

                    fc = fws.OpenFeatureClass(fcname + "WGS1984");

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

                    createFeatureclass.spatial_reference = unknownSpatialReference;
                    createFeatureclass.template = fcname;
                    createFeatureclass.out_name = fcname + "WGS1984";
                    createFeatureclass.out_path = fullPath;
                    createFeatureclass.geometry_type = geoType;

                    Helper.ExecuteGPTool(geoprocessor, createFeatureclass, null);

                    wo.SetText("正在拷贝投影数据库的第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname + "WGS1984");

                    append.inputs = fcname + "WGS1984";
                    append.target = fullPath + "\\" + fcname + "WGS1984";
                    append.schema_type = "TEST";
                    Helper.ExecuteGPTool(geoprocessor, append, null);
                }
                else
                {
                    esriGeometryType geometryType = fc.ShapeType;
                    string geoType = GetGeometryType(geometryType);
                    if (string.IsNullOrEmpty(geoType))
                    {
                        MessageBox.Show("要素类" + fcname + "的几何类型不受支持，无法创建", "Error", MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        continue;
                    }

                    if (fc.FeatureType == esriFeatureType.esriFTAnnotation)
                    {
                        Console.WriteLine("要素类" + fcname + "为注记类，无法投影");
                        continue;
                    }

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

                    createFeatureclass.spatial_reference = unknownSpatialReference;
                    createFeatureclass.template = fcname;
                    createFeatureclass.out_name = fcname;
                    createFeatureclass.out_path = fullPath;
                    createFeatureclass.geometry_type = geoType;

                    Helper.ExecuteGPTool(geoprocessor, createFeatureclass, null);

                    wo.SetText("正在拷贝投影数据库的第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname);

                    append.inputs = fcname;
                    append.target = fullPath + "\\" + fcname;
                    append.schema_type = "TEST";
                    Helper.ExecuteGPTool(geoprocessor, append, null);
                }
            }
        }

        public void AddField(FeatureClass fc, string fcname, WaitOperation wo)
        {
            wo.SetText("正在为" + "要素类" + fcname + "的要素添加字段");

            // 创建GP工具对象
            Geoprocessor geoprocessor = new Geoprocessor();
            geoprocessor.OverwriteOutput = true;

            AddField addField = new AddField();
            addField.in_table = fullPath + "\\" + fcname;
            addField.field_name = "FeatureOID";
            addField.field_type = "TEXT";
            addField.field_is_nullable = "NULLABLE";
            addField.field_is_required = "NON_REQUIRED";

            Helper.ExecuteGPTool(geoprocessor, addField, null);
        }

        public void CalculateField(FeatureClass fc, string fcname, WaitOperation wo)
        {
            wo.SetText("正在为" + "要素类" + fcname + "的要素添加字段");

            // 创建GP工具对象
            Geoprocessor geoprocessor = new Geoprocessor();
            geoprocessor.OverwriteOutput = true;

            CalculateField calculateField = new CalculateField();
            calculateField.in_table = fullPath + "\\" + fcname;
            calculateField.field = "FeatureOID";
            calculateField.expression = "[OBJECTID]";
            calculateField.expression_type = "VB";

            Helper.ExecuteGPTool(geoprocessor, calculateField, null);
        }

        public static KeyValuePair<string, IFeatureClass> FCMultipartToSinglepart(Geoprocessor geoprocessor, IWorkspace ws, String fcname, IFeatureClass fc, int fcTotalNum, int fcNum, WaitOperation wo)
        {
            fws = ws as IFeatureWorkspace;

            wo.SetText("正在多部件处理投影数据库的第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname);

            MultipartToSinglepart multipartToSinglepart = new MultipartToSinglepart();
            multipartToSinglepart.in_features = fcname;
            String fcname_MultipartToSinglep = fcname + MultipartToSinglepsuffix;
            multipartToSinglepart.out_feature_class = fullPath + "\\" + fcname_MultipartToSinglep;

            Helper.ExecuteGPTool(geoprocessor, multipartToSinglepart, null);

            IFeatureClass fc_MultipartToSinglep = fws.OpenFeatureClass(fcname_MultipartToSinglep);

            var kv_MultipartToSinglep =
                new KeyValuePair<string, IFeatureClass>(fcname_MultipartToSinglep, fc_MultipartToSinglep);

            ((IDataset)fc).Delete(); // 删除原始要素类

            return kv_MultipartToSinglep;
        }

        public static KeyValuePair<string, IFeatureClass> FCToUnknown(IFeatureWorkspace fws, String fcname, IFeatureClass fc, WaitOperation wo)
        {
            wo.SetText("正在未知坐标系处理投影数据库的要素类" + fcname);

            // 创建GP工具对象
            Geoprocessor geoprocessor = new Geoprocessor();
            geoprocessor.OverwriteOutput = true;

            String fcname_Unknown = fcname + Unknownsuffix;

            // 使用CreateFeatureclass工具
            CreateFeatureclass createFeatureclass = new CreateFeatureclass();

            // 设置为未知坐标系统
            ISpatialReference unknownSpatialReference = new UnknownCoordinateSystem() as ISpatialReference;

            // 使用Append工具
            Append append = new Append();

            esriGeometryType geometryType = fc.ShapeType;

            createFeatureclass.spatial_reference = unknownSpatialReference;
            createFeatureclass.template = fullPath + "\\" + fcname;
            createFeatureclass.out_name = fcname_Unknown;
            createFeatureclass.out_path = fullPath;
            createFeatureclass.geometry_type = GetGeometryType(geometryType);

            Helper.ExecuteGPTool(geoprocessor, createFeatureclass, null);

            append.inputs = fullPath + "\\" + fcname;
            append.schema_type = "TEST";
            append.target = fullPath + "\\" + fcname_Unknown;
            Helper.ExecuteGPTool(geoprocessor, append, null);

            IFeatureClass fc_Unknown = fws.OpenFeatureClass(fcname_Unknown);

            var kv_Unknown =
                new KeyValuePair<string, IFeatureClass>(fcname_Unknown, fc_Unknown);

            ((IDataset)fc).Delete(); // 删除不是未知坐标系的多部件要素类

            return kv_Unknown;
        }

        public static string RemoveSuffix(string input, string suffix)
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

        // 可能不成功的原因：https://desktop.arcgis.com/zh-cn/arcmap/latest/tools/supplement/tiled-processing-of-large-datasets.htm#GUID-03AE5F53-40DE-4BA7-888B-5198435B0C42
        public KeyValuePair<string, IFeatureClass> FCToDissolved(IFeatureWorkspace fws, string fcname, IFeatureClass fc, WaitOperation wo)
        {
            wo.SetText("正在融合" + "要素类" + fcname + "的要素");

            // 创建一个 Geoprocessor 实例并执行 Dissolve 工具
            Geoprocessor geoprocessor = new Geoprocessor();
            geoprocessor.OverwriteOutput = true;

            String fcname_Dissolved = fcname + Dissolvedsuffix;

            // 创建一个 Dissolve 工具实例
            Dissolve dissolveTool = new Dissolve();

            // 设置输入要素类
            dissolveTool.in_features = fullPath + "\\" + fcname;

            // 设置输出要素类
            dissolveTool.out_feature_class = fullPath + "\\" + fcname_Dissolved; // 替换为输出要素类的路径

            // 设置要素融合的字段
            dissolveTool.dissolve_field = "ORIG_FID"; // 替换为用于融合的字段名

            dissolveTool.statistics_fields = "ORIG_FID COUNT";

            Helper.ExecuteGPTool(geoprocessor, dissolveTool, null);

            IFeatureClass fc_Dissolved = fws.OpenFeatureClass(fcname_Dissolved);

            ((IDataset)fc).Delete(); // 删除未知坐标系的多部件要素类

            var kv_Dissolved =
                new KeyValuePair<string, IFeatureClass>(fcname_Dissolved, fc_Dissolved);

            return kv_Dissolved;
        }

        public static KeyValuePair<string, IFeatureClass> FCRemoveSuffix(IFeatureWorkspace fws, string fcname, IFeatureClass fc, WaitOperation wo)
        {
            wo.SetText("正在更改" + "要素类" + fcname + "的名称");

            // 创建一个 Geoprocessor 实例并执行 Copy 工具
            Geoprocessor geoprocessor = new Geoprocessor();
            geoprocessor.OverwriteOutput = true;

            string fcname_Changed = RemoveSuffix(fcname, suffixToRemove);

            // 创建一个 Copy 工具实例
            Copy copy = new Copy();

            // 设置输入要素类
            copy.in_data = fullPath + "\\" + fcname;

            // 设置输出要素类
            copy.out_data = fullPath + "\\" + fcname_Changed; // 替换为输出要素类的路径

            Helper.ExecuteGPTool(geoprocessor, copy, null);

            IFeatureClass fc_Changed = fws.OpenFeatureClass(fcname_Changed);

            ((IDataset)fc).Delete(); // 删除未改名的未知坐标系的多部件要素类

            var kv_Changed =
                new KeyValuePair<string, IFeatureClass>(fcname_Changed, fc_Changed);

            return kv_Changed;
        }
    }
}