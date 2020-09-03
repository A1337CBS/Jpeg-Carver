using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using JpegRecoveryUI.Models;
using System.Threading.Tasks;
using JpegRecoveryLibrary;
using Avalonia.Media.Imaging;
using System.IO;

namespace JpegRecoveryUI
{
    public class MainWindow : Window
    {
        private ProgressBar _progress;
        private Button _run_btn;
        private Button _browse_btn;
        private TextBlock _result;
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new TxtViewModel() {
                //Path = @"C:\Users\Ahmad\Desktop\QCRIInternship\Code\jpeg-carver-csharp-master\Dataset\Original\full_dragon.jpg",
                //Imagepath = new Bitmap(@"C:\Users\Ahmad\Desktop\QCRIInternship\Code\jpeg-carver-csharp-master\Dataset\Original\full_dragon.jpg")
            };

            _progress = this.FindControl<ProgressBar>("progress");
            _run_btn = this.FindControl<Button>("runBtn");
            _browse_btn = this.FindControl<Button>("browseBtn");
            _result = this.FindControl<TextBlock>("result");


#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public async Task<string> GetPath()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                AllowMultiple = false,
                Title = "Choose a picture file to load"
                /*Filters = new List<FileDialogFilter>
                    {
                        new FileDialogFilter {Name = "Pictures", Extensions = new List<string> {"png", "jpg"}}
                    }*/
            };
            //dialog.Filters.Add(new FileDialogFilter() { Name = "Text", Extensions = { "txt" } });

            string[] result = await dialog.ShowAsync(this);

            //if (result != null)
            //{
            //    await GetPath();
            //}

            return string.Join(" ", result);
        }

        public async void Browse_Clicked(object sender, RoutedEventArgs args)
        {
            string _path = await GetPath();

            var context = this.DataContext as TxtViewModel;
            context.Path = _path;
        }

        public async void Run_Clicked(object sender, RoutedEventArgs args)
        {
            var optComboBox = this.Find<ComboBox>("optComboBox");
            var context = this.DataContext as TxtViewModel;
            string outMsg = "";
            _result.Text = "";
            var watch = System.Diagnostics.Stopwatch.StartNew();
            _progress.Value = 10;
            _run_btn.IsEnabled = false;
            _browse_btn.IsEnabled = false;


            if (optComboBox.SelectedIndex == 0)
            {//Jpeg carving
                Procedures p1 = new Procedures();
                string outFile = "";
                try
                {
                    await Task.Run(() => {
                        var result = p1.procedure_1(context.Path);
                        outFile = result.Item1;
                        outMsg = result.Item2;
                    });
                }
                catch (System.Exception e)
                {
                    
                }
               
                if (File.Exists(outFile))
                {
                    context.Imagepath = new Bitmap(outFile);
                }
                
            }
            else if(optComboBox.SelectedIndex == 1)
            {//Storage carving
                Procedures p2 = new Procedures();
                string outFile = "";
                try
                {

                    await Task.Run(() => {
                        var result = p2.procedure_2(context.Path);
                        outFile = result.Item1;
                        outMsg = result.Item2=="Success"?"Check output image fragments in input path":result.Item2;
                    });
                }
                catch (System.Exception e)
                {

                }

            }
            else if (optComboBox.SelectedIndex == 2)
            {//Network packet carving
                Procedures p3 = new Procedures();
                string outFile = "";
                try
                {
                    await Task.Run(() => {
                        var result = p3.procedure_3(context.Path);
                        outFile = result.Item1;
                        outMsg = result.Item2 == "Success" ? "Check output image fragments in input path" : result.Item2;
                    });
                    
                }
                catch (System.Exception e)
                {

                }
            }

            _result.Text = outMsg;
            _run_btn.IsEnabled = true;
            _browse_btn.IsEnabled = true;
            _progress.Value = 100;
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
        }

    }
}
