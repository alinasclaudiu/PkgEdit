using System.Windows;

namespace PkgEdit
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            PkgEdit.ViewModel.PkgViewModel pvm = new ViewModel.PkgViewModel();
            MainWindow mw = new MainWindow();
            mw.DataContext = pvm;
            mw.Show();
        }
    }
}
