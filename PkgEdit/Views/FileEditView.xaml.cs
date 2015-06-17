using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace PkgEdit.Views
{
    /// <summary>
    /// Interaction logic for FileEditView.xaml
    /// </summary>
    public partial class FileEditView : Window
    {
        public FileEditView()
        {
            InitializeComponent();
        }

        private void PermissionsPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            int hexNumber;
            e.Handled = !int.TryParse(e.Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out hexNumber);
        }

        private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            this.Close();
        }
    }
}
