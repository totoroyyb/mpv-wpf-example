using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace mpv_wpf_example
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string FilePath;
        private const int MPV_FORMAT_STRING = 1;
        private const int MPV_FORMAT_DOUBLE = 5;
        private IntPtr _mpvHandle;
        private bool isPaused = false;
        private double duration;

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Imports
        private const string libmpv = "mpv.dll";

        [DllImport(libmpv, EntryPoint = "mpv_create", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr _mpvCreate();

        [DllImport(libmpv, EntryPoint = "mpv_initialize", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _mpvInitialize(IntPtr mpvHandle);

        [DllImport(libmpv, EntryPoint = "mpv_command", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _mpvCommand(IntPtr mpvHandle, IntPtr strings);

        [DllImport(libmpv, EntryPoint = "mpv_terminate_destroy", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _mpvTerminateDestroy(IntPtr mpvHandle);

        [DllImport(libmpv, EntryPoint = "mpv_set_option", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _mpvSetOption(IntPtr mpvHandle, byte[] name, int format, ref long data);

        [DllImport(libmpv, EntryPoint = "mpv_set_option_string", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _mpvSetOptionString(IntPtr mpvHandle, byte[] name, byte[] value);

        [DllImport(libmpv, EntryPoint = "mpv_get_property_string", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _mpvGetPropertyString(IntPtr mpvHandle, byte[] name, int format, ref IntPtr data);

        [DllImport(libmpv, EntryPoint = "mpv_set_property", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _mpvSetProperty(IntPtr mpvHandle, byte[] name, int format, ref byte[] data);

        [DllImport(libmpv, EntryPoint = "mpv_free", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _mpvFree(IntPtr data);
        #endregion Imports

        public void Pause()
        {
            if (_mpvHandle == IntPtr.Zero)
                return;

            var bytes = GetUtf8Bytes("yes");
            _mpvSetProperty(_mpvHandle, GetUtf8Bytes("pause"), MPV_FORMAT_STRING, ref bytes);
        }

        private void Play()
        {
            if (_mpvHandle == IntPtr.Zero)
                return;

            var bytes = GetUtf8Bytes("no");
            _mpvSetProperty(_mpvHandle, GetUtf8Bytes("pause"), MPV_FORMAT_STRING, ref bytes);
        }

        public bool IsPaused()
        {
            if (_mpvHandle == IntPtr.Zero)
                return true;

            var lpBuffer = IntPtr.Zero;
            _mpvGetPropertyString(_mpvHandle, GetUtf8Bytes("pause"), MPV_FORMAT_STRING, ref lpBuffer);
            //var isPaused = Marshal.PtrToStringAnsi(lpBuffer) == "yes";
            _mpvFree(lpBuffer);
            return isPaused;
        }

        public void SetTime(double value)
        {
            if (_mpvHandle == IntPtr.Zero)
                return;

            DoMpvCommand("seek", value.ToString(CultureInfo.InvariantCulture), "absolute");
        }

        private static byte[] GetUtf8Bytes(string s)
        {
            return Encoding.UTF8.GetBytes(s + "\0");
        }

        public static IntPtr AllocateUtf8IntPtrArrayWithSentinel(string[] arr, out IntPtr[] byteArrayPointers)
        {
            int numberOfStrings = arr.Length + 1; // add extra element for extra null pointer last (sentinel)
            byteArrayPointers = new IntPtr[numberOfStrings];
            IntPtr rootPointer = Marshal.AllocCoTaskMem(IntPtr.Size * numberOfStrings);
            for (int index = 0; index < arr.Length; index++)
            {
                var bytes = GetUtf8Bytes(arr[index]);
                IntPtr unmanagedPointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, unmanagedPointer, bytes.Length);
                byteArrayPointers[index] = unmanagedPointer;
            }
            Marshal.Copy(byteArrayPointers, 0, rootPointer, numberOfStrings);
            return rootPointer;
        }

        private void DoMpvCommand(params string[] args)
        {
            IntPtr[] byteArrayPointers;
            var mainPtr = AllocateUtf8IntPtrArrayWithSentinel(args, out byteArrayPointers);
            _mpvCommand(_mpvHandle, mainPtr);
            foreach (var ptr in byteArrayPointers)
            {
                Marshal.FreeHGlobal(ptr);
            }
            Marshal.FreeHGlobal(mainPtr);
        }

        private void buttonPlay_Click(object sender, EventArgs e)
        {
            if (_mpvHandle != IntPtr.Zero)
                _mpvTerminateDestroy(_mpvHandle);

            _mpvHandle = _mpvCreate();
            if (_mpvHandle == IntPtr.Zero)
                return;

            _mpvInitialize(_mpvHandle);
            _mpvSetOptionString(_mpvHandle, GetUtf8Bytes("keep-open"), GetUtf8Bytes("always"));
            int mpvFormatInt64 = 4;
            var windowId = MediaBox.Handle.ToInt64();
            _mpvSetOption(_mpvHandle, GetUtf8Bytes("wid"), mpvFormatInt64, ref windowId);
            DoMpvCommand("loadfile", FilePath);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_mpvHandle != IntPtr.Zero)
                _mpvTerminateDestroy(_mpvHandle);
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "All Files(*.*)|*.*"; 

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                FilePath = dlg.FileName;
                buttonPlay_Click(null, null);
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsPaused())
            {
                Play();
            }
            else
            {
                Pause();
            }
            isPaused = !isPaused;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            Pause();
            SetTime(0);
        }

        private void VideoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double percentage = (int)(VideoSlider.Value / 10);

            SeekByPercentage(percentage);
        }

        private void SeekByPercentage(double percentage)
        {
            if (_mpvHandle == IntPtr.Zero)
                return;

            DoMpvCommand("seek", percentage.ToString(CultureInfo.InvariantCulture), "absolute-percent");
        }

        //private double getDouble(string name)
        //{
        //    double data = new Double();
            
        //    IntPtr dataPtr = ToPtr(data);
        //    _mpvGetPropertyString(_mpvHandle, GetUtf8Bytes(name), MPV_FORMAT_DOUBLE, ref data);
        //    double test = ToDouble(dataPtr);

        //    return data;
        //}

        //private void DurationButton_Click(object sender, RoutedEventArgs e)
        //{
        //    duration = getDouble("duration");
        //    DurationBlock.Text = duration.ToString();
        //}

        //public static double ToDouble(IntPtr mem1)
        //{
        //    if (mem1 != IntPtr.Zero)
        //    {
        //        byte[] ba = new byte[sizeof(double)];

        //        for (int i = 0; i < ba.Length; i++)
        //            ba[i] = Marshal.ReadByte(mem1, i);
        //        double v = BitConverter.ToDouble(ba, 0);
        //        return v;
        //    }
        //    return 0;
        //}

        //public static IntPtr ToPtr(double val)
        //{
        //    IntPtr ptr = Marshal.AllocHGlobal(sizeof(double));

        //    byte[] byteDouble = BitConverter.GetBytes(val);
        //    Marshal.Copy(byteDouble, 0, ptr, byteDouble.Length);
        //    return ptr;
        //}
    }
}
