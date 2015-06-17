using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PkgEdit.Model;
using System.Windows.Input;
using System.IO;
using System.Globalization;

namespace PkgEdit.ViewModel
{
    public class FileEditViewModel : ViewModelBase
    {
        private PkgFile pkgFile;
        public PkgFile PkgFile
        {
            get { return pkgFile; }
            set { SetField(ref pkgFile, value); }
        }

        public string Permissions
        {
            get
            {
                return PkgFile.Permissions.ToString("X4");
            }
            set
            {
                ushort tmp;
                if (ushort.TryParse(value, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out tmp))
                    PkgFile.Permissions = tmp;
            }
        }

        public ICommand Import { get { return new RelayCommand(ImportExecute); } }
        public ICommand Export { get { return new RelayCommand(ExportExecute); } }

        public FileEditViewModel(PkgFile pkgFile)
        {
            this.PkgFile = pkgFile;
        }

        private void ImportExecute()
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                FileStream f = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read);
                PkgFile.ImportDataFromStream(f);
                f.Close();
            }
        }

        private void ExportExecute()
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.FileName = PkgFile.GetPreferredFileName();
            bool? result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                FileStream f = new FileStream(saveFileDialog.FileName, FileMode.Create, FileAccess.Write);
                PkgFile.ExportDataToStream(f);
                f.Close();
            }
        }
    }
}
