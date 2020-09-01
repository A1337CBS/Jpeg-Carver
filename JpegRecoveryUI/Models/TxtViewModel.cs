using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace JpegRecoveryUI.Models
{
    class TxtViewModel : INotifyPropertyChanged
    {
        private string _path;
        private Bitmap _imagepath;


        public string Path
        {
            get => _path;
            set
            {
                if (value != _path)
                {
                    _path = value;
                    OnPropertyChanged();
                }
            }
        }

        public Bitmap Imagepath
        {
            get => _imagepath;
            set
            {
                //var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                //var bitmap = new Bitmap(imgSrc);

                if (value != _imagepath)
                {
                    _imagepath = value;
                    OnPropertyChanged();
                }
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
