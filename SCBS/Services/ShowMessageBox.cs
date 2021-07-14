using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SCBS.Services
{
    /// <summary>
    /// Shows a popup message box to user
    /// </summary>
    public static class ShowMessageBox
    {
        /// <summary>
        /// Shows a message box to user
        /// </summary>
        /// <param name="message">Message to display to user</param>
        public static void Show(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    MessageBox.Show(Application.Current.MainWindow, message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch
                {
                }
            });
        }
        /// <summary>
        /// Shows a message box to user
        /// </summary>
        /// <param name="message">Message to display to user</param>
        /// <param name="title">Title of the message box</param>
        public static void Show(string message, string title)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch
                {
                }
            });
        }
        /// <summary>
        /// Shows a message box to user
        /// </summary>
        /// <param name="message">Message to display to user</param>
        /// <param name="title">Title of the message box</param>
        /// <param name="button">Button message</param>
        /// <param name="image">message box image</param>
        public static void Show(string message, string title, MessageBoxButton button, MessageBoxImage image)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    MessageBox.Show(Application.Current.MainWindow, message, title, button, image);
                }
                catch
                {
                }
            });
        }
    }
}
