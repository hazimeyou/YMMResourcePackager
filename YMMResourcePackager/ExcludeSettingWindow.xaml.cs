using System.Collections.Generic;
using System.Windows;
using YMMResourcePackagerPlugin.Models;

namespace YMMResourcePackager
{
    public partial class ExcludeSettingWindow : Window
    {
        public List<ExcludeItem> ExcludeItems { get; private set; }

        public ExcludeSettingWindow(List<ExcludeItem> items)
        {
            InitializeComponent();
            ExcludeItems = items;
            ExcludeListView.ItemsSource = ExcludeItems;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in ExcludeItems)
            {
                item.IsExcluded = true;
            }
            ExcludeListView.Items.Refresh();
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in ExcludeItems)
            {
                item.IsExcluded = false;
            }
            ExcludeListView.Items.Refresh();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
