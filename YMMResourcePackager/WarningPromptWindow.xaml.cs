using System.ComponentModel;

namespace YMMResourcePackager
{
    // 再表示抑制つきの、シンプルな確認ダイアログ。
    public partial class WarningPromptWindow : Window, INotifyPropertyChanged
    {
        private string _windowTitle = string.Empty;
        private string _message = string.Empty;
        private string _yesButtonText = "はい";
        private string _noButtonText = "いいえ";
        private bool _suppressThisWarning;

        public WarningPromptWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set { _windowTitle = value; OnPropertyChanged(nameof(WindowTitle)); }
        }

        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(nameof(Message)); }
        }

        public string YesButtonText
        {
            get => _yesButtonText;
            set { _yesButtonText = value; OnPropertyChanged(nameof(YesButtonText)); }
        }

        public string NoButtonText
        {
            get => _noButtonText;
            set { _noButtonText = value; OnPropertyChanged(nameof(NoButtonText)); }
        }

        public bool SuppressThisWarning
        {
            get => _suppressThisWarning;
            set { _suppressThisWarning = value; OnPropertyChanged(nameof(SuppressThisWarning)); }
        }

        public bool? Result { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            // 確認済みとして閉じる。
            Result = true;
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            // キャンセルとして閉じる。
            Result = false;
            DialogResult = false;
            Close();
        }

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
