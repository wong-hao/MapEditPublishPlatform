using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Carto;
using System.Windows.Forms;
using SMGI.Common;
using ESRI.ArcGIS.Geoprocessing;
using System.Runtime.InteropServices;

namespace SMGI.Plugin.EmergencyMap
{
    public class PolygonGeneralizeCmd : SMGI.Common.SMGICommand
    {
        public PolygonGeneralizeCmd()
        {
        }

        public override bool Enabled
        {
            get
            {
                return m_Application != null && m_Application.Workspace != null &&
                    m_Application.EngineEditor.EditState != esriEngineEditState.esriEngineStateNotEditing;
            }
        }

        public override void OnClick()
        {
            var layerSelector = new LayerSelectWithGeneralizeForm(m_Application);
            layerSelector.GeoTypeFilter = esriGeometryType.esriGeometryPolygon;

            if (layerSelector.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            if (layerSelector.pSelectLayer == null)
            {
                return;
            }

            if (layerSelector.txtBend.Text.Trim() == null ||
                layerSelector.txtSmooth.Text.Trim() == null ||
                layerSelector.txtScale.Text.Trim() == null)
            {
                return;
            }

            double bendValue = double.Parse(layerSelector.txtBend.Text.Trim());
            double smoothValue = double.Parse(layerSelector.txtSmooth.Text.Trim());
            double mapScale = double.Parse(layerSelector.txtScale.Text.Trim());
            IFeatureClass inputFC = (layerSelector.pSelectLayer as IFeatureLayer).FeatureClass;

            bool bsuccess = false;
            using (var wo = m_Application.SetBusy())
            {
                var editor = m_Application.EngineEditor;
                editor.StartOperation();
                bsuccess = Generalize(inputFC, bendValue * mapScale / 1000, smoothValue * mapScale / 1000, true, wo);
                editor.StopOperation("面化简");
            }
            m_Application.ActiveView.Refresh();
            if (bsuccess)
            {
                MessageBox.Show("处理完毕");
            }
        }

        public static bool Generalize(IFeatureClass inputFC, double bendValue, double smoothValue, bool needEdit, WaitOperation wo = null)
        {
            bool bSuccess = false;

            //临时工作空间
            string fullPath = DCDHelper.GetAppDataPath() + "\\MyWorkspace.gdb";
            IWorkspace ws = DCDHelper.createTempWorkspace(fullPath);
            IFeatureWorkspace fws = ws as IFeatureWorkspace;

            IFeatureClass tempFC = null;
            IFeatureClass tempFC_Simplify = null;
            IFeatureClass tempFC_Simplify_Smooth = null;

            //复制数据到临时要素类中
            IQueryFilter qf = new QueryFilterClass();
            if (inputFC.FindField(cmdUpdateRecord.CollabVERSION) != -1)
                qf.WhereClause = string.Format("({0} <> {1} or {2} is null)", cmdUpdateRecord.CollabVERSION, cmdUpdateRecord.DeleteState, cmdUpdateRecord.CollabVERSION);
            tempFC = DCDHelper.CreateFeatureClassStructToWorkspace(ws as IFeatureWorkspace, inputFC, inputFC.AliasName + "_temp");
            DCDHelper.CopyFeaturesToFeatureClass(inputFC, qf, tempFC, false);


            var gp = GApplication.Application.GPTool;
            gp.OverwriteOutput = true;
            try
            {
                string simplifyInFeature = ws.PathName + @"\" + tempFC.AliasName;
                string simplifyOutFeature = ws.PathName + @"\" + tempFC.AliasName + "_Simplify";

                ESRI.ArcGIS.CartographyTools.SimplifyPolygon simplifyPolygonTool = new ESRI.ArcGIS.CartographyTools.SimplifyPolygon(simplifyInFeature, simplifyOutFeature, "BEND_SIMPLIFY", bendValue);
                IGeoProcessorResult geoResult = null;

                if (wo != null)
                    wo.SetText(string.Format("正在对要素类【{0}】进行化简......", inputFC.AliasName));
                geoResult = (IGeoProcessorResult)gp.Execute(simplifyPolygonTool, null);

                if (geoResult.Status == ESRI.ArcGIS.esriSystem.esriJobStatus.esriJobSucceeded)
                {
                    tempFC_Simplify = (ws as IFeatureWorkspace).OpenFeatureClass(tempFC.AliasName + "_Simplify");

                    if (wo != null)
                        wo.SetText(string.Format("正在对要素类【{0}】进行平滑处理......", inputFC.AliasName));
                    string smoothLineOutFeature = ws.PathName + @"\" + tempFC_Simplify.AliasName + "_Smooth";
                    ESRI.ArcGIS.CartographyTools.SmoothPolygon smoothPolygonTool = new ESRI.ArcGIS.CartographyTools.SmoothPolygon(simplifyOutFeature, smoothLineOutFeature, "PAEK", smoothValue);
                    geoResult = (IGeoProcessorResult)gp.Execute(smoothPolygonTool, null);
                    if (geoResult.Status == ESRI.ArcGIS.esriSystem.esriJobStatus.esriJobSucceeded)
                    {
                        tempFC_Simplify_Smooth = (ws as IFeatureWorkspace).OpenFeatureClass(tempFC_Simplify.AliasName + "_Smooth");

                        int guidIndex = inputFC.FindField(cmdUpdateRecord.CollabGUID);


                        if (needEdit && guidIndex != -1)
                        {
                            bool isEdit = GApplication.Application.EngineEditor.EditState == esriEngineEditState.esriEngineStateEditing;
                            if (!isEdit)
                            {
                                GApplication.Application.EngineEditor.StartEditing(GApplication.Application.Workspace.EsriWorkspace, GApplication.Application.ActiveView.FocusMap);
                                GApplication.Application.EngineEditor.EnableUndoRedo(true);

                                GApplication.Application.EngineEditor.StartOperation();
                                try
                                {
                                    //获取化简后的要素信息
                                    Dictionary<string, int> guid2OID = new Dictionary<string, int>();
                                    IFeatureCursor simpCursor = tempFC_Simplify_Smooth.Search(null, true);
                                    IFeature simpFe = null;
                                    while ((simpFe = simpCursor.NextFeature()) != null)
                                    {
                                        guid2OID.Add(simpFe.get_Value(guidIndex).ToString(), simpFe.OID);
                                    }
                                    Marshal.ReleaseComObject(simpCursor);


                                    //更新要素
                                    IFeatureCursor inCursor = inputFC.Search(qf, false);
                                    IFeature fe = null;
                                    while ((fe = inCursor.NextFeature()) != null)
                                    {
                                        string guid = fe.get_Value(guidIndex).ToString();

                                        if (guid2OID.ContainsKey(guid))
                                        {
                                            int oid = guid2OID[guid];
                                            simpFe = tempFC_Simplify_Smooth.GetFeature(oid);

                                            fe.Shape = simpFe.ShapeCopy;
                                            fe.Store();
                                        }
                                        else
                                        {
                                            fe.Delete();
                                        }
                                    }
                                    Marshal.ReleaseComObject(inCursor);

                                    GApplication.Application.EngineEditor.StopOperation("面化简");

                                    GApplication.Application.EngineEditor.StopEditing(true);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Trace.WriteLine(ex.Message);
                                    System.Diagnostics.Trace.WriteLine(ex.Source);
                                    System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                                    GApplication.Application.EngineEditor.AbortOperation();

                                    GApplication.Application.EngineEditor.StopEditing(false);


                                    throw ex;
                                }


                            }
                        }
                        else
                        {
                            //清空原要素类
                            (inputFC as ITable).DeleteSearchedRows(qf);

                            //将化简后的数据拷贝回原要素类中
                            DCDHelper.CopyFeaturesToFeatureClass(tempFC_Simplify_Smooth, null, inputFC, false);
                        }

                        bSuccess = true;
                    }
                    else
                    {
                        MessageBox.Show("面化简失败");
                    }
                }
                else
                {
                    MessageBox.Show("面化简失败");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                System.Diagnostics.Trace.WriteLine(ex.Source);
                System.Diagnostics.Trace.WriteLine(ex.StackTrace);

                MessageBox.Show(ex.Message);

                bSuccess = false;
            }
            finally
            {
                if (tempFC != null)
                {
                    (tempFC as IDataset).Delete();
                }

                if (tempFC_Simplify != null)
                {
                    (tempFC_Simplify as IDataset).Delete();
                }

                if (tempFC_Simplify_Smooth != null)
                {
                    (tempFC_Simplify_Smooth as IDataset).Delete();
                }
            }

            return bSuccess;
        }
    }
}
