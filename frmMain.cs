using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using R100ManagerSDKLib;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

using System.Threading;

namespace R100Sample
{
    public partial class frmMain : Form
    {
        private readonly CapturedImages _capturedImage;


        private readonly string _enrollmentImagesDirectory = "Enrollment";
        private readonly string _TempImagesDirectory = "Temp";
        private readonly string _faceImagesDirectory = "Face";

        private readonly R100DeviceControl _iCAMR100DeviceControl = new R100DeviceControl();

        
        int m_nWhiteBalance;
        int m_nSaturation;
        int m_nBrightness;
        int m_nISO;
        int m_nSharpness;

        int m_nNumOfUser;

        int m_nEYE = 0;
        int m_nIrisType = -1;
        string m_strUserID;
        int m_nCaptureMode = IS_INIT;

        private delegate void DelProgressbarCtrl(bool bIsRun);
        DelProgressbarCtrl m_ProgressbarCtrl;

        CHostDBCtrl m_HostDBControl;

        public const int NONE = 0;
        public const int COMPARE = 1;
        public const int ALL_INSERT = 2;
        public const int ALL_DELETE = 3;
        public const int FINISH = 4;
        public const int FILE_SAVE = 5;

        public const int IS_INIT = 0;
        public const int IS_ENROLL = 1;
        public const int IS_IDENTIFY = 2;
        public const int IS_VERIFY_BY_ID = 3;
        public const int IS_CAPTURE = 4;
        public const int IS_VERIFY_BY_TEMPLATE = 5;

        
        int m_nStatus = NONE;

        int m_nSelectedRow = 0;

        public byte[] m_pLeftIrisImage;
        public byte[] m_pRightIrisImage;
        public byte[] m_pLeftIrisTemplate;
        public byte[] m_pRightIrisTemplate;

        int m_nColorOffsetX = 0;
        int m_nColorOffsetY = 0;


        private delegate void DelLoadEnrolledUserInfo();
        DelLoadEnrolledUserInfo m_LoadEnrolledUserInfo;


