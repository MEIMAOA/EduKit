using System.Windows;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class LauncherEditWindow : Window
    {
        public LauncherEditWindow(LaunchItem existing)
        {
            InitializeComponent();

            if (existing == null)
            {
                Item = new LaunchItem();
            }
            else
            {
                Item = existing;
                TitleText.Text = "编辑快捷方式";
                NameBox.Text = existing.Name;
                TargetBox.Text = existing.Target;
                ArgsBox.Text = existing.Arguments;
                CategoryBox.Text = existing.Category;
            }
        }

        public LaunchItem Item { get; }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var name = NameBox.Text.Trim();
            var target = TargetBox.Text.Trim();

            if (name.Length == 0)
            {
                Ui.Warn("名称不能为空。");
                return;
            }

            if (target.Length == 0)
            {
                Ui.Warn("目标不能为空。");
                return;
            }

            Item.Name = name;
            Item.Target = target;
            Item.Arguments = ArgsBox.Text.Trim();
            Item.Category = string.IsNullOrWhiteSpace(CategoryBox.Text) ? "常用" : CategoryBox.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
