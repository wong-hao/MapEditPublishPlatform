using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Display;
using System.Windows.Forms;
using ESRI.ArcGIS.esriSystem;

namespace SMGI.Plugin.AnnoEdit
{
    public class AnnoMerge : SMGI.Common.SMGICommand
    {
        /// <summary>
        /// 选择注记的要素层，以第一个选择的要素图层为准
        /// </summary>
        private IFeatureLayer m_FeatureLayer;
        /// <summary>
        /// Map中选择要素的枚举器
        /// </summary>
        private IEnumFeature m_MapEnumFeature;

        public AnnoMerge()
        {
            m_caption = "注记合并";
            m_toolTip = "鼠标左键进行合并注记选择，右键进行注记合并操作，提示：只能合并同一注记层里的注记";
            m_category = "注记编辑";
        }

        public override bool Enabled
        {
            get
            {
                return m_Application != null && m_Application.Workspace != null && m_Application.EngineEditor.EditState == esriEngineEditState.esriEngineStateEditing && (m_Application.MapControl.Map.FeatureSelection as IEnumFeature).Next() != null;
            }
        }

        public override void OnClick()
        {
            #region 获取选中的注记要素
            IAnnotationFeature selAnnoFeature = null;

            IEnumFeature enumFeature = m_Application.MapControl.Map.FeatureSelection as IEnumFeature;
            IFeature feature1 = null;
            while ((feature1 = enumFeature.Next()) != null)
            {
                IAnnotationFeature annoFeature1 = feature1 as IAnnotationFeature;
                if (annoFeature1 == null || annoFeature1.Annotation == null)
                    continue;

                IElement element = annoFeature1.Annotation;
                if (element is AnnotationElement)
                {
                    if (selAnnoFeature == null)
                    {
                        selAnnoFeature = annoFeature1;
                    }
                }
            }
            Marshal.ReleaseComObject(enumFeature);
            #endregion

            #region 合并创建多部件文字注记
            IElement annoElement = selAnnoFeature.Annotation;//选中的注记元素
            IMultiPartTextElement mutiPartElement = annoElement as IMultiPartTextElement;
            if (mutiPartElement != null)
            {
                IAnnotationClassExtension2 annoExtension = (selAnnoFeature as IFeature).Class.Extension as IAnnotationClassExtension2;
                if (!mutiPartElement.IsMultipart)
                    mutiPartElement.ConvertToMultiPart(annoExtension.get_Display(annoElement));
                while (mutiPartElement.PartCount > 0)
                {
                    mutiPartElement.DeletePart(0);//删除原部分
                }
            }
            #endregion

            ISelection pSelection = m_Application.MapControl.Map.FeatureSelection;
            m_MapEnumFeature = pSelection as IEnumFeature;
            if (m_FeatureLayer == null)
            {   //需要初始化选择的注记图层
                m_MapEnumFeature.Reset();
                IFeature pFeature = m_MapEnumFeature.Next();
                var lyrs = m_Application.Workspace.LayerManager.GetLayer(new SMGI.Common.LayerManager.LayerChecker(l =>
                { return l is IFDOGraphicsLayer; })).ToArray();
                for (int i = 0; i < lyrs.Length; i++)
                {
                    IFeatureLayer pFeatureLayer = lyrs[i] as IFeatureLayer;
                    if (pFeatureLayer != null && pFeatureLayer.FeatureClass.AliasName == pFeature.Class.AliasName)
                    {
                        m_FeatureLayer = pFeatureLayer;
                        break;
                    }
                }
            }
            m_MapEnumFeature.Reset();
            List<IFeature> pSelectFeatureList = new List<IFeature>();
            IFeature pSelectFeature = m_MapEnumFeature.Next();

            while (pSelectFeature != null)
            {
                pSelectFeatureList.Add(pSelectFeature);
                pSelectFeature = m_MapEnumFeature.Next();
            }
            if (pSelectFeatureList.Count < 2)
            {
                //小于两条注记要素
                return;
            }
            IFeatureClass annoFeatureClass = m_FeatureLayer.FeatureClass;
            IAnnotationFeature2 annoFeature = pSelectFeatureList[0] as IAnnotationFeature2;//第一个注记元素
            int pIndex = pSelectFeatureList[0].Fields.FindField("分类");//“分类”字段的索引值
            int FeatureID = annoFeature.LinkedFeatureID;
            int AnnotationClassID = annoFeature.AnnotationClassID;

            #region 将打散的注记插入至多部件注记

            IPolyline pl = new PolylineClass();//字体基线

            for (int i = 0; i < pSelectFeatureList.Count; i++)
            {
                annoFeature = pSelectFeatureList[i] as IAnnotationFeature2;

                ITextElement textElement = annoFeature.Annotation as ITextElement;
                if (textElement != null)
                {
                    pl.FromPoint = new PointClass()
                    {
                        X = (pSelectFeatureList[i].Shape.Envelope.XMin + pSelectFeatureList[i].Shape.Envelope.XMax) / 2,
                        Y = (pSelectFeatureList[i].Shape.Envelope.YMin + pSelectFeatureList[i].Shape.Envelope.YMax) / 2
                    };
                    pl.ToPoint = new PointClass()
                    {
                        X = (pSelectFeatureList[i].Shape.Envelope.XMin + pSelectFeatureList[i].Shape.Envelope.XMax) / 2,
                        Y = (pSelectFeatureList[i].Shape.Envelope.YMin + pSelectFeatureList[i].Shape.Envelope.YMax) / 2
                    };
                    mutiPartElement.InsertPart(i, textElement.Text, pl);
                }
            }
            #endregion

            m_Application.EngineEditor.EnableUndoRedo(true);
            m_Application.EngineEditor.StartOperation();
            IFeature feature = annoFeatureClass.CreateFeature();
            IAnnotationFeature2 merge_annoFeature = feature as IAnnotationFeature2;
            mutiPartElement.ConvertToSinglePart();
            merge_annoFeature.Annotation = mutiPartElement as IElement;
            merge_annoFeature.LinkedFeatureID = FeatureID;
            merge_annoFeature.AnnotationClassID = AnnotationClassID;
            merge_annoFeature.Status = esriAnnotationStatus.esriAnnoStatusPlaced;
            feature.set_Value(pIndex, pSelectFeatureList[0].get_Value(pIndex));
            feature.Store();
            //删除原有的注记要素
            for (int i = 0; i < pSelectFeatureList.Count; i++)
            {
                pSelectFeatureList[i].Delete();
            }
            pSelectFeatureList.Clear();
            //清空m_FeatureLayer图层
            m_FeatureLayer = null;
            //进行合并后注记要素的高亮显示
            m_Application.MapControl.Map.SelectByShape(feature.Shape, null, true);
            m_Application.MapControl.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewAll, null, feature.Extent);
            m_Application.EngineEditor.StopOperation("注记合并");
        }
    }
}
