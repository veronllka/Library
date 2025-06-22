using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Library.Models;
using Library.Services;

namespace Library.Views
{
    public partial class ReportsWindow : Window
    {
        private readonly ReportService _reportService;

        public ReportsWindow()
        {
            InitializeComponent();
            _reportService = new ReportService();
            InitializeWindow();
        }

        private void InitializeWindow()
        {
            StartDatePicker.SelectedDate = DateTime.Now.AddMonths(-1);
            EndDatePicker.SelectedDate = DateTime.Now;
        }

        private ReportParameters GetReportParameters(ReportType reportType)
        {
            var topCountText = (TopCountComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            int topCount = topCountText == "Все" ? 0 : int.Parse(topCountText ?? "10");

            return new ReportParameters
            {
                ReportType = reportType,
                StartDate = StartDatePicker.SelectedDate ?? DateTime.Now.AddMonths(-1),
                EndDate = EndDatePicker.SelectedDate ?? DateTime.Now,
                TopCount = topCount
            };
        }

        private async void GenerateBookPopularityReport_Click(object sender, RoutedEventArgs e)
        {
            await GenerateReportAsync(ReportType.BookPopularity);
        }

        private async void GenerateReaderActivityReport_Click(object sender, RoutedEventArgs e)
        {
            await GenerateReportAsync(ReportType.ReaderActivity);
        }

        private async void GenerateGenreStatisticsReport_Click(object sender, RoutedEventArgs e)
        {
            await GenerateReportAsync(ReportType.GenreStatistics);
        }

        private async Task GenerateReportAsync(ReportType reportType)
        {
            try
            {
                StatusText.Text = "Генерация отчета...";
                SetButtonsEnabled(false);

                var parameters = GetReportParameters(reportType);
                var report = await _reportService.GenerateReportAsync(parameters);

                DisplayReport(report);
                StatusText.Text = "Отчет успешно сгенерирован";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при генерации отчета: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка генерации отчета";
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            BookPopularityButton.IsEnabled = enabled;
            ReaderActivityButton.IsEnabled = enabled;
            GenreStatisticsButton.IsEnabled = enabled;
        }

        private void DisplayReport(BaseReport report)
        {
            ReportContentPanel.Children.Clear();
            WelcomeText.Visibility = Visibility.Collapsed;

            // Заголовок отчета
            var titleBlock = new TextBlock
            {
                Text = report.Title,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            ReportContentPanel.Children.Add(titleBlock);

            var descBlock = new TextBlock
            {
                Text = report.Description,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 5)
            };
            ReportContentPanel.Children.Add(descBlock);

            var periodBlock = new TextBlock
            {
                Text = $"Период: {report.Period}",
                FontSize = 12,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 20)
            };
            ReportContentPanel.Children.Add(periodBlock);

            // Отображение данных в зависимости от типа отчета
            switch (report)
            {
                case BookPopularityReport bookReport:
                    DisplayBookPopularityReport(bookReport);
                    break;
                case ReaderActivityReport readerReport:
                    DisplayReaderActivityReport(readerReport);
                    break;
                case OverdueLoansReport overdueReport:
                    DisplayOverdueLoansReport(overdueReport);
                    break;
                case GenreStatisticsReport genreReport:
                    DisplayGenreStatisticsReport(genreReport);
                    break;
            }
        }

        private void DisplayBookPopularityReport(BookPopularityReport report)
        {
            var statsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            
            var totalLoansBlock = new TextBlock
            {
                Text = $"📊 Всего выдач: {report.TotalLoans}",
                FontSize = 14,
                Margin = new Thickness(0, 0, 20, 0)
            };
            statsPanel.Children.Add(totalLoansBlock);

            var booksCountBlock = new TextBlock
            {
                Text = $"📚 Книг проанализировано: {report.Books.Count}",
                FontSize = 14
            };
            statsPanel.Children.Add(booksCountBlock);

            ReportContentPanel.Children.Add(statsPanel);

            var dataGrid = new DataGrid
            {
                ItemsSource = report.Books,
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Height = 400,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                AlternatingRowBackground = Brushes.LightGray
            };

            dataGrid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding("КнигаID"), Width = 60 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Название", Binding = new Binding("Название"), Width = 350 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Автор", Binding = new Binding("АвторИмя"), Width = 280 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Жанр", Binding = new Binding("ЖанрНазвание"), Width = 150 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Выдач", Binding = new Binding("КоличествоВыдач"), Width = 100 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Статус", Binding = new Binding("Статус"), Width = 200 });

            ReportContentPanel.Children.Add(dataGrid);
        }

        private void DisplayReaderActivityReport(ReaderActivityReport report)
        {
            var statsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            
            var activeReadersBlock = new TextBlock
            {
                Text = $"👥 Активных читателей: {report.TotalActiveReaders}",
                FontSize = 14,
                Margin = new Thickness(0, 0, 20, 0)
            };
            statsPanel.Children.Add(activeReadersBlock);

            var avgLoansBlock = new TextBlock
            {
                Text = $"📈 Среднее выдач: {report.AverageLoansPerReader:F1}",
                FontSize = 14
            };
            statsPanel.Children.Add(avgLoansBlock);

            ReportContentPanel.Children.Add(statsPanel);

            var dataGrid = new DataGrid
            {
                ItemsSource = report.Readers,
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Height = 400,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                AlternatingRowBackground = Brushes.LightGray
            };

            dataGrid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding("ЧитательID"), Width = 60 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "ФИО", Binding = new Binding("ИмяЧитателя"), Width = 300 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Телефон", Binding = new Binding("Телефон"), Width = 150 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Выдач", Binding = new Binding("КоличествоВыдач"), Width = 100 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Активных", Binding = new Binding("АктивныхВыдач"), Width = 120 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Последняя выдача", Binding = new Binding("ПоследняяВыдача") { StringFormat = "dd.MM.yyyy" }, Width = 180 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Статус", Binding = new Binding("СтатусАктивности"), Width = 150 });

            ReportContentPanel.Children.Add(dataGrid);
        }

        private void DisplayOverdueLoansReport(OverdueLoansReport report)
        {
            var statsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            
            var overdueCountBlock = new TextBlock
            {
                Text = $"⚠️ Просрочек: {report.TotalOverdueLoans}",
                FontSize = 14,
                Margin = new Thickness(0, 0, 20, 0)
            };
            statsPanel.Children.Add(overdueCountBlock);

            var fineAmountBlock = new TextBlock
            {
                Text = $"💰 Штрафы: {report.TotalOverdueFine:C}",
                FontSize = 14
            };
            statsPanel.Children.Add(fineAmountBlock);

            ReportContentPanel.Children.Add(statsPanel);

            var dataGrid = new DataGrid
            {
                ItemsSource = report.OverdueLoans,
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Height = 400,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                AlternatingRowBackground = Brushes.LightGray
            };

            dataGrid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding("ВыдачаID"), Width = 60 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Книга", Binding = new Binding("НазваниеКниги"), Width = 300 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Читатель", Binding = new Binding("ИмяЧитателя"), Width = 200 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Телефон", Binding = new Binding("Телефон"), Width = 150 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Дата выдачи", Binding = new Binding("ДатаВыдачи") { StringFormat = "dd.MM.yyyy" }, Width = 120 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Дата возврата", Binding = new Binding("ДатаВозврата") { StringFormat = "dd.MM.yyyy" }, Width = 120 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Дней просрочки", Binding = new Binding("ДнейПросрочки"), Width = 120 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Штраф", Binding = new Binding("РазмерШтрафа") { StringFormat = "C" }, Width = 100 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Статус", Binding = new Binding("СтатусШтрафа"), Width = 150 });

