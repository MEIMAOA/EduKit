using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EduKit.Core;

namespace EduKit.Views
{
    public partial class LauncherPage : UserControl
    {
        private sealed class LaunchView
        {
            public LaunchView(LaunchItem item)
            {
                Item = item;
            }

            public LaunchItem Item { get; }

            public string Name => Item.Name;

            public string Target => string.IsNullOrWhiteSpace(Item.Arguments)
                ? Item.Target
                : Item.Target + "  " + Item.Arguments;

            public string Category => Item.Category;

            public string Initial
            {
                get
                {
                    var name = Item.Name;
                    if (string.IsNullOrEmpty(name))
                    {
                        return "?";
                    }

                    return name.Substring(0, 1).ToUpperInvariant();
                }
            }
        }

        private List<LaunchView> _views = new List<LaunchView>();

        public LauncherPage()
        {
            InitializeComponent();
            Loaded += (s, e) => Reload();
        }

        private void Reload(string selectName = null)
        {
            _views = AppData.Launcher.Items
                .OrderBy(i => i.Category)
                .ThenBy(i => i.Name)
                .Select(i => new LaunchView(i))
                .ToList();

            ItemList.ItemsSource = null;
            ItemList.ItemsSource = _views;

            if (selectName != null)
            {
                var target = _views.FirstOrDefault(v => v.Item.Name == selectName);
                if (target != null)
                {
                    ItemList.SelectedItem = target;
                    ItemList.ScrollIntoView(target);
                }
            }
        }

        private LaunchView Selected => ItemList.SelectedItem as LaunchView;

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new LauncherEditWindow(null) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
            {
                AppData.Launcher.Items.Add(dialog.Item);
                AppData.SaveLauncher();
                Reload(dialog.Item.Name);
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected;
            if (selected == null)
            {
                Ui.Warn("请先选择一个快捷方式。");
                return;
            }

            var dialog = new LauncherEditWindow(selected.Item) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
            {
                AppData.SaveLauncher();
                Reload(dialog.Item.Name);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected;
            if (selected == null)
            {
                Ui.Warn("请先选择一个快捷方式。");
                return;
            }

            if (!Ui.Confirm("确定删除「" + selected.Item.Name + "」吗？"))
            {
                return;
            }

            AppData.Launcher.Items.Remove(selected.Item);
            AppData.SaveLauncher();
            Reload();
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            Move(-1);
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            Move(1);
        }

        private void Move(int offset)
        {
            var selected = Selected;
            if (selected == null)
            {
                return;
            }

            var items = AppData.Launcher.Items;
            var index = items.IndexOf(selected.Item);
            var newIndex = index + offset;
            if (index < 0 || newIndex < 0 || newIndex >= items.Count)
            {
                return;
            }

            items.RemoveAt(index);
            items.Insert(newIndex, selected.Item);
            AppData.SaveLauncher();
            Reload(selected.Item.Name);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var selected = Selected;
            if (selected == null)
            {
                Ui.Warn("请先选择一个快捷方式。");
                return;
            }

            Open(selected.Item);
        }

        private void ItemList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var selected = Selected;
            if (selected != null)
            {
                Open(selected.Item);
            }
        }

        private static void Open(LaunchItem item)
        {
            try
            {
                var target = item.Target;
                if (string.IsNullOrWhiteSpace(target))
                {
                    Ui.Warn("这个快捷方式没有设置目标。");
                    return;
                }

                var lower = target.ToLowerInvariant();
                if (lower.StartsWith("http://") || lower.StartsWith("https://"))
                {
                    Native.OpenUrl(target);
                    return;
                }

                if (lower.StartsWith("shell:"))
                {
                    Native.Start(target);
                    return;
                }

                if (!Native.Start(target, string.IsNullOrWhiteSpace(item.Arguments) ? null : item.Arguments))
                {
                    Ui.Error("无法打开：" + target + "\r\n请检查路径是否正确。");
                }
            }
            catch (Exception ex)
            {
                Ui.Error("打开失败：" + ex.Message);
            }
        }
    }
}
