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

        // 由于公式限制，若不对原始地理坐标系数据进行额外处理，则需要保证L - midlL的范围为[-180, 180]
        double getl(double L, double midlL)
        {
            return (L - midlL) * Math.PI / 180;
        }

        double geta()
        {
            return an * l / ln;
        }

        // L：经度（坐标）；B：纬度（坐标）；mapScale：比例尺（真实值）；midlL：中央经线（坐标）
        public void multiConicProjection(ref double x, ref double y, double L, double B, double midlL, double mapScale)
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
}