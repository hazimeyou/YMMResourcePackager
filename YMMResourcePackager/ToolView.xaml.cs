using System.Windows.Controls;
using YMMResourcePackagerPlugin.ViewModel;

namespace YMMResourcePackagerPlugin.View
{
    public partial class ToolView : UserControl
    {
        public ToolView()
        {
            InitializeComponent();
            this.DataContext = new ToolViewModel();
        }
    }
}
