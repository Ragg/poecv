using System.Windows;
using System.Windows.Controls;

namespace PoECV
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //DataContext = new ConversationsViewModel();
        }

        private void TreeView_OnSelectedItemChanged(object sender,
            RoutedPropertyChangedEventArgs<object> e)
        {
            var vm = (ConversationsViewModel) DataContext;
            var view = (TreeView) sender;
            var item = (TreeViewItem) view.SelectedValue;
            vm.SelectedNode = (NodeEntry) item.Header;
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }
    }
}