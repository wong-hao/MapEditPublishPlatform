using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SMGI.Plugin.EmergencyMap
{
    public partial class ProjectionParaSet : Form
    {
        public ProjectionParaSet()
        {
            InitializeComponent();
        }

        public string projectionParameter
        {
            get;
            set;
        }
    
        private void ParaSet_Load(object sender, EventArgs e)
        {
            comboBox1.Items.Add("等差分纬线多圆锥");
            comboBox1.Items.Add("伪方位");

            comboBox1.SelectedIndex = 0;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (comboBox1.Text.Trim() == "")
            {
                MessageBox.Show("输入不能为空！");
                return;
            }

            projectionParameter = comboBox1.Text.Trim();
            DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
