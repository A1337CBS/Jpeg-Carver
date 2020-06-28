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

        public string imgSrc
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
