using System;
using System.Drawing;

namespace ReadWriteImage2
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(12, 56);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textBox1.Size = new System.Drawing.Size(704, 406);
            this.textBox1.TabIndex = 1;
            this.textBox1.ReadOnly = true;
            this.textBox1.Text =
                  "1. Backup the image folder you want to repair (just in case). " + Environment.NewLine
                + "2. Use the button above to choose the image folder. " + Environment.NewLine
                + "3. Repaired images will be found in the RepairedImages folder" + Environment.NewLine
                + "4. Replace the bad images by copying the Repaired images back to your folder." + Environment.NewLine
                + "5. Remove (or move) the Repaired Images folder elsewhere," + Environment.NewLine
                + "    before loading your main folder into Timelapse, as otherwise they will be included.";
            this.textBox1.SelectionLength = 0;
            this.textBox1.SelectionStart = 0;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(17, 0);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(699, 50);
            this.button1.TabIndex = 2;
            this.button1.Text = "Fix image files";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(742, 480);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.textBox1);
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Read Write Image";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button button1;
    }
}

