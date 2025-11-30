using System.Windows;

namespace XiloAdventures.Wpf.Windows
{
    public partial class AlertWindow : Window
    {
        public AlertWindow()
        {
            InitializeComponent();
        }

        public AlertWindow(string message) : this()
        {
            Title = "Aviso";
            MessageTextBlock.Text = message;
        }

        public AlertWindow(string message, string title) : this()
        {
            MessageTextBlock.Text = message;
            Title = title;
        }

        public static void Show(string message, Window? owner = null)
        {
            var w = new AlertWindow(message);
            if (owner != null)
            {
                w.Owner = owner;
            }
            w.ShowDialog();
        }

        public static void Show(string title, string message, Window? owner = null)
        {
            var w = new AlertWindow(title, message);
            if (owner != null)
            {
                w.Owner = owner;
            }
            w.ShowDialog();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
