using System;
using System.Diagnostics;
using System.Windows;

namespace RuknBoqMapper
{
    public partial class RuknBoqGeneratorWindow : Window
    {
        private readonly RuknBoqGeneratorViewModel _viewModel;

        public RuknBoqGeneratorWindow(RuknBoqGeneratorViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void LogoButton_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://www.ruknbim.com/") { UseShellExecute = true }); }
            catch { }
        }

        private void SelectAllCategories_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel?.CategoryItems == null) return;
            foreach (var item in _viewModel.CategoryItems)
            {
                item.IsSelected = true;
            }
        }

        private void SelectNoCategories_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel?.CategoryItems == null) return;
            foreach (var item in _viewModel.CategoryItems)
            {
                item.IsSelected = false;
            }
        }
    }
}
