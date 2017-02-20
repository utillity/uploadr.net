using System.Windows;

namespace uTILLIty.UploadrNet.Windows
{
	/// <summary>
	///   Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			DataContext = new MainWindowViewModel(new FlickrManager());
		}
	}
}