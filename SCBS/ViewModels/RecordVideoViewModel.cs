using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Emgu.CV;
using Emgu.CV.Structure;
using Caliburn.Micro;
using System.Drawing;
using System.Windows;

namespace SCBS.ViewModels
{
    class RecordVideoViewModel : Screen
    {
        private WriteableBitmap imageWebcam;
        private VideoCapture capture;
        public WriteableBitmap VideoPlayback
        {
            get { return imageWebcam; }
            set
            {
                imageWebcam = value;
                NotifyOfPropertyChange(() => VideoPlayback);
            }
        }

        public void StartRecordButton()
        {
            if(capture == null)
            {
                capture = new VideoCapture(0);
            }
            capture.ImageGrabbed += Capture_ImageGrabbed;
            capture.Start();
        }

        private void Capture_ImageGrabbed(object sender, EventArgs e)
        {
            try
            {
                Mat m = new Mat();
                capture.Retrieve(m);

                System.Windows.Media.Imaging.BitmapSource bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
  m.ToImage<Bgr, byte>().Bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty,
  System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                System.Windows.Media.Imaging.WriteableBitmap writeableBitmap = new System.Windows.Media.Imaging.WriteableBitmap(bitmapSource);

                VideoPlayback = writeableBitmap;

            }
            catch
            {

            }
        }

        public void StopRecordButton()
        {
            if(capture != null)
            {
                capture = null;
            }
        }
    }
}
