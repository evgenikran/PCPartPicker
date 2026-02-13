using Microsoft.EntityFrameworkCore;
using PcBuilder.Infrastructure.Data;

using System.Text;
using System;
using System.Text;
using System.Windows;
using PcBuilder.Core.Repositories;
using PcBuilder.Core.Services;
using PcBuilder.Core.Profiles;
using PcBuilder.Core.Models;

namespace PcBuilder.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly IBuildGenerator _generator;

        public MainWindow()
        {
            InitializeComponent();

            IPartRepository repo =
                new JsonPartRepository("Data/parts.json");

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
