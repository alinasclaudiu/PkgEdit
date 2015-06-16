using PkgEdit.Model;
using System;
using System.IO;
using System.Windows.Input;

namespace PkgEdit.ViewModel
{
    class PkgViewModel : ViewModelBase
    {
        private Pkg pkg;
        public Pkg Pkg
        {
            get { return pkg; }
            set { SetField(ref pkg, value); }
        }

        private int listIndex;
        public int ListIndex
        {
            get { return listIndex; }
            set { SetField(ref listIndex, value); }
        }

        public ICommand OpenFile { get { return new RelayCommand(OpenPkgExecute); } }
        public ICommand SaveFile { get { return new RelayCommand(SavePkgExecute); } }
        public ICommand ExitApp { get { return new RelayCommand(ExitAppExecute); } }
        public ICommand ExportItem { get { return new RelayCommand(ExportItemExecute, ExportItemCanExecute); } }
        public ICommand InsertElement { get { return new RelayCommand<string>(InsertItemExecute); } }
        public ICommand RemoveItem { get { return new RelayCommand(RemoveItemExecute, RemoveItemCanExecute); } }
        public ICommand MoveUp { get { return new RelayCommand(MoveUpExecute, MoveCanExecute); } }
        public ICommand MoveDown { get { return new RelayCommand(MoveDownExecute, MoveCanExecute); } }

        public PkgViewModel()
        {
            Pkg = new Pkg();
        }

        private void OpenPkgExecute()
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.DefaultExt = ".pkg";
            openFileDialog.Filter = "PKG Files (*.pkg)|*.pkg";
            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                Pkg = new Pkg();
                GC.Collect();
                Pkg.LoadPackageFromFile(openFileDialog.FileName);
                GC.Collect();
            }
        }

        private void SavePkgExecute()
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.FileName = "OS_Package.pkg";
            bool? result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                GC.Collect();
                Pkg.SavePackageToFile(saveFileDialog.FileName);
                GC.Collect();
            }
        }

        private void ExitAppExecute()
        {
            App.Current.Shutdown();
        }

        private void ExportItemExecute()
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.FileName = Pkg.Chunks[ListIndex].GetPreferredFileName();
            bool? result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                FileStream f = new FileStream(saveFileDialog.FileName, FileMode.Create, FileAccess.Write);
                Pkg.Chunks[ListIndex].ExportDataToStream(f);
                f.Close();
            }
        }

        private bool ExportItemCanExecute()
        {
            if ((ListIndex >= 0) && (ListIndex < Pkg.Chunks.Count) && (Pkg.Chunks.Count > 0) && !(Pkg.Chunks[ListIndex] is PkgDirectory))
                return true;
            else
                return false;
        }

        private void InsertItemExecute(string s)
        {
            int i;
            if (ListIndex < 0)
                i = 0;
            else if (ListIndex >= pkg.Chunks.Count)
                i = ListIndex;
            else
                i = ListIndex + 1;
            switch (s)
            {
                case "Header":
                    pkg.Chunks.Insert(i, new PkgHeader());
                    break;
                case "Kernel 1":
                    pkg.Chunks.Insert(i, new PkgKernel1());
                    break;
                case "Ramdisk":
                    pkg.Chunks.Insert(i, new PkgRamdisk());
                    break;
                case "Installer":
                    pkg.Chunks.Insert(i, new PkgInstaller());
                    break;
                case "Installer Xml":
                    pkg.Chunks.Insert(i, new PkgInstallerXml());
                    break;
                case "Installer Sh":
                    pkg.Chunks.Insert(i, new PkgInstallerSh());
                    break;
                case "Kernel 2":
                    pkg.Chunks.Insert(i, new PkgKernel2());
                    break;
                case "Directory":
                    pkg.Chunks.Insert(i, new PkgDirectory());
                    break;
                case "File":
                    pkg.Chunks.Insert(i, new PkgFile());
                    break;
                case "File System":
                    pkg.Chunks.Insert(i, new PkgFileSystem());
                    break;
            }
        }

        private void RemoveItemExecute()
        {
            int i = ListIndex;
            if((ListIndex >= 0) && (ListIndex < Pkg.Chunks.Count))
                Pkg.Chunks.RemoveAt(ListIndex);
            ListIndex = i;
        }

        private bool RemoveItemCanExecute()
        {
            if ((ListIndex >= 0) && (ListIndex < Pkg.Chunks.Count) && (Pkg.Chunks.Count > 0))
                return true;
            else
                return false;
        }

        private void MoveUpExecute()
        {
            int tmpIndex = ListIndex;
            if (tmpIndex > 0)
            {
                PkgChunk tmp = Pkg.Chunks[tmpIndex];
                Pkg.Chunks[tmpIndex] = Pkg.Chunks[tmpIndex - 1];
                Pkg.Chunks[tmpIndex - 1] = tmp;
                ListIndex = tmpIndex - 1;
            }
        }

        private void MoveDownExecute()
        {
            int tmpIndex = ListIndex;
            if (tmpIndex < (Pkg.Chunks.Count - 1))
            {
                PkgChunk tmp = Pkg.Chunks[tmpIndex];
                Pkg.Chunks[tmpIndex] = Pkg.Chunks[tmpIndex + 1];
                Pkg.Chunks[tmpIndex + 1] = tmp;
                ListIndex = tmpIndex + 1;
            }
        }

        private bool MoveCanExecute()
        {
            if ((ListIndex >= 0) && (ListIndex < Pkg.Chunks.Count) && (Pkg.Chunks.Count >= 2))
                return true;
            else
                return false;
        }
    }
}
