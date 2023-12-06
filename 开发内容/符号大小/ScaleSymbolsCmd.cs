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

namespace SMGI.Plugin.EmergencyMap
{

    public class SaleSymbolsCmd : SMGI.Common.SMGICommand
    {
        public override bool Enabled
        {
            get
            {
                return m_Application != null && m_Application.Workspace != null;
            }
        }

        public override void OnClick()
        {
            double mapScale = m_Application.MapControl.Map.ReferenceScale;
            if (mapScale == 0)
            {
                MessageBox.Show("请先设置参考比例尺！");
                return;
            }

            IMap map = m_Application.MapControl.Map;

            using (var wo = m_Application.SetBusy())
            {
                ScaleSymbols.setScaleSymbols(map, wo);
            }
        }
    }
}