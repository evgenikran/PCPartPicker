using Microsoft.EntityFrameworkCore;
using PcBuilder.Core.Models;
using PcBuilder.Core.Profiles;
using PcBuilder.Core.Repositories;
using PcBuilder.Core.Services;
using PcBuilder.Infrastructure.Data;
using PcBuilder.Infrastructure.Repositories;
using System;
using System.Text;
using System.Windows;



namespace PcBuilder.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly IBuildGenerator _generator;

        public MainWindow()
        {
            InitializeComponent();

            var options = new DbContextOptionsBuilder<PcBuilderDbContext>()
                .UseSqlServer("Server=DESKTOP-EQN3H9P\\MSSQLSERVER01;Database=PcBuilderDb;Trusted_Connection=True;TrustServerCertificate=True;")
                .Options;

            var context = new PcBuilderDbContext(options);
            IPartRepository repo = new EfPartRepository(context);
            _generator = new BuildGenerator(repo);


            WorkloadComboBox.Items.Add("Gaming");
            WorkloadComboBox.Items.Add("Video Editing");
            WorkloadComboBox.Items.Add("AI");

            WorkloadComboBox.SelectedIndex = 0;
        }

        
        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            OutputTextBox.Text = "Build generation logic not implemented yet.";
            if (!decimal.TryParse(BudgetTextBox.Text, out decimal budget))
            {
                MessageBox.Show("Invalid budget.");
                return;
            }

            WorkloadProfile profile =
                WorkloadComboBox.SelectedIndex switch
                {
                    0 => WorkloadProfiles.Gaming,
                    1 => WorkloadProfiles.VideoEditing,
                    2 => WorkloadProfiles.AI,
                    _ => WorkloadProfiles.Gaming
                };

            var builds = _generator.Generate(budget, profile);

            var sb = new StringBuilder();

            foreach (var build in builds)
            {
                sb.AppendLine($"Build type: {build.BuildType}");
                sb.AppendLine($"Total price: {build.TotalPrice}");

                foreach (var part in build.Parts)
                {
                    sb.AppendLine($"- {part.Type}: {part.Name} (${part.Price})");
                }

                sb.AppendLine();
            }

            OutputTextBox.Text = sb.ToString();
        }
    }
}
