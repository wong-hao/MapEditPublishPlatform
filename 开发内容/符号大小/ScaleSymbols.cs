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

    public class ScaleSymbols
    {
        public static void setScaleSymbols(IMap map, WaitOperation wo)
        {
            int totalLyrCount = map.LayerCount;

            // 遍历地图中的所有图层
            for (int i = 0; i <= totalLyrCount - 1; i++)
            {
                ILayer layer = map.get_Layer(i);

                if (layer == null)
                {
                    continue;
                }

                // 尝试将图层强制转换为FeatureLayer
                IFeatureLayer featureLayer = layer as FeatureLayer;

                // 如果转换成功，则禁用该图层的scale symbols
                if (featureLayer != null)
                {
                    featureLayer.ScaleSymbols = false;
                }

                if (layer is IGroupLayer)
                {
                    ICompositeLayer pGroupLayer = layer as ICompositeLayer;

                    for (int j = 0; j <= pGroupLayer.Count - 1; j++)
                    {
                        wo.SetText("正在处理第" + (j + 1) + "/" + pGroupLayer.Count + "个图层");
                        Console.WriteLine("正在处理第" + (j + 1) + "/" + pGroupLayer.Count + "个图层");

                        ILayer pCompositeLayer =pGroupLayer.get_Layer(j);

                        // 尝试将图层强制转换为FeatureLayer
                        IFeatureLayer pCompositeFeatureLayer = pCompositeLayer as FeatureLayer;

                        // 如果转换成功，则禁用该图层的scale symbols
                        if (pCompositeFeatureLayer != null)
                        {
                            pCompositeFeatureLayer.ScaleSymbols = false;
                        }
                    }
                }
            }
        }
    }
}