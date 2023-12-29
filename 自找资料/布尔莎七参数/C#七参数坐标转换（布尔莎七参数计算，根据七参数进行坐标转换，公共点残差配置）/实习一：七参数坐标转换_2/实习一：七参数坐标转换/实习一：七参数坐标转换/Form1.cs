using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace 实习一_七参数坐标转换
{
    public partial class Form1 : Form
    {
        double[,] oldX = new double[20,3];
        string[] name = new string[20];
        double[,] newX = new double[20,3];
        double[,] para = new double[7,1];
        double[,] oldX2 = new double[20, 3];
        string[] name2 = new string[20];
        int length2 = 0;
        int length=0;
        public Form1()
        {
            InitializeComponent();
        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog MyDlg = new OpenFileDialog();  //定义新的打开文件的界面，
            MyDlg.Title = "加载坐标数据";//修改对话框的标题栏
            MyDlg.Filter = "TXT Files(*.txt)|*.txt";
            string pathname = null;//定义字符串变量，用于存储文件的路径名称
            
            if (MyDlg.ShowDialog() == DialogResult.OK) //一个判断窗口，判断你是否点击ok  
            {                                            
                pathname = MyDlg.FileName;
                
            }                                                      
            else
            {
                MessageBox.Show("加载文件失败！", "错误");
                return;
            }
            var reader = new StreamReader(pathname);
            string buf = reader.ReadLine();
            for (int i = 0; i < 20; i++)
            {
                buf = reader.ReadLine();
                length = i ;
                if (buf == null) break;
                var arr = buf.Split(',');
                
                //int.Parse(arr[0]);
                name[i] = arr[0];
                oldX[i, 0] = double.Parse(arr[1]);//X
                oldX[i, 1] = double.Parse(arr[2]);//Y
                oldX[i, 2] = double.Parse(arr[3]);//Z
            }
            reader.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog MyDlg = new OpenFileDialog();  //定义新的打开文件的界面，
            MyDlg.Title = "加载坐标数据";//修改对话框的标题栏
            MyDlg.Filter = "TXT Files(*.txt)|*.txt";
            string pathname = null;//定义字符串变量，用于存储文件的路径名称

            if (MyDlg.ShowDialog() == DialogResult.OK) //一个判断窗口，判断你是否点击ok  
            {
                pathname = MyDlg.FileName;

            }
            else
            {
                MessageBox.Show("加载文件失败！", "错误");
                return;
            }
            var reader = new StreamReader(pathname);
            string buf = reader.ReadLine();
            if (length != 0)
            {
                for (int i = 0; i < length; i++)
                {
                    buf = reader.ReadLine();
                    if (buf == null) break;
                    var arr = buf.Split(',');
                    //int.Parse(arr[0]);
                    newX[i, 0] = double.Parse(arr[1]);//X
                    newX[i, 1] = double.Parse(arr[2]);//Y
                    newX[i, 2] = double.Parse(arr[3]);//Z
                }
            }else
            {
                MessageBox.Show("请先加载旧坐标", "错误");
                return;
            }
            reader.Close();
           
        }

        private void button3_Click(object sender, EventArgs e)
        {
            double[,] S = new double[7,1];
            if (newX != null)
            {
                Matrix Matrix = new Matrix();
                double[,] B = new double[3 * length, 7]; for (int i = 0; i < 3 * length; i++) for (int j = 0; j < 7; j++) B[i, j] = 0;
                double[,] l = new double[3 * length,1];
                for (int i = 0; i < length; i++)//B,l
                {
                    B[3 * i, 0] = 1; B[3 * i + 1, 1] = 1; B[3 * i + 2, 2] = 1;
                    B[3 * i, 3] = oldX[i, 0]; B[3 * i + 1, 3] = oldX[i, 1]; B[3 * i + 2, 3] = oldX[i, 2];
                    B[3 * i, 5] = -oldX[i, 2]; B[3 * i + 1, 6] = -oldX[i, 0]; B[3 * i + 2, 4] = -oldX[i, 1];
                    B[3 * i, 6] = oldX[i, 1]; B[3 * i + 1, 4] = oldX[i, 2]; B[3 * i + 2, 5] = oldX[i, 0];
                    l[3 * i,0] = newX[i, 0]; l[3 * i + 1,0] = newX[i, 1]; l[3 * i + 2,0] = newX[i, 2];
                }
                para = Matrix.MultiplyMatrix(Matrix.Athwart(Matrix.MultiplyMatrix(Matrix.Transpose(B), B)), Matrix.MultiplyMatrix(Matrix.Transpose(B), l)); S = para;
                xtextBox.Text = para[0, 0].ToString(); ytextBox.Text = para[1, 0].ToString(); ztextBox.Text = para[2, 0].ToString();
                mtextBox.Text = Convert.ToString((para[3, 0] - 1) * 1000000);
                extextBox.Text = Convert.ToString(para[4, 0] / para[3, 0] * 206265);
                eytextBox.Text = Convert.ToString(para[5, 0] / para[3, 0] * 206265);
                eztextBox.Text = Convert.ToString(para[6, 0] / para[3, 0]*206265);
            }
            else
            {
                MessageBox.Show("请先加载新坐标", "错误");
                return;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            // "保存为"对话框
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "文本文件|*.txt";
            // 显示对话框
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                // 文件名
                string fileName = dialog.FileName;
                // 创建文件，准备写入
                FileStream fs = File.Open(fileName,FileMode.Create, FileAccess.Write);
                StreamWriter wr = new StreamWriter(fs);
                wr.WriteLine("布尔莎七参数计算结果：");
                wr.WriteLine(label1.Text + ":" + xtextBox.Text + label8.Text);
                wr.WriteLine(label2.Text + ":" + ytextBox.Text + label9.Text);
                wr.WriteLine(label3.Text + ":" + ztextBox.Text + label10.Text);
                wr.WriteLine(label4.Text + ":" + mtextBox.Text + label11.Text);
                wr.WriteLine(label5.Text + ":" + extextBox.Text + label12.Text);
                wr.WriteLine(label6.Text + ":" + eytextBox.Text + label13.Text);
                wr.WriteLine(label7.Text + ":" + eztextBox.Text + label14.Text);
                // 关闭文件
                wr.Flush();
                wr.Close();
                fs.Close();
            }
            else 
            {
                MessageBox.Show("保存文件失败！", "错误");
                return;
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            label15.Text = "to do...";
            OpenFileDialog MyDlg = new OpenFileDialog();  //定义新的打开文件的界面，
            MyDlg.Title = "加载坐标数据";//修改对话框的标题栏
            MyDlg.Filter = "TXT Files(*.txt)|*.txt";
            string pathname = null;//定义字符串变量，用于存储文件的路径名称

            if (MyDlg.ShowDialog() == DialogResult.OK) //一个判断窗口，判断你是否点击ok  
            {
                pathname = MyDlg.FileName;

            }
            else
            {
                MessageBox.Show("加载文件失败！", "错误");
                return;
            }
            var reader = new StreamReader(pathname);
            string buf = reader.ReadLine();
            for (int i = 0; i < 20; i++)
            {
                buf = reader.ReadLine();
                length2 = i;
                if (buf == null) break;
                var arr = buf.Split(',');

                //int.Parse(arr[0]);
                name2[i] = arr[0];
                oldX2[i, 0] = double.Parse(arr[1]);//X
                oldX2[i, 1] = double.Parse(arr[2]);//Y
                oldX2[i, 2] = double.Parse(arr[3]);//Z
            }
            reader.Close();
            label15.Text = "Result is OK! ";
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if(oldX2!=null&&length2!=0)
            {
                label15.Text = "to do...";
                #region 改正数计算
                Matrix Matrix = new Matrix();
                double[,] B = new double[3 * (length+length2), 7]; for (int i = 0; i < 3 * (length+length2); i++) for (int j = 0; j < 7; j++) B[i, j] = 0;
                for (int i = 0; i < length; i++)//B,l
                {
                    B[3 * i, 0] = 1; B[3 * i + 1, 1] = 1; B[3 * i + 2, 2] = 1;
                    B[3 * i, 3] = oldX[i, 0]; B[3 * i + 1, 3] = oldX[i, 1]; B[3 * i + 2, 3] = oldX[i, 2];
                    B[3 * i, 5] = -oldX[i, 2]; B[3 * i + 1, 6] = -oldX[i, 0]; B[3 * i + 2, 4] = -oldX[i, 1];
                    B[3 * i, 6] = oldX[i, 1]; B[3 * i + 1, 4] = oldX[i, 2]; B[3 * i + 2, 5] = oldX[i, 0];
                }
                for(int i=0;i<length2;i++)
                {
                    B[3 * (i + length), 0] = 1; B[3 * (i + length) + 1, 1] = 1; B[3 * (i + length) + 2, 2] = 1;
                    B[3 * (i + length), 3] = oldX2[i, 0]; B[3 * (i + length) + 1, 3] = oldX2[i, 1]; B[3 * (i + length) + 2, 3] = oldX2[i, 2];
                    B[3 * (i + length), 5] = -oldX2[i, 2]; B[3 * (i + length) + 1, 6] = -oldX2[i, 0]; B[3 * (i + length) + 2, 4] = -oldX2[i, 1];
                    B[3 * (i + length), 6] = oldX2[i, 1]; B[3 * (i + length) + 1, 4] = oldX2[i, 2]; B[3 * (i + length) + 2, 5] = oldX2[i, 0];
                }
                double[,] X2 = Matrix.MultiplyMatrix(B, para);
                double[,] V = new double[length + length2, 3];
                double[] P = new double[length];
                double sum;
                double[,] psum=new double[length2,3];
                //计算公共点改正数
                //配置残差
                for(int i=0;i<length;i++)
                {
                    V[i, 0] = newX[i, 0] - X2[3 * i, 0]; V[i, 1] = newX[i, 1] - X2[3 * i + 1, 0]; V[i, 2] = newX[i, 2] - X2[3 * i+2, 0];
                }
                //定权
                
                for(int i=0;i<length2;i++)
                {
                    sum=0;
                    psum[i,0] = 0;psum[i,1]=0;psum[i,2]=0;
                    for(int j=0;j<length;j++)
                    {
                        P[j] = (double)1 / (Math.Pow((X2[3 * j, 0] - X2[(3*length + 3*i), 0]), 2) + Math.Pow((X2[3 * j+1, 0] - X2[3*(length + i)+1, 0]), 2) + Math.Pow((X2[3 * j+2, 0] - X2[3*(length + i)+2, 0]), 2));
                        sum += P[j];
                        psum[i, 0] += P[j] * V[j, 0]; psum[i, 1] += P[j] * V[j, 1]; psum[i, 2] += P[j] * V[j, 2];
                    }
                    V[length + i, 0] = psum[i, 0] / sum; V[length + i, 1] = psum[i, 1] / sum; V[length + i, 2] = psum[i, 2] / sum;
                }
                #endregion
                // "保存为"对话框
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Filter = "文本文件|*.txt";
                // 显示对话框
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    // 文件名
                    string fileName = dialog.FileName;
                    // 创建文件，准备写入
                    FileStream fs = File.Open(fileName, FileMode.Create, FileAccess.Write);
                    StreamWriter wr = new StreamWriter(fs);
                    wr.WriteLine("公共点残差配置后的转换坐标：");
                    wr.WriteLine("点名，X坐标(m)，Y坐标(m)，Z坐标(m)，X改正数(m)，Y改正数(m)，Z改正数(m)");
                    for (int i = 0; i < length;i++)
                    {
                        wr.WriteLine(name[i] + "," + newX[i, 0] + "," + newX[i, 1] + "," + newX[i, 2] + "," + "{0:F4}" + "," + "{1:F4}"+"," +"{2:F4}" , V[i, 0], V[i, 1],V[i, 2]);
                    }
                    for (int i = 0; i < length2;i++ )
                    {
                        wr.WriteLine(name2[i] + ",{0:F4},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4}", (X2[3 * length + 3 * i, 0] + V[length + i, 0]), (X2[3 * length + 3 * i + 1, 0] + V[length + i, 1]), (X2[3 * length + 3 * i + 2, 0] + V[length + i, 2]), V[length + i, 0], V[i + length, 1], V[i + length, 2]);
                    }
                    label15.Text = "Result is OK! ";
                    // 关闭文件
                    wr.Flush();
                    wr.Close();
                    fs.Close();
                }
                else
                {
                    MessageBox.Show("保存文件失败！", "错误");
                    return;
                }
            }
            else
            {
                MessageBox.Show("加载文件失败！", "错误");
                return;
            }
        }
    }
}
