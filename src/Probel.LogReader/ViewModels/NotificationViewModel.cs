﻿using Caliburn.Micro;
using Notifications.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Probel.LogReader.ViewModels
{
    public class NotificationViewModel : PropertyChangedBase
    {
        private readonly INotificationManager _manager;

        public string Title { get; set; }
        public string Message { get; set; }

        public NotificationViewModel(INotificationManager manager)
        {
            _manager = manager;
        }

        public async void Ok()
        {
            await Task.Delay(500);
            _manager.Show(new NotificationContent { Title = "Success!", Message = "Ok button was clicked.", Type = NotificationType.Success });
        }

        public async void Cancel()
        {
            await Task.Delay(500);
            _manager.Show(new NotificationContent { Title = "Error!", Message = "Cancel button was clicked!", Type = NotificationType.Error });
        }
    }
}
