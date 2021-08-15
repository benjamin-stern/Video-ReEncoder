using Microsoft.WindowsAPICodePack.Dialogs;
using ReEncode.Encode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using System.Windows.Shapes;

namespace ReEncode
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            log4net.Config.XmlConfigurator.Configure(new System.IO.FileInfo("log4net.config"));

            //https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full.7z


            //using (WebClient client = new WebClient())
            //{
            //    client.DownloadFileAsync(new Uri("https://www.gyan.dev/ffmpeg/builds/ffmpeg-git-full.7z"), "ffmpeg-git-full.7z");
            //}

            encodeFormatComboBox.ItemsSource = Enum.GetValues(typeof(TargetEncodeFormat)).Cast<TargetEncodeFormat>();
            encodeFormatComboBox.SelectedItem = TargetEncodeFormat.H265xNVidia;
            Application.Current.Exit += (object sender, ExitEventArgs e) =>
            {
                manager?.StopProcessing();
            };

            var lastTask = ConfigData.Instance.GetLatestTask();
            if (lastTask != null) {
                loadFolderConfigData(inputPath.Text = lastTask.InputPath);
            }

            Log.i.Debug($"{GetType().Name}: Starting Up Application");
        }

        private void loadFolderConfigData(string inputPath) {
            var task = ConfigData.Instance.GetTask(inputPath);
            vmafTarget.Text = task.Data.VMAFTarget.ToString();
            vmafOvershootPct.Text = task.Data.VMAFOvershootPercent.ToString();
            encodeFormatComboBox.SelectedItem = task.Data.EncodeType;
            outputPath.Text = task.OutputPath ?? outputPath.Text;
        }

        private void inputPath_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            inputPath.Text = GetUserTargetDirectoryPath() ?? inputPath.Text;
            if (!string.IsNullOrEmpty(inputPath.Text)) {
                loadFolderConfigData(inputPath.Text);
            }
        }
        private string GetUserTargetDirectoryPath() {
            string result = null;

            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.EnsurePathExists = true;
            dialog.ShowPlacesList = true;
            dialog.InitialDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";//My Computer
            CommonFileDialogResult dialogueResult = dialog.ShowDialog();
            if (dialogueResult == CommonFileDialogResult.Ok)
            {
                result = dialog.FileName;
            }

            return result;
        }
        private void vmafOvershootPct_LostFocus(object sender, RoutedEventArgs e)
        {
            float value = 1;
            float.TryParse(vmafOvershootPct.Text, out value);
            float baseAmount = int.Parse(vmafTarget.Text); 
            value = Math.Min(100-baseAmount, Math.Max(value, 1));
            vmafOvershootPct.Text = value.ToString();
        }

        private void vmafTarget_LostFocus(object sender, RoutedEventArgs e)
        {
            float value = 95;
            float.TryParse(vmafTarget.Text, out value);
            value = Math.Min(99, Math.Max(value, 60));
            vmafTarget.Text = value.ToString();
        }

        private EncodeManager manager;
        private int currentFileCount;
        private int totalFileCount;

        private void startAction_Click(object sender, RoutedEventArgs e)
        {
            if (manager == null && !String.IsNullOrWhiteSpace(inputPath.Text) && !String.IsNullOrWhiteSpace(outputPath.Text)) {
                manager = new EncodeManager {
                    InputPath = inputPath.Text,
                    OutputPath = outputPath.Text,
                    EncodeFormat = (TargetEncodeFormat)encodeFormatComboBox.SelectedItem,
                    VMAFTarget = float.Parse(vmafTarget.Text),
                    VMAFOvershoot = float.Parse(vmafOvershootPct.Text)
                };

                manager.AllTasksCompleted += () => UIThreadActionCallback(()=>{
                    infoProcessingBlock.Text = $"Completed";
                    stopManager();
                });

                manager.CurrentProcessedChanged += (amount) => UIThreadActionCallback(() => {
                    currentFileCount = amount;
                    UpdateProcessingInfo();
                });

                manager.TotalToProcessChanged += (amount) => UIThreadActionCallback(() => {
                    totalFileCount = amount;
                    UpdateProcessingInfo();
                });


                startAction.IsEnabled = false;
                stopAction.IsEnabled = true;
                encodeFormatComboBox.IsEnabled = false;
                vmafTarget.IsEnabled = false;
                vmafOvershootPct.IsEnabled = false;
                inputPath.IsEnabled = false;
                outputPath.IsEnabled = false;

                manager.StartProcessing();
            }
        }

        private void UIThreadActionCallback(Action a) {
            Application.Current.Dispatcher.Invoke(new Action(() => a()));
        }

        private void stopAction_Click(object sender, RoutedEventArgs e)
        {
            stopManager();
        }

        private void UpdateProcessingInfo() {
            infoProcessingBlock.Text = $"Processing {currentFileCount}/{totalFileCount}";
            if (totalFileCount > 0)
            {
                progress.Value = ((double)currentFileCount / (double)totalFileCount) * 100;
            }
        }

        private void stopManager() {
            manager?.StopProcessing();
            manager = null;

            startAction.IsEnabled = true;
            stopAction.IsEnabled = false;

            encodeFormatComboBox.IsEnabled = true;
            vmafTarget.IsEnabled = true;
            vmafOvershootPct.IsEnabled = true;
            inputPath.IsEnabled = true;
            outputPath.IsEnabled = true;
            progress.Value = 0;
        }
    }
}