        public frmMain()
        {
            InitializeComponent();


            _iCAMR100DeviceControl.OnEnrollReport += new _IR100DeviceControlEvents_OnEnrollReportEventHandler(OnEnrollReport);
            _iCAMR100DeviceControl.OnGetFaceImage += new _IR100DeviceControlEvents_OnGetFaceImageEventHandler(OnGetFaceImage);
            _iCAMR100DeviceControl.OnGetIrisImage += new _IR100DeviceControlEvents_OnGetIrisImageEventHandler(OnGetIrisImage);
            _iCAMR100DeviceControl.OnGetIrisTemplate += new _IR100DeviceControlEvents_OnGetIrisTemplateEventHandler(OnGetIrisTemplate);
            _iCAMR100DeviceControl.OnGetLiveImage += new _IR100DeviceControlEvents_OnGetLiveImageEventHandler(OnGetLiveImage);
            _iCAMR100DeviceControl.OnGetStatus += new _IR100DeviceControlEvents_OnGetStatusEventHandler(OnGetStatus);
            _iCAMR100DeviceControl.OnMatchReport += new _IR100DeviceControlEvents_OnMatchReportEventHandler(OnMatchReport);
            _iCAMR100DeviceControl.OnCaptureReport += new _IR100DeviceControlEvents_OnCaptureReportEventHandler(OnCaptureReport);
            _iCAMR100DeviceControl.OnUserDB += new _IR100DeviceControlEvents_OnUserDBEventHandler(OnUserDB);


            this._capturedImage = new CapturedImages();
            
            try
            {
                if (!Directory.Exists(_faceImagesDirectory))
                    Directory.CreateDirectory(_faceImagesDirectory);

                if (!Directory.Exists(_TempImagesDirectory))
                    Directory.CreateDirectory(_TempImagesDirectory);

                if (!Directory.Exists(_enrollmentImagesDirectory))
                    Directory.CreateDirectory(_enrollmentImagesDirectory);

                //display sdk version
                string sdkVersion = string.Empty;
                _iCAMR100DeviceControl.GetVersion(Constants.IS_DEV_VER_SDK, out sdkVersion);

                this.Text += "  "+ sdkVersion;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetUIOnDisconnect();

            m_ProgressbarCtrl = new DelProgressbarCtrl(ProgressbarCtrl);
            m_HostDBControl = new CHostDBCtrl();



            m_LoadEnrolledUserInfo = new DelLoadEnrolledUserInfo(LoadEnrolledUserInfo);
                        
        }

        
        private void ProgressbarCtrl(bool bIsRun)
        {
            if (bIsRun)
            {
                prgUpdate.Value = 0;
                prgUpdate.Style = ProgressBarStyle.Marquee;
                prgUpdate.MarqueeAnimationSpeed = 30;
            }else
                prgUpdate.Style = ProgressBarStyle.Blocks;
            

        }
        
        public void ProcessError(int errorCode)
        {
            MessageBox.Show(this, ((Constants.Error)errorCode).ToString() + " (" + errorCode + ")",
                                    Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #region UI Events

        private void btnConnect_Click(object sender, EventArgs e)
        {            
            int nResult;

            nResult = _iCAMR100DeviceControl.Open();


            if (nResult != Constants.IS_ERROR_NONE)
            {
                _iCAMR100DeviceControl.Close();
                ProcessError(nResult);
                return;
            }

            initialize();
        }

        private void initialize()
        {
            int nResult;
            string strValue;
           

            //-----------------------------------------------------------------------------------Version Info
            
            nResult = _iCAMR100DeviceControl.GetVersion(Constants.IS_DEV_VER_ICAMSW, out strValue);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;

            txtVeriCAMSW.Text = strValue;

            //-------------------------------------------------------------------compatibility chek
            
            if (Convert.ToInt16(strValue.Substring(0, 1)) != 4)
            {
                tabCtrlEtc.Enabled = true;
                tabCtrlEtc.SelectedIndex= 2;

                frameLanguageType.Enabled = false;
                frameVolumeControl.Enabled = false;
                frameUserInterface.Enabled = false;
                frameEnrollment.Enabled = false;
                frameUpdateVoiceMessage.Enabled = false;

                frameUpgrade.Enabled = true;

                MessageBox.Show(this, @"Compatibility issues! You should update to use 'Manager SDK'.", Constants.TITLE, MessageBoxButtons.OK,
                                        MessageBoxIcon.Information);

                return;
            }
            else
            {
                frameLanguageType.Enabled = true;
                frameVolumeControl.Enabled = true;
                frameUserInterface.Enabled = true;
                frameEnrollment.Enabled = true;
                frameUpdateVoiceMessage.Enabled = true;

            }
            //-------------------------------------------------------------------------------------


            nResult = _iCAMR100DeviceControl.GetVersion(Constants.IS_DEV_VER_SDK, out strValue);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;

            txtVerSDK.Text = strValue;



            nResult = _iCAMR100DeviceControl.GetVersion(Constants.IS_DEV_VER_FS, out strValue);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;

            txtVerFS.Text = strValue;

            nResult = _iCAMR100DeviceControl.GetVersion(Constants.IS_DEV_VER_ICAM_CMDCENTER, out strValue);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;

            txtVerCMDCenter.Text = strValue;


            nResult = _iCAMR100DeviceControl.GetVersion(Constants.IS_DEV_VER_ICAM_MANAGER, out strValue);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;

            txtVerDeviceMgr.Text = strValue;


            nResult = _iCAMR100DeviceControl.GetVersion(Constants.IS_DEV_VER_OS, out strValue);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;

            txtVerOS.Text = strValue;


            nResult = _iCAMR100DeviceControl.GetVersion(Constants.IS_DEV_VER_LIB_CAPTURE, out strValue);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;

            txtVerlibCapture.Text = strValue;

            nResult = _iCAMR100DeviceControl.GetVersion(Constants.IS_DEV_VER_LIB_EYESEEK, out strValue);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;

            txtVerlibEyeseek.Text = strValue;

            nResult = _iCAMR100DeviceControl.GetVersion(Constants.IS_DEV_VER_LIB_COUNTERMEASURE, out strValue);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;

            txtVerlibCountermeasure.Text = strValue;

            nResult = _iCAMR100DeviceControl.GetVersion(Constants.IS_DEV_VER_LIB_RECOG, out strValue);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;

            txtVerlibRecog.Text = strValue;

            nResult = _iCAMR100DeviceControl.GetVersion(Constants.IS_DEV_VER_LIB_LENS, out strValue);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;
            
            txtVerlibLens.Text = strValue;

            nResult = _iCAMR100DeviceControl.GetVersion(Constants.IS_DEV_VER_LIB_TWOPI, out strValue);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;
            
            txtVerlibTwoPi.Text = strValue;


            nResult = _iCAMR100DeviceControl.GetColorOffset(out m_nColorOffsetX, out m_nColorOffsetY);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;

            txtColorOffset.Text = "( " + m_nColorOffsetX + ", " + m_nColorOffsetY + " )";
            

            nResult = _iCAMR100DeviceControl.GetSerialNumber(out strValue);

            if (nResult != Constants.IS_ERROR_NONE)
                strValue = "Failure Code : " + nResult;
            else
            {
                nResult = m_HostDBControl.Open(strValue);

                if( nResult != Constants.IS_ERROR_NONE)
                    MessageBox.Show(this, @"There is a problem using the database.", Constants.TITLE, MessageBoxButtons.OK,
                                       MessageBoxIcon.Information);

            }

            txtSerialNumber.Text = strValue;
            
            

            //-----------------------------------------------------------------------------------Volume Control

            int nVolume;

            nResult = _iCAMR100DeviceControl.GetSoundVolume(out nVolume);

            if (nResult == Constants.IS_ERROR_NONE)
                trackBarVolume.Value = nVolume;


            int nLanguage;

            nResult = _iCAMR100DeviceControl.GetVoiceLanguage(out nLanguage);

            if (nResult == Constants.IS_ERROR_NONE)
                cmbLanguage.SelectedIndex = nLanguage;

            nResult = _iCAMR100DeviceControl.DownloadUserDB(out m_nNumOfUser);

            if (nResult == Constants.IS_ERROR_NONE)
            {
                if (m_nNumOfUser > 0)
                    m_nStatus = COMPARE;
                else
                    m_nStatus = ALL_DELETE;
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            int nResult;

            nResult = _iCAMR100DeviceControl.Close();

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }

            SetUIOnDisconnect();
        }

        private void btnEnroll_Click(object sender, EventArgs e)
        {

            int nResult = 0;
            int nWhichEye = 0;
            int nCounterMeasureLevel = (cmbEnrollCounterMeasureLevel.SelectedIndex == 0 ? Constants.IS_FED_LEVEL_1 : Constants.IS_FED_LEVEL_2);
            int nLensDetectionLevel = cmbEnrollLensDetection.SelectedIndex;
            int nIsLive = (chkEnrollLiveImage.Checked ? Constants.IS_ENABLE : Constants.IS_DISABLE);
            int nIsAuditFace = (chkEnrollAuditFace.Checked ? Constants.IS_FACE_AUDIT_ON : Constants.IS_FACE_AUDIT_OFF);
            int nIsRetry = (chkEnrollRetry.Checked ? Constants.IS_ENABLE : Constants.IS_DISABLE);
            int nIsVerify = (chkEnrollVerify.Checked ? Constants.IS_ENABLE : Constants.IS_DISABLE);
            int nTimeOut = 0;

            if (txtEnrollTimeOut.Text.Length <= 0)
            {
                MessageBox.Show(this, " Check the 'Time Out'.",
                                    Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }

            nTimeOut = Convert.ToInt32(txtEnrollTimeOut.Text);

            switch (cmbEnrollWhichEye.SelectedIndex)
            {
                case 0:
                    nWhichEye = Constants.IS_EYE_RIGHT;
                    break;
                case 1:
                    nWhichEye = Constants.IS_EYE_LEFT;
                    break;
                case 2:
                    nWhichEye = Constants.IS_EYE_BOTH;
                    break;
            }

            if (txtEnrollUserID.Text.Length <= 0 || txtEnrollUserID.Text.Length > 40)
            {

                MessageBox.Show(this, " Check the 'User ID'.",
                                    Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }


            nResult = _iCAMR100DeviceControl.EnrollUser(txtEnrollUserID.Text, nWhichEye, nCounterMeasureLevel, nLensDetectionLevel, nTimeOut, nIsAuditFace, nIsLive, nIsRetry, nIsVerify);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }

            m_strUserID = txtEnrollUserID.Text;

            m_nCaptureMode = IS_ENROLL;
            initFrameIrisCamera(false);


            if (nIsLive == Constants.IS_ENABLE)
            {
                pnlAlign.Visible = true;
            }

            _iCAMR100DeviceControl.ControlIndicator(Constants.IS_SND_CENTER_EYES_IN_MIRROR, Constants.IS_IND_NONE);

        }

        private void initFrameIrisCamera(bool bIsEnable)
        {
            switch (m_nCaptureMode)
            {
                case IS_ENROLL:
                    cmbEnrollWhichEye.Enabled = bIsEnable;
                    cmbEnrollCounterMeasureLevel.Enabled = bIsEnable;
                    cmbEnrollLensDetection.Enabled = bIsEnable;

                    chkEnrollAuditFace.Enabled = bIsEnable;
                    chkEnrollLiveImage.Enabled = bIsEnable;
                    chkEnrollRetry.Enabled = bIsEnable;
                    chkEnrollVerify.Enabled = bIsEnable;

                    txtEnrollTimeOut.Enabled = bIsEnable;
                    txtEnrollUserID.Enabled = bIsEnable;

                    btnEnroll.Enabled = bIsEnable;
                    btnEnrollAbort.Enabled = !bIsEnable;

                    break;
                case IS_IDENTIFY:
                    cmbIdentifyWhichEye.Enabled = bIsEnable;
                    cmbIdentifyCounterMeasureLevel.Enabled = bIsEnable;
                    cmbIdentifyLensDetection.Enabled = bIsEnable;


                    chkIdentifyAuditFace.Enabled = bIsEnable;
                    chkIdentifyLiveImage.Enabled = bIsEnable;

                    txtIdentifyTimeOut.Enabled = bIsEnable;


                    btnIdentify.Enabled = bIsEnable;
                    btnIdentifyAbort.Enabled = !bIsEnable;
                    break;
                case IS_VERIFY_BY_ID:
                    cmbVerifyWhichEye.Enabled = bIsEnable;
                    cmbVerifyCounterMeasureLevel.Enabled = bIsEnable;
                    cmbVerifyLensDetection.Enabled = bIsEnable;

                    chkVerifyAuditFace.Enabled = bIsEnable;
                    chkVerifyLiveImage.Enabled = bIsEnable;
                    
                    txtVerifyTimeOut.Enabled = bIsEnable;
                    txtVerifyUserID.Enabled = bIsEnable;

                    btnVerify.Enabled = bIsEnable;
                    btnVerifyAbort.Enabled = !bIsEnable;
                    break;
                
                case IS_CAPTURE:
                    cmbCapturePurpose.Enabled = bIsEnable;
                    cmbCaptureIrisType.Enabled = bIsEnable;
                    cmbCaptureWhichEye.Enabled = bIsEnable;
                    cmbCaptureCounterMeasureLevel.Enabled = bIsEnable;
                    cmbCaptureLensDetection.Enabled = bIsEnable;
                    
                    chkCaptureAuditFace.Enabled = bIsEnable;
                    chkCaptureLiveImage.Enabled = bIsEnable;
                    
                    txtCaptureTimeOut.Enabled = bIsEnable;
                    txtCaptureUserID.Enabled = bIsEnable;
                    
                    btnStartIrisCapture.Enabled = bIsEnable;
                    btnCaptureAbort.Enabled = !bIsEnable;

                    btnAddIrisImage.Enabled = false;
                    btnAddIrisTemplate.Enabled = false;
                    btnVerifyByTemplate.Enabled = false;

                    break;
                case IS_VERIFY_BY_TEMPLATE:
                    cmbCapturePurpose.Enabled = bIsEnable;
                    cmbCaptureIrisType.Enabled = bIsEnable;
                    cmbCaptureWhichEye.Enabled = bIsEnable;
                    cmbCaptureCounterMeasureLevel.Enabled = bIsEnable;
                    cmbCaptureLensDetection.Enabled = bIsEnable;

                    chkCaptureAuditFace.Enabled = bIsEnable;
                    chkCaptureLiveImage.Enabled = bIsEnable;

                    txtCaptureTimeOut.Enabled = bIsEnable;
                    txtCaptureUserID.Enabled = bIsEnable;

                    btnStartIrisCapture.Enabled = bIsEnable;
                    btnCaptureAbort.Enabled = !bIsEnable;

                    break;
            }
            
            if (!bIsEnable)
            {
                labQuality.Hide();
                prgRQuality.Hide();
                prgLQuality.Hide();

                prgRQuality.Value = 0;
                prgLQuality.Value = 0;
                labRightQualityValue.Text = string.Empty;
                labLeftQualityValue.Text = string.Empty;

                labRightCountermeasure.Text = string.Empty;
                labLeftCountermeasure.Text = string.Empty;

                labRightLensStatus.Text = string.Empty;
                labLeftLensStatus.Text = string.Empty;

                labEnrollResult.Text = string.Empty;
                labIdentifyResult.Text = string.Empty;
                labVerifyResult.Text = string.Empty;
                labCaptureResult.Text = string.Empty;

                picRightEye.Image = picLeftEye.Image = picLiveFace.Image = null;
                _capturedImage.RawRightIris = _capturedImage.RawLeftIris = _capturedImage.FaceImage = null;
                btnSaveIrisImages.Enabled = false;

                btnFaceCaptureStart.Enabled = true;
                btnFaceCaptureStop.Enabled = false;
                btnFaceCaptureAbort.Enabled = false;
                btnAF.Enabled = false;

                initFrameUserInfo();
            }
            else
            {
                if (pnlAlign.Visible)
                    pnlAlign.Visible = false;

                 picLiveFace.Image = null;
            }
            
        }


        //private void initFrameIrisCamera()
        //{
        //    txtEnrollUserID.Enabled = true;
        //    cmbEnrollWhichEye.Enabled = true;
        //    cmbEnrollCounterMeasureLevel.Enabled = true;
        //    cmbEnrollLensDetection.Enabled = true;


        //    chkEnrollAuditFace.Enabled = true;
        //    chkEnrollLiveImage.Enabled = true;
        //    chkEnrollRetry.Enabled = true;

        //    btnEnroll.Enabled = true;
        //    btnIdentify.Enabled = true;
        //    btnVerify.Enabled = true;
        //    btnStartIrisCapture.Enabled = true;

        //    btnEnrollAbort.Enabled = false;
        //    btnAddIrisImage.Enabled = false;
        //    btnAddIrisTemplate.Enabled = false;
        //}


        private void initFrameUserInfo()
        {
            txtUser_ID.Text = string.Empty;
            txtUserName.Text = string.Empty;
            txtCardID.Text = string.Empty;
            txtCardName.Text = string.Empty;
            txtInsertDate.Text = string.Empty;

            labRQuality.Text = string.Empty;
            labLQuality.Text = string.Empty;

            picEnrolledREye.Image = picEnrolledLEye.Image = picEnrolledAudit.Image = null;
            _capturedImage.FaceImage = null;
        }

        private void btnPlayVoiceMessages_Click(object sender, EventArgs e)
        {
            int nResult;

            int nVoiceMessage = cmbVoiceMessages.SelectedIndex;


            nResult = _iCAMR100DeviceControl.ControlIndicator(nVoiceMessage, Constants.IS_IND_NONE);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }
        }

        private void btnPlayLEDMessages_Click(object sender, EventArgs e)
        {
            int nResult;

            int nIndicatorType = cmbLED.SelectedIndex;


            nResult = _iCAMR100DeviceControl.ControlIndicator(Constants.IS_SND_NONE, nIndicatorType);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }
        }


        private void chkMute_CheckedChanged(object sender, EventArgs e)
        {
            int nResult;
            int nVolume;
            bool bIsEnabled;
            
            if (chkMute.Checked)
            {
                nVolume = 0;
                bIsEnabled = false;
            }
            else
            {
                nVolume = trackBarVolume.Value;
                bIsEnabled = true;
            }

            
            nResult = _iCAMR100DeviceControl.SetSoundVolume(nVolume);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }


             trackBarVolume.Enabled  = bIsEnabled;
        }

        private int SaveIrisImage(string strDirectory, string strFileName, byte[] pBuff, out string strPath)
        {
            strPath = string.Empty;

            if (!Directory.Exists(strDirectory))
                Directory.CreateDirectory(strDirectory);


            if (pBuff != null && pBuff.Length != 0)
            {
                Image imageIris = Helper.RawToBitmap(pBuff, 640, 480, PixelFormat.Format8bppIndexed);
                strPath = strDirectory + @"\" + strFileName + ".bmp";

                imageIris.Save(strPath, ImageFormat.Bmp);

                return Constants.IS_RST_SUCCESS;
            }

            return Constants.IS_RST_FAILURE;
        }
        private void btnSaveIrisImages_Click(object sender, EventArgs e)
        {
            if (_capturedImage.RawRightIris == null && _capturedImage.RawLeftIris == null)
            {
                MessageBox.Show(@"Nothing to save.", Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string msgDisplay = string.Empty;
            if ((_capturedImage.RawRightIris != null && _capturedImage.RawRightIris.Length != 0) ||
                (_capturedImage.RawLeftIris != null && _capturedImage.RawLeftIris.Length != 0))
            {
                string guid = Guid.NewGuid().ToString();

                ////Create directory if not present
                if (!Directory.Exists(_TempImagesDirectory))
                    Directory.CreateDirectory(_TempImagesDirectory);


                if (_capturedImage.RawRightIris != null && _capturedImage.RawRightIris.Length != 0)
                {
                    Image imageRightIris = Helper.RawToBitmap(_capturedImage.RawRightIris, 640, 480, PixelFormat.Format8bppIndexed );
                    imageRightIris.Save(_TempImagesDirectory + @"\" + "R_" + guid + ".bmp", ImageFormat.Bmp);

                    msgDisplay = _TempImagesDirectory + @"\" + "R_" + guid + ".bmp";
                }
                
                if (_capturedImage.RawLeftIris != null && _capturedImage.RawLeftIris.Length != 0)
                {
                    Image imageLeftIris = Helper.RawToBitmap(_capturedImage.RawLeftIris, 640, 480, PixelFormat.Format8bppIndexed);
                    imageLeftIris.Save(_TempImagesDirectory + @"\" + "L_" + guid + ".bmp", ImageFormat.Bmp);

                    msgDisplay += Environment.NewLine + _TempImagesDirectory + @"\" + "L_" + guid + ".bmp";
                }
            }

            MessageBox.Show(@"Iris image(s) saved successfully." + Environment.NewLine + msgDisplay, Constants.TITLE,
                            MessageBoxButtons.OK, MessageBoxIcon.Information);

            btnSaveIrisImages.Enabled = false;
        }

        private void trackBarVolume_Scroll(object sender, EventArgs e)
        {
            if (chkMute.Checked) return;

            int nResult;

            nResult =  _iCAMR100DeviceControl.SetSoundVolume(trackBarVolume.Value);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }
        }

        #endregion UI Events

        #region Device Control Events


        private void OnGetStatus(int nStatusType, int nStatusValue)
        {
            Console.WriteLine(" nStatusType" + nStatusType + "nStatusValue" + nStatusValue);

            switch (nStatusType)
            {
                case Constants.IS_STAT_DISCONNECT:
                    {
                        MessageBox.Show(this, @"iCAM got disconnected.", Constants.TITLE, MessageBoxButtons.OK,
                                        MessageBoxIcon.Information);

                        SetUIOnDisconnect();

                        break;
                    }
                case Constants.IS_STAT_UPGRADE_STATUS:

                   
                    this.Invoke(m_ProgressbarCtrl, false);


                    MessageBox.Show(this, @"Upgrade file transfer has been completed. Please wait while the device reboot.", Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);

                    if (nStatusValue != 0)
                    {
                        ProcessError(nStatusValue);
                    }

                    break;
                case Constants.IS_STAT_UPSIDEDOWN:

                    if (nStatusValue == Constants.IS_NORMAL)
                        txtUpsideDown.Text = "Normal";
                    else if (nStatusValue == Constants.IS_UPSIDEDOWN)
                        txtUpsideDown.Text = "Up-side down";
                    else
                        txtUpsideDown.Text = nStatusValue.ToString();


                    break;
                default:
                    {
                        MessageBox.Show(@"Unknown status [TYPE=" + nStatusValue + @" VALUE=" + nStatusValue + @"]");
                        break;
                    }
            }
        }
        ImageConverter imgcvt = new ImageConverter();

        private void OnGetLiveImage(int nImageSize, object objLiveImage)
        {
            if (picboxAlign.Visible)
            {
                createStaticProcessedImage(objLiveImage);
            }

            picLiveFace.Image = (Image)imgcvt.ConvertFrom(objLiveImage);
        }

        private void createStaticProcessedImage(object theImage)
        {
            int nX = 0;
            int nY = 0;
            Image whiteBackground = Properties.Resources.whiteBackground;
            
            Image dotImage = Properties.Resources.orangeDot;
            Image tImage = (Image)imgcvt.ConvertFrom(theImage);

            
            var bitmap = new Bitmap(whiteBackground, new Size(picboxAlign.Width, picboxAlign.Height));

            using (var canvas = Graphics.FromImage(bitmap))
            {
                canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;

                nX = m_nColorOffsetX - (picboxAlign.Width / 2);

                if (nX < 0)
                    nX = 0;
                else if( 640 < (nX + picboxAlign.Width))
                    nX = 480 - picboxAlign.Width;


                nY = m_nColorOffsetY - (picboxAlign.Height / 2);

                if (nY < 0)
                    nY = 0;
                else if ( 640 < (nY + picboxAlign.Height))
                    nY = 640 - picboxAlign.Height;




                canvas.DrawImage(tImage,
                       new Rectangle(0, 0, picboxAlign.Width, picboxAlign.Height),
                       new Rectangle(nX, nY, picboxAlign.Width, picboxAlign.Height),
                       GraphicsUnit.Pixel);

                
                canvas.DrawImage(dotImage,
                         new Rectangle( ((picboxAlign.Width/2) - 13),
                                      ((picboxAlign.Height / 2) - 13),
                                     25, 25),
                         new Rectangle(0, 0, dotImage.Width, dotImage.Height),
                         GraphicsUnit.Pixel);
                
                canvas.Save();
            }

            picboxAlign.Image = bitmap;
        }

        private void OnEnrollReport(int nReportResult, int nFailureCode, int nRightIrisQualityValue, int nLeftIrisQualityValue, string strMatchedUserID)
        {
            try
            {
                int nResult;
                stUSERINFO stEnrolledUserInfo;
                string[] strUserInfo;
                string strRPath = string.Empty;
                string strLPath = string.Empty;
                string strFacePath = string.Empty;


                //initFrameIrisCamera();

                labEnrollResult.Text = string.Empty;

                labQuality.Show();

                if (nRightIrisQualityValue != 0)
                {
                    prgRQuality.Show();
                    prgRQuality.Value = nRightIrisQualityValue;
                    labRightQualityValue.Text = nRightIrisQualityValue.ToString();
                }

                if (nLeftIrisQualityValue != 0)
                {
                    prgLQuality.Show();
                    prgLQuality.Value = nLeftIrisQualityValue;
                    labLeftQualityValue.Text = nLeftIrisQualityValue.ToString();
                }
                if (nReportResult == Constants.IS_RST_SUCCESS)
                {

                    labEnrollResult.Text = "[OnEnrollReport]\n  nReportResult : Success\n";

                    _iCAMR100DeviceControl.ControlIndicator(Constants.IS_SND_FINISH_IRIS_CAPTURE, Constants.IS_IND_SUCCESS);

                    stEnrolledUserInfo = new stUSERINFO();

                    nResult = _iCAMR100DeviceControl.GetUserInfo(m_strUserID, out stEnrolledUserInfo);

                    if (nResult == Constants.IS_ERROR_NONE)
                    {
                        string guid = Guid.NewGuid().ToString();

                        if (m_nEYE != Constants.IS_EYE_LEFT)
                        {
                            nResult = SaveIrisImage(_enrollmentImagesDirectory, "R" + m_strUserID + "_" + guid, _capturedImage.RawRightIris, out strRPath);

                            if (nResult != Constants.IS_RST_SUCCESS)
                            {
                                ProcessError(nResult);
                                return;
                            }
                        }
                        if (m_nEYE != Constants.IS_EYE_RIGHT)
                        {
                            SaveIrisImage(_enrollmentImagesDirectory, "L" + m_strUserID + "_" + guid, _capturedImage.RawLeftIris, out strLPath);

                            if (nResult != Constants.IS_RST_SUCCESS)
                            {
                                ProcessError(nResult);
                                return;
                            }
                        }

                        if (_capturedImage.FaceImage != null && _capturedImage.FaceImage.Length != 0)
                        {
                            strFacePath = Path.Combine(_faceImagesDirectory, "F" + m_strUserID + "_" + guid + ".jpeg");

                            Helper.ByteArrayToFile(_faceImagesDirectory, "F" + m_strUserID + "_" + guid + ".jpeg", _capturedImage.FaceImage);
                        }

                        if (m_HostDBControl.IsAvailable())
                        {
                            m_HostDBControl.InsertUserInfo(m_strUserID, nRightIrisQualityValue, nLeftIrisQualityValue, strFacePath, strRPath, strLPath, ConvertToString(stEnrolledUserInfo.pInsertDate), ConvertToString(stEnrolledUserInfo.pUpdateDate));

                            m_HostDBControl.SelectEnrolledUserInfo(m_strUserID, out strUserInfo);

                            ListViewItem item = new ListViewItem(strUserInfo[0]);
                            item.SubItems.Add(strUserInfo[1]);
                            item.SubItems.Add(strUserInfo[2]);


                            lstEnrolledUserInfo.Items.Add(item);

                            txtUser_ID.Text = strUserInfo[1];

                            labRQuality.Text = strUserInfo[6];
                            labLQuality.Text = strUserInfo[7];

                            txtInsertDate.Text = strUserInfo[10];

                            picEnrolledAudit.ImageLocation = strUserInfo[3];
                            picEnrolledREye.ImageLocation = strUserInfo[4];
                            picEnrolledLEye.ImageLocation = strUserInfo[5];
                            
                        }
                        
                    }
                    else
                    {
                        ProcessError(nResult);
                    }

                }
                else
                {
                    labEnrollResult.Text += "[OnEnrollReport]\n  FailureCode : " + ((Constants.Error)nFailureCode).ToString() + "\n";

                    if (nFailureCode == Constants.IS_FAIL_ALREADY_EXIST)
                    {
                        labEnrollResult.Text += "  Already exist user. (User ID : " + strMatchedUserID + ")\n";
                    }


                    _iCAMR100DeviceControl.ControlIndicator(Constants.IS_SND_NONE, Constants.IS_IND_FAILURE);
                    
                    
                }

            }
            finally
            {
                if (nReportResult != Constants.IS_RST_FAIL_STATUS)
                    initFrameIrisCamera(true);
            }
            

        }

        private void OnMatchReport(int nMatchType, int nReportResult, int nFailureCode, string strMatchedUserID)
        {
            Console.WriteLine("OnMatchReport");
            try
            {
                int nResult;

                string[] strUserInfo;

                //initFrameIrisCamera();

                if (nReportResult == Constants.IS_ERROR_NONE)
                {
                    if (nMatchType == Constants.IS_REP_IDENTIFY)
                    {
                        labIdentifyResult.Text = "[OnMatchReport]\n  nReportResult : Success\n";
                    }
                    else if (nMatchType == Constants.IS_REP_VERIFY_ID)
                    {
                        labVerifyResult.Text = "[OnMatchReport]\n  nReportResult : Success\n";
                    }
                    else if (nMatchType == Constants.IS_REP_VERIFY_TEMPLATE)
                    {
                        labCaptureResult.Text = "[OnMatchReport]\n  nReportResult : Success\n";
                    }

                    _iCAMR100DeviceControl.ControlIndicator(Constants.IS_SND_IDENTIFIED, Constants.IS_IND_SUCCESS);

                    if (nMatchType != Constants.IS_REP_VERIFY_TEMPLATE)
                    {
                        nResult = m_HostDBControl.SelectEnrolledUserInfo(strMatchedUserID, out strUserInfo);

                        if (nResult == Constants.IS_ERROR_NONE)
                        {

                            txtUser_ID.Text = strUserInfo[1];
                            txtUserName.Text = strUserInfo[2];
                            txtCardName.Text = strUserInfo[8];
                            txtCardID.Text = strUserInfo[9];


                            labRQuality.Text = strUserInfo[6];
                            labLQuality.Text = strUserInfo[7];

                            txtInsertDate.Text = strUserInfo[10];

                            picEnrolledAudit.ImageLocation = strUserInfo[3];
                            picEnrolledREye.ImageLocation = strUserInfo[4];
                            picEnrolledLEye.ImageLocation = strUserInfo[5];
                        }
                    }


                }
                else
                {
                    if (nMatchType == Constants.IS_REP_IDENTIFY)
                    {
                        labIdentifyResult.Text += "[OnMatchReport]\n  FailureCode : " + ((Constants.Error)nFailureCode).ToString() + "\n";
                    }
                    else if (nMatchType == Constants.IS_REP_VERIFY_ID)
                    {
                        labVerifyResult.Text += "[OnMatchReport]\n  FailureCode : " + ((Constants.Error)nFailureCode).ToString() + "\n";
                    }
                    else if (nMatchType == Constants.IS_REP_VERIFY_TEMPLATE)
                    {
                        labCaptureResult.Text += "[OnMatchReport]\n  FailureCode : " + ((Constants.Error)nFailureCode).ToString() + "\n";
                    }
                    

                    if (nFailureCode != Constants.IS_FAIL_ABORT && nFailureCode != Constants.IS_FAIL_TIMEOUT)
                    {
                        _iCAMR100DeviceControl.ControlIndicator(Constants.IS_SND_NOT_IDENTIFY, Constants.IS_IND_FAILURE);
                    }
                                                            
                    //ProcessError(nFailureCode);

                }

            }
            finally
            {
                initFrameIrisCamera(true);
            }
        }

        private void OnCaptureReport(int nReportResult, int nFailureCode)
        {
            Console.WriteLine("OnCaptureReport");

            labCaptureResult.Text = string.Empty;

            m_nCaptureMode = IS_CAPTURE;
            initFrameIrisCamera(true);

            if (nReportResult == Constants.IS_ERROR_NONE)
            {
                labCaptureResult.Text = "[OnCaptureReport]\n  nReportResult : Success\n";
                    
                if(m_nIrisType == Constants.IS_IRIS_IMAGE)
                {
                    btnAddIrisImage.Enabled = true;
                }
                else if( m_nIrisType == Constants.IS_IRIS_TEMPLATE)
                {
                    btnAddIrisTemplate.Enabled = true;
                    btnVerifyByTemplate.Enabled = true;
                }

            }
            else
                labCaptureResult.Text += "[OnCaptureReport]\n  FailureCode : " + ((Constants.Error)nFailureCode).ToString() + "\n";

           
        }


        private void OnUserDB(int nNumOfUser, int nSizeOfUserDB, object pUserDB)
        {
            int nResult;

            Console.WriteLine("OnUserDB ");
            
            if (m_nStatus == FILE_SAVE)
            {
                if (nNumOfUser == 0)
                {
                    MessageBox.Show("Database is empty!", Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    if (Helper.ByteArrayToFile(".", "DB.dat", (byte[])pUserDB))
                    {
                        if (new FileInfo("DB.dat").Exists)
                            btnUpload.Enabled = true;
                        else
                            btnUpload.Enabled = false;

                        if (MessageBox.Show("Download complete.( DB.dat ) \n Do you want synchronization?", Constants.TITLE, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            nResult = _iCAMR100DeviceControl.DownloadUserDB(out m_nNumOfUser);

                            if (nResult == Constants.IS_ERROR_NONE)
                            {
                                if (m_nNumOfUser > 0)
                                    m_nStatus = COMPARE;
                                else
                                    m_nStatus = ALL_DELETE;
                            }
                        }
                    }
                }


            }
            else
            {
                stUSERINFO st;
                string[,] strUserDB;
                int nSize;
                int nNumOfUser_Device = nNumOfUser;
                st = default(stUSERINFO);
                nSize = Marshal.SizeOf(st);


                strUserDB = new string[nNumOfUser_Device, 3];

                int dw = System.Environment.TickCount; //tick

                for (int i = 0; i < nNumOfUser_Device; i++)
                {
                    //    ListViewItem item;

                    IntPtr iPtr = Marshal.AllocHGlobal(nSize);
                    Marshal.Copy((byte[])pUserDB, i * nSize, iPtr, nSize);

                    st = (stUSERINFO)Marshal.PtrToStructure(iPtr, typeof(stUSERINFO));


                    strUserDB[i, 0] = ConvertToString(st.pID);
                    strUserDB[i, 1] = ConvertToString(st.pInsertDate);
                    strUserDB[i, 2] = ConvertToString(st.pUpdateDate);

                }


                if (!m_HostDBControl.IsAvailable())
                {
                    SetUIOnConnect();
                    return;
                }

                bool[] bIsExist_Host;
                bool[] bIsExist_Device;

                if (m_nStatus == COMPARE)
                {
                    if (m_HostDBControl.IsNewDB())
                        m_nStatus = ALL_INSERT;
                    else
                    {

                        int nNumOfUser_Host;
                        string[,] strUserDB_Host;

                        nResult = m_HostDBControl.LoadEnrolledUserID(out nNumOfUser_Host, out strUserDB_Host);

                        if (nNumOfUser_Host == 0)
                        {
                            m_nStatus = ALL_INSERT;
                        }
                        else
                        {
                            int nInsertUser = 0;
                            string[,] strInsertUserInfo = new string[nNumOfUser, 3];

                            bIsExist_Host = new bool[nNumOfUser_Host];
                            bIsExist_Device = new bool[nNumOfUser];

                            for (int i = 0; i < nNumOfUser; i++)
                            {
                                for (int j = 0; j < nNumOfUser_Host; j++)
                                {
                                    if ((strUserDB[i, 0] == strUserDB_Host[j, 0]))
                                    {
                                        if ((strUserDB[i, 1] == strUserDB_Host[j, 1]))
                                        {
                                            bIsExist_Device[i] = true;
                                            bIsExist_Host[j] = true;

                                            break;
                                        }
                                    }
                                }

                                if (bIsExist_Device[i] == false)
                                {
                                    strInsertUserInfo[nInsertUser, 0] = strUserDB[i, 0];
                                    strInsertUserInfo[nInsertUser, 1] = strUserDB[i, 1];
                                    strInsertUserInfo[nInsertUser, 2] = strUserDB[i, 2];
                                    nInsertUser++;
                                }
                            }
                            

                            int nCheck = 0;

                            for (int i = 0; i < nNumOfUser_Host; i++)
                            {
                                if (bIsExist_Host[i] == false)
                                {
                                    nCheck++;
                                    m_HostDBControl.DeleteUserInfo(strUserDB_Host[i, 0]);
                                }
                            }


                            if (nInsertUser > 0)
                            {
                                m_HostDBControl.InsertDownloadedghadoUserInfo(nInsertUser, strInsertUserInfo);
                            }
                            
                            m_nStatus = FINISH;

                        }

                    }

                    if (m_nStatus == ALL_INSERT)
                    {
                        m_HostDBControl.InsertDownloadedghadoUserInfo(nNumOfUser, strUserDB);
                        m_nStatus = FINISH;
                    }
                }
                else if (m_nStatus == ALL_DELETE)
                {
                    if (!m_HostDBControl.IsNewDB())
                    {
                        m_HostDBControl.DeleteAllUserInfo();

                    }

                    m_nStatus = FINISH;

                }

                LoadEnrolledUserInfo();

                SetUIOnConnect();

            }

        }

        private void LoadEnrolledUserInfo()
        {
            ListViewItem item;
            int nNumOfUser;
            string [,] strUserDB;

            initFrameUserInfo();
            lstEnrolledUserInfo.Items.Clear();


            int nResult = m_HostDBControl.LoadEnrolledUserInfo(out nNumOfUser, out strUserDB);

            if (nResult == Constants.IS_ERROR_NONE)
            {
                for (int i = 0; i < nNumOfUser; i++)
                {

                    item = new ListViewItem(strUserDB[i, 0]);
                    item.SubItems.Add(strUserDB[i, 1]);
                    item.SubItems.Add(strUserDB[i, 2]);


                    lstEnrolledUserInfo.Items.Add(item);

                }
            }

        }
        
        private void OnGetFaceImage(int nImageType, int nImageResolution, int nImageSize,object objFaceImage)
        {

            if (nImageType == Constants.IS_FACE_CAPTRUE)
            {
                if (nImageResolution == Constants.IS_ERROR_ICAM)
                {
                    MessageBox.Show("[OnGetFaceImage] Error");
                    return;
                }

                _iCAMR100DeviceControl.ControlIndicator(Constants.IS_SND_CAMERA_SHUTTER, Constants.IS_IND_NONE);


                _capturedImage.FaceImage = (byte[])objFaceImage;
                picEnrolledAudit.Image = (Image)imgcvt.ConvertFrom(objFaceImage);

                

            }
            else if (nImageType == Constants.IS_FACE_AUDIT)
            {
                _capturedImage.FaceImage = (byte[])objFaceImage;
                


                if (_capturedImage.FaceImage != null)
                {
                    frmFaceView faceView = new frmFaceView();

                    faceView.DisplayFaceImage((byte[])_capturedImage.FaceImage);
                    faceView.ShowDialog(this);
                }

            }
            
            picLiveFace.Image = null;

        }


        private void OnGetIrisTemplate(int nRightIrisFEDStatus, int nRightIrisLensStatus, int nRightIrisTemplateSize, object objRightIrisTemplate, int nLeftIrisFEDStatus, int nLeftIrisLensStatus, int nLeftIrisTemplateSize, object objLeftIrisTemplate)
        {
            Console.WriteLine("OnGetIrisTemplate");
            try
            {
                if (nRightIrisTemplateSize != 0)
                {

                    switch (nRightIrisFEDStatus)
                    {
                        case Constants.IS_IRIS_STAT_NONE:
                            labRightCountermeasure.Text = "None";
                            break;
                        case Constants.IS_IRIS_STAT_LIVE:
                            labRightCountermeasure.Text = "Live";
                            break;
                        case Constants.IS_IRIS_STAT_FAKE:
                            labRightCountermeasure.Text = "Fake";
                            break;
                        case Constants.IS_IRIS_STAT_FAIL:
                            labRightCountermeasure.Text = "Fail";
                            break;
                        default:
                            labRightCountermeasure.Text = nRightIrisFEDStatus.ToString();
                            break;
                    }

                    switch (nRightIrisLensStatus)
                    {
                        case Constants.IS_LENS_STAT_NONE:
                            labRightLensStatus.Text = "None";
                            break;
                        case Constants.IS_LENS_STAT_HARD:
                            labRightLensStatus.Text = "Hard";
                            break;
                        case Constants.IS_LENS_STAT_PATTERN:
                            labRightLensStatus.Text = "Pattern";
                            break;
                        default:
                            labRightLensStatus.Text = nRightIrisLensStatus.ToString();
                            break;
                    }
                }

                if (nLeftIrisTemplateSize != 0)
                {
                    switch (nLeftIrisFEDStatus)
                    {
                        case Constants.IS_IRIS_STAT_NONE:
                            labLeftCountermeasure.Text = "None";
                            break;
                        case Constants.IS_IRIS_STAT_LIVE:
                            labLeftCountermeasure.Text = "Live";
                            break;
                        case Constants.IS_IRIS_STAT_FAKE:
                            labLeftCountermeasure.Text = "Fake";
                            break;
                        case Constants.IS_IRIS_STAT_FAIL:
                            labLeftCountermeasure.Text = "Fail";
                            break;
                        default:
                            labLeftCountermeasure.Text = nLeftIrisFEDStatus.ToString();
                            break;
                    }

                    switch (nLeftIrisLensStatus)
                    {
                        case Constants.IS_LENS_STAT_NONE:
                            labLeftLensStatus.Text = "None";
                            break;
                        case Constants.IS_LENS_STAT_HARD:
                            labLeftLensStatus.Text = "Hard";
                            break;
                        case Constants.IS_LENS_STAT_PATTERN:
                            labLeftLensStatus.Text = "Pattern";
                            break;
                        default:
                            labLeftLensStatus.Text = nRightIrisLensStatus.ToString();
                            break;
                    }
                }

            }
            finally
            {


                //cmbCounterMeasureLevel.Enabled = true;

                //chkAuditFace.Enabled = true;
                //chkLiveImage.Enabled = true;
                //cmbWhichEye.Enabled = true;

                //cmbLensDetection.Enabled = true;


                initFrameIrisCamera(true);

                if (m_nCaptureMode == IS_CAPTURE)
                {
                    m_pRightIrisTemplate = (byte[])objRightIrisTemplate;
                    m_pLeftIrisTemplate = (byte[])objLeftIrisTemplate;
                    
                }
            }
        }

        private void OnGetIrisImage(int nRightIrisFEDStatus, int nRightIrisLensStatus, int nRightIrisImageSize, object objRightIrisImage, int nLeftIrisFEDStatus, int nLeftIrisLensStatus, int nLeftIrisImageSize, object objLeftIrisImage)
        {
            Console.WriteLine("OnGetIrisImage");

            try
            {                
                m_nEYE = 0;
                
                
                if ( nRightIrisImageSize != 0)
                {
                    m_nEYE += Constants.IS_EYE_RIGHT;

                    picRightEye.Image = Helper.RawToBitmap((byte[])objRightIrisImage, 640, 480, PixelFormat.Format8bppIndexed);
                    this._capturedImage.RawRightIris = (byte[])objRightIrisImage;
                                        
                    btnSaveIrisImages.Enabled = true;

                    switch (nRightIrisFEDStatus)
                    {
                        case Constants.IS_IRIS_STAT_NONE:
                            labRightCountermeasure.Text = "None";
                            break;
                        case Constants.IS_IRIS_STAT_LIVE:
                            labRightCountermeasure.Text = "Live";
                            break;
                        case Constants.IS_IRIS_STAT_FAKE:
                            labRightCountermeasure.Text = "Fake";
                            break;
                        case Constants.IS_IRIS_STAT_FAIL:
                            labRightCountermeasure.Text = "Fail";
                            break;
                        default:
                            labRightCountermeasure.Text = nRightIrisFEDStatus.ToString();
                            break;
                    }

                    switch (nRightIrisLensStatus)
                    {
                        case Constants.IS_LENS_STAT_NONE:
                            labRightLensStatus.Text = "None";
                            break;
                        case Constants.IS_LENS_STAT_HARD:
                            labRightLensStatus.Text = "Hard";
                            break;
                        case Constants.IS_LENS_STAT_PATTERN:
                            labRightLensStatus.Text = "Pattern";
                            break;
                        default:
                            labRightLensStatus.Text = nRightIrisLensStatus.ToString();
                            break;
                    }
                }

                if (nLeftIrisImageSize != 0)
                {
                    m_nEYE += Constants.IS_EYE_LEFT;

                    picLeftEye.Image = Helper.RawToBitmap((byte[])objLeftIrisImage, 640, 480, PixelFormat.Format8bppIndexed);
                    this._capturedImage.RawLeftIris = (byte[])objLeftIrisImage;
                    
                    btnSaveIrisImages.Enabled = true;

                    switch (nLeftIrisFEDStatus)
                    {
                        case Constants.IS_IRIS_STAT_NONE:
                            labLeftCountermeasure.Text = "None";
                            break;
                        case Constants.IS_IRIS_STAT_LIVE:
                            labLeftCountermeasure.Text = "Live";
                            break;
                        case Constants.IS_IRIS_STAT_FAKE:
                            labLeftCountermeasure.Text = "Fake";
                            break;
                        case Constants.IS_IRIS_STAT_FAIL:
                            labLeftCountermeasure.Text = "Fail";
                            break;
                        default:
                            labLeftCountermeasure.Text = nLeftIrisFEDStatus.ToString();
                            break;
                    }

                    switch (nLeftIrisLensStatus)
                    {
                        case Constants.IS_LENS_STAT_NONE:
                            labLeftLensStatus.Text = "None";
                            break;
                        case Constants.IS_LENS_STAT_HARD:
                            labLeftLensStatus.Text = "Hard";
                            break;
                        case Constants.IS_LENS_STAT_PATTERN:
                            labLeftLensStatus.Text = "Pattern";
                            break;
                        default:
                            labLeftLensStatus.Text = nRightIrisLensStatus.ToString();
                            break;
                    }
                }
                
                btnSaveIrisImages.Enabled = true;
                
            }
            finally
            {

                //cmbCounterMeasureLevel.Enabled = true;
                //chkAuditFace.Enabled = true;
                //chkLiveImage.Enabled = true;
                //cmbWhichEye.Enabled = true;
                //cmbLensDetection.Enabled = true;


                initFrameIrisCamera(true);


                if (m_nCaptureMode == IS_CAPTURE)
                {
                    m_pRightIrisImage = (byte[])objRightIrisImage;
                    m_pLeftIrisImage = (byte[])objLeftIrisImage;

                }

            }

        }

        #endregion Device Control Events

        #region Helper Functions

        private void SetUIOnConnect()
        {
            //Enable/Disable appropriate controls
            btnDisconnect.Enabled = true;
            btnConnect.Enabled = false;

            if (new FileInfo("DB.dat").Exists)
                btnUpload.Enabled = true;
            else
                btnUpload.Enabled = false;
            
            tabCtrlIris.Enabled = true;
            tabCtrlEtc.Enabled = true;

            frameIrisImages.Enabled = true;
            frameLiveFaceImages.Enabled = true;
            frameFaceCamera.Enabled = true;
            frameFaceCameraSettings.Enabled = true;
            frameUpsideDown.Enabled = true;
            frameUserInfo.Enabled = true;

        }


        private void initComponent()
        {
            tabCtrlIris.SelectedIndex = 0;
            tabCtrlEtc.SelectedIndex = 0;

            // tabPageEnroll 
            btnEnroll.Enabled = true;
            btnEnrollAbort.Enabled = false;

            cmbEnrollWhichEye.SelectedIndex = 2;
            cmbEnrollCounterMeasureLevel.SelectedIndex = 0;
            cmbEnrollLensDetection.SelectedIndex = 0;
            
            chkEnrollAuditFace.Checked = false;
            chkEnrollLiveImage.Checked = false;
            chkEnrollRetry.Checked = false;
            chkEnrollVerify.Checked = false;

            txtEnrollTimeOut.Text = "20";
            txtEnrollUserID.Text = string.Empty;

            
            // tabPageIdentify           
            btnIdentify.Enabled = true;
            btnIdentifyAbort.Enabled = false;

            cmbIdentifyWhichEye.SelectedIndex = 3;
            cmbIdentifyCounterMeasureLevel.SelectedIndex = 0;
            cmbIdentifyLensDetection.SelectedIndex = 0;

            chkIdentifyAuditFace.Checked = false;
            chkIdentifyLiveImage.Checked = false;

            txtIdentifyTimeOut.Text = "20";


            // tabPageVerify
            btnVerify.Enabled = true;
            btnVerifyAbort.Enabled = false;

            cmbVerifyWhichEye.SelectedIndex = 3;
            cmbVerifyCounterMeasureLevel.SelectedIndex = 0;
            cmbVerifyLensDetection.SelectedIndex = 0;

            chkVerifyAuditFace.Checked = false;
            chkVerifyLiveImage.Checked = false;

            txtVerifyTimeOut.Text = "20";
            txtVerifyUserID.Text = string.Empty;


            //tabPageCapture
            btnStartIrisCapture.Enabled = true;
            btnCaptureAbort.Enabled = false;
            btnAddIrisImage.Enabled = false;
            btnAddIrisTemplate.Enabled = false;
            btnVerifyByTemplate.Enabled = false;

            cmbCapturePurpose.SelectedIndex = 0;
            cmbCaptureIrisType.SelectedIndex = 0;
            cmbCaptureWhichEye.SelectedIndex = 2;
            cmbCaptureCounterMeasureLevel.SelectedIndex = 0;
            cmbCaptureLensDetection.SelectedIndex = 0;

            chkCaptureAuditFace.Checked = false;
            chkCaptureLiveImage.Checked = false;

            txtCaptureTimeOut.Text = "20";
            txtCaptureUserID.Text = string.Empty;


            //tabPageVersion
            txtSerialNumber.Text = string.Empty;
            txtVerSDK.Text = string.Empty;
            txtVeriCAMSW.Text = string.Empty;
            txtVerFS.Text = string.Empty;
            txtVerCMDCenter.Text = string.Empty;
            txtVerDeviceMgr.Text = string.Empty;
            txtVerOS.Text = string.Empty;
            txtVerlibCapture.Text = string.Empty;
            txtVerlibRecog.Text = string.Empty;
            txtVerlibEyeseek.Text = string.Empty;
            txtVerlibCountermeasure.Text = string.Empty;
            txtVerlibLens.Text = string.Empty;
            txtVerlibTwoPi.Text = string.Empty;
            txtColorOffset.Text = string.Empty;

            //tabPageSetting
            cmbVoiceMessages.SelectedIndex = 0;
            cmbLED.SelectedIndex = 0;
            txtQualityThresholdSingle.Text = string.Empty;
            txtQualityThresholdFirst.Text = string.Empty;
            txtQualityThresholdSecond.Text = string.Empty;
            txtQualityThresholdThird.Text = string.Empty;

            //tabPageUpgrade
            txtFileName.Text = "UpdatePackage.dat";
            cmbLanguageIndex.SelectedIndex = 0;


            //frameFaceCamera
            cmbFaceCapturedImageType.SelectedIndex = 1;
            cmbLEDFlash.SelectedIndex = 2;


            m_nCaptureMode = IS_INIT;
            initFrameIrisCamera(false);
            lstEnrolledUserInfo.Items.Clear();

            txtUpsideDown.Text = string.Empty;
            
            
        }

        private void SetUIOnDisconnect()
        {
            m_strUserID = string.Empty;

            //Enable/Disable appropriate controls
            
            btnDisconnect.Enabled = false;
            btnConnect.Enabled = true;
            btnUpload.Enabled = false;
            
            tabCtrlIris.Enabled = false;
            tabCtrlEtc.Enabled = false;
            
            frameIrisImages.Enabled = false;
            frameLiveFaceImages.Enabled = false;
            frameFaceCamera.Enabled = false;
            frameFaceCameraSettings.Enabled = false;
            frameUpsideDown.Enabled = false;
            frameUserInfo.Enabled = false;

            initComponent();

        }

        #endregion Helper Functions

        #region Nested type: CapturedImages

        private class CapturedImages
        {
            public byte[] FaceImage;

            public byte[] RawLeftIris;
            public byte[] RawRightIris;

        }

        #endregion


        private void btnSetLanguage_Click(object sender, EventArgs e)
        {            
            int nResult;

            nResult = _iCAMR100DeviceControl.SetVoiceLanguage(cmbLanguage.SelectedIndex);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                if (nResult == Constants.IS_ERROR_VOICE_FILES_NOT_FOUND)
                    MessageBox.Show(@"Warning: Few of the " + ((Constants.Language)(cmbLanguage.SelectedIndex)).ToString() + @" voice files are missing.", Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else if (nResult == Constants.IS_ERROR_VOICE_FILES_DIR_EMPTY)
                    MessageBox.Show(@"Error: " + ((Constants.Language)(cmbLanguage.SelectedIndex)).ToString() + @" voice files not found." + Environment.NewLine + "Please upload " + ((Constants.Language)(cmbLanguage.SelectedIndex)).ToString() + "-voice.tar file", Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                    MessageBox.Show(this, @"Error in setting voice language type." + Environment.NewLine + @"ERROR CODE: " + nResult, Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                return;
            }

        }

        private void btnUpgrade_Click(object sender, EventArgs e)
        {
            this.Invoke(m_ProgressbarCtrl, true);

            int nResult = _iCAMR100DeviceControl.Upgrade(txtFileName.Text);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                this.Invoke(m_ProgressbarCtrl, false);
                return;
            }
        }

       
        
        private void cmbWB_SelectedIndexChanged(object sender, EventArgs e)
        {
            int nResult;

            if (cmbWB.SelectedIndex != m_nWhiteBalance)
            {
                nResult = _iCAMR100DeviceControl.SetColorEffect(Constants.COLOR_EFFECT_WHITE_BALANCE, cmbWB.SelectedIndex);

                if (nResult != Constants.IS_ERROR_NONE)
                {
                    cmbWB.SelectedIndex = m_nWhiteBalance;
                    ProcessError(nResult);

                    return;

                }

                m_nWhiteBalance = cmbWB.SelectedIndex;
            }
        }

        private void cmbSaturation_SelectedIndexChanged(object sender, EventArgs e)
        {
            int nResult;

            if (cmbSaturation.SelectedIndex != m_nSaturation)
            {
                nResult = _iCAMR100DeviceControl.SetColorEffect(Constants.COLOR_EFFECT_SATURATION, cmbSaturation.SelectedIndex);

                if (nResult != Constants.IS_ERROR_NONE)
                {
                    cmbSaturation.SelectedIndex = m_nSaturation;
                    ProcessError(nResult);

                    return;
                }

                m_nSaturation = cmbSaturation.SelectedIndex;
            }
        }

        private void cmbBrightness_SelectedIndexChanged(object sender, EventArgs e)
        {
            int nResult;

            if (cmbBrightness.SelectedIndex != m_nBrightness)
            {
                nResult = _iCAMR100DeviceControl.SetColorEffect(Constants.COLOR_EFFECT_BRIGHTNESS, cmbBrightness.SelectedIndex);

                if (nResult != Constants.IS_ERROR_NONE)
                {
                    cmbBrightness.SelectedIndex = m_nBrightness;
                    ProcessError(nResult);

                    return;
                }

                m_nBrightness = cmbBrightness.SelectedIndex;
            }
        }

        private void cmbISO_SelectedIndexChanged(object sender, EventArgs e)
        {
            int nResult;

            if (cmbISO.SelectedIndex != m_nISO)
            {
                nResult = _iCAMR100DeviceControl.SetColorEffect(Constants.COLOR_EFFECT_ISO, cmbISO.SelectedIndex);

                if (nResult != Constants.IS_ERROR_NONE)
                {
                    cmbISO.SelectedIndex = m_nISO;
                    ProcessError(nResult);
                    
                    return;
                }

                m_nISO = cmbISO.SelectedIndex;
            }
        }

        private void cmbSharpness_SelectedIndexChanged(object sender, EventArgs e)
        {
            int nResult;

            if (cmbSharpness.SelectedIndex != m_nSharpness)
            {
                nResult = _iCAMR100DeviceControl.SetColorEffect(Constants.COLOR_EFFECT_SHARPNESS, cmbSharpness.SelectedIndex);

                if (nResult != Constants.IS_ERROR_NONE)
                {
                    cmbSharpness.SelectedIndex = m_nSharpness;
                    ProcessError(nResult);

                    return;
                }

                m_nSharpness = cmbSharpness.SelectedIndex;
                
            }
        }

        private void btnFaceCaptureStart_Click(object sender, EventArgs e)
        {
            int nResult;

            nResult = _iCAMR100DeviceControl.StartFaceCapture();

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);

                return;
            }
            
            btnFaceCaptureStart.Enabled = false;
            btnFaceCaptureStop.Enabled = true;
            btnFaceCaptureAbort.Enabled = true;
            btnAF.Enabled = true;
        }

        private void btnFaceCaptureStop_Click(object sender, EventArgs e)
        {
            int nResult;

            int nImageType = cmbFaceCapturedImageType.SelectedIndex + 1;
            int nStrobe = cmbLEDFlash.SelectedIndex;

           


            nResult = _iCAMR100DeviceControl.StopFaceCapture(nImageType, nStrobe);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }
            
            cmbFaceCapturedImageType.Enabled = true;
            cmbLEDFlash.Enabled = true;
            btnFaceCaptureStart.Enabled = true;
            btnFaceCaptureStop.Enabled = false;
            btnFaceCaptureAbort.Enabled = false;
            btnAF.Enabled = false;

        }

        private void btnFaceCaptureAbort_Click(object sender, EventArgs e)
        {
            int nResult;

            nResult = _iCAMR100DeviceControl.StopFaceCapture(0, 0);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }

            cmbFaceCapturedImageType.Enabled = true;
            cmbLEDFlash.Enabled = true;
            btnFaceCaptureStart.Enabled = true;
            btnFaceCaptureStop.Enabled = false;
            btnFaceCaptureAbort.Enabled = false;
            btnAF.Enabled = false;

            picLiveFace.Image = null;
        
        }

        private void cmbLanguageIndex_SelectedIndexChanged(object sender, EventArgs e)
        {
            txtLanguageFileName.Text = ((Constants.Language)cmbLanguageIndex.SelectedIndex).ToString() +"-voice.tar";
        }

        private void btnUpdateVoiceMessage_Click(object sender, EventArgs e)
        {
            int nResult = _iCAMR100DeviceControl.UpdateVoiceMessage(txtLanguageFileName.Text, cmbLanguageIndex.SelectedIndex);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }
        }
        
        public string ConvertToString(sbyte[] arr)
        {
            string returnStr;

            unsafe
            {

                fixed (sbyte* fixedPtr = arr)
                {
                    returnStr = new string(fixedPtr);
                }
            }

            return (returnStr);
        }

        private void btnIdentify_Click(object sender, EventArgs e)
        {
            int nResult = 0;
            int nWhichEye = 0;
            int nCounterMeasureLevel = (cmbIdentifyCounterMeasureLevel.SelectedIndex == 0 ? Constants.IS_FED_LEVEL_1 : Constants.IS_FED_LEVEL_2);
            int nLensDetectionLevel = cmbIdentifyLensDetection.SelectedIndex;
            int nIsLive = (chkIdentifyLiveImage.Checked ? Constants.IS_ENABLE : Constants.IS_DISABLE);
            int nIsAuditFace = (chkIdentifyAuditFace.Checked ? Constants.IS_FACE_AUDIT_ON : Constants.IS_FACE_AUDIT_OFF);
            int nTimeOut = 0;

            if (txtIdentifyTimeOut.Text.Length <= 0)
            {
                MessageBox.Show(this, " Check the 'Time Out'.",
                                    Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }

            nTimeOut = Convert.ToInt32(txtIdentifyTimeOut.Text);

            switch (cmbIdentifyWhichEye.SelectedIndex)
            {
                case 0:
                    nWhichEye = Constants.IS_EYE_RIGHT;
                    break;
                case 1:
                    nWhichEye = Constants.IS_EYE_LEFT;
                    break;
                case 2:
                    nWhichEye = Constants.IS_EYE_BOTH;
                    break;
                case 3:
                    nWhichEye = Constants.IS_EYE_EITHER;
                    break;
            }

            nResult = _iCAMR100DeviceControl.IdentifyUser(nWhichEye, nCounterMeasureLevel, nLensDetectionLevel, nTimeOut, nIsAuditFace, nIsLive);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }

            m_nCaptureMode = IS_IDENTIFY;
            initFrameIrisCamera(false);


            if (nIsLive == Constants.IS_ENABLE)
            {
                pnlAlign.Visible = true;
            }

            _iCAMR100DeviceControl.ControlIndicator(Constants.IS_SND_CENTER_EYES_IN_MIRROR, Constants.IS_IND_NONE);

        }

        private void btnVerify_Click(object sender, EventArgs e)
        {
            int nResult = 0;
            int nWhichEye = 0;
            int nCounterMeasureLevel = (cmbVerifyCounterMeasureLevel.SelectedIndex == 0 ? Constants.IS_FED_LEVEL_1 : Constants.IS_FED_LEVEL_2);
            int nLensDetectionLevel = cmbVerifyLensDetection.SelectedIndex;
            int nIsLive = (chkVerifyLiveImage.Checked ? Constants.IS_ENABLE : Constants.IS_DISABLE);
            int nIsAuditFace = (chkVerifyAuditFace.Checked ? Constants.IS_FACE_AUDIT_ON : Constants.IS_FACE_AUDIT_OFF);
            int nTimeOut = 0;

            if (txtVerifyTimeOut.Text.Length <= 0)
            {
                MessageBox.Show(this, " Check the 'Time Out'.",
                                    Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }

            nTimeOut = Convert.ToInt32(txtVerifyTimeOut.Text);

            switch (cmbVerifyWhichEye.SelectedIndex)
            {
                case 0:
                    nWhichEye = Constants.IS_EYE_RIGHT;
                    break;
                case 1:
                    nWhichEye = Constants.IS_EYE_LEFT;
                    break;
                case 2:
                    nWhichEye = Constants.IS_EYE_BOTH;
                    break;
                case 3:
                    nWhichEye = Constants.IS_EYE_EITHER;
                    break;
            }

            if (txtVerifyUserID.Text.Length <= 0 || txtVerifyUserID.Text.Length > 40)
            {
                MessageBox.Show(this, " Check the 'User ID'.",
                                    Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }

            nResult = _iCAMR100DeviceControl.VerifyByID(txtVerifyUserID.Text, nWhichEye, nCounterMeasureLevel, nLensDetectionLevel, nTimeOut, nIsAuditFace, nIsLive);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }

            m_strUserID = txtVerifyUserID.Text;

            m_nCaptureMode = IS_VERIFY_BY_ID;
            initFrameIrisCamera(false);


            if (nIsLive == Constants.IS_ENABLE)
            {
                pnlAlign.Visible = true;
            }

            _iCAMR100DeviceControl.ControlIndicator(Constants.IS_SND_CENTER_EYES_IN_MIRROR, Constants.IS_IND_NONE);
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            int nResult = Constants.IS_ERROR_UNKNOWN;

            if (MessageBox.Show("Do you want to deletes the whole user database in device?", Constants.TITLE, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                _iCAMR100DeviceControl.ClearUserDB();
                nResult = m_HostDBControl.DeleteAllUserInfo();

                if (nResult == Constants.IS_ERROR_NONE)
                {
                    LoadEnrolledUserInfo();
                    MessageBox.Show(this, " Delete complete.", Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        
        private void btnUserInfo_Click(object sender, EventArgs e)
        {
            int nResult;

            string strUserID;


            strUserID = txtUser_ID.Text;

            nResult = _iCAMR100DeviceControl.DeleteUserInfo(strUserID);

            if (nResult == Constants.IS_ERROR_NONE)
            {
                m_HostDBControl.DeleteUserInfo(strUserID);
            }
            else
            {
                ProcessError(nResult);
                return;

            }

            

            LoadEnrolledUserInfo();

            MessageBox.Show(this, " Delete complete.(User ID : " + strUserID + ")",
                                    Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
                       
        }

        

        private void lstEnrolledUserInfo_SelectedIndexChanged(object sender, EventArgs e)
        {
            string strUserID;
            string[] strUserInfo;
            bool bIsSelectedItem = false;



            ListView.SelectedListViewItemCollection itemCollection = lstEnrolledUserInfo.SelectedItems;

            foreach (ListViewItem item in itemCollection)
            {
                bIsSelectedItem = true;
                strUserID = item.SubItems[1].Text;

                m_HostDBControl.SelectEnrolledUserInfo(strUserID, out strUserInfo);

                txtUser_ID.Text = strUserInfo[1];
                txtUserName.Text = strUserInfo[2];
                txtCardName.Text = strUserInfo[8];
                txtCardID.Text = strUserInfo[9];


                labRQuality.Text = strUserInfo[6];
                labLQuality.Text = strUserInfo[7];

                txtInsertDate.Text = strUserInfo[10];


                m_nSelectedRow = item.Index;
                
                picEnrolledAudit.ImageLocation = strUserInfo[3];
                picEnrolledREye.ImageLocation = strUserInfo[4];
                picEnrolledLEye.ImageLocation = strUserInfo[5];

            }

            if (!bIsSelectedItem)
            {
                initFrameUserInfo();
            }
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {
            int nResult;


            if (_capturedImage.FaceImage != null && picEnrolledAudit.Image != null)
            {
                string guid = Guid.NewGuid().ToString();
                string strFacePath = Path.Combine(_faceImagesDirectory, "F" + m_strUserID + "_" + guid + ".jpeg");
                
                nResult =  m_HostDBControl.UpdateEnrolledFacePath(txtUser_ID.Text, strFacePath);

                if (nResult != Constants.IS_ERROR_NONE)
                {
                    ProcessError(nResult);
                    return;
                }

                Helper.ByteArrayToFile(_faceImagesDirectory, "F" + m_strUserID + "_" + guid + ".jpeg", _capturedImage.FaceImage);
            }

            nResult = m_HostDBControl.UpdateEnrolledUserInfo(txtUser_ID.Text, txtUserName.Text, txtCardName.Text, txtCardID.Text);

            if (nResult == Constants.IS_ERROR_NONE)
            {

                if (lstEnrolledUserInfo.Items[m_nSelectedRow].SubItems[1].Text == txtUser_ID.Text)
                    lstEnrolledUserInfo.Items[m_nSelectedRow].SubItems[2].Text = txtUserName.Text;
                else
                    LoadEnrolledUserInfo();

                MessageBox.Show(this, " Update complete.(User ID : " + txtUser_ID.Text + ")",
                                    Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
                ProcessError(nResult);

            
        }

        private void btnGetUserInfo_Click(object sender, EventArgs e)
        {
            int nResult;
            stUSERINFO stEnrolledUserInfo = new stUSERINFO();

            nResult = _iCAMR100DeviceControl.GetUserInfo(txtUser_ID.Text, out stEnrolledUserInfo);

            if (nResult == Constants.IS_ERROR_NONE)
            {
                MessageBox.Show(this, "GetUserInfo [" + txtUser_ID.Text + "] \n   pID : " + ConvertToString(stEnrolledUserInfo.pID)
                    + "\n   EnrolledIris : " + stEnrolledUserInfo.lEnrolledIris + "\n   InsertDate : " + ConvertToString(stEnrolledUserInfo.pInsertDate)
                    + "\n   UpdateDate : " + ConvertToString(stEnrolledUserInfo.pUpdateDate), Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
                ProcessError(nResult);
        }

        private void btnGetNum_Click(object sender, EventArgs e)
        {
            int nNumOfUserDB;
            
            nNumOfUserDB = _iCAMR100DeviceControl.GetNumberOfUserDB();

            MessageBox.Show(this, "GetNumberOfUserDB(Device Database) : " + nNumOfUserDB , Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
            
        }

        private void txtUser_ID_TextChanged(object sender, EventArgs e)
        {
            txtUserName.Text = string.Empty;
            txtCardID.Text = string.Empty;
            txtCardName.Text = string.Empty;
            txtInsertDate.Text = string.Empty;

            labRQuality.Text = string.Empty;
            labLQuality.Text = string.Empty;

            picEnrolledREye.Image = picEnrolledLEye.Image = picEnrolledAudit.Image = null;
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            int nResult;

            btnUpload.Enabled = false;

            nResult = _iCAMR100DeviceControl.DownloadUserDB(out m_nNumOfUser);

            if (nResult == Constants.IS_ERROR_NONE)
            {
                m_nStatus = FILE_SAVE;
            }
            else
            {
                m_nStatus = NONE;
                ProcessError(nResult);
            }
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {



            int nResult;

            byte[] pBuff = null;

            using (FileStream fileStream = new FileStream("DB.dat", FileMode.Open))
                {

                    pBuff = new byte[fileStream.Length];

                    fileStream.Read(pBuff, 0, pBuff.Length);

                    fileStream.Close();

                }


            nResult = _iCAMR100DeviceControl.UploadUserDB(pBuff.Length / 1104, pBuff);

            if (nResult == Constants.IS_ERROR_NONE)
            {
                if (MessageBox.Show("Upload complete.( DB.dat ) \n Do you want synchronization?", Constants.TITLE, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    nResult = _iCAMR100DeviceControl.DownloadUserDB(out m_nNumOfUser);

                    if (nResult == Constants.IS_ERROR_NONE)
                    {
                        if (m_nNumOfUser > 0)
                            m_nStatus = COMPARE;
                        else
                            m_nStatus = ALL_DELETE;
                    }
                        
                }
            
            }
            else
                ProcessError(nResult);
        }

       

        private void btnGetQualityThreshold_Click(object sender, EventArgs e)
        {
            int nResult;

            int nSingleTry;
            int nFirstTry;
            int nSecondTry;
            int nThirdTry;

            nResult = _iCAMR100DeviceControl.GetEnrollmentQualityThreshold(out nSingleTry, out nFirstTry, out nSecondTry, out nThirdTry);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }

            txtQualityThresholdSingle.Text = nSingleTry.ToString();
            txtQualityThresholdFirst.Text = nFirstTry.ToString();
            txtQualityThresholdSecond.Text = nSecondTry.ToString();
            txtQualityThresholdThird.Text = nThirdTry.ToString();

        }

        private void btnSetQualityThreshold_Click(object sender, EventArgs e)
        {
            int nResult;

            int nSingleTry;
            int nFirstTry;
            int nSecondTry;
            int nThirdTry;


            if (txtQualityThresholdSingle.Text.Length <= 0 || txtQualityThresholdFirst.Text.Length <= 0 || txtQualityThresholdSecond.Text.Length <= 0 || txtQualityThresholdThird.Text.Length <= 0)
            {
                MessageBox.Show(this, " Check the value.",
                                    Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }


            nSingleTry = Convert.ToInt32(txtQualityThresholdSingle.Text);
            nFirstTry = Convert.ToInt32(txtQualityThresholdFirst.Text);
            nSecondTry = Convert.ToInt32(txtQualityThresholdSecond.Text);
            nThirdTry = Convert.ToInt32(txtQualityThresholdThird.Text);

            nResult = _iCAMR100DeviceControl.SetEnrollmentQualityThreshold(nSingleTry, nFirstTry, nSecondTry, nThirdTry);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }

        }
                
        private void btnGetLanguage_Click(object sender, EventArgs e)
        {
            int nResult;
            int nLanguage;

            nResult = _iCAMR100DeviceControl.GetVoiceLanguage(out nLanguage);

            if (nResult != Constants.IS_ERROR_NONE)
            {
                MessageBox.Show(this, @"Error in getting voice language type." + Environment.NewLine + @"ERROR CODE: " + nResult, Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            cmbLanguage.SelectedIndex = nLanguage;

            //txtLanguage.Text = ((Constants.Language)nLanguage).ToString();
        }

        private void picEnrolledAudit_Click(object sender, EventArgs e)
        {
            if (picEnrolledAudit.Image != null)
            {
                frmFaceView faceView = new frmFaceView();

                faceView.DisplayFaceImage(picEnrolledAudit.Image);
                faceView.ShowDialog(this);
            }
            
        }

        private void btnStartIrisCapture_Click(object sender, EventArgs e)
        {
            int nResult = 0;
            int nPurpose, nWhichEye, nCounterMeasureLevel, nLensDetectionLevel, nTimeOut, nIsAuditFace, nIsLive;
            
            nPurpose = (cmbCapturePurpose.SelectedIndex == 0 ? Constants.IS_ENROLLMENT : Constants.IS_RECOGNITION);
            m_nIrisType = (cmbCaptureIrisType.SelectedIndex == 0 ? Constants.IS_IRIS_IMAGE : Constants.IS_IRIS_TEMPLATE);
            nCounterMeasureLevel = (cmbCaptureCounterMeasureLevel.SelectedIndex == 0 ? Constants.IS_FED_LEVEL_1 : Constants.IS_FED_LEVEL_2);
            nLensDetectionLevel = cmbCaptureLensDetection.SelectedIndex;
            nIsLive = (chkCaptureLiveImage.Checked ? Constants.IS_ENABLE : Constants.IS_DISABLE);
            nIsAuditFace = (chkCaptureAuditFace.Checked ? Constants.IS_FACE_AUDIT_ON : Constants.IS_FACE_AUDIT_OFF);


            switch (cmbCaptureWhichEye.SelectedIndex)
            {
                case 0:
                    nWhichEye = Constants.IS_EYE_RIGHT;
                    break;
                case 1:
                    nWhichEye = Constants.IS_EYE_LEFT;
                    break;
                case 2:
                    nWhichEye = Constants.IS_EYE_BOTH;
                    break;
                case 3:
                    nWhichEye = Constants.IS_EYE_EITHER;
                    break;
                default:
                    nWhichEye = 0;
                    break;
            }

            if (txtCaptureTimeOut.Text.Length <= 0)
            {
                MessageBox.Show(this, " Check the 'Time Out'.",
                                    Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }

            nTimeOut = Convert.ToInt32(txtCaptureTimeOut.Text);


            m_pRightIrisImage = null;
            m_pLeftIrisImage = null;
            m_pRightIrisTemplate = null;
            m_pLeftIrisTemplate = null;

            nResult = _iCAMR100DeviceControl.StartIrisCapture(nPurpose, m_nIrisType, nWhichEye, nCounterMeasureLevel, nLensDetectionLevel, nTimeOut, nIsAuditFace, nIsLive);


            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }

            if (nIsLive == Constants.IS_ENABLE)
            {
                pnlAlign.Visible = true;
            }

            _iCAMR100DeviceControl.ControlIndicator(Constants.IS_SND_CENTER_EYES_IN_MIRROR, Constants.IS_IND_NONE);
            
            m_nCaptureMode = IS_CAPTURE;
            initFrameIrisCamera(false);

        }

        private void btnAF_Click(object sender, EventArgs e)
        {
            int nResult;


            nResult = _iCAMR100DeviceControl.SetAutoFocus();

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }
        }

        private void btnAddIrisTemplate_Click(object sender, EventArgs e)
        {
            int nResult = 0;
            int nWhichEye;
            int nRightStatus, nLeftStatus, nRightQuality, nLeftQuality;
            string strEnrolledUserID;

            switch (cmbCaptureWhichEye.SelectedIndex)
            {
                case 0:
                    nWhichEye = Constants.IS_EYE_RIGHT;
                    break;
                case 1:
                    nWhichEye = Constants.IS_EYE_LEFT;
                    break;
                case 2:
                    nWhichEye = Constants.IS_EYE_BOTH;
                    break;
                case 3:
                    nWhichEye = Constants.IS_EYE_EITHER;
                    break;
                default:
                    nWhichEye = 0;
                    break;
            }

            nResult = _iCAMR100DeviceControl.AddUserIrisTemplate(txtCaptureUserID.Text, nWhichEye, m_pRightIrisTemplate, m_pLeftIrisTemplate, out nRightStatus, out nLeftStatus, out nRightQuality, out nLeftQuality, out strEnrolledUserID);


            if (nResult == Constants.IS_ERROR_NONE)
            {
                stUSERINFO stEnrolledUserInfo;
                string[] strUserInfo;
                string strRPath = string.Empty;
                string strLPath = string.Empty;
                string strFacePath = string.Empty;
                
                if (nRightQuality != 0)
                {
                    labRightQualityValue.Text = nRightQuality.ToString();
                }

                if (nLeftQuality != 0)
                {
                    labLeftQualityValue.Text = nLeftQuality.ToString();
                }


                stEnrolledUserInfo = new stUSERINFO();


                nResult = _iCAMR100DeviceControl.GetUserInfo(txtCaptureUserID.Text, out stEnrolledUserInfo);

                if (nResult == Constants.IS_ERROR_NONE)
                {
                    string guid = Guid.NewGuid().ToString();


                    if (_capturedImage.FaceImage != null && _capturedImage.FaceImage.Length != 0)
                    {
                        strFacePath = Path.Combine(_faceImagesDirectory, "F" + txtCaptureUserID.Text + "_" + guid + ".jpeg");

                        Helper.ByteArrayToFile(_faceImagesDirectory, "F" + txtCaptureUserID.Text + "_" + guid + ".jpeg", _capturedImage.FaceImage);
                    }


                    m_HostDBControl.InsertUserInfo(txtCaptureUserID.Text, nRightQuality, nLeftQuality, strFacePath, strRPath, strLPath, ConvertToString(stEnrolledUserInfo.pInsertDate), ConvertToString(stEnrolledUserInfo.pUpdateDate));

                    m_HostDBControl.SelectEnrolledUserInfo(txtCaptureUserID.Text, out strUserInfo);

                    ListViewItem item = new ListViewItem(strUserInfo[0]);
                    item.SubItems.Add(strUserInfo[1]);
                    item.SubItems.Add(strUserInfo[2]);


                    lstEnrolledUserInfo.Items.Add(item);

                    txtUser_ID.Text = strUserInfo[1];

                    labRQuality.Text = strUserInfo[6];
                    labLQuality.Text = strUserInfo[7];

                    txtInsertDate.Text = strUserInfo[10];

                    picEnrolledAudit.ImageLocation = strUserInfo[3];


                }
                else
                {
                    ProcessError(nResult);
                }
            }
            else if (nResult == Constants.IS_ERROR_ALREADY_EXIST)
            {
                MessageBox.Show(this, " Already exist user (User ID : " + strEnrolledUserID + ") " + Environment.NewLine + " nRightStatus :" + ((Constants.Error)nRightStatus).ToString() + " (" + nRightStatus + ") " +  Environment.NewLine + " nLeftStatus :" + ((Constants.Error)nLeftStatus).ToString() + " (" + nLeftStatus + ") ", Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, "nResult : " + ((Constants.Error)nResult).ToString() + " (" + nResult + ") " + Environment.NewLine + " nRightStatus :" + ((Constants.Error)nRightStatus).ToString() + " (" + nRightStatus + ") " + Environment.NewLine + " nLeftStatus :" + ((Constants.Error)nLeftStatus).ToString() + " (" + nLeftStatus + ") ", Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            //m_pRightIrisTemplate = null;
            //m_pLeftIrisTemplate = null;

            //btnAddIrisTemplate.Enabled = false;
        }

        private void btnAddIrisImage_Click(object sender, EventArgs e)
        {
            int nResult = 0;
            int nWhichEye;
            int nRightStatus, nLeftStatus, nRightQuality, nLeftQuality;
            string strEnrolledUserID;


            switch (cmbCaptureWhichEye.SelectedIndex)
            {
                case 0:
                    nWhichEye = Constants.IS_EYE_RIGHT;
                    break;
                case 1:
                    nWhichEye = Constants.IS_EYE_LEFT;
                    break;
                case 2:
                    nWhichEye = Constants.IS_EYE_BOTH;
                    break;
                default:
                    nWhichEye = 0;
                    break;
            }

            nResult = _iCAMR100DeviceControl.AddUserIrisImage(txtCaptureUserID.Text, nWhichEye, m_pRightIrisImage, m_pLeftIrisImage, out nRightStatus, out nLeftStatus, out nRightQuality, out nLeftQuality, out strEnrolledUserID);
            
            
            if(nResult == Constants.IS_ERROR_NONE)
            {
                stUSERINFO stEnrolledUserInfo;
                string[] strUserInfo;
                string strRPath = string.Empty;
                string strLPath = string.Empty;
                string strFacePath = string.Empty;

                labQuality.Show();

                if (nRightQuality != 0)
                {
                    prgRQuality.Show();
                    prgRQuality.Value = nRightQuality;
                    labRightQualityValue.Text = nRightQuality.ToString();
                }

                if (nLeftQuality != 0)
                {
                    prgLQuality.Show();
                    prgLQuality.Value = nLeftQuality;
                    labLeftQualityValue.Text = nLeftQuality.ToString();
                }

                
                stEnrolledUserInfo = new stUSERINFO();


                nResult = _iCAMR100DeviceControl.GetUserInfo(txtCaptureUserID.Text, out stEnrolledUserInfo);

                if (nResult == Constants.IS_ERROR_NONE)
                {
                    string guid = Guid.NewGuid().ToString();

                    if (nWhichEye != Constants.IS_EYE_LEFT)
                    {
                        nResult = SaveIrisImage(_enrollmentImagesDirectory, "R" + txtCaptureUserID.Text + "_" + guid, m_pRightIrisImage, out strRPath);

                        if (nResult != Constants.IS_RST_SUCCESS)
                        {
                            ProcessError(nResult);
                            return;
                        }
                    }
                    if (nWhichEye != Constants.IS_EYE_RIGHT)
                    {
                        SaveIrisImage(_enrollmentImagesDirectory, "L" + txtCaptureUserID.Text + "_" + guid, m_pLeftIrisImage, out strLPath);

                        if (nResult != Constants.IS_RST_SUCCESS)
                        {
                            ProcessError(nResult);
                            return;
                        }
                    }

                    if (_capturedImage.FaceImage != null && _capturedImage.FaceImage.Length != 0)
                    {
                        strFacePath = Path.Combine(_faceImagesDirectory, "F" + txtCaptureUserID.Text + "_" + guid + ".jpeg");

                        Helper.ByteArrayToFile(_faceImagesDirectory, "F" + txtCaptureUserID.Text + "_" + guid + ".jpeg", _capturedImage.FaceImage);
                    }


                    m_HostDBControl.InsertUserInfo(txtCaptureUserID.Text, nRightQuality, nLeftQuality, strFacePath, strRPath, strLPath, ConvertToString(stEnrolledUserInfo.pInsertDate), ConvertToString(stEnrolledUserInfo.pUpdateDate));

                    m_HostDBControl.SelectEnrolledUserInfo(txtCaptureUserID.Text, out strUserInfo);

                    ListViewItem item = new ListViewItem(strUserInfo[0]);
                    item.SubItems.Add(strUserInfo[1]);
                    item.SubItems.Add(strUserInfo[2]);


                    lstEnrolledUserInfo.Items.Add(item);

                    txtUser_ID.Text = strUserInfo[1];

                    labRQuality.Text = strUserInfo[6];
                    labLQuality.Text = strUserInfo[7];

                    txtInsertDate.Text = strUserInfo[10];

                    picEnrolledAudit.ImageLocation = strUserInfo[3];
                    picEnrolledREye.ImageLocation = strUserInfo[4];
                    picEnrolledLEye.ImageLocation = strUserInfo[5];

                }
                else
                {
                    ProcessError(nResult);
                }
            }
            else if (nResult == Constants.IS_ERROR_ALREADY_EXIST)
            {
                MessageBox.Show(this, " Already exist user (User ID : " + strEnrolledUserID + ") " + Environment.NewLine + " nRightStatus :" + ((Constants.Error)nRightStatus).ToString() + " (" + nRightStatus + ") " + Environment.NewLine + " nLeftStatus :" + ((Constants.Error)nLeftStatus).ToString() + " (" + nLeftStatus + ") ", Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, "nResult : " + ((Constants.Error)nResult).ToString() + " (" + nResult + ") " + Environment.NewLine + " nRightStatus :" + ((Constants.Error)nRightStatus).ToString() + " (" + nRightStatus + ") " + Environment.NewLine + " nLeftStatus :" + ((Constants.Error)nLeftStatus).ToString() + " (" + nLeftStatus + ") ", Constants.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            
           
        }

        private void Abort()
        {
            int nResult;

            nResult = _iCAMR100DeviceControl.AbortCapture();

            if (nResult != Constants.IS_ERROR_NONE)
                ProcessError(nResult);


            m_strUserID = string.Empty;
            
            initFrameIrisCamera(true);
        } 

        private void btnEnrollAbort_Click(object sender, EventArgs e)
        {
            Abort();
        }

        private void btnIdentifyAbort_Click(object sender, EventArgs e)
        {
            Abort();

        }

        private void btnVerifyAbort_Click(object sender, EventArgs e)
        {
            Abort();
        }

        private void btnCaptureAbort_Click(object sender, EventArgs e)
        {
            Abort();
        }

        private void btnVerifyByTemplate_Click(object sender, EventArgs e)
        {
            int nResult = 0;
            int nWhichEye, nCounterMeasureLevel, nLensDetectionLevel, nTimeOut, nIsAuditFace, nIsLive;

            nCounterMeasureLevel = (cmbCaptureCounterMeasureLevel.SelectedIndex == 0 ? Constants.IS_FED_LEVEL_1 : Constants.IS_FED_LEVEL_2);
            nLensDetectionLevel = cmbCaptureLensDetection.SelectedIndex;
            nIsLive = (chkCaptureLiveImage.Checked ? Constants.IS_ENABLE : Constants.IS_DISABLE);
            nIsAuditFace = (chkCaptureAuditFace.Checked ? Constants.IS_FACE_AUDIT_ON : Constants.IS_FACE_AUDIT_OFF);


            switch (cmbCaptureWhichEye.SelectedIndex)
            {
                case 0:
                    nWhichEye = Constants.IS_EYE_RIGHT;
                    break;
                case 1:
                    nWhichEye = Constants.IS_EYE_LEFT;
                    break;
                case 2:
                    nWhichEye = Constants.IS_EYE_BOTH;
                    break;
                case 3:
                    nWhichEye = Constants.IS_EYE_EITHER;
                    break;
                default:
                    nWhichEye = 0;
                    break;
            }

            nTimeOut = Convert.ToInt32(txtCaptureTimeOut.Text);

            nResult = _iCAMR100DeviceControl.VerifyByIrisTemplate(m_pRightIrisTemplate, m_pLeftIrisTemplate, nWhichEye, nCounterMeasureLevel, nLensDetectionLevel, nTimeOut, nIsAuditFace, nIsLive);
            //btnVerifyByTemplate.Enabled = false;

            if (nResult != Constants.IS_ERROR_NONE)
            {
                ProcessError(nResult);
                return;
            }


            _iCAMR100DeviceControl.ControlIndicator(Constants.IS_SND_CENTER_EYES_IN_MIRROR, Constants.IS_IND_NONE);

            m_nCaptureMode = IS_VERIFY_BY_TEMPLATE;
            initFrameIrisCamera(false);
        }
    }
} 