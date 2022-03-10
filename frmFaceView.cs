using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace R100Sample
{
    public partial class frmFaceView : Form
    {
        private string _faceImagesDirectory = "Temp";

        ImageConverter imgcvt = new ImageConverter();

        public frmFaceView()
        {
            InitializeComponent();
        }
       
        public void DisplayFaceImage(Image img)
        {
            picFace.Image = img;
            lblInfo.Text = string.Empty;
        }
        public void DisplayFaceImage(byte[] faceImage)
        {

            picFace.Image = (Image)imgcvt.ConvertFrom(faceImage);

            lblInfo.Text = string.Empty;
        }
       
        private void btnCancel_Click(object sender, EventArgs e)
        {
            picFace.Image = null;
            this.Close();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            //Create directory if not present
            if (!Directory.Exists(_faceImagesDirectory))
                Directory.CreateDirectory(_faceImagesDirectory);

            string fileName = "F_" + Guid.NewGuid() + ".jpeg";

            picFace.Image.Save(_faceImagesDirectory + @"\" + fileName, ImageFormat.Jpeg);

            MessageBox.Show(
                    @"Face image saved successfully." + Environment.NewLine + _faceImagesDirectory + @"\" + fileName,
                    Constants.TITLE, MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

            this.Close();

            return;           
        }
    }
}