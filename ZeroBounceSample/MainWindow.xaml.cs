using System;
using System.Diagnostics;
using System.Windows;
using ZeroBounceSDK;

namespace ZeroBounceSample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            ZeroBounce.Instance.Initialize("<YOUR_API_KEY>");
            Debug.WriteLine("ZeroBounce Initialized");
        }

        private void ValidateButton_OnClick(object sender, RoutedEventArgs e)
        {
            ZeroBounce.Instance.Validate("<EMAIL_TO_TEST>", null,
                response => Debug.WriteLine("Validate success response " + response),
                error => Debug.WriteLine("Validate failure error " + error));
        }

        private void CreditsButton_OnClick(object sender, RoutedEventArgs e)
        {
            ZeroBounce.Instance.GetCredits(
                response => Debug.WriteLine("GetCredits success response " + response),
                error => Debug.WriteLine("GetCredits failure error " + error));
        }

        private void SendFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            ZeroBounce.Instance.SendFile("./email_file.csv", 1,
                new ZeroBounce.SendFileOptions
                {
                    FirstNameColumn = 2, LastNameColumn = 3, HasHeaderRow = true
                },
                response => Debug.WriteLine("SendFile success response " + response),
                error => Debug.WriteLine("SendFile failure error " + error));
        }

        private void FileStatusButton_OnClick(object sender, RoutedEventArgs e)
        {
            ZeroBounce.Instance.FileStatus("<YOUR_FILE_ID>", 
                response => Debug.WriteLine("FileStatus success response " + response),
                error => Debug.WriteLine("FileStatus failure error " + error));
        }

        private void GetFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            ZeroBounce.Instance.GetFile(
                "<YOUR_FILE_ID>", 
                "./downloads/file.csv", 
                response => Debug.WriteLine("GetFile success response " + response), 
                error => Debug.WriteLine("GetFile failure error " + error));
        }

        private void ApiUsageButton_OnClick(object sender, RoutedEventArgs e)
        {
            var startDate = DateTime.Now.AddDays(-5);
            var endDate = DateTime.Now;
            ZeroBounce.Instance.GetApiUsage(
                startDate, endDate, 
                response => Debug.WriteLine("GetApiUsage success response " + response), 
                error => Debug.WriteLine("GetApiUsage failure error " + error));
        }

        private void DeleteFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            ZeroBounce.Instance.DeleteFile(
                "<YOUR_FILE_ID>", 
                response => Debug.WriteLine("DeleteFile success response " + response), 
                error => Debug.WriteLine("DeleteFile failure error " + error));
        }
    }
    
}