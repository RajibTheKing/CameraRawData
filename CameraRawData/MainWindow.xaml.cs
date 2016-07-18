using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
//using System.Windows.Shapes;


//এই দুইটা DLL ক্যামেরা থেকে Raw ডাটা নিতে সাহায্য করেছে 
using AForge.Video;
using AForge.Video.DirectShow;

//এইটা Bitmap ডাটা নিয়ে কাজ করার জন্য ব্যবহার করা হয়েছে
using System.Drawing;

//এইটা MemoryStream ব্যবহার করায় লিখতে হয়েছে
using System.IO;

// এইটা ImageCodecInfo পাওয়ার জন্য ব্যবহার করা হয়েছে
using System.Drawing.Imaging;

using System.ComponentModel;

using System.Windows.Interop;

namespace CameraRawData
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    //DateTime time;


    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        VideoCaptureDevice videoSource;
        public static int m_iVideoWidth = 640;
        public static int m_iVideoHeight = 480;


        public static MainWindow mainWindow;
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            mainWindow = this;
            this.Loaded += Form1_Load;
            this.Closed += MainWindow_Closed;
        }

        void MainWindow_Closed(object sender, EventArgs e)
        {
            videoSource.Stop();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            //List all available video sources. (That can be webcams as well as tv cards, etc)
            FilterInfoCollection videosources = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            //Check if atleast one video source is available
            if (videosources != null)
            {
                //For example use first video device. You may check if this is your webcam.
                videoSource = new VideoCaptureDevice(videosources[0].MonikerString);
                
                try
                {
                    //Check if the video device provides a list of supported resolutions
                    if (videoSource.VideoCapabilities.Length > 0)
                    {
                        string highestSolution = "0;0";
                        string sSupportedResolution = "";
                        int iNeededSolution = 0;
                        //Search for the highest resolution
                        for (int i = 0; i < videoSource.VideoCapabilities.Length; i++)
                        {
                            if (videoSource.VideoCapabilities[i].FrameSize.Width > Convert.ToInt32(highestSolution.Split(';')[0]))
                            {
                                highestSolution = videoSource.VideoCapabilities[i].FrameSize.Width.ToString() + ";" + i.ToString();
                                sSupportedResolution = videoSource.VideoCapabilities[i].FrameSize.Width.ToString() + " x " + videoSource.VideoCapabilities[i].FrameSize.Height.ToString();
                                Console.WriteLine("Supported Resoluton, (Width x Height) = " + sSupportedResolution);
                                if(videoSource.VideoCapabilities[i].FrameSize.Width == m_iVideoWidth)
                                {
                                    iNeededSolution = i;
                                }
                            }
                        }
                        //Set the highest resolution as active
                        videoSource.VideoResolution = videoSource.VideoCapabilities[iNeededSolution];
                    }
                }
                catch { }

                //Create NewFrame event handler
                //(This one triggers every time a new frame/image is captured
                videoSource.NewFrame += new NewFrameEventHandler(videoSource_NewFrame);

                
            }
        }

        void videoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Bitmap b = (Bitmap)eventArgs.Frame.Clone();
            //Console.WriteLine("Bitmap size = " + b.Size);

            ShowOnScreen(b);

            byte[] rgbValues = ConvertBitMapToRGB24ByteArray(b);
            Console.WriteLine("rgbValues length = " + rgbValues.Length);

            
            byte[] yuv420values = new byte[m_iVideoHeight * m_iVideoWidth * 3 / 2];
            ConvertRGB24ToI420(rgbValues, yuv420values);
            Console.WriteLine("Yuv420 Length = " + yuv420values.Length);
            
             
             
            //AppendAllBytes("myFile.rgb24", rgbValues);

            //AppendAllBytes("myYuv420.yuv", yuv420values);
            
        }

        byte[] realRgb = new byte[m_iVideoWidth * m_iVideoHeight * 3];
        public byte[] ConvertBitMapToRGB24ByteArray(Bitmap bitmap)
        {
            byte[] result = ConvertBitMapToRGB24ByteArray_WithBmpHeader(bitmap);

            for (int i = 54, j = 0; i < result.Length; i++, j++) //54 byte is BMP header Length
            {
                realRgb[j] = result[i];
            }

            return realRgb;


            /*
            //এই Solution টা মাইক্রোসফট এর ওয়েবসাইট থেকে সংগ্রহ  করেছি... এইভাবেও Bitmap Data থেকে RGB Data তে Convert করা যায়। 
            //
            // Lock the bitmap's bits.  
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            System.Drawing.Imaging.BitmapData bmpData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmap.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(bmpData.Stride) * bitmap.Height;
             byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            // Set every third value to 255. A 24bpp bitmap will look red.  
            for (int counter = 2; counter < rgbValues.Length; counter += 3)
                rgbValues[counter] = 0;

            // Copy the RGB values back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);

            // Unlock the bits.
            bitmap.UnlockBits(bmpData);

            return rgbValues;*/

        }


        public byte[] ConvertBitMapToRGB24ByteArray_WithBmpHeader(Bitmap bitmap)
        {
            //First Solution
            //এইটা হচ্ছে Bitmap থেকে RGB byte পাওয়ার সবচেয়ে সহজ পদ্ধতি। 
            //

            byte[] result = null;
            if (bitmap != null)
            {
                MemoryStream stream = new MemoryStream();
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
                result = stream.ToArray();
            }
            return result;
        }

        

        public static void AppendAllBytes(string path, byte[] bytes)
        {
            //argument-checking here.

            using (var stream = new FileStream(path, FileMode.Append))
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        
        static byte[] clip = new byte[896];
        bool bInit = false;

        static void InitClip() 
        {
	       //memset(clip, 0, 320);
            for(int i=0;i<320;i++)
            {
                clip[i] = 0;
            }
	        for (int i = 0; i<256; ++i) 
            {
                clip[i + 320] = (byte)i;
            }
            for(int i=320+256;i<(320+256+320);i++)
            {
                clip[i] = 255;
            }
	        //memset(clip + 320 + 256, 255, 320);
        }

        static  byte Clip(int x)
        {
	        return clip[320 + ((x + 0x8000) >> 16)];
        }


        int ConvertRGB24ToI420(byte[] lpIndata, byte[] lpOutdata)
        {
	        

	        if (!bInit)
	        {
		        bInit = true;
		        InitClip();
	        }

	        const int cyb = (int)(0.114 * 219 / 255 * 65536 + 0.5);
	        const int cyg = (int)(0.587 * 219 / 255 * 65536 + 0.5);
	        const int cyr = (int)(0.299 * 219 / 255 * 65536 + 0.5);

	        int py = 0;
	        int pu =  m_iVideoWidth * m_iVideoHeight;
	        int pv = pu + m_iVideoWidth*m_iVideoHeight / 4;

	        for (int row = 0; row < m_iVideoHeight; ++row)
	        {
		        int rgb = 0 + m_iVideoWidth * 3 * (m_iVideoHeight - 1 - row);

		        for (int col = 0; col < m_iVideoWidth; col += 2)
		        {
			        // y1 and y2 can't overflow
			        int y1 = (cyb*lpIndata[rgb + 0] + cyg*lpIndata[rgb + 1] + cyr*lpIndata[rgb + 2] + 0x108000) >> 16;
			        lpOutdata[py++] = (byte)y1;

			        int y2 = (cyb*lpIndata[rgb + 3] + cyg*lpIndata[rgb + 4] + cyr*lpIndata[rgb + 5] + 0x108000) >> 16;
			        lpOutdata[py++] = (byte)y2;

			        if ((row & 1) == 0)
			        {
				        int scaled_y = (y1 + y2 - 32) * (int)(255.0 / 219.0 * 32768 + 0.5);
				        int b_y = ((lpIndata[rgb + 0] + lpIndata[rgb + 3]) << 15) - scaled_y;
				        byte u = lpOutdata[pu++] = Clip((b_y >> 10) * (int)(1 / 2.018 * 1024 + 0.5) + 0x800000);  // u
				        int r_y = ((lpIndata[rgb + 2] + lpIndata[rgb + 5]) << 15) - scaled_y;
				        byte v = lpOutdata[pv++] = Clip((r_y >> 10) * (int)(1 / 1.596 * 1024 + 0.5) + 0x800000);  // v
			        }
			        rgb += 6;
		        }
	        }

	        return m_iVideoHeight * m_iVideoWidth * 3 / 2;
        }


        //
        //List of Render related Tasks

        private void ShowOnScreen(Bitmap b)
        {
            byte[] rgb24WithBmpHeader = ConvertBitMapToRGB24ByteArray_WithBmpHeader(b);
            MainWindow.mainWindow.ChangeImage(rgb24WithBmpHeader);
        }


        public void ChangeImage(byte[] rgbvalues)
        {
            try
            {
                BitmapImage image = convertByteArrayToBitmapImage(rgbvalues);
                if (image != null)
                {
                    MyImageSource = image;
                }
            }
            catch (Exception es)
            {
                Console.WriteLine("Exception inside ChangeImage Conversion " + es.Message);
            }
        }



        public static BitmapImage convertByteArrayToBitmapImage(byte[] byteArray)
        {  
            try
            {
                var image = new BitmapImage();
                using (var ms = new System.IO.MemoryStream(byteArray))
                {
                    image.BeginInit();
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = null;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                }
                return image;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception inside ByteArraytoBitmapImage Conversion " + ex.Message);
            }
            return null;
            
        }


        private ImageSource _myImageSource;
        public ImageSource MyImageSource
        {
            get
            {
                return _myImageSource;
            }
            set
            {
                if (value == _myImageSource)
                    return;
                _myImageSource = value;

                OnPropertyChanged("MyImageSource");
            }
        }

        #region "INotifyPropertyChanged"
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion






        //
        //User Action Button Related Tasks
        //
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //Start Button
            Console.WriteLine("Inside Start Button");

            //Start Getting Data
            videoSource.Start();


        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Inside Stop Button");
            videoSource.Stop();

        }

    }

    
}
