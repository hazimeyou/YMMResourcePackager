namespace YMMResourcePackagerPlugin.View
{
    public partial class ToolView : UserControl
    {
        private bool _startupInitialized;

        public ToolView()
        {
            InitializeComponent();
            this.DataContext = new ToolViewModel();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_startupInitialized)
                return;

            _startupInitialized = true;

            // 初回表示だけ、ViewModel の起動時チェックを走らせる。
            if (DataContext is ToolViewModel viewModel)
                await viewModel.InitializeAsync();
        }
    }
}
