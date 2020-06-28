using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using JpegRecoveryUI.Models;
using System.Threading.Tasks;
using System;

namespace JpegRecoveryUI
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new TxtViewModel() { Path = @"C:\Users\Ahmad\Desktop\sign.jpg" };
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
            OpenFileDialog dialog = new OpenFileDialog();
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
            
            System.Console.WriteLine("Hello");
            var context = this.DataContext as TxtViewModel;
            context.Path = "Hello";
       
        }

    }
}
