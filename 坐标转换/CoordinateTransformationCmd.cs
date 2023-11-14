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
                return m_Application != null;
            }
        }

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

        void multiConicProjection(double[] x, double[] y, double L, double B, double midlL, double mapScale)
        {
            huL = L * Math.PI / 180;
            huB = B * Math.PI / 180;

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
            multiConicProjection(x, y, 324, 33, 150, 1100);
            MessageBox.Show("x[0]: " + x[0] + " y[0]: " + y[0]);
        }
    }
}
