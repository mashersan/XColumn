using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace XColumn
{
    public partial class PipWindow : Window
    {
        private readonly string _url;
        private readonly string _userDataFolder;

        public PipWindow(string url, string userDataFolder)
        {
            InitializeComponent();
            _url = url;
            _userDataFolder = userDataFolder;
            InitializeWebViewAsync();
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
                await PipWebView.EnsureCoreWebView2Async(env);
                PipWebView.CoreWebView2.Navigate(_url);
            }
            catch (Exception ex)
            {
                Logger.Log($"PiP Initialize Error: {ex.Message}");
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            PipWebView.Dispose();
            this.Close();
        }
    }
}