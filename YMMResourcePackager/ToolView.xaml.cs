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

            if (DataContext is ToolViewModel viewModel)
                await viewModel.InitializeAsync();
        }
    }
}
