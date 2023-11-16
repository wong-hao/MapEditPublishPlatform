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

        private double mapScale = 0.0;
        private double R = 6371116;
        private double mu0 = 1 / 26000000;
        private double x0;
        private double xn;
        private double yn;
        private double q;
        private double sinan;
        private double an;
        private double a;
        private double l;
        private double ln = 201.6 * Math.PI / 180;
        double[] x = new double[1];
        double[] y = new double[1];
        private double huL;
        private double huB;

        public double getx0()
        {
            return (huB + 0.06683225 * Math.Pow(huB, 4)) * R / 140000;
        }

        double getxn()
        {
            return x0 + 0.20984 * huB * R / 140000;
        }

        double getyn()
        {
            return Math.Sqrt(Math.Pow(112, 2) - Math.Pow(xn, 2)) + 20;
        }

        double getq()
        {
            return (Math.Pow(yn, 2) + Math.Pow(xn - x0, 2)) / (2 * (xn - x0));
        }

        double getsinan()
        {
            return yn / q;
        }

        double getan()
        {
            return Math.Asin(sinan);
        }

        double getl(double L, double midlL)
        {
            return (L - midlL) * Math.PI / 180;
        }

        double geta()
        {
            return an * l / ln;
        }

        // L：经度（坐标）；B：纬度（坐标）
        void multiConicProjection(double[] x, double[] y, double L, double B, double midlL, double mapScale)
        {
            huL = L * Math.PI / 180; //纬度（弧度）
            huB = B * Math.PI / 180; //经度（弧度）

            x0 = getx0();
            xn = getxn();
            yn = getyn();

            //MessageBox.Show("L: " + L + " B: " + B + " huL: " + huL + " huB: " + huB + " x0: " + x0 + " xn: " + xn + " yn: " + yn, "中间结果1");

            if (huB < 0)
            {
                q = getq();
                sinan = getsinan();
                an = getan();
                l = getl(L, midlL);
                a = geta();

                //MessageBox.Show(" q: " + q + " sinan: " + sinan + " an: " + an + " l: " + l + " a: " + a, "中间结果2");
                //MessageBox.Show((q * Math.Sin(a) * 14000).ToString(), "中间结果3");

                x[0] = (x0 + q * (1 - Math.Cos(a))) * (-0.888428) * 14000 / mapScale;
                y[0] = q * Math.Sin(a) * 14000 / mapScale;
            }
            else if (huB == 0)
            {
                x[0] = 0;
                y[0] = yn * l / ln * 14000 / mapScale;
            }
            else
            {
                q = getq();
                sinan = getsinan();
                an = getan();
                l = getl(L, midlL);
                a = geta();

                x[0] = (x0 + q * (1 - Math.Cos(a))) * (0.888428) * 14000 / mapScale;
                y[0] = q * Math.Sin(a) * 14000 / mapScale;
            }
        }

        public override void OnClick()
        {

            mapScale = m_Application.MapControl.Map.ReferenceScale;

            if (mapScale == 0)
            {
                MessageBox.Show("请先设置参考比例尺！");
                return;
            }

            //multiConicProjection(x, y, 324, 33, 150, mapScale);
            //MessageBox.Show("x[0]: " + x[0] + " y[0]: " + y[0]);

            using (var wo = m_Application.SetBusy())
            {
                GeoDBDataTransfer(m_Application.Workspace.EsriWorkspace, wo);
            }

        }

        public void GeoDBDataTransfer(IWorkspace ws, WaitOperation wo)
        {
            IFeatureWorkspace fws = ws as IFeatureWorkspace;
            Dictionary<string, IFeatureClass> fcName2FC = DCDHelper.GetAllFeatureClassFromWorkspace(fws);
            // 获取数据库中要素类的数量
            int fcTotalNum = fcName2FC.Count;
            int fcNum = 0;
            foreach (var kv in fcName2FC)
            {
                fcNum++;
                IFeatureClass fc = kv.Value;
                String fcname = kv.Key;

                ISpatialReference ISR = (fc as IGeoDataset).SpatialReference;
                esriGeometryType geometryType = fc.ShapeType;

                FeatureClassTransfer(kv, fws, geometryType, ISR, fcNum, fcTotalNum, wo);
            }

        }

        public void FeatureClassTransfer(KeyValuePair<string, IFeatureClass> fcName2FC, IFeatureWorkspace fws, esriGeometryType geometryType, ISpatialReference ISR, int fcNum, int fcTotalNum, WaitOperation wo)
        {
            IFeatureClass fc = fcName2FC.Value;
            String fcname = fcName2FC.Key;

            // 获取要素类中要素的数量
            int featureCount = fc.FeatureCount(null); // 如果传入 null，则计算所有的要素数量
            IFeatureClassManage featureClassManage = (IFeatureClassManage)fc;

            // 设置为未知坐标系统
            ISpatialReference unknownSpatialReference = new UnknownCoordinateSystem() as ISpatialReference;

            IFeature feature;
            IGeometry pGeo;
            IPoint point;
            IPointCollection pointCollection;
            int pointCount;

            int featurecount = 0;

            double longitude;
            double latitude;
            IFeatureCursor featureCursor = null;
            try
            {
                featureCursor = fc.Search(null, false);

                while ((feature = (IFeature)featureCursor.NextFeature()) != null)
                {
                    featurecount++;
                    pGeo = feature.Shape;
                    wo.SetText("正在处理第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname + "的第" + featurecount + "/" + featureCount + "个要素");
                    Console.WriteLine("正在处理第" + fcNum + "/" + fcTotalNum + "个要素类" + fcname + "的第" + featurecount + "/" + featureCount + "个要素");
                    //pGeo.SpatialReference = unknownSpatialReference;

                    // 不考虑几何为空的要素
                    if (pGeo == null || pGeo.IsEmpty)
                    {
                        continue;
                    }

                    // 根据几何类型输出相应信息
                    switch (geometryType)
                    {
                        case esriGeometryType.esriGeometryPoint:

                            // 获取点要素的几何对象
                            point = pGeo as IPoint;

                            // 设置点要素的坐标
                            longitude = point.X;
                            latitude = point.Y;

                            multiConicProjection(x, y, longitude, latitude, 150, mapScale);

                            point.PutCoords(longitude + 2, latitude + 2);

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

                                multiConicProjection(x, y, longitude, latitude, 150, mapScale);

                                point.PutCoords(longitude + 2, latitude + 2);

                                // 将移动后的点重新设置到要素中
                                pointCollection.UpdatePoint(i, point);
                            }

                            // 更新要素
                            feature.Shape = pointCollection as IGeometry;
                            feature.Store();
                            break;
                        case esriGeometryType.esriGeometryPolygon:
                            IPolygon polygon = (IPolygon)pGeo;

                            IGeometryCollection ringCollection = (IGeometryCollection)polygon;
                            for (int i = 0; i < ringCollection.GeometryCount; i++)
                            {
                                IRing ring = (IRing)ringCollection.get_Geometry(i);
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

                                    multiConicProjection(x, y, longitude, latitude, 150, mapScale);

                                    point.PutCoords(longitude + 2, latitude + 2);

                                    // 将移动后的点重新设置到环中
                                    pointCollection.UpdatePoint(j, point);
                                }
                            }

                            // 更新要素的几何对象
                            feature.Shape = (IGeometry)polygon;
                            feature.Store();
                            break;
                        /* 不对面形变做出处理
                           case esriGeometryType.esriGeometryPolygon:
                           IPolygon polygon = (IPolygon)pGeo;
                           
                           IGeometryCollection ringCollection = (IGeometryCollection)polygon;
                           for (int i = 0; i < ringCollection.GeometryCount; i++)
                           {
                           IRing ring = (IRing)ringCollection.get_Geometry(i);
                           pointCollection = (IPointCollection)ring;
                           
                           for (int j = 0; j < pointCollection.PointCount; j++)
                           {
                           point = pointCollection.get_Point(j);
                           
                            // 设置点要素的坐标
                           longitude = point.X;
                           latitude = point.Y;
                           
                           multiConicProjection(x, y, longitude, latitude, 150, mapScale);
                                                      
                           point.PutCoords(longitude + 2, latitude + 2);
                           
                           // 将移动后的点重新设置到环中
                           pointCollection.UpdatePoint(j, point);
                           }
                           }
                           
                           // 更新要素的几何对象
                           feature.Shape = (IGeometry)polygon;
                           feature.Store();
                           break;
                         */
                        default:
                            MessageBox.Show("出现未知几何类型要素！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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