            ReportContentPanel.Children.Add(dataGrid);
        }

        private void DisplayGenreStatisticsReport(GenreStatisticsReport report)
        {
            var statsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            
            var totalGenresBlock = new TextBlock
            {
                Text = $"📚 Жанров: {report.TotalGenres}",
                FontSize = 14,
                Margin = new Thickness(0, 0, 20, 0)
            };
            statsPanel.Children.Add(totalGenresBlock);

            var totalLoansBlock = new TextBlock
            {
                Text = $"📊 Всего выдач: {report.TotalLoans}",
                FontSize = 14
            };
            statsPanel.Children.Add(totalLoansBlock);

            ReportContentPanel.Children.Add(statsPanel);

            var dataGrid = new DataGrid
            {
                ItemsSource = report.Genres,
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Height = 400,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                AlternatingRowBackground = Brushes.LightGray
            };

            dataGrid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding("ЖанрID"), Width = 60 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Название жанра", Binding = new Binding("НазваниеЖанра"), Width = 200 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Книг", Binding = new Binding("КоличествоКниг"), Width = 100 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Выдач", Binding = new Binding("КоличествоВыдач"), Width = 100 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Процент (%)", Binding = new Binding("Процент") { StringFormat = "F1" }, Width = 120 });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Популярность", Binding = new Binding("ПопулярностьУровень"), Width = 150 });

            ReportContentPanel.Children.Add(dataGrid);
        }
    }
} 