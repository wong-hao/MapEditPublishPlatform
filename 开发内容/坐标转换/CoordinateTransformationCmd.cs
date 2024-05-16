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

    public class CoordinateTransformationCmd : SMGI.Common.SMGICommand
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
            using (var wo = m_Application.SetBusy())
            {
                ProjectionParaSet pa = new ProjectionParaSet();
                if (pa.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                string projectionType = pa.projectionParameter;
                GDBOperation.GDBProject(m_Application.Workspace.EsriWorkspace, projectionType, wo);
            }
        }
    }
}