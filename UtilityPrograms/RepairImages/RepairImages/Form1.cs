using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ReadWriteImage2
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            string[] files;
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    textBox1.Text = String.Empty;

                    files = Directory.GetFiles(fbd.SelectedPath);
                    string newPath = Path.Combine(fbd.SelectedPath, "RepairedImages");
                    //string newPath = fbd.SelectedPath;
                    if (!Directory.Exists(newPath))
                    {
                        Directory.CreateDirectory(newPath);
                    }
                    foreach (string file in files)
                    {
                        if (Path.GetExtension(file).ToLower() == ".jpg")
                        {
                           // textBox1.Text += file + Environment.NewLine;

                            //read image
                            Bitmap bmp = new Bitmap(file);
                            if (bmp.HorizontalResolution <= 1 || bmp.VerticalResolution <= 1)
                            {
                                // Set the resolution to the highest of the two
                                float resolution = Math.Max(bmp.HorizontalResolution, bmp.VerticalResolution);
                                if (resolution <= 1)
                                {
                                    resolution = 72.0f;
                                }
                                this.textBox1.Text += "Repairing : " + file + Environment.NewLine;

                                bmp.SetResolution(resolution, resolution);

                                // write image
                                string newFilePath = Path.Combine(newPath, Path.GetFileName(file));
                                bmp.Save(newFilePath);
                            }
                            else
                            {
                                this.textBox1.Text += "File is ok:   " + file + Environment.NewLine;
                            }
                        }
                    }
                }
            }
        }
    }
}
