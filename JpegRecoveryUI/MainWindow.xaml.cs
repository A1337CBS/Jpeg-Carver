using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using JpegRecoveryUI.Models;
using System.Threading.Tasks;
using JpegRecoveryLibrary;
using Avalonia.Media.Imaging;

namespace JpegRecoveryUI
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new TxtViewModel() {
                Path = @"C:\Users\Ahmad\Desktop\QCRIInternship\Code\jpeg-carver-csharp-master\Dataset\Original\full_dragon.jpg",
                //Imagepath = new Bitmap(@"C:\Users\Ahmad\Desktop\QCRIInternship\Code\jpeg-carver-csharp-master\Dataset\Original\full_dragon.jpg")
            };



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
            if (optComboBox.SelectedIndex == 0)
            {//Jpeg carving
                Procedures p1 = new Procedures();
                p1.procedure_1(context.Path);
                context.Imagepath = new Bitmap(@"C:\Users\Ahmad\Desktop\QCRIInternship\Code\jpeg-carver-csharp-master\Dataset\Original\deagon_test_4kib_recov.jpg");
            }
            else if(optComboBox.SelectedIndex == 1)
            {//Storage carving

            }
            else if (optComboBox.SelectedIndex == 2)
            {//Network packet carving

            }
        }

    }
}